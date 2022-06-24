// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Common;

namespace Yarp.ReverseProxy;

public class WebSocketTests
{
    [Theory]
    [InlineData(WebSocketMessageType.Binary)]
    [InlineData(WebSocketMessageType.Text)]
    public async Task WebSocketTest(WebSocketMessageType messageType)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var test = CreateTestEnvironment();

        await test.Invoke(async uri =>
        {
            using var client = new ClientWebSocket();
            var webSocketsTarget = uri.Replace("https://", "wss://").Replace("http://", "ws://");
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
        }, cts.Token);
    }

    [Fact]
    public async Task RawUpgradeTest()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var test = CreateTestEnvironment();

        await test.Invoke(async uri =>
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                UseProxy = false
            };
            using var client = new HttpMessageInvoker(handler);
            var targetUri = new Uri(new Uri(uri, UriKind.Absolute), "rawupgrade");
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);

            // TODO: https://github.com/microsoft/reverse-proxy/issues/255 Until this is fixed the "Upgrade: WebSocket" header is required.
            request.Headers.TryAddWithoutValidation("Upgrade", "WebSocket");

            request.Headers.TryAddWithoutValidation("Connection", "upgrade");
            request.Version = new Version(1, 1);

            var response = await client.SendAsync(request, cts.Token);

            Assert.Equal(HttpStatusCode.SwitchingProtocols, response.StatusCode);

#if NET
            using var rawStream = await response.Content.ReadAsStreamAsync(cts.Token);
#elif NETCOREAPP3_1
            using var rawStream = await response.Content.ReadAsStreamAsync();
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif

            var buffer = new byte[5];
            for (var i = 0; i <= 255; i++)
            {
                buffer[0] = (byte)i;
                await rawStream.WriteAsync(buffer, 0, buffer.Length, cts.Token);
                var read = await rawStream.ReadAsync(buffer, cts.Token);

                Assert.Equal(buffer.Length, read);
                Assert.Equal(i, buffer[0]);
            }

            await rawStream.WriteAsync(Encoding.UTF8.GetBytes("close"));
            while (await rawStream.ReadAsync(buffer, cts.Token) != 0) { }
            rawStream.Dispose();
        }, cts.Token);
    }

    [Fact]
    // https://github.com/microsoft/reverse-proxy/issues/255 IIS claims all requests are upgradeable.
    public async Task FalseUpgradeTest()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var test = CreateTestEnvironment(forceUpgradable: true);

        await test.Invoke(async uri =>
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                UseProxy = false
            };
            using var client = new HttpMessageInvoker(handler);
            var targetUri = new Uri(new Uri(uri, UriKind.Absolute), "post");
            using var request = new HttpRequestMessage(HttpMethod.Post, targetUri);
            request.Content = new StringContent("Hello World");
            request.Version = new Version(1, 1);

            var response = await client.SendAsync(request, cts.Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
#if NET
            Assert.Equal("Hello World", await response.Content.ReadAsStringAsync(cts.Token));
#elif NETCOREAPP3_1
            Assert.Equal("Hello World", await response.Content.ReadAsStringAsync());
#else
#error A target framework was added to the project and needs to be added to this condition.
#endif
        }, cts.Token);
    }

    private static TestEnvironment CreateTestEnvironment(bool forceUpgradable = false)
    {
        return new TestEnvironment(
            destinationServies =>
            {
                destinationServies.AddRouting();
            },
            destinationApp =>
            {
                destinationApp.UseWebSockets();
                destinationApp.UseRouting();
                destinationApp.UseEndpoints(builder =>
                {
                    builder.Map("/websockets", WebSocket);
                    builder.Map("/rawupgrade", RawUpgrade);
                    builder.Map("/post", Post);
                });
            },
            proxyServices => { },
            proxyBuilder => { },
            proxyApp =>
            {
                // Mimic the IIS issue https://github.com/microsoft/reverse-proxy/issues/255
                proxyApp.Use((context, next) =>
                {
                    if (forceUpgradable && !(context.Features.Get<IHttpUpgradeFeature>()?.IsUpgradableRequest == true))
                    {
                        context.Features.Set<IHttpUpgradeFeature>(new AlwaysUpgradeFeature());
                    }
                    return next();
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
            if (upgradeFeature is null || !upgradeFeature.IsUpgradableRequest)
            {
                httpContext.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
                return;
            }

            await using var stream = await upgradeFeature.UpgradeAsync();
            var buffer = new byte[5];
            int read;
            while ((read = await stream.ReadAsync(buffer, httpContext.RequestAborted)) != 0)
            {
                await stream.WriteAsync(buffer, 0, read, httpContext.RequestAborted);

                if (string.Equals("close", Encoding.UTF8.GetString(buffer, 0, read), StringComparison.Ordinal))
                {
                    break;
                }
            }
        }

        static async Task Post(HttpContext httpContext)
        {
            var body = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
            await httpContext.Response.WriteAsync(body);
        }
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
