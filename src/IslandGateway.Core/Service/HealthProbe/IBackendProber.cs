// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Core.RuntimeModel;
using IslandGateway.Utilities;

namespace IslandGateway.Core.Service.HealthProbe
{
    /// <summary>
    /// An interface for the prober <see cref="BackendProber"/>. Prober is the worker to check and update
    /// the health states of all the endpoints in backend. One backend owns one prober.
    /// </summary>
    internal interface IBackendProber
    {
        /// <summary>
        /// The backend ID of the service prober is checking.
        /// </summary>
        public string BackendId { get; }

        /// <summary>
        /// The backend configuration of the service prober is checking.
        /// </summary>
        public BackendConfig Config { get; }

        /// <summary>
        /// Start the probing for all endpoints.
        /// </summary>
        void Start(AsyncSemaphore semaphore);

        /// <summary>
        /// Gracefully stops the probing for all endpoints.
        /// </summary>
        Task StopAsync();
    }
}
