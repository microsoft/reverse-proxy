// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.SessionAffinity;

internal sealed class ArrCookieSessionAffinityPolicy : BaseHashCookieSessionAffinityPolicy
{
    private readonly ConditionalWeakTable<DestinationState, string> _hashes = new();

    public ArrCookieSessionAffinityPolicy(
        IClock clock,
        ILogger<ArrCookieSessionAffinityPolicy> logger)
        : base(clock, logger) { }

    public override string Name => SessionAffinityConstants.Policies.ArrCookie;

    protected override string GetDestinationHash(DestinationState d)
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
