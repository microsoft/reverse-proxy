// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.ServiceFabric;

/// <summary>
/// A wrapper for the service fabric service management client SDK to make service fabric API unit testable.
/// See Microsoft documentation: https://docs.microsoft.com/en-us/dotnet/api/system.fabric.fabricclient.servicemanagementclient?view=azure-dotnet .
/// </summary>
internal interface IServiceManagementClientWrapper
{
    /// <summary>
    /// Gets the provisioned service manifest document in the specified application type name and application type version.
    /// Also takes in timeout interval, which is the maximum of time the system will allow this operation to continue before returning.
    /// </summary>
    Task<string> GetServiceManifestAsync(string applicationTypeName, string applicationTypeVersion, string serviceManifestName, TimeSpan timeout, CancellationToken cancellationToken);
}
