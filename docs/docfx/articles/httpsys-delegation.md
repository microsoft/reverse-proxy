# Http.sys Delegation

## Introduction
Http.sys delegation is a kernel level feature added into newer versions of Windows which allows a request to be transferred from the receiving process's http.sys queue to a target process's http.sys queue with very little overhead or added latency. For this delegation to work, the receiving process is only allowed to read the request headers. If the body has started to be read or a response has started, trying to delegate the request will fail. The response will not be visible to the proxy after delegation, which limits the functionality of the session affinity and passive health checks components, as well as some of the load balancing algorithms. Internally, YARP leverage's ASP.NET Core's [IHttpSysRequestDelegationFeature](https://docs.microsoft.com/dotnet/api/microsoft.aspnetcore.server.httpsys.ihttpsysrequestdelegationfeature) 

## Requirements
Http.sys delegation requires:
- ASP.NET Core 6+
- [ASP.NET Core's Http.sys server](https://docs.microsoft.com/aspnet/core/fundamentals/servers/httpsys)
- Windows Server 2019 or Windows 10 (build number 1809) or newer.

## Defaults
Http.sys delegation won't be used unless added to the proxy pipeline and enabled in the destination configuration. 

## Configuration
Http.sys delegation can be enabled per destination by adding the `HttpSysDelegationQueue` metadata to the destination. The value of this metadata should be the target http.sys queue name. The destination's Address is used to specify the url prefix of the http.sys queue.

```json
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
            "Address": "http://*:80/",
            "Metadata": {
              "HttpSysDelegationQueue": "TargetHttpSysQueueName"
            }
          }
        }
      }
    }
  }
}
```

In host configuration, configure the host to use the Http.sys server.
```c#
webBuilder.UseHttpSys();
```

In application configuration, use the `MapReverseProxy` overload that lets you customize the pipeline and add http.sys delegation by calling `UseHttpSysDelegation`.
```c#
app.MapReverseProxy(proxyPipeline =>
{
    // Add the three middleware YARP adds by default plus the Http.sys delegation middleware
    proxyPipeline.UseSessionAffinity(); // Has no affect on delegation destinations
    proxyPipeline.UseLoadBalancing();
    proxyPipeline.UsePassiveHealthChecks();
    proxyPipeline.UseHttpSysDelegation();
});