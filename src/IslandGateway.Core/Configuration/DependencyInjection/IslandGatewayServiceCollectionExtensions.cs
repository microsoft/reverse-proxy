// <copyright file="IslandGatewayServiceCollectionExtensions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;

namespace IslandGateway.Core.Configuration.DependencyInjection
{
    /// <summary>
    /// Extensions for <see cref="IServiceCollection"/>
    /// used to register Island Gateway's components.
    /// </summary>
    public static class IslandGatewayServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Island Gateway's services to Dependency Injection.
        /// </summary>
        public static IIslandGatewayBuilder AddIslandGateway(this IServiceCollection services)
        {
            var builder = new IslandGatewayBuilder(services);
            builder
                .AddTelemetryShims()
                .AddMetrics()
                .AddInMemoryRepos()
                .AddConfigBuilder()
                .AddRuntimeStateManagers()
                .AddConfigManager()
                .AddDynamicEndpointDataSource()
                .AddProxy()
                .AddBackgroundWorkers();

            return builder;
        }
    }
}
