// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Transforms for a given route.
    /// </summary>
    internal class StructuredTransformer : HttpTransformer
    {
        private static readonly HashSet<string> EmptyHash = new HashSet<string>(0);

        /// <summary>
        /// Creates a new <see cref="StructuredTransformer"/> instance.
        /// </summary>
        internal StructuredTransformer(bool? copyRequestHeaders, IList<RequestTransform> requestTransforms,
            Dictionary<string, ResponseHeaderTransform> responseHeaderTransforms,
            Dictionary<string, ResponseHeaderTransform> responseTrailerTransforms)
        {
            ShouldCopyRequestHeaders = copyRequestHeaders;
            RequestTransforms = requestTransforms ?? throw new ArgumentNullException(nameof(requestTransforms));
            ResponseHeaderTransforms = responseHeaderTransforms ?? throw new ArgumentNullException(nameof(responseHeaderTransforms));
            ResponseTrailerTransforms = responseTrailerTransforms ?? throw new ArgumentNullException(nameof(responseTrailerTransforms));
        }

        /// <summary>
        /// Indicates if all request headers should be proxied in absence of other transforms.
        /// </summary>
        internal bool? ShouldCopyRequestHeaders { get; }

        /// <summary>
        /// Request transforms.
        /// </summary>
        internal IList<RequestTransform> RequestTransforms { get; }

        /// <summary>
        /// Response header transforms.
        /// </summary>
        internal Dictionary<string, ResponseHeaderTransform> ResponseHeaderTransforms { get; }

        /// <summary>
        /// Response trailer transforms.
        /// </summary>
        internal Dictionary<string, ResponseHeaderTransform> ResponseTrailerTransforms { get; }

        // These intentionally do not call base because the logic here overlaps with the default header copy logic.
        public override async Task TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
        {
            if (ShouldCopyRequestHeaders.GetValueOrDefault(true))
            {
                await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);
            }

            if (RequestTransforms.Count == 0)
            {
                return;
            }

            var transformContext = new RequestTransformContext()
            {
                DestinationPrefix = destinationPrefix,
                HttpContext = httpContext,
                ProxyRequest = proxyRequest,
                Path = httpContext.Request.Path,
                Query = new QueryTransformContext(httpContext.Request),
            };

            foreach (var requestTransform in RequestTransforms)
            {
                requestTransform.Apply(transformContext);
            }

            // Allow a transform to directly set a custom RequestUri.
            proxyRequest.RequestUri ??= RequestUtilities.MakeDestinationAddress(
                transformContext.DestinationPrefix, transformContext.Path, transformContext.Query.QueryString);

        }

        public override Task TransformResponseAsync(HttpContext context, HttpResponseMessage proxyResponse)
        {
            HashSet<string> transformsRun = null;
            var responseHeaders = context.Response.Headers;
            CopyResponseHeaders(proxyResponse, proxyResponse.Headers, context, responseHeaders, ResponseHeaderTransforms, ref transformsRun);
            if (proxyResponse.Content != null)
            {
                CopyResponseHeaders(proxyResponse, proxyResponse.Content.Headers, context, responseHeaders, ResponseHeaderTransforms, ref transformsRun);
            }
            RunRemainingResponseTransforms(proxyResponse, context, responseHeaders, ResponseHeaderTransforms, transformsRun);
            return Task.CompletedTask;
        }

        public override Task TransformResponseTrailersAsync(HttpContext context, HttpResponseMessage proxyResponse)
        {
            var responseTrailersFeature = context.Features.Get<IHttpResponseTrailersFeature>();
            var outgoingTrailers = responseTrailersFeature?.Trailers;
            HashSet<string> transformsRun = null;
            if (outgoingTrailers != null && !outgoingTrailers.IsReadOnly)
            {
                CopyResponseHeaders(proxyResponse, proxyResponse.TrailingHeaders, context, outgoingTrailers, ResponseTrailerTransforms, ref transformsRun);
                RunRemainingResponseTransforms(proxyResponse, context, outgoingTrailers, ResponseTrailerTransforms, transformsRun);
            }
            return Task.CompletedTask;
        }

        private static void CopyResponseHeaders(HttpResponseMessage response, HttpHeaders source, HttpContext context, IHeaderDictionary destination,
            IReadOnlyDictionary<string, ResponseHeaderTransform> transforms, ref HashSet<string> transformsRun)
        {
            foreach (var header in source)
            {
                var headerName = header.Key;
                if (RequestUtilities.ResponseHeadersToSkip.Contains(headerName))
                {
                    continue;
                }

                var headerValue = new StringValues(header.Value.ToArray());

                if (transforms.TryGetValue(headerName, out var transform))
                {
                    (transformsRun ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(headerName);
                    headerValue = transform.Apply(context, response, headerValue);
                }
                if (!StringValues.IsNullOrEmpty(headerValue))
                {
                    destination.Append(headerName, headerValue);
                }
            }
        }

        private static void RunRemainingResponseTransforms(HttpResponseMessage response, HttpContext context, IHeaderDictionary destination,
            IReadOnlyDictionary<string, ResponseHeaderTransform> transforms, HashSet<string> transformsRun)
        {
            transformsRun ??= EmptyHash;

            // Run any transforms that weren't run yet.
            foreach (var (headerName, transform) in transforms) // TODO: What about multiple transforms per header? Last wins?
            {
                if (!transformsRun.Contains(headerName))
                {
                    var headerValue = StringValues.Empty;
                    headerValue = transform.Apply(context, response, headerValue);
                    if (!StringValues.IsNullOrEmpty(headerValue))
                    {
                        destination.Append(headerName, headerValue);
                    }
                }
            }
        }
    }
}
