// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Middleware;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Extension methods for <see cref="IEndpointRouteBuilder"/>
    /// used to add Reverse Proxy to the ASP .NET Core request pipeline.
    /// </summary>
    public static class ReverseProxyIEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Adds Reverse Proxy routes to the route table using the default processing pipeline.
        /// </summary>
        public static void MapReverseProxy(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapReverseProxy(app =>
            {
                app.UseAffinitizedDestinationLookup();
                app.UseProxyLoadBalancing();
                app.UseRequestAffinitizer();
            });
        }

        /// <summary>
        /// Adds Reverse Proxy routes to the route table with the customized processing pipeline. The pipeline includes
        /// by default the initialization step and the final proxy step, but not LoadBalancingMiddleware or other intermediate components.
        /// </summary>
        public static void MapReverseProxy(this IEndpointRouteBuilder endpoints, Action<IApplicationBuilder> configureApp)
        {
            if (endpoints is null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }
            if (configureApp is null)
            {
                throw new ArgumentNullException(nameof(configureApp));
            }

            var appBuilder = endpoints.CreateApplicationBuilder();
            appBuilder.UseMiddleware<DestinationInitializerMiddleware>();
            configureApp(appBuilder);
            appBuilder.UseMiddleware<ProxyInvokerMiddleware>();
            var app = appBuilder.Build();

            var routeBuilder = endpoints.ServiceProvider.GetRequiredService<IRuntimeRouteBuilder>();
            routeBuilder.SetProxyPipeline(app);

            var configManager = endpoints.ServiceProvider.GetRequiredService<IProxyConfigManager>();

            // Config validation is async but startup is sync. We want this to block so that A) any validation errors can prevent
            // the app from starting, and B) so that all the config is ready before the server starts accepting requests.
            // Reloads will be async.
            var dataSource = configManager.LoadAsync().GetAwaiter().GetResult();
            endpoints.DataSources.Add(dataSource);
        }
    }
}
