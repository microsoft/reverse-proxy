// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Changes the <see cref="IProxyAppState"/>.
    /// </summary>
    internal interface IProxyAppStateSetter
    {
        /// <summary>
        /// Sets a flag indicating the proxy is fully initialized and ready to serve client requests.
        /// </summary>
        void SetFullyInitialized();
    }
}
