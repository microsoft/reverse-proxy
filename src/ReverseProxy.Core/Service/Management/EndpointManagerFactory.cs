// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.Service.Management
{
    /// <summary>
    /// Default implementation of <see cref="IEndpointManagerFactory"/>
    /// which creates instances of <see cref="EndpointManager"/>
    /// to manage endpoints of a backend at runtime.
    /// </summary>
    internal class EndpointManagerFactory : IEndpointManagerFactory
    {
        /// <inheritdoc/>
        public IEndpointManager CreateEndpointManager()
        {
            return new EndpointManager();
        }
    }
}
