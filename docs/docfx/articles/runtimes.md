---
uid: runtimes
title: Supported Runtimes
---

# YARP Supported Runtimes

YARP 1.0 previews support ASP.NET Core 3.1 and 5.0. You can download the .NET 5 SDK from https://dotnet.microsoft.com/download/dotnet/5.0. See [Releases](https://github.com/microsoft/reverse-proxy/releases) for specific version support.

YARP is taking advantage of ASP.NET Core 5.0 features and optimizations. This does mean that some features may not be available if you're running on ASP.NET Core 3.1.

## Difference in features supported on .NET Core 3.1 vs .NET 5.0 and higher

### Yarp.ReverseProxy package
The following features are supported on NET 5.0 and higher:
- EnableMultipleHttp2Connections - enables opening additional HTTP/2 connections to the same server when the maximum number of concurrent streams is reached on all existing connections. Full path: `ClusterConfig.HttpClient.EnableMultipleHttp2Connections`. Type: `bool?`
- RequestHeaderEncoding - allows to set a non-ASCII header encoding for outgoing requests. Full path: `ClusterConfig.HttpClient.RequestHeaderEncoding`. Type: `string?`
- VersionPolicy - policy applied to version selection, e.g. whether to prefer downgrades, upgrades or request an exact version. The default is `RequestVersionOrLower`. Full path: `ClusterConfig.HttpRequest.VersionPolicy`. Type: `HttpVersionPolicy?`

### Yarp.ReverseProxy.Telemetry.Consumption package
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
YARP can be run on .Net Core 3.1 runtime, but .NET 5.0 SDK is required to build it for this runtime version because YARP code uses C# 9 language features. Thus, the steps YARP-based application are as follows:
1. Install .NET 5.0 SDK
2. Add the reference to the YARP package to the application project
3. Add the property `<LangVersion>9.0</LangVersion>` to the .csproj file

## Related 5.0 Runtime Improvements

These are related improvements in .NET 5.0 or ASP.NET Core 5.0 that YARP is able to take advantage of:
- Kestrel [reloadable config](https://github.com/dotnet/aspnetcore/issues/19376).
- Kestrel HTTP/2 performance improvements:
  - [HPACK static compression](https://github.com/dotnet/aspnetcore/pull/20058).
  - [HPACK dynamic compression](https://github.com/dotnet/aspnetcore/pull/19521).
  - [Allocation savings via stream pooling](https://github.com/dotnet/aspnetcore/pull/18601).
  - [Allocation savings via pipe pooling](https://github.com/dotnet/aspnetcore/pull/19356).
- HttpClient HTTP/2 [performance improvements](https://github.com/dotnet/runtime/issues/35184).
