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
    internal class Transforms
    {
        /// <summary>
        /// Creates a new <see cref="Transforms"/> instance.
        /// </summary>
        internal Transforms(bool? copyRequestHeaders, IList<RequestParametersTransform> requestTransforms,
            Dictionary<string, RequestHeaderTransform> requestHeaderTransforms,
            Dictionary<string, ResponseHeaderTransform> responseHeaderTransforms,
            Dictionary<string, ResponseHeaderTransform> responseTrailerTransforms)
        {
            ShouldCopyRequestHeaders = copyRequestHeaders;
            RequestTransforms = requestTransforms ?? throw new ArgumentNullException(nameof(requestTransforms));
            RequestHeaderTransforms = requestHeaderTransforms ?? throw new ArgumentNullException(nameof(requestHeaderTransforms));
            ResponseHeaderTransforms = responseHeaderTransforms ?? throw new ArgumentNullException(nameof(responseHeaderTransforms));
            ResponseTrailerTransforms = responseTrailerTransforms ?? throw new ArgumentNullException(nameof(responseTrailerTransforms));

            AdaptedTransforms = new HttpTransforms()
            {
                // Use the default copy logic only if we don't have any transforms.
                CopyRequestHeaders = RequestHeaderTransforms.Count == 0 && (ShouldCopyRequestHeaders ?? true),
                OnRequest = RequestTransforms.Count == 0 && RequestHeaderTransforms.Count == 0 ? null : TransformRequestAsync,

                CopyResponseHeaders = ResponseHeaderTransforms.Count == 0,
                OnResponse = ResponseHeaderTransforms.Count == 0 ? null : TransformResponseHeadersAsync,

                CopyResponseTrailers = ResponseTrailerTransforms.Count == 0,
                OnResponseTrailers = ResponseTrailerTransforms.Count == 0 ? null : TransformResponseTralersAsync
            };
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

        /// <summary>
        /// This is the structure used by IHttpProxy transforms.
        /// </summary>
        internal HttpTransforms AdaptedTransforms { get; }

        private Task TransformRequestAsync(HttpContext context, HttpRequestMessage request, string destinationPrefix)
        {
            var transformContext = new RequestParametersTransformContext()
            {
                DestinationPrefix = destinationPrefix,
                HttpContext = context,
                Request = request,
                Path = context.Request.Path,
                Query = new QueryTransformContext(context.Request),
            };

            foreach (var requestTransform in RequestTransforms)
            {
                requestTransform.Apply(transformContext);
            }

            // Allow a transform to directly set a custom RequestUri.
            request.RequestUri ??= RequestUtilities.MakeDestinationAddress(
                transformContext.DestinationPrefix, transformContext.Path, transformContext.Query.QueryString);

            CopyRequestHeaders(context, request);

            return Task.CompletedTask;
        }

        private void CopyRequestHeaders(HttpContext context, HttpRequestMessage destination)
        {
            // Transforms that were run in the first pass.
            HashSet<string> transformsRun = null;
            if (ShouldCopyRequestHeaders ?? true)
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
                        headerValue = transform.Apply(context, headerValue);
                        if (StringValues.IsNullOrEmpty(headerValue))
                        {
                            continue;
                        }
                    }

                    RequestUtilities.AddHeader(destination, headerName, headerValue);
                }
            }

            // Run any transforms that weren't run yet.
            foreach (var (headerName, transform) in RequestHeaderTransforms)
            {
                if (!(transformsRun?.Contains(headerName) ?? false))
                {
                    var headerValue = transform.Apply(context, StringValues.Empty);
                    if (!StringValues.IsNullOrEmpty(headerValue))
                    {
                        RequestUtilities.AddHeader(destination, headerName, headerValue);
                    }
                }
            }
        }

        private Task TransformResponseHeadersAsync(HttpContext context, HttpResponseMessage source)
        {
            HashSet<string> transformsRun = null;
            var responseHeaders = context.Response.Headers;
            CopyResponseHeaders(source, source.Headers, context, responseHeaders, ResponseHeaderTransforms, ref transformsRun);
            if (source.Content != null)
            {
                CopyResponseHeaders(source, source.Content.Headers, context, responseHeaders, ResponseHeaderTransforms, ref transformsRun);
            }
            RunRemainingResponseTransforms(source, context, responseHeaders, ResponseHeaderTransforms, transformsRun);
            return Task.CompletedTask;
        }

        private Task TransformResponseTralersAsync(HttpContext context, HttpResponseMessage source)
        {
            // Trailers support was already verified by the caller.
            var responseTrailersFeature = context.Features.Get<IHttpResponseTrailersFeature>();
            var outgoingTrailers = responseTrailersFeature?.Trailers;
            HashSet<string> transformsRun = null;
            CopyResponseHeaders(source, source.TrailingHeaders, context, outgoingTrailers, ResponseTrailerTransforms, ref transformsRun);
            RunRemainingResponseTransforms(source, context, outgoingTrailers, ResponseTrailerTransforms, transformsRun);
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
            // Run any transforms that weren't run yet.
            foreach (var (headerName, transform) in transforms) // TODO: What about multiple transforms per header? Last wins?
            {
                if (!(transformsRun?.Contains(headerName) ?? false))
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
