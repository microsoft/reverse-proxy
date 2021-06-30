---
uid: runtimes
title: Supported Runtimes
---

# YARP Supported Runtimes

YARP 1.0 previews support ASP.NET Core 3.1 and 5.0. You can download the .NET 5 SDK from https://dotnet.microsoft.com/download/dotnet/5.0. See [Releases](https://github.com/microsoft/reverse-proxy/releases) for specific version support.

YARP is taking advantage of ASP.NET Core 5.0 features and optimizations. This does mean that some features may not be available if you're running on ASP.NET Core 3.1.

## Related 5.0 Runtime Improvements

These are related improvements in .NET 5.0 or ASP.NET Core 5.0 that YARP is able to take advantage of:
- Kestrel [reloadable config](https://github.com/dotnet/aspnetcore/issues/19376).
- Kestrel HTTP/2 performance improvements:
  - [HPACK static compression](https://github.com/dotnet/aspnetcore/pull/20058).
  - [HPACK dynamic compression](https://github.com/dotnet/aspnetcore/pull/19521).
  - [Allocation savings via stream pooling](https://github.com/dotnet/aspnetcore/pull/18601).
  - [Allocation savings via pipe pooling](https://github.com/dotnet/aspnetcore/pull/19356).
- HttpClient HTTP/2 [performance improvements](https://github.com/dotnet/runtime/issues/35184).
