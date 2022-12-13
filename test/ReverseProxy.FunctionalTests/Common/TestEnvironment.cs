// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Common;

public class TestEnvironment
{
    public ILoggerFactory LoggerFactory { get; set; }

    public HttpProtocols ProxyProtocol { get; set; } = HttpProtocols.Http1AndHttp2;

    public bool UseHttpsOnProxy { get; set; }

    public Encoding HeaderEncoding { get; set; }

    public Action<IServiceCollection> ConfigureProxyServices { get; set; } = _ => { };

    public Action<IReverseProxyBuilder> ConfigureProxy { get; set; } = _ => { };

    public Action<IApplicationBuilder> ConfigureProxyApp { get; set; } = _ => { };

    public string ClusterId { get; set; } = "cluster1";

    public Func<ClusterConfig, RouteConfig, (ClusterConfig Cluster, RouteConfig Route)> ConfigTransformer { get; set; } = (a, b) => (a, b);

    public Version DestionationHttpVersion { get; set; }

    public HttpVersionPolicy? DestionationHttpVersionPolicy { get; set; }

    public HttpProtocols DestinationProtocol { get; set; } = HttpProtocols.Http1AndHttp2;

    public bool UseHttpsOnDestination { get; set; }

    public Action<IServiceCollection> ConfigureDestinationServices { get; set; } = _ => { };

    public Action<IApplicationBuilder> ConfigureDestinationApp { get; set; } = _ => { };

    public TestEnvironment() { }

    public TestEnvironment(RequestDelegate destinationGetDelegate)
    {
        ConfigureDestinationApp = destinationApp =>
        {
            destinationApp.Run(destinationGetDelegate);
        };
    }

    public async Task Invoke(Func<string, Task> clientFunc, CancellationToken cancellationToken = default)
    {
        using var destination = CreateHost(DestinationProtocol, UseHttpsOnDestination, HeaderEncoding, ConfigureDestinationServices, ConfigureDestinationApp);
        await destination.StartAsync(cancellationToken);

        using var proxy = CreateProxy(destination.GetAddress());
        await proxy.StartAsync(cancellationToken);

        try
        {
            await clientFunc(proxy.GetAddress());
        }
        finally
        {
            await proxy.StopAsync(cancellationToken);
            await destination.StopAsync(cancellationToken);
        }
    }

    public IHost CreateProxy(string destinationAddress)
    {
        return CreateHost(ProxyProtocol, UseHttpsOnProxy, HeaderEncoding,
            services =>
            {
                ConfigureProxyServices(services);

                var route = new RouteConfig
                {
                    RouteId = "route1",
                    ClusterId = ClusterId,
                    Match = new RouteMatch { Path = "/{**catchall}" }
                };

                var cluster = new ClusterConfig
                {
                    ClusterId = ClusterId,
                    Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "destination1",  new DestinationConfig() { Address = destinationAddress } }
                    },
                    HttpClient = new HttpClientConfig
                    {
                        DangerousAcceptAnyServerCertificate = UseHttpsOnDestination,
                        RequestHeaderEncoding = HeaderEncoding?.WebName,
                    },
                    HttpRequest = new Forwarder.ForwarderRequestConfig
                    {
                        Version = DestionationHttpVersion,
                        VersionPolicy = DestionationHttpVersionPolicy,
                    }
                };
                (cluster, route) = ConfigTransformer(cluster, route);
                var proxyBuilder = services.AddReverseProxy().LoadFromMemory(new[] { route }, new[] { cluster });
                ConfigureProxy(proxyBuilder);
            },
            app =>
            {
                ConfigureProxyApp(app);
                app.UseRouting();
                app.UseEndpoints(builder =>
                {
                    builder.MapReverseProxy();
                });
            });
    }

    private IHost CreateHost(HttpProtocols protocols, bool useHttps, Encoding requestHeaderEncoding,
        Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configureApp)
    {
        var hostBuilder = new HostBuilder();
        if (LoggerFactory != null)
        {
            hostBuilder.ConfigureServices(s => s.AddSingleton(LoggerFactory));
        }

        hostBuilder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>()
            {
                { "Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics", "Information" }
            });
        })
        .ConfigureLogging((hostingContext, loggingBuilder) =>
        {
            loggingBuilder.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
            loggingBuilder.AddEventSourceLogger();
        })
        .ConfigureWebHost(webHostBuilder =>
        {
            webHostBuilder
                .ConfigureServices(configureServices)
                .UseKestrel(kestrel =>
                {
                    if (requestHeaderEncoding is not null)
                    {
                        kestrel.RequestHeaderEncodingSelector = _ => requestHeaderEncoding;
                    }
                    kestrel.Listen(IPAddress.Loopback, 0, listenOptions =>
                    {
                        listenOptions.Protocols = protocols;
                        if (useHttps)
                        {
                            listenOptions.UseHttps(TestResources.GetTestCertificate());
                        }
                    });
                })
                .Configure(configureApp);
        });

        return hostBuilder.Build();
    }
}
