using System;
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
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Abstractions;
using Xunit;

namespace Microsoft.ReverseProxy
{
    public abstract class HttpProxyCookieTests
    {
        public const string CookieAKey = "testA";
        public const string CookieAValue = "A_Cookie";
        public const string CookieBKey = "testB";
        public const string CookieBValue = "B_Cookie";

        public static readonly string CookieA = $"{CookieAKey}={CookieAValue}";
        public static readonly string CookieB = $"{CookieBKey}={CookieBValue}";
        public static readonly string Cookies = $"{CookieA}; {CookieB}";

        public abstract HttpProtocols HttpProtocol { get; }
        public abstract Task ProcessHttpRequest(Uri proxyHostUri);

        [Fact]
        public async Task ProxyAsync_RequestWithCookieHeaders()
        {
            var tcs = new TaskCompletionSource<StringValues>();

            using var destinationHost = CreateDestinationHost(
                context =>
                {
                    if (context.Request.Headers.TryGetValue(HeaderNames.Cookie, out var cookieHeaders))
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

            using var proxyHost = CreateReverseProxyHost(HttpProtocol, destinationHostUrl);
            await proxyHost.StartAsync();
            var proxyHostUri = new Uri(proxyHost.GetAddress());

            await ProcessHttpRequest(proxyHostUri);

            Assert.True(tcs.Task.IsCompleted);
            var cookieHeaders = await tcs.Task;
            var cookies = Assert.Single(cookieHeaders);
            Assert.Equal(Cookies, cookies);

            await destinationHost.StopAsync();
            await proxyHost.StopAsync();
        }

        private IHost CreateReverseProxyHost(HttpProtocols httpProtocol, string destinationHostUrl)
        {
            return CreateHost(httpProtocol,
                services =>
                {
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
                    app.UseMiddleware<CheckCookieHeaderMiddleware>();
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

        private class CheckCookieHeaderMiddleware
        {
            private readonly RequestDelegate _next;

            public CheckCookieHeaderMiddleware(RequestDelegate next)
            {
                _next = next;
            }

            public async Task Invoke(HttpContext context)
            {
                // Ensure that CookieA is the first and CookieB the last.
                Assert.True(context.Request.Headers.TryGetValue(HeaderNames.Cookie, out var headerValues));
                Assert.StartsWith(CookieA, headerValues);
                Assert.EndsWith(CookieB, headerValues);

                await _next.Invoke(context);
            }
        }
    }
    public class HttpProxyCookieTests_Http1 : HttpProxyCookieTests
    {
        public override HttpProtocols HttpProtocol => HttpProtocols.Http1;

        // Using simple TcpClient since HttpClient will always concatenate cookies with comma separator.
        public override async Task ProcessHttpRequest(Uri proxyHostUri)
        {
            using var client = new TcpClient(proxyHostUri.Host, proxyHostUri.Port);
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, Encoding.ASCII);
            using var reader = new StreamReader(stream, Encoding.ASCII);

            await writer.WriteAsync($"GET / HTTP/1.1\r\n");
            await writer.WriteAsync($"Host: {proxyHostUri.Authority}\r\n");
            await writer.WriteAsync($"Cookie: {Cookies}\r\n");
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
    }

#if NET5_0
    public class HttpProxyCookieTests_Http2 : HttpProxyCookieTests
    {
        public override HttpProtocols HttpProtocol => HttpProtocols.Http2;

        // HttpClient for H/2 will use different header frames for cookies from a container and message headers.
        // It will first send message header cookie and than the container one and we expect them in the order of cookieA;cookieB.
        public override async Task ProcessHttpRequest(Uri proxyHostUri)
        {
            var handler = new HttpClientHandler();
            handler.CookieContainer.Add(new System.Net.Cookie(CookieBKey, CookieBValue, path: "/", domain: proxyHostUri.Host));
            var client = new HttpClient(handler);
            var message = new HttpRequestMessage(HttpMethod.Get, proxyHostUri);
            message.Version = HttpVersion.Version20;
            message.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            message.Headers.Add(HeaderNames.Cookie, $"{CookieA}");
            using var response = await client.SendAsync(message);
            response.EnsureSuccessStatusCode();
        }
    }
#elif NETCOREAPP3_1
// Do not test HTTP/2 on .NET Core 3.1
#else
#error A target framework was added to the project and needs to be added to this condition. 
#endif
}