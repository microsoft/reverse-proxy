// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Common;
using Microsoft.ReverseProxy.Core.Service.Metrics;
using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.Service.Proxy
{
    /// <summary>
    /// Default implementation of <see cref="IHttpProxy"/>.
    /// </summary>
    internal class HttpProxy : IHttpProxy
    {
        internal static readonly Version Http2Version = new Version(2, 0);

        // TODO: Enumerate all headers to skip
        private static readonly HashSet<string> _headersToSkipGoingUpstream = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Host",
        };
        private static readonly HashSet<string> _headersToSkipGoingDownstream = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Transfer-Encoding",
        };

        private readonly ILogger _logger;
        private readonly ProxyMetrics _metrics;

        public HttpProxy(ILogger<HttpProxy> logger, ProxyMetrics metrics)
        {
            Contracts.CheckValue(logger, nameof(logger));
            Contracts.CheckValue(metrics, nameof(metrics));
            _logger = logger;
            _metrics = metrics;
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
        public Task ProxyAsync(
            HttpContext context,
            Uri targetUri,
            IProxyHttpClientFactory httpClientFactory,
            ProxyTelemetryContext proxyTelemetryContext,
            CancellationToken shortCancellation,
            CancellationToken longCancellation)
        {
            Contracts.CheckValue(context, nameof(context));
            Contracts.CheckValue(targetUri, nameof(targetUri));
            Contracts.CheckValue(httpClientFactory, nameof(httpClientFactory));

            var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();
            if (upgradeFeature == null || !upgradeFeature.IsUpgradableRequest)
            {
                return NormalProxyAsync(context, targetUri, httpClientFactory.CreateNormalClient(), proxyTelemetryContext, shortCancellation, longCancellation);
            }
            else
            {
                return UpgradableProxyAsync(context, upgradeFeature, targetUri, httpClientFactory.CreateUpgradableClient(), proxyTelemetryContext, shortCancellation, longCancellation);
            }
        }

        /// <summary>
        /// Proxies a normal (i.e. non-upgradable) request to the upstream server, and the response back to our client.
        /// </summary>
        /// <remarks>
        /// Normal proxying comprises the following steps:
        ///    (0) Disable ASP .NET Core limits for streaming requests
        ///    (1) Create outgoing HttpRequestMessage
        ///    (2) Setup copy of request body (background)             Downstream --► Proxy --► Upstream
        ///    (3) Copy request headers                                Downstream --► Proxy --► Upstream
        ///    (4) Send the outgoing request using HttpMessageInvoker  Downstream --► Proxy --► Upstream
        ///    (5) Copy response status line                           Downstream ◄-- Proxy ◄-- Upstream
        ///    (6) Copy response headers                               Downstream ◄-- Proxy ◄-- Upstream
        ///    (7) Copy response body                                  Downstream ◄-- Proxy ◄-- Upstream
        ///    (8) Copy response trailer headers and finish response   Downstream ◄-- Proxy ◄-- Upstream
        ///    (9) Wait for completion of step 2: copying request body Downstream --► Proxy --► Upstream
        ///
        /// ASP .NET Core (Kestrel) will finally send response trailers (if any)
        /// after we complete the steps above and relinquish control.
        /// </remarks>
        private async Task NormalProxyAsync(
            HttpContext context,
            Uri targetUri,
            HttpMessageInvoker httpClient,
            ProxyTelemetryContext proxyTelemetryContext,
            CancellationToken shortCancellation,
            CancellationToken longCancellation)
        {
            Contracts.CheckValue(context, nameof(context));
            Contracts.CheckValue(targetUri, nameof(targetUri));
            Contracts.CheckValue(httpClient, nameof(httpClient));

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 0: Disable ASP .NET Core limits for streaming requests
            var isIncomingHttp2 = HttpProtocol.IsHttp2(context.Request.Protocol);

            // NOTE: We heuristically assume gRPC-looking requests may require streaming semantics.
            // See https://github.com/microsoft/reverse-proxy/issues/118 for design discussion.
            var isStreamingRequest = isIncomingHttp2 && GrpcProtocolHelper.IsGrpcContentType(context.Request.ContentType);
            if (isStreamingRequest)
            {
                DisableMinRequestBodyDataRateAndMaxRequestBodySize(context);
            }

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 1: Create outgoing HttpRequestMessage
            var upstreamRequest = new HttpRequestMessage(HttpUtilities.GetHttpMethod(context.Request.Method), targetUri)
            {
                // We request HTTP/2, but HttpClient will fallback to HTTP/1.1 if it cannot establish HTTP/2 with the target.
                // This is done without extra round-trips thanks to ALPN. We can detect a downgrade after calling HttpClient.SendAsync
                // (see Step 3 below). TBD how this will change when HTTP/3 is supported.
                Version = Http2Version,
            };

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 2: Setup copy of request body (background) Downstream --► Proxy --► Upstream
            // Note that we must do this before step (3) because step (3) may also add headers to the HttpContent that we set up here.
            var bodyToUpstreamContent = SetupCopyBodyUpstream(context.Request.Body, upstreamRequest, in proxyTelemetryContext, isStreamingRequest, longCancellation);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 3: Copy request headers Downstream --► Proxy --► Upstream
            CopyHeadersToUpstream(context.Request.Headers, upstreamRequest);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 4: Send the outgoing request using HttpClient
            ////this.logger.LogInformation($"   Starting Proxy --> upstream request");
            var upstreamResponse = await httpClient.SendAsync(upstreamRequest, shortCancellation);

            // Detect connection downgrade, which may be problematic for e.g. gRPC.
            if (isIncomingHttp2 && upstreamResponse.Version.Major != 2)
            {
                // TODO: Do something on connection downgrade...
                Log.HttpDowngradeDeteced(_logger);
            }

            // Assert that, if we are proxying content upstream, it must have started by now
            // (since HttpClient.SendAsync has already completed asynchronously).
            // If this check fails, there is a coding defect which would otherwise
            // cause us to wait forever in step 9, so fail fast here.
            if (bodyToUpstreamContent != null && !bodyToUpstreamContent.Started)
            {
                throw new InvalidOperationException("Proxying the downstream request body to the upstream server hasn't started. This is a coding defect.");
            }

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 5: Copy response status line Downstream ◄-- Proxy ◄-- Upstream
            ////this.logger.LogInformation($"   Setting downstream <-- Proxy status: {(int)upstreamResponse.StatusCode} {upstreamResponse.ReasonPhrase}");
            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            context.Features.Get<IHttpResponseFeature>().ReasonPhrase = upstreamResponse.ReasonPhrase;

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 6: Copy response headers Downstream ◄-- Proxy ◄-- Upstream
            CopyHeadersToDownstream(upstreamResponse, context.Response.Headers);

            // NOTE: it may *seem* wise to call `context.Response.StartAsync()` at this point
            // since it looks like we are ready to send back response headers
            // (and this might help reduce extra delays while we wait to receive the body from upstream).
            // HOWEVER, this would produce the wrong result if it turns out that there is no content
            // from the upstream -- instead of sending headers and terminating the stream at once,
            // we would send headers thinking a body may be coming, and there is none.
            // This is problematic on gRPC connections when the upstream server encounters an error,
            // in which case it immediately returns the response headers and trailing headers, but no content,
            // and clients misbehave if the initial headers response does not indicate stream end.

            // TODO: Some of the tasks in steps (7) - (9) may go unobserved depending on what fails first. Needs more consideration.

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 7: Copy response body Downstream ◄-- Proxy ◄-- Upstream
            await CopyBodyDownstreamAsync(upstreamResponse.Content, context.Response.Body, proxyTelemetryContext, longCancellation);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 8: Copy response trailer headers and finish response Downstream ◄-- Proxy ◄-- Upstream
            CopyTrailingHeadersToDownstream(upstreamResponse, context);

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
            // :: Step 9: Wait for completion of step 2: copying request body Downstream --► Proxy --► Upstream
            if (bodyToUpstreamContent != null)
            {
                ////this.logger.LogInformation($"   Waiting for downstream --> Proxy --> upstream body proxying to complete");
                await bodyToUpstreamContent.ConsumptionTask;
            }
        }

        /// <summary>
        /// Proxies an upgradable request to the upstream server, treating the upgraded stream as an opaque duplex channel.
        /// </summary>
        /// <remarks>
        /// Upgradable request proxying comprises the following steps:
        ///    (1)  Create outgoing HttpRequestMessage
        ///    (2)  Copy request headers                                              Downstream ---► Proxy ---► Upstream
        ///    (3)  Send the outgoing request using HttpMessageInvoker                Downstream ---► Proxy ---► Upstream
        ///    (4)  Copy response status line                                         Downstream ◄--- Proxy ◄--- Upstream
        ///    (5)  Copy response headers                                             Downstream ◄--- Proxy ◄--- Upstream
        ///       Scenario A: upgrade with upstream worked (got 101 response)
        ///          (A-6)  Upgrade downstream channel (also sends response headers)  Downstream ◄--- Proxy ◄--- Upstream
        ///          (A-7)  Copy duplex streams                                       Downstream ◄--► Proxy ◄--► Upstream
        ///       ---- or ----
        ///       Scenario B: upgrade with upstream failed (got non-101 response)
        ///          (B-6)  Send response headers                                     Downstream ◄--- Proxy ◄--- Upstream
        ///          (B-7)  Copy response body                                        Downstream ◄--- Proxy ◄--- Upstream
        ///
        /// This takes care of WebSockets as well as any other upgradable protocol.
        /// </remarks>
        private async Task UpgradableProxyAsync(
            HttpContext context,
            IHttpUpgradeFeature upgradeFeature,
            Uri targetUri,
            HttpMessageInvoker httpClient,
            ProxyTelemetryContext proxyTelemetryContext,
            CancellationToken shortCancellation,
            CancellationToken longCancellation)
        {
            Contracts.CheckValue(context, nameof(context));
            Contracts.CheckValue(upgradeFeature, nameof(upgradeFeature));
            Contracts.CheckValue(targetUri, nameof(targetUri));
            Contracts.CheckValue(httpClient, nameof(httpClient));

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 1: Create outgoing HttpRequestMessage
            var upstreamRequest = new HttpRequestMessage(HttpUtilities.GetHttpMethod(context.Request.Method), targetUri)
            {
                // Default to HTTP/1.1 for proxying upgradable requests. This is already the default as of .NET Core 3.1
                Version = new Version(1, 1),
            };

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 2: Copy request headers Downstream --► Proxy --► Upstream
            CopyHeadersToUpstream(context.Request.Headers, upstreamRequest);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 3: Send the outgoing request using HttpMessageInvoker
            var upstreamResponse = await httpClient.SendAsync(upstreamRequest, shortCancellation);
            var upgraded = upstreamResponse.StatusCode == HttpStatusCode.SwitchingProtocols && upstreamResponse.Content != null;

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 4: Copy response status line Downstream ◄-- Proxy ◄-- Upstream
            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            context.Features.Get<IHttpResponseFeature>().ReasonPhrase = upstreamResponse.ReasonPhrase;

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 5: Copy response headers Downstream ◄-- Proxy ◄-- Upstream
            CopyHeadersToDownstream(upstreamResponse, context.Response.Headers);

            if (!upgraded)
            {
                // :::::::::::::::::::::::::::::::::::::::::::::
                // :: Step B-6: Send response headers Downstream ◄-- Proxy ◄-- Upstream
                // This is important to avoid any extra delays in sending response headers
                // e.g. if the upstream server is slow to provide its response body.
                await context.Response.StartAsync(shortCancellation);

                // :::::::::::::::::::::::::::::::::::::::::::::
                // :: Step B-7: Copy response body Downstream ◄-- Proxy ◄-- Upstream
                await CopyBodyDownstreamAsync(upstreamResponse.Content, context.Response.Body, proxyTelemetryContext, longCancellation);
                return;
            }

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step A-6: Upgrade the downstream channel. This will send all response headers too.
            using var downstreamStream = await upgradeFeature.UpgradeAsync();

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step A-7: Copy duplex streams
            var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync();

            var upstreamCopier = new StreamCopier(
                _metrics,
                new StreamCopyTelemetryContext(
                    direction: "upstream",
                    backendId: proxyTelemetryContext.BackendId,
                    routeId: proxyTelemetryContext.RouteId,
                    endpointId: proxyTelemetryContext.EndpointId));
            var upstreamTask = upstreamCopier.CopyAsync(downstreamStream, upstreamStream, longCancellation);

            var downstreamCopier = new StreamCopier(
                _metrics,
                new StreamCopyTelemetryContext(
                    direction: "downstream",
                    backendId: proxyTelemetryContext.BackendId,
                    routeId: proxyTelemetryContext.RouteId,
                    endpointId: proxyTelemetryContext.EndpointId));
            var downstreamTask = downstreamCopier.CopyAsync(upstreamStream, downstreamStream, longCancellation);

            await Task.WhenAll(upstreamTask, downstreamTask);
        }

        private StreamCopyHttpContent SetupCopyBodyUpstream(Stream source, HttpRequestMessage upstreamRequest, in ProxyTelemetryContext proxyTelemetryContext, bool isStreamingRequest, CancellationToken cancellation)
        {
            StreamCopyHttpContent contentToUpstream = null;
            if (source != null)
            {
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
                        backendId: proxyTelemetryContext.BackendId,
                        routeId: proxyTelemetryContext.RouteId,
                        endpointId: proxyTelemetryContext.EndpointId));
                contentToUpstream = new StreamCopyHttpContent(
                    source: source,
                    streamCopier: streamCopier,
                    autoFlushHttpClientOutgoingStream: isStreamingRequest,
                    cancellation: cancellation);
                upstreamRequest.Content = contentToUpstream;
            }

            return contentToUpstream;
        }

        private void CopyHeadersToUpstream(IHeaderDictionary source, HttpRequestMessage destination)
        {
            foreach (var header in source)
            {
                var headerValueCount = header.Value.Count;
                if (headerValueCount == 0)
                {
                    continue;
                }

                // Filter out HTTP/2 pseudo headers like ":method" and ":path", those go into other fields.
                if (header.Key.Length > 0 && header.Key[0] == ':')
                {
                    continue;
                }

                if (_headersToSkipGoingUpstream.Contains(header.Key))
                {
                    continue;
                }

                ////this.logger.LogInformation($"   Copying downstream --> Proxy --> upstream request header {header.Key}: {header.Value}");

                // Note: HttpClient.SendAsync will end up sending the union of
                // HttpRequestMessage.Headers and HttpRequestMessage.Content.Headers.
                // We don't really care where the proxied headers appear among those 2,
                // as long as they appear in one (and only one, otherwise they would be duplicated).
                if (headerValueCount == 1)
                {
                    string headerValue = header.Value;
                    if (!destination.Headers.TryAddWithoutValidation(header.Key, headerValue))
                    {
                        destination.Content?.Headers.TryAddWithoutValidation(header.Key, headerValue);
                    }
                }
                else
                {
                    string[] headerValues = header.Value;
                    if (!destination.Headers.TryAddWithoutValidation(header.Key, headerValues))
                    {
                        destination.Content?.Headers.TryAddWithoutValidation(header.Key, headerValues);
                    }
                }
            }
        }

        private void CopyHeadersToDownstream(HttpResponseMessage source, IHeaderDictionary destination)
        {
            CopyHeaders(source.Headers, destination);
            if (source.Content != null)
            {
                CopyHeaders(source.Content.Headers, destination);
            }

            static void CopyHeaders(HttpHeaders source, IHeaderDictionary destination)
            {
                foreach (var header in source)
                {
                    if (_headersToSkipGoingDownstream.Contains(header.Key))
                    {
                        continue;
                    }

                    ////this.logger.LogInformation($"   Copying downstream <-- Proxy <-- upstream response header {header.Key}: {string.Join(",", header.Value)}");
                    destination.TryAdd(header.Key, new StringValues(header.Value.ToArray()));
                }
            }
        }

        private async Task CopyBodyDownstreamAsync(HttpContent upstreamResponseContent, Stream destination, ProxyTelemetryContext proxyTelemetryContext, CancellationToken cancellation)
        {
            if (upstreamResponseContent != null)
            {
                var streamCopier = new StreamCopier(
                    _metrics,
                    new StreamCopyTelemetryContext(
                        direction: "downstream",
                        backendId: proxyTelemetryContext.BackendId,
                        routeId: proxyTelemetryContext.RouteId,
                        endpointId: proxyTelemetryContext.EndpointId));

                ////this.logger.LogInformation($"   Waiting for downstream <-- Proxy <-- upstream body proxying");
                var upstreamResponseStream = await upstreamResponseContent.ReadAsStreamAsync();
                await streamCopier.CopyAsync(upstreamResponseStream, destination, cancellation);
            }
        }

        private void CopyTrailingHeadersToDownstream(HttpResponseMessage source, HttpContext context)
        {
            // NOTE: Deliberately not using `context.Response.SupportsTrailers()`, `context.Response.AppendTrailer(...)`
            // because they lookup `IHttpResponseTrailersFeature` for every call. Here we do it just once instead.
            var responseTrailersFeature = context.Features.Get<IHttpResponseTrailersFeature>();
            if (responseTrailersFeature?.Trailers != null && !responseTrailersFeature.Trailers.IsReadOnly)
            {
                // Note that trailers, if any, should already have been declared in Proxy's response
                // by virtue of us having proxied all upstream response headers in step 6.
                foreach (var header in source.TrailingHeaders)
                {
                    responseTrailersFeature.Trailers.Add(header.Key, new StringValues(header.Value.ToArray()));
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
            private static readonly Action<ILogger, Exception> _httpDowngradeDeteced = LoggerMessage.Define(
                LogLevel.Information,
                EventIds.HttpDowngradeDeteced,
                "Health check has gracefully shut down.");

            public static void HttpDowngradeDeteced(ILogger logger)
            {
                _httpDowngradeDeteced(logger, null);
            }
        }
    }
}
