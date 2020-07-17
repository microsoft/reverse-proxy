using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Configuration;
using Xunit;

namespace Microsoft.ReverseProxy
{
    public class WebSocketTests
    {
        [Theory]
        [InlineData(WebSocketMessageType.Binary)]
        [InlineData(WebSocketMessageType.Text)]
        public async Task WebSocketTest(WebSocketMessageType messageType)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            using var destinationHost = CreateDestinationHost();
            await destinationHost.StartAsync(cts.Token);
            var destinationHostUrl = destinationHost.GetAddress();

            using var proxyHost = CreateReverseProxyHost(destinationHostUrl);
            await proxyHost.StartAsync(cts.Token);
            var proxyHostUrl = proxyHost.GetAddress();

            using var client = new ClientWebSocket();
            var webSocketsTarget = proxyHostUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            var targetUri = new Uri(new Uri(webSocketsTarget, UriKind.Absolute), "websockets");
            await client.ConnectAsync(targetUri, cts.Token);

            var buffer = new byte[1024];
            var textToSend = $"Hello World!";
            var numBytes = Encoding.UTF8.GetBytes(textToSend, buffer.AsSpan());
            await client.SendAsync(new ArraySegment<byte>(buffer, 0, numBytes),
                messageType,
                endOfMessage: true,
                cts.Token);

            var message = await client.ReceiveAsync(buffer, cts.Token);

            Assert.Equal(messageType, message.MessageType);
            Assert.True(message.EndOfMessage);

            var text = Encoding.UTF8.GetString(buffer.AsSpan(0, message.Count));
            Assert.Equal(textToSend, text);

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", cts.Token);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, client.CloseStatus);
            Assert.Equal("Bye", client.CloseStatusDescription);

            await destinationHost.StopAsync(cts.Token);
            await proxyHost.StopAsync(cts.Token);
        }

        [Fact]
        public async Task RawUpgradeTest()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            using var destinationHost = CreateDestinationHost();
            await destinationHost.StartAsync(cts.Token);
            var destinationHostUrl = destinationHost.GetAddress();

            using var proxyHost = CreateReverseProxyHost(destinationHostUrl);
            await proxyHost.StartAsync(cts.Token);
            var proxyHostUrl = proxyHost.GetAddress();

            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                UseProxy = false
            };
            using var client = new HttpMessageInvoker(handler);
            var targetUri = new Uri(new Uri(proxyHostUrl, UriKind.Absolute), "rawupgrade");
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);

            // TODO: https://github.com/microsoft/reverse-proxy/issues/255 Until this is fixed the "Upgrade: WebSocket" header is required.
            request.Headers.TryAddWithoutValidation("Upgrade", "WebSocket");

            request.Headers.TryAddWithoutValidation("Connection", "upgrade");
            request.Version = new Version(1, 1);

            var response = await client.SendAsync(request, cts.Token);

            Assert.Equal(HttpStatusCode.SwitchingProtocols, response.StatusCode);

#if NETCOREAPP3_1
            using var rawStream = await response.Content.ReadAsStreamAsync();
#elif NETCOREAPP5_0
            using var rawStream = await response.Content.ReadAsStreamAsync(cts.Token);
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif

            var buffer = new byte[1];
            for (var i = 0; i <= 255; i++)
            {
                buffer[0] = (byte)i;
                await rawStream.WriteAsync(buffer, 0, 1, cts.Token);
                var read = await rawStream.ReadAsync(buffer, cts.Token);

                Assert.Equal(1, read);
                Assert.Equal(i, buffer[0]);
            }

            rawStream.Dispose();
            await destinationHost.StopAsync(cts.Token);
            await proxyHost.StopAsync(cts.Token);
        }

        [Fact]
        // https://github.com/microsoft/reverse-proxy/issues/255 IIS claims all requests are upgradeable.
        public async Task FalseUpgradeTest()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            using var destinationHost = CreateDestinationHost();
            await destinationHost.StartAsync(cts.Token);
            var destinationHostUrl = destinationHost.GetAddress();

            using var proxyHost = CreateReverseProxyHost(destinationHostUrl, forceUpgradable: true);
            await proxyHost.StartAsync(cts.Token);
            var proxyHostUrl = proxyHost.GetAddress();

            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                UseProxy = false
            };
            using var client = new HttpMessageInvoker(handler);
            var targetUri = new Uri(new Uri(proxyHostUrl, UriKind.Absolute), "post");
            using var request = new HttpRequestMessage(HttpMethod.Post, targetUri);
            request.Content = new StringContent("Hello World");
            request.Version = new Version(1, 1);

            var response = await client.SendAsync(request, cts.Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
#if NETCOREAPP3_1
            Assert.Equal("Hello World", await response.Content.ReadAsStringAsync());
#elif NETCOREAPP5_0
            Assert.Equal("Hello World", await response.Content.ReadAsStringAsync(cts.Token));
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif

            await destinationHost.StopAsync(cts.Token);
            await proxyHost.StopAsync(cts.Token);
        }

        private IHost CreateReverseProxyHost(string destinationHostUrl, bool forceUpgradable = false)
        {
            return CreateHost(services =>
            {
                services.AddRouting();

                services.AddReverseProxy();
                services.Configure<ProxyConfigOptions>(x =>
                {
                    var proxyRoute = new ProxyRoute
                    {
                        RouteId = "route1",
                        ClusterId = "cluster1"
                    };
                    proxyRoute.Match.Path = "/{**catchall}";

                    x.Routes.Add(proxyRoute);


                    var cluster = new Cluster
                    {
                        Id = "cluster1"
                    };

                    var destination = new Destination
                    {
                        Address = destinationHostUrl
                    };

                    cluster.Destinations.Add("cluster1", destination);

                    x.Clusters.Add("cluster1", cluster);
                });

                services.AddHostedService<ProxyConfigLoader>();
            }, app =>
            {
                // Mimic the IIS issue https://github.com/microsoft/reverse-proxy/issues/255
                app.Use((context, next) =>
                {
                    if (forceUpgradable && !(context.Features.Get<IHttpUpgradeFeature>()?.IsUpgradableRequest == true))
                    {
                        context.Features.Set<IHttpUpgradeFeature>(new AlwaysUpgradeFeature());
                    }
                    return next();
                });
                app.UseRouting();
                app.UseEndpoints(builder =>
                {
                    builder.MapReverseProxy();
                });
            });
        }

        private IHost CreateDestinationHost()
        {
            return CreateHost(services =>
            {
                services.AddRouting();
            }, app =>
            {
                app.UseWebSockets();
                app.UseRouting();
                app.UseEndpoints(builder =>
                {
                    builder.Map("/websockets", WebSocket);
                    builder.Map("/rawupgrade", RawUpgrade);
                    builder.Map("/post", Post);
                });
            });

            static async Task WebSocket(HttpContext httpContext)
            {
                if (!httpContext.WebSockets.IsWebSocketRequest)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                }

                using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();

                var buffer = new byte[1024];
                while (true)
                {
                    var message = await webSocket.ReceiveAsync(buffer, httpContext.RequestAborted);
                    if (message.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, message.CloseStatusDescription, httpContext.RequestAborted);
                        return;
                    }

                    await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, message.Count),
                        message.MessageType,
                        message.EndOfMessage,
                        httpContext.RequestAborted);
                }
            }

            static async Task RawUpgrade(HttpContext httpContext)
            {
                var upgradeFeature = httpContext.Features.Get<IHttpUpgradeFeature>();
                if (upgradeFeature == null || !upgradeFeature.IsUpgradableRequest)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
                    return;
                }

                await using var stream = await upgradeFeature.UpgradeAsync();
                var buffer = new byte[1];
                int read;
                while ((read = await stream.ReadAsync(buffer, httpContext.RequestAborted)) != 0)
                {
                    await stream.WriteAsync(buffer, 0, read, httpContext.RequestAborted);
                }
            }

            static async Task Post(HttpContext httpContext)
            {
                var body = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                await httpContext.Response.WriteAsync(body);
            }
        }

        private IHost CreateHost(Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configureApp)
        {
            return new HostBuilder()
               .ConfigureWebHost(webHostBuilder =>
               {
                   webHostBuilder
                   .ConfigureServices(configureServices)
                   .UseKestrel()
                   .UseUrls(TestUrlHelper.GetTestUrl())
                   .Configure(configureApp);
               }).Build();
        }

        private class AlwaysUpgradeFeature : IHttpUpgradeFeature
        {
            public bool IsUpgradableRequest => true;

            public Task<Stream> UpgradeAsync()
            {
                throw new InvalidOperationException("This wasn't supposed to get called.");
            }
        }
    }
}
