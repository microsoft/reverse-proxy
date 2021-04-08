# Configuration Files

Introduced: preview1
Updated: preview5

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

## Configuration contract
File-based configuration is dynamically mapped to the types in [Yarp.ReverseProxy.Abstractions](xref:Yarp.ReverseProxy.Abstractions) namespace by an [IProxyConfigProvider](xref:Yarp.ReverseProxy.Service.IProxyConfigProvider) implementation converts at application start and each time the configuration changes.

## Configuration Structure
The configuration consists of a named section that you specified above via `Configuration.GetSection("ReverseProxy")`, and contains subsections for routes and clusters.

Example:
```JSON
{
  "ReverseProxy": {
    "Routes": {
      "route1" : {
        "ClusterId": "cluster1",
        "Match": {
          "Path": "{**catch-all}"
        },
      }
    },
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

### Routes
The routes section is an ordered list of route matches and their associated configuration. A route requires at least the following fields:
- RouteId - A unique name
- ClusterId - Refers to the name of an entry in the clusters section.
- Match containing either a Hosts array or a Path pattern string.

[Headers](header-routing.md), [Authorization](authn-authz.md), [CORS](cors.md), and other route based policies can be configured on each route entry. For additional fields see [ProxyRoute](xref:Yarp.ReverseProxy.Abstractions.ProxyRoute).

The proxy will apply the given matching criteria and policies, and then pass off the request to the specified cluster.

### Clusters
The clusters section is an unordered collection of named clusters. A cluster primarily contains a collection of named destinations and their addresses, any of which is considered capable of handling requests for a given route. The proxy will process the request according to the route and cluster configuration in order to select a destination.

For additional fields see [Cluster](xref:Yarp.ReverseProxy.Abstractions.Cluster).

## All config properties
```JSON
{
  // Base URLs the server listens on, must be configured independently of the routes below
  "Urls": "http://localhost:5000;https://localhost:5001",
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
    "Routes": { 
      "minimumroute" : {
        // Matches anything and routes it to www.example.com
        "ClusterId": "minimumcluster",
        "Match": {
          "Path": "{**catch-all}"
        }
      },
      "allrouteprops" : {
        // matches /something/* and routes to "allclusterprops"
        "ClusterId": "allclusterprops", // Name of one of the clusters
        "Order" : 100, // Lower numbers have higher precidence
        "Authorization Policy" : "Anonymous", // Name of the policy or "Default", "Anonymous"
        "CorsPolicy" : "Default", // Name of the CorsPolicy to apply to this route or "Default", "Disable"
        "Match": {
          "Path": "/something/{**remainder}", // The path to match using ASP.NET syntax. 
          "Hosts" : [ "www.aaaaa.com", "www.bbbbb.com"], // The host names to match, unspecified is any
          "Methods" : [ "GET", "PUT" ], // The HTTP methods that match, uspecified is all
          "Headers" : [ // The headers to match, unspecified is any
            {
              "Name" : "MyCustomHeader", // Name of the header
              "Values" : ["value1", "value2", "another value"], // Matches are against any of these values
              "Mode" : "ExactHeader", // or "HeaderPrefix", "Exists"
              "IsCaseSensitive" : true
            }
          ],
        },
        "MetaData" : { // List of key value pairs that can be used by custom extensions
          "MyName" : "MyValue"
        },
        "Transforms" : [ // List of transforms. See ./Transforms.html for more details
          {
            "RequestHeader": "MyHeader",
            "Set": "MyValue",
          } 
        ]
      }
    },
    // Clusters tell the proxy where and how to forward requests
    "Clusters": {
      "minimumcluster": {
        "Destinations": {
          "example.com": {
            "Address": "http://www.example.com/"
          }
        }
      },
      "allclusterprops": {
        "Destinations": {
          "first_destination": {
            "Address": "https://contoso.com"
          },
          "another_destination": {
            "Address": "https://10.20.30.40",
            "Health" : "https://10.20.30.40:12345/test" // override for active health checks
          }
        },
        "LoadBalancingPolicy" : "PowerOfTwoChoices", // Alternatively "First", "Random", "RoundRobin", "LeastRequests"
        "SessionAffinity": {
          "Enabled": true, // Defaults to 'false'
          "Mode": "Cookie", // Default, alternatively "CustomHeader"
          "FailurePolicy": "Redistribute", // default, Alternatively "Return503"
          "Settings" : {
              "CustomHeaderName": "MySessionHeaderName" // Defaults to 'X-Yarp-Proxy-Affinity`
          }
        },
        "HealthCheck": {
          "Active": { // Makes API calls to validate the health. 
            "Enabled": "true",
            "Interval": "00:00:10",
            "Timeout": "00:00:10",
            "Policy": "ConsecutiveFailures",
            "Path": "/api/health" // API endpoint to query for health state
          },
          "Passive": { // Disables destinations based on HTTP response codes
            "Enabled": true, // Defaults to false
            "Policy" : "TransportFailureRateHealthPolicy", // Required
            "ReactivationPeriod" : "00:00:10" // 10s
          }
        },
        "HttpClient" : { // Configuration of HttpClient instance used to contact destinations
          "SSLProtocols" : "Tls13",
          "DangerousAcceptAnyServerCertificate" : false,
          "ClientCertificate" : {
            // From a file use
            "Path ": "mycert.pfx", 
            "KeyPath ": null, 
            "Password ": "myPassword1234",
            // From the cert store use
            "Subject ": null, 
            "Store ": null, 
            "Location ": null, 
            "AllowInvalid ": null 
          },
          "MaxConnectionsPerServer" : 1024,
          "ActivityContextHeaders" : "None", // Or "Baggage", "CorrelationContext", "BaggageAndCorrelationContext"
          "EnableMultipleHttp2Connections" : true,
          "RequestHeaderEncoding" : "Latin1" // How to interpret non ASCII characters in header values
        },
        "HttpRequest" : { // Options for sending request to destination
          "Timeout" : "00:02:00",
          "Version" : "2",
          "VersionPolicy" : "RequestVersionOrLower"
        },
        "MetaData" : { // Custom Key value pairs
          "TransportFailureRateHealthPolicy.RateLimit": "0.5", // Used by Passive health policy
          "MyKey" : "MyValue"
        }
      }
    }
  }
}
```
