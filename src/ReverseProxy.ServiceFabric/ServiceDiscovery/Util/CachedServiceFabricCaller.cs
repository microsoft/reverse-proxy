// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Fabric.Health;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.ServiceFabric.Utilities;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.ServiceFabric
{
    internal sealed class CachedServiceFabricCaller : ICachedServiceFabricCaller
    {
        public static readonly TimeSpan CacheExpirationOffset = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(60);
        private readonly ILogger<CachedServiceFabricCaller> _logger;
        private readonly IClock _clock;
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
            ILogger<CachedServiceFabricCaller> logger,
            IClock clock,
            IQueryClientWrapper queryClientWrapper,
            IServiceManagementClientWrapper serviceManagementClientWrapper,
            IPropertyManagementClientWrapper propertyManagementClientWrapper,
            IHealthClientWrapper healthClientWrapper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _queryClientWrapper = queryClientWrapper ?? throw new ArgumentNullException(nameof(queryClientWrapper));
            _serviceManagementClientWrapper = serviceManagementClientWrapper ?? throw new ArgumentNullException(nameof(serviceManagementClientWrapper));
            _propertyManagementClientWrapper = propertyManagementClientWrapper ?? throw new ArgumentNullException(nameof(propertyManagementClientWrapper));
            _healthClientWrapper = healthClientWrapper ?? throw new ArgumentNullException(nameof(healthClientWrapper));

            _applicationListCache = new Cache<IEnumerable<ApplicationWrapper>>(_clock, CacheExpirationOffset);
            _serviceListCache = new Cache<IEnumerable<ServiceWrapper>>(_clock, CacheExpirationOffset);
            _partitionListCache = new Cache<IEnumerable<Guid>>(_clock, CacheExpirationOffset);
            _replicaListCache = new Cache<IEnumerable<ReplicaWrapper>>(_clock, CacheExpirationOffset);
            _serviceManifestCache = new Cache<string>(_clock, CacheExpirationOffset);
            _serviceManifestNameCache = new Cache<string>(_clock, CacheExpirationOffset);
            _propertiesCache = new Cache<IDictionary<string, string>>(_clock, CacheExpirationOffset);
        }

        public async Task<IEnumerable<ApplicationWrapper>> GetApplicationListAsync(CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "GetApplicationList",
                func: () => _queryClientWrapper.GetApplicationListAsync(timeout: _defaultTimeout, cancellationToken: cancellation),
                cache: _applicationListCache,
                key: string.Empty,
                cancellation);
        }

        public async Task<IEnumerable<ServiceWrapper>> GetServiceListAsync(Uri applicationName, CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "GetServiceList",
                func: () => _queryClientWrapper.GetServiceListAsync(applicationName: applicationName, timeout: _defaultTimeout, cancellation),
                cache: _serviceListCache,
                key: applicationName.ToString(),
                cancellation);
        }

        public async Task<IEnumerable<Guid>> GetPartitionListAsync(Uri serviceName, CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "GetPartitionList",
                func: () => _queryClientWrapper.GetPartitionListAsync(serviceName: serviceName, timeout: _defaultTimeout, cancellation),
                cache: _partitionListCache,
                key: serviceName.ToString(),
                cancellation);
        }

        public async Task<IEnumerable<ReplicaWrapper>> GetReplicaListAsync(Guid partition, CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "GetReplicaList",
                func: () => _queryClientWrapper.GetReplicaListAsync(partitionId: partition, timeout: _defaultTimeout, cancellation),
                cache: _replicaListCache,
                key: partition.ToString(),
                cancellation);
        }

        public async Task<string> GetServiceManifestAsync(string applicationTypeName, string applicationTypeVersion, string serviceManifestName, CancellationToken cancellation)
        {
            return await TryWithCacheFallbackAsync(
                operationName: "GetServiceManifest",
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
                operationName: "GetServiceManifestName",
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
                operationName: "EnumerateProperties",
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
            Uri serviceName = null;
            if (healthReport is ServiceHealthReport service)
            {
                serviceName = service.ServiceName;
            }
            Log.ReportHealth(_logger, healthReport.Kind, healthReport.HealthInformation.HealthState, healthReport.GetType().FullName, serviceName);

            _healthClientWrapper.ReportHealth(healthReport, sendOptions);
        }

        public void CleanUpExpired()
        {
            _applicationListCache.Cleanup();
            _serviceListCache.Cleanup();
            _partitionListCache.Cleanup();
            _replicaListCache.Cleanup();
            _serviceManifestCache.Cleanup();
            _serviceManifestNameCache.Cleanup();
            _propertiesCache.Cleanup();
        }

        private async Task<T> TryWithCacheFallbackAsync<T>(string operationName, Func<Task<T>> func, Cache<T> cache, string key, CancellationToken cancellation)
        {
            Log.StartCacheOperation(_logger, operationName, key);

            var outcome = "UnhandledException";
            try
            {
                Log.StartInnerCacheOperation(_logger, operationName, key);
                var value = await func();
                cache.Set(key, value);
                outcome = "Success";
                return value;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                outcome = "Canceled";
                throw;
            }
            catch (Exception)
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
                Log.CacheOperationCompleted(_logger, operationName, key, outcome);
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, HealthReportKind, HealthState, string, Uri, Exception> _reportHealth =
                LoggerMessage.Define<HealthReportKind, HealthState, string, Uri>(
                    LogLevel.Debug,
                    EventIds.ErrorSignalingChange,
                    "Reporting health, kind='{healthReportKind}', healthState='{healthState}', type='{healthReportType}', serviceName='{serviceName}'");

            private static readonly Action<ILogger, string, string, Exception> _startCacheOperation =
                LoggerMessage.Define<string, string>(
                    LogLevel.Debug,
                    EventIds.StartCacheOperation,
                    "Starting operation '{cacheOperationName}'.Cache, key='{cacheKey}'");

            private static readonly Action<ILogger, string, string, Exception> _startInnerCacheOperation =
                LoggerMessage.Define<string, string>(
                    LogLevel.Debug,
                    EventIds.StartInnerCacheOperation,
                    "Starting inner operation '{cacheOperationName}', key='{cacheKey}'");

            private static readonly Action<ILogger, string, string, string, Exception> _cacheOperationCompleted =
                LoggerMessage.Define<string, string, string>(
                    LogLevel.Information,
                    EventIds.CacheOperationCompleted,
                    "Operation '{cacheOperationName}'.Cache completed with key='{cacheKey}', outcome='{cacheOperationOutcome}'");

            public static void ReportHealth(ILogger<CachedServiceFabricCaller> logger, HealthReportKind kind, HealthState healthState, string healthReportType, Uri serviceName)
            {
                _reportHealth(logger, kind, healthState, healthReportType, serviceName, null);
            }

            public static void StartCacheOperation(ILogger<CachedServiceFabricCaller> logger, string cacheOperationName, string cacheKey)
            {
                _startCacheOperation(logger, cacheOperationName, cacheKey, null);
            }

            public static void StartInnerCacheOperation(ILogger<CachedServiceFabricCaller> logger, string cacheOperationName, string cacheKey)
            {
                _startInnerCacheOperation(logger, cacheOperationName, cacheKey, null);
            }

            public static void CacheOperationCompleted(ILogger<CachedServiceFabricCaller> logger, string cacheOperationName, string cacheKey, string cacheOperationOutcome)
            {
                _cacheOperationCompleted(logger, cacheOperationName, cacheKey, cacheOperationOutcome, null);
            }
        }
    }
}
