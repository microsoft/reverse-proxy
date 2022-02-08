// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Delegation;

internal sealed class HttpSysDelegationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpSysDelegationMiddleware> _logger;
    private readonly IHttpSysDelegationRuleManager _delegationRuleManager;
    private readonly IRandomFactory _randomFactory;

    public HttpSysDelegationMiddleware(
        RequestDelegate next,
        ILogger<HttpSysDelegationMiddleware> logger,
        IHttpSysDelegationRuleManager delegationRuleManager,
        IRandomFactory randomFactory)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _delegationRuleManager = delegationRuleManager ?? throw new ArgumentNullException(nameof(delegationRuleManager));
        _randomFactory = randomFactory ?? throw new ArgumentNullException(nameof(randomFactory));
    }

    public Task Invoke(HttpContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));
        var reverseProxyFeature = context.GetReverseProxyFeature();
        var destinations = reverseProxyFeature.AvailableDestinations
            ?? throw new InvalidOperationException($"The {nameof(IReverseProxyFeature)} Destinations collection was not set.");
        var cluster = reverseProxyFeature.Cluster
            ?? throw new InvalidOperationException($"The {nameof(IReverseProxyFeature)} Cluster was not set.");

        if (destinations.Any())
        {
            // This logic mimics behavior in ForwarderMiddleware, except we save the chosen destination back
            // to the proxy feature to ensure a delegation destination doesn't slip past this middleware.
            var destination = destinations[0];
            if (destinations.Count > 1)
            {
                var random = _randomFactory.CreateRandomInstance();
                Log.MultipleDestinationsAvailable(_logger, reverseProxyFeature.Cluster.Config.ClusterId);
                destination = destinations[random.Next(destinations.Count)];
                reverseProxyFeature.AvailableDestinations = destination;
            }

            if (destination.ShouldUseHttpSysQueueDelegation())
            {
                reverseProxyFeature.ProxiedDestination = destination;

                var delegationFeature = context.Features.Get<IHttpSysRequestDelegationFeature>()
                    ?? throw new InvalidOperationException($"{typeof(IHttpSysRequestDelegationFeature).FullName} is missing.");

                if (!delegationFeature.CanDelegate)
                {
                    throw new InvalidOperationException(
                        "Current request can't be delegated. Either the request body has started to be read or the response has started to be sent.");
                }

                if (!_delegationRuleManager.TryGetDelegationRule(destination, out var delegationRule))
                {
                    Log.DelegationRuleNotFound(_logger, cluster.Config.ClusterId, destination.DestinationId);
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(ForwarderError.HttpSysDelegationRuleNotFound, ex: null));
                    return Task.CompletedTask;
                }

                try
                {
                    Log.DelegatingRequest(_logger, delegationRule?.QueueName);
                    delegationFeature.DelegateRequest(delegationRule!);
                }
                catch (Exception ex)
                {
                    Log.DelegationFailed(_logger, cluster.Config.ClusterId, destination.DestinationId, ex);
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    context.Features.Set<IForwarderErrorFeature>(new ForwarderErrorFeature(ForwarderError.HttpSysDelegationFailed, ex));
                }

                return Task.CompletedTask;
            }
        }

        return _next(context);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _multipleDestinationsAvailable = LoggerMessage.Define<string>(
            LogLevel.Warning,
            EventIds.MultipleDestinationsAvailable,
            "More than one destination available for cluster '{clusterId}', load balancing may not be configured correctly. Choosing randomly.");

        private static readonly Action<ILogger, string?, Exception?> _delegatingRequest = LoggerMessage.Define<string?>(
            LogLevel.Information,
            EventIds.DelegatingRequest,
            "Delegating to queue '{queueName}'");

        private static readonly Action<ILogger, string, string, Exception?> _delegationRuleNotFound = LoggerMessage.Define<string, string>(
            LogLevel.Error,
            EventIds.DelegationRuleNotFound,
            "Failed to get delegation rule for cluster '{clusterId}' and destination '{destinationId}'");

        private static readonly Action<ILogger, string, string, Exception?> _delegationFailed = LoggerMessage.Define<string, string>(
            LogLevel.Error,
            EventIds.DelegationFailed,
            "Failed to delegate request for cluster '{clusterId}' and destination '{destinationId}'");

        public static void MultipleDestinationsAvailable(ILogger logger, string clusterId)
        {
            _multipleDestinationsAvailable(logger, clusterId, null);
        }

        public static void DelegatingRequest(ILogger logger, string? queueName)
        {
            _delegatingRequest(logger, queueName, null);
        }

        public static void DelegationRuleNotFound(ILogger logger, string  clusterId, string destinationId)
        {
            _delegationRuleNotFound(logger, clusterId, destinationId, null);
        }

        public static void DelegationFailed(ILogger logger, string clusterId, string destinationId, Exception ex)
        {
            _delegationFailed(logger, clusterId, destinationId, ex);
        }
    }
}
#endif
