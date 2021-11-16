# YARP Roadmap

## Current status

YARP 1.0 has [released](https://github.com/microsoft/reverse-proxy/releases/tag/v1.0.0).

We are planning our next steps, we think the outline will probably look something like:

- 1.0.x - Servicing high impact issues found by customers deploying 1.0 (bug fixes, small features)
- 1.1 - Larger feedback items, non-breaking API changes
- 2.0+ - Major features eg Kubernetes, Service Fabric, HTTP/3

The cadence for these will depend on the issues reported by customers.

## Support

YARP support is provided by the product team - the engineers working on YARP - which is a combination of members from ASP.NET and the .NET library teams. We do not provide 24/7 support or 'carry pagers', but as we have team members located in Prague and Redmond we generally have good timezone coverage. Bugs should be reported in github using the issue templates, and will typically be responded to within 24hrs. If you find a security issue we ask you to [report it via the Microsoft Security Response Center (MSRC)](https://github.com/microsoft/reverse-proxy/blob/main/SECURITY.md).

The support period for YARP releases is as follows:

| Release	| Issue Type | Support period |
| --- | ---| --- |
| 1.x	| Security Bugs, Major behavior defects	| Until next 1.x GA + 6 Months |
| | Minor behavior defects	| Until next 1.x GA |
| Preview | Security Bugs, Major behavior defects | Until next preview |
| | All other | None - may be addressed by next preview |

For example, if 2 months after 1.3 (making up a number) is released, a security issue is found, then we will patch:
- 1.3 - its the latest release
- 1.2 - as it has 4 months of support remaining
- 1.1 - provided that 1.2 was released less than 6 months before

This support schedule is designed to provide a reasonable time period for customers to be able to update to new releases. 

### Building your own copy of a release

YARP is an open source project, so any customers that need fixes faster, or for older releases, are able to build their own copy of YARP. The build environment for YARP is included in the repo and maintained in sync with the source. Each release (GA and Preview) of YARP is tagged, which means that if you need to patch a specific release you can sync to the tag and build. For example you can rebuild v1.0.0 with:

```shell
git clone -b v1.0.0 https://github.com/microsoft/reverse-proxy.git yarp1_0_0
cd yarp1_0_0
restore.cmd 

<make your changes>

build.cmd -c release
```

This will produce the `Yarp.ReverseProxy.dll` into `artifacts/bin/Yarp.ReverseProxy/Release/net6.0`, and peer folder(s) for .NET 5 & Core 3.1. If you need to build a nuget package, that can be done with: 

```shell
pack.cmd -c release
```

The nuget package will be output to `artifacts/packages/release/Shipping`. 
