// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    /// <summary>
    /// Provides the Island Gateway's configuration labels as gathered from Service Fabric.
    /// It looks for the labels in the ServiceManifest.xml and overrides in the service's properties.
    /// </summary>
    /// <remarks>
    /// The key-value labels to configure the Island Gateway are first read from the IslandGateway extension
    /// in the "Extensions" section of a service in the ServiceManifest.xml. Example:
    /// <![CDATA[
    /// <StatelessServiceType ServiceTypeName="ExampleServiceTypeName">
    ///   <Extensions>
    ///     <Extension Name="IslandGateway">
    ///       <Labels xmlns="http://schemas.microsoft.com/2015/03/fabact-no-schema">
    ///         <Label Key="IslandGateway.Enable">true</Label>
    ///         <Label Key="IslandGateway.Backend.BackendId">exampleId</Label>
    ///       </Labels>
    ///     </Extension>
    ///   </Extensions>
    /// </StatelessServiceType>
    /// ]]>
    /// Once gathered, the labels are overrode with properties of the service. See
    /// <seealso href="https://docs.microsoft.com/en-us/rest/api/servicefabric/sfclient-index-property-management"/>
    /// for more information about properties.
    /// Refer to the Island Gateway documentation for further details about labels and their format.
    /// </remarks>
    internal interface IServiceFabricExtensionConfigProvider
    {
        /// <summary>
        /// Gets the labels representing the current Island Gateway configuration for the specified service.
        /// </summary>
        /// <exception cref="ServiceFabricIntegrationException">Failed to get or parse the required information from service fabric.</exception>
        Task<Dictionary<string, string>> GetExtensionLabelsAsync(ApplicationWrapper application, ServiceWrapper service, CancellationToken cancellationToken);
    }
}
