using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions;
using Xunit;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy
{
    public class HttpProxyTests
    {
        public static IEnumerable<object[]> RequestWithCookieHeadersTestData()
        {
            yield return new object[] { HttpProtocols.Http1, (Func<Uri, (string Key, string Value)[], Task>)ProcessHttp11Request };
#if NET5_0
            yield return new object[] { HttpProtocols.Http2, (Func<Uri, (string Key, string Value)[], Task>)ProcessHttp20Request };
#endif

            // Using simple TcpClient since HttpClient will always merge cookies into a single header.
            static async Task ProcessHttp11Request(Uri proxyUri, (string Key, string Value)[] cookies)
            {
                using var client = new TcpClient(proxyUri.Host, proxyUri.Port);
                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream, Encoding.ASCII);
                using var reader = new StreamReader(stream, Encoding.ASCII);

                await writer.WriteAsync($"GET / HTTP/1.1\r\n");
                await writer.WriteAsync($"Host: {proxyUri.Authority}\r\n");
                await writer.WriteAsync($"Cookie: {cookies[0].Key}={cookies[0].Value}\r\n");
                await writer.WriteAsync($"Cookie: {cookies[1].Key}={cookies[1].Value}\r\n");
                await writer.WriteAsync($"Connection: close\r\n");
                await writer.WriteAsync($"\r\n");
                await writer.FlushAsync();

                string line = null;
                string statusLine = null;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    statusLine ??= line;
                }

                Assert.Equal($"HTTP/1.1 200 OK", statusLine);
            }

#if NET5_0
            // HttpClient for H/2 will use different header frames for cookies from a container and from message headers.
            static async Task ProcessHttp20Request(Uri proxyUri, (string Key, string Value)[] cookies)
            {
                // It will first send message header cookie and than the container one thus swap the cookie order to get the order we expect.
                var handler = new HttpClientHandler();
                handler.CookieContainer.Add(new System.Net.Cookie(cookies[1].Key, cookies[1].Value, path: "/", domain: proxyUri.Host));
                var client = new HttpClient(handler);
                var message = new HttpRequestMessage(HttpMethod.Get, proxyUri);
                message.Version = HttpVersion.Version20;
                message.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

                message.Headers.Add("Cookie", $"{cookies[0].Key}={cookies[0].Value}");
                using var response = await client.SendAsync(message);
                response.EnsureSuccessStatusCode();
            }
#endif
        }

        [Theory]
        [MemberData(nameof(RequestWithCookieHeadersTestData))]
        public async Task ProxyAsync_RequestWithCookieHeaders(HttpProtocols httpProtocol, Func<Uri, (string Key, string Value)[], Task> processRequest)
        {
            var cookies = new (string Key, string Value)[] { ("testA", "A_Cookie"), ("testB", "B_Cookie") };

            var tcs = new TaskCompletionSource<StringValues>();

            using var destinationHost = CreateDestinationHost(
                context =>
                {
                    if (context.Request.Headers.TryGetValue("Cookie", out var cookieHeaders))
                    {
                        tcs.SetResult(cookieHeaders);
                    }
                    else
                    {
                        tcs.SetException(new Exception("Missing 'Cookie' header in request"));
                    }
                    return Task.CompletedTask;
                });

            await destinationHost.StartAsync();
            var destinationHostUrl = destinationHost.GetAddress();

            using var proxyHost = CreateReverseProxyHost(httpProtocol, destinationHostUrl);
            await proxyHost.StartAsync();
            var proxyHostUri = new Uri(proxyHost.GetAddress());

            await processRequest(proxyHostUri, cookies);

            Assert.True(tcs.Task.IsCompleted);
            var cookieHeaders = await tcs.Task;
            var cookie = Assert.Single(cookieHeaders);
            Assert.Equal(String.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}")), cookie);

            await destinationHost.StopAsync();
            await proxyHost.StopAsync();
        }

        private IHost CreateReverseProxyHost(HttpProtocols httpProtocols, string destinationHostUrl)
        {
            return CreateHost(httpProtocols,
                services =>
                {
                    services.AddRouting();

                    var proxyRoute = new ProxyRoute
                    {
                        RouteId = "route1",
                        ClusterId = "cluster1",
                        Match = { Path = "/{**catchall}" }
                    };

                    var cluster = new Cluster
                    {
                        Id = "cluster1",
                        Destinations =
                        {
                            { "cluster1",  new Destination() { Address = destinationHostUrl } }
                        }
                    };

                    services.AddReverseProxy().LoadFromMemory(new[] { proxyRoute }, new[] { cluster });
                },
                app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(builder =>
                    {
                        builder.MapReverseProxy();
                    });
                });
        }

        private IHost CreateDestinationHost(RequestDelegate getDelegate)
        {
            return CreateHost(HttpProtocols.Http1AndHttp2,
                services =>
                {
                    services.AddRouting();
                },
                app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapGet("/", getDelegate));
                });
        }

        private IHost CreateHost(HttpProtocols httpProtocols, Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configureApp)
        {
            return new HostBuilder()
               .ConfigureWebHost(webHostBuilder =>
               {
                   webHostBuilder
                   .ConfigureServices(configureServices)
                   .UseKestrel(kestrel => 
                   {
                       kestrel.Listen(IPAddress.Loopback, 0, listenOptions =>
                       {
                           listenOptions.Protocols = httpProtocols;
                       });
                   })
                   .Configure(configureApp);
               }).Build();
        }
    }
}