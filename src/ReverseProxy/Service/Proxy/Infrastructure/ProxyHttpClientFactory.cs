// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Telemetry;

namespace Yarp.ReverseProxy.Service.Proxy.Infrastructure
{
    /// <summary>
    /// Default implementation of <see cref="IProxyHttpClientFactory"/>.
    /// </summary>
    internal class ProxyHttpClientFactory : IProxyHttpClientFactory
    {
        private readonly ILogger<ProxyHttpClientFactory> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProxyHttpClientFactory"/> class.
        /// </summary>
        public ProxyHttpClientFactory(ILogger<ProxyHttpClientFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public HttpMessageInvoker CreateClient(ProxyHttpClientContext context)
        {
            if (CanReuseOldClient(context))
            {
                Log.ProxyClientReused(_logger, context.ClusterId);
                return context.OldClient;
            }

            var newClientOptions = context.NewOptions;
            var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false

                // NOTE: MaxResponseHeadersLength = 64, which means up to 64 KB of headers are allowed by default as of .NET Core 3.1.
            };

            if (newClientOptions.SslProtocols.HasValue)
            {
                handler.SslOptions.EnabledSslProtocols = newClientOptions.SslProtocols.Value;
            }
            if (newClientOptions.ClientCertificate != null)
            {
                handler.SslOptions.ClientCertificates = new X509CertificateCollection
                {
                    newClientOptions.ClientCertificate
                };
            }
            if (newClientOptions.MaxConnectionsPerServer != null)
            {
                handler.MaxConnectionsPerServer = newClientOptions.MaxConnectionsPerServer.Value;
            }
            if (newClientOptions.DangerousAcceptAnyServerCertificate ?? false)
            {
                handler.SslOptions.RemoteCertificateValidationCallback = delegate { return true; };
            }
#if NET
            handler.EnableMultipleHttp2Connections = newClientOptions.EnableMultipleHttp2Connections.GetValueOrDefault(true);

            if (newClientOptions.RequestHeaderEncoding != null)
            {
                handler.RequestHeaderEncodingSelector = (_, _) => newClientOptions.RequestHeaderEncoding;
            }
#endif

            var webProxy = TryCreateWebProxy(newClientOptions.WebProxy);
            if (webProxy != null)
            {
                handler.Proxy = webProxy;
                handler.UseProxy = true;
            }

            Log.ProxyClientCreated(_logger, context.ClusterId);

            var activityContextHeaders = newClientOptions.ActivityContextHeaders.GetValueOrDefault(ActivityContextHeaders.BaggageAndCorrelationContext);
            if (activityContextHeaders != ActivityContextHeaders.None)
            {
                return new HttpMessageInvoker(new ActivityPropagationHandler(activityContextHeaders, handler), disposeHandler: true);
            }

            return new HttpMessageInvoker(handler, disposeHandler: true);
        }

        private static IWebProxy TryCreateWebProxy(WebProxyOptions webProxyOptions)
        {
            if (webProxyOptions == null || webProxyOptions.Address == null)
            {
                return null;
            }

            var webProxy = new WebProxy(webProxyOptions.Address);
            webProxy.UseDefaultCredentials = webProxyOptions.UseDefaultCredentials.GetValueOrDefault(false);

            webProxy.BypassProxyOnLocal = webProxyOptions.BypassOnLocal.GetValueOrDefault(false);

            return webProxy;
        }

        private static bool CanReuseOldClient(ProxyHttpClientContext context)
        {
            return context.OldClient != null && context.NewOptions == context.OldOptions;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _proxyClientCreated = LoggerMessage.Define<string>(
                  LogLevel.Debug,
                  EventIds.ProxyClientCreated,
                  "New proxy client created for cluster '{clusterId}'.");

            private static readonly Action<ILogger, string, Exception> _proxyClientReused = LoggerMessage.Define<string>(
                LogLevel.Debug,
                EventIds.ProxyClientReused,
                "Existing proxy client reused for cluster '{clusterId}'.");

            public static void ProxyClientCreated(ILogger logger, string clusterId)
            {
                _proxyClientCreated(logger, clusterId, null);
            }

            public static void ProxyClientReused(ILogger logger, string clusterId)
            {
                _proxyClientReused(logger, clusterId, null);
            }
        }
    }
}
