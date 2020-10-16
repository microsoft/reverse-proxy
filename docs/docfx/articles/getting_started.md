---
uid: getting_started
title: Getting Started with YARP
---

# Getting Started with YARP

YARP is designed as a library that provides the core proxy functionality which you can then customize by adding or replacing modules. YARP is currently provided as a NuGet package and code snippets. We plan on providing a project template and pre-built exe in the future. 

YARP 1.0.0 Preview 6 supports ASP.NET Core 3.1 and 5.0.0 RC 2 or later. You can download the .NET 5 Preview SDK from https://dotnet.microsoft.com/download/dotnet/5.0. 5.0 requires Visual Studio 2019 v16.8 Preview3 or newer.

### Create a new project

Start by creating an "Empty" ASP.NET Core application using the command line:

```
dotnet new web -n MyProxy 
```

use `-f` to specify `netcoreapp3.1` or `net5.0`.

Or create a new ASP.NET Core web application in Visual Studio, and choose "Empty" for the project template. 

### Update the project file

Open the Project and make sure it includes the appropriate target framework: 
 
 ```XML
<PropertyGroup>
  <TargetFramework>net5.0</TargetFramework>
</PropertyGroup> 
```

And then add:
 
 ```XML
<ItemGroup> 
  <PackageReference Include="Microsoft.ReverseProxy" Version="1.0.0-preview.6.*" />
</ItemGroup> 
```

### Update Startup

YARP is implemented as a ASP.NET Core component, and so the majority of the sample code is in Startup.cs. 

YARP currently uses configuration files to define the routes and endpoints for the proxy. That is loaded in the `ConfigureServices` method. 

The `Configure` method defines the ASP.NET pipeline for processing requests. The reverse proxy is plugged into ASP.NET endpoint routing, and then has its own sub-pipeline for the proxy. Here proxy pipeline modules, such as load balancing, can be added to customize the handling of the request. 

```C#
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
 
### Configuration 

The configuration for YARP is defined in the appsettings.json file. See [Configuration Files](configfiles.md) for details.

The configuration can also be provided programmatically. See [Configuration Providers](configproviders.md) for details.

You can find out more about the available configuration options by looking at [ProxyRoute](xref:Microsoft.ReverseProxy.Abstractions.ProxyRoute) and [Cluster](xref:Microsoft.ReverseProxy.Abstractions.Cluster).
 
 ```JSON
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
        "RouteId": "route1",
        "ClusterId": "cluster1",
        "Match": {
          "Path": "{**catch-all}"
        },
      }
    ],
    "Clusters": {
      "cluster1": {
        "Destinations": {
          "cluster1/destination1": {
            "Address": "https://example.com/"
          }
        }
      }
    }
  }
}
```
