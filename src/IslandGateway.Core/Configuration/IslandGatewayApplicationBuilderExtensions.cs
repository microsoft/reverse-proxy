// <copyright file="IslandGatewayApplicationBuilderExtensions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Core.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IslandGateway.Core
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/>
    /// used to add Island Gateway to the ASP .NET Core request pipeline.
    /// </summary>
    public static class IslandGatewayApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds Island Gateway to the ASP .NET Core request pipeline.
        /// </summary>
        public static IApplicationBuilder UseIslandGateway(this IApplicationBuilder app)
        {
            // Branch the pipeline so that our endpoints don't interfere with any already setup in the current pipeline
            return app.Map(
                PathString.Empty,
                branched =>
                {
                    branched.UseIslandGatewayOnBranchedPipeline();
                });
        }

        // NOTE: This is a separate method to help ensure no other middlewares are mistakenly added to the parent pipeline
        private static void UseIslandGatewayOnBranchedPipeline(this IApplicationBuilder app)
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
                var dataSource = (EndpointDataSource)endpoints.ServiceProvider.GetRequiredService<IGatewayDynamicEndpointDataSource>();
                endpoints.DataSources.Add(dataSource);
            });
        }
    }
}
