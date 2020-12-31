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
            IList<ResponseTransform> responseTransforms,
            IList<ResponseTransform> responseTrailerTransforms)
        {
            ShouldCopyRequestHeaders = copyRequestHeaders;
            RequestTransforms = requestTransforms ?? throw new ArgumentNullException(nameof(requestTransforms));
            ResponseTransforms = responseTransforms ?? throw new ArgumentNullException(nameof(responseTransforms));
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
        internal IList<ResponseTransform> ResponseTransforms { get; }

        /// <summary>
        /// Response trailer transforms.
        /// </summary>
        internal IList<ResponseTransform> ResponseTrailerTransforms { get; }

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
                await requestTransform.ApplyAsync(transformContext);
            }

            // Allow a transform to directly set a custom RequestUri.
            proxyRequest.RequestUri ??= RequestUtilities.MakeDestinationAddress(
                transformContext.DestinationPrefix, transformContext.Path, transformContext.Query.QueryString);

        }

        public override async Task TransformResponseAsync(HttpContext context, HttpResponseMessage proxyResponse)
        {
            await base.TransformResponseAsync(context, proxyResponse);

            foreach (var responseTransform in ResponseTransforms)
            {
                await responseTransform.ApplyAsync(context, proxyResponse);
            }
        }

        public override async Task TransformResponseTrailersAsync(HttpContext context, HttpResponseMessage proxyResponse)
        {
            await base.TransformResponseTrailersAsync(context, proxyResponse);

            // Only run the transforms if trailers are actually supported by the client response.
            var responseTrailersFeature = context.Features.Get<IHttpResponseTrailersFeature>();
            var outgoingTrailers = responseTrailersFeature?.Trailers;
            if (outgoingTrailers != null && !outgoingTrailers.IsReadOnly)
            {
                foreach (var responseTrailerTransform in ResponseTrailerTransforms)
                {
                    await responseTrailerTransform.ApplyAsync(context, proxyResponse);
                }
            }
        }
    }
}
