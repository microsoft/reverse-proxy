// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Yarp.ReverseProxy
{
    public class TelemetryConsumptionTests
    {
        public enum RegistrationApproach
        {
            WithInstanceHelper,
            WithGenericHelper,
            Manual
        }

        private static void Register(IServiceCollection services, RegistrationApproach approach)
        {
            if (approach == RegistrationApproach.WithInstanceHelper)
            {
                services.AddTelemetryConsumer(new TelemetryConsumer());
                services.AddTelemetryConsumer(new SecondTelemetryConsumer());
            }
            else if (approach == RegistrationApproach.WithGenericHelper)
            {
                services.AddTelemetryConsumer<TelemetryConsumer>();
                services.AddTelemetryConsumer<SecondTelemetryConsumer>();
            }
            else if (approach == RegistrationApproach.Manual)
            {
                services.AddSingleton<TelemetryConsumer>();
                services.AddSingleton(services => (IForwarderTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
                services.AddSingleton(services => (IKestrelTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
#if NET
                services.AddSingleton(services => (IHttpTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
                services.AddSingleton(services => (INameResolutionTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
                services.AddSingleton(services => (INetSecurityTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
                services.AddSingleton(services => (ISocketsTelemetryConsumer)services.GetRequiredService<TelemetryConsumer>());
#endif

                services.AddSingleton<SecondTelemetryConsumer>();
                services.AddSingleton(services => (IForwarderTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
                services.AddSingleton(services => (IKestrelTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
#if NET
                services.AddSingleton(services => (IHttpTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
                services.AddSingleton(services => (INameResolutionTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
                services.AddSingleton(services => (INetSecurityTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
                services.AddSingleton(services => (ISocketsTelemetryConsumer)services.GetRequiredService<SecondTelemetryConsumer>());
#endif

                services.AddTelemetryListeners();
            }
        }

        private static void VerifyStages(string[] expected, List<(string Stage, DateTime Timestamp)> stages)
        {
            Assert.Equal(expected, stages.Select(s => s.Stage).ToArray());

            for (var i = 1; i < stages.Count; i++)
            {
                Assert.True(stages[i - 1].Timestamp <= stages[i].Timestamp);
            }
        }

        [Theory]
        [InlineData(RegistrationApproach.WithInstanceHelper)]
        [InlineData(RegistrationApproach.WithGenericHelper)]
        [InlineData(RegistrationApproach.Manual)]
        public async Task TelemetryConsumptionWorks(RegistrationApproach registrationApproach)
        {
            var test = new TestEnvironment(
                async context => await context.Response.WriteAsync("Foo"),
                proxyBuilder => Register(proxyBuilder.Services, registrationApproach),
                proxyApp => { },
                useHttpsOnDestination: true);

            test.ClusterId = Guid.NewGuid().ToString();

            await test.Invoke(async uri =>
            {
                using var httpClient = new HttpClient();
                await httpClient.GetStringAsync(uri);
            });

            var expected = new[]
            {
                "OnRequestStart-Kestrel",
                "OnForwarderInvoke",
                "OnForwarderStart",
                "OnForwarderStage-SendAsyncStart",
#if NET
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
#endif
                "OnForwarderStage-SendAsyncStop",
                "OnForwarderStage-ResponseContentTransferStart",
                "OnContentTransferred",
                "OnForwarderStop",
                "OnRequestStop-Kestrel"
            };

            foreach (var consumerType in new[] { typeof(TelemetryConsumer), typeof(SecondTelemetryConsumer) })
            {
                Assert.True(TelemetryConsumer.PerClusterTelemetry.TryGetValue((test.ClusterId, consumerType), out var stages));
                VerifyStages(expected, stages);
            }
        }

#if NET
        [Theory]
        [InlineData(RegistrationApproach.WithInstanceHelper)]
        [InlineData(RegistrationApproach.WithGenericHelper)]
        [InlineData(RegistrationApproach.Manual)]
        public async Task NonProxyTelemetryConsumptionWorks(RegistrationApproach registrationApproach)
        {
            var test = new TestEnvironment(
                async context => await context.Response.WriteAsync("Foo"),
                proxyBuilder => Register(proxyBuilder.Services, registrationApproach),
                proxyApp => { },
                useHttpsOnDestination: true);

            var path = $"/{Guid.NewGuid()}";

            await test.Invoke(async uri =>
            {
                using var httpClient = new HttpClient();
                await httpClient.GetStringAsync($"{uri.TrimEnd('/')}{path}");
            });

            var expected = new[]
            {
                "OnRequestStart",
                "OnConnectStart",
                "OnConnectStop",
                "OnConnectionEstablished",
                "OnRequestHeadersStart",
                "OnRequestHeadersStop",
                "OnResponseHeadersStart",
                "OnResponseHeadersStop",
                "OnRequestStop"
            };

            foreach (var consumerType in new[] { typeof(TelemetryConsumer), typeof(SecondTelemetryConsumer) })
            {
                Assert.True(TelemetryConsumer.PerPathAndQueryTelemetry.TryGetValue((path, consumerType), out var stages));
                VerifyStages(expected, stages);
            }
        }
#endif

        private class SecondTelemetryConsumer : TelemetryConsumer { }

        private class TelemetryConsumer :
            IForwarderTelemetryConsumer,
            IKestrelTelemetryConsumer
#if NET
            ,
            IHttpTelemetryConsumer,
            INameResolutionTelemetryConsumer,
            INetSecurityTelemetryConsumer,
            ISocketsTelemetryConsumer
#endif
        {
            public static readonly ConcurrentDictionary<(string, Type), List<(string Stage, DateTime Timestamp)>> PerClusterTelemetry = new();
            public static readonly ConcurrentDictionary<(string, Type), List<(string Stage, DateTime Timestamp)>> PerPathAndQueryTelemetry = new();

            private readonly AsyncLocal<List<(string Stage, DateTime Timestamp)>> _stages = new();

            private void AddStage(string stage, DateTime timestamp)
            {
                var stages = _stages.Value ??= new List<(string Stage, DateTime Timestamp)>();

                lock (stages)
                {
                    stages.Add((stage, timestamp));
                }
            }

            public void OnForwarderStart(DateTime timestamp, string destinationPrefix) => AddStage(nameof(OnForwarderStart), timestamp);
            public void OnForwarderStop(DateTime timestamp, int statusCode) => AddStage(nameof(OnForwarderStop), timestamp);
            public void OnForwarderFailed(DateTime timestamp, ForwarderError error) => AddStage(nameof(OnForwarderFailed), timestamp);
            public void OnForwarderStage(DateTime timestamp, Telemetry.Consumption.ForwarderStage stage) => AddStage($"{nameof(OnForwarderStage)}-{stage}", timestamp);
            public void OnContentTransferring(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime) => AddStage(nameof(OnContentTransferring), timestamp);
            public void OnContentTransferred(DateTime timestamp, bool isRequest, long contentLength, long iops, TimeSpan readTime, TimeSpan writeTime, TimeSpan firstReadTime) => AddStage(nameof(OnContentTransferred), timestamp);
            public void OnForwarderInvoke(DateTime timestamp, string clusterId, string routeId, string destinationId)
            {
                AddStage(nameof(OnForwarderInvoke), timestamp);
                PerClusterTelemetry.TryAdd((clusterId, GetType()), _stages.Value);
            }
#if NET
            public void OnRequestStart(DateTime timestamp, string scheme, string host, int port, string pathAndQuery, int versionMajor, int versionMinor, HttpVersionPolicy versionPolicy)
            {
                AddStage(nameof(OnRequestStart), timestamp);
                PerPathAndQueryTelemetry.TryAdd((pathAndQuery, GetType()), _stages.Value);
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
#else
            public void OnRequestStart(DateTime timestamp, string connectionId, string requestId) => AddStage($"{nameof(OnRequestStart)}-Kestrel", timestamp);
            public void OnRequestStop(DateTime timestamp, string connectionId, string requestId) => AddStage($"{nameof(OnRequestStop)}-Kestrel", timestamp);
#endif
        }

        [Fact]
        public async Task MetricsConsumptionWorks()
        {
            MetricsOptions.Interval = TimeSpan.FromMilliseconds(10);

            var consumer = new MetricsConsumer();

            var test = new TestEnvironment(
                async context =>
                {
                    await context.Response.WriteAsync("Foo");
                },
                proxyBuilder =>
                {
                    var services = proxyBuilder.Services;

                    services.AddSingleton<IForwarderMetricsConsumer>(consumer);
#if NET
                    services.AddSingleton<IKestrelMetricsConsumer>(consumer);
                    services.AddSingleton<IHttpMetricsConsumer>(consumer);
                    services.AddSingleton<ISocketsMetricsConsumer>(consumer);
                    services.AddSingleton<INetSecurityMetricsConsumer>(consumer);
                    services.AddSingleton<INameResolutionMetricsConsumer>(consumer);
#endif

                    services.AddTelemetryListeners();
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
                    WaitAsync(() => consumer.ProxyMetrics.LastOrDefault()?.RequestsStarted > 0, nameof(ForwarderMetrics))
#if NET
                    ,
                    WaitAsync(() => consumer.KestrelMetrics.LastOrDefault()?.TotalConnections > 0, nameof(KestrelMetrics)),
                    WaitAsync(() => consumer.HttpMetrics.LastOrDefault()?.RequestsStarted > 0, nameof(HttpMetrics)),
                    WaitAsync(() => consumer.SocketsMetrics.LastOrDefault()?.OutgoingConnectionsEstablished > 0, nameof(SocketsMetrics)),
                    WaitAsync(() => consumer.NetSecurityMetrics.LastOrDefault()?.TotalTlsHandshakes > 0, nameof(NetSecurityMetrics)),
                    WaitAsync(() => consumer.NameResolutionMetrics.LastOrDefault()?.DnsLookupsRequested > 0, nameof(NameResolutionMetrics))
#endif
                    );
            });

            VerifyTimestamp(consumer.ProxyMetrics.Last().Timestamp);
#if NET
            VerifyTimestamp(consumer.KestrelMetrics.Last().Timestamp);
            VerifyTimestamp(consumer.HttpMetrics.Last().Timestamp);
            VerifyTimestamp(consumer.SocketsMetrics.Last().Timestamp);
            VerifyTimestamp(consumer.NetSecurityMetrics.Last().Timestamp);
            VerifyTimestamp(consumer.NameResolutionMetrics.Last().Timestamp);
#endif

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
            IForwarderMetricsConsumer
#if NET
            ,
            IKestrelMetricsConsumer,
            IHttpMetricsConsumer,
            INameResolutionMetricsConsumer,
            INetSecurityMetricsConsumer,
            ISocketsMetricsConsumer
#endif
        {
            public readonly ConcurrentQueue<ForwarderMetrics> ProxyMetrics = new ConcurrentQueue<ForwarderMetrics>();
#if NET
            public readonly ConcurrentQueue<KestrelMetrics> KestrelMetrics = new();
            public readonly ConcurrentQueue<HttpMetrics> HttpMetrics = new();
            public readonly ConcurrentQueue<SocketsMetrics> SocketsMetrics = new();
            public readonly ConcurrentQueue<NetSecurityMetrics> NetSecurityMetrics = new();
            public readonly ConcurrentQueue<NameResolutionMetrics> NameResolutionMetrics = new();
#endif

            public void OnForwarderMetrics(ForwarderMetrics oldMetrics, ForwarderMetrics newMetrics) => ProxyMetrics.Enqueue(newMetrics);
#if NET
            public void OnKestrelMetrics(KestrelMetrics oldMetrics, KestrelMetrics newMetrics) => KestrelMetrics.Enqueue(newMetrics);
            public void OnSocketsMetrics(SocketsMetrics oldMetrics, SocketsMetrics newMetrics) => SocketsMetrics.Enqueue(newMetrics);
            public void OnNetSecurityMetrics(NetSecurityMetrics oldMetrics, NetSecurityMetrics newMetrics) => NetSecurityMetrics.Enqueue(newMetrics);
            public void OnNameResolutionMetrics(NameResolutionMetrics oldMetrics, NameResolutionMetrics newMetrics) => NameResolutionMetrics.Enqueue(newMetrics);
            public void OnHttpMetrics(HttpMetrics oldMetrics, HttpMetrics newMetrics) => HttpMetrics.Enqueue(newMetrics);
#endif
        }
    }
}
