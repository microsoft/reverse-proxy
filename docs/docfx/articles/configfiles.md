# Configuration Files

Introduced: preview1

## Introduction
The reverse proxy can load configuration for routes and clusters from files using the IConfiguration abstraction from Microsoft.Extensions. The examples given here use JSON, but any IConfiguration source should work. The configuration will also be updated without restarting the proxy if the source file changes.

## Loading Configuration
To load the proxy configuration from IConfiguration add the following code in Startup:
```c#
public IConfiguration Configuration { get; }

public Startup(IConfiguration configuration)
{
    Configuration = configuration;
}

public void ConfigureServices(IServiceCollection services) 
{ 
    services.AddReverseProxy() 
        .LoadFromConfig(Configuration.GetSection("ReverseProxy")); 
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

## Configuration Structure
The configuration consists of a named section that you specified above via `Configuration.GetSection("ReverseProxy")`, and contains subsections for routes and clusters.

Example:
```JSON
{
  "ReverseProxy": {
    "Routes": [
      {
        "RouteId": "route1",
        "ClusterId": "cluster1",
        "Match": {
          "Hosts": [ "localhost" ]
        },
      }
    ],
    "Clusters": {
      "cluster1": {
        "Destinations": {
          "cluster1/destination1": {
            "Address": "https://localhost:10001/"
          }
        }
      }
    }
  }
}
```

### Routes
The routes section is an ordered list of route matches and their associated configuration. A route requires at least the following fields:
- RouteId - A unique name
- ClusterId - Refers to the name of an entry in the clusters section.
- Match containing either a Hosts array or a Path pattern string.

[Authorization](authn-authz.md), [CORS](cors.md), and other route based policies can be configured on each route entry. For additional fields see [ProxyRoute](xref:Microsoft.ReverseProxy.Abstractions.ProxyRoute).

The proxy will apply the given matching criteria and policies, and then pass off the request to the specified cluster.

### Clusters
The clusters section is an unordered collection of named clusters. A cluster primarily contains a collection of named destinations and their addresses, any of which is considered capable of handling requests for a given route. The proxy will process the request according to the route and cluster configuration in order to select a destination.

For additional fields see [Cluster](xref:Microsoft.ReverseProxy.Abstractions.Cluster).
