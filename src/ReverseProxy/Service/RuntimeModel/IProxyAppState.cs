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
        /// Whether the proxy is fully initialized and ready to serve client requests.
        /// </summary>
        bool IsFullyInitialized { get; }

        /// <summary>
        /// Waits for the full proxy initialization.
        /// </summary>
        /// <returns><see cref="Task"/> that completes once the proxy gets fully initialized.</returns>
        Task WaitForFullInitialization();
    }
}
