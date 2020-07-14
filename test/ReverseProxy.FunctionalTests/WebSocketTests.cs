using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Xunit;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Configuration;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy
{
    public class WebSocketTests
    {
        [Theory]
        [InlineData(WebSocketMessageType.Binary)]
        [InlineData(WebSocketMessageType.Text)]
        public async Task WebSocketEchoTest(WebSocketMessageType messageType)
        {
            var destinationHost = CreateWebSocketHost();
            await destinationHost.StartAsync();
            var destinationHostUrl = Helpers.GetAddress(destinationHost);

            var proxyHost = CreateReverseProxyHost(destinationHostUrl);
            await proxyHost.StartAsync();
            var proxyHostUrl = Helpers.GetAddress(proxyHost);

            var client = new ClientWebSocket();
            var webSocketsTarget = proxyHostUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            var targetUri = new Uri(new Uri(webSocketsTarget, UriKind.Absolute), "/");
            await client.ConnectAsync(targetUri, CancellationToken.None);

            var buffer = new byte[1024];
            var textToSend = $"Hello World!";
            var numBytes = Encoding.UTF8.GetBytes(textToSend, buffer.AsSpan());
            await client.SendAsync(new ArraySegment<byte>(buffer, 0, numBytes),
                messageType,
                endOfMessage: true,
                CancellationToken.None);

            var message = await client.ReceiveAsync(buffer, CancellationToken.None);

            var text = Encoding.UTF8.GetString(buffer.AsSpan(0, message.Count));
            Assert.Equal(textToSend, text);
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
                    proxyRoute.Match.Path = "/";

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
                    builder.Map("/", RunPingPongAsync);
                });
            });

            static async Task RunPingPongAsync(HttpContext httpcontext)
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
        }

        public IHost CreateHost(Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configureApp)
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
