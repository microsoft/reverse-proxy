// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.Management
{
    /// <summary>
    /// Default implementation of <see cref="IDestinationManagerFactory"/>
    /// which creates instances of <see cref="DestinationManager"/>
    /// to manage destinations of a backend at runtime.
    /// </summary>
    internal class DestinationManagerFactory : IDestinationManagerFactory
    {
        /// <inheritdoc/>
        public IDestinationManager CreateDestinationManager()
        {
            return new DestinationManager();
        }
    }
}
