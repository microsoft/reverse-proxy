// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.ServiceDiscovery;

/// <summary>
/// Represents a collection of resolved destinations.
/// </summary>
/// <param name="Destinations">The resolved destinations.</param>
/// <param name="ChangeToken">An optional change token which indicates when the destination collection should be refreshed.</param>
public record class ResolvedDestinationCollection(IReadOnlyDictionary<string, DestinationConfig> Destinations, IChangeToken? ChangeToken);
