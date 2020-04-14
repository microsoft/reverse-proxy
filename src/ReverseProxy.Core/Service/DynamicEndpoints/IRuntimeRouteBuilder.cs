// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Core.ConfigModel;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace Microsoft.ReverseProxy.Core.Service
{
    /// <summary>
    /// Interface for a class that transforms Core Proxy routes
    /// into corresponding ASP .NET Core endpoints
    /// with applicable constraints and metadata.
    /// </summary>
    internal interface IRuntimeRouteBuilder
    {
        /// <summary>
        /// Converts the provided <paramref name="source"/> route information
        /// into the corresponding runtime representation used by Reverse Proxy
        /// This includes computing the set of ASP .NET Core endpoints corresponding to the given route.
        /// </summary>
        /// <param name="source">
        /// Parsed proxy route, this is the source of settings that are used to create the new runtime objects.
        /// </param>
        /// <param name="backendOrNull">Backend that this route maps to.</param>
        /// <param name="runtimeRoute">
        /// Representation of the route during runtime,
        /// whose <see cref="RouteInfo.Config"/> property is updated with a new objected computed from
        /// <paramref name="source"/> and <paramref name="backendOrNull"/>.
        /// </param>
        RouteConfig Build(ParsedRoute source, BackendInfo backendOrNull, RouteInfo runtimeRoute);

        /// <summary>
        /// Sets the middleware pipeline to use when building routes.
        /// </summary>
        void SetProxyPipeline(RequestDelegate pipeline);
    }
}
