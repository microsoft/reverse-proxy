---
uid: runtimes
title: Supported Runtimes
---

# YARP Supported Runtimes

YARP supports ASP.NET Core 3.1 and 5.0.0 Preview 4 or later. You can download the .NET 5 Preview 4 SDK from https://dotnet.microsoft.com/download/dotnet/5.0.

YARP will be taking advantage of 5.0.0 features and optimizations as they become available. This does mean that some features may not be available if you're running on 3.1.

## Related 5.0.0 Runtime Improvements

These are related improvements in .NET or ASP.NET Core 5.0.0 that YARP is able to take advantage of. We expect to add more as they become available.
- Kestrel [reloadable config](https://github.com/dotnet/aspnetcore/issues/19376).
- Kestrel HTTP/2 performance improvements.
  - [HPACK static compression](https://github.com/dotnet/aspnetcore/pull/20058)
  - [HPACK dynamic compression](https://github.com/dotnet/aspnetcore/pull/19521).
  - [Allocation savings via stream pooling](https://github.com/dotnet/aspnetcore/pull/18601)
  - [Allocation savings via pipe pooling](https://github.com/dotnet/aspnetcore/pull/19356)
- HttpClient HTTP/2 [performance improvements](https://github.com/dotnet/runtime/issues/35184).
