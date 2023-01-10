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

internal sealed partial class HashCookieSessionAffinityPolicy : ISessionAffinityPolicy
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
            var affinityCookieOptions = AffinityHelpers.CreateCookieOptions(config.Cookie, context.Request.IsHttps, _clock);
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
}
