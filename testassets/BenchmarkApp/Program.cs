// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Crank.EventSources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;

BenchmarksEventSource.MeasureAspNetVersion();
BenchmarksEventSource.MeasureNetCoreAppVersion();

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables(prefix: "ASPNETCORE_")
    .AddCommandLine(args)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var builder = new WebHostBuilder()
    .ConfigureLogging(loggerFactory =>
    {
        if (Enum.TryParse(config["LogLevel"], out LogLevel logLevel))
        {
            Console.WriteLine($"Console Logging enabled with level '{logLevel}'");
            loggerFactory.AddConsole().SetMinimumLevel(logLevel);
        }
    })
    .UseKestrel((context, kestrelOptions) =>
    {
        kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ServerCertificate = new X509Certificate2(Path.Combine(context.HostingEnvironment.ContentRootPath, "testCert.pfx"), "testPassword");
        });
    })
    .UseContentRoot(Directory.GetCurrentDirectory())
    .UseConfiguration(config)
    .ConfigureServices(services =>
    {
        services.AddHttpForwarder();
    })
    ;

builder.Configure(app =>
{
    var forwarder = app.ApplicationServices.GetRequiredService<IHttpForwarder>();
    var clusterUrl = GetClusterUrl();
    var httpClient = new HttpMessageInvoker(CreateHandler());

    app.Run(async context =>
    {
        await forwarder.SendAsync(context, clusterUrl, httpClient, ForwarderRequestConfig.Empty, HttpTransformer.Default);
    });
});

builder.Build().Run();

string GetClusterUrl()
{
    var clusterUrls = config["clusterUrls"];

    if (string.IsNullOrWhiteSpace(clusterUrls))
    {
        throw new ArgumentException("--clusterUrls is required");
    }

    var clusterUrl = clusterUrls.Split(';')[0];

    Console.WriteLine($"ClusterUrl: {clusterUrl}");

    return clusterUrl;
}

static SocketsHttpHandler CreateHandler()
{
    var handler = new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false,
        EnableMultipleHttp2Connections = true,
        ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current),
        ConnectTimeout = TimeSpan.FromSeconds(15),
    };

    handler.SslOptions.RemoteCertificateValidationCallback = delegate { return true; };

    return handler;
}
