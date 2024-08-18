// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Yarp.ReverseProxy.Forwarder.Tests;

public class FastCgiHttpMessageHandlerTests
{
    private readonly ITestOutputHelper _output;

    public FastCgiHttpMessageHandlerTests(ITestOutputHelper output)
    {
        _output = output;
    }


    [Fact]
    public async Task CanDoBasicGet()
    {
        using var client = new HttpMessageInvoker(new FastCgiHttpMessageHandler(Options.Create(new SocketConnectionFactoryOptions()), NullLogger.Instance));

        var req = new HttpRequestMessage()
        {
            RequestUri = new("http://127.0.0.1:9000/index.php"),
            Method = HttpMethod.Get,
        };
        req.Headers.Host = "example.com";

        req.Options.Set(FastCgiHttpMessageHandler.FastCgiHttpOptions.DOCUMENT_ROOT, "/var/www/html");

        var result = await client.SendAsync(req, cancellationToken: default);

        using (var stream = result.Content.ReadAsStream())
        {
            var reader = new StreamReader(result.Content.ReadAsStream());
            var content = reader.ReadToEnd();
            Assert.True(content.Length > 0);
        }

        Assert.NotNull(result.Headers);

    }


    [Fact]
    public async Task CanDoBasicPost()
    {
        using var client = new HttpMessageInvoker(new FastCgiHttpMessageHandler(Options.Create(new SocketConnectionFactoryOptions()), NullLogger.Instance));

        var req = new HttpRequestMessage()
        {
            RequestUri = new("http://127.0.0.1:9000/index.php"),
            Method = HttpMethod.Post,
        };
        req.Headers.Host = "example.com";
        req.Content = new StringContent(""" { "test": 1} """);

        req.Options.Set(FastCgiHttpMessageHandler.FastCgiHttpOptions.DOCUMENT_ROOT, "/var/www/html");

        var result = await client.SendAsync(req, cancellationToken: default);

        using (var stream = result.Content.ReadAsStream())
        {
            var reader = new StreamReader(result.Content.ReadAsStream());
            var content = reader.ReadToEnd();
            Assert.True(content.Length > 0);
        }

        Assert.NotNull(result.Headers);
    }
}
