// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// A wrapper for the service fabric query client SDK to make service fabric API unit testable.
    /// Microsoft documentation: https://docs.microsoft.com/en-us/dotnet/api/system.fabric.fabricclient.queryclient?view=azure-dotnet .
    /// </summary>
    internal interface IQueryClientWrapper
    {
        /// <summary>
        /// Get details for a specific application.
        /// Also takes in timeout interval, which is the maximum of time the system will allow this operation to continue before returning.
        /// </summary>
        Task<IEnumerable<ApplicationWrapper>> GetApplicationListAsync(Uri applicationNameFilter, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Get details for all applications.
        /// Also takes in timeout interval, which is the maximum of time the system will allow this operation to continue before returning.
        /// </summary>
        Task<IEnumerable<ApplicationWrapper>> GetApplicationListAsync(TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the details for all services of an application or just the specified service. If the services do not fit in a page, one
        /// page of results is returned as well as a continuation token which can be used to get the next page.
        /// </summary>
        Task<IEnumerable<ServiceWrapper>> GetServiceListAsync(Uri applicationName, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the ServiceManifestName for a specific service,
        /// Also takes in timeout interval, which is the maximum of time the system will allow this operation to continue before returning.
        /// </summary>
        Task<string> GetServiceManifestName(string applicationTypeName, string applicationTypeVersion, string serviceTypeNameFilter, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Get details for all partitions of a service.
        /// </summary>
        Task<IEnumerable<Guid>> GetPartitionListAsync(Uri serviceName, TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Get all the replicas for a specific partition.
        /// </summary>
        Task<IEnumerable<ReplicaWrapper>> GetReplicaListAsync(Guid partitionId, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
