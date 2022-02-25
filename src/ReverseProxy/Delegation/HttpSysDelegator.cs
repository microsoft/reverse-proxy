// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Delegation;

internal sealed class HttpSysDelegator : IHttpSysDelegator, IClusterChangeListener
{
    private readonly IServerDelegationFeature? _serverDelegationFeature;
    private readonly ILogger<HttpSysDelegator> _logger;
    private readonly ConcurrentDictionary<QueueKey, WeakReference<DelegationQueue>> _queues;
    private readonly ConditionalWeakTable<DestinationState, DelegationQueue> _queuesPerDestination;

    public HttpSysDelegator(
            IServer server,
            ILogger<HttpSysDelegator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // IServerDelegationFeature isn't added to DI https://github.com/dotnet/aspnetcore/issues/40043
        // IServerDelegationFeature may not be set if not http.sys server or the OS doesn't support delegation
        _serverDelegationFeature = server.Features?.Get<IServerDelegationFeature>();

        _queues = new ConcurrentDictionary<QueueKey, WeakReference<DelegationQueue>>();
        _queuesPerDestination = new ConditionalWeakTable<DestinationState, DelegationQueue>();
    }

    public void ResetQueue(string queueName, string urlPrefix)
    {
        if (_serverDelegationFeature != null)
        {
            var key = new QueueKey(queueName, urlPrefix);
            if (_queues.TryGetValue(key, out var queueWeakRef) && queueWeakRef.TryGetTarget(out var queue))
            {
                var queueState = queue.Reset(_serverDelegationFeature);
                Log.QueueReset(_logger, queueName, urlPrefix, queueState.InitializationException);
            }
        }
    }

    public void DelegateRequest(HttpContext context, DestinationState destination)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));
        _ = destination ?? throw new ArgumentNullException(nameof(destination));

        var requestDelegationFeature = context.Features.Get<IHttpSysRequestDelegationFeature>()
                    ?? throw new InvalidOperationException($"{typeof(IHttpSysRequestDelegationFeature).FullName} is missing.");

        if (!requestDelegationFeature.CanDelegate)
        {
            throw new InvalidOperationException(
                "Current request can't be delegated. Either the request body has started to be read or the response has started to be sent.");
        }

        if (_serverDelegationFeature is null || !_queuesPerDestination.TryGetValue(destination, out var queue))
        {
            Log.QueueNotFound(_logger, destination);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(ForwarderError.NoAvailableDestinations, ex: null));
            return;
        }

        // Opportunistically retry initialization if it failed previously.
        // This helps when the target queue wasn't yet created because
        // the target process hadn't yet started up.
        if (!queue.TryInitialize(_serverDelegationFeature, out var initializationException))
        {
            Log.QueueNotInitialized(_logger, destination, initializationException);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(ForwarderError.NoAvailableDestinations, initializationException));
            return;
        }

        Log.DelegatingRequest(_logger, destination);
        if (!queue.TryDelegate(_logger, _serverDelegationFeature, requestDelegationFeature, destination, out var delegationException))
        {
            Log.DelegationFailed(_logger, destination, delegationException);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(ForwarderError.Request, delegationException));
        }
    }

    void IClusterChangeListener.OnClusterAdded(ClusterState cluster)
    {
        AddOrUpdateRules(cluster);
    }

    void IClusterChangeListener.OnClusterChanged(ClusterState cluster)
    {
        AddOrUpdateRules(cluster);
        RemoveDeadQueueReferences();
    }

    void IClusterChangeListener.OnClusterRemoved(ClusterState cluster)
    {
        RemoveDeadQueueReferences();
    }

    private void AddOrUpdateRules(ClusterState cluster)
    {
        if (_serverDelegationFeature is null)
        {
            return;
        }

        // We support multiple destinations referencing the same queue but http.sys only
        // allows us to create one handle to the queue, so we keep track of queues two ways.
        // 1. We map destination => queue using a ConditionalWeakTable
        //    This allows us to find the queue to delegate to when processing a request
        // 2. We map (queue name + url prefix) => queue using a WeakReference
        //    This allows us to find the already created queue if more than one destination points to the same queue.
        //    It also allows us to find the queue by name/url prefix to support reset.
        //
        // Using weak references means we ensure the queue exist as long as the referencing destinations exist.
        // Once all the destinations are gone, GC will eventually finalize the underlying SafeHandle to the http.sys
        // queue, which will clean up references in http.sys, allowing us to re-create it again later if needed.
        foreach (var destination in cluster.Destinations.Select(kvp => kvp.Value))
        {
            var queueName = destination.GetHttpSysDelegationQueue();
            var urlPrefix = destination.Model?.Config?.Address;
            if (queueName is not null && urlPrefix is not null)
            {
                var queueKey = new QueueKey(queueName, urlPrefix);
                if (!_queuesPerDestination.TryGetValue(destination, out var queue) || !queue.Equals(queueKey))
                {
                    if (!_queues.TryGetValue(queueKey, out var queueWeakRef) || !queueWeakRef.TryGetTarget(out queue))
                    {
                        // Either the queue hasn't been created or it has been cleaned up.
                        // Create a new one, and try to add it if someone else didn't beat us to it.
                        queue = new DelegationQueue(queueName, urlPrefix);
                        queueWeakRef = new WeakReference<DelegationQueue>(queue);
                        queueWeakRef = _queues.AddOrUpdate(
                            queueKey,
                            (key, newValue) => newValue,
                            (key, value, newValue) => value.TryGetTarget(out _) ? value : newValue,
                            queueWeakRef);
                        queueWeakRef.TryGetTarget(out queue);
                    }

                    if (queue is not null)
                    {
                        // We call this outside of the above if bock so that if previous
                        // initialization failed, we will retry it for every new destination added.
                        if (!queue.TryInitialize(_serverDelegationFeature, out var initializationException))
                        {
                            Log.QueueInitFailed(
                                    _logger,
                                    destination.DestinationId,
                                    queueName,
                                    urlPrefix,
                                    initializationException);
                        }

                        _queuesPerDestination.AddOrUpdate(destination, queue);
                    }
                    else
                    {
                        // This should never happen because we always create a new one above
                        _queuesPerDestination.Remove(destination);
                        Log.QueueInitFailed(
                            _logger,
                            destination.DestinationId,
                            queueName,
                            urlPrefix,
                            new Exception("Delegation queue is null after adding a new one. This shouldn't happen."));
                    }
                }
            }
            else
            {
                // Handles the case a destination switches from delegation to proxy
                _queuesPerDestination.Remove(destination);
            }
        }
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
        public const uint ERROR_FILE_NOT_FOUND = 2;
        public const uint ERROR_INVALID_PARAMETER = 87;

        private readonly QueueKey _queueKey;
        private readonly object _syncRoot;
        private readonly ManualResetEventSlim _ruleResetEvent;
        private readonly AtomicCounter _delegatingCount;

        private DelegationQueueState _currentState;

        public DelegationQueue(string queueName, string urlPrefix)
        {
            _queueKey = new QueueKey(queueName, urlPrefix);
            _syncRoot = new object();
            _ruleResetEvent = new ManualResetEventSlim(initialState: true);
            _currentState = new DelegationQueueState();
            _delegatingCount = new AtomicCounter();
        }

        public bool TryDelegate(
            ILogger logger,
            IServerDelegationFeature serverDelegationFeature,
            IHttpSysRequestDelegationFeature requestDelegationFeature,
            DestinationState destination,
            [NotNullWhen(false)] out Exception? delegationException)
        {
            var state = _currentState;
            var delegated = false;
            delegationException = state.InitializationException;

            if (state.IsInitialized)
            {
                delegated = TryDelegateInternal(requestDelegationFeature, state.Rule, _ruleResetEvent, _delegatingCount, out delegationException);
                if (!delegated && ShouldTryToReset(delegationException, destination))
                {
                    state = Reset(serverDelegationFeature, state);

                    Log.QueueReset(logger, destination, state.InitializationException);

                    if (state.IsInitialized)
                    {
                        delegated = TryDelegateInternal(requestDelegationFeature, state.Rule, _ruleResetEvent, _delegatingCount, out delegationException);
                    }
                }
            }

            return delegated;

            static bool TryDelegateInternal(
                IHttpSysRequestDelegationFeature requestDelegationFeature,
                DelegationRule rule,
                ManualResetEventSlim resetEvent,
                AtomicCounter delegatingCount,
                [NotNullWhen(false)] out Exception? delegateException)
            {
                try
                {
                    // This ensures we don't dispose/reset the while a request is trying to delegate
                    var ready = false;
                    do
                    {
                        resetEvent.Wait();
                        delegatingCount.Increment();

                        ready = resetEvent.IsSet;
                        if (!ready)
                        {
                            // The event was reset before we could increment the counter, start over
                            delegatingCount.Decrement();
                        }

                    } while (!ready);

                    requestDelegationFeature.DelegateRequest(rule);

                    delegateException = null;
                    return true;
                }
                catch (Exception ex)
                {
                    delegateException = ex;
                    return false;
                }
                finally
                {
                    delegatingCount.Decrement();
                }
            }
        }

        public bool TryInitialize(IServerDelegationFeature delegationFeature, [NotNullWhen(false)] out Exception? initializationException)
        {
            var state = _currentState;
            if (!state.IsInitialized && ShouldRetryInitialization(state.InitializationException))
            {
                lock (_syncRoot)
                {
                    state = _currentState;
                    if (!state.IsInitialized && ShouldRetryInitialization(state.InitializationException))
                    {
                        try
                        {
                            state = new DelegationQueueState(delegationFeature.CreateDelegationRule(_queueKey.QueueName, _queueKey.UrlPrefix));
                        }
                        catch (Exception ex)
                        {
                            state = new DelegationQueueState(ex);
                        }

                        _currentState = state;
                    }
                }
            }

            initializationException = state.InitializationException;
            return state.IsInitialized;
        }

        public DelegationQueueState Reset(IServerDelegationFeature delegationFeature)
        {
            return Reset(delegationFeature, _currentState);
        }

        public bool Equals(QueueKey queueKey)
        {
            return _queueKey.Equals(queueKey);
        }

        private DelegationQueueState Reset(IServerDelegationFeature delegationFeature, DelegationQueueState state)
        {
            // To prevent multiple request from resetting the state multiple times,
            // we check to make sure the state hasn't changed since they got it.
            // This ensures we only reset once due to failing requests.
            if (_currentState.IsInitialized && _currentState == state)
            {
                lock (_syncRoot)
                {
                    if (_currentState == state)
                    {
                        try
                        {
                            _ruleResetEvent.Reset();
                            if (_delegatingCount.Value > 0)
                            {
                                // Requests are in the process of delegating, wait until they are done.
                                // Since delegation is quick, just spin instead of sleep.
                                var spin = new SpinWait();
                                do
                                {
                                    spin.SpinOnce();
                                } while (_delegatingCount.Value > 0);
                            }

                            state.Rule?.Dispose();
                            state = new DelegationQueueState(delegationFeature.CreateDelegationRule(_queueKey.QueueName, _queueKey.UrlPrefix));
                        }
                        catch (Exception ex)
                        {
                            state = new DelegationQueueState(ex);
                        }
                        finally
                        {
                            _currentState = state;
                            _ruleResetEvent.Set();
                        }

                        return state;
                    }
                }
            }

            return _currentState;
        }

        private static bool ShouldRetryInitialization(Exception? exception)
        {
            return exception switch
            {
                null => true,
                HttpSysException httpSysEx when httpSysEx.ErrorCode == ERROR_FILE_NOT_FOUND => true,
                _ => false,
            };
        }

        private static bool ShouldTryToReset(Exception? delegateException, DestinationState destination)
        {
            return delegateException switch
            {
                // Work around for https://github.com/dotnet/aspnetcore/issues/40358
                HttpSysException ex when ex.ErrorCode == ERROR_INVALID_PARAMETER => true,

                // Receiver has shut down or detached from the queue
                HttpSysException ex when ex.ErrorCode == ERROR_FILE_NOT_FOUND && destination.ShouldResetDelegationQueueOnErrorNotFound() => true,

                _ => false,
            };
        }
    }

    private class DelegationQueueState
    {
        public DelegationQueueState()
        {
            IsInitialized = false;
        }

        public DelegationQueueState(DelegationRule rule)
        {
            IsInitialized = true;
            Rule = rule;
        }

        public DelegationQueueState(Exception ex)
        {
            IsInitialized = false;
            InitializationException = ex;
        }

        [MemberNotNullWhen(true, nameof(Rule))]
        public bool IsInitialized { get; }

        public DelegationRule? Rule { get; }

        public Exception? InitializationException { get; }
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
            EventIds.DelegationQueueInitializationFailed,
            "Failed to initialize queue for destination '{destinationId}' with queue name '{queueName}' and url prefix '{urlPrefix}'.");

        private static readonly Action<ILogger, string, string?, string?, Exception?> _queueNotFound = LoggerMessage.Define<string, string?, string?>(
            LogLevel.Warning,
            EventIds.DelegationQueueNotFound,
            "Failed to get delegation queue for destination '{destinationId}' with queue name '{queueName}' and url prefix '{urlPrefix}'");

        private static readonly Action<ILogger, string, string?, string?, Exception?> _queueNotInitialized = LoggerMessage.Define<string, string?, string?>(
            LogLevel.Information,
            EventIds.DelegationQueueNotInitialized,
            "Delegation queue not initialized for destination '{destinationId}' with queue '{queueName}' and url prefix '{urlPrefix}'.");

        private static readonly Action<ILogger, string?, string?, Exception?> _queueReset = LoggerMessage.Define<string?, string?>(
            LogLevel.Information,
            EventIds.DelegationQueueReset,
            "Reset queue with name '{queueName}' and url prefix '{urlPrefix}'");

        private static readonly Action<ILogger, string, string?, string?, Exception?> _delegatingRequest = LoggerMessage.Define<string, string?, string?>(
            LogLevel.Information,
            EventIds.DelegatingRequest,
            "Delegating to destination '{destinationId}' with queue '{queueName}' and url prefix '{urlPrefix}'");

        private static readonly Action<ILogger, string, string?, string?, Exception?> _delegationFailed = LoggerMessage.Define<string, string?, string?>(
            LogLevel.Error,
            EventIds.DelegationFailed,
            "Failed to delegate request for destination '{destinationId}' with queue name '{queueName}' and url prefix '{urlPrefix}'");

        public static void QueueInitFailed(ILogger logger, string destinationId, string queueName, string urlPrefix, Exception? ex)
        {
            _queueInitFailed(logger, destinationId, queueName, urlPrefix, ex);
        }

        public static void QueueNotFound(ILogger logger, DestinationState destination)
        {
            _queueNotFound(logger, destination.DestinationId, destination.GetHttpSysDelegationQueue(), destination.Model?.Config?.Address, null);
        }

        public static void QueueNotInitialized(ILogger logger, DestinationState destination, Exception? ex)
        {
            _queueNotInitialized(logger, destination.DestinationId, destination.GetHttpSysDelegationQueue(), destination.Model?.Config?.Address, ex);
        }

        public static void QueueReset(ILogger logger, string queueName, string urlPrefix, Exception? ex)
        {
            _queueReset(logger, queueName, urlPrefix, ex);
        }

        public static void QueueReset(ILogger logger, DestinationState destination, Exception? ex)
        {
            _queueReset(logger, destination.GetHttpSysDelegationQueue(), destination.Model?.Config?.Address, ex);
        }

        public static void DelegatingRequest(ILogger logger, DestinationState destination)
        {
            _delegatingRequest(logger, destination.DestinationId, destination.GetHttpSysDelegationQueue(), destination.Model?.Config?.Address, null);
        }

        public static void DelegationFailed(ILogger logger, DestinationState destination, Exception ex)
        {
            _delegationFailed(logger, destination.DestinationId, destination.GetHttpSysDelegationQueue(), destination.Model?.Config?.Address, ex);
        }
    }
}
#endif
