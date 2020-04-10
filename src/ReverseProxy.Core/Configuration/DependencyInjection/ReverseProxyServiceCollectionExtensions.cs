// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.ReverseProxy.Core.Configuration.DependencyInjection
{
    /// <summary>
    /// Extensions for <see cref="IServiceCollection"/>
    /// used to register the ReverseProxy's components.
    /// </summary>
    public static class ReverseProxyServiceCollectionExtensions
    {
        /// <summary>
        /// Adds ReverseProxy's services to Dependency Injection.
        /// </summary>
        public static IReverseProxyBuilder AddReverseProxy(this IServiceCollection services)
        {
            var builder = new ReverseProxyBuilder(services);
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

        /// <summary>
        /// Loads routes and endpoints from config.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="config"></param>
        /// <param name="reloadOnChange"></param>
        /// <returns></returns>
        public static IReverseProxyBuilder LoadFromConfig(this IReverseProxyBuilder builder, IConfiguration config, bool reloadOnChange = true)
        {
            builder.Services.Configure<ProxyConfigOptions>(config);
            builder.Services.Configure<ProxyConfigOptions>(options => options.ReloadOnChange = reloadOnChange);
            builder.Services.AddHostedService<ProxyConfigLoader>();

            return builder;
        }
    }
}
