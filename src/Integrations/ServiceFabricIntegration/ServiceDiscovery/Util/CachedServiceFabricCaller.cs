// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Fabric.Health;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Abstractions.Time;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    internal sealed class CachedServiceFabricCaller : IServiceFabricCaller
    {
        public static readonly TimeSpan CacheExpirationOffset = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        private readonly ILogger logger;
        private readonly IOperationLogger<CachedServiceFabricCaller> operationLogger;
        private readonly IMonotonicTimer timer;
        private readonly IQueryClientWrapper queryClientWrapper;
        private readonly IServiceManagementClientWrapper serviceManagementClientWrapper;
        private readonly IPropertyManagementClientWrapper propertyManagementClientWrapper;
        private readonly IHealthClientWrapper healthClientWrapper;

        private readonly Cache<IEnumerable<ApplicationWrapper>> applicationListCache;
        private readonly Cache<IEnumerable<ServiceWrapper>> serviceListCache;
        private readonly Cache<IEnumerable<Guid>> partitionListCache;
        private readonly Cache<IEnumerable<ReplicaWrapper>> replicaListCache;
        private readonly Cache<string> serviceManifestCache;
        private readonly Cache<string> serviceManifestNameCache;
        private readonly Cache<IDictionary<string, string>> propertiesCache;

        public CachedServiceFabricCaller(
            ILogger<CachedServiceFabricCaller> logger,
            IOperationLogger<CachedServiceFabricCaller> operationLogger,
            IMonotonicTimer timer,
            IQueryClientWrapper queryClientWrapper,
            IServiceManagementClientWrapper serviceManagementClientWrapper,
            IPropertyManagementClientWrapper propertyManagementClientWrapper,
            IHealthClientWrapper healthClientWrapper)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(operationLogger, nameof(operationLogger));
            Contracts.CheckValue(timer, nameof(timer));
            Contracts.CheckValue(queryClientWrapper, nameof(queryClientWrapper));
            Contracts.CheckValue(serviceManagementClientWrapper, nameof(serviceManagementClientWrapper));
            Contracts.CheckValue(propertyManagementClientWrapper, nameof(propertyManagementClientWrapper));
            Contracts.CheckValue(healthClientWrapper, nameof(healthClientWrapper));

            this.logger = logger;
            this.operationLogger = operationLogger;
            this.timer = timer;
            this.queryClientWrapper = queryClientWrapper;
            this.serviceManagementClientWrapper = serviceManagementClientWrapper;
            this.propertyManagementClientWrapper = propertyManagementClientWrapper;
            this.healthClientWrapper = healthClientWrapper;

            this.applicationListCache = new Cache<IEnumerable<ApplicationWrapper>>(this.timer, CacheExpirationOffset);
            this.serviceListCache = new Cache<IEnumerable<ServiceWrapper>>(this.timer, CacheExpirationOffset);
            this.partitionListCache = new Cache<IEnumerable<Guid>>(this.timer, CacheExpirationOffset);
            this.replicaListCache = new Cache<IEnumerable<ReplicaWrapper>>(this.timer, CacheExpirationOffset);
            this.serviceManifestCache = new Cache<string>(this.timer, CacheExpirationOffset);
            this.serviceManifestNameCache = new Cache<string>(this.timer, CacheExpirationOffset);
            this.propertiesCache = new Cache<IDictionary<string, string>>(this.timer, CacheExpirationOffset);
        }

        public async Task<IEnumerable<ApplicationWrapper>> GetApplicationListAsync(CancellationToken cancellation)
        {
            return await this.TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetApplicationList",
                func: () => this.queryClientWrapper.GetApplicationListAsync(timeout: DefaultTimeout, cancellationToken: cancellation),
                cache: this.applicationListCache,
                key: string.Empty,
                cancellation);
        }
        public async Task<IEnumerable<ServiceWrapper>> GetServiceListAsync(Uri applicationName, CancellationToken cancellation)
        {
            return await this.TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetServiceList",
                func: () => this.queryClientWrapper.GetServiceListAsync(applicationName: applicationName, timeout: DefaultTimeout, cancellation),
                cache: this.serviceListCache,
                key: applicationName.ToString(),
                cancellation);
        }

        public async Task<IEnumerable<Guid>> GetPartitionListAsync(Uri serviceName, CancellationToken cancellation)
        {
            return await this.TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetPartitionList",
                func: () => this.queryClientWrapper.GetPartitionListAsync(serviceName: serviceName, timeout: DefaultTimeout, cancellation),
                cache: this.partitionListCache,
                key: serviceName.ToString(),
                cancellation);
        }

        public async Task<IEnumerable<ReplicaWrapper>> GetReplicaListAsync(Guid partition, CancellationToken cancellation)
        {
            return await this.TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetReplicaList",
                func: () => this.queryClientWrapper.GetReplicaListAsync(partitionId: partition, timeout: DefaultTimeout, cancellation),
                cache: this.replicaListCache,
                key: partition.ToString(),
                cancellation);
        }

        public async Task<string> GetServiceManifestAsync(string applicationTypeName, string applicationTypeVersion, string serviceManifestName, CancellationToken cancellation)
        {
            return await this.TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetServiceManifest",
                func: () => this.serviceManagementClientWrapper.GetServiceManifestAsync(
                    applicationTypeName: applicationTypeName,
                    applicationTypeVersion: applicationTypeVersion,
                    serviceManifestName: serviceManifestName,
                    timeout: DefaultTimeout,
                    cancellation),
                cache: this.serviceManifestCache,
                key: $"{Uri.EscapeDataString(applicationTypeName)}:{Uri.EscapeDataString(applicationTypeVersion)}:{Uri.EscapeDataString(serviceManifestName)}",
                cancellation);
        }

        public async Task<string> GetServiceManifestName(string applicationTypeName, string applicationTypeVersion, string serviceTypeNameFilter, CancellationToken cancellation)
        {
            return await this.TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.GetServiceManifestName",
                func: () => this.queryClientWrapper.GetServiceManifestName(
                    applicationTypeName: applicationTypeName,
                    applicationTypeVersion: applicationTypeVersion,
                    serviceTypeNameFilter: serviceTypeNameFilter,
                    timeout: DefaultTimeout,
                    cancellationToken: cancellation),
                cache: this.serviceManifestNameCache,
                key: $"{Uri.EscapeDataString(applicationTypeName)}:{Uri.EscapeDataString(applicationTypeVersion)}:{Uri.EscapeDataString(serviceTypeNameFilter)}",
                cancellation);
        }

        public async Task<IDictionary<string, string>> EnumeratePropertiesAsync(Uri parentName, CancellationToken cancellation)
        {
            return await this.TryWithCacheFallbackAsync(
                operationName: "IslandGateway.ServiceFabric.EnumerateProperties",
                func: () => this.propertyManagementClientWrapper.EnumeratePropertiesAsync(
                    parentName: parentName,
                    timeout: DefaultTimeout,
                    cancellationToken: cancellation),
                cache: this.propertiesCache,
                key: parentName.ToString(),
                cancellation);
        }

        public void ReportHealth(HealthReport healthReport, HealthReportSendOptions sendOptions)
        {
            this.operationLogger.Execute(
                "IslandGateway.ServiceFabric.ReportHealth",
                () =>
                {
                    var operationContext = this.operationLogger.Context;
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

                    this.healthClientWrapper.ReportHealth(healthReport, sendOptions);
                });
        }

        private async Task<T> TryWithCacheFallbackAsync<T>(string operationName, Func<Task<T>> func, Cache<T> cache, string key, CancellationToken cancellation)
        {
            return await this.operationLogger.ExecuteAsync(
                operationName + ".Cache",
                async () =>
                {
                    var operationContext = this.operationLogger.Context;
                    operationContext.SetProperty("key", key);

                    // TODO: trigger async cache cleanup before SF call
                    string outcome = "UnhandledException";
                    try
                    {
                        var value = await this.operationLogger.ExecuteAsync(
                            operationName,
                            () =>
                            {
                                var innerOperationContext = this.operationLogger.Context;
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
                        if (cache.TryGetValue(key, out T value))
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
