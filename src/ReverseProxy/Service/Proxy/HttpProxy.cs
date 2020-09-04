// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Service.Metrics;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Proxy
{
    /// <summary>
    /// Default implementation of <see cref="IHttpProxy"/>.
    /// </summary>
    internal class HttpProxy : IHttpProxy
    {
        private static readonly HashSet<string> _headersToSkipGoingDownstream = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Transfer-Encoding",
        };

        private readonly ILogger _logger;
        private readonly ProxyMetrics _metrics;

        public HttpProxy(ILogger<HttpProxy> logger, ProxyMetrics metrics)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        /// <summary>
        /// Proxies the incoming request to the upstream server, and the response back to our client.
        /// </summary>
        /// <remarks>
        /// In what follows, as well as throughout in Reverse Proxy, we consider
        /// the following picture as illustrative of the Proxy.
        /// <code>
        ///      +-------------------+
        ///      |  Upstream server  +
        ///      +-------------------+
        ///            ▲       |
        ///        (b) |       | (c)
        ///            |       ▼
        ///      +-------------------+
        ///      |      Proxy        +
        ///      +-------------------+
        ///            ▲       |
        ///        (a) |       | (d)
        ///            |       ▼
        ///      +-------------------+
        ///      | Downstream client +
        ///      +-------------------+
        /// </code>
        ///
        /// (a) and (b) show the *request* path, going *upstream* from the client to the target.
        /// (c) and (d) show the *response* path, going *downstream* from the target back to the client.
        /// </remarks>
        /// <param name="longCancellation">This should be linked to a client disconnect notification like <see cref="HttpContext.RequestAborted"/>
        /// to avoid leaking long running requests.</param>
        public async Task ProxyAsync(
            HttpContext context,
            string destinationPrefix,
            Transforms transforms,
            HttpMessageInvoker httpClient,
            ProxyTelemetryContext proxyTelemetryContext,
            CancellationToken shortCancellation,
            CancellationToken longCancellation)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));
            _ = destinationPrefix ?? throw new ArgumentNullException(nameof(destinationPrefix));
            _ = transforms ?? throw new ArgumentNullException(nameof(transforms));
            _ = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 1: Create outgoing HttpRequestMessage
            var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
            var isUpgrade = (upgradeFeature?.IsUpgradableRequest ?? false)
                // Mitigate https://github.com/microsoft/reverse-proxy/issues/255, IIS considers all requests upgradeable.
                && string.Equals("WebSocket", context.Request.Headers[HeaderNames.Upgrade], StringComparison.OrdinalIgnoreCase);
            // Default to HTTP/1.1 for proxying upgradeable requests. This is already the default as of .NET Core 3.1
            // Otherwise request HTTP/2 and let HttpClient fallback to HTTP/1.1 if it cannot establish HTTP/2 with the target.
            // This is done without extra round-trips thanks to ALPN. We can detect a downgrade after calling HttpClient.SendAsync
            // (see Step 3 below). TBD how this will change when HTTP/3 is supported.
            var version = isUpgrade ? ProtocolHelper.Http11Version : ProtocolHelper.Http2Version;

            var destinationRequest = CreateRequestMessage(context, destinationPrefix, version, transforms.RequestTransforms);

            var isIncomingHttp2 = ProtocolHelper.IsHttp2(context.Request.Protocol);

            // NOTE: We heuristically assume gRPC-looking requests may require streaming semantics.
            // See https://github.com/microsoft/reverse-proxy/issues/118 for design discussion.
            var isStreamingRequest = isIncomingHttp2 && ProtocolHelper.IsGrpcContentType(context.Request.ContentType);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 2: Setup copy of request body (background) Client --► Proxy --► Destination
            // Note that we must do this before step (3) because step (3) may also add headers to the HttpContent that we set up here.
            var requestContent = SetupRequestBodyCopy(context.Request, destinationRequest, in proxyTelemetryContext, isStreamingRequest, longCancellation);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 3: Copy request headers Client --► Proxy --► Destination
            CopyRequestHeaders(context, destinationRequest, transforms.CopyRequestHeaders, transforms.RequestHeaderTransforms);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 4: Send the outgoing request using HttpClient
            ////this.logger.LogInformation($"   Starting Proxy --> Destination request");
            var httpClient = httpClientFactory.CreateClient();
            var destinationResponse = await httpClient.SendAsync(destinationRequest, shortCancellation);

            // Detect connection downgrade, which may be problematic for e.g. gRPC.
            if (!isUpgrade && isIncomingHttp2 && destinationResponse.Version.Major != 2)
            {
                // TODO: Do something on connection downgrade...
                Log.HttpDowngradeDetected(_logger);
            }

            // Assert that, if we are proxying content Destination, it must have started by now
            // (since HttpClient.SendAsync has already completed asynchronously).
            // If this check fails, there is a coding defect which would otherwise
            // cause us to wait forever in step 9, so fail fast here.
            if (requestContent != null && !requestContent.Started)
            {
                // TODO: bodyToDestinationContent is never null. HttpClient might would not need to read the body in some scenarios, such as an early auth failure with Expect: 100-continue.
                throw new InvalidOperationException("Proxying the Client request body to the Destination server hasn't started. This is a coding defect.");
            }

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 5: Copy response status line Client ◄-- Proxy ◄-- Destination
            ////this.logger.LogInformation($"   Setting Client <-- Proxy status: {(int)DestinationResponse.StatusCode} {DestinationResponse.ReasonPhrase}");
            context.Response.StatusCode = (int)destinationResponse.StatusCode;
            context.Features.Get<IHttpResponseFeature>().ReasonPhrase = destinationResponse.ReasonPhrase;

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 6: Copy response headers Client ◄-- Proxy ◄-- Destination
            CopyResponseHeaders(destinationResponse, context, transforms.ResponseHeaderTransforms);

            var upgraded = destinationResponse.StatusCode == HttpStatusCode.SwitchingProtocols && destinationResponse.Content != null;
            if (upgraded)
            {
                await HandleUpgradedResponse(upgradeFeature, destinationResponse, proxyTelemetryContext, longCancellation);
                return;
            }

            // NOTE: it may *seem* wise to call `context.Response.StartAsync()` at this point
            // since it looks like we are ready to send back response headers
            // (and this might help reduce extra delays while we wait to receive the body from Destination).
            // HOWEVER, this would produce the wrong result if it turns out that there is no content
            // from the Destination -- instead of sending headers and terminating the stream at once,
            // we would send headers thinking a body may be coming, and there is none.
            // This is problematic on gRPC connections when the Destination server encounters an error,
            // in which case it immediately returns the response headers and trailing headers, but no content,
            // and clients misbehave if the initial headers response does not indicate stream end.

            // TODO: Some of the tasks in steps (7) - (9) may go unobserved depending on what fails first. Needs more consideration.

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 7: Copy response body Client ◄-- Proxy ◄-- Destination
            await CopyResponseBodyAsync(destinationResponse.Content, context.Response.Body, proxyTelemetryContext, longCancellation);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 8: Copy response trailer headers and finish response Client ◄-- Proxy ◄-- Destination
            CopyResponseTrailingHeaders(destinationResponse, context, transforms.ResponseTrailerTransforms);

            if (isStreamingRequest)
            {
                // NOTE: We must call `CompleteAsync` so that Kestrel will flush all bytes to the client.
                // In the case where there was no response body,
                // this is also when headers and trailing headers are sent to the client.
                // Without this, the client might wait forever waiting for response bytes,
                // while we might wait forever waiting for request bytes,
                // leading to a stuck connection and no way to make progress.
                await context.Response.CompleteAsync();
            }

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 9: Wait for completion of step 2: copying request body Client --► Proxy --► Destination
            if (requestContent != null)
            {
                ////this.logger.LogInformation($"   Waiting for Client --> Proxy --> Destination body proxying to complete");
                await requestContent.ConsumptionTask;
            }
        }

        private async Task HandleUpgradedResponse(IHttpUpgradeFeature upgradeFeature, HttpResponseMessage destinationResponse,
            ProxyTelemetryContext proxyTelemetryContext, CancellationToken longCancellation)
        {
            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step A-6: Upgrade the downstream channel. This will send all response headers too.
            using var downstreamStream = await upgradeFeature.UpgradeAsync();

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step A-7: Copy duplex streams
            var upstreamStream = await destinationResponse.Content.ReadAsStreamAsync();

            var upstreamCopier = new StreamCopier(
                _metrics,
                new StreamCopyTelemetryContext(
                    direction: "upstream",
                    clusterId: proxyTelemetryContext.ClusterId,
                    routeId: proxyTelemetryContext.RouteId,
                    destinationId: proxyTelemetryContext.DestinationId));
            var upstreamTask = upstreamCopier.CopyAsync(downstreamStream, upstreamStream, longCancellation);

            var downstreamCopier = new StreamCopier(
                _metrics,
                new StreamCopyTelemetryContext(
                    direction: "downstream",
                    clusterId: proxyTelemetryContext.ClusterId,
                    routeId: proxyTelemetryContext.RouteId,
                    destinationId: proxyTelemetryContext.DestinationId));
            var downstreamTask = downstreamCopier.CopyAsync(upstreamStream, downstreamStream, longCancellation);

            await Task.WhenAll(upstreamTask, downstreamTask);
        }

        private HttpRequestMessage CreateRequestMessage(HttpContext context,
            string destinationAddress,
            Version httpVersion,
            IReadOnlyList<RequestParametersTransform> transforms)
        {
            // "http://a".Length = 8
            if (destinationAddress == null || destinationAddress.Length < 8)
            {
                throw new ArgumentException(nameof(destinationAddress));
            }

            // TODO Perf: We could probably avoid splitting this and just append the final path and query
            UriHelper.FromAbsolute(destinationAddress, out var destinationScheme, out var destinationHost, out var destinationPathBase, out _, out _); // Query and Fragment are not supported here.

            var request = context.Request;
            if (transforms.Count == 0)
            {
                var url = UriHelper.BuildAbsolute(destinationScheme, destinationHost, destinationPathBase, request.Path, request.QueryString);
                Log.Proxying(_logger, url);
                var uri = new Uri(url, UriKind.Absolute);
                return new HttpRequestMessage(HttpUtilities.GetHttpMethod(context.Request.Method), uri) { Version = httpVersion };
            }

            var transformContext = new RequestParametersTransformContext()
            {
                HttpContext = context,
                Version = httpVersion,
                Method = request.Method,
                Path = request.Path,
                Query = new QueryTransformContext(request),
            };
            foreach (var transform in transforms)
            {
                transform.Apply(transformContext);
            }

            var targetUrl = UriHelper.BuildAbsolute(destinationScheme, destinationHost, destinationPathBase, transformContext.Path, transformContext.Query.QueryString);
            Log.Proxying(_logger, targetUrl);
            var targetUri = new Uri(targetUrl, UriKind.Absolute);
            return new HttpRequestMessage(HttpUtilities.GetHttpMethod(transformContext.Method), targetUri) { Version = transformContext.Version };
        }

        private StreamCopyHttpContent SetupRequestBodyCopy(HttpRequest request, HttpRequestMessage upstreamRequest, in ProxyTelemetryContext proxyTelemetryContext, bool isStreamingRequest, CancellationToken cancellation)
        {
            // If we generate an HttpContent without a Content-Length then for HTTP/1.1 HttpClient will add a Transfer-Encoding: chunked header
            // even if it's a GET request. Some servers reject requests containing a Transfer-Encoding header if they're not expecting a body.
            // Try to be as specific as possible about the client's intent to send a body. The one thing we don't want to do is to start
            // reading the body early because that has side-effects like 100-continue.
            var hasBody = true;
            var contentLength = request.Headers.ContentLength;
            var method = request.Method;
            // https://tools.ietf.org/html/rfc7231#section-4.3.8
            // A client MUST NOT send a message body in a TRACE request.
            if (HttpMethods.IsTrace(method))
            {
                hasBody = false;
            }
            // https://tools.ietf.org/html/rfc7230#section-3.3.3
            // All HTTP/1.1 requests should have Transfer-Encoding or Content-Length.
            // Http.Sys/IIS will even add a Transfer-Encoding header to HTTP/2 requests with bodies for back-compat.
            // HTTP/1.0 Connection: close bodies are only allowed on responses, not requests.
            // https://tools.ietf.org/html/rfc1945#section-7.2.2
            //
            // Transfer-Encoding overrides Content-Length per spec
            else if (request.Headers.TryGetValue(HeaderNames.TransferEncoding, out var transferEncoding)
                && transferEncoding.Count == 1
                && string.Equals("chunked", transferEncoding.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                hasBody = true;
            }
            else if (contentLength.HasValue)
            {
                hasBody = contentLength > 0;
            }
            // Kestrel HTTP/2: There are no required headers that indicate if there is a request body so we need to sniff other fields.
            else if (!ProtocolHelper.IsHttp2OrGreater(request.Protocol))
            {
                hasBody = false;
            }
            // https://tools.ietf.org/html/rfc7231#section-5.1.1
            // A client MUST NOT generate a 100-continue expectation in a request that does not include a message body.
            else if (request.Headers.TryGetValue(HeaderNames.Expect, out var expect)
                && expect.Count == 1
                && string.Equals("100-continue", expect.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                hasBody = true;
            }
            // https://tools.ietf.org/html/rfc7231#section-4.3.1
            // A payload within a GET/HEAD/DELETE/CONNECT request message has no defined semantics; sending a payload body on a
            // GET/HEAD/DELETE/CONNECT request might cause some existing implementations to reject the request.
            else if (HttpMethods.IsGet(method)
                || HttpMethods.IsHead(method)
                || HttpMethods.IsDelete(method)
                || HttpMethods.IsConnect(method))
            {
                hasBody = false;
            }
            // else hasBody defaults to true

            StreamCopyHttpContent contentToUpstream = null;
            if (hasBody)
            {
                if (isStreamingRequest)
                {
                    DisableMinRequestBodyDataRateAndMaxRequestBodySize(request.HttpContext);
                }
                ////this.logger.LogInformation($"   Setting up downstream --> Proxy --> upstream body proxying");

                // Note on `autoFlushHttpClientOutgoingStream: isStreamingRequest`:
                // The.NET Core HttpClient stack keeps its own buffers on top of the underlying outgoing connection socket.
                // We flush those buffers down to the socket on every write when this is set,
                // but it does NOT result in calls to flush on the underlying socket.
                // This is necessary because we proxy http2 transparently,
                // and we are deliberately unaware of packet structure used e.g. in gRPC duplex channels.
                // Because the sockets aren't flushed, the perf impact of this choice is expected to be small.
                // Future: It may be wise to set this to true for *all* http2 incoming requests,
                // but for now, out of an abundance of caution, we only do it for requests that look like gRPC.
                var streamCopier = new StreamCopier(
                    _metrics,
                    new StreamCopyTelemetryContext(
                        direction: "upstream",
                        clusterId: proxyTelemetryContext.ClusterId,
                        routeId: proxyTelemetryContext.RouteId,
                        destinationId: proxyTelemetryContext.DestinationId));
                contentToUpstream = new StreamCopyHttpContent(
                    source: request.Body,
                    streamCopier: streamCopier,
                    autoFlushHttpClientOutgoingStream: isStreamingRequest,
                    cancellation: cancellation);
                upstreamRequest.Content = contentToUpstream;
            }

            return contentToUpstream;
        }

        private void CopyRequestHeaders(HttpContext context, HttpRequestMessage destination, bool? copyAllHeaders, IReadOnlyDictionary<string, RequestHeaderTransform> transforms)
        {
            // Transforms that were run in the first pass.
            HashSet<string> transformsRun = null;
            if (copyAllHeaders ?? true)
            {
                foreach (var header in context.Request.Headers)
                {
                    var headerName = header.Key;
                    var value = header.Value;
                    if (StringValues.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    // Filter out HTTP/2 pseudo headers like ":method" and ":path", those go into other fields.
                    if (headerName.Length > 0 && headerName[0] == ':')
                    {
                        continue;
                    }

                    if (transforms.TryGetValue(headerName, out var transform))
                    {
                        (transformsRun ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase)).Add(headerName);
                        value = transform.Apply(context, value);
                        if (StringValues.IsNullOrEmpty(value))
                        {
                            continue;
                        }
                    }

                    AddHeader(destination, headerName, value);
                }
            }

            // Run any transforms that weren't run yet.
            foreach (var (headerName, transform) in transforms)
            {
                if (!(transformsRun?.Contains(headerName) ?? false))
                {
                    var headerValue = context.Request.Headers[headerName];
                    headerValue = transform.Apply(context, headerValue);
                    if (!StringValues.IsNullOrEmpty(headerValue))
                    {
                        AddHeader(destination, headerName, headerValue);
                    }
                }
            }

            // Note: HttpClient.SendAsync will end up sending the union of
            // HttpRequestMessage.Headers and HttpRequestMessage.Content.Headers.
            // We don't really care where the proxied headers appear among those 2,
            // as long as they appear in one (and only one, otherwise they would be duplicated).
            static void AddHeader(HttpRequestMessage request, string headerName, StringValues value)
            {
                if (value.Count == 1)
                {
                    string headerValue = value;
                    if (!request.Headers.TryAddWithoutValidation(headerName, headerValue))
                    {
                        request.Content?.Headers.TryAddWithoutValidation(headerName, headerValue);
                    }
                }
                else
                {
                    string[] headerValues = value;
                    if (!request.Headers.TryAddWithoutValidation(headerName, headerValues))
                    {
                        request.Content?.Headers.TryAddWithoutValidation(headerName, headerValues);
                    }
                }
            }
        }

        private void CopyResponseHeaders(HttpResponseMessage source, HttpContext context, IReadOnlyDictionary<string, ResponseHeaderTransform> transforms)
        {
            // Transforms that were run in the first pass.
            HashSet<string> transformsRun = null;
            var responseHeaders = context.Response.Headers;
            CopyHeaders(source, source.Headers, context, responseHeaders, transforms, ref transformsRun);
            if (source.Content != null)
            {
                CopyHeaders(source, source.Content.Headers, context, responseHeaders, transforms, ref transformsRun);
            }
            RunRemainingResponseTransforms(source, context, responseHeaders, transforms, transformsRun);
        }

        private async Task CopyResponseBodyAsync(HttpContent upstreamResponseContent, Stream destination, ProxyTelemetryContext proxyTelemetryContext, CancellationToken cancellation)
        {
            if (upstreamResponseContent != null)
            {
                var streamCopier = new StreamCopier(
                    _metrics,
                    new StreamCopyTelemetryContext(
                        direction: "downstream",
                        clusterId: proxyTelemetryContext.ClusterId,
                        routeId: proxyTelemetryContext.RouteId,
                        destinationId: proxyTelemetryContext.DestinationId));

                var upstreamResponseStream = await upstreamResponseContent.ReadAsStreamAsync();
                await streamCopier.CopyAsync(upstreamResponseStream, destination, cancellation);
            }
        }

        private void CopyResponseTrailingHeaders(HttpResponseMessage source, HttpContext context, IReadOnlyDictionary<string, ResponseHeaderTransform> transforms)
        {
            // NOTE: Deliberately not using `context.Response.SupportsTrailers()`, `context.Response.AppendTrailer(...)`
            // because they lookup `IHttpResponseTrailersFeature` for every call. Here we do it just once instead.
            var responseTrailersFeature = context.Features.Get<IHttpResponseTrailersFeature>();
            var outgoingTrailers = responseTrailersFeature?.Trailers;
            if (outgoingTrailers != null && !outgoingTrailers.IsReadOnly)
            {
                // Note that trailers, if any, should already have been declared in Proxy's response
                // by virtue of us having proxied all upstream response headers in step 6.
                HashSet<string> transformsRun = null;
                CopyHeaders(source, source.TrailingHeaders, context, outgoingTrailers, transforms, ref transformsRun);
                RunRemainingResponseTransforms(source, context, outgoingTrailers, transforms, transformsRun);
            }
        }

        private static void CopyHeaders(HttpResponseMessage response, HttpHeaders source, HttpContext context, IHeaderDictionary destination, IReadOnlyDictionary<string, ResponseHeaderTransform> transforms, ref HashSet<string> transformsRun)
        {
            foreach (var header in source)
            {
                var headerName = header.Key;
                // TODO: this list only contains "Transfer-Encoding" because that messes up Kestrel. If we don't need to add any more here then it would be more efficient to
                // check for the single value directly.
                if (_headersToSkipGoingDownstream.Contains(headerName))
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

        private static void RunRemainingResponseTransforms(HttpResponseMessage response, HttpContext context, IHeaderDictionary destination, IReadOnlyDictionary<string, ResponseHeaderTransform> transforms, HashSet<string> transformsRun)
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

        /// <summary>
        /// Disable some ASP .NET Core server limits so that we can handle long-running gRPC requests unconstrained.
        /// Note that the gRPC server implementation on ASP .NET Core does the same for client-streaming and duplex methods.
        /// Since in Gateway we have no way to determine if the current request requires client-streaming or duplex comm,
        /// we do this for *all* incoming requests that look like they might be gRPC.
        /// </summary>
        /// <remarks>
        /// Inspired on
        /// <see href="https://github.com/grpc/grpc-dotnet/blob/3ce9b104524a4929f5014c13cd99ba9a1c2431d4/src/Grpc.AspNetCore.Server/Internal/CallHandlers/ServerCallHandlerBase.cs#L127"/>.
        /// </remarks>
        private void DisableMinRequestBodyDataRateAndMaxRequestBodySize(HttpContext httpContext)
        {
            var minRequestBodyDataRateFeature = httpContext.Features.Get<IHttpMinRequestBodyDataRateFeature>();
            if (minRequestBodyDataRateFeature != null)
            {
                minRequestBodyDataRateFeature.MinDataRate = null;
            }

            var maxRequestBodySizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (maxRequestBodySizeFeature != null)
            {
                if (!maxRequestBodySizeFeature.IsReadOnly)
                {
                    maxRequestBodySizeFeature.MaxRequestBodySize = null;
                }
                else
                {
                    // IsReadOnly could be true if middleware has already started reading the request body
                    // In that case we can't disable the max request body size for the request stream
                    _logger.LogWarning("Unable to disable max request body size.");
                }
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception> _httpDowngradeDetected = LoggerMessage.Define(
                LogLevel.Information,
                EventIds.HttpDowngradeDetected,
                "Health check has gracefully shut down.");

            private static readonly Action<ILogger, string, Exception> _proxying = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.Proxying,
                "Proxying to {targetUrl}");

            public static void HttpDowngradeDetected(ILogger logger)
            {
                _httpDowngradeDetected(logger, null);
            }

            public static void Proxying(ILogger logger, string targetUrl)
            {
                _proxying(logger, targetUrl, null);
            }
        }
    }
}
