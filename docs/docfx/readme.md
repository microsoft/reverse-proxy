# DocFx

This project builds the API and concept docs for this reverse-proxy repo. It uses a library called [DocFx](https://dotnet.github.io/docfx/tutorial/docfx_getting_started.html).

## Bulding the docs

The docs will be built with everything else when running `build.cmd/sh` in the repo root.

## Testing the docs

The build will produce a series of HTML files in the `_site` directory. Many of the links won't work if you try to open the HTML files directly. A tool like [dotnet-serve](https://github.com/natemcmaster/dotnet-serve) can be run in the `_site` directory to properly render the content.

## Publishing the docs

The docs are automatically built and published by a [GitHub Action](https://github.com/microsoft/reverse-proxy/blob/main/.github/workflows/docfx_build.yml) on every push to `release/docs`. The built `_site` directory is pushed to the `gh-pages` branch and served by [https://microsoft.github.io/reverse-proxy/](https://microsoft.github.io/reverse-proxy/). Maintaining a seperate branch for the released docs allows us to choose when to publish them and with what content, and without modifying the build scripts each release.

Doc edits for the current public release should go into that release's branch (e.g. `release/1.0.0-preview3`) and merged forward into `main`. Then `release/docs` should be reset to that release branch's position.

When publishing a new product version (e.g. `release/1.0.0-preview4`) `release/docs` should be reset to that position after the docs have been updated.
