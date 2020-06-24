// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Fabric.Health;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.ServiceFabric.Utilities;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    internal sealed class CachedServiceFabricCaller : IServiceFabricCaller
    {
        public static readonly TimeSpan CacheExpirationOffset = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(60);

        private readonly IOperationLogger<CachedServiceFabricCaller> _operationLogger;
        private readonly IMonotonicTimer _timer;
        private readonly IQueryClientWrapper _queryClientWrapper;
        private readonly IServiceManagementClientWrapper _serviceManagementClientWrapper;
        private readonly IPropertyManagementClientWrapper _propertyManagementClientWrapper;
        private readonly IHealthClientWrapper _healthClientWrapper;

        private readonly Cache<IEnumerable<ApplicationWrapper>> _applicationListCache;
        private readonly Cache<IEnumerable<ServiceWrapper>> _serviceListCache;
        private readonly Cache<IEnumerable<Guid>> _partitionListCache;
        private readonly Cache<IEnumerable<ReplicaWrapper>> _replicaListCache;
        private readonly Cache<string> _serviceManifestCache;
        private readonly Cache<string> _serviceManifestNameCache;
        private readonly Cache<IDictionary<string, string>> _propertiesCache;

        public CachedServiceFabricCaller(
            IOperationLogger<CachedServiceFabricCaller> operationLogger,
            IMonotonicTimer timer,
            IQueryClientWrapper queryClientWrapper,
            IServiceManagementClientWrapper serviceManagementClientWrapper,
            IPropertyManagementClientWrapper propertyManagementClientWrapper,
            IHealthClientWrapper healthClientWrapper)
        {
            _operationLogger = operationLogger ?? throw new ArgumentNullException(nameof(operationLogger));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _queryClientWrapper = queryClientWrapper ?? throw new ArgumentNullException(nameof(queryClientWrapper));
            _serviceManagementClientWrapper = serviceManagementClientWrapper ?? throw new ArgumentNullException(nameof(serviceManagementClientWrapper));
            _propertyManagementClientWrapper = propertyManagementClientWrapper ?? throw new ArgumentNullException(nameof(propertyManagementClientWrapper));
            _healthClientWrapper = healthClientWrapper ?? throw new ArgumentNullException(nameof(healthClientWrapper));

            _applicationListCache = new Cache<IEnumerable<ApplicationWrapper>>(_timer, CacheExpirationOffset);
            _serviceListCache = new Cache<IEnumerable<ServiceWrapper>>(_timer, CacheExpirationOffset);
            _partitionListCache = new Cache<IEnumerable<Guid>>(_timer, CacheExpirationOffset);
            _replicaListCache = new Cache<IEnumerable<ReplicaWrapper>>(_timer, CacheExpirationOffset);
            _serviceManifestCache = new Cache<string>(_timer, CacheExpirationOffset);
            _serviceManifestNameCache = new Cache<string>(_timer, CacheExpirationOffset);
            _propertiesCache = new Cache<IDictionary<string, string>>(_timer, CacheExpirationOffset);
        }

        public async Task<IEnumerable<ApplicationWrapper>> GetApplicationListAsync(CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetApplicationList",
                func: () => _queryClientWrapper.GetApplicationListAsync(timeout: _defaultTimeout, cancellationToken: cancellation),
                cache: _applicationListCache,
                key: string.Empty,
                cancellation);
        }
        public async Task<IEnumerable<ServiceWrapper>> GetServiceListAsync(Uri applicationName, CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetServiceList",
                func: () => _queryClientWrapper.GetServiceListAsync(applicationName: applicationName, timeout: _defaultTimeout, cancellation),
                cache: _serviceListCache,
                key: applicationName.ToString(),
                cancellation);
        }

        public async Task<IEnumerable<Guid>> GetPartitionListAsync(Uri serviceName, CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetPartitionList",
                func: () => _queryClientWrapper.GetPartitionListAsync(serviceName: serviceName, timeout: _defaultTimeout, cancellation),
                cache: _partitionListCache,
                key: serviceName.ToString(),
                cancellation);
        }

        public async Task<IEnumerable<ReplicaWrapper>> GetReplicaListAsync(Guid partition, CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetReplicaList",
                func: () => _queryClientWrapper.GetReplicaListAsync(partitionId: partition, timeout: _defaultTimeout, cancellation),
                cache: _replicaListCache,
                key: partition.ToString(),
                cancellation);
        }

        public async Task<string> GetServiceManifestAsync(string applicationTypeName, string applicationTypeVersion, string serviceManifestName, CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetServiceManifest",
                func: () => _serviceManagementClientWrapper.GetServiceManifestAsync(
                    applicationTypeName: applicationTypeName,
                    applicationTypeVersion: applicationTypeVersion,
                    serviceManifestName: serviceManifestName,
                    timeout: _defaultTimeout,
                    cancellation),
                cache: _serviceManifestCache,
                key: $"{Uri.EscapeDataString(applicationTypeName)}:{Uri.EscapeDataString(applicationTypeVersion)}:{Uri.EscapeDataString(serviceManifestName)}",
                cancellation);
        }

        public async Task<string> GetServiceManifestName(string applicationTypeName, string applicationTypeVersion, string serviceTypeNameFilter, CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetServiceManifestName",
                func: () => _queryClientWrapper.GetServiceManifestName(
                    applicationTypeName: applicationTypeName,
                    applicationTypeVersion: applicationTypeVersion,
                    serviceTypeNameFilter: serviceTypeNameFilter,
                    timeout: _defaultTimeout,
                    cancellationToken: cancellation),
                cache: _serviceManifestNameCache,
                key: $"{Uri.EscapeDataString(applicationTypeName)}:{Uri.EscapeDataString(applicationTypeVersion)}:{Uri.EscapeDataString(serviceTypeNameFilter)}",
                cancellation);
        }

        public async Task<IDictionary<string, string>> EnumeratePropertiesAsync(Uri parentName, CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.EnumerateProperties",
                func: () => _propertyManagementClientWrapper.EnumeratePropertiesAsync(
                    parentName: parentName,
                    timeout: _defaultTimeout,
                    cancellationToken: cancellation),
                cache: _propertiesCache,
                key: parentName.ToString(),
                cancellation);
        }

        public void ReportHealth(HealthReport healthReport, HealthReportSendOptions sendOptions)
        {
            _operationLogger.Execute(
                "IslandGateway.ServiceFabric.ReportHealth",
                () =>
                {
                    var operationContext = _operationLogger.Context;
                    operationContext.SetProperty("kind", healthReport.Kind.ToString());
                    operationContext.SetProperty("healthState", healthReport.HealthInformation.HealthState.ToString());
                    switch (healthReport)
                    {
                        case ServiceHealthReport service:
                            operationContext.SetProperty("serviceName", service.ServiceName.ToString());
                            break;
                        default:
                            operationContext.SetProperty("type", healthReport.GetType().FullName);
                            break;
                    }

                    _healthClientWrapper.ReportHealth(healthReport, sendOptions);
                });
        }

        private async Task<T> TryWithCacheFallbackAsync<T>(string operationName, Func<Task<T>> func, Cache<T> cache, string key, CancellationToken cancellation)
        {
            return await _operationLogger.ExecuteAsync(
                operationName + ".Cache",
                async () =>
                {
                    var operationContext = _operationLogger.Context;
                    operationContext.SetProperty("key", key);

                    // TODO: trigger async cache cleanup before SF call
                    var outcome = "UnhandledException";
                    try
                    {
                        var value = await _operationLogger.ExecuteAsync(
                            operationName,
                            () =>
                            {
                                var innerOperationContext = _operationLogger.Context;
                                innerOperationContext.SetProperty("key", key);

                                return func();
                            });
                        cache.Set(key, value);
                        outcome = "Success";
                        return value;
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        outcome = "Canceled";
                        throw;
                    }
                    catch (Exception ex) when (!ex.IsFatal())
                    {
                        if (cache.TryGetValue(key, out var value))
                        {
                            outcome = "CacheFallback";
                            return value;
                        }
                        else
                        {
                            outcome = "Error";
                            throw;
                        }
                    }
                    finally
                    {
                        operationContext.SetProperty("outcome", outcome);
                    }
                });
        }
    }
}
