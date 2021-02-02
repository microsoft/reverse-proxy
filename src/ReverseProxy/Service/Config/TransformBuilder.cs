// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.Config;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Config
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
        public IReadOnlyList<Exception> Validate(ProxyRoute route)
        {
            var context = new TransformValidationContext()
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
                transformProvider.Validate(context);
            }

            return context.Errors.ToList();
        }

        /// <inheritdoc/>
        public HttpTransformer Build(ProxyRoute route)
        {
            return BuildInternal(route);
        }

        // This is separate from Build for testing purposes.
        internal StructuredTransformer BuildInternal(ProxyRoute route)
        {
            var rawTransforms = route.Transforms;

            var context = new TransformBuilderContext
            {
                Services = _services,
                Route = route,
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

            // Suppress the host by default
            if (context.CopyRequestHeaders.GetValueOrDefault(true) && !context.UseOriginalHost.GetValueOrDefault())
            {
                // Headers were automatically copied, remove the Host header afterwards.
                context.RequestTransforms.Add(new RequestHeaderValueTransform(HeaderNames.Host, string.Empty, append: false));
            }
            else if (!context.CopyRequestHeaders.GetValueOrDefault(true) && context.UseOriginalHost.GetValueOrDefault())
            {
                // Headers were not copied, copy the Host manually.
                context.RequestTransforms.Add(RequestCopyHostTransform.Instance);
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
