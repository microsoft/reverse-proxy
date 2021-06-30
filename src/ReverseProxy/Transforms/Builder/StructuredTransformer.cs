// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy.Transforms.Builder
{
    /// <summary>
    /// Transforms for a given route.
    /// </summary>
    internal sealed class StructuredTransformer : HttpTransformer
    {
        /// <summary>
        /// Creates a new <see cref="StructuredTransformer"/> instance.
        /// </summary>
        internal StructuredTransformer(bool? copyRequestHeaders, bool? copyResponseHeaders, bool? copyResponseTrailers,
            IList<RequestTransform> requestTransforms,
            IList<ResponseTransform> responseTransforms,
            IList<ResponseTrailersTransform> responseTrailerTransforms)
        {
            ShouldCopyRequestHeaders = copyRequestHeaders;
            ShouldCopyResponseHeaders = copyResponseHeaders;
            ShouldCopyResponseTrailers = copyResponseTrailers;
            RequestTransforms = requestTransforms ?? throw new ArgumentNullException(nameof(requestTransforms));
            ResponseTransforms = responseTransforms ?? throw new ArgumentNullException(nameof(responseTransforms));
            ResponseTrailerTransforms = responseTrailerTransforms ?? throw new ArgumentNullException(nameof(responseTrailerTransforms));
        }

        /// <summary>
        /// Indicates if all request headers should be copied to the proxy request before applying transforms.
        /// </summary>
        internal bool? ShouldCopyRequestHeaders { get; }

        /// <summary>
        /// Indicates if all response headers should be copied to the client response before applying transforms.
        /// </summary>
        internal bool? ShouldCopyResponseHeaders { get; }

        /// <summary>
        /// Indicates if all response trailers should be copied to the client response before applying transforms.
        /// </summary>
        internal bool? ShouldCopyResponseTrailers { get; }

        /// <summary>
        /// Request transforms.
        /// </summary>
        internal IList<RequestTransform> RequestTransforms { get; }

        /// <summary>
        /// Response header transforms.
        /// </summary>
        internal IList<ResponseTransform> ResponseTransforms { get; }

        /// <summary>
        /// Response trailer transforms.
        /// </summary>
        internal IList<ResponseTrailersTransform> ResponseTrailerTransforms { get; }

        public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
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
                HeadersCopied = ShouldCopyRequestHeaders.GetValueOrDefault(true),
            };

            foreach (var requestTransform in RequestTransforms)
            {
                await requestTransform.ApplyAsync(transformContext);
            }

            // Allow a transform to directly set a custom RequestUri.
            proxyRequest.RequestUri ??= RequestUtilities.MakeDestinationAddress(
                transformContext.DestinationPrefix, transformContext.Path, transformContext.Query.QueryString);
        }

        public override async ValueTask<bool> TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            if (ShouldCopyResponseHeaders.GetValueOrDefault(true))
            {
                await base.TransformResponseAsync(httpContext, proxyResponse);
            }

            if (ResponseTransforms.Count == 0)
            {
                return true;
            }

            var transformContext = new ResponseTransformContext()
            {
                HttpContext = httpContext,
                ProxyResponse = proxyResponse,
                HeadersCopied = ShouldCopyResponseHeaders.GetValueOrDefault(true),
            };

            foreach (var responseTransform in ResponseTransforms)
            {
                await responseTransform.ApplyAsync(transformContext);
            }

            return !transformContext.SuppressResponseBody;
        }

        public override async ValueTask TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            if (ShouldCopyResponseTrailers.GetValueOrDefault(true))
            {
                await base.TransformResponseTrailersAsync(httpContext, proxyResponse);
            }

            if (ResponseTrailerTransforms.Count == 0)
            {
                return;
            }

            // Only run the transforms if trailers are actually supported by the client response.
            var responseTrailersFeature = httpContext.Features.Get<IHttpResponseTrailersFeature>();
            var outgoingTrailers = responseTrailersFeature?.Trailers;
            if (outgoingTrailers != null && !outgoingTrailers.IsReadOnly)
            {
                var transformContext = new ResponseTrailersTransformContext()
                {
                    HttpContext = httpContext,
                    ProxyResponse = proxyResponse,
                    HeadersCopied = ShouldCopyResponseTrailers.GetValueOrDefault(true),
                };

                foreach (var responseTrailerTransform in ResponseTrailerTransforms)
                {
                    await responseTrailerTransform.ApplyAsync(transformContext);
                }
            }
        }
    }
}
