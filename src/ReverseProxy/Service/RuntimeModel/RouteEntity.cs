// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;

namespace Yarp.ReverseProxy.RuntimeModel
{
    /// <summary>
    /// Representation of a route for use at runtime.
    /// </summary>
    /// <remarks>
    /// Note that while this class is immutable, specific members such as <see cref="Model"/> hold mutable
    /// references that can be updated atomically and which will always have latest information.
    /// All members are thread safe.
    /// </remarks>
    internal sealed class RouteEntity
    {
        private volatile RouteModel _model;

        public RouteEntity(string routeId)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                throw new ArgumentNullException(nameof(routeId));
            }
            RouteId = routeId;
        }

        public string RouteId { get; }

        /// <summary>
        /// Encapsulates parts of a route that can change atomically
        /// in reaction to config changes.
        /// </summary>
        internal RouteModel Model
        {
            get => _model;
            set => _model = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Tracks changes to the cluster configuration for use with rebuilding the route endpoint.
        /// </summary>
        internal int? ClusterRevision { get; set; }

        /// <summary>
        /// A cached Endpoint that will be cleared and rebuilt if the RouteConfig or cluster config change.
        /// </summary>
        internal Endpoint CachedEndpoint { get; set; }
    }
}
