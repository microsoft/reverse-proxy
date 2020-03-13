// <copyright file="RouteInfo.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Utilities;
using IslandGateway.Signals;

namespace IslandGateway.Core.RuntimeModel
{
    /// <summary>
    /// Representation of a route for use at runtime.
    /// </summary>
    /// <remarks>
    /// Note that while this class is immutable, specific members such as
    /// <see cref="Config"/> use <see cref="Signal{T}"/> to hold mutable references
    /// that can be updated atomically and which will always have latest information.
    /// All members are thread safe.
    /// </remarks>
    internal sealed class RouteInfo
    {
        public RouteInfo(string routeId)
        {
            Contracts.CheckNonEmpty(routeId, nameof(routeId));
            RouteId = routeId;
        }

        public string RouteId { get; }

        /// <summary>
        /// Encapsulates parts of a route that can change atomically
        /// in reaction to config changes.
        /// </summary>
        public Signal<RouteConfig> Config { get; } = SignalFactory.Default.CreateSignal<RouteConfig>();
    }
}
