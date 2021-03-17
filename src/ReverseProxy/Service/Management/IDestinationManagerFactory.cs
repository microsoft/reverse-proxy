// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Service.Management
{
    /// <summary>
    /// Provides a method <see cref="CreateDestinationManager"/> used to manage
    /// destinations of a cluster at runtime.
    /// </summary>
    internal interface IDestinationManagerFactory
    {
        /// <summary>
        /// Creates a new instance of <see cref="IDestinationManager"/> to manage
        /// destinations of a cluster at runtime.
        /// </summary>
        IDestinationManager CreateDestinationManager();
    }
}
