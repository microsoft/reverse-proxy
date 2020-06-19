// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Fabric;
using System.Fabric.Health;
using System.Fabric.Query;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Utilities;
using Microsoft.ServiceFabric.Services.Communication;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    /// <inheritdoc/>
    internal class ServiceFabricDiscoveryWorker : IServiceFabricDiscoveryWorker
    {
        public static readonly string HealthReportSourceId = "IslandGateway";
        public static readonly string HealthReportProperty = "IslandGatewayConfig";

        private readonly ILogger<ServiceFabricDiscoveryWorker> _logger;
        private readonly IServiceFabricExtensionConfigProvider _serviceFabricExtensionConfigProvider;
        private readonly IServiceFabricCaller _serviceFabricCaller;
        private readonly IClustersRepo _clustersRepo;
        private readonly IRoutesRepo _routesRepo;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceFabricDiscoveryWorker"/> class.
        /// </summary>
        public ServiceFabricDiscoveryWorker(
            ILogger<ServiceFabricDiscoveryWorker> logger,
            IServiceFabricCaller serviceFabricCaller,
            IServiceFabricExtensionConfigProvider serviceFabricExtensionConfigProvider,
            IClustersRepo clustersRepo,
            IRoutesRepo routesRepo)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(serviceFabricCaller, nameof(serviceFabricCaller));
            Contracts.CheckValue(serviceFabricExtensionConfigProvider, nameof(serviceFabricExtensionConfigProvider));
            Contracts.CheckValue(clustersRepo, nameof(clustersRepo));
            Contracts.CheckValue(routesRepo, nameof(routesRepo));

            _logger = logger;
            _serviceFabricCaller = serviceFabricCaller;
            _serviceFabricExtensionConfigProvider = serviceFabricExtensionConfigProvider;
            _clustersRepo = clustersRepo;
            _routesRepo = routesRepo;
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(ServiceFabricServiceDiscoveryOptions options, CancellationToken cancellation)
        {
            Contracts.CheckValue(options, nameof(options));

            var discoveredBackends = new Dictionary<string, Cluster>(StringComparer.Ordinal);
            var discoveredRoutes = new List<ProxyRoute>();
            IEnumerable<ApplicationWrapper> applications;

            try
            {
                applications = await _serviceFabricCaller.GetApplicationListAsync(cancellation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                // The serviceFabricCaller does their best effort to use LKG information, nothing we can do at this point
                _logger.LogError(ex, "Could not get applications list from Service Fabric, continuing with zero applications.");
                applications = Enumerable.Empty<ApplicationWrapper>();
            }

            foreach (var application in applications)
            {
                IEnumerable<ServiceWrapper> services;

                try
                {
                    services = await _serviceFabricCaller.GetServiceListAsync(application.ApplicationName, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    _logger.LogError(ex, $"Could not get service list for application {application.ApplicationName}, skipping application.");
                    continue;
                }

                foreach (var service in services)
                {
                    try
                    {
                        var serviceExtensionLabels = await _serviceFabricExtensionConfigProvider.GetExtensionLabelsAsync(application, service, cancellation);

                        // If this service wants to use Island Gateway
                        if (serviceExtensionLabels.GetValueOrDefault("IslandGateway.Enable", null) != "true")
                        {
                            // Skip this service
                            continue;
                        }

                        var cluster = LabelsParser.BuildCluster(service.ServiceName, serviceExtensionLabels);
                        await DiscoverDestinationsAsync(cluster, options, service, serviceExtensionLabels, cancellation);
                        if (!discoveredBackends.TryAdd(cluster.Id, cluster))
                        {
                            throw new ConfigException($"Duplicated cluster id '{cluster.Id}'. Skipping repeated definition, service '{service.ServiceName}'");
                        }

                        var routes = LabelsParser.BuildRoutes(service.ServiceName, serviceExtensionLabels);
                        discoveredRoutes.AddRange(routes);

                        ReportServiceHealth(options, service.ServiceName, HealthState.Ok, $"Successfully built cluster '{cluster.Id}' with {routes.Count} routes.");
                    }
                    catch (ConfigException ex)
                    {
                        // The user's problem
                        _logger.LogInformation($"Config error found when trying to load service '{service.ServiceName}', skipping. Error: {ex}.");

                        // TODO: emit Error health report once we are able to detect config issues *during* (as opposed to *after*) a target service upgrade.
                        // Proactive Error health report would trigger a rollback of the target service as desired. However, an Error report after rhe fact
                        // will NOT cause a rollback and will prevent the target service from performing subsequent monitored upgrades to mitigate, making things worse.
                        ReportServiceHealth(options, service.ServiceName, HealthState.Warning, $"Could not load service to Island Gateway: {ex.Message}.");
                    }
                    catch (Exception ex) when (!ex.IsFatal())
                    {
                        // Not user's problem
                        _logger.LogError(ex, $"Unexpected error when trying to load service '{service.ServiceName}, skipping'.");
                    }
                }
            }

            // TODO : keep track of seen backends and RemoveEndpointsAsync when not seen again
            _logger.LogInformation($"Discovered {discoveredBackends.Count} backends, {discoveredRoutes.Count} routes.");
            await _clustersRepo.SetClustersAsync(discoveredBackends, cancellation);
            await _routesRepo.SetRoutesAsync(discoveredRoutes, cancellation);
        }

        private static Destination BuildDestination(ReplicaWrapper replica, string listenerName, string healthListenerName)
        {
            if (!ServiceEndpointCollection.TryParseEndpointsString(replica.ReplicaAddress, out var serviceEndpointCollection))
            {
                throw new ConfigException($"Could not parse endpoints for replica {replica.Id}.");
            }

            // TODO: FabricServiceEndpoint has some other fields we are ignoring here. Decide which ones are relevant and fix this call.
            var serviceEndpoint = new FabricServiceEndpoint(
                listenerNames: new[] { listenerName },
                allowedSchemePredicate: HttpsSchemeSelector,
                emptyStringMatchesAnyListener: true);
            if (!FabricServiceEndpointSelector.TryGetEndpoint(serviceEndpoint, serviceEndpointCollection, out var endpointUri))
            {
                throw new ConfigException($"No acceptable endpoints found for replica '{replica.Id}'. Search criteria: listenerName='{listenerName}', emptyStringMatchesAnyListener=true.");
            }

            // Get service endpoint from the health listener, health listener is optional.
            Uri healthEndpointUri = null;
            if (!string.IsNullOrEmpty(healthListenerName))
            {
                var healthEndpoint = new FabricServiceEndpoint(
                    listenerNames: new[] { healthListenerName },
                    allowedSchemePredicate: HttpsSchemeSelector,
                    emptyStringMatchesAnyListener: true);
                if (!FabricServiceEndpointSelector.TryGetEndpoint(healthEndpoint, serviceEndpointCollection, out healthEndpointUri))
                {
                    throw new ConfigException($"No acceptable health endpoints found for replica '{replica.Id}'. Search criteria: listenerName='{healthListenerName}', emptyStringMatchesAnyListener=true.");
                }
            }

            return new Destination
            {
                Address = endpointUri.ToString(),
                HealthAddress = healthEndpointUri?.ToString(),
                Metadata = null, // TODO
            };
        }

        private static bool HttpsSchemeSelector(string urlScheme)
        {
            return urlScheme == "https";
        }

        private static TimeSpan HealthReportTimeToLive(ServiceFabricServiceDiscoveryOptions options) => options.DiscoveryPeriod.Multiply(3);

        private static bool IsHealthyReplica(ReplicaWrapper replica)
        {
            // TODO: Should we only consider replicas that Service Fabric reports as healthy (`replica.HealthState != HealthState.Error`)?
            // That is precisely what Traefik does, see: https://github.com/containous/traefik-extra-service-fabric/blob/a5c54b8d5409be7aa21b06d55cf186ee4cc25a13/servicefabric.go#L219
            // It seems misguided in our case, however, since we have an active health probing model
            // that can determine endpoint health more reliably. In particular because Service Fabric "Error" states does not necessarily mean
            // that the replica is unavailable, rather only that something in the cluster issued an "Error" report against it.
            // Skipping the replica here because we *suspect* it might be unavailable could lead to snowball cascading failures.
            return replica.ReplicaStatus == ServiceReplicaStatus.Ready;
        }

        private static bool IsReplicaEligible(ReplicaWrapper replica, StatefulReplicaSelectionMode statefulReplicaSelectionMode)
        {
            if (replica.ServiceKind != ServiceKind.Stateful)
            {
                // Stateless service replicas are always eligible
                return true;
            }

            switch (statefulReplicaSelectionMode)
            {
                case StatefulReplicaSelectionMode.Primary:
                    return replica.Role == ReplicaRole.Primary;
                case StatefulReplicaSelectionMode.ActiveSecondary:
                    return replica.Role == ReplicaRole.ActiveSecondary;
                case StatefulReplicaSelectionMode.All:
                default:
                    return true;
            }
        }

        /// <summary>
        /// Finds all eligible destinations (replica endpoints) for the <paramref name="service"/> specified,
        /// and populates the specified <paramref name="cluster"/>'s <see cref="Cluster.Destinations"/> accordingly.
        /// </summary>
        /// <remarks>All non-fatal exceptions are caught and logged.</remarks>
        private async Task DiscoverDestinationsAsync(
            Cluster cluster,
            ServiceFabricServiceDiscoveryOptions options,
            ServiceWrapper service,
            Dictionary<string, string> serviceExtensionLabels,
            CancellationToken cancellation)
        {
            IEnumerable<Guid> partitions;
            try
            {
                partitions = await _serviceFabricCaller.GetPartitionListAsync(service.ServiceName, cancellation);
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                _logger.LogError(ex, $"Could not get partition list for service {service.ServiceName}, skipping endpoints.");
                return;
            }

            var listenerName = serviceExtensionLabels.GetValueOrDefault("IslandGateway.Backend.ServiceFabric.ListenerName", string.Empty);
            var healthListenerName = serviceExtensionLabels.GetValueOrDefault("IslandGateway.Backend.Healthcheck.ServiceFabric.ListenerName", string.Empty);
            var statefulReplicaSelectionMode = ParseStatefulReplicaSelectionMode(serviceExtensionLabels);
            foreach (var partition in partitions)
            {
                IEnumerable<ReplicaWrapper> replicas;
                try
                {
                    replicas = await _serviceFabricCaller.GetReplicaListAsync(partition, cancellation);
                }
                catch (Exception ex) when (!ex.IsFatal())
                {
                    _logger.LogError(ex, $"Could not get replica list for partition {partition} of service {service.ServiceName}, skipping partition.");
                    continue;
                }

                foreach (var replica in replicas)
                {
                    if (!IsHealthyReplica(replica))
                    {
                        _logger.LogInformation($"Skipping unhealthy replica '{replica.Id}' from partition '{partition}', service '{service.ServiceName}': ReplicaStatus={replica.ReplicaStatus}, HealthState={replica.HealthState}.");
                        continue;
                    }

                    // If service is stateful, we need to determine which replica should we route to (e.g Primary, Secondary, All).
                    if (!IsReplicaEligible(replica, statefulReplicaSelectionMode))
                    {
                        // Skip this endpoint.
                        _logger.LogInformation($"Skipping ineligible endpoint '{replica.Id}' of service '{service.ServiceName}'. {nameof(statefulReplicaSelectionMode)}: {statefulReplicaSelectionMode}.");
                        continue;
                    }

                    try
                    {
                        var destination = BuildDestination(replica, listenerName, healthListenerName);

                        ReportReplicaHealth(options, service, partition, replica, HealthState.Ok, $"Successfully built the endpoint from listener '{listenerName}'.");
                        if (!cluster.Destinations.TryAdd(replica.Id.ToString(), destination))
                        {
                            throw new ConfigException($"Duplicated endpoint id '{replica.Id}'. Skipping repeated definition, service '{service.ServiceName}', backend id '{cluster.Id}'");
                        }
                    }
                    catch (ConfigException ex)
                    {
                        // The user's problem
                        _logger.LogInformation($"Config error found when trying to build endpoint for replica '{replica.Id}' of service '{service.ServiceName}', skipping. Error: {ex}.");

                        // TODO: emit Error health report once we are able to detect config issues *during* (as opposed to *after*) a target service upgrade.
                        // Proactive Error health report would trigger a rollback of the target service as desired. However, an Error report after rhe fact
                        // will NOT cause a rollback and will prevent the target service from performing subsequent monitored upgrades to mitigate, making things worse.
                        ReportReplicaHealth(options, service, partition, replica, HealthState.Warning, $"Could not build endpoint for Island Gateway: {ex.Message}");
                    }
                    catch (Exception ex) when (!ex.IsFatal())
                    {
                        // Not the user's problem
                        _logger.LogError(ex, $"Could not build endpoint for replica {replica.Id} of service {service.ServiceName}.");
                    }
                }
            }
        }

        private StatefulReplicaSelectionMode ParseStatefulReplicaSelectionMode(Dictionary<string, string> serviceExtensionLabels)
        {
            // Parse the value for StatefulReplicaSelectionMode: case insensitive, and trim the white space.
            var statefulReplicaSelectionMode = serviceExtensionLabels.GetValueOrDefault("IslandGateway.Backend.ServiceFabric.StatefulReplicaSelectionMode", StatefulReplicaSelectionLabel.All).Trim();
            if (string.Equals(statefulReplicaSelectionMode, StatefulReplicaSelectionLabel.PrimaryOnly, StringComparison.OrdinalIgnoreCase))
            {
                return StatefulReplicaSelectionMode.Primary;
            }

            if (string.Equals(statefulReplicaSelectionMode, StatefulReplicaSelectionLabel.SecondaryOnly, StringComparison.OrdinalIgnoreCase))
            {
                return StatefulReplicaSelectionMode.ActiveSecondary;
            }

            if (string.Equals(statefulReplicaSelectionMode, StatefulReplicaSelectionLabel.All, StringComparison.OrdinalIgnoreCase))
            {
                return StatefulReplicaSelectionMode.All;
            }

            _logger.LogWarning($"Invalid replica selection mode: {statefulReplicaSelectionMode}, fallback to selection mode: All.");
            return StatefulReplicaSelectionMode.All;
        }

        private void ReportServiceHealth(
            ServiceFabricServiceDiscoveryOptions options,
            Uri serviceName,
            HealthState state,
            string description = null)
        {
            // TODO: this method and the one below have repeated code. Refactor out.
            var healthReport = new ServiceHealthReport(
                serviceName: serviceName,
                healthInformation: new HealthInformation(
                    sourceId: HealthReportSourceId,
                    property: HealthReportProperty,
                    healthState: state)
                {
                    Description = description,
                    TimeToLive = HealthReportTimeToLive(options),
                    RemoveWhenExpired = true,
                });
            var sendOptions = new HealthReportSendOptions { Immediate = state != HealthState.Ok }; // Report immediately if unhealthy
            try
            {
                _serviceFabricCaller.ReportHealth(healthReport, sendOptions);
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                _logger.LogError(ex, $"Failed to report health '{state}' for service '{serviceName}'.");
            }
        }

        private void ReportReplicaHealth(
            ServiceFabricServiceDiscoveryOptions options,
            ServiceWrapper service,
            Guid partitionId,
            ReplicaWrapper replica,
            HealthState state,
            string description = null)
        {
            if (!options.ReportReplicasHealth)
            {
                return;
            }

            var healthInformation = new HealthInformation(
                sourceId: HealthReportSourceId,
                property: HealthReportProperty,
                healthState: state)
            {
                Description = description,
                TimeToLive = HealthReportTimeToLive(options),
                RemoveWhenExpired = true,
            };

            HealthReport healthReport;
            switch (service.ServiceKind)
            {
                case ServiceKind.Stateful:
                    healthReport = new StatefulServiceReplicaHealthReport(
                        partitionId: partitionId,
                        replicaId: replica.Id,
                        healthInformation: healthInformation);
                    break;
                case ServiceKind.Stateless:
                    healthReport = new StatelessServiceInstanceHealthReport(
                        partitionId: partitionId,
                        instanceId: replica.Id,
                        healthInformation: healthInformation);
                    break;
                default:
                    _logger.LogError($"Failed to report health '{state}' for replica {replica.Id}: unexpected ServiceKind '{service.ServiceKind}'.");
                    return;
            }

            var sendOptions = new HealthReportSendOptions { Immediate = state != HealthState.Ok }; // Report immediately if unhealthy
            try
            {
                _serviceFabricCaller.ReportHealth(healthReport, sendOptions);
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                _logger.LogError($"Failed to report health '{state}' for replica {replica.Id}.");
            }
        }
    }
}
