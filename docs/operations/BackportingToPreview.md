# Backporting changes to a preview branch

Backporting changes is very similar to a regular release. Changes are made on a separate branch, the builds are validated and ultimately released from the preview branch.

- Checkout the preview branch

  `git checkout release/1.0.0-previewX`
- Create a new branch for the patch (based on the preview's head)

  `git checkout -b release/1.0.0-previewX-fooPatch`
- Push the branch to the origin repository

  `git push origin release/1.0.0-previewX-fooPatch`
- You should be able to see the created branch [on GitHub](https://github.com/microsoft/reverse-proxy/branches) with the same history as the preview branch
- Make and commit changes **on the patch branch**
- Push the changes **to your own fork** and submit a PR against the patch branch
- Once the PR is merged, wait for the internal [`microsoft-reverse-proxy-official`](https://dev.azure.com/dnceng/internal/_build?definitionId=809&_a=summary&view=branches) pipeline to produce a build
- Validate the build the same way you would for a regular release [docs](https://github.com/microsoft/reverse-proxy/blob/master/docs/operations/Release.md#validate-the-final-build)
- Package Artifacts from this build can be shared to validate the patch. Optionally, the artifacts from the [public pipeline](https://dev.azure.com/dnceng/public/_build?definitionId=807&view=branches) can be used
- Continue iterating on the patch branch until satisfied with the validation of the change
- Open a PR to merge the patch branch into the preview branch
- [Release the build](https://github.com/microsoft/reverse-proxy/blob/master/docs/operations/Release.md#release-the-build) from the preview branch
- Delete the [patch branch](https://github.com/microsoft/reverse-proxy/branches/yours)