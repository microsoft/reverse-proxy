// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Delegation;

internal sealed class HttpSysDelegator : IClusterChangeListener
{
    private readonly IServerDelegationFeature? _delegationFeature;
    private readonly ILogger<HttpSysDelegator> _logger;
    private readonly ConcurrentDictionary<QueueKey, WeakReference<DelegationQueue>> _queues;
    private readonly ConditionalWeakTable<DestinationState, DelegationQueue> _queuesPerDestination;

    public HttpSysDelegator(
            IServerDelegationFeature? delegationFeature,
            ILogger<HttpSysDelegator> logger)
    {
        _delegationFeature = delegationFeature;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _queues = new ConcurrentDictionary<QueueKey, WeakReference<DelegationQueue>>();
        _queuesPerDestination = new ConditionalWeakTable<DestinationState, DelegationQueue>();
    }

    public void DelegateRequest(HttpContext context, DestinationState destination)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));
        _ = destination ?? throw new ArgumentNullException(nameof(destination));

        var delegationFeature = context.Features.Get<IHttpSysRequestDelegationFeature>()
                    ?? throw new InvalidOperationException($"{typeof(IHttpSysRequestDelegationFeature).FullName} is missing.");

        if (!delegationFeature.CanDelegate)
        {
            throw new InvalidOperationException(
                "Current request can't be delegated. Either the request body has started to be read or the response has started to be sent.");
        }

        _queuesPerDestination.TryGetValue(destination, out var queue);
        if (queue == null)
        {
            Log.QueueNotFound(_logger, destination);
        }
        else if (!queue.IsInitialized && _delegationFeature != null)
        {
            // Opportunistically retry initialization if it failed previously.
            // This helps when the target queue wasn't yet created because
            // the target process hadn't yet started up.
            queue.TryInitialize(_delegationFeature);
        }


        if (queue == null || !queue.IsInitialized)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(ForwarderError.NoAvailableDestinations, queue?.InitializationException));
            return;
        }

        try
        {
            Log.DelegatingRequest(_logger, destination);
            delegationFeature.DelegateRequest(queue.DelegationRule!);
        }
        catch (Exception ex)
        {
            Log.DelegationFailed(_logger, destination, ex);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(ForwarderError.Request, ex));
        }
    }

    void IClusterChangeListener.OnClusterAdded(ClusterState cluster)
    {
        AddOrUpdateRules(cluster);
    }

    void IClusterChangeListener.OnClusterChanged(ClusterState cluster)
    {
        AddOrUpdateRules(cluster);
    }

    void IClusterChangeListener.OnClusterRemoved(ClusterState cluster)
    {
        RemoveDeadQueueReferences();
    }

    private void AddOrUpdateRules(ClusterState cluster)
    {
        if (_delegationFeature == null)
        {
            return;
        }

        // We support multiple destinations referencing the same queue but http.sys only
        // allows us to create one handle to the queue, so we keep track of queues two ways.
        // 1. We map destination => queue using a ConditionalWeakTable
        //    This allows us to find the queue to delegate to when processing a request
        // 2. We map (queue name + url prefix) => queue using a WeakReference
        //    This allows us to find the already created queue if more than one destination points to the same queue.
        //
        // Using weak references means we ensure the queue exist as long as the referencing destinations exist.
        // Once all the destinations are gone, GC will eventually finalize the underlying SafeHandle to the http.sys
        // queue, which will clean up references in http.sys, allowing us to re-create it again later if needed.
        foreach (var destination in cluster.Destinations.Select(kvp => kvp.Value))
        {
            var queueName = destination.GetHttpSysDelegationQueue();
            var urlPrefix = destination.Model?.Config?.Address;
            if (queueName != null && urlPrefix != null)
            {
                if (!_queuesPerDestination.TryGetValue(destination, out var queue))
                {
                    var queueKey = new QueueKey(queueName, urlPrefix);
                    if (!_queues.TryGetValue(queueKey, out var queueWeakRef) || !queueWeakRef.TryGetTarget(out queue))
                    {
                        // Either the queue hasn't been created or it has been cleaned up.
                        // Create a new one, and try to add it if someone else didn't beat us to it.
                        queue = new DelegationQueue(queueName, urlPrefix);
                        queueWeakRef = new WeakReference<DelegationQueue>(queue);
                        _queues.AddOrUpdate(
                            queueKey,
                            (key, newValue) => newValue,
                            (key, value, newValue) => value.TryGetTarget(out _) ? value : newValue,
                            queueWeakRef);
                    }

                    try
                    {
                        // We call this outside of the above if bock so that if previous
                        // initialization failed, we will retry it for every new destination added.
                        queue.Initialize(_delegationFeature);
                    }
                    catch (Exception ex)
                    {
                        Log.QueueInitFailed(
                                _logger,
                                destination.DestinationId,
                                queueName,
                                urlPrefix,
                                ex);
                    }

                    _queuesPerDestination.AddOrUpdate(destination, queue);
                }
            }
        }

        RemoveDeadQueueReferences();
    }

    private void RemoveDeadQueueReferences()
    {
        foreach (var kvp in _queues)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                _queues.TryRemove(kvp);
            }
        }
    }

    private class DelegationQueue
    {
        private const int ERROR_FILE_NOT_FOUND = 2;

        private readonly string _queueName;
        private readonly string _urlPrefix;
        private readonly object _syncRoot;

        public DelegationQueue(string queueName, string urlPrefix)
        {
            _queueName = queueName;
            _urlPrefix = urlPrefix;
            _syncRoot = new object();
        }

        public bool IsInitialized { get; private set; }

        public DelegationRule? DelegationRule { get; private set; }

        public Exception? InitializationException { get; private set; }

        public void Initialize(IServerDelegationFeature delegationFeature)
        {
            if (!IsInitialized && ShouldRetryInitialization())
            {
                lock (_syncRoot)
                {
                    if (!IsInitialized && ShouldRetryInitialization())
                    {
                        try
                        {
                            InitializationException = null;
                            DelegationRule = delegationFeature.CreateDelegationRule(_queueName, _urlPrefix);
                            IsInitialized = true;
                        }
                        catch (Exception ex)
                        {
                            InitializationException = ex;
                            throw;
                        }
                    }
                }
            }
        }

        public bool TryInitialize(IServerDelegationFeature delegationFeature)
        {
            try
            {
                Initialize(delegationFeature);
                return true;
            }
            catch (Exception) { }

            return false;
        }

        private bool ShouldRetryInitialization()
        {
            return InitializationException switch
            {
                null => true,
                HttpSysException httpSysEx when httpSysEx.ErrorCode == ERROR_FILE_NOT_FOUND => true,
                _ => false,
            };
        }
    }

    private readonly struct QueueKey : IEquatable<QueueKey>
    {
        private readonly int _hashCode;

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
        private static readonly Action<ILogger, string, string, string, Exception?> _queueInitFailed = LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            EventIds.MultipleDestinationsAvailable,
            "Failed to initialize queue for destination '{destinationId}' with queue name '{queueName}' and url prefix '{urlPrefix}'.");

        private static readonly Action<ILogger, string, string?, string?, Exception?> _delegatingRequest = LoggerMessage.Define<string, string?, string?>(
            LogLevel.Information,
            EventIds.DelegatingRequest,
            "Delegating to destination '{destinationId}' with queue '{queueName}' and url prefix '{urlPrefix}'");

        private static readonly Action<ILogger, string, string?, string?, Exception?> _queueNotFound = LoggerMessage.Define<string, string?, string?>(
            LogLevel.Warning,
            EventIds.DelegationRuleNotFound,
            "Failed to get delegation queue for destination '{destinationId}' with queue name '{queueName}' and url prefix '{urlPrefix}'");

        private static readonly Action<ILogger, string, string?, string?, Exception?> _delegationFailed = LoggerMessage.Define<string, string?, string?>(
            LogLevel.Error,
            EventIds.DelegationFailed,
            "Failed to delegate request for destination '{destinationId}' with queue name '{queueName}' and url prefix '{urlPrefix}'");

        public static void QueueInitFailed(ILogger logger, string destinationId, string queueName, string urlPrefix, Exception ex)
        {
            _queueInitFailed(logger, destinationId, queueName, urlPrefix, ex);
        }

        public static void DelegatingRequest(ILogger logger, DestinationState destination)
        {
            _delegatingRequest(logger, destination.DestinationId, destination.GetHttpSysDelegationQueue(), destination.Model?.Config?.Address, null);
        }

        public static void QueueNotFound(ILogger logger, DestinationState destination)
        {
            _queueNotFound(logger, destination.DestinationId, destination.GetHttpSysDelegationQueue(), destination.Model?.Config?.Address, null);
        }

        public static void DelegationFailed(ILogger logger, DestinationState destination, Exception ex)
        {
            _delegationFailed(logger, destination.DestinationId, destination.GetHttpSysDelegationQueue(), destination.Model?.Config?.Address, ex);
        }
    }
}
#endif
