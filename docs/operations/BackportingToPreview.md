# Backporting changes to a preview branch

Backporting changes is very similar to a regular release. Changes are made on the preview branch, the builds are validated and ultimately released.

- Checkout the preview branch

  `git checkout release/1.0.0-previewX`
- Make and commit any changes
- Push the changes **to your own fork** and submit a PR against the preview branch
- Once the PR is merged, wait for the internal [`microsoft-reverse-proxy-official`](https://dev.azure.com/dnceng/internal/_build?definitionId=809&_a=summary&view=branches) pipeline to produce a build
- Validate the build the same way you would for a regular release [docs](https://github.com/microsoft/reverse-proxy/blob/main/docs/operations/Release.md#validate-the-final-build)
- Package Artifacts from this build can be shared to validate the patch. Optionally, the artifacts from the [public pipeline](https://dev.azure.com/dnceng/public/_build?definitionId=807&view=branches) can be used
- Continue iterating on the preview branch until satisfied with the validation of the change
- [Release the build](https://github.com/microsoft/reverse-proxy/blob/main/docs/operations/Release.md#release-the-build) from the preview branch
- Create a new git tag for the released commit

  **While still on the preview branch:**
  - `git tag v1.0.0-previewX.build.d`
  - `git push upstream --tags`
- Create a new [release](https://github.com/microsoft/reverse-proxy/releases).
