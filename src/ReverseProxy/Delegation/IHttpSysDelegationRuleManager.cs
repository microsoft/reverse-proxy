// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Server.HttpSys;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Delegation;

/// <summary>
/// Manages Http.sys delegation rules for the configured clusters/destination pairs.
/// </summary>
public interface IHttpSysDelegationRuleManager
{
    /// <summary>
    /// Attempts to get the <see cref="DelegationRule"/> for the given cluster/destination pair.
    /// </summary>
    /// <param name="destination">The destination to get the <see cref="DelegationRule"/> for.</param>
    /// <param name="delegationRule">
    /// <paramref name="delegationRule"/> contains the <see cref="DelegationRule"/> for the given cluster/destination pair if found or null if not found.
    /// </param>
    /// <returns>true if the <see cref="DelegationRule"/> was found; otherwise, false.</returns>
    bool TryGetDelegationRule(DestinationState destination, [MaybeNullWhen(false)] out DelegationRule delegationRule);
}
#endif
