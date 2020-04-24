# Generating a status report

The `.\eng\Generate-StatusReport.ps1` script can be used to generate a comment for the status report issue.

The easiest way to use it is to run it from within the repository. You'll need a GitHub Personal Access Token (PAT). In this example, I've stored the token in a PowerShell variable `$GitHubToken`.

```
.\eng\Generate-StatusReport.ps1 -GitHubToken $GitHubToken -Clip
```

That will generate output like the following

```
Generating status report...
Start Time: 04/10/2020 00:00:00 UTC (04/09/2020 17:00:00 -07:00 Local Time)
End Time: 04/24/2020 00:00:00 UTC (04/23/2020 17:00:00 -07:00 Local Time)
Repository: microsoft/reverse-proxy
## April 24, 2020

## What are we doing now?

*Fill this in*

### Community contributions

*Below is a list of all the community contributions between April 10, 2020 and April 24, 2020 (UTC time). Thanks to all our contributors for their enthusiasm and support!*

* @isaacabraham [Remove C#-specific reference](https://github.com/microsoft/reverse-proxy/pull/84)
* @Kahbazi [Use HttpMethods.IsXXX in HttpUtilities](https://github.com/microsoft/reverse-proxy/pull/76)
* @Kahbazi [Use is pattern matching with variable](https://github.com/microsoft/reverse-proxy/pull/70)
* @Kahbazi [Remove FluentAssertion](https://github.com/microsoft/reverse-proxy/pull/69)
```

The `-Clip` option means it will also put the status report itself in to your clipboard, for pasting and further editing. Fill in the freeform "What are we doing now?" section and post the comment!

Other options are available, and the script can be used from within any GitHub repo. Use `Get-Help .\eng\Generate-StatusReport.ps1 -Detailed` to get more help, including a list of parameters and their purpose.