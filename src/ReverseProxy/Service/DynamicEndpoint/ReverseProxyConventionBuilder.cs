using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.ReverseProxy
{
    public class ReverseProxyConventionBuilder : IEndpointConventionBuilder
    {
        // The lock is shared with the data source.
        private readonly object _lock;
        private readonly List<Action<EndpointBuilder>> _conventions;

        internal ReverseProxyConventionBuilder(object @lock, List<Action<EndpointBuilder>> conventions)
        {
            _lock = @lock;
            _conventions = conventions;
        }

        /// <summary>
        /// Adds the specified convention to the builder. Conventions are used to customize <see cref="EndpointBuilder"/> instances.
        /// </summary>
        /// <param name="convention">The convention to add to the builder.</param>
        public void Add(Action<EndpointBuilder> convention)
        {
            _ = convention ?? throw new ArgumentNullException(nameof(convention));

            // The lock is shared with the data source. We want to lock here
            // to avoid mutating this list while its read in the data source.
            lock (_lock)
            {
                _conventions.Add(convention);
            }
        }

        /// <summary>
        /// Configures the endpoint for all routes 
        /// </summary>
        /// <param name="convention">The convention to add to the builder.</param>
        /// <returns></returns>
        public ReverseProxyConventionBuilder Configure(Action<IEndpointConventionBuilder> convention)
        {
            _ = convention ?? throw new ArgumentNullException(nameof(convention));

            void Action(EndpointBuilder endpointBuilder)
            {
                var conventionBuilder = new EndpointBuilderConventionBuilder(endpointBuilder);
                convention(conventionBuilder);
            }

            Add(Action);

            return this;
        }

        /// <summary>
        /// Configures the endpoint for all routes 
        /// </summary>
        /// <param name="convention">The convention to add to the builder.</param>
        /// <returns></returns>
        public ReverseProxyConventionBuilder Configure(Action<IEndpointConventionBuilder, ProxyRoute> convention)
        {
            _ = convention ?? throw new ArgumentNullException(nameof(convention));

            void Action(EndpointBuilder endpointBuilder)
            {
                var routeConfig = endpointBuilder.Metadata.OfType<RouteConfig>().Single();

                var proxyRoute = routeConfig.ProxyRoute;
                if (proxyRoute != null)
                {
                    var conventionBuilder = new EndpointBuilderConventionBuilder(endpointBuilder);
                    convention(conventionBuilder, proxyRoute.DeepClone());
                }
            }

            Add(Action);

            return this;
        }

        /// <summary>
        /// Configures the endpoint for all routes 
        /// </summary>
        /// <param name="convention">The convention to add to the builder.</param>
        /// <returns></returns>
        public ReverseProxyConventionBuilder Configure(Action<IEndpointConventionBuilder, ProxyRoute, Cluster> convention)
        {
            _ = convention ?? throw new ArgumentNullException(nameof(convention));

            void Action(EndpointBuilder endpointBuilder)
            {
                var routeConfig = endpointBuilder.Metadata.OfType<RouteConfig>().Single();

                var cluster = routeConfig.Cluster?.Config.Cluster;
                var proxyRoute = routeConfig.ProxyRoute;
                if (proxyRoute != null && cluster != null)
                {
                    var conventionBuilder = new EndpointBuilderConventionBuilder(endpointBuilder);
                    convention(conventionBuilder, proxyRoute.DeepClone(), cluster.DeepClone());
                }
            }

            Add(Action);

            return this;
        }

        /// <summary>
        /// Configures the endpoint for a specific route
        /// </summary>
        /// <param name="routeId">The route id to aplly convention to its endpoint.</param>
        /// <param name="convention">The convention to add to the builder.</param>
        /// <returns></returns>
        public ReverseProxyConventionBuilder ConfigureRoute(string routeId, Action<IEndpointConventionBuilder> convention)
        {
            _ = convention ?? throw new ArgumentNullException(nameof(convention));

            void Action(EndpointBuilder endpointBuilder)
            {
                var routeConfig = endpointBuilder.Metadata.OfType<RouteConfig>().Single();
                if (routeConfig.Route.RouteId.Equals(routeId, StringComparison.OrdinalIgnoreCase))
                {
                    var conventionBuilder = new EndpointBuilderConventionBuilder(endpointBuilder);
                    convention(conventionBuilder);
                }
            }

            Add(Action);

            return this;
        }

        /// <summary>
        /// Configures the endpoint for a specific route
        /// </summary>
        /// <param name="routeId">The route id to aplly convention to its endpoint.</param>
        /// <param name="convention">The convention to add to the builder.</param>
        /// <returns></returns>
        public ReverseProxyConventionBuilder ConfigureRoute(string routeId, Action<IEndpointConventionBuilder, ProxyRoute> convention)
        {
            _ = convention ?? throw new ArgumentNullException(nameof(convention));

            void Action(EndpointBuilder endpointBuilder)
            {
                var routeConfig = endpointBuilder.Metadata.OfType<RouteConfig>().Single();
                if (routeConfig.Route.RouteId.Equals(routeId, StringComparison.OrdinalIgnoreCase))
                {
                    var proxyRoute = routeConfig.ProxyRoute;
                    if (proxyRoute != null)
                    {
                        var conventionBuilder = new EndpointBuilderConventionBuilder(endpointBuilder);
                        convention(conventionBuilder, proxyRoute.DeepClone());
                    }
                }
            }

            Add(Action);

            return this;
        }

        /// <summary>
        /// Configures the endpoint for a specific route
        /// </summary>
        /// <param name="routeId">The route id to aplly convention to its endpoint.</param>
        /// <param name="convention">The convention to add to the builder.</param>
        /// <returns></returns>
        public ReverseProxyConventionBuilder ConfigureRoute(string routeId, Action<IEndpointConventionBuilder, ProxyRoute, Cluster> convention)
        {
            _ = convention ?? throw new ArgumentNullException(nameof(convention));

            void Action(EndpointBuilder endpointBuilder)
            {
                var routeConfig = endpointBuilder.Metadata.OfType<RouteConfig>().Single();
                if (routeConfig.Route.RouteId.Equals(routeId, StringComparison.OrdinalIgnoreCase))
                {
                    var cluster = routeConfig.Cluster?.Config.Cluster;
                    var proxyRoute = routeConfig.ProxyRoute;
                    if (proxyRoute != null && cluster != null)
                    {
                        var conventionBuilder = new EndpointBuilderConventionBuilder(endpointBuilder);
                        convention(conventionBuilder, proxyRoute.DeepClone(), cluster.DeepClone());
                    }
                }
            }

            Add(Action);

            return this;
        }

        /// <summary>
        /// Configures the endpoint for all routes that map to a specific cluster
        /// </summary>
        /// <param name="clusterId">The cluster id to aplly convention to its route's endpoint.</param>
        /// <param name="convention">The convention to add to the builder.</param>
        /// <returns></returns>
        public ReverseProxyConventionBuilder ConfigureCluster(string clusterId, Action<IEndpointConventionBuilder> convention)
        {
            _ = convention ?? throw new ArgumentNullException(nameof(convention));

            void Action(EndpointBuilder endpointBuilder)
            {
                var routeConfig = endpointBuilder.Metadata.OfType<RouteConfig>().Single();
                if (routeConfig.Cluster.ClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase))
                {
                    var conventionBuilder = new EndpointBuilderConventionBuilder(endpointBuilder);
                    convention(conventionBuilder);
                }
            }

            Add(Action);

            return this;
        }

        /// <summary>
        /// Configures the endpoint for all routes that map to a specific cluster
        /// </summary>
        /// <param name="clusterId">The cluster id to aplly convention to its route's endpoint.</param>
        /// <param name="convention">The convention to add to the builder.</param>
        /// <returns></returns>
        public ReverseProxyConventionBuilder ConfigureCluster(string clusterId, Action<IEndpointConventionBuilder, ProxyRoute> convention)
        {
            _ = convention ?? throw new ArgumentNullException(nameof(convention));

            void Action(EndpointBuilder endpointBuilder)
            {
                var routeConfig = endpointBuilder.Metadata.OfType<RouteConfig>().Single();
                if (routeConfig.Cluster.ClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase))
                {
                    var proxyRoute = routeConfig.ProxyRoute;
                    if (proxyRoute != null)
                    {
                        var conventionBuilder = new EndpointBuilderConventionBuilder(endpointBuilder);
                        convention(conventionBuilder, proxyRoute.DeepClone());
                    }
                }
            }

            Add(Action);

            return this;
        }

        /// <summary>
        /// Configures the endpoint for all routes that map to a specific cluster
        /// </summary>
        /// <param name="clusterId">The cluster id to aplly convention to its route's endpoint.</param>
        /// <param name="convention">The convention to add to the builder.</param>
        /// <returns></returns>
        public ReverseProxyConventionBuilder ConfigureCluster(string clusterId, Action<IEndpointConventionBuilder, ProxyRoute, Cluster> convention)
        {
            _ = convention ?? throw new ArgumentNullException(nameof(convention));

            void Action(EndpointBuilder endpointBuilder)
            {
                var routeConfig = endpointBuilder.Metadata.OfType<RouteConfig>().Single();
                if (routeConfig.Cluster.ClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase))
                {
                    var cluster = routeConfig.Cluster?.Config.Cluster;
                    var proxyRoute = routeConfig.ProxyRoute;
                    if (proxyRoute != null && cluster != null)
                    {
                        var conventionBuilder = new EndpointBuilderConventionBuilder(endpointBuilder);
                        convention(conventionBuilder, proxyRoute.DeepClone(), cluster.DeepClone());
                    }
                }
            }

            Add(Action);

            return this;
        }

        internal class EndpointBuilderConventionBuilder : IEndpointConventionBuilder
        {
            private readonly EndpointBuilder _endpointBuilder;

            public EndpointBuilderConventionBuilder(EndpointBuilder endpointBuilder)
            {
                _endpointBuilder = endpointBuilder;
            }

            public void Add(Action<EndpointBuilder> convention)
            {
                convention(_endpointBuilder);
            }
        }
    }
}
