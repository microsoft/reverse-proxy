---
uid: getting_started
title: Getting Started with YARP
---

# Getting Started with YARP

YARP is designed as a library that provides the core proxy functionality which you can then customize by adding or replacing modules. YARP is currently supplied as a nuget package and code snippets. We plan on having a project template, and pre-built exe in the future. 

YARP supports ASP.NET Core 3.1 or 5.0 preview4 or later. You can download preview 4 of .NET 5 SDK from https://dotnet.microsoft.com/download/dotnet/5.0.

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
  <TargetFramework>netcoreapp5.0</TargetFramework>
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

The `Configure` method defines the ASP.NET pipeline for processing requests. The reverse proxy is plugged in to ASP.NET endpoint routing, and then has its own sub pipeline for the proxy. Here proxy pipeline modules, such as load balancing can be added to customize the handling of the request. 
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

Routes - which map incoming requests to the backend clusters based on aspects of the request such as host name, path, method, request headers etc. Specifying a Host is the only required field. Routes are ordered, so the "app1" route needs to be defined first as "route2" will act as a catchall for all paths that have not already been matched. 

Backends - which are the clusters of destination servers that requests can be routed to and load balanced across.

Address is the URI prefix that will have the original request path appended to it.

You can find out more about the available configuration options by looking at [ProxyConfigOptions](xref:Microsoft.ReverseProxy.Configuration.ProxyConfigOptions).
 
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
        "BackendId": "backend1",
        "Match": {
          "Methods": [ "GET", "POST" ],
          "Host": "localhost",
          "Path": "/app1/"
        }
      },
      {
        "RouteId": "route2",
        "BackendId": "backend2",
        "Match": {
          "Host": "localhost"
        }
      }
    ],
    "Backends": {
      "backend1": {
        "LoadBalancing": {
          "Mode": "Random"
        },
        "Destinations": {
          "backend1_destination1": {
            "Address": "https://example.com/"
          },
          "backend1_destination2": {
            "Address": "http://example.com/"
          }
        }
      },
      "backend2": {
        "Destinations": {
          "backend2_destination1": {
            "Address": "https://example.com:10001/"
          }
        }
      }
    }
  }
}
```
