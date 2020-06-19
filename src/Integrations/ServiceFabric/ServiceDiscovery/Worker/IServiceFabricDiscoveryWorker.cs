// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    /// <summary>
    /// Discovers Service Fabric services that want to use the Island Gateway and builds the needed abstractions for it.
    /// </summary>
    /// <remarks>Implementations should take the Island Gateway's repos in the constructor.</remarks>
    internal interface IServiceFabricDiscoveryWorker
    {
        /// <summary>
        /// Execute the discovery and update entities.
        /// </summary>
        Task ExecuteAsync(ServiceFabricServiceDiscoveryOptions options, CancellationToken cancellation);
    }
}
