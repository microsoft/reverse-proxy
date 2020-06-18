// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Management;

namespace Microsoft.ReverseProxy.Service.HealthProbe
{
    /// <summary>
    /// Interface for the factory of <see cref="ClusterProber"/>. The factory provide a way of dependency injection to pass
    /// prober into the healthProbeWorker class. Also make the healthProbeWorker unit testable.
    /// </summary>
    internal interface IClusterProberFactory
    {
        /// <summary>
        /// Create a instance of <see cref="ClusterProber"/>.
        /// </summary>
        IClusterProber CreateClusterProber(string clusterId, ClusterConfig config, IDestinationManager destinationManager);
    }
}
