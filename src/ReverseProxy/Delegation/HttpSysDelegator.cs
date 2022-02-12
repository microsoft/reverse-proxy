// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Delegation;

internal sealed class HttpSysDelegator : IClusterChangeListener, IDisposable
{
    private readonly IServerDelegationFeature? _delegationFeature;
    private readonly ILogger<HttpSysDelegator> _logger;
    private readonly ConcurrentDictionary<QueueKey, DelegationQueue> _queues;
    private readonly ConcurrentDictionary<string, HashSet<QueueKey>> _queuesPerCluster;

    private bool _disposed;

    public HttpSysDelegator(
            IServerDelegationFeature? delegationFeature,
            ILogger<HttpSysDelegator> logger)
    {
        _delegationFeature = delegationFeature;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _queues = new ConcurrentDictionary<QueueKey, DelegationQueue>();
        _queuesPerCluster = new ConcurrentDictionary<string, HashSet<QueueKey>>(StringComparer.OrdinalIgnoreCase);
    }

    public void DelegateRequest(HttpContext context, string queueName, string urlPrefix)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));
        _ = queueName ?? throw new ArgumentNullException(nameof(queueName));
        _ = urlPrefix ?? throw new ArgumentNullException(nameof(urlPrefix));

        var delegationFeature = context.Features.Get<IHttpSysRequestDelegationFeature>()
                    ?? throw new InvalidOperationException($"{typeof(IHttpSysRequestDelegationFeature).FullName} is missing.");

        if (!delegationFeature.CanDelegate)
        {
            throw new InvalidOperationException(
                "Current request can't be delegated. Either the request body has started to be read or the response has started to be sent.");
        }

        if (!_queues.TryGetValue(new QueueKey(queueName, urlPrefix), out var queue))
        {
            Log.QueueNotFound(_logger, queueName, urlPrefix);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(ForwarderError.NoAvailableDestinations, ex: null));
            return;
        }

        try
        {
            Log.DelegatingRequest(_logger, queueName);
            queue.DelegateRequest(delegationFeature);
        }
        catch (Exception ex)
        {
            Log.DelegationFailed(_logger, queueName, urlPrefix, ex);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(ForwarderError.Request, ex));
        }
    }

    void IClusterChangeListener.OnClusterAdded(ClusterState cluster)
    {
        if (!_disposed)
        {
            AddOrUpdateRules(cluster);
        }
    }

    void IClusterChangeListener.OnClusterChanged(ClusterState cluster)
    {
        if (!_disposed)
        {
            AddOrUpdateRules(cluster);
        }
    }

    void IClusterChangeListener.OnClusterRemoved(ClusterState cluster)
    {
        if (!_disposed)
        {
            RemoveRules(cluster.ClusterId);
        }
    }

    void IDisposable.Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            foreach (var entry in _queuesPerCluster)
            {
                RemoveRules(entry.Key);
            }
        }
    }

    private void AddOrUpdateRules(ClusterState cluster)
    {
        if (_delegationFeature == null)
        {
            return;
        }

        var incomingQueues = cluster.Destinations
            .Where(kvp => kvp.Value.ShouldUseHttpSysDelegation())
            .Select(kvp => new QueueKey(kvp.Value))
            .ToHashSet();

        if (!incomingQueues.Any() && !_queuesPerCluster.ContainsKey(cluster.ClusterId))
        {
            // No new or existing rules
            return;
        }

        var currentQueues = _queuesPerCluster.GetOrAdd(
            cluster.ClusterId,
            (key, size) => new HashSet<QueueKey>(size),
            incomingQueues.Count);

        lock (currentQueues)
        {
            currentQueues.RemoveWhere(queue =>
            {
                if (!incomingQueues.Contains(queue))
                {
                    RemoveRule(queue);
                    return true;
                }

                return false;
            });

            foreach (var incomingQueue in incomingQueues)
            {
                if (!currentQueues.Contains(incomingQueue))
                {
                    var queue = _queues.GetOrAdd(incomingQueue, queue => new DelegationQueue(queue.QueueName, queue.UrlPrefix));
                    lock (queue)
                    {
                        // There is a small chance the queue was removed from the dictionary before we got the lock
                        _queues.TryAdd(incomingQueue, queue);

                        try
                        {
                            queue.Initialize(_delegationFeature);
                        }
                        catch (Exception ex)
                        {
                            Log.CreateDelegationRuleFailed(
                                _logger,
                                incomingQueue.QueueName,
                                incomingQueue.UrlPrefix,
                                ex);
                        }
                    }

                    currentQueues.Add(incomingQueue);
                }
            }
        }
    }

    private void RemoveRules(string clusterId)
    {
        if (_queuesPerCluster.TryRemove(clusterId, out var currentRules))
        {
            lock (currentRules)
            {
                foreach (var ruleKey in currentRules)
                {
                    RemoveRule(ruleKey);
                }
            }
        }
    }

    private void RemoveRule(QueueKey queueKey)
    {
        var queue = _queues.GetValueOrDefault(queueKey);
        if (queue != null)
        {
            lock (queue)
            {
                queue.Release();
                if (queue.RefCount == 0)
                {
                    _queues.TryRemove(queueKey, out _);
                }
            }
        }
    }

    private class DelegationQueue
    {
        private readonly string _queueName;
        private readonly string _urlPrefix;

        private DelegationRule? _rule;
        private Exception? _createRuleError;

        public DelegationQueue(string queueName, string urlPrefix)
        {
            _queueName = queueName;
            _urlPrefix = urlPrefix;
            RefCount = 0;
        }

        public int RefCount { get; private set; }

        public DelegationRule? DelegationRule { get; set; }

        public void DelegateRequest(IHttpSysRequestDelegationFeature delegator)
        {
            if (_createRuleError != null)
            {
                ExceptionDispatchInfo.Capture(_createRuleError).Throw();
            }

            // There is a possible race condition here where a request is trying to
            // delegate at the same the queue is being removed from routing config.
            // So it's possible _rule is null. It's also possible it is not null but it gets
            // disposed before the request is delegated which throws an ObjectDisposedException.
            var rule = _rule;
            if (rule == null)
            {
                throw new ObjectDisposedException($"{nameof(DelegationRule)} has been disposed");
            }

            delegator.DelegateRequest(rule);
        }

        public void Initialize(IServerDelegationFeature delegationFeature)
        {
            RefCount++;
            if (RefCount == 1)
            {
                try
                {
                    _createRuleError = null;
                    _rule = delegationFeature.CreateDelegationRule(_queueName, _urlPrefix);
                }
                catch (Exception ex)
                {
                    _createRuleError = ex;
                    throw;
                }
            }
        }

        public void Release()
        {
            RefCount--;
            if (RefCount == 0)
            {
                _rule?.Dispose();
                _rule = null;
            }
        }
    }

    private readonly struct QueueKey : IEquatable<QueueKey>
    {
        private readonly int _hashCode;

        public QueueKey(DestinationState destination)
            : this(destination.GetHttpSysDelegationQueue()!, destination.Model.Config.Address)
        {
        }

        public QueueKey(string queueName, string urlPrefix)
        {
            QueueName = queueName;
            UrlPrefix = urlPrefix;
            _hashCode = HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(queueName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(urlPrefix));
        }

        public string QueueName { get; }

        public string UrlPrefix { get; }

        public bool Equals(QueueKey other)
        {
            return _hashCode == other._hashCode
                && string.Equals(QueueName, other.QueueName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(UrlPrefix, other.UrlPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is QueueKey other && Equals(other);
        }

        public override int GetHashCode() => _hashCode;
    }


    private static class Log
    {
        private static readonly Action<ILogger, string, string, Exception?> _createDelegationRuleFailed = LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            EventIds.MultipleDestinationsAvailable,
            "Failed to create rule for queue name '{queueName}' and url prefix '{urlPrefix}'.");

        private static readonly Action<ILogger, string, Exception?> _delegatingRequest = LoggerMessage.Define<string>(
            LogLevel.Information,
            EventIds.DelegatingRequest,
            "Delegating to queue '{queueName}'");

        private static readonly Action<ILogger, string, string, Exception?> _queueNotFound = LoggerMessage.Define<string, string>(
            LogLevel.Error,
            EventIds.DelegationRuleNotFound,
            "Failed to get delegation rule for queue name '{queueName}' and url prefix '{urlPrefix}'");

        private static readonly Action<ILogger, string, string, Exception?> _delegationFailed = LoggerMessage.Define<string, string>(
            LogLevel.Error,
            EventIds.DelegationFailed,
            "Failed to delegate request for queue name '{queueName}' and url prefix '{urlPrefix}'");

        public static void CreateDelegationRuleFailed(ILogger logger, string queueName, string urlPrefix, Exception ex)
        {
            _createDelegationRuleFailed(logger, queueName, urlPrefix, ex);
        }

        public static void DelegatingRequest(ILogger logger, string queueName)
        {
            _delegatingRequest(logger, queueName, null);
        }

        public static void QueueNotFound(ILogger logger, string queueName, string urlPrefix)
        {
            _queueNotFound(logger, queueName, urlPrefix, null);
        }

        public static void DelegationFailed(ILogger logger, string queueName, string urlPrefix, Exception ex)
        {
            _delegationFailed(logger, queueName, urlPrefix, ex);
        }
    }
}
#endif
