// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// Provides the Proxy configuration labels as gathered from Service Fabric.
    /// It looks for the labels in the ServiceManifest.xml and overrides in the service's properties.
    /// </summary>
    /// <remarks>
    /// The key-value labels to configure the Proxy are first read from the YARP extension
    /// in the "Extensions" section of a service in the ServiceManifest.xml. Example:
    /// <![CDATA[
    /// <StatelessServiceType ServiceTypeName="ExampleServiceTypeName">
    ///   <Extensions>
    ///     <Extension Name="YARP-preview">
    ///       <Labels xmlns="http://schemas.microsoft.com/2015/03/fabact-no-schema">
    ///         <Label Key="YARP.Enable">true</Label>
    ///         <Label Key="YARP.Backend.BackendId">exampleId</Label>
    ///       </Labels>
    ///     </Extension>
    ///   </Extensions>
    /// </StatelessServiceType>
    /// ]]>
    /// Once gathered, the labels are overrode with properties of the service. See
    /// <seealso href="https://docs.microsoft.com/en-us/rest/api/servicefabric/sfclient-index-property-management"/>
    /// for more information about properties.
    /// </remarks>
    internal interface IServiceExtensionLabelsProvider
    {
        /// <summary>
        /// Gets the labels representing the current Proxy configuration for the specified service.
        /// </summary>
        /// <exception cref="ServiceFabricIntegrationException">Failed to get or parse the required information from service fabric.</exception>
        Task<Dictionary<string, string>> GetExtensionLabelsAsync(ApplicationWrapper application, ServiceWrapper service, CancellationToken cancellationToken);
    }
}
