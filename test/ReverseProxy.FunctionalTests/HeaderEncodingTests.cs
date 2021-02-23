using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Microsoft.ReverseProxy.Common;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Service.Proxy.Infrastructure;
using Xunit;

namespace Microsoft.ReverseProxy
{
#if NET
    public class HeaderEncodingTests
    {
        [Theory]
        [InlineData("http://www.ěščřžýáíé.com", "utf-8")]
        [InlineData("http://www.çáéôîèñøæ.com", "iso-8859-1")]
        public async Task ProxyAsync_RequestWithEncodedHeaderValue(string headerValue, string encodingName)
        {
            var encoding = Encoding.GetEncoding(encodingName);
            var tcs = new TaskCompletionSource<StringValues>(TaskCreationOptions.RunContinuationsAsynchronously);

            IProxyErrorFeature proxyError = null;
            Exception unhandledError = null;

            var test = new TestEnvironment(
                context =>
                {
                    if (context.Request.Headers.TryGetValue(HeaderNames.Referer, out var header))
                    {
                        tcs.SetResult(header);
                    }
                    else
                    {
                        tcs.SetException(new Exception($"Missing '{HeaderNames.Referer}' header in request"));
                    }
                    return Task.CompletedTask;
                },
                proxyBuilder =>
                {
                    proxyBuilder.Services.AddSingleton<IProxyHttpClientFactory>(new HeaderEncodingClientFactory(encoding));
                },
                proxyApp =>
                {
                    proxyApp.Use(async (context, next) =>
                    {
                        try
                        {
                            Assert.True(context.Request.Headers.TryGetValue(HeaderNames.Referer, out var header));
                            var value = Assert.Single(header);
                            Assert.Equal(headerValue, value);

                            await next();
                            proxyError = context.Features.Get<IProxyErrorFeature>();
                        }
                        catch (Exception ex)
                        {
                            unhandledError = ex;
                            throw;
                        }
                    });
                },
                proxyProtocol: HttpProtocols.Http1, headerEncoding: encoding);

            await test.Invoke(async proxyUri =>
            {
                var proxyHostUri = new Uri(proxyUri);

                using var tcpClient = new TcpClient(proxyHostUri.Host, proxyHostUri.Port);
                await using var stream = tcpClient.GetStream();
                await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n"));
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"Host: {proxyHostUri.Host}:{proxyHostUri.Port}\r\n"));
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: 0\r\n"));
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"Connection: close\r\n"));
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"Referer: "));
                await stream.WriteAsync(encoding.GetBytes(headerValue));
                await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
                await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
                var buffer = new byte[4096];
                var responseBuilder = new StringBuilder();
                while (true)
                {
                    var count = await stream.ReadAsync(buffer);
                    if (count == 0)
                    {
                        break;
                    }
                    responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, count));
                }
                var response = responseBuilder.ToString();

                Assert.Null(proxyError);
                Assert.Null(unhandledError);

                Assert.StartsWith("HTTP/1.1 200 OK", response);

                Assert.True(tcs.Task.IsCompleted);
                var refererHeader = await tcs.Task;
                var referer = Assert.Single(refererHeader);
                Assert.Equal(headerValue, referer);
            });
        }

        [Theory]
        [InlineData("http://www.ěščřžýáíé.com", "utf-8")]
        [InlineData("http://www.çáéôîèñøæ.com", "iso-8859-1")]
        public async Task ProxyAsync_ResponseWithEncodedHeaderValue(string headerValue, string encodingName)
        {
            var encoding = Encoding.GetEncoding(encodingName);

            var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var destinationTask = Task.Run(async () =>
            {
                using var tcpClient = await tcpListener.AcceptTcpClientAsync();
                await using var stream = tcpClient.GetStream();
                var buffer = new byte[4096];
                var requestBuilder = new StringBuilder();
                while (true)
                {
                    var count = await stream.ReadAsync(buffer);
                    if (count == 0)
                    {
                        break;
                    }

                    requestBuilder.Append(encoding.GetString(buffer, 0, count));

                    // End of the request
                    if (requestBuilder.Length >= 4 &&
                        requestBuilder[^4] == '\r' && requestBuilder[^3] == '\n' &&
                        requestBuilder[^2] == '\r' && requestBuilder[^1] == '\n')
                    {
                        break;
                    }
                }

                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\n"));
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: 0\r\n"));
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"Connection: close\r\n"));
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"Location: "));
                await stream.WriteAsync(encoding.GetBytes(headerValue));
                await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
                await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
            });

            IProxyErrorFeature proxyError = null;
            Exception unhandledError = null;

            using var proxy = TestEnvironment.CreateProxy(HttpProtocols.Http1, false, encoding, "cluster1", $"http://{tcpListener.LocalEndpoint}",
                proxyBuilder =>
                {
                    proxyBuilder.Services.AddSingleton<IProxyHttpClientFactory>(new HeaderEncodingClientFactory(encoding));
                },
                proxyApp =>
                {
                    proxyApp.Use(async (context, next) =>
                    {
                        try
                        {
                            await next();
                            proxyError = context.Features.Get<IProxyErrorFeature>();
                        }
                        catch (Exception ex)
                        {
                            unhandledError = ex;
                            throw;
                        }
                    });
                });

            await proxy.StartAsync();

            try
            {
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(proxy.GetAddress());

                Assert.Null(proxyError);
                Assert.Null(unhandledError);

                response.EnsureSuccessStatusCode();

                Assert.True(response.Headers.TryGetValues(HeaderNames.Location, out var refererHeader));
                var referer = Assert.Single(refererHeader);
                Assert.Equal(headerValue, referer);

                Assert.True(destinationTask.IsCompleted);
                await destinationTask;
            }
            finally
            {
                await proxy.StopAsync();
                tcpListener.Stop();
            }
        }

        private class HeaderEncodingClientFactory : IProxyHttpClientFactory
        {
            private Encoding _encoding;

            public HeaderEncodingClientFactory(Encoding encoding)
            {
                _encoding = encoding;
            }

            // Partial kopypasta of ProxyHttpClientFactory
            public HttpMessageInvoker CreateClient(ProxyHttpClientContext context)
            {
                if (CanReuseOldClient(context))
                {
                    return context.OldClient;
                }

                var newClientOptions = context.NewOptions;
                var handler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.None,
                    UseCookies = false,
                    RequestHeaderEncodingSelector = (_, _) => _encoding,
                    ResponseHeaderEncodingSelector = (_, _) => _encoding

                    // NOTE: MaxResponseHeadersLength = 64, which means up to 64 KB of headers are allowed by default as of .NET Core 3.1.
                };

                if (newClientOptions.MaxConnectionsPerServer != null)
                {
                    handler.MaxConnectionsPerServer = newClientOptions.MaxConnectionsPerServer.Value;
                }
                if (newClientOptions.DangerousAcceptAnyServerCertificate ?? false)
                {
                    handler.SslOptions.RemoteCertificateValidationCallback = delegate { return true; };
                }

                return new HttpMessageInvoker(handler, disposeHandler: true);
            }

            private bool CanReuseOldClient(ProxyHttpClientContext context)
            {
                return context.OldClient != null && context.NewOptions == context.OldOptions;
            }
        }
    }
#endif
}
