// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.SessionAffinity;

internal sealed class HashCookieSessionAffinityPolicy : ISessionAffinityPolicy
{
    private static readonly object AffinityKeyId = new();
    private readonly ConditionalWeakTable<DestinationState, string> _hashes = new();
    private readonly ILogger _logger;
    private readonly IClock _clock;

    public HashCookieSessionAffinityPolicy(
        IClock clock,
        ILogger<HashCookieSessionAffinityPolicy> logger)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => SessionAffinityConstants.Policies.HashCookie;

    public void AffinitizeResponse(HttpContext context, ClusterState cluster, SessionAffinityConfig config, DestinationState destination)
    {
        if (!config.Enabled.GetValueOrDefault())
        {
            throw new InvalidOperationException($"Session affinity is disabled for cluster.");
        }

        // Affinity key is set on the response only if it's a new affinity.
        if (!context.Items.ContainsKey(AffinityKeyId))
        {
            var affinityKey = GetDestinationHash(destination);

            // Nothing is written to the response
            var affinityCookieOptions = new CookieOptions
            {
                Path = config.Cookie?.Path ?? "/",
                SameSite = config.Cookie?.SameSite ?? SameSiteMode.Unspecified,
                HttpOnly = config.Cookie?.HttpOnly ?? true,
                MaxAge = config.Cookie?.MaxAge,
                Domain = config.Cookie?.Domain,
                IsEssential = config.Cookie?.IsEssential ?? false,
                Secure = config.Cookie?.SecurePolicy == CookieSecurePolicy.Always || (config.Cookie?.SecurePolicy == CookieSecurePolicy.SameAsRequest && context.Request.IsHttps),
                Expires = config.Cookie?.Expiration is not null ? _clock.GetUtcNow().Add(config.Cookie.Expiration.Value) : default(DateTimeOffset?),
            };

            context.Response.Cookies.Append(config.AffinityKeyName, affinityKey, affinityCookieOptions);
        }
    }

    public AffinityResult FindAffinitizedDestinations(HttpContext context, ClusterState cluster, SessionAffinityConfig config, IReadOnlyList<DestinationState> destinations)
    {
        if (!config.Enabled.GetValueOrDefault())
        {
            throw new InvalidOperationException($"Session affinity is disabled for cluster {cluster.ClusterId}.");
        }

        var affinityHash = context.Request.Cookies[config.AffinityKeyName];
        if (affinityHash is null)
        {
            return new(null, AffinityStatus.AffinityKeyNotSet);
        }

        foreach (var d in destinations)
        {
            var hashValue = GetDestinationHash(d);

            if (affinityHash == hashValue)
            {
                context.Items[AffinityKeyId] = affinityHash;
                return new(d, AffinityStatus.OK);
            }
        }

        if (destinations.Count == 0)
        {
            Log.AffinityCannotBeEstablishedBecauseNoDestinationsFound(_logger, cluster.ClusterId);
        }
        else
        {
            Log.DestinationMatchingToAffinityKeyNotFound(_logger, cluster.ClusterId);
        }

        return new(null, AffinityStatus.DestinationNotFound);
    }

    private string GetDestinationHash(DestinationState d)
    {
        return _hashes.GetValue(d, static d =>
        {
            // Matches the format used by ARR
            var destinationIdBytes = Encoding.Unicode.GetBytes(d.DestinationId.ToLowerInvariant());
            var hashBytes = SHA256.HashData(destinationIdBytes);
            return Convert.ToHexString(hashBytes);
        });
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> _affinityCannotBeEstablishedBecauseNoDestinationsFound = LoggerMessage.Define<string>(
            LogLevel.Warning,
            EventIds.AffinityCannotBeEstablishedBecauseNoDestinationsFoundOnCluster,
            "The request affinity cannot be established because no destinations are found on cluster `{clusterId}`.");

        private static readonly Action<ILogger, string, Exception?> _destinationMatchingToAffinityKeyNotFound = LoggerMessage.Define<string>(
            LogLevel.Warning,
            EventIds.DestinationMatchingToAffinityKeyNotFound,
            "Destination matching to the request affinity key is not found on cluster `{backnedId}`. Configured failure policy will be applied.");

        public static void AffinityCannotBeEstablishedBecauseNoDestinationsFound(ILogger logger, string clusterId)
        {
            _affinityCannotBeEstablishedBecauseNoDestinationsFound(logger, clusterId, null);
        }

        public static void DestinationMatchingToAffinityKeyNotFound(ILogger logger, string clusterId)
        {
            _destinationMatchingToAffinityKeyNotFound(logger, clusterId, null);
        }
    }
}
