// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.ServiceDiscovery;

/// <summary>
/// An <see cref="IDestinationResolver"/> which performs no action.
/// </summary>
internal sealed class NoOpDestinationResolver : IDestinationResolver
{
    public ValueTask<ResolvedDestinationCollection> ResolveDestinationsAsync(IReadOnlyDictionary<string, DestinationConfig> destinations, CancellationToken cancellationToken)
        => new(new ResolvedDestinationCollection(destinations, ChangeToken: null));
}
