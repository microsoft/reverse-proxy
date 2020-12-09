// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Fabric.Health;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// TODO.
    /// </summary>
    // TODO: think of a better name. Suggestions?
    // TODO: should we make this a singleton? Or make sure it is created just once?
    internal interface ICachedServiceFabricCaller
    {
        /// <summary>
        /// TODO.
        /// </summary>
        Task<IEnumerable<ApplicationWrapper>> GetApplicationListAsync(CancellationToken cancellation);

        /// <summary>
        /// TODO.
        /// </summary>
        Task<IEnumerable<ServiceWrapper>> GetServiceListAsync(Uri applicationName, CancellationToken cancellation);

        /// <summary>
        /// TODO.
        /// </summary>
        Task<IEnumerable<Guid>> GetPartitionListAsync(Uri serviceName, CancellationToken cancellation);

        /// <summary>
        /// TODO.
        /// </summary>
        Task<IEnumerable<ReplicaWrapper>> GetReplicaListAsync(Guid partition, CancellationToken cancellation);

        /// <summary>
        /// TODO.
        /// </summary>
        Task<string> GetServiceManifestAsync(string applicationTypeName, string applicationTypeVersion, string serviceManifestName, CancellationToken cancellation);

        /// <summary>
        /// TODO.
        /// </summary>
        Task<string> GetServiceManifestName(string applicationTypeName, string applicationTypeVersion, string serviceTypeNameFilter, CancellationToken cancellation);

        /// <summary>
        /// Enumerates all Service Fabric properties under a given name.
        /// </summary>
        Task<IDictionary<string, string>> EnumeratePropertiesAsync(Uri parentName, CancellationToken cancellationToken);

        /// <summary>
        /// Reports health on a Service Fabric entity.
        /// </summary>
        void ReportHealth(HealthReport healthReport, HealthReportSendOptions sendOptions);

        /// <summary>
        /// Cleans up the cache by removing expired entries.
        /// </summary>
        void CleanUpExpired();
    }
}
