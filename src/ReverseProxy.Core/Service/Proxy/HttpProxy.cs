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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Core.Abstractions;
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
        ///    (1)  Create outgoing HttpRequestMessage
        ///    (2)  Setup copy of request body (background)             Downstream --► Proxy --► Upstream
        ///    (3)  Copy request headers                                Downstream --► Proxy --► Upstream
        ///    (4)  Send the outgoing request using HttpMessageInvoker  Downstream --► Proxy --► Upstream
        ///    (5)  Copy response status line                           Downstream ◄-- Proxy ◄-- Upstream
        ///    (6)  Copy response headers                               Downstream ◄-- Proxy ◄-- Upstream
        ///    (7)  Send response headers                               Downstream ◄-- Proxy ◄-- Upstream
        ///    (8)  Copy response body                                  Downstream ◄-- Proxy ◄-- Upstream
        ///    (9)  Wait for completion of step 2: copying request body Downstream --► Proxy --► Upstream
        ///    (10) Copy response trailer headers                       Downstream ◄-- Proxy ◄-- Upstream
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
            var bodyToUpstreamContent = SetupCopyBodyUpstream(context.Request.Body, upstreamRequest, in proxyTelemetryContext, longCancellation);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 3: Copy request headers Downstream --► Proxy --► Upstream
            CopyHeadersToUpstream(context.Request.Headers, upstreamRequest);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 4: Send the outgoing request using HttpClient
            ////this.logger.LogInformation($"   Starting Proxy --> upstream request");
            var upstreamResponse = await httpClient.SendAsync(upstreamRequest, shortCancellation);

            // Detect connection downgrade, which may be problematic for e.g. gRPC.
            if (upstreamResponse.Version.Major != 2 && HttpUtilities.IsHttp2(context.Request.Protocol))
            {
                // TODO: Do something on connection downgrade...
                _logger.LogInformation($"HTTP version downgrade detected! This may break gRPC communications.");
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

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 7: Send response headers Downstream ◄-- Proxy ◄-- Upstream
            // This is important to avoid any extra delays in sending response headers
            // e.g. if the upstream server is slow to provide its response body.
            ////this.logger.LogInformation($"   Starting downstream <-- Proxy response");
            // TODO: Some of the tasks in steps (7) - (9) may go unobserved depending on what fails first. Needs more consideration.
            await context.Response.StartAsync(shortCancellation);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 8: Copy response body Downstream ◄-- Proxy ◄-- Upstream
            await CopyBodyDownstreamAsync(upstreamResponse.Content, context.Response.Body, proxyTelemetryContext, longCancellation);

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 9: Wait for completion of step 2: copying request body Downstream --► Proxy --► Upstream
            if (bodyToUpstreamContent != null)
            {
                ////this.logger.LogInformation($"   Waiting for downstream --> Proxy --> upstream body proxying to complete");
                await bodyToUpstreamContent.ConsumptionTask;
            }

            // :::::::::::::::::::::::::::::::::::::::::::::
            // :: Step 10: Copy response trailer headers Downstream ◄-- Proxy ◄-- Upstream
            CopyTrailingHeadersToDownstream(upstreamResponse, context);
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

        private StreamCopyHttpContent SetupCopyBodyUpstream(Stream source, HttpRequestMessage upstreamRequest, in ProxyTelemetryContext proxyTelemetryContext, CancellationToken cancellation)
        {
            StreamCopyHttpContent contentToUpstream = null;
            if (source != null)
            {
                ////this.logger.LogInformation($"   Setting up downstream --> Proxy --> upstream body proxying");

                var streamCopier = new StreamCopier(
                    _metrics,
                    new StreamCopyTelemetryContext(
                        direction: "upstream",
                        backendId: proxyTelemetryContext.BackendId,
                        routeId: proxyTelemetryContext.RouteId,
                        endpointId: proxyTelemetryContext.EndpointId));
                contentToUpstream = new StreamCopyHttpContent(source, streamCopier, cancellation);
                upstreamRequest.Content = contentToUpstream;
            }

            return contentToUpstream;
        }

        private void CopyHeadersToUpstream(IHeaderDictionary source, HttpRequestMessage destination)
        {
            foreach (var header in source)
            {
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
                if (!destination.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                {
                    destination.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
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
    }
}
