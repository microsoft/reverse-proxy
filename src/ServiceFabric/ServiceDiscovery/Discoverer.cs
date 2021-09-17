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
using Microsoft.ServiceFabric.Services.Communication;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.ServiceFabric
{
    /// Default implementation of the <see cref="IDiscoverer"/> class.
    internal sealed class Discoverer : IDiscoverer
    {
        public static readonly string HealthReportSourceId = "YARP";
        public static readonly string HealthReportProperty = "DynamicConfig";

        private readonly ILogger<Discoverer> _logger;
        private readonly ICachedServiceFabricCaller _serviceFabricCaller;
        private readonly IServiceExtensionLabelsProvider _serviceFabricExtensionConfigProvider;
        private readonly IConfigValidator _configValidator;
        private readonly IOptionsMonitor<ServiceFabricDiscoveryOptions> _optionsMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="Discoverer"/> class.
        /// </summary>
        public Discoverer(
            ILogger<Discoverer> logger,
            ICachedServiceFabricCaller serviceFabricCaller,
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
        public async Task<(IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters)> DiscoverAsync(CancellationToken cancellation)
        {
            // Take a snapshot of current options and use that consistently for this execution.
            var options = _optionsMonitor.CurrentValue;

            _serviceFabricCaller.CleanUpExpired();

            var discoveredBackends = new Dictionary<string, ClusterConfig>(StringComparer.Ordinal);
            var discoveredRoutes = new List<RouteConfig>();
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
                Log.GettingApplicationFailed(_logger, ex);
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
                    Log.GettingServiceFailed(_logger, application.ApplicationName, ex);
                    continue;
                }

                foreach (var service in services)
                {
                    try
                    {
                        var serviceExtensionLabels = await _serviceFabricExtensionConfigProvider.GetExtensionLabelsAsync(application, service, cancellation);

                        // If this service wants to use us as the proxy
                        if (serviceExtensionLabels.GetValueOrDefault("YARP.Enable", null) != "true")
                        {
                            // Skip this service
                            continue;
                        }

                        var destinations = await DiscoverDestinationsAsync(options, service, serviceExtensionLabels, cancellation);
                        var cluster = LabelsParser.BuildCluster(service.ServiceName, serviceExtensionLabels, destinations);
                        var clusterValidationErrors = await _configValidator.ValidateClusterAsync(cluster);
                        if (clusterValidationErrors.Count > 0)
                        {
                            throw new ConfigException($"Skipping cluster id '{cluster.ClusterId} due to validation errors.", new AggregateException(clusterValidationErrors));
                        }

                        if (!discoveredBackends.TryAdd(cluster.ClusterId, cluster))
                        {
                            throw new ConfigException($"Duplicated cluster id '{cluster.ClusterId}'. Skipping repeated definition, service '{service.ServiceName}'");
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
                            throw new ConfigException($"Skipping ALL routes for cluster id '{cluster.ClusterId} due to validation errors.", new AggregateException(routeValidationErrors));
                        }

                        discoveredRoutes.AddRange(routes);

                        ReportServiceHealth(options, service.ServiceName, HealthState.Ok, $"Successfully built cluster '{cluster.ClusterId}' with {routes.Count} routes.");
                    }
                    catch (ConfigException ex)
                    {
                        // User error
                        Log.InvalidServiceConfig(_logger, service.ServiceName, ex);

                        // TODO: emit Error health report once we are able to detect config issues *during* (as opposed to *after*) a target service upgrade.
                        // Proactive Error health report would trigger a rollback of the target service as desired. However, an Error report after rhe fact
                        // will NOT cause a rollback and will prevent the target service from performing subsequent monitored upgrades to mitigate, making things worse.
                        ReportServiceHealth(options, service.ServiceName, HealthState.Warning, $"Could not load service configuration: {ex.Message}.");
                    }
                    catch (Exception ex) // TODO: davidni: not fatal?
                    {
                        // Not user's problem
                        Log.ErrorLoadingServiceConfig(_logger, service.ServiceName, ex);
                    }
                }
            }

            Log.ServiceDiscovered(_logger, discoveredBackends.Count, discoveredRoutes.Count);
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

        private DestinationConfig BuildDestination(ReplicaWrapper replica, string listenerName, string healthListenerName, PartitionWrapper partition)
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

            return new DestinationConfig
            {
                Address = endpointUri.AbsoluteUri,
                Health = healthEndpointUri?.AbsoluteUri,
                Metadata = new Dictionary<string, string>
                {
                    { "PartitionId", partition.Id.ToString() ?? string.Empty },
                    { "NamedPartitionName", partition.Name ?? string.Empty },
                    { "ReplicaId", replica.Id.ToString() ?? string.Empty }
                }
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
        /// and populates the specified <paramref name="cluster"/>'s <see cref="ClusterConfig.Destinations"/> accordingly.
        /// </summary>
        /// <remarks>All non-fatal exceptions are caught and logged.</remarks>
        private async Task<IReadOnlyDictionary<string, DestinationConfig>> DiscoverDestinationsAsync(
            ServiceFabricDiscoveryOptions options,
            ServiceWrapper service,
            Dictionary<string, string> serviceExtensionLabels,
            CancellationToken cancellation)
        {
            IEnumerable<PartitionWrapper> partitions;
            try
            {
                partitions = await _serviceFabricCaller.GetPartitionListAsync(service.ServiceName, cancellation);
            }
            catch (Exception ex) // TODO: davidni: not fatal?
            {
                Log.GettingPartitionFailed(_logger, service.ServiceName, ex);
                return null;
            }

            var destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase);

            var listenerName = serviceExtensionLabels.GetValueOrDefault("YARP.Backend.ServiceFabric.ListenerName", string.Empty);
            var healthListenerName = serviceExtensionLabels.GetValueOrDefault("YARP.Backend.HealthCheck.Active.ServiceFabric.ListenerName", string.Empty);
            var statefulReplicaSelectionMode = ParseStatefulReplicaSelectionMode(serviceExtensionLabels, service.ServiceName);
            foreach (var partition in partitions)
            {
                IEnumerable<ReplicaWrapper> replicas;
                try
                {
                    replicas = await _serviceFabricCaller.GetReplicaListAsync(partition.Id, cancellation);
                }
                catch (Exception ex) // TODO: davidni: not fatal?
                {
                    Log.GettingReplicaFailed(_logger, partition.Id, service.ServiceName, ex);
                    continue;
                }

                foreach (var replica in replicas)
                {
                    if (!IsHealthyReplica(replica))
                    {
                        Log.UnhealthyReplicaSkipped(_logger, replica.Id, partition.Id, service.ServiceName, replica.ReplicaStatus, replica.HealthState);
                        continue;
                    }

                    // If service is stateful, we need to determine which replica should we route to (e.g Primary, Secondary, All).
                    if (!IsReplicaEligible(replica, statefulReplicaSelectionMode))
                    {
                        // Skip this endpoint.
                        Log.IneligibleEndpointSkipped(_logger, replica.Id, service.ServiceName, statefulReplicaSelectionMode);
                        continue;
                    }

                    try
                    {
                        var destination = BuildDestination(replica, listenerName, healthListenerName, partition);

                        ReportReplicaHealth(options, service, partition.Id, replica, HealthState.Ok, $"Successfully built the endpoint from listener '{listenerName}'.");

                        // DestinationId is the concatenation of partitionId and replicaId.
                        var destinationId = $"{partition.Id}/{replica.Id}";
                        if (!destinations.TryAdd(destinationId, destination))
                        {
                            throw new ConfigException($"Duplicated endpoint id '{replica.Id}'. Skipping repeated definition for service '{service.ServiceName}'.");
                        }
                    }
                    catch (ConfigException ex)
                    {
                        // The user's problem
                        Log.InvalidReplicaConfig(_logger, replica.Id, service.ServiceName, ex);

                        // TODO: emit Error health report once we are able to detect config issues *during* (as opposed to *after*) a target service upgrade.
                        // Proactive Error health report would trigger a rollback of the target service as desired. However, an Error report after rhe fact
                        // will NOT cause a rollback and will prevent the target service from performing subsequent monitored upgrades to mitigate, making things worse.
                        ReportReplicaHealth(options, service, partition.Id, replica, HealthState.Warning, $"Could not build service endpoint: {ex.Message}");
                    }
                    catch (Exception ex) // TODO: davidni: not fatal?
                    {
                        // Not the user's problem
                        Log.ErrorLoadingReplicaConfig(_logger, replica.Id, service.ServiceName, ex);
                    }
                }
            }

            return destinations;
        }

        private StatefulReplicaSelectionMode ParseStatefulReplicaSelectionMode(Dictionary<string, string> serviceExtensionLabels, Uri serviceName)
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

            Log.InvalidReplicaSelectionMode(_logger, statefulReplicaSelectionMode, serviceName);
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
                Log.ServiceHealthReportFailed(_logger, state, serviceName, ex);
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
                    Log.ReplicaHealthReportFailedInvalidServiceKind(_logger, state, replica.Id, service.ServiceKind);
                    return;
            }

            var sendOptions = new HealthReportSendOptions { Immediate = state != HealthState.Ok }; // Report immediately if unhealthy
            try
            {
                _serviceFabricCaller.ReportHealth(healthReport, sendOptions);
            }
            catch (Exception ex) // TODO: davidni: not fatal?
            {
                Log.ReplicaHealthReportFailed(_logger, state, replica.Id, ex);
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception> _gettingServiceFabricApplicationFailed =
                LoggerMessage.Define(
                    LogLevel.Error,
                    EventIds.GettingApplicationFailed,
                    "Could not get applications list from Service Fabric, continuing with zero applications.");

            private static readonly Action<ILogger, Uri, Exception> _gettingServiceFailed =
                LoggerMessage.Define<Uri>(
                    LogLevel.Error,
                    EventIds.GettingServiceFailed,
                    "Could not get service list for application '{applicationName}', skipping application.");

            private static readonly Action<ILogger, Uri, Exception> _invalidServiceConfig =
                LoggerMessage.Define<Uri>(
                    LogLevel.Information,
                    EventIds.InvalidServiceConfig,
                    "Config error found when trying to load service '{serviceName}', skipping.");

            private static readonly Action<ILogger, Uri, Exception> _errorLoadingServiceConfig =
                LoggerMessage.Define<Uri>(
                    LogLevel.Error,
                    EventIds.ErrorLoadingServiceConfig,
                    "Unexpected error when trying to load service '{serviceName}', skipping.");

            private static readonly Action<ILogger, int, int, Exception> _serviceDiscovered =
                LoggerMessage.Define<int, int>(
                    LogLevel.Information,
                    EventIds.ServiceDiscovered,
                    "Discovered '{discoveredBackendsCount}' backends, '{discoveredRoutesCount}' routes.");

            private static readonly Action<ILogger, Uri, Exception> _gettingPartitionFailed =
                LoggerMessage.Define<Uri>(
                    LogLevel.Error,
                    EventIds.GettingPartitionFailed,
                    "Could not get partition list for service '{serviceName}', skipping endpoints.");

            private static readonly Action<ILogger, Guid, Uri, Exception> _gettingReplicaFailed =
                LoggerMessage.Define<Guid, Uri>(
                    LogLevel.Error,
                    EventIds.GettingReplicaFailed,
                    "Could not get replica list for partition '{partition}' of service '{serviceName}', skipping partition.");

            private static readonly Action<ILogger, long, Guid, Uri, ServiceReplicaStatus, HealthState, Exception> _unhealthyReplicaSkipped =
                LoggerMessage.Define<long, Guid, Uri, ServiceReplicaStatus, HealthState>(
                    LogLevel.Information,
                    EventIds.UnhealthyReplicaSkipped,
                    "Skipping unhealthy replica '{replicaId}' from partition '{partition}', service '{serviceName}': ReplicaStatus={replicaStatus}, HealthState={healthState}.");

            private static readonly Action<ILogger, long, Uri, StatefulReplicaSelectionMode, Exception> _ineligibleEndpointSkipped =
                LoggerMessage.Define<long, Uri, StatefulReplicaSelectionMode>(
                    LogLevel.Information,
                    EventIds.IneligibleEndpointSkipped,
                    "Skipping ineligible endpoint '{replicaId}' of service '{serviceName}'. statefulReplicaSelectionMode: {statefulReplicaSelectionMode}.");

            private static readonly Action<ILogger, long, Uri, Exception> _invalidReplicaConfig =
                LoggerMessage.Define<long, Uri>(
                    LogLevel.Information,
                    EventIds.InvalidReplicaConfig,
                    "Config error found when trying to build endpoint for replica '{replicaId}' of service '{serviceName}', skipping.");

            private static readonly Action<ILogger, long, Uri, Exception> _errorLoadingReplicaConfig =
                LoggerMessage.Define<long, Uri>(
                    LogLevel.Error,
                    EventIds.ErrorLoadingReplicaConfig,
                    "Could not build endpoint for replica '{replicaId}' of service '{serviceName}'.");

            private static readonly Action<ILogger, string, Uri, Exception> _invalidReplicaSelectionMode =
                LoggerMessage.Define<string, Uri>(
                    LogLevel.Warning,
                    EventIds.InvalidReplicaSelectionMode,
                    "Invalid replica selection mode: '{statefulReplicaSelectionMode}' for service '{serviceName}', fallback to selection mode: All.");

            private static readonly Action<ILogger, HealthState, Uri, Exception> _serviceHealthReportFailed =
                LoggerMessage.Define<HealthState, Uri>(
                    LogLevel.Error,
                    EventIds.ServiceHealthReportFailed,
                    "Failed to report health '{state}' for service '{serviceName}'.");

            private static readonly Action<ILogger, HealthState, long, ServiceKind, Exception> _replicaHealthReportFailedInvalidServiceKind =
                LoggerMessage.Define<HealthState, long, ServiceKind>(
                    LogLevel.Error,
                    EventIds.ReplicaHealthReportFailedInvalidServiceKind,
                    "Failed to report health '{state}' for replica '{replicaId}': unexpected ServiceKind '{serviceKind}'.");


            private static readonly Action<ILogger, HealthState, long, Exception> _replicaHealthReportFailed =
                LoggerMessage.Define<HealthState, long>(
                    LogLevel.Error,
                    EventIds.ReplicaHealthReportFailed,
                    "Failed to report health '{state}' for replica '{replicaId}'.");

            public static void GettingApplicationFailed(ILogger logger, Exception exception)
            {
                _gettingServiceFabricApplicationFailed(logger, exception);
            }

            public static void GettingServiceFailed(ILogger logger, Uri application, Exception exception)
            {
                _gettingServiceFailed(logger, application, exception);
            }

            public static void InvalidServiceConfig(ILogger logger, Uri service, Exception exception)
            {
                _invalidServiceConfig(logger, service, exception);
            }

            public static void ErrorLoadingServiceConfig(ILogger logger, Uri service, Exception exception)
            {
                _errorLoadingServiceConfig(logger, service, exception);
            }

            public static void ServiceDiscovered(ILogger logger, int discoveredBackendsCount, int discoveredRoutesCount)
            {
                _serviceDiscovered(logger, discoveredBackendsCount, discoveredRoutesCount, null);
            }

            public static void GettingPartitionFailed(ILogger logger, Uri service, Exception exception)
            {
                _gettingPartitionFailed(logger, service, exception);
            }

            public static void GettingReplicaFailed(ILogger logger, Guid partition, Uri service, Exception exception)
            {
                _gettingReplicaFailed(logger, partition, service, exception);
            }

            public static void UnhealthyReplicaSkipped(ILogger logger, long replicaId, Guid partition, Uri service, ServiceReplicaStatus replicaStatus, HealthState healthState)
            {
                _unhealthyReplicaSkipped(logger, replicaId, partition, service, replicaStatus, healthState, null);
            }

            public static void IneligibleEndpointSkipped(ILogger<Discoverer> logger, long replicaId, Uri serviceName, StatefulReplicaSelectionMode statefulReplicaSelectionMode)
            {
                _ineligibleEndpointSkipped(logger, replicaId, serviceName, statefulReplicaSelectionMode, null);
            }

            public static void InvalidReplicaConfig(ILogger<Discoverer> logger, long replicaId, Uri serviceName, Exception exception)
            {
                _invalidReplicaConfig(logger, replicaId, serviceName, exception);
            }

            public static void ErrorLoadingReplicaConfig(ILogger<Discoverer> logger, long replicaId, Uri serviceName, Exception exception)
            {
                _errorLoadingReplicaConfig(logger, replicaId, serviceName, exception);
            }

            public static void InvalidReplicaSelectionMode(ILogger<Discoverer> logger, string statefulReplicaSelectionMode, Uri serviceName)
            {
                _invalidReplicaSelectionMode(logger, statefulReplicaSelectionMode, serviceName, null);
            }

            public static void ServiceHealthReportFailed(ILogger<Discoverer> logger, HealthState state, Uri serviceName, Exception exception)
            {
                _serviceHealthReportFailed(logger, state, serviceName, exception);
            }

            public static void ReplicaHealthReportFailedInvalidServiceKind(ILogger<Discoverer> logger, HealthState state, long replicaId, ServiceKind serviceKind)
            {
                _replicaHealthReportFailedInvalidServiceKind(logger, state, replicaId, serviceKind, null);
            }

            public static void ReplicaHealthReportFailed(ILogger<Discoverer> logger, HealthState state, long replicaId, Exception exception)
            {
                _replicaHealthReportFailed(logger, state, replicaId, exception);
            }
        }
    }
}
