// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Utilities.Tests;

namespace Microsoft.ReverseProxy.Common
{
    public class TestEnvironment
    {
        private readonly Action<IServiceCollection> _configureDestinationServices;
        private readonly Action<IApplicationBuilder> _configureDestinationApp;
        private readonly Action<IReverseProxyBuilder> _configureProxy;
        private readonly Action<IApplicationBuilder> _configureProxyApp;
        private readonly HttpProtocols _proxyProtocol;
        private readonly bool _useHttpsOnDestination;

        public string ClusterId { get; set; } = "cluster1";

        public TestEnvironment(
            RequestDelegate destinationGetDelegate,
            Action<IReverseProxyBuilder> configureProxy, Action<IApplicationBuilder> configureProxyApp,
            HttpProtocols proxyProtocol = HttpProtocols.Http1AndHttp2, bool useHttpsOnDestination = false)
            : this(
                  destinationServices => { },
                  destinationApp =>
                  {
                      destinationApp.Use(async (context, next) => await destinationGetDelegate(context));
                  },
                  configureProxy,
                  configureProxyApp,
                  proxyProtocol,
                  useHttpsOnDestination)
        { }

        public TestEnvironment(
            Action<IServiceCollection> configureDestinationServices, Action<IApplicationBuilder> configureDestinationApp,
            Action<IReverseProxyBuilder> configureProxy, Action<IApplicationBuilder> configureProxyApp,
            HttpProtocols proxyProtocol = HttpProtocols.Http1AndHttp2, bool useHttpsOnDestination = false)
        {
            _configureDestinationServices = configureDestinationServices;
            _configureDestinationApp = configureDestinationApp;
            _configureProxy = configureProxy;
            _configureProxyApp = configureProxyApp;
            _proxyProtocol = proxyProtocol;
            _useHttpsOnDestination = useHttpsOnDestination;
        }

        public async Task Invoke(Func<string, Task> clientFunc, CancellationToken cancellationToken = default)
        {
            using var destination = CreateHost(HttpProtocols.Http1AndHttp2, _useHttpsOnDestination, _configureDestinationServices, _configureDestinationApp);
            await destination.StartAsync(cancellationToken);

            using var proxy = CreateHost(_proxyProtocol, false,
                services =>
                {
                    var proxyRoute = new ProxyRoute
                    {
                        RouteId = "route1",
                        ClusterId = ClusterId,
                        Match = { Path = "/{**catchall}" }
                    };

                    var cluster = new Cluster
                    {
                        Id = ClusterId,
                        Destinations = new Dictionary<string, Destination>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "destination1",  new Destination() { Address = destination.GetAddress() } }
                        },
                        HttpClient = new ProxyHttpClientOptions
                        {
                            DangerousAcceptAnyServerCertificate = _useHttpsOnDestination
                        }
                    };

                    var proxyBuilder = services.AddReverseProxy().LoadFromMemory(new[] { proxyRoute }, new[] { cluster });
                    _configureProxy(proxyBuilder);
                },
                app =>
                {
                    _configureProxyApp(app);
                    app.UseRouting();
                    app.UseEndpoints(builder =>
                    {
                        builder.MapReverseProxy();
                    });
                });
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

        private static IHost CreateHost(HttpProtocols protocols, bool useHttps, Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configureApp)
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
                               listenOptions.Protocols = protocols;
                               if (useHttps)
                               {
                                   listenOptions.UseHttps(TestResources.GetTestCertificate());
                               }
                           });
                       })
                       .Configure(configureApp);
               }).Build();
        }
    }
}
