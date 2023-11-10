# Request Timeouts

## Introduction

.NET 8 introduced the [Request Timeouts Middleware](https://learn.microsoft.com/aspnet/core/performance/timeouts) to enable configuring request timeouts globally as well as per endpoint. This functionality is also available in YARP 2.1 when running on .NET 8.

## Defaults
Requests do not have any timeouts by default, other than the [Activity Timeout](http-client-config.md#HttpRequest) used to clean up idle requests. A default policy specified in [RequestTimeoutOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.timeouts.requesttimeoutoptions) will apply to proxied requests as well.

## Configuration
Timeouts and Timeout Policies can be specified per route via [RouteConfig](xref:Yarp.ReverseProxy.Configuration.RouteConfig) and can be bound from the `Routes` sections of the config file. As with other route properties, this can be modified and reloaded without restarting the proxy. Policy names are case insensitive.

Timeouts are specified in a TimeSpan HH:MM:SS format. Specifying both Timeout and TimeoutPolicy on the same route is invalid and will cause the configuration to be rejected.

Example:
```JSON
{
  "ReverseProxy": {
    "Routes": {
      "route1" : {
        "ClusterId": "cluster1",
        "TimeoutPolicy": "customPolicy",
        "Match": {
          "Hosts": [ "localhost" ]
        },
      }
      "route2" : {
        "ClusterId": "cluster1",
        "Timeout": "00:01:00",
        "Match": {
          "Hosts": [ "localhost2" ]
        },
      }
    },
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

Timeout policies can be configured in Startup.ConfigureServices as follows:
```
public void ConfigureServices(IServiceCollection services)
{
    services.AddRequestTimeouts(options =>
    {
        options.AddPolicy("customPolicy", TimeSpan.FromSeconds(20));
    });
}
```

In Startup.Configure add the timeout middleware between Routing and Endpoints.

```
public void Configure(IApplicationBuilder app)
{
    app.UseRouting();

    app.UseRequestTimeouts();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapReverseProxy();
    });
}
```


### DefaultPolicy

Specifying the value `default` in a route's `TimeoutPolicy` parameter means that route will use the policy defined in [RequestTimeoutOptions.DefaultPolicy](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.timeouts.requesttimeoutoptions.defaultpolicy#microsoft-aspnetcore-http-timeouts-requesttimeoutoptions-defaultpolicy).

### Disable timeouts

Specifying the value `disable` in a route's `TimeoutPolicy` parameter means the request timeout middleware will not apply timeouts to this route.

### WebSockets

Request timeouts are disabled after the initial WebSocket handshake.
