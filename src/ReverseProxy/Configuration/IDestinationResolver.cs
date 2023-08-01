// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Configuration;

/// <summary>
/// Resolves destination addresses.
/// </summary>
public interface IDestinationResolver
{
    /// <summary>
    /// Resolves the provided destinations and returns resolved destinations.
    /// </summary>
    /// <param name="destinations">The destinations to resolve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The resolved destinations and a change token used to indicate when resolution should be performed again.
    /// </returns>
    ValueTask<ResolvedDestinationCollection> ResolveDestinationsAsync(
        IReadOnlyDictionary<string, DestinationConfig> destinations,
        CancellationToken cancellationToken);
}
