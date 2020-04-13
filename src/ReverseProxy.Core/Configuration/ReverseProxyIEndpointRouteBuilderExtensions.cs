// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Core.Middleware;
using Microsoft.ReverseProxy.Core.Service;

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
                app.UseMiddleware<LoadBalancingMiddleware>();
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
            appBuilder.UseMiddleware<EndpointInitializerMiddleware>();
            configureApp(appBuilder);
            appBuilder.UseMiddleware<ProxyInvokerMiddleware>();
            var app = appBuilder.Build();

            var routeBuilder = endpoints.ServiceProvider.GetRequiredService<IRuntimeRouteBuilder>();
            routeBuilder.SetProxyPipeline(app);

            var dataSource = (EndpointDataSource)endpoints.ServiceProvider.GetRequiredService<IProxyDynamicEndpointDataSource>();
            endpoints.DataSources.Add(dataSource);
        }
    }
}
