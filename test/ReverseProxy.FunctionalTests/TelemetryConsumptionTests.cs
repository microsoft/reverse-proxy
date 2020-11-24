// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET5_0
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Common;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Telemetry;
using Microsoft.ReverseProxy.Telemetry.Consumption;
using Xunit;

namespace Microsoft.ReverseProxy
{
    public class TelemetryConsumptionTests
    {
        [Fact]
        public async Task TelemetryConsumptionWorks()
        {
            var consumers = new ConcurrentBag<TelemetryConsumer>();

            var test = new TestEnvironment(
                async context =>
                {
                    await context.Response.WriteAsync("Foo");
                },
                proxyBuilder =>
                {
                    var services = proxyBuilder.Services;

                    services.AddScoped(services =>
                    {
                        var consumer = new TelemetryConsumer();
                        consumers.Add(consumer);
                        return consumer;
                    });
                    services.AddScoped<IProxyTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
                    services.AddScoped<IKestrelTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
                    services.AddScoped<IHttpTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
                    services.AddScoped<ISocketsTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
                    services.AddScoped<INetSecurityTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());
                    services.AddScoped<INameResolutionTelemetryConsumer>(services => services.GetRequiredService<TelemetryConsumer>());

                    proxyBuilder.AddTelemetryListeners();
                },
                proxyApp => { },
                useHttpsOnDestination: true);

            var guid = Guid.NewGuid().ToString();

            await test.Invoke(async uri =>
            {
                var httpClient = new HttpClient();
                await httpClient.GetStringAsync($"{uri}/{guid}");
            });

            var stages = Assert.Single(consumers, c => c.PathAndQuery != null && c.PathAndQuery.Contains(guid)).Stages;

            var expected = new[]
            {
                "OnRequestStart-Kestrel",
                "OnProxyInvoke",
                "OnProxyStart",
                "OnProxyStage-SendAsyncStart",
                "OnRequestStart",
                "OnConnectStart",
                "OnConnectStop",
                "OnHandshakeStart",
                "OnHandshakeStop",
                "OnConnectionEstablished",
                "OnRequestHeadersStart",
                "OnRequestHeadersStop",
                "OnResponseHeadersStart",
                "OnResponseHeadersStop",
                "OnRequestStop",
                "OnProxyStage-SendAsyncStop",
                "OnProxyStage-ResponseContentTransferStart",
                "OnContentTransferred",
                "OnProxyStop",
                "OnRequestStop-Kestrel"
            };

            Assert.Equal(expected, stages.Select(s => s.Stage).ToArray());

            for (var i = 1; i < stages.Count; i++)
            {
                Assert.True(stages[i - 1].Timestamp <= stages[i].Timestamp);
            }
        }

        private sealed class TelemetryConsumer :
            IProxyTelemetryConsumer,
            IKestrelTelemetryConsumer,
            IHttpTelemetryConsumer,
            INameResolutionTelemetryConsumer,
            INetSecurityTelemetryConsumer,
            ISocketsTelemetryConsumer
        {
            public readonly List<(string Stage, DateTime Timestamp)> Stages = new(16);

            public string PathAndQuery { get; set; }

            private void AddStage(string stage, DateTime timestamp)
            {
                lock (Stages)
                {
                    Stages.Add((stage, timestamp));
                }
            }

            public void OnProxyStart(DateTime timestamp, string destinationPrefix) => AddStage(nameof(OnProxyStart), timestamp);
            public void OnProxyStop(DateTime timestamp, int statusCode) => AddStage(nameof(OnProxyStop), timestamp);
            public void OnProxyFailed(DateTime timestamp, ProxyError error) => AddStage(nameof(OnProxyFailed), timestamp);
            public void OnProxyStage(DateTime timestamp, ProxyStage stage) => AddStage($"{nameof(OnProxyStage)}-{stage}", timestamp);
            public void OnContentTransferring(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime) => AddStage(nameof(OnContentTransferring), timestamp);
            public void OnContentTransferred(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime, TimeSpan firstReadTime) => AddStage(nameof(OnContentTransferred), timestamp);
            public void OnProxyInvoke(DateTime timestamp, string clusterId, string routeId, string destinationId) => AddStage(nameof(OnProxyInvoke), timestamp);
            public void OnRequestStart(DateTime timestamp, string scheme, string host, int port, string pathAndQuery, int versionMajor, int versionMinor, HttpVersionPolicy versionPolicy)
            {
                AddStage(nameof(OnRequestStart), timestamp);
                PathAndQuery = pathAndQuery;
            }
            public void OnRequestStop(DateTime timestamp) => AddStage(nameof(OnRequestStop), timestamp);
            public void OnRequestFailed(DateTime timestamp) => AddStage(nameof(OnRequestFailed), timestamp);
            public void OnConnectionEstablished(DateTime timestamp, int versionMajor, int versionMinor) => AddStage(nameof(OnConnectionEstablished), timestamp);
            public void OnRequestLeftQueue(DateTime timestamp, TimeSpan timeOnQueue, int versionMajor, int versionMinor) => AddStage(nameof(OnRequestLeftQueue), timestamp);
            public void OnRequestHeadersStart(DateTime timestamp) => AddStage(nameof(OnRequestHeadersStart), timestamp);
            public void OnRequestHeadersStop(DateTime timestamp) => AddStage(nameof(OnRequestHeadersStop), timestamp);
            public void OnRequestContentStart(DateTime timestamp) => AddStage(nameof(OnRequestContentStart), timestamp);
            public void OnRequestContentStop(DateTime timestamp, long contentLength) => AddStage(nameof(OnRequestContentStop), timestamp);
            public void OnResponseHeadersStart(DateTime timestamp) => AddStage(nameof(OnResponseHeadersStart), timestamp);
            public void OnResponseHeadersStop(DateTime timestamp) => AddStage(nameof(OnResponseHeadersStop), timestamp);
            public void OnResolutionStart(DateTime timestamp, string hostNameOrAddress) => AddStage(nameof(OnResolutionStart), timestamp);
            public void OnResolutionStop(DateTime timestamp) => AddStage(nameof(OnResolutionStop), timestamp);
            public void OnResolutionFailed(DateTime timestamp) => AddStage(nameof(OnResolutionFailed), timestamp);
            public void OnHandshakeStart(DateTime timestamp, bool isServer, string targetHost) => AddStage(nameof(OnHandshakeStart), timestamp);
            public void OnHandshakeStop(DateTime timestamp, SslProtocols protocol) => AddStage(nameof(OnHandshakeStop), timestamp);
            public void OnHandshakeFailed(DateTime timestamp, bool isServer, TimeSpan elapsed, string exceptionMessage) => AddStage(nameof(OnHandshakeFailed), timestamp);
            public void OnConnectStart(DateTime timestamp, string address) => AddStage(nameof(OnConnectStart), timestamp);
            public void OnConnectStop(DateTime timestamp) => AddStage(nameof(OnConnectStop), timestamp);
            public void OnConnectFailed(DateTime timestamp, SocketError error, string exceptionMessage) => AddStage(nameof(OnConnectFailed), timestamp);
            public void OnRequestStart(DateTime timestamp, string connectionId, string requestId, string httpVersion, string path, string method) => AddStage($"{nameof(OnRequestStart)}-Kestrel", timestamp);
            public void OnRequestStop(DateTime timestamp, string connectionId, string requestId, string httpVersion, string path, string method) => AddStage($"{nameof(OnRequestStop)}-Kestrel", timestamp);
        }

        [Fact]
        public async Task MetricsConsumptionWorks()
        {
            MetricsOptions.IntervalSeconds = 0.01f;

            var consumer = new MetricsConsumer();

            var test = new TestEnvironment(
                async context =>
                {
                    await context.Response.WriteAsync("Foo");
                },
                proxyBuilder =>
                {
                    var services = proxyBuilder.Services;

                    services.AddSingleton<IProxyMetricsConsumer>(consumer);
                    services.AddSingleton<IKestrelMetricsConsumer>(consumer);
                    services.AddSingleton<IHttpMetricsConsumer>(consumer);
                    services.AddSingleton<ISocketsMetricsConsumer>(consumer);
                    services.AddSingleton<INetSecurityMetricsConsumer>(consumer);
                    services.AddSingleton<INameResolutionMetricsConsumer>(consumer);

                    proxyBuilder.AddTelemetryListeners();
                },
                proxyApp => { },
                useHttpsOnDestination: true);

            await test.Invoke(async uri =>
            {
                var httpClient = new HttpClient();
                await httpClient.GetStringAsync(uri);

                try
                {
                    // Do some arbitrary DNS work to get metrics, since we're connecting to localhost
                    _ = await Dns.GetHostAddressesAsync("microsoft.com");
                }
                catch { }

                await Task.WhenAll(
                    WaitAsync(() => consumer.ProxyMetrics.LastOrDefault()?.RequestsStarted > 0, nameof(ProxyMetrics)),
                    WaitAsync(() => consumer.KestrelMetrics.LastOrDefault()?.TotalConnections > 0, nameof(KestrelMetrics)),
                    WaitAsync(() => consumer.HttpMetrics.LastOrDefault()?.RequestsStarted > 0, nameof(HttpMetrics)),
                    WaitAsync(() => consumer.SocketsMetrics.LastOrDefault()?.OutgoingConnectionsEstablished > 0, nameof(SocketsMetrics)),
                    WaitAsync(() => consumer.NetSecurityMetrics.LastOrDefault()?.TotalTlsHandshakes > 0, nameof(NetSecurityMetrics)),
                    WaitAsync(() => consumer.NameResolutionMetrics.LastOrDefault()?.DnsLookupsRequested > 0, nameof(NameResolutionMetrics)));
            });

            VerifyTimestamp(consumer.ProxyMetrics.Last().Timestamp);
            VerifyTimestamp(consumer.KestrelMetrics.Last().Timestamp);
            VerifyTimestamp(consumer.HttpMetrics.Last().Timestamp);
            VerifyTimestamp(consumer.SocketsMetrics.Last().Timestamp);
            VerifyTimestamp(consumer.NetSecurityMetrics.Last().Timestamp);
            VerifyTimestamp(consumer.NameResolutionMetrics.Last().Timestamp);

            static void VerifyTimestamp(DateTime timestamp)
            {
                var now = DateTime.UtcNow;
                Assert.InRange(timestamp, now.Subtract(TimeSpan.FromSeconds(10)), now.AddSeconds(10));
            }

            static async Task WaitAsync(Func<bool> condition, string name)
            {
                var stopwatch = Stopwatch.StartNew();
                while (!condition())
                {
                    if (stopwatch.Elapsed > TimeSpan.FromSeconds(10))
                    {
                        throw new TimeoutException($"Timed out waiting for {name}");
                    }
                    await Task.Delay(10);
                }
            }
        }

        private sealed class MetricsConsumer :
            IProxyMetricsConsumer,
            IKestrelMetricsConsumer,
            IHttpMetricsConsumer,
            INameResolutionMetricsConsumer,
            INetSecurityMetricsConsumer,
            ISocketsMetricsConsumer
        {
            public readonly ConcurrentQueue<ProxyMetrics> ProxyMetrics = new();
            public readonly ConcurrentQueue<KestrelMetrics> KestrelMetrics = new();
            public readonly ConcurrentQueue<HttpMetrics> HttpMetrics = new();
            public readonly ConcurrentQueue<SocketsMetrics> SocketsMetrics = new();
            public readonly ConcurrentQueue<NetSecurityMetrics> NetSecurityMetrics = new();
            public readonly ConcurrentQueue<NameResolutionMetrics> NameResolutionMetrics = new();

            public void OnSocketsMetrics(SocketsMetrics oldMetrics, SocketsMetrics newMetrics) => SocketsMetrics.Enqueue(newMetrics);
            public void OnNetSecurityMetrics(NetSecurityMetrics oldMetrics, NetSecurityMetrics newMetrics) => NetSecurityMetrics.Enqueue(newMetrics);
            public void OnNameResolutionMetrics(NameResolutionMetrics oldMetrics, NameResolutionMetrics newMetrics) => NameResolutionMetrics.Enqueue(newMetrics);
            public void OnHttpMetrics(HttpMetrics oldMetrics, HttpMetrics newMetrics) => HttpMetrics.Enqueue(newMetrics);
            public void OnKestrelMetrics(KestrelMetrics oldMetrics, KestrelMetrics newMetrics) => KestrelMetrics.Enqueue(newMetrics);
            public void OnProxyMetrics(ProxyMetrics oldMetrics, ProxyMetrics newMetrics) => ProxyMetrics.Enqueue(newMetrics);
        }
    }
}
#endif
