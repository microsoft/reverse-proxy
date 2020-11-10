# Releasing YARP

This document provides a guide on how to release a preview of YARP.

## Ensure there's a release branch created.

See [Branching](Branching.md).

## Identify the Final Build

First, identify the final build of the [`microsoft-reverse-proxy-official` Azure Pipeline](https://dev.azure.com/dnceng/internal/_build?definitionId=809&_a=summary) (on dnceng/internal). The final build will be the latest successful build **in the relevant `release/x` branch**. Use the "Branches" tab on Azure DevOps to help identify it. If the branch hasn't been mirrored yet (see [code-mirror pipeline](https://dev.azure.com/dnceng/internal/_build?definitionId=16&keywordFilter=microsoft%20reverse-proxy)) and there are no outstanding changesets in the branch, the build of the correspoding commit from the master branch can be used.

Once you've identified that build, click in to the build details.



## Validate the Final Build

At this point, you can perform any validation that makes sense. At a minimum, we should validate that the sample can run with the candidate packages. You can download the final build using the "Artifacts" which can be accessed under "Related" in the header:

![image](https://user-images.githubusercontent.com/7574/81447119-e4204800-9130-11ea-8952-9a0f9831f678.png)

The packages can be accessed from the `PackageArtifacts` artifact:

![image](https://user-images.githubusercontent.com/7574/81447168-fef2bc80-9130-11ea-8aa0-5a83d90efa0d.png)

### Consume .nupkg
- Visual Studio: Place it in a local folder and add that folder as a nuget feed in Visual Studio.
- Command Line: `dotnet nuget add source <directory> -n local`

Walk through the [Getting Started](https://microsoft.github.io/reverse-proxy/articles/getting_started.html) instructions and update them in the release branch as needed.

Also validate any major new scenarios this release and their associated docs.

## Release the build

Once validation has been completed, it's time to release. Go back to the Final Build in Azure DevOps. It's probably good to triple-check the version numbers of the packages in the artifacts against whatever validation was done at this point.

Select "Release" from the triple-dot menu in the top-right of the build details page:

![image](https://user-images.githubusercontent.com/7574/81447354-55f89180-9131-11ea-84bc-0138d7b211e4.png)

Verify the Release Pipeline selected is `microsoft-reverse-proxy-release`, that the `NuGet.org` stage has a blue border (meaning it will automatically deploy) and that the build number under Artifacts matches the build number of the final build (it will not match the package version). The defaults selected by Azure Pipelines should configure everything correctly but it's a good idea to double check.

![image](https://user-images.githubusercontent.com/7574/81447433-76c0e700-9131-11ea-9e8b-e4984ab7c31a.png)

Click "Create" to start the release! Unless you're a release approver, you're done here!

## Approve the release

The Azure Pipeline will send an email to all the release approvers asking one of them to approve the release:

![image](https://user-images.githubusercontent.com/7574/81447680-f3ec5c00-9131-11ea-821c-37dbe467faee.png)

Click "View Approval", or navigate to the release directly in Azure DevOps. You'll see that the stage is "Pending Approval"

![image](https://user-images.githubusercontent.com/7574/81447753-10889400-9132-11ea-9dd2-26b2f6bc8970.png)

Click "Approve" to bring up the Approval dialog. Enter a comment such as "release for preview X" and click "Approve" to finalize the release. **After pressing "Approve", packages will be published automatically**. It *is* possible to cancel the pipeline, but it might be too late. See "Troubleshooting" below.

![image](https://user-images.githubusercontent.com/7574/81447898-4d548b00-9132-11ea-89df-b4624a5e037d.png)

Click "Reject" to cancel the release.

The packages will be pushed and when the "NuGet.org" stage turns green, the packages are published!

*Note: NuGet publishing is quick, but there is a background indexing process that can mean it will take up to several hours for packages to become available*

## Tag the commit

Create and push a git tag for the commit associated with the final build (not necessarily the HEAD of the current release branch). See prior tags for the preferred format. Use a lightweight tag, not annotated, e.g.: `git tag v1.0.0-preview6`.

## Draft release notes

Create a draft release at https://github.com/microsoft/reverse-proxy/releases using the new tag. See prior releases for the recommended content and format.

## Publish the docs

Reset the `release/docs` branch to the head of the current preview branch to publish the latest docs. See [docs](../docfx/readme.md).

## Publish the release notes

Publish the draft release notes. These should be referencing the latest docs, packages, etc..

## Close the old milestone

It should be empty now. If it's not, move the outstanding issues to the next one.

## Announce on social media

David Fowler has a lot of twitter followers interested in YARP. Tweet a link to the release notes and let him retweet it.

## Complete any outstanding branching tasks.

See [Branching](Branching.md).
- Make sure the versions in master have been updated for the next milestone.
- Update the runtime dependency flow with DARC
- Update the SDK

## Troubleshooting

### Authentication Errors

The pipeline is authenticated via a "Service Connection" in Azure DevOps. If there are authentication errors, it's likely the API key is invalid. Follow these steps to update the API key:

1. Go to NuGet.org, log in with an account associated with an `@microsoft.com` address that has access to the `dotnetframework` organization.
2. Generate a new API key with "dotnetframework" as the Package Owner and "*" as the Package "glob".
3. Copy that API key and fill it in to the "nuget.org (dotnetframework organization)" [Service Connection](https://dev.azure.com/dnceng/internal/_settings/adminservices) in Azure DevOps.

In the event you don't have access, contact `dnceng@microsoft.com` for guidance.

### Accidental Overpublish

In the event you overpublish (publish a package that wasn't intended to be released), you should "unlist" the package on NuGet. It is not possible to delete packages on NuGet.org, by design, but you can remove them from search results. Users who reference the version you published directly will still be able to download it, but it won't show up in search queries or non-version-specific actions (like installing the latest).

1. Go to NuGet.org, log in with an account associated with an `@microsoft.com` address that has access to the `dotnetframework` organization.
2. Go to the package page and click "Manage package" on the "Info" sidebar on the right.
3. Expand "Listing"
4. Select the version that was accidentally published
5. Uncheck the "List in search results" box
6. Click "Save"

### Package was rejected

NuGet.org has special criteria for all packages starting `Microsoft.`. If the package is rejected for not meeting one of those criteria, go to the [NuGet @ Microsoft](http://aka.ms/nuget) page for more information on required criteria and guidance for how to configure the package appropriately.
