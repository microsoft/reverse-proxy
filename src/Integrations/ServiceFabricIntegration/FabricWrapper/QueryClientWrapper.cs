// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Fabric.Query;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IslandGateway.CoreServicesBorrowed.CoreFramework;
using Microsoft.Extensions.Logging;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    /// <summary>
    /// A wrapper class for the service fabric client SDK.
    /// Microsoft documentation: https://docs.microsoft.com/en-us/dotnet/api/system.fabric.fabricclient.queryclient?view=azure-dotnet .
    /// </summary>
    internal class QueryClientWrapper : IQueryClientWrapper
    {
        private readonly ILogger<QueryClientWrapper> logger;
        private readonly FabricClient.QueryClient queryClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryClientWrapper"/> class.
        /// </summary>
        public QueryClientWrapper(ILogger<QueryClientWrapper> logger)
        {
            Contracts.CheckValue(logger, nameof(logger));
            this.logger = logger;

            this.queryClient = new FabricClient().QueryManager;
        }

        /// <summary>
        /// Gets the details for all applications created in the system.
        /// Also takes in timeout interval, which is the maximum of time the system will allow this operation to continue before returning.
        /// </summary>
        public async Task<IEnumerable<ApplicationWrapper>> GetApplicationListAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return await this.GetApplicationListAsync(null, timeout, cancellationToken);
        }

        /// <summary>
        /// Gets the details for all applications or for a specific application created in the system.
        /// Also takes in timeout interval, which is the maximum of time the system will allow this operation to continue before returning.
        /// </summary>
        public async Task<IEnumerable<ApplicationWrapper>> GetApplicationListAsync(Uri applicationNameFilter, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var applicationList = new List<ApplicationWrapper>();
            ApplicationList previousResult = null;

            // Set up the counter that record the time lapse.
            Stopwatch stopWatch = Stopwatch.StartNew();
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = timeout - stopWatch.Elapsed;
                if (remaining.Ticks < 0)
                {
                    // If the passing time is longer than the timeout duration.
                    throw new TimeoutException($"Unable to enumerate all application pages in the allotted time budget of {timeout.TotalSeconds} seconds");
                }

                previousResult = await ExceptionsHelper.TranslateCancellations(
                    () => this.queryClient.GetApplicationListAsync(
                        applicationNameFilter: applicationNameFilter,
                        continuationToken: previousResult?.ContinuationToken,
                        timeout: remaining,
                        cancellationToken: cancellationToken),
                    cancellationToken);

                applicationList.AddRange(previousResult.Select(MapApp));
            }
            while (!string.IsNullOrEmpty(previousResult?.ContinuationToken));

            return applicationList;

            ApplicationWrapper MapApp(Application app) =>
                new ApplicationWrapper
                {
                    ApplicationName = app.ApplicationName,
                    ApplicationTypeName = app.ApplicationTypeName,
                    ApplicationTypeVersion = app.ApplicationTypeVersion,
                    ApplicationParameters = MapAppParameters(app),
                };

            IDictionary<string, string> MapAppParameters(Application app)
            {
                // NOTE: App Params in Service Fabric are case insensitive (verified on version 7.0.457.9590).
                // Since this is not documented behavior, the code below tries to play it safe by ignoring
                // duplicated app params instead of throwing and preventing such service from working at all
                // behind Island Gateway.
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var param in app.ApplicationParameters)
                {
                    if (!result.TryAdd(param.Name, param.Value))
                    {
                        this.logger.LogInformation($"Duplicate app parameter '{param.Name}' for application '{app.ApplicationName}'");
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the details for all services of an application or just the specified service. If the services do not fit in a page, one
        /// page of results is returned as well as a continuation token which can be used to get the next page. Let serviceNameFilter to be null because we are getting all ServiceName.
        /// </summary>
        public async Task<IEnumerable<ServiceWrapper>> GetServiceListAsync(Uri applicationName, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var serviceList = new List<ServiceWrapper>();
            ServiceList previousResult = null;

            // Set up the counter that record the time lapse.
            Stopwatch stopWatch = Stopwatch.StartNew();
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = timeout - stopWatch.Elapsed;
                if (remaining.Ticks < 0)
                {
                    // If the passing time is longer than the timeout duration.
                    throw new TimeoutException($"Unable to enumerate all service pages in the allotted time budget of {timeout.TotalSeconds} seconds");
                }

                previousResult = await ExceptionsHelper.TranslateCancellations(
                    () => this.queryClient.GetServiceListAsync(
                        applicationName: applicationName,
                        serviceNameFilter: null,
                        continuationToken: previousResult?.ContinuationToken,
                        timeout: remaining,
                        cancellationToken: cancellationToken),
                    cancellationToken);

                foreach (Service service in previousResult)
                {
                    serviceList.Add(
                        new ServiceWrapper
                        {
                            ServiceName = service.ServiceName,
                            ServiceTypeName = service.ServiceTypeName,
                            ServiceManifestVersion = service.ServiceManifestVersion,
                            ServiceKind = service.ServiceKind,
                        });
                }
            }
            while (!string.IsNullOrEmpty(previousResult?.ContinuationToken));

            return serviceList;
        }

        /// <summary>
        /// Gets the ServiceManifestName for a specific service,
        /// Also takes in timeout interval, which is the maximum of time the system will allow this operation to continue before returning.
        /// </summary>
        public async Task<string> GetServiceManifestName(string applicationTypeName, string applicationTypeVersion, string serviceTypeName, TimeSpan timeout, CancellationToken cancellationToken)
        {
            ServiceTypeList serviceTypes = await ExceptionsHelper.TranslateCancellations(
                () => this.queryClient.GetServiceTypeListAsync(
                    applicationTypeName: applicationTypeName,
                    applicationTypeVersion: applicationTypeVersion,
                    serviceTypeNameFilter: serviceTypeName,
                    timeout: timeout,
                    cancellationToken: cancellationToken),
                cancellationToken);
            if (serviceTypes.Count == 0)
            {
                throw new InvalidOperationException($"Did not find a service manifest for ApplicationTypeName={applicationTypeName} ApplicationTypeVersion={applicationTypeVersion} ServiceTypeName={serviceTypeName}");
            }
            return serviceTypes[0].ServiceManifestName;
        }

        /// <summary>
        /// Get ID for all partitions of a service. If the partition do not fit in a page, one
        /// page of results is returned as well as a continuation token which can be used to get the next page. Let PartitionFilter to be null because we are getting all partition.
        /// </summary>
        public async Task<IEnumerable<Guid>> GetPartitionListAsync(Uri serviceName, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var partitionList = new List<Guid>();
            ServicePartitionList previousResult = null;

            // Set up the counter that record the time lapse.
            Stopwatch stopWatch = Stopwatch.StartNew();
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = timeout - stopWatch.Elapsed;
                if (remaining.Ticks < 0)
                {
                    // If the passing time is longer than the timeout duration.
                    throw new TimeoutException($"Unable to enumerate all partition pages in the allotted time budget of {timeout.TotalSeconds} seconds");
                }

                previousResult = await ExceptionsHelper.TranslateCancellations(
                    () => this.queryClient.GetPartitionListAsync(
                        serviceName: serviceName,
                        partitionIdFilter: null,
                        continuationToken: previousResult?.ContinuationToken,
                        timeout: remaining,
                        cancellationToken: cancellationToken),
                    cancellationToken);

                foreach (Partition partition in previousResult)
                {
                    partitionList.Add(partition.PartitionInformation.Id);
                }
            }
            while (!string.IsNullOrEmpty(previousResult?.ContinuationToken));

            return partitionList;
        }

        public async Task<IEnumerable<ReplicaWrapper>> GetReplicaListAsync(Guid partitionId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var replicaList = new List<ReplicaWrapper>();
            ServiceReplicaList previousResult = null;

            // Set up the counter that record the time lapse.
            Stopwatch stopWatch = Stopwatch.StartNew();
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = timeout - stopWatch.Elapsed;
                if (remaining.Ticks < 0)
                {
                    // If the passing time is longer than the timeout duration.
                    throw new TimeoutException($"Unable to enumerate all replicas pages in the allotted time budget of {timeout.TotalSeconds} seconds");
                }

                previousResult = await ExceptionsHelper.TranslateCancellations(
                    () => this.queryClient.GetReplicaListAsync(
                        partitionId: partitionId,
                        continuationToken: previousResult?.ContinuationToken,
                        timeout: remaining,
                        cancellationToken: cancellationToken),
                    cancellationToken);

                foreach (Replica replica in previousResult)
                {
                    replicaList.Add(
                        new ReplicaWrapper
                        {
                            Id = replica.Id,
                            ReplicaAddress = replica.ReplicaAddress,
                            ReplicaStatus = replica.ReplicaStatus,
                            HealthState = replica.HealthState,
                            ServiceKind = replica.ServiceKind,
                            Role = replica.ServiceKind == ServiceKind.Stateful ? ((StatefulServiceReplica)replica).ReplicaRole : (ReplicaRole?)null,
                        });
                }
            }
            while (!string.IsNullOrEmpty(previousResult?.ContinuationToken));

            return replicaList;
        }
    }
}
