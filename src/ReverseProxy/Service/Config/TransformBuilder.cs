// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.HttpOverrides;
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
        public IList<Exception> Validate(ProxyRoute route)
        {
            var errors = new List<Exception>();
            var rawTransforms = route?.Transforms;

            if (rawTransforms == null || rawTransforms.Count == 0)
            {
                return errors;
            }

            var context = new TransformValidationContext()
            {
                Services = _services,
                Route = route,
                Errors = errors,
            };

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
                    throw new ArgumentException($"Unknown transform: {string.Join(';', rawTransform.Keys)}");
                }
            }

            foreach (var rawTransform in rawTransforms)
            {
                if (rawTransform.TryGetValue("ClientCert", out var clientCertHeader))
                {
                    TryCheckTooManyParameters(errors.Add, rawTransform, expected: 1);
                }
                else if (rawTransform.TryGetValue("HttpMethod", out var fromHttpMethod))
                {
                    CheckTooManyParameters(rawTransform, expected: 2);
                    if (!rawTransform.TryGetValue("Set", out var _))
                    {
                        errors.Add(new ArgumentException($"Unexpected parameters for HttpMethod: {string.Join(';', rawTransform.Keys)}. Expected 'Set'"));
                    }
                }
                else
                {
                    errors.Add(new ArgumentException($"Unknown transform: {string.Join(';', rawTransform.Keys)}"));
                }
            }

            return errors;
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

            bool? copyRequestHeaders = null;
            bool? copyResponseHeaders = null;
            bool? copyResponseTrailers = null;
            bool? useOriginalHost = null;
            bool? forwardersSet = null;
            var requestTransforms = new List<RequestTransform>();
            var responseTransforms = new List<ResponseTransform>();
            var responseTrailersTransforms = new List<ResponseTrailersTransform>();

            if (rawTransforms?.Count > 0)
            {
                foreach (var rawTransform in rawTransforms)
                {
                    if (rawTransform.TryGetValue("ClientCert", out var clientCertHeader))
                    {
                        CheckTooManyParameters(rawTransform, expected: 1);
                        requestTransforms.Add(new RequestHeaderClientCertTransform(clientCertHeader));
                    }
                    else if (rawTransform.TryGetValue("HttpMethod", out var fromHttpMethod))
                    {
                        CheckTooManyParameters(rawTransform, expected: 2);
                        if (rawTransform.TryGetValue("Set", out var toHttpMethod))
                        {
                            requestTransforms.Add(new HttpMethodTransform(fromHttpMethod, toHttpMethod));
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown transform: {string.Join(';', rawTransform.Keys)}");
                    }
                }
            }

            var transformBuilderContext = new TransformBuilderContext
            {
                Services = _services,
                Route = route,
                CopyRequestHeaders = copyRequestHeaders,
                CopyResponseHeaders = copyResponseHeaders,
                CopyResponseTrailers = copyResponseTrailers,
                UseOriginalHost = useOriginalHost,
                UseDefaultForwarders = !forwardersSet,
                RequestTransforms = requestTransforms,
                ResponseTransforms = responseTransforms,
                ResponseTrailersTransforms = responseTrailersTransforms,
            };

            if (rawTransforms?.Count > 0)
            {
                foreach (var rawTransform in rawTransforms)
                {
                    var handled = false;
                    foreach (var factory in _factories)
                    {
                        if (factory.Build(transformBuilderContext, rawTransform))
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
                transformProvider.Apply(transformBuilderContext);
            }

            // TODO: UseOriginalHost doesn't work as expected when the value is true and CopyRequestHeaders is false.

            // Suppress the host by default
            if (!transformBuilderContext.UseOriginalHost.GetValueOrDefault())
            {
                requestTransforms.Add(new RequestHeaderValueTransform(HeaderNames.Host, string.Empty, append: false));
            }

            // Add default forwarders
            if (transformBuilderContext.UseDefaultForwarders.GetValueOrDefault(true))
            {
                requestTransforms.Add(new RequestHeaderXForwardedProtoTransform(ForwardedHeadersDefaults.XForwardedProtoHeaderName, append: true));
                requestTransforms.Add(new RequestHeaderXForwardedHostTransform(ForwardedHeadersDefaults.XForwardedHostHeaderName, append: true));
                requestTransforms.Add(new RequestHeaderXForwardedForTransform(ForwardedHeadersDefaults.XForwardedForHeaderName, append: true));
                requestTransforms.Add(new RequestHeaderXForwardedPathBaseTransform("X-Forwarded-PathBase", append: true));
            }

            return new StructuredTransformer(
                transformBuilderContext.CopyRequestHeaders,
                transformBuilderContext.CopyResponseHeaders,
                transformBuilderContext.CopyResponseTrailers,
                requestTransforms,
                responseTransforms,
                responseTrailersTransforms);
        }

        private static void TryCheckTooManyParameters(Action<Exception> onError, IReadOnlyDictionary<string, string> rawTransform, int expected)
        {
            if (rawTransform.Count > expected)
            {
                onError(new InvalidOperationException("The transform contains more parameters than expected: " + string.Join(';', rawTransform.Keys)));
            }
        }

        private static void CheckTooManyParameters(IReadOnlyDictionary<string, string> rawTransform, int expected)
        {
            TryCheckTooManyParameters(ex => throw ex, rawTransform, expected);
        }
    }
}
