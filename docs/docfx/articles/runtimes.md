---
uid: runtimes
title: Supported Runtimes
---

# YARP Supported Runtimes

YARP 1.1 supports ASP.NET Core 3.1, 5.0 and 6.0. You can download the .NET SDK from https://dotnet.microsoft.com/download/dotnet/. See [Releases](https://github.com/microsoft/reverse-proxy/releases) for specific version support.

YARP is taking advantage of ASP.NET Core 6.0 features and optimizations. This does mean that some features may not be available if you're running on the previous versions of ASP.NET.

## Difference in features supported on .NET Core 3.1 vs .NET 5.0 and higher

### Yarp.ReverseProxy package
The following features are supported on NET 5.0 and higher:
- [EnableMultipleHttp2Connections](http-client-config.md#httpclient) - enables opening additional HTTP/2 connections to the same server when the maximum number of concurrent streams is reached on all existing connections. Full path: `ClusterConfig.HttpClient.EnableMultipleHttp2Connections`. Type: `bool?`
- [RequestHeaderEncoding](http-client-config.md#httpclient) - allows to set a non-ASCII header encoding for outgoing requests. Full path: `ClusterConfig.HttpClient.RequestHeaderEncoding`. Type: `string?`
- [VersionPolicy](http-client-config.md#httprequest) - policy applied to version selection, e.g. whether to prefer downgrades, upgrades or request an exact version. The default is `RequestVersionOrLower`. Full path: `ClusterConfig.HttpRequest.VersionPolicy`. Type: `HttpVersionPolicy?`

### Yarp.Telemetry.Consumption package
On .NET Core 3.1 it supports only:
- YARP events and metrics
- Kestrel events

On .NET 5 and higher it supports events and metrics for:
- YARP
- Kestrel
- HTTP (SocketsHttpHandler)
- NameResolution (DNS)
- NetSecurity (SslStream)
- Sockets

## Building for .NET Core 3.1
YARP can be run on the .Net Core 3.1 runtime, but the .NET 5.0 SDK is still required for this runtime version because YARP APIs use C# 9 language features. Thus, the steps to create a YARP-based application are as follows:
1. Install the .NET 5.0 SDK: https://dotnet.microsoft.com/download/dotnet/5.0
2. Add the reference to the YARP package to the application project
3. Add the property `<LangVersion>9.0</LangVersion>` to the .csproj file
4. Add a new `IsExternalInit.cs` file with the following content to the root of the project.
```C#
namespace System.Runtime.CompilerServices
{
    // Allows "init" properties in .NET Core 3.1.
    internal static class IsExternalInit { }
}
```

## Related 5.0 Runtime Improvements

These are related improvements in .NET 5.0 or ASP.NET Core 5.0 that YARP is able to take advantage of:
- Kestrel [reloadable config](https://github.com/dotnet/aspnetcore/issues/19376).
- Kestrel HTTP/2 performance improvements:
  - [HPACK static compression](https://github.com/dotnet/aspnetcore/pull/20058).
  - [HPACK dynamic compression](https://github.com/dotnet/aspnetcore/pull/19521).
  - [Allocation savings via stream pooling](https://github.com/dotnet/aspnetcore/pull/18601).
  - [Allocation savings via pipe pooling](https://github.com/dotnet/aspnetcore/pull/19356).
- HttpClient HTTP/2 [performance improvements](https://github.com/dotnet/runtime/issues/35184).

## Related 6.0 Runtime Improvements

- [HTTP/3](http3.md) - support for inbound and outbound connections (preview).
- [Distributed Tracing](distributed-tracing.md) - .NET 6.0 has built-in configurable support that YARP takes advantage of to enable more scenarios out-of-the-box.
- [Http.sys Delegation](httpsys-delegation.md) - a kernel-level ASP.NET Core 6 feature that allows a request to be transferred to a different process.
- [UseHttpLogging](diagnosing-yarp-issues.md#using-aspnet-6-request-logging) - includes an additional middleware component that can be used to provide more details about the request and response.
- [Dynamic HTTP/2 window scaling](https://github.com/dotnet/runtime/pull/54755) - improves HTTP/2 download speed on high-latency connections.
- [NonValidated headers](https://github.com/microsoft/reverse-proxy/pull/1507) - improves perfomance by using non-validated HttpClient headers.


## Related 7.0 Runtime Improvements

- [HTTP/3](http3.md) - support for inbound and outbound connections (stable).
- [Zero-byte reads on HttpClient's response streams](https://github.com/dotnet/runtime/pull/61913) - reduces memory usage.
- [Header allocation reductions](https://github.com/dotnet/runtime/pull/62981) - reduces memory usage.
- [Kestrel Http/2 perf improvements](https://github.com/dotnet/aspnetcore/pull/40925) - Improve contention and throughput for multiple requests on one connection.

