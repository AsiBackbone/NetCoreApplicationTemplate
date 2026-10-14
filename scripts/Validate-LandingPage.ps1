[CmdletBinding()]
param(
    [string]$SiteDirectory,
    [switch]$SelfTest
)

# Validates the documentation homepage against the organization landing-page standard
# (AsiBackbone/.github LANDING-PAGE-STANDARD.md). The checks assert required destinations,
# section order, and shared statements; visual review remains manual.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

$designPhilosophyStatement = 'We favor architectures where consequential operations are explicit, constrained, reviewable, and auditable.'

$requiredHomepageSections = @(
    '<div class="home-hero">'
    '## Start here'
    '## Is the template a fit?'
    '## At a Glance'
    '## Related projects'
    '## Design philosophy'
)

$requiredHomepageContent = @(
    'class="btn btn-primary" href="articles/template-packaging.md"'
    'href="articles/getting-started.md"'
    'href="articles/production-deployment-checklist.md"'
    'When this may be unnecessary'
    'Not every application needs every feature the template provides.'
    'Neither related project is a dependency of the template.'
    '| Supported platform | .NET 10.0 |'
    '| Template short name | `netcoreapp-template` |'
    '| Default authentication | Cookie authentication with authenticated-by-default routed endpoints |'
    '| Default data provider | SQLite for local development |'
    'https://asibackbone.github.io/Learning/architecture/when-aspnet-core-authorization-is-enough.html'
    'https://asibackbone.github.io/Learning/architecture/when-a-simple-application-service-is-enough.html'
    '<article class="project-card" aria-current="page">'
    'https://asibackbone.github.io/Learning/'
    'https://asibackbone.github.io/AsiBackbone/'
    'https://github.com/AsiBackbone/NetCoreApplicationTemplate'
    'https://doi.org/10.5281/zenodo.21938556'
    'https://doi.org/10.5281/zenodo.20546032'
    'https://doi.org/10.5281/zenodo.20373042'
    $designPhilosophyStatement
)

$ecosystemNavigation = [ordered]@{
    'Organization' = 'https://asibackbone.github.io/'
    'Learning' = 'https://asibackbone.github.io/Learning/'
    'Community' = 'https://asibackbone.github.io/#community'
    'Framework' = 'https://asibackbone.github.io/AsiBackbone/'
    'Template' = 'index.md'
    'GitHub' = 'https://github.com/AsiBackbone'
}

$requiredFooterContent = @(
    'Part of <a href=\"https://asibackbone.github.io/\">AsiBackbone</a>'
    'https://github.com/AsiBackbone/NetCoreApplicationTemplate\"'
    'https://github.com/orgs/AsiBackbone/discussions'
    'https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/LICENSE.txt'
    'https://doi.org/10.5281/zenodo.20373042'
)

# DocFX heading anchors, in order, expected in the built homepage.
$requiredBuiltSectionIds = @(
    'id="start-here"'
    'id="is-the-template-a-fit"'
    'id="related-projects"'
    'id="design-philosophy"'
)

function Test-InOrder {
    param(
        [string]$Content,
        [string[]]$Markers,
        [string]$Name,
        [System.Collections.Generic.List[string]]$Errors
    )

    $previousIndex = -1
    $previousMarker = $null
    foreach ($marker in $Markers) {
        $index = $Content.IndexOf($marker, [StringComparison]::Ordinal)
        if ($index -lt 0) {
            $Errors.Add("$Name is missing required section: $marker")
            continue
        }

        if ($index -lt $previousIndex) {
            $Errors.Add("$Name places '$marker' before '$previousMarker'; required order is $($Markers -join ', ').")
        }

        $previousIndex = $index
        $previousMarker = $marker
    }
}

function Test-Homepage {
    param([string]$Content, [System.Collections.Generic.List[string]]$Errors)

    Test-InOrder -Content $Content -Markers $requiredHomepageSections -Name 'docs/index.md' -Errors $Errors
    foreach ($requiredText in $requiredHomepageContent) {
        if (-not $Content.Contains($requiredText, [StringComparison]::Ordinal)) {
            $Errors.Add("docs/index.md is missing required landing-page content: $requiredText")
        }
    }
}

function Test-Navigation {
    param([string]$Content, [System.Collections.Generic.List[string]]$Errors)

    $lines = $Content -split "`r?`n" | ForEach-Object { $_.Trim() }
    foreach ($entry in $ecosystemNavigation.GetEnumerator()) {
        $labelIndex = [Array]::IndexOf($lines, "- name: $($entry.Key)")
        $linked = $labelIndex -ge 0 -and
            $labelIndex + 1 -lt $lines.Count -and
            $lines[$labelIndex + 1] -eq "href: $($entry.Value)"

        if (-not $linked) {
            $Errors.Add("docs/toc.yml must link the '$($entry.Key)' ecosystem destination to '$($entry.Value)'.")
        }
    }
}

function Test-Footer {
    param([string]$Content, [System.Collections.Generic.List[string]]$Errors)

    $footerLine = ($Content -split "`r?`n") | Where-Object { $_.Contains('"_appFooter"', [StringComparison]::Ordinal) } | Select-Object -First 1
    if ($null -eq $footerLine) {
        $Errors.Add('docs/docfx.json must define _appFooter.')
        return
    }

    foreach ($requiredText in $requiredFooterContent) {
        if (-not $footerLine.Contains($requiredText, [StringComparison]::Ordinal)) {
            $Errors.Add("docs/docfx.json _appFooter is missing required footer content: $requiredText")
        }
    }
}

function Test-BuiltHomepage {
    param([string]$Content, [System.Collections.Generic.List[string]]$Errors)

    Test-InOrder -Content $Content -Markers $requiredBuiltSectionIds -Name 'built index.html' -Errors $Errors
    if (-not $Content.Contains($designPhilosophyStatement, [StringComparison]::Ordinal)) {
        $Errors.Add('built index.html does not render the Design Philosophy statement.')
    }
}

function Invoke-SelfTest {
    $validHomepage = ($requiredHomepageSections + $requiredHomepageContent) -join "`n"
    $validToc = ($ecosystemNavigation.GetEnumerator() | ForEach-Object { "    - name: $($_.Key)`n      href: $($_.Value)" }) -join "`n"
    $validFooter = '"_appFooter": "' + ($requiredFooterContent -join ' · ') + '",'
    $validSite = ($requiredBuiltSectionIds + $designPhilosophyStatement) -join "`n"
    $reorderedHomepage = (@($requiredHomepageSections[0..3]) + '## Design philosophy' + '## Related projects' + $requiredHomepageContent) -join "`n"

    $cases = @(
        @{ Name = 'valid homepage'; Check = { param($e) Test-Homepage $validHomepage $e }; Pass = $true }
        @{ Name = 'missing product fit'; Check = { param($e) Test-Homepage $validHomepage.Replace('## Is the template a fit?', '') $e }; Pass = $false }
        @{ Name = 'philosophy before related projects'; Check = { param($e) Test-Homepage $reorderedHomepage $e }; Pass = $false }
        @{ Name = 'missing not-a-dependency statement'; Check = { param($e) Test-Homepage $validHomepage.Replace('Neither related project is a dependency of the template.', '') $e }; Pass = $false }
        @{ Name = 'valid navigation'; Check = { param($e) Test-Navigation $validToc $e }; Pass = $true }
        @{ Name = 'missing Community'; Check = { param($e) Test-Navigation $validToc.Replace('- name: Community', '- name: Discussions') $e }; Pass = $false }
        @{ Name = 'valid footer'; Check = { param($e) Test-Footer $validFooter $e }; Pass = $true }
        @{ Name = 'footer without license'; Check = { param($e) Test-Footer $validFooter.Replace('LICENSE.txt', 'README.md') $e }; Pass = $false }
        @{ Name = 'valid built homepage'; Check = { param($e) Test-BuiltHomepage $validSite $e }; Pass = $true }
        @{ Name = 'built homepage missing section'; Check = { param($e) Test-BuiltHomepage $validSite.Replace('id="related-projects"', '') $e }; Pass = $false }
    )

    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($case in $cases) {
        $caseErrors = [System.Collections.Generic.List[string]]::new()
        & $case.Check $caseErrors
        if (($caseErrors.Count -eq 0) -ne $case.Pass) {
            $expected = if ($case.Pass) { 'success' } else { 'failure' }
            $failures.Add("Self-test '$($case.Name)' expected $expected but got $($caseErrors.Count) error(s).")
        }
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
        exit 1
    }

    Write-Host 'Landing-page validator self-test passed.'
    exit 0
}

function Read-RequiredFile {
    param([string]$Path, [System.Collections.Generic.List[string]]$Errors)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        $Errors.Add("Required file is missing: $Path")
        return $null
    }

    return Get-Content -LiteralPath $Path -Raw
}

if ($SelfTest) {
    Invoke-SelfTest
}

$errors = [System.Collections.Generic.List[string]]::new()

$homepage = Read-RequiredFile (Join-Path $repoRoot 'docs/index.md') $errors
if ($null -ne $homepage) { Test-Homepage $homepage $errors }

$toc = Read-RequiredFile (Join-Path $repoRoot 'docs/toc.yml') $errors
if ($null -ne $toc) { Test-Navigation $toc $errors }

$docfx = Read-RequiredFile (Join-Path $repoRoot 'docs/docfx.json') $errors
if ($null -ne $docfx) { Test-Footer $docfx $errors }

if ($SiteDirectory) {
    $siteRoot = if ([System.IO.Path]::IsPathRooted($SiteDirectory)) { $SiteDirectory } else { Join-Path $repoRoot $SiteDirectory }
    $builtHomepage = Read-RequiredFile (Join-Path $siteRoot 'index.html') $errors
    if ($null -ne $builtHomepage) { Test-BuiltHomepage $builtHomepage $errors }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

if ($SiteDirectory) {
    Write-Host 'Documentation landing-page source and built homepage satisfy the organization landing-page standard.'
}
else {
    Write-Host 'Documentation landing-page source satisfies the organization landing-page standard.'
}
