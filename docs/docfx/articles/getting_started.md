---
uid: getting_started
title: Getting Started with YARP
---

# Getting Started with YARP

YARP is designed as a library that provides the core proxy functionality which you can then customize by adding or replacing modules. YARP is currently provided as a NuGet package and code snippets. We plan on providing a project template and pre-built exe in the future. 

YARP supports ASP.NET Core 3.1 and 5.0.0 Preview 4 or later. You can download the .NET 5 Preview 4 SDK from https://dotnet.microsoft.com/download/dotnet/5.0. It requires Visual Studio 2019 (v16.6) or newer.

### Create a new project

Start by creating an "Empty" ASP.NET Core application using the command line:

```
dotnet new web -n MyProxy 
```

use `-f` to specify `netcoreapp3.1` or `netcoreapp5.0`.

Or create a new ASP.NET Core web application in Visual Studio, and choose "Empty" for the project template. 

### Update the project file

Open the Project and make sure it includes the appropriate target framework: 
 
 ```
<PropertyGroup>
  <TargetFramework>net5.0</TargetFramework>
</PropertyGroup> 
```

And then add:
 
 ```
<ItemGroup> 
  <PackageReference Include="Microsoft.ReverseProxy" Version="1.0.0-preview.1.*" /> 
</ItemGroup> 
```

### Update Startup

YARP is implemented as a ASP.NET Core component, and so the majority of the sample code is in Startup.cs. 

YARP currently uses configuration files to define the routes and endpoints for the proxy. That is loaded in the `ConfigureServices` method. 

```
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
```

The `Configure` method defines the ASP.NET pipeline for processing requests. The reverse proxy is plugged into ASP.NET endpoint routing, and then has its own sub-pipeline for the proxy. Here proxy pipeline modules, such as load balancing, can be added to customize the handling of the request. 
```
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseRouting();
    app.UseEndpoints(endpoints => 
    {
        endpoints.MapReverseProxy(proxyPipeline => 
        { 
            proxyPipeline.UseProxyLoadBalancing(); 
        }); 
    }); 
} 
```
 
### Configuration 

The configuration for YARP is defined in the appsettings.json file. It defines a set of:

Routes - which map incoming requests to the clusters based on aspects of the request such as host name, path, method, request headers etc. A route must specify at least a host or path. Routes are ordered, so the "app1" route needs to be defined first since "route2" will act as a catchall for all paths that have not already been matched. 

Clusters - which are the groups of destination servers that requests can be routed to and load balanced across.

Address is the URI prefix that will have the original request path and query appended to it.

You can find out more about the available configuration options by looking at [ProxyRoute](xref:Microsoft.ReverseProxy.Abstractions.ProxyRoute) and [Cluster](xref:Microsoft.ReverseProxy.Abstractions.Cluster).
 
 ```
 {
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "AllowedHosts": "*",
  "ReverseProxy": {
    "Routes": [
      {
        "RouteId": "app1",
        "ClusterId": "cluster1",
        "Match": {
          "Methods": [ "GET", "POST" ],
          "Host": "localhost",
          "Path": "/app1/"
        }
      },
      {
        "RouteId": "route2",
        "ClusterId": "cluster2",
        "Match": {
          "Host": "localhost"
        }
      }
    ],
    "Clusters": {
      "cluster1": {
        "LoadBalancing": {
          "Mode": "Random"
        },
        "Destinations": {
          "cluster1_destination1": {
            "Address": "https://example.com/"
          },
          "cluster1_destination2": {
            "Address": "http://example.com/"
          }
        }
      },
      "cluster2": {
        "Destinations": {
          "cluster2_destination1": {
            "Address": "https://example.com:10001/"
          }
        }
      }
    }
  }
}
```
