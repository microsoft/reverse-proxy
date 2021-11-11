# YARP Roadmap

## Current status

YARP 1.0 has [released](/releases/tag/v1.0.0).

We are planning our next steps, we think the outline will probably look something like:

- 1.0.x - Servicing high impact issues found by customers deploying 1.0 (bug fixes, small features)
- 1.1 - Larger feedback items, non-breaking API changes
- 2.0 - Major features eg Kubernetes, Service Fabric, HTTP/3

The cadence for these will depend on the issues reported by customers.

## Support

YARP support is provided by the product team - the engineers working on YARP - which is a combination of members from ASP.NET and the .NET library teams. We do not provide 24/7 support or 'carry pagers', but as we have team members located in Prague and Redmond we generally have good timezone coverage. Bugs should be reported in github using the issue templates, and will typically be responded to within 24hrs. If you find a security issue we ask you to [report it via the Microsoft Security Response Center (MSRC)](https://github.com/microsoft/reverse-proxy/blob/main/SECURITY.md).

The support period for YARP is as follows:

| Release	| Issue Type | Support period |
| --- | ---| --- |
| 1.0	| Security Bugs, Major behavior defects	| 2.0 GA + 6 Months|
| | Minor behavior problems	| Until Next 1.x GA |
| |	Other	| None – will be rolled into the next preview release |
| 1.x |	Security Bugs, Major behavior defects	| 2.0 GA + 6 Months |
| |	Minor behavior problems |	Until Next 1.x GA |

### Building your own copy of a release

YARP is an open source project, so any customers that need fixes faster are able to build their own copy of YARP. The build environment for YARP is included in the repo and maintained in sync with the source. Each release of YARP is tagged, which means that if you need to patch a specific release you can sync to the tag and build. For example you can rebuild Preview 10 with:

```shell
git clone -b v1.0.0-preview10 https://github.com/microsoft/reverse-proxy.git preview10
cd preview10
restore.cmd 

<make your changes>

build.cmd -c release
```

This will produce the `Yarp.ReverseProxy.dll` into `artifacts/bin/Yarp.ReverseProxy/Release/net5.0`, and a peer folder for .NET Core 3.1. If you need to build a nuget package, that can be done with: 

```shell
pack.cmd -c release
```

The nuget package will be output to `artifacts/packages/release/Shipping`.
