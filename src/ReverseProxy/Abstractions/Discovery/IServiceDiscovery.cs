// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Discovers cluster, routes and destinations of services that want to use the Island Gateway.
    /// </summary>
    /// <remarks>
    /// Implementations should notify the <see cref="IIslandGatewayConfigManager"/> when its repositories were updated.
    /// </remarks>
    public interface IServiceDiscovery
    {
        /// <summary>
        /// The name of this service discovery.
        /// </summary>
        /// <remarks>
        /// This name is used to identify this service discovery among other available instances of IServiceDiscovery.
        /// </remarks>
        string Name { get; }

        /// <summary>
        /// Starts the discovery.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the discovery.
        /// </summary>
        Task StopAsync(CancellationToken cancellation);

        /// <summary>
        /// Sets the discovery configuration.
        /// </summary>
        Task SetConfigAsync(IConfigurationSection newConfig, CancellationToken cancellation);
    }
}
