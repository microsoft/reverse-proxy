# Branching Tasks

We are aiming to ship YARP previews aligned with .NET 5, since we are working on changes to the runtime that will improve the experience for YARP. As part of that, our schedule for a preview milestone has three phases:

1. Open development and receiving new builds of .NET 5 - Commits are going to `master` and new builds of .NET 5 are coming regularly
2. Open development, .NET 5 builds frozen - After .NET 5 branches for a release, we will switch `master` to receive builds from that branch. Our development will continue.
3. Branch and prepare for release - Prior to the release date, we'll branch and prepare for release.

## Scheduling

We need to ship versions of our package that depend upon the **released** preview builds. In order to ensure we do that, we try to ship same-date or shortly after .NET 5 previews (see the [.NET Wiki (Internal)](https://aka.ms/dotnet-wiki) for schedule details).

## Prerequisites

* Properly configured `darc` global tool:
    1. Run `.\eng\common\darc-init.ps1` to install the global tool. 
    2. Once installed, run `darc authenticate` and follow the instructions in the file opened in your editor to set up the necessary access tokens
    3. Save and close the file, and `darc` will be ready to go.

## When .NET 5 branches for a preview

When .NET 5 branches for a preview, we need to switch the "channel" from which we are getting builds. See [Dependency Flow Onboarding](https://github.com/dotnet/arcade/blob/master/Documentation/DependencyFlowOnboarding.md) for more information on channels.

To do this, run the following commands:

1. Run `darc get-subscriptions --target-repo microsoft/reverse-proxy --source-repo dotnet/runtime` to get the subscription from `dotnet/runtime` to `microsoft/reverse-proxy`
2. Copy the `Id` value
3. Run `darc update-subscription --id [Id]`. An editor will open.
4. Change the value of the `Channel` field to `.NET 5 Preview X` (where `X` is the preview we are remaining on)
4. Change the value of the `Update Frequency` field to `EveryBuild` (since there should be relatively few builds and we want to be sure we're on the latest).
5. Save and close the file in your editor, `darc` will proceed to make the update.
6. Answer `y` to `Trigger this subscription immediately?` and a PR will be opened to update versions to the latest ones in that channel.
7. Merge the PR as soon as feasible.

## When we are ready to branch

When we are ready to branch our code, we first need to create the branch:

1. In a local clone, run `git checkout master` and `git pull origin master` to make sure you have the latest `master`
2. Run `git checkout -b release/1.0.0-previewX` where `X` is the YARP preview number.
3. Run `git push origin release/1.0.0-previewX` to push the branch to the server.

Then, set up dependency flow so we continue getting new runtime bits if necessary:

1. Run `darc add-subscription`
2. Fill in the template that opens in your editor as follows:
    * `Channel` = `.NET 5 Preview X` where `X` is the .NET 5 preview that matches the YARP preview
    * `Source Repository URL` = `https://github.com/dotnet/runtime`
    * `Target Repository URL` = `https://github.com/microsoft/reverse-proxy`
    * `Target Branch` = `release/1.0.0-previewX` (where `X` is the YARP preview number; this should be the same as the branch you created above)
    * `Update Frequency` = `everyBuild` (Builds will be rare in this channel and we'll want every one)
    * `Merge Policies` is a multiline value, it should look like this:

```
Merge Policies:
- Name: Standard
  Properties: {}
```

3. Save and close the editor window.

Finally, restore the `master` branch to pulling the latest bits from .NET 5:

1. Run `darc get-subscriptions --target-repo microsoft/reverse-proxy --source-repo dotnet/runtime` to get the subscription from `dotnet/runtime` to `microsoft/reverse-proxy`
2. Copy the `Id` value
3. Run `darc update-subscription --id [Id]`. An editor will open.
4. Change the value of the `Channel` field to `.NET 5 Dev`
4. Change the value of the `Update Frequency` field back to `EveryWeek` to reduce PR noise.
5. Save and close the file in your editor, `darc` will proceed to make the update.
6. Answer `y` to `Trigger this subscription immediately?` and a PR will be opened to update versions to the latest ones in that channel.
7. Merge the PR as soon as feasible.

## Releasing Packages

In order to release packages, we first need to identify the final build. Assuming we've branched, the latest build of the Azure Pipeline [`microsoft-reverse-proxy-official`](https://dev.azure.com/dnceng/internal/_build?definitionId=809&_a=summary) should contain the final bits.

Remaining docs TBD.