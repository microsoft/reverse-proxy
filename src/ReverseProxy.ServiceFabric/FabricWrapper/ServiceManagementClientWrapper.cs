// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// A wrapper class for the service fabric client SDK.
    /// See Microsoft documentation: https://docs.microsoft.com/en-us/dotnet/api/system.fabric.fabricclient.servicemanagementclient?view=azure-dotnet .
    /// </summary>
    internal sealed class ServiceManagementClientWrapper : IServiceManagementClientWrapper
    {
        // Represents the enabling of the services to be managed.
        private readonly FabricClient.ServiceManagementClient _serviceManagementClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceManagementClientWrapper"/> class.
        /// Wraps QueryManager, PropertyManager and ServiceManager SF SDK.
        /// </summary>
        public ServiceManagementClientWrapper(IFabricClientWrapper fabricClientWrapper)
        {
            _serviceManagementClient = fabricClientWrapper.FabricClient.ServiceManager;
        }

        /// <summary>
        /// Gets the provisioned service manifest document in the specified application type name and application type version.
        /// Also takes in timeout interval, which is the maximum of time the system will allow this operation to continue before returning.
        /// </summary>
        public async Task<string> GetServiceManifestAsync(string applicationTypeName, string applicationTypeVersion, string serviceManifestName, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return await ExceptionsHelper.TranslateCancellations(
                () => _serviceManagementClient.GetServiceManifestAsync(applicationTypeName, applicationTypeVersion, serviceManifestName, timeout, cancellationToken),
                cancellationToken);
        }
    }
}
