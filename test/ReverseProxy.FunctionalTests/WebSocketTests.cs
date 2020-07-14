using System;
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
            var destinationHost = CreateWebSocketHost();
            await destinationHost.StartAsync();
            var destinationHostUrl = Helpers.GetAddress(destinationHost);

            var proxyHost = CreateReverseProxyHost(destinationHostUrl);
            await proxyHost.StartAsync();
            var proxyHostUrl = Helpers.GetAddress(proxyHost);

            var client = new ClientWebSocket();
            var webSocketsTarget = proxyHostUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            var targetUri = new Uri(new Uri(webSocketsTarget, UriKind.Absolute), "/websockets");
            await client.ConnectAsync(targetUri, CancellationToken.None);

            var buffer = new byte[1024];
            var textToSend = $"Hello World!";
            var numBytes = Encoding.UTF8.GetBytes(textToSend, buffer.AsSpan());
            await client.SendAsync(new ArraySegment<byte>(buffer, 0, numBytes),
                messageType,
                endOfMessage: true,
                CancellationToken.None);

            var message = await client.ReceiveAsync(buffer, CancellationToken.None);

            Assert.Equal(messageType, message.MessageType);
            Assert.True(message.EndOfMessage);

            var text = Encoding.UTF8.GetString(buffer.AsSpan(0, message.Count));
            Assert.Equal(textToSend, text);
        }

        [Fact]
        public async Task RawUpgradeTest()
        {
            var destinationHost = CreateWebSocketHost();
            await destinationHost.StartAsync();
            var destinationHostUrl = Helpers.GetAddress(destinationHost);

            var proxyHost = CreateReverseProxyHost(destinationHostUrl);
            await proxyHost.StartAsync();
            var proxyHostUrl = Helpers.GetAddress(proxyHost);

            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                UseProxy = false
            };
            using var client = new HttpMessageInvoker(handler);
            var webSocketsTarget = proxyHostUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            var targetUri = new Uri(new Uri(webSocketsTarget, UriKind.Absolute), "rawupgrade");
            var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
            request.Headers.TryAddWithoutValidation("Connection", "upgrade");
            request.Version = new Version(1, 1);

            var response = await client.SendAsync(request, CancellationToken.None);

            Assert.Equal(HttpStatusCode.SwitchingProtocols, response.StatusCode);

            var rawStream = await response.Content.ReadAsStreamAsync();

            var buffer = new byte[1];
            for (var i = 0; i <= 255; i++)
            {
                buffer[0] = (byte)i;
                await rawStream.WriteAsync(buffer, 0, 1, CancellationToken.None);
                var read = await rawStream.ReadAsync(buffer, CancellationToken.None);
                if (i == 255)
                {
                    Assert.Equal(1, read);
                }
                else
                {
                    Assert.Equal(1, read);
                    Assert.Equal(i, buffer[0]);
                }
            }
        }

        private IHost CreateReverseProxyHost(string destinationHostUrl)
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
                app.UseRouting();
                app.Use((context, next) =>
                {
                    var endpoint = context.GetEndpoint();

                    return next();
                });
                app.UseEndpoints(builder =>
                {
                    builder.MapReverseProxy();
                });
            });
        }

        private IHost CreateWebSocketHost()
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
                });
            });

            static async Task WebSocket(HttpContext httpcontext)
            {
                if (!httpcontext.WebSockets.IsWebSocketRequest)
                {
                    httpcontext.Response.StatusCode = StatusCodes.Status400BadRequest;
                }

                using (var webSocket = await httpcontext.WebSockets.AcceptWebSocketAsync())
                {
                    var buffer = new byte[1024];
                    while (true)
                    {
                        var message = await webSocket.ReceiveAsync(buffer, httpcontext.RequestAborted);
                        if (message.MessageType == WebSocketMessageType.Close)
                        {
                            await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Bye", httpcontext.RequestAborted);
                            return;
                        }

                        await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, message.Count),
                            message.MessageType,
                            message.EndOfMessage,
                            httpcontext.RequestAborted);
                    }
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
                    await stream.WriteAsync(buffer, 0, read);

                    if (buffer[0] == 255)
                    {
                        // Goodbye
                        break;
                    }
                }
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
                   .UseUrls(TestUrlHelper.GetTestUrl(ServerType.Kestrel))
                   .Configure(configureApp);
               }).Build();
        }
    }
}
