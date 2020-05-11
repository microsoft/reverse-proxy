// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.Service.Management
{
    /// <summary>
    /// Provides a method <see cref="CreateDestinationManager"/> used to manage
    /// destinations of a backend at runtime.
    /// </summary>
    internal interface IDestinationManagerFactory
    {
        /// <summary>
        /// Creates a new instance of <see cref="IDestinationManager"/> to manage
        /// destinations of a backend at runtime.
        /// </summary>
        IDestinationManager CreateDestinationManager();
    }
}
