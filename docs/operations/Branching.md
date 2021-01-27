# Branching Tasks

When we are ready to branch our code, we first need to create the branch:

1. In a local clone, run `git checkout master` and `git pull origin master` to make sure you have the latest `master`
2. Run `git checkout -b release/1.0.0-previewX` where `X` is the YARP preview number.
3. Run `git push origin release/1.0.0-previewX` to push the branch to the server.

Finally, update branding in `master`:

1. Edit the file [`eng/Version.props`](../../eng/Version.props)
2. Set `PreReleaseVersionLabel` to `preview.X` (where `X` is the next preview number)
3. Send a PR and merge it ASAP (auto-merge is your friend).
