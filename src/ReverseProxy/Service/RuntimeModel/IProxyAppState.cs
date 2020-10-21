// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// State of this reverse proxy application.
    /// </summary>
    public interface IProxyAppState
    {
        /// <summary>
        /// Tasks representing a proxy initialization.
        /// </summary>
        /// <returns><see cref="Task"/> that completes once the proxy gets fully initialized.</returns>
        Task InitializationTask { get; }

        /// <summary>
        /// Sets a flag indicating the proxy is fully initialized and ready to serve client requests.
        /// </summary>
        void SetFullyInitialized();
    }
}
