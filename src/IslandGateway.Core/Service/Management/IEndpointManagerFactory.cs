// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace IslandGateway.Core.Service.Management
{
    /// <summary>
    /// Provides a method <see cref="CreateEndpointManager"/> used to manage
    /// endpoints of a backend at runtime.
    /// </summary>
    internal interface IEndpointManagerFactory
    {
        /// <summary>
        /// Creates a new instance of <see cref="IEndpointManager"/> to manage
        /// endpoints of a backend at runtime.
        /// </summary>
        IEndpointManager CreateEndpointManager();
    }
}
