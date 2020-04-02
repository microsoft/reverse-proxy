// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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
        /// Adds Reverse Proxy routes to the route table.
        /// </summary>
        public static void MapReverseProxy(this IEndpointRouteBuilder endpoints)
        {
            var dataSource = (EndpointDataSource)endpoints.ServiceProvider.GetRequiredService<IProxyDynamicEndpointDataSource>();
            endpoints.DataSources.Add(dataSource);
        }
    }
}
