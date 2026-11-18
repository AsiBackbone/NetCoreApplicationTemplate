[CmdletBinding()]
param(
    # Checks a built DocFX site, for example ./docs/_site, before it is deployed.
    [string]$SiteDirectory,

    # Checks the deployed site, for example https://asibackbone.github.io/NetCoreApplicationTemplate/.
    [string]$BaseUrl,

    # The commit the deployment is expected to serve. With -BaseUrl, deployment-revision.txt must report it before
    # the pages are compared, so deployment lag is reported separately from a wrong version.
    [string]$ExpectedRevision,

    # Overrides the README Current Release block, which is the source of truth for the latest published release.
    [string]$ExpectedVersion,

    [ValidateRange(1, 60)]
    [int]$MaximumAttempts = 1,

    [ValidateRange(0, 300)]
    [int]$RetryDelaySeconds = 20,

    [switch]$SelfTest
)

# Verifies that the generated documentation homepage and Template Packaging page identify the latest published stable
# release (#718). scripts/Validate-VersionConsistency.ps1 checks the Markdown sources; this script checks the HTML that
# DocFX generated and, after deployment, the HTML that GitHub Pages serves. Findings are reported as deployment lag,
# HTTP failure, missing marker, or version mismatch.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionPattern = '\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?'
$releaseTagBase = 'https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v'

$pages = @(
    [pscustomobject]@{
        Name = 'Documentation homepage'
        Path = 'index.html'
        Markers = @(
            [pscustomobject]@{ Label = 'Current release row'; Pattern = "Current release\s+(?<version>$versionPattern)" }
        )
        RequiresReleaseLinks = $true
    }
    [pscustomobject]@{
        Name = 'Template Packaging'
        Path = 'articles/template-packaging.html'
        Markers = @(
            [pscustomobject]@{ Label = 'Current package version row'; Pattern = "Current package version\s+(?<version>$versionPattern)" }
            [pscustomobject]@{ Label = 'pinned NuGet install command'; Pattern = "dotnet new install NetCoreApplicationTemplate::(?<version>$versionPattern)" }
            [pscustomobject]@{ Label = 'pinned local package example'; Pattern = "NetCoreApplicationTemplate\.(?<version>$versionPattern)\.nupkg" }
        )
        RequiresReleaseLinks = $false
    }
)

function Get-PublishedReleaseVersion {
    param([string]$ReadmeText)

    $match = [regex]::Match(
        $ReadmeText,
        "Current release:\s*__\[Release (?<label>$versionPattern)\]\([^\)]*/releases/tag/v(?<url>$versionPattern)\)__")
    if (-not $match.Success -or $match.Groups['label'].Value -cne $match.Groups['url'].Value) {
        throw 'README.md Current Release block was not found or its label and URL disagree; run ./scripts/Validate-VersionConsistency.ps1.'
    }

    return $match.Groups['label'].Value
}

# Reduces HTML to whitespace-normalized text so that table cells, code spans, and links read as plain prose.
function ConvertTo-PlainText {
    param([string]$Html)

    $text = [regex]::Replace($Html, '(?is)<(script|style)\b[^>]*>.*?</\1\s*>', ' ')
    $text = [regex]::Replace($text, '<[^>]*>', ' ')
    $text = [System.Net.WebUtility]::HtmlDecode($text)
    return [regex]::Replace($text, '\s+', ' ').Trim()
}

function New-Finding {
    param([string]$Category, [string]$Source, [string]$Message)

    return [pscustomobject]@{ Category = $Category; Source = $Source; Message = $Message }
}

# Returns findings for one page's HTML; an empty result means every marker names the expected release.
function Test-PageHtml {
    param($Page, [string]$Html, [string]$Expected, [string]$Source)

    $findings = [System.Collections.Generic.List[object]]::new()
    $text = ConvertTo-PlainText $Html

    foreach ($marker in $Page.Markers) {
        $versions = [System.Collections.Generic.List[string]]::new()
        foreach ($match in [regex]::Matches($text, $marker.Pattern)) {
            $value = $match.Groups['version'].Value
            if (-not $versions.Contains($value)) {
                $versions.Add($value)
            }
        }

        if ($versions.Count -eq 0) {
            $findings.Add((New-Finding 'missing-marker' $Source "$($Page.Name) has no $($marker.Label); expected $Expected."))
        }
        elseif ($versions.Count -gt 1 -or $versions[0] -cne $Expected) {
            $findings.Add((New-Finding 'version-mismatch' $Source "$($Page.Name) $($marker.Label) states $($versions -join ', '); expected $Expected."))
        }
    }

    if ($Page.RequiresReleaseLinks) {
        $releaseUrl = "$releaseTagBase$Expected"
        $notesPage = "release-notes-$($Expected.Replace('.', '')).html"
        if (-not $Html.Contains("href=`"$releaseUrl`"", [StringComparison]::Ordinal)) {
            $findings.Add((New-Finding 'missing-marker' $Source "$($Page.Name) does not link the GitHub Release $releaseUrl."))
        }
        if ($Html -notmatch [regex]::Escape($notesPage)) {
            $findings.Add((New-Finding 'missing-marker' $Source "$($Page.Name) does not link the release notes page $notesPage."))
        }
    }

    return , $findings.ToArray()
}

function Test-SiteDirectory {
    param([string]$Directory, [string]$Expected)

    $findings = [System.Collections.Generic.List[object]]::new()
    foreach ($page in $pages) {
        $path = Join-Path $Directory $page.Path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $findings.Add((New-Finding 'missing-marker' $path "$($page.Name) was not generated at $path."))
            continue
        }

        foreach ($finding in (Test-PageHtml $page (Get-Content -LiteralPath $path -Raw) $Expected $path)) {
            $findings.Add($finding)
        }
    }

    return , $findings.ToArray()
}

# Set by the self-test to answer requests offline; $null in normal runs.
$script:Responder = $null

function Get-Url {
    param([string]$Url)

    if ($script:Responder) {
        return & $script:Responder $Url
    }

    # The query string bypasses cached copies, so a stale cache is not mistaken for the deployment.
    $separator = if ($Url.Contains('?')) { '&' } else { '?' }
    $response = Invoke-WebRequest -Uri "$Url${separator}release-check=$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())" `
        -Headers @{ 'Cache-Control' = 'no-cache' } -TimeoutSec 30 -SkipHttpErrorCheck
    $code = [int]$response.StatusCode
    if ($code -lt 200 -or $code -ge 300) {
        throw "HTTP $code"
    }

    return [string]$response.Content
}

# One pass over the deployed site. Deployment lag and HTTP failures are retryable; marker findings on the expected
# revision are final.
function Test-DeployedSite {
    param([string]$Root, [string]$Expected, [string]$Revision)

    $base = if ($Root.EndsWith('/')) { $Root } else { "$Root/" }

    if ($Revision) {
        try {
            $served = (Get-Url "${base}deployment-revision.txt").Trim()
        }
        catch {
            return [pscustomobject]@{ Retryable = $true; Findings = @(New-Finding 'http-failure' "${base}deployment-revision.txt" "deployment-revision.txt could not be read: $($_.Exception.Message)") }
        }

        if ($served -cne $Revision) {
            return [pscustomobject]@{ Retryable = $true; Findings = @(New-Finding 'deployment-lag' "${base}deployment-revision.txt" "The site still serves revision $served; expected $Revision.") }
        }
    }

    $findings = [System.Collections.Generic.List[object]]::new()
    $httpFailed = $false
    foreach ($page in $pages) {
        $url = "$base$($page.Path)"
        try {
            $html = Get-Url $url
        }
        catch {
            $httpFailed = $true
            $findings.Add((New-Finding 'http-failure' $url "$($page.Name) could not be read: $($_.Exception.Message)"))
            continue
        }

        foreach ($finding in (Test-PageHtml $page $html $Expected $url)) {
            $findings.Add($finding)
        }
    }

    # Without a revision to wait for, a wrong or missing marker may still be deployment lag, so it is retried too.
    $retryable = $httpFailed -or ((-not $Revision) -and $findings.Count -gt 0)
    return [pscustomobject]@{ Retryable = $retryable; Findings = $findings.ToArray() }
}

function Write-Findings {
    param([object[]]$Findings, [string]$Heading)

    foreach ($finding in $Findings) {
        Write-Host "::error::[$($finding.Category)] $($finding.Message) ($($finding.Source))"
    }

    if ($env:GITHUB_STEP_SUMMARY) {
        $lines = @("## $Heading", '', '| Finding | Source | Details |', '| --- | --- | --- |')
        $lines += @($Findings | ForEach-Object { "| $($_.Category) | $($_.Source.Replace('|', '\|')) | $($_.Message.Replace('|', '\|')) |" })
        $lines | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Encoding utf8
    }
}

if ($SelfTest) {
    $failures = [System.Collections.Generic.List[string]]::new()
    $homeHtml = {
        param($v)
        "<table><tr><td>Current release</td>`n<td><a href=`"$releaseTagBase$v`">$v</a> (<a href=`"articles/release-notes-$($v.Replace('.', '')).html`">release notes</a>)</td></tr></table>"
    }
    $packagingHtml = {
        param($v)
        "<table><tr><td>Current package version</td><td><code>$v</code></td></tr></table>" +
        "<pre><code class=`"lang-bash`">dotnet new install NetCoreApplicationTemplate::$v</code></pre>" +
        "<pre><code class=`"lang-bash`">dotnet new install ./artifacts/template-package/NetCoreApplicationTemplate.$v.nupkg</code></pre>" +
        "<pre><code class=`"lang-bash`">dotnet new install NetCoreApplicationTemplate</code></pre>"
    }
    $categories = { param($Findings) (@($Findings | ForEach-Object Category | Sort-Object -Unique) -join ',') }

    if ((Get-PublishedReleaseVersion "Current release: __[Release 2.11.10](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.11.10)__") -cne '2.11.10') {
        $failures.Add('The README Current Release block was not parsed.')
    }
    try {
        $null = Get-PublishedReleaseVersion "Current release: __[Release 2.11.10](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.11.9)__"
        $failures.Add('A README block whose label and URL disagree was accepted.')
    }
    catch {
    }

    $pageCases = @(
        @{ Name = 'current homepage'; Page = 0; Html = (& $homeHtml '2.11.10'); Expect = '' }
        @{ Name = 'stale homepage'; Page = 0; Html = (& $homeHtml '2.11.9'); Expect = 'missing-marker,version-mismatch' }
        @{ Name = 'homepage without release row'; Page = 0; Html = '<p>Latest GitHub release</p>'; Expect = 'missing-marker' }
        @{ Name = 'current packaging page'; Page = 1; Html = (& $packagingHtml '2.11.10'); Expect = '' }
        @{ Name = 'packaging page naming an unpublished candidate'; Page = 1; Html = (& $packagingHtml '2.12.0'); Expect = 'version-mismatch' }
        @{ Name = 'packaging page with mixed versions'; Page = 1; Html = ((& $packagingHtml '2.11.10') + '<code>NetCoreApplicationTemplate.2.11.9.nupkg</code>'); Expect = 'version-mismatch' }
    )
    foreach ($case in $pageCases) {
        $actual = & $categories (Test-PageHtml $pages[$case.Page] $case.Html '2.11.10' 'fixture')
        if ($actual -cne $case.Expect) {
            $failures.Add("Self-test '$($case.Name)' expected [$($case.Expect)] but got [$actual].")
        }
    }

    $siteRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ncat-release-site-{0}" -f [guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path (Join-Path $siteRoot 'articles') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $siteRoot 'index.html') -Value (& $homeHtml '2.11.10') -Encoding utf8
        Set-Content -LiteralPath (Join-Path $siteRoot 'articles/template-packaging.html') -Value (& $packagingHtml '2.11.10') -Encoding utf8
        if ((Test-SiteDirectory $siteRoot '2.11.10').Count -ne 0) {
            $failures.Add('A current built site was rejected.')
        }
        Remove-Item -LiteralPath (Join-Path $siteRoot 'articles/template-packaging.html')
        if ((& $categories (Test-SiteDirectory $siteRoot '2.11.10')) -cne 'missing-marker') {
            $failures.Add('A built site without the Template Packaging page was not reported.')
        }
    }
    finally {
        Remove-Item -LiteralPath $siteRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    # Deployed-site cases run through Get-Url with an offline responder.
    $deployCases = @(
        @{ Name = 'deployed site current'; Revision = 'abc'; Served = 'abc'; Version = '2.11.10'; Down = $false; Expect = ''; Retryable = $false }
        @{ Name = 'deployment lag'; Revision = 'abc'; Served = 'old'; Version = '2.11.10'; Down = $false; Expect = 'deployment-lag'; Retryable = $true }
        @{ Name = 'site unavailable'; Revision = 'abc'; Served = 'abc'; Version = '2.11.10'; Down = $true; Expect = 'http-failure'; Retryable = $true }
        @{ Name = 'expected revision serves a stale version'; Revision = 'abc'; Served = 'abc'; Version = '2.11.9'; Down = $false; Expect = 'missing-marker,version-mismatch'; Retryable = $false }
        @{ Name = 'stale version without a revision is retried'; Revision = ''; Served = 'abc'; Version = '2.11.9'; Down = $false; Expect = 'missing-marker,version-mismatch'; Retryable = $true }
    )
    foreach ($case in $deployCases) {
        $deployCase = $case
        $script:Responder = {
            param($Url)
            if ($Url.EndsWith('deployment-revision.txt')) { return "$($deployCase.Served)`n" }
            if ($deployCase.Down) { throw 'HTTP 503' }
            if ($Url.EndsWith('articles/template-packaging.html')) { return & $packagingHtml $deployCase.Version }
            return & $homeHtml $deployCase.Version
        }
        try {
            $result = Test-DeployedSite 'https://example.test/ncat' '2.11.10' $case.Revision
        }
        finally {
            $script:Responder = $null
        }

        $actual = & $categories $result.Findings
        if ($actual -cne $case.Expect -or [bool]$result.Retryable -ne $case.Retryable) {
            $failures.Add("Self-test '$($case.Name)' expected [$($case.Expect)] retryable=$($case.Retryable) but got [$actual] retryable=$($result.Retryable).")
        }
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Host "::error::$_" }
        exit 1
    }

    Write-Host 'Published release-reference self-test passed.'
    exit 0
}

if ([string]::IsNullOrWhiteSpace($SiteDirectory) -eq [string]::IsNullOrWhiteSpace($BaseUrl)) {
    throw 'Specify exactly one of -SiteDirectory or -BaseUrl.'
}

$expected = if ($ExpectedVersion) { $ExpectedVersion } else { Get-PublishedReleaseVersion (Get-Content -LiteralPath (Join-Path $repoRoot 'README.md') -Raw) }
Write-Host "Expected published release: $expected"

if ($SiteDirectory) {
    # Assigned directly: the function returns its array whole, so @() would wrap it in a second array.
    $findings = Test-SiteDirectory ([System.IO.Path]::GetFullPath($SiteDirectory)) $expected
    if ($findings.Count -gt 0) {
        Write-Findings $findings 'Generated documentation release references'
        exit 1
    }

    Write-Host "Generated homepage and Template Packaging page identify release $expected."
    exit 0
}

$last = $null
for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++) {
    $last = Test-DeployedSite $BaseUrl $expected $ExpectedRevision
    if ($last.Findings.Count -eq 0) {
        Write-Host "$BaseUrl serves release $expected on the homepage and Template Packaging page."
        exit 0
    }

    if (-not $last.Retryable -or $attempt -eq $MaximumAttempts) {
        break
    }

    Write-Host "Attempt ${attempt}: $(@($last.Findings | ForEach-Object Category | Sort-Object -Unique) -join ', '); retrying in $RetryDelaySeconds seconds."
    Start-Sleep -Seconds $RetryDelaySeconds
}

Write-Findings $last.Findings 'Deployed documentation release references'
exit 1
