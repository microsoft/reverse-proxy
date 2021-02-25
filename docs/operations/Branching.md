# Branching Tasks

When we are ready to branch our code, we first need to create the branch:

1. In a local clone, run `git checkout main` and `git pull origin main` to make sure you have the latest `main`
2. Run `git checkout -b release/1.0.0-previewX` where `X` is the YARP preview number.
3. Run `git push origin release/1.0.0-previewX` to push the branch to the server.

Update branding in `main`:

1. Edit the file [`eng/Version.props`](../../eng/Version.props)
2. Set `PreReleaseVersionLabel` to `preview.X` (where `X` is the next preview number)
3. Send a PR and merge it ASAP (auto-merge is your friend).

Update the runtimes and SDKs in `global.json` in `main`:

Check that the global.json includes the latest 3.1 runtime versions from [here](https://dotnet.microsoft.com/download/dotnet-core/3.1), and the 5.0 SDK version from [here](https://dotnet.microsoft.com/download/dotnet/5.0).