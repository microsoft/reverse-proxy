<#
.SYNOPSIS
    Generates a template status report for the repository
.PARAMETER GitHubToken
    A GitHub Bearer Token. The easiest way to get one of these is to go to https://github.com/settings/tokens
.PARAMETER Repository
    The repository to generate the status report for, in the form 'owner/repo'. Defaults to whichever repo your current git 'origin' remote is pointing to.
.PARAMETER StartTime
    The start time to scan for merged PRs. Defaults to 14 days prior to EndTime.
.PARAMETER EndTime
    The end time to scan for merged PRs. Defaults to UTC midnight today.
.PARAMETER OutputFile
    An optional file name to output the report to. If not specified, it will be written to standard output.
.PARAMETER Clip
    Set this switch to put the report in the clipboard.
#>

param(
    [Parameter(Mandatory=$true)]
    [string]
    $GitHubToken,

    [Parameter(Mandatory=$false)]
    [string]
    $Repository,

    [Parameter(Mandatory=$false)]
    [DateTimeOffset]
    $StartTime,

    [Parameter(Mandatory=$false)]
    [DateTimeOffset]
    $EndTime,

    [Parameter(Mandatory=$false)]
    [string]
    $GraphQlEndpoint = "https://api.github.com/graphql",

    [Parameter(Mandatory=$false)]
    [string]
    $OutputFile,

    [Parameter(Mandatory=$false)]
    [switch]
    $Clip
)

# Author Associations that will not be called out as "external contributions"
# https://developer.github.com/v4/enum/commentauthorassociation/
$ExcludedAssociations = @("COLLABORATOR", "OWNER", "MEMBER")

# Individual Users that will not be considered "external contributors"
$ExcludedAuthors = @("dotnet-maestro")

if (!$EndTime) {
    $EndTime = New-Object DateTimeOffset ([DateTime]::UtcNow.Date, [TimeSpan]::Zero)
}

if (!$StartTime) {
    $StartTime = $EndTime.AddDays(-14)
}

if (!$Repository) {
    try {
        $RemoteUrl = git remote get-url origin
    } catch {
        throw "Failed to detect Git Repository. Make sure you're running this from within a clone of a GitHub repository."
    }

    if ($RemoteUrl -match "^(https?://|ssh://)?(git@)?(www\.)?github.com/(?<owner>[^/]+)/(?<name>[^/]+)$") {
        $Repository = "$($matches["owner"])/$($matches["name"])"
    }
    else {
        throw "Could not detect GitHub repo identity from remote URL: $RemoteUrl"
    }
}

Write-Host -ForegroundColor Green "Generating status report..."
Write-Host -ForegroundColor Magenta "Start Time: $($StartTime.UtcDateTime) UTC ($($StartTime.ToLocalTime()) Local Time)"
Write-Host -ForegroundColor Magenta "End Time: $($EndTime.UtcDateTime) UTC ($($EndTime.ToLocalTime()) Local Time)"
Write-Host -ForegroundColor Magenta "Repository: $Repository"

$SearchQuery = "is:pr is:merged merged:$($StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))..$($EndTime.ToString("yyyy-MM-ddTHH:mm:ssZ")) repo:$Repository";

$GraphQlQuery = @"
query(`$Query: String!) { 
  search(type:ISSUE, query:`$Query, first: 100) {
    pageInfo{
      hasNextPage,
      endCursor
    },
    issueCount,
    nodes {
      ... on PullRequest {
        author{ login, avatarUrl }
        authorAssociation,
        number,
        title,
        url
      }
    }
  }
}
"@

$Parameters = @{
    "Query" = $SearchQuery
}

Write-Debug "Executing GraphQL Query:"
Write-Debug $GraphQlQuery
Write-Debug "Parameters:"
$Parameters.Keys | ForEach-Object { Write-Debug "* $_ = $($Parameters[$_])" }

$RequestBody = @{
    "query" = $GraphQlQuery;
    "variables" = $Parameters;
} | ConvertTo-Json
$Headers = @{
    "Authorization" = "Bearer $GitHubToken";
    "Content-Type" = "application/json";
}

$Response = Invoke-RestMethod -Method "POST" -Headers $Headers -Body $RequestBody $GraphQlEndpoint

$Cursor = $null;
$ContributorPRs = @();
do {
    if ($Cursor) {
        throw "Multi-page results not yet supported!"
    }

    $ContributorPRs += @($Response.data.search.nodes | Where-Object { 
        ($ExcludedAssociations -notcontains $_.authorAssociation) -and
        ($ExcludedAuthors -notcontains $_.author.login)
    }) 

    $Cursor = $Response.data.search.pageInfo.endCursor
} while($Response.data.search.pageInfo.hasNextPage)

# Generate the markdown

$ContributorPRMarkdown = @($ContributorPRs | ForEach-Object {
    "* @$($_.author.login) [$($_.title)]($($_.url))"
})

$Markdown = @"
## $([DateTime]::Now.ToString("MMMM dd, yyyy"))

## What are we doing now?

*Fill this in*

### Community contributions

*Below is a list of all the community contributions between $($StartTime.ToString("MMMM dd, yyyy")) and $($EndTime.ToString("MMMM dd, yyyy")) (UTC time). Thanks to all our contributors for their enthusiasm and support!*

$([string]::Join([Environment]::NewLine, $ContributorPRMarkdown))
"@

if ($Clip) {
    if ($PSVersionTable.Platform -eq "Win32NT") {
        $Markdown | clip.exe
    } else {
        Write-Warning "The -Clip option is currently only supported on Windows."
    }
}

if ($OutputFile) {
    $Markdown | Out-File $OutputFile -Encoding UTF8
} else {
    $Markdown
}