// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Core.RuntimeModel;
using Microsoft.ReverseProxy.Core.Service.Management;

namespace Microsoft.ReverseProxy.Core.Service.HealthProbe
{
    /// <summary>
    /// Interface for the factory of <see cref="BackendProber"/>. The factory provide a way of dependency injection to pass
    /// prober into the healthProbeWorker class. Also make the healthProbeWorker unit testable.
    /// </summary>
    internal interface IBackendProberFactory
    {
        /// <summary>
        /// Create a instance of <see cref="BackendProber"/>.
        /// </summary>
        IBackendProber CreateBackendProber(string backendId, BackendConfig config, IEndpointManager endpointManager);
    }
}
