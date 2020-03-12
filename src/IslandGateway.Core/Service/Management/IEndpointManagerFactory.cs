// <copyright file="IEndpointManagerFactory.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

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