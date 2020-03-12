// <copyright file="IRuntimeRouteBuilder.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Core.ConfigModel;
using IslandGateway.Core.RuntimeModel;

namespace IslandGateway.Core.Service
{
    /// <summary>
    /// Interface for a class that transforms Core Gateway routes
    /// into corresponding ASP .NET Core endpoints
    /// with applicable constraints and metadata.
    /// </summary>
    internal interface IRuntimeRouteBuilder
    {
        /// <summary>
        /// Converts the provided <paramref name="source"/> route information
        /// into the corresponding runtime representation used by Island Gateway
        /// This includes computing the set of ASP .NET Core endpoints corresponding to the given route.
        /// </summary>
        /// <param name="source">
        /// Parsed gateway route, this is the source of settings that are used to create the new runtime objects.
        /// </param>
        /// <param name="backendOrNull">Backend that this route maps to.</param>
        /// <param name="runtimeRoute">
        /// Representation of the route during runtime,
        /// whose <see cref="RouteInfo.Config"/> property is updated with a new objected computed from
        /// <paramref name="source"/> and <paramref name="backendOrNull"/>.
        /// </param>
        RouteConfig Build(ParsedRoute source, BackendInfo backendOrNull, RouteInfo runtimeRoute);
    }
}