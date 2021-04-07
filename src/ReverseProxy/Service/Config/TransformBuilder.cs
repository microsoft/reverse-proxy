// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Abstractions.Config;
using Yarp.ReverseProxy.Service.Proxy;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Yarp.ReverseProxy.Service.Config
{
    /// <summary>
    /// Validates and builds request and response transforms for a given route.
    /// </summary>
    internal class TransformBuilder : ITransformBuilder
    {
        private readonly IServiceProvider _services;
        private readonly List<ITransformFactory> _factories;
        private readonly List<ITransformProvider> _providers;

        /// <summary>
        /// Creates a new <see cref="TransformBuilder"/>
        /// </summary>
        public TransformBuilder(IServiceProvider services, IEnumerable<ITransformFactory> factories, IEnumerable<ITransformProvider> providers)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _factories = factories?.ToList() ?? throw new ArgumentNullException(nameof(factories));
            _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
        }

        /// <inheritdoc/>
        public IReadOnlyList<Exception> ValidateRoute(ProxyRoute route)
        {
            var context = new TransformRouteValidationContext()
            {
                Services = _services,
                Route = route,
            };

            var rawTransforms = route?.Transforms;
            if (rawTransforms?.Count > 0)
            {
                foreach (var rawTransform in rawTransforms)
                {
                    var handled = false;
                    foreach (var factory in _factories)
                    {
                        if (factory.Validate(context, rawTransform))
                        {
                            handled = true;
                            break;
                        }
                    }

                    if (!handled)
                    {
                        context.Errors.Add(new ArgumentException($"Unknown transform: {string.Join(';', rawTransform.Keys)}"));
                    }
                }
            }

            // Let the app add any more validation it wants.
            foreach (var transformProvider in _providers)
            {
                transformProvider.ValidateRoute(context);
            }

            // We promise not to modify the list after we return it.
            return (IReadOnlyList<Exception>)context.Errors;
        }

        /// <inheritdoc/>
        public IReadOnlyList<Exception> ValidateCluster(Cluster cluster)
        {
            var context = new TransformClusterValidationContext()
            {
                Services = _services,
                Cluster = cluster,
            };

            // Let the app add any more validation it wants.
            foreach (var transformProvider in _providers)
            {
                transformProvider.ValidateCluster(context);
            }

            // We promise not to modify the list after we return it.
            return (IReadOnlyList<Exception>)context.Errors;
        }

        /// <inheritdoc/>
        public HttpTransformer Build(ProxyRoute route, Cluster cluster)
        {
            return BuildInternal(route, cluster);
        }

        // This is separate from Build for testing purposes.
        internal StructuredTransformer BuildInternal(ProxyRoute route, Cluster cluster)
        {
            var rawTransforms = route.Transforms;

            var context = new TransformBuilderContext
            {
                Services = _services,
                Route = route,
                Cluster = cluster,
            };

            if (rawTransforms?.Count > 0)
            {
                foreach (var rawTransform in rawTransforms)
                {
                    var handled = false;
                    foreach (var factory in _factories)
                    {
                        if (factory.Build(context, rawTransform))
                        {
                            handled = true;
                            break;
                        }
                    }

                    if (!handled)
                    {
                        throw new ArgumentException($"Unknown transform: {string.Join(';', rawTransform.Keys)}");
                    }
                }
            }

            // Let the app add any more transforms it wants.
            foreach (var transformProvider in _providers)
            {
                transformProvider.Apply(context);
            }

            // RequestHeaderOriginalHostKey defaults to false, and CopyRequestHeaders defaults to true.
            // If RequestHeaderOriginalHostKey was not specified then we need to make sure the transform gets
            // added anyways to remove the original host. If CopyRequestHeaders is false then we can omit the
            // transform.
            if (context.CopyRequestHeaders.GetValueOrDefault(true)
                && !context.RequestTransforms.Any(item => item is RequestHeaderOriginalHostTransform))
            {
                context.AddOriginalHost(false);
            }

            // Add default forwarders only if they haven't already been added or disabled.
            if (context.UseDefaultForwarders.GetValueOrDefault(true))
            {
                context.AddXForwarded();
            }

            return new StructuredTransformer(
                context.CopyRequestHeaders,
                context.CopyResponseHeaders,
                context.CopyResponseTrailers,
                context.RequestTransforms,
                context.ResponseTransforms,
                context.ResponseTrailersTransforms);
        }
    }
}
