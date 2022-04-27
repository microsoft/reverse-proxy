# Lets Encrypt

## Introduction
YARP can support a certificate authority [Lets Encrypt](https://letsencrypt.org/) by using the API of another ASP.NET Core project [LettuceEncrypt](https://github.com/natemcmaster/LettuceEncrypt). It allows to set up TLS between the client and YARP and then use HTTP communication to the backend.

## Requirements

LettuceEncrypt package should be added into project:
```csproj
<PackageReference Include="LettuceEncrypt" Version="1.1.2" />
```

## Configuration
There are required options for LettuceEncrypt that should be set, see the example of `appsettings.json`:

```JSON
{
  // Base URLs the server listens on, must be configured independently of the routes below.
  // Can also be configured via Kestrel/Endpoints, see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints
  "Urls": "http://localhost:80;https://localhost:443",

  //Sets the Logging level for ASP.NET
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      // Uncomment to hide diagnostic messages from runtime and proxy
      // "Microsoft": "Warning",
      // "Yarp" : "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },

  "ReverseProxy": {
    // Routes tell the proxy which requests to forward
    "Routes": { ...  },
    // Clusters tell the proxy where and how to forward requests
    "Clusters": { ...  }
  },
  
  "LettuceEncrypt": {
    // Set this to automatically accept the terms of service of your certificate authority.
    // If you don't set this in config, you will need to press "y" whenever the application starts
    "AcceptTermsOfService": true,

    // You must at least one domain name
    "DomainNames": [ "example.com" ],

    // You must specify an email address to register with the certificate authority
    "EmailAddress": "it-admin@example.com"
  }
}
```

## Update Startup

```C#
using Microsoft.Extensions.DependencyInjection;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLettuceEncrypt();
    }
}
```

For more options (i.e. saving certificates) see examples in [LettuceEncrypt doc](ttps://github.com/natemcmaster/LettuceEncrypt).

## Middleware

If your project is explicitly using kestrel options to configure IP addresses, ports, or HTTPS settings, you will also need to call `UseLettuceEncrypt`. This is required to make Lettuce Encrypt work.

Example:

```C#
var myHostBuilder = Host.CreateDefaultBuilder(args);
myHostBuilder.ConfigureWebHostDefaults(webHostBuilder =>
{
    webHostBuilder.ConfigureKestrel(kestrel =>
    {
        kestrel.ListenAnyIP(443, portOptions =>
        {
            portOptions.UseHttps(h =>
            {
                h.UseLettuceEncrypt(kestrel.ApplicationServices);
            });
        });
    });
    webHostBuilder.UseStartup<Startup>();
});
```

