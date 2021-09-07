# YARP Roadmap

## Current status

YARP is currently in preview, and we are regularly releasing preview builds. Packages for each build are available on [Nuget](https://www.nuget.org/packages/Yarp.ReverseProxy/).

The key distinction of preview releases is that we don't guarantee API compatibility between preview builds. We believe that we have made most of the API cleanup changes needed for YARP.

We try to release previews on approximatly a monthly cadence, but will adjust the timing depending to optimize for features that are nearing completion. 

## Release Candidates

The next stage in YARPs progress will be Release Candidates (RC). The key difference between RCs and preview releases is that we need to be considered feature complete before we go to RC. We are very unlikely to add additional functionality between RC's and the numbered release - changes will be primarily for bugs or broken behavior.

The remaining feature work for YARP is tracked with the [YARP 1.0.0 Milestone](https://github.com/microsoft/reverse-proxy/milestone/3). [Contributions](https://github.com/microsoft/reverse-proxy/blob/main/contributing.md) are always welcome.

The bar for API breakages will be much higher - we don't guarantee that we won't make breaking changes, but there has to be a really good reason for the change. 

## Numbered Releases

YARP 1.0 will be declared when:

* We have reached feature complete
* We have had deployment by at least one major partner service

There are some bugs that usually only present themselves when used at large scale and accepting inputs from a wide range of clients - this is why we want a major deployment before we consider YARP to be 1.0. At the time of publishing this roadmap, we have a couple of teams who are close to that point and are rolling out YARP to staging environments.

For YARP 1.0 we will support .NET Core 3.1, .NET 5 and (when released) .NET 6.

## Support

YARP support is provided by the product team - the engineers working on YARP - which is a combination of members from ASP.NET and the BCL networking teams. We do not provide 24/7 support or 'carry pagers', but as we have team members located in Prague and Redmond we generally have good timezone coverage. Bugs should be reported in github using the issue templates, and will typically be responded to within 24hrs. If you find a security issue we ask you to [report it via the Microsoft Security Response Center (MSRC)](https://github.com/microsoft/reverse-proxy/blob/main/SECURITY.md).

If any customers find a blocking issue during rollout to production or in production that was not found during testing and is not fixable by app code, then we will endeavor to patching and re-releasing the most recent preview or RC. If a security bug is found, we will patch and re-release the preview or RC. This will ensure that customers can redeploy the preview with minimal risk of compatibility problems. Other bugs are typically fixed in the next preview or RC and are available from our rolling builds [feed](https://github.com/microsoft/reverse-proxy/blob/main/docs/DailyBuilds.md). 

### Building your own copy of a release

YARP is an open source project, so any customers that need fixes faster are able to build their own copy of YARP. The build environment for YARP is included in the repo and maintained in sync with the source. Each release of YARP is tagged, which means that if you need to patch a specific release you can sync to the tag and build. For example you can rebuild Preview 10 with:

```shell
git clone -b v1.0.0-preview10 https://github.com/microsoft/reverse-proxy.git preview10
cd preview10
restore.cmd 

<make your changes>

build.cmd -c release
```

This will produce the `Yarp.ReverseProxy.dll` into `artifacts/bin/Yarp.ReverseProxy/Release/net5.0`, and a peer folder for .NET 3.1. If you need to build a nuget package, that can be done with: 

```shell
pack.cmd -c release
```

The nuget package will be output to `artifacts/packages/release/Shipping`.
