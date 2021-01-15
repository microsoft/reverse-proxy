// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// A wrapper for the service fabric property management client SDK to make service fabric API unit testable.
    /// See Microsoft documentation: https://docs.microsoft.com/en-us/dotnet/api/system.fabric.fabricclient.propertymanagementclient?view=azure-dotnet .
    /// </summary>
    internal interface IPropertyManagementClientWrapper
    {
        /// <summary>
        /// Get the specified NamedProperty.
        /// </summary>
        Task<string> GetPropertyAsync(Uri parentName, string propertyName, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Enumerates all Service Fabric properties under a given name.
        /// </summary>
        Task<IDictionary<string, string>> EnumeratePropertiesAsync(Uri parentName, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
