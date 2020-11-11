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
using Microsoft.Extensions.Options;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service;
using Microsoft.ServiceFabric.Services.Communication;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// Default implementation of the <see cref="IDiscoverer"/> class.
    internal class Discoverer : IDiscoverer
    {
        public static readonly string HealthReportSourceId = "YARP";
        public static readonly string HealthReportProperty = "DynamicConfig";

        private readonly ILogger<Discoverer> _logger;
        private readonly IServiceFabricCaller _serviceFabricCaller;
        private readonly IServiceExtensionLabelsProvider _serviceFabricExtensionConfigProvider;
        private readonly IConfigValidator _configValidator;
        private readonly IOptionsMonitor<ServiceFabricDiscoveryOptions> _optionsMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="Discoverer"/> class.
        /// </summary>
        public Discoverer(
            ILogger<Discoverer> logger,
            IServiceFabricCaller serviceFabricCaller,
            IServiceExtensionLabelsProvider serviceFabricExtensionConfigProvider,
            IConfigValidator configValidator,
            IOptionsMonitor<ServiceFabricDiscoveryOptions> optionsMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceFabricCaller = serviceFabricCaller ?? throw new ArgumentNullException(nameof(serviceFabricCaller));
            _serviceFabricExtensionConfigProvider = serviceFabricExtensionConfigProvider ?? throw new ArgumentNullException(nameof(serviceFabricExtensionConfigProvider));
            _configValidator = configValidator ?? throw new ArgumentNullException(nameof(configValidator));
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        }

        /// <inheritdoc/>
        public async Task<(IReadOnlyList<ProxyRoute> Routes, IReadOnlyList<Cluster> Clusters)> DiscoverAsync(CancellationToken cancellation)
        {
            // Take a snapshot of current options and use that consistently for this execution.
            var options = _optionsMonitor.CurrentValue;

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
            catch (Exception ex) // TODO: davidni: not fatal?
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
                catch (Exception ex) // TODO: davidni: not fatal?
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
                        if (serviceExtensionLabels.GetValueOrDefault("YARP.Enable", null) != "true")
                        {
                            // Skip this service
                            continue;
                        }

                        var cluster = LabelsParser.BuildCluster(service.ServiceName, serviceExtensionLabels);
                        await DiscoverDestinationsAsync(cluster, options, service, serviceExtensionLabels, cancellation);
                        var clusterValidationErrors = await _configValidator.ValidateClusterAsync(cluster);
                        if (clusterValidationErrors.Count > 0)
                        {
                            throw new ConfigException($"Skipping cluster id '{cluster.Id} due to validation errors.", new AggregateException(clusterValidationErrors));
                        }

                        if (!discoveredBackends.TryAdd(cluster.Id, cluster))
                        {
                            throw new ConfigException($"Duplicated cluster id '{cluster.Id}'. Skipping repeated definition, service '{service.ServiceName}'");
                        }

                        var routes = LabelsParser.BuildRoutes(service.ServiceName, serviceExtensionLabels);
                        var routeValidationErrors = new List<Exception>();
                        foreach (var route in routes)
                        {
                            routeValidationErrors.AddRange(await _configValidator.ValidateRouteAsync(route));
                        }

                        if (routeValidationErrors.Count > 0)
                        {
                            // Don't add ANY routes if even a single one is bad. Trying to add partial routes
                            // could lead to unexpected results (e.g. a typo in the configuration of higher-priority route
                            // could lead to a lower-priority route being selected for requests it should not be handling).
                            throw new ConfigException($"Skipping ALL routes for cluster id '{cluster.Id} due to validation errors.", new AggregateException(routeValidationErrors));
                        }

                        discoveredRoutes.AddRange(routes);

                        ReportServiceHealth(options, service.ServiceName, HealthState.Ok, $"Successfully built cluster '{cluster.Id}' with {routes.Count} routes.");
                    }
                    catch (ConfigException ex)
                    {
                        // User error
                        _logger.LogInformation($"Config error found when trying to load service '{service.ServiceName}', skipping. Error: {ex}.");

                        // TODO: emit Error health report once we are able to detect config issues *during* (as opposed to *after*) a target service upgrade.
                        // Proactive Error health report would trigger a rollback of the target service as desired. However, an Error report after rhe fact
                        // will NOT cause a rollback and will prevent the target service from performing subsequent monitored upgrades to mitigate, making things worse.
                        ReportServiceHealth(options, service.ServiceName, HealthState.Warning, $"Could not load service to Island Gateway: {ex.Message}.");
                    }
                    catch (Exception ex) // TODO: davidni: not fatal?
                    {
                        // Not user's problem
                        _logger.LogError(ex, $"Unexpected error when trying to load service '{service.ServiceName}, skipping'.");
                    }
                }
            }

            _logger.LogInformation($"Discovered {discoveredBackends.Count} backends, {discoveredRoutes.Count} routes.");
            return (discoveredRoutes, discoveredBackends.Values.ToList());
        }

        private static TimeSpan HealthReportTimeToLive(ServiceFabricDiscoveryOptions options) => options.DiscoveryPeriod.Multiply(3);

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

            return statefulReplicaSelectionMode switch
            {
                StatefulReplicaSelectionMode.Primary => replica.Role == ReplicaRole.Primary,
                StatefulReplicaSelectionMode.ActiveSecondary => replica.Role == ReplicaRole.ActiveSecondary,
                _ => true,
            };
        }

        private Destination BuildDestination(ReplicaWrapper replica, string listenerName, string healthListenerName)
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
                Health = healthEndpointUri?.ToString(),
                Metadata = null, // TODO
            };
        }

        private bool HttpsSchemeSelector(string urlScheme)
        {
            if (_optionsMonitor.CurrentValue.DiscoverInsecureHttpDestinations)
            {
                return urlScheme == "https" || urlScheme == "http";
            }

            return urlScheme == "https";
        }

        /// <summary>
        /// Finds all eligible destinations (replica endpoints) for the <paramref name="service"/> specified,
        /// and populates the specified <paramref name="cluster"/>'s <see cref="Cluster.Destinations"/> accordingly.
        /// </summary>
        /// <remarks>All non-fatal exceptions are caught and logged.</remarks>
        private async Task DiscoverDestinationsAsync(
            Cluster cluster,
            ServiceFabricDiscoveryOptions options,
            ServiceWrapper service,
            Dictionary<string, string> serviceExtensionLabels,
            CancellationToken cancellation)
        {
            IEnumerable<Guid> partitions;
            try
            {
                partitions = await _serviceFabricCaller.GetPartitionListAsync(service.ServiceName, cancellation);
            }
            catch (Exception ex) // TODO: davidni: not fatal?
            {
                _logger.LogError(ex, $"Could not get partition list for service {service.ServiceName}, skipping endpoints.");
                return;
            }

            var listenerName = serviceExtensionLabels.GetValueOrDefault("YARP.Backend.ServiceFabric.ListenerName", string.Empty);
            var healthListenerName = serviceExtensionLabels.GetValueOrDefault("YARP.Backend.Healthcheck.ServiceFabric.ListenerName", string.Empty);
            var statefulReplicaSelectionMode = ParseStatefulReplicaSelectionMode(serviceExtensionLabels);
            foreach (var partition in partitions)
            {
                IEnumerable<ReplicaWrapper> replicas;
                try
                {
                    replicas = await _serviceFabricCaller.GetReplicaListAsync(partition, cancellation);
                }
                catch (Exception ex) // TODO: davidni: not fatal?
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
                    catch (Exception ex) // TODO: davidni: not fatal?
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
            var statefulReplicaSelectionMode = serviceExtensionLabels.GetValueOrDefault("YARP.Backend.ServiceFabric.StatefulReplicaSelectionMode", StatefulReplicaSelectionLabel.All).Trim();
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
            ServiceFabricDiscoveryOptions options,
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
            var sendOptions = new HealthReportSendOptions
            {
                // Report immediately if unhealthy or if explicitly requested
                Immediate = options.AlwaysSendImmediateHealthReports ? true : state != HealthState.Ok
            };
            try
            {
                _serviceFabricCaller.ReportHealth(healthReport, sendOptions);
            }
            catch (Exception ex) // TODO: davidni: not fatal?
            {
                _logger.LogError(ex, $"Failed to report health '{state}' for service '{serviceName}'.");
            }
        }

        private void ReportReplicaHealth(
            ServiceFabricDiscoveryOptions options,
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
            catch (Exception ex) // TODO: davidni: not fatal?
            {
                _logger.LogError($"Failed to report health '{state}' for replica {replica.Id}: {ex}.");
            }
        }
    }
}
