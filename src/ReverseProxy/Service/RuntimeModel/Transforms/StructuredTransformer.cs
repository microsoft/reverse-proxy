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
        internal StructuredTransformer(bool? copyRequestHeaders, IList<RequestParametersTransform> requestTransforms,
            Dictionary<string, RequestHeaderTransform> requestHeaderTransforms,
            Dictionary<string, ResponseHeaderTransform> responseHeaderTransforms,
            Dictionary<string, ResponseHeaderTransform> responseTrailerTransforms)
        {
            ShouldCopyRequestHeaders = copyRequestHeaders;
            RequestTransforms = requestTransforms ?? throw new ArgumentNullException(nameof(requestTransforms));
            RequestHeaderTransforms = requestHeaderTransforms ?? throw new ArgumentNullException(nameof(requestHeaderTransforms));
            ResponseHeaderTransforms = responseHeaderTransforms ?? throw new ArgumentNullException(nameof(responseHeaderTransforms));
            ResponseTrailerTransforms = responseTrailerTransforms ?? throw new ArgumentNullException(nameof(responseTrailerTransforms));
        }

        /// <summary>
        /// Indicates if all request headers should be proxied in absence of other transforms.
        /// </summary>
        internal bool? ShouldCopyRequestHeaders { get; }

        /// <summary>
        /// Request parameter transforms.
        /// </summary>
        internal IList<RequestParametersTransform> RequestTransforms { get; }

        /// <summary>
        /// Request header transforms.
        /// </summary>
        internal Dictionary<string, RequestHeaderTransform> RequestHeaderTransforms { get; }

        /// <summary>
        /// Response header transforms.
        /// </summary>
        internal Dictionary<string, ResponseHeaderTransform> ResponseHeaderTransforms { get; }

        /// <summary>
        /// Response trailer transforms.
        /// </summary>
        internal Dictionary<string, ResponseHeaderTransform> ResponseTrailerTransforms { get; }

        // These intentionally do not call base because the logic here overlaps with the default header copy logic.
        public override Task TransformRequestAsync(HttpContext context, HttpRequestMessage proxyRequest, string destinationPrefix)
        {
            var transformContext = new RequestParametersTransformContext()
            {
                DestinationPrefix = destinationPrefix,
                HttpContext = context,
                ProxyRequest = proxyRequest,
                Path = context.Request.Path,
                Query = new QueryTransformContext(context.Request),
            };

            foreach (var requestTransform in RequestTransforms)
            {
                requestTransform.Apply(transformContext);
            }

            // Allow a transform to directly set a custom RequestUri.
            proxyRequest.RequestUri ??= RequestUtilities.MakeDestinationAddress(
                transformContext.DestinationPrefix, transformContext.Path, transformContext.Query.QueryString);

            CopyRequestHeaders(context, proxyRequest);

            return Task.CompletedTask;
        }

        private void CopyRequestHeaders(HttpContext context, HttpRequestMessage proxyRequest)
        {
            // Transforms that were run in the first pass.
            HashSet<string> transformsRun = null;
            if (ShouldCopyRequestHeaders.GetValueOrDefault(true))
            {
                foreach (var header in context.Request.Headers)
                {
                    var headerName = header.Key;
                    var headerValue = header.Value;
                    if (StringValues.IsNullOrEmpty(headerValue))
                    {
                        continue;
                    }

                    // Filter out HTTP/2 pseudo headers like ":method" and ":path", those go into other fields.
                    if (headerName.Length > 0 && headerName[0] == ':')
                    {
                        continue;
                    }

                    if (RequestHeaderTransforms.TryGetValue(headerName, out var transform))
                    {
                        (transformsRun ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(headerName);
                        headerValue = transform.Apply(context, proxyRequest, headerValue);
                        if (StringValues.IsNullOrEmpty(headerValue))
                        {
                            continue;
                        }
                    }

                    RequestUtilities.AddHeader(proxyRequest, headerName, headerValue);
                }
            }

            transformsRun ??= EmptyHash;

            // Run any transforms that weren't run yet.
            foreach (var (headerName, transform) in RequestHeaderTransforms)
            {
                if (!transformsRun.Contains(headerName))
                {
                    var headerValue = transform.Apply(context, proxyRequest, StringValues.Empty);
                    if (!StringValues.IsNullOrEmpty(headerValue))
                    {
                        RequestUtilities.AddHeader(proxyRequest, headerName, headerValue);
                    }
                }
            }
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
