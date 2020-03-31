// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Core.Service;

namespace Microsoft.ReverseProxy.Core
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/>
    /// used to add Reverse Proxy to the ASP .NET Core request pipeline.
    /// </summary>
    public static class ReverseProxyApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds Reverse Proxy to the ASP .NET Core request pipeline.
        /// </summary>
        public static IApplicationBuilder UseReverseProxy(this IApplicationBuilder app)
        {
            // Branch the pipeline so that our endpoints don't interfere with any already setup in the current pipeline
            return app.Map(
                PathString.Empty,
                branched =>
                {
                    branched.UseReverseProxyOnBranchedPipeline();
                });
        }

        // NOTE: This is a separate method to help ensure no other middlewares are mistakenly added to the parent pipeline
        private static void UseReverseProxyOnBranchedPipeline(this IApplicationBuilder app)
        {
            app.UseRouting();

            app.Use(
                async (context, next) =>
                {
                    // TODO: Remove debug point
                    await next();
                });

            app.UseEndpoints(endpoints =>
            {
                var dataSource = (EndpointDataSource)endpoints.ServiceProvider.GetRequiredService<IProxyDynamicEndpointDataSource>();
                endpoints.DataSources.Add(dataSource);
            });
        }
    }
}
