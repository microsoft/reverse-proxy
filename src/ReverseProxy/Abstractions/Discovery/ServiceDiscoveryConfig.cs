// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Microsoft.ReverseProxy.Abstractions
{
    /// <summary>
    /// Service discovery settings for Island Gateway.
    /// The specified service discovery mechanism will be used
    /// by invoking an implementation of <see cref="IServiceDiscovery"/> whose
    /// <see cref="IServiceDiscovery.Name"/> matches <see cref="ServiceDiscoveryName"/>.
    /// See <see cref="ServiceDiscoveryConfigApplier"/> for details.
    /// </summary>
    public class ServiceDiscoveryConfig
    {
        /// <summary>
        /// The name of the service discovery used to configure Clusters, Destinations and Routes in Island Gateway.
        /// </summary>
        public string ServiceDiscoveryName { get; set; }

        /// <summary>
        /// Named configurations for the used service discovery to reference from <see cref="ServiceDiscoveryConfigs"/>.
        /// </summary>
        public IDictionary<string, IConfigurationSection> ServiceDiscoveryConfigs { get; set; }
    }
}
