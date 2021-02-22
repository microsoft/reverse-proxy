// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Transforms for a given route.
    /// </summary>
    internal class StructuredTransformer : HttpTransformer
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

        public override Task TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
        {
            if (ShouldCopyRequestHeaders.GetValueOrDefault(true))
            {
                var task = base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix);
                if (!task.IsCompletedSuccessfully)
                {
                    return AwaitTransformRequestAsync(task, httpContext, proxyRequest, destinationPrefix);
                }
            }

            if (RequestTransforms.Count == 0)
            {
                return Task.CompletedTask;
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

            for (var i = 0; i < RequestTransforms.Count; i++)
            {
                var task = RequestTransforms[i].ApplyAsync(transformContext);
                if (!task.IsCompletedSuccessfully)
                {
                    return AwaitTransformRequestAsync(task, i, transformContext, proxyRequest);
                }
                task.GetAwaiter().GetResult();
            }

            // Allow a transform to directly set a custom RequestUri.
            proxyRequest.RequestUri ??= RequestUtilities.MakeDestinationAddress(
                transformContext.DestinationPrefix, transformContext.Path, transformContext.Query.QueryString);

            return Task.CompletedTask;
        }

        private async Task AwaitTransformRequestAsync(Task task, HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix)
        {
            await task;

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

        private async Task AwaitTransformRequestAsync(ValueTask task, int i, RequestTransformContext transformContext, HttpRequestMessage proxyRequest)
        {
            await task;

            for (i++; i < RequestTransforms.Count; i++)
            {
                await RequestTransforms[i].ApplyAsync(transformContext);
            }

            // Allow a transform to directly set a custom RequestUri.
            proxyRequest.RequestUri ??= RequestUtilities.MakeDestinationAddress(
                transformContext.DestinationPrefix, transformContext.Path, transformContext.Query.QueryString);
        }

        public override Task TransformResponseAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            if (ShouldCopyResponseHeaders.GetValueOrDefault(true))
            {
                var task = base.TransformResponseAsync(httpContext, proxyResponse);
                if (!task.IsCompletedSuccessfully)
                {
                    return AwaitTransformResponseAsync(task, httpContext, proxyResponse);
                }
            }

            if (ResponseTransforms.Count == 0)
            {
                return Task.CompletedTask;
            }

            var transformContext = new ResponseTransformContext()
            {
                HttpContext = httpContext,
                ProxyResponse = proxyResponse,
                HeadersCopied = ShouldCopyResponseHeaders.GetValueOrDefault(true),
            };

            for (var i = 0; i < ResponseTransforms.Count; i++)
            {
                var task = ResponseTransforms[i].ApplyAsync(transformContext);
                if (!task.IsCompletedSuccessfully)
                {
                    return AwaitTransformResponseAsync(task, i, transformContext);
                }

                task.GetAwaiter().GetResult();
            }

            return Task.CompletedTask;
        }

        private async Task AwaitTransformResponseAsync(Task task, HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            await task;

            if (ResponseTransforms.Count == 0)
            {
                return;
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
        }

        private async Task AwaitTransformResponseAsync(ValueTask task, int i, ResponseTransformContext transformContext)
        {
            await task;

            for (i++; i < ResponseTransforms.Count; i++)
            {
                await ResponseTransforms[i].ApplyAsync(transformContext);
            }
        }

        public override Task TransformResponseTrailersAsync(HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            if (ShouldCopyResponseTrailers.GetValueOrDefault(true))
            {
                var task = base.TransformResponseTrailersAsync(httpContext, proxyResponse);
                if (!task.IsCompletedSuccessfully)
                {
                    return AwaitTransformResponseTrailersAsync(task, httpContext, proxyResponse);
                }
            }

            if (ResponseTrailerTransforms.Count == 0)
            {
                return Task.CompletedTask;
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

                for (var i = 0; i < ResponseTrailerTransforms.Count; i++)
                {
                    var task = ResponseTrailerTransforms[i].ApplyAsync(transformContext);
                    if (!task.IsCompletedSuccessfully)
                    {
                        return AwaitTransformResponseTrailersAsync(task, i, transformContext);
                    }
                    task.GetAwaiter().GetResult();
                }
            }

            return Task.CompletedTask;
        }

        private async Task AwaitTransformResponseTrailersAsync(Task task, HttpContext httpContext, HttpResponseMessage proxyResponse)
        {
            await task;

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

        private async Task AwaitTransformResponseTrailersAsync(ValueTask task, int i, ResponseTrailersTransformContext transformContext)
        {
            await task;

            for (i++; i < ResponseTrailerTransforms.Count; i++)
            {
                await ResponseTrailerTransforms[i].ApplyAsync(transformContext);
            }
        }
    }
}
