# Configuration Providers

Introduced: preview4

## Introduction
Proxy configuration can be loaded programmatically from the source of your choosing by implementing an [IProxyConfigProvider](xref:Microsoft.ReverseProxy.Service.IProxyConfigProvider).

## Structure
[IProxyConfigProvider](xref:Microsoft.ReverseProxy.Service.IProxyConfigProvider) has a single method `GetConfig()` that returns an [IProxyConfig](xref:Microsoft.ReverseProxy.Service.IProxyConfig) instance. The IProxyConfig has lists of the current routes and clusters, as well as an `IChangeToken` to notify the proxy when this information is out of date and should be reloaded by calling `GetConfig()` again.

### Routes
The routes section is an ordered list of route matches and their associated configuration. A route requires at least the following fields:
- RouteId - A unique name
- ClusterId - Refers to the name of an entry in the clusters section.
- Match containing either a Hosts array or a Path pattern string.

[Headers](header-routing.md), [Authorization](authn-authz.md), [CORS](cors.md), and other route based policies can be configured on each route entry. For additional fields see [ProxyRoute](xref:Microsoft.ReverseProxy.Abstractions.ProxyRoute).

The proxy will apply the given matching criteria and policies, and then pass off the request to the specified cluster.

### Clusters
The clusters section is an unordered collection of named clusters. A cluster primarily contains a collection of named destinations and their addresses, any of which is considered capable of handling requests for a given route. The proxy will process the request according to the route and cluster configuration in order to select a destination.

For additional fields see [Cluster](xref:Microsoft.ReverseProxy.Abstractions.Cluster).

## Lifecycle

### Startup
The `IProxyConfigProvider` should be registered in the DI container as a singleton. At startup the proxy will resolve this instance and call `GetConfig()`. On this first call the provider may choose to:
- Throw an exception if the provider cannot produce a valid proxy configuration for any reason. This will prevent the application from starting.
- Synchronously block while it loads the configuraiton. This will block the application from starting until valid route data is available.
- Or, it may choose to return an empty `IProxyConfig` instance while it loads the configuration in the background. The provider will need to trigger the `IChangeToken` when the configuration is available.

The proxy will validate the given configuration and if it's invalid, an exception will be thrown that prevents the application from starting. The provider can avoid this by using the [IConfigValidator](xref:Microsoft.ReverseProxy.Service.IConfigValidator) to pre-validate routes and clusters and take whatever action it deems appropriate such as excluding invalid entries.

### Reload
If the `IChangeToken` supports `ActiveChangeCallbacks`, once the proxy has processed the initial set of configuration it will register a callback with this token. Note the proxy does not support polling for changes.

When the provider wants to provide new configuration to the proxy it should first load that configuration in the background, optionally validate it using the [IConfigValidator](xref:Microsoft.ReverseProxy.Service.IConfigValidator), and only then signal the `IChangeToken` from the prior `IProxyConfig` instance that new data is available. The proxy will call `GetConfig()` again to retrieve the new data.

There are important differences when reloading configuration vs the first configuration load.
- The new configuration will be diffed against the current one and only modified routes or clusters will be updated. The update will be applied atomically and will only affect new requests, not request currently in progress.
- Any errors in the reload process will be logged and suppressed. The application will continue using the last known good configuration.
- If `GetConfig()` throws the proxy will be unable to listen for future changes because `IChangeToken`s are single-use.

Once the new configuration has been validated and applied, the proxy will register a callback with the new `IChangeToken`. Note if there are multiple reloads signaled in close succession, the proxy may skip some and load the next available configuration as soon as it's ready. Each `IProxyConfig` contains the full configuration state so nothing will be lost.

## Example
The following is an example `IProxyConfigProvider` that has routes and clusters manually loaded into it.

```C#
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Primitives;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Configuration;
using Microsoft.ReverseProxy.Service;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class InMemoryConfigProviderExtensions
    {
        public static IReverseProxyBuilder LoadFromMemory(this IReverseProxyBuilder builder, IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters)
        {
            builder.Services.AddSingleton<IProxyConfigProvider>(new InMemoryConfigProvider(routes, clusters));
            return builder;
        }
    }
}

namespace Microsoft.ReverseProxy.Configuration
{
    public class InMemoryConfigProvider : IProxyConfigProvider
    {
        private volatile InMemoryConfig _config;

        public InMemoryConfigProvider(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters)
        {
            _config = new InMemoryConfig(routes, clusters);
        }

        public IProxyConfig GetConfig() => _config;

        public void Update(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters)
        {
            var oldConfig = _config;
            _config = new InMemoryConfig(routes, clusters);
            oldConfig.SignalChange();
        }

        private class InMemoryConfig : IProxyConfig
        {
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();

            public InMemoryConfig(IReadOnlyList<ProxyRoute> routes, IReadOnlyList<Cluster> clusters)
            {
                Routes = routes;
                Clusters = clusters;
                ChangeToken = new CancellationChangeToken(_cts.Token);
            }

            public IReadOnlyList<ProxyRoute> Routes { get; }

            public IReadOnlyList<Cluster> Clusters { get; }

            public IChangeToken ChangeToken { get; }

            internal void SignalChange()
            {
                _cts.Cancel();
            }
        }
    }
}
```

And here's how it's called in Startup.cs:
```C#
public void ConfigureServices(IServiceCollection services)
{
    var routes = new[]
    {
        new ProxyRoute()
        {
            RouteId = "route1",
            ClusterId = "cluster1",
            Match = new ProxyMatch
            {
                Path = "{**catch-all}"
            }
        }
    };
    var clusters = new[]
    {
        new Cluster()
        {
            Id = "cluster1",
            Destinations = new Dictionary<string, Destination>(StringComparer.OrdinalIgnoreCase)
            {
                { "destination1", new Destination() { Address = "https://example.com" } }
            }
        }
    };

    services.AddReverseProxy()
        .LoadFromMemory(routes, clusters);
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseRouting();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapReverseProxy();
    });
}
```
