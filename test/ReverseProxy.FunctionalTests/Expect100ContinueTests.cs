// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Xunit;
using Yarp.ReverseProxy.Common;
using Yarp.ReverseProxy.Forwarder;

namespace Yarp.ReverseProxy
{
    public class Expect100ContinueTests
    {
        [Theory]
        [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, true, false, 200)]
        [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, false, false, 200)]
        [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, true, true, 200)]
        [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, false, true, 200)]
        [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, true, false, 200)]
        [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, false, false, 200)]
        [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, true, true, 200)]
        [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, false, true, 200)]
        [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, true, false, 400)]
        [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, false, false, 400)]
        [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, true, true, 400)]
        [InlineData(HttpProtocols.Http1, HttpProtocols.Http1, false, true, 400)]
        [InlineData(HttpProtocols.Http1, HttpProtocols.Http2, true, false, 400)]
        [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, true, false, 400)]
        [InlineData(HttpProtocols.Http1, HttpProtocols.Http2, true, true, 400)]
        [InlineData(HttpProtocols.Http2, HttpProtocols.Http2, true, true, 400)]
        public async Task PostExpect100_BodyAlwaysUploaded(HttpProtocols proxyProtocol, HttpProtocols destProtocol, bool useContentLength, bool hasResponseBody, int destResponseCode)
        {
            var headerTcs = new TaskCompletionSource<StringValues>(TaskCreationOptions.RunContinuationsAsynchronously);
            var bodyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            var contentString = new string('a', 1024 * 1024 * 10);
            var test = new TestEnvironment(
                async context =>
                {
                    if ((context.Request.Protocol == "HTTP/1.1" && destProtocol != HttpProtocols.Http1)
                        || (context.Request.Protocol == "HTTP/2.0" && destProtocol != HttpProtocols.Http2))
                    {
                        headerTcs.SetException(new Exception($"Unexpected request protocol {context.Request.Protocol}"));
                    }
                    else if (context.Request.Headers.TryGetValue(HeaderNames.Expect, out var expectHeader))
                    {
                        headerTcs.SetResult(expectHeader);
                    }
                    else
                    {
                        headerTcs.SetException(new Exception("Missing 'Expect' header in request"));
                    }

                    if (destResponseCode == 200)
                    {
                        // 100 response code is sent automatically on reading Body.
                        await ReadContent(context, bodyTcs, Encoding.UTF8.GetByteCount(contentString));
                    }

                    context.Response.StatusCode = destResponseCode;

                    if (hasResponseBody)
                    {
                        var responseBody = Encoding.UTF8.GetBytes(contentString + "Response");
                        if (useContentLength)
                        {
                            context.Response.Headers.ContentLength = responseBody.Length;
                        }
                        else
                        {
                            context.Response.Headers[HeaderNames.TransferEncoding] = "chuncked";
                        }

                        await context.Response.Body.WriteAsync(responseBody.AsMemory());
                    }
                },
                proxyBuilder => {
                    proxyBuilder.Services.RemoveAll(typeof(IForwarderHttpClientFactory));
                    proxyBuilder.Services.TryAddSingleton<IForwarderHttpClientFactory, TestForwarderHttpClientFactory>();
                },
                proxyApp => { },
                proxyProtocol: proxyProtocol,
                useHttpsOnDestination: true,
                useHttpsOnProxy: true,
                configTransformer: (c, r) =>
                {
                    c = c with
                    {
                        HttpRequest = new ForwarderRequestConfig
                        {
                            Version = destProtocol == HttpProtocols.Http2 ? HttpVersion.Version20 : HttpVersion.Version11,
                        }
                    };
                    return (c, r);
                });

            await test.Invoke(async uri =>
            {
                await ProcessHttpRequest(new Uri(uri), proxyProtocol, contentString, useContentLength, hasResponseBody, destResponseCode);

                Assert.True(headerTcs.Task.IsCompleted);
                var expectHeader = await headerTcs.Task;
                var expectValue = Assert.Single(expectHeader);
                Assert.Equal("100-continue", expectValue);

                if (destResponseCode == 200)
                {
                    Assert.True(bodyTcs.Task.IsCompleted);
                    var actualString = await bodyTcs.Task;
                    Assert.Equal(contentString, actualString);
                }
                else
                {
                    Assert.False(bodyTcs.Task.IsCompleted);
                }
            });
        }

        private static async Task ReadContent(Microsoft.AspNetCore.Http.HttpContext context, TaskCompletionSource<string> bodyTcs, int byteCount)
        {
            try
            {
                var buffer = new byte[byteCount];
                var readCount = 0;
                var totalReadCount = 0;
                do
                {
                    readCount = await context.Request.Body.ReadAsync(buffer, totalReadCount, buffer.Length - totalReadCount);
                    totalReadCount += readCount;
                } while (readCount != 0);

                var actualString = Encoding.UTF8.GetString(buffer);
                bodyTcs.SetResult(actualString);
            }
            catch (Exception e)
            {
                bodyTcs.SetException(e);
            }
        }

        private async Task ProcessHttpRequest(Uri proxyHostUri, HttpProtocols protocol, string contentString, bool useContentLength, bool hasResponseBody, int expectedCode)
        {
            using var handler = new SocketsHttpHandler() { Expect100ContinueTimeout = TimeSpan.FromSeconds(60) };
            handler.UseProxy = false;
            handler.AllowAutoRedirect = false;
            handler.SslOptions.RemoteCertificateValidationCallback = delegate { return true; };
            using var client = new HttpClient(handler);
            using var message = new HttpRequestMessage(HttpMethod.Post, proxyHostUri);
            message.Version = protocol == HttpProtocols.Http2 ? HttpVersion.Version20 : HttpVersion.Version11;
            message.Headers.ExpectContinue = true;

            var content = Encoding.UTF8.GetBytes(contentString);
            using var contentStream = new MemoryStream(content);
            message.Content = new StreamContent(contentStream);
            if (useContentLength)
            {
                message.Content.Headers.ContentLength = content.Length;
            }
            else
            {
                message.Headers.TransferEncodingChunked = true;
            }

            using var response = await client.SendAsync(message);

            Assert.Equal((int)response.StatusCode, expectedCode);
            if (expectedCode == 200)
            {
                Assert.Equal(content.Length, contentStream.Position);
                if (hasResponseBody)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    Assert.Equal(contentString + "Response", responseBody);
                }
            }
            else
            {
                Assert.Equal(0, contentStream.Position);
                if (hasResponseBody)
                {
                    await Assert.ThrowsAsync<HttpRequestException>(() => response.Content.ReadAsStringAsync());
                }
            }
        }

        private class TestForwarderHttpClientFactory : ForwarderHttpClientFactory
        {
            protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
            {
                base.ConfigureHandler(context, handler);
                handler.Expect100ContinueTimeout = TimeSpan.FromSeconds(60);
            }
        }
    }
}
