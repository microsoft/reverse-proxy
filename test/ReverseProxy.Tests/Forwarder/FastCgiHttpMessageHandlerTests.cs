// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Yarp.ReverseProxy.Forwarder.Tests;

public class FastCgiHttpMessageHandlerTests
{
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

        req.Options.Set(FastCgiHttpMessageHandler.FastCgiHttpOptions.SCRIPT_FILENAME, "/var/www/html/index.php");

        var result = await client.SendAsync(req, cancellationToken: default);

        await using (var stream = await result.Content.ReadAsStreamAsync())
        {
            var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
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

        req.Options.Set(FastCgiHttpMessageHandler.FastCgiHttpOptions.SCRIPT_FILENAME, "/var/www/html/index.php");

        var result = await client.SendAsync(req, cancellationToken: default);

        await using (var stream = await result.Content.ReadAsStreamAsync())
        {
            var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            Assert.True(content.Length > 0);
        }

        Assert.NotNull(result.Headers);
    }
}
