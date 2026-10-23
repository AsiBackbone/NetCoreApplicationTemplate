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
    '## At a glance'
    '## Related projects'
    '## Design philosophy'
)

# Related-project descriptions are owned by each project and must match the wording on the
# Learning and AsiBackbone homepages (LANDING-PAGE-STANDARD.md, Related Projects contract).
$sharedProjectDescriptions = @(
    '<p>Practical .NET architecture tutorials, labs, runnable samples, and reference patterns for governed execution, secure applications, AI integration, and policy-driven systems, including comparisons that show when these patterns are unnecessary.</p>'
    '<p>A .NET governance and policy-control framework for auditable decision flows: policy evaluation, auditable decision records, acknowledgment workflows, capability-based authorization, host-owned execution boundaries, and ASP.NET Core integration.</p>'
)

# Homepage h2 headings use sentence case; only these proper nouns and acronyms may be capitalized after the first word.
$headingProperNouns = @('API', 'APIs', 'ADR', 'ADRs', 'ASP.NET', 'AsiBackbone', 'Core', 'DocFX', 'EF', 'NCAT', '.NET')

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
) + $sharedProjectDescriptions

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

# Site metadata, homepage layout, and accessibility contract (AsiBackbone/.github#21).
$siteBaseUrl = 'https://asibackbone.github.io/NetCoreApplicationTemplate/'
$socialImageUrl = 'https://asibackbone.github.io/NetCoreApplicationTemplate/images/social-preview.png'
$expectedHomepageTitle = 'Production-Oriented ASP.NET Core Baseline | .NET Core Application Template'
$legacyBrandSpelling = 'ASI Backbone'
$maximumDescriptionLength = 160

$requiredDocFxMetadata = @(
    '"_lang": "en"'
    '"_appName": ".NET Core Application Template"'
    '"_appTitle": ".NET Core Application Template"'
    "`"_canonicalBaseUrl`": `"$siteBaseUrl`""
    "`"_socialImageUrl`": `"$socialImageUrl`""
    '"_socialImageAlt": "'
    '"_structuredDataSiteName": ".NET Core Application Template"'
    '"_structuredDataPublisherName": "AsiBackbone"'
    '"_structuredDataPublisherUrl": "https://github.com/AsiBackbone"'
)

$requiredLayoutContent = @(
    '<html {{#_lang}}lang="{{_lang}}"{{/_lang}}>'
    '{{#_appTitle}} | {{_appTitle}}{{/_appTitle}}</title>'
    '{{#_canonicalUrl}}<link rel="canonical" href="{{_canonicalUrl}}">{{/_canonicalUrl}}'
    '<meta property="og:image" content="{{_socialImageUrl}}">'
    '<script type="application/ld+json">{{{_structuredDataJson}}}</script>'
    '{{^_disableToc}}<meta name="docfx:tocrel" content="{{_tocRel}}">{{/_disableToc}}'
    '<a class="skip-link" href="#main-content">Skip to content</a>'
    '<main class="container-xxl" id="main-content" tabindex="-1">'
)

$requiredBuiltHomepageMetadata = @(
    '<html lang="en">'
    "<title>$expectedHomepageTitle</title>"
    "<link rel=`"canonical`" href=`"$siteBaseUrl`">"
    "<meta property=`"og:url`" content=`"$siteBaseUrl`">"
    "<meta property=`"og:image`" content=`"$socialImageUrl`">"
    '<meta name="description" content="'
    '<script type="application/ld+json">'
    '<a class="skip-link" href="#main-content">Skip to content</a>'
    '<main class="container-xxl" id="main-content" tabindex="-1">'
)

# The homepage uses the full content width: no table-of-contents sidebar or its loader.
$forbiddenBuiltHomepageContent = @(
    'id="tocOffcanvas"'
    'name="docfx:tocrel"'
    $legacyBrandSpelling
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

function Test-HeadingCase {
    param([string]$Content, [System.Collections.Generic.List[string]]$Errors)

    foreach ($heading in [regex]::Matches($Content, '(?m)^## (?<text>.+?)\s*$')) {
        $words = @($heading.Groups['text'].Value -split '\s+' | Select-Object -Skip 1)
        foreach ($word in $words) {
            $bare = $word.Trim('?', '!', ':', ',', '(', ')')
            if ($bare -cmatch '^[A-Z]' -and $headingProperNouns -cnotcontains $bare) {
                $Errors.Add("docs/index.md heading '## $($heading.Groups['text'].Value)' must use sentence case; '$bare' is not an allowed proper noun.")
                break
            }
        }
    }
}

function Test-Homepage {
    param([string]$Content, [System.Collections.Generic.List[string]]$Errors)

    Test-InOrder -Content $Content -Markers $requiredHomepageSections -Name 'docs/index.md' -Errors $Errors
    Test-HeadingCase -Content $Content -Errors $Errors
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

function Test-DocFxMetadata {
    param([string]$Content, [System.Collections.Generic.List[string]]$Errors)

    if ($Content -notmatch '"template"\s*:\s*\[\s*"default"\s*,\s*"modern"\s*,\s*"templates"\s*\]') {
        $Errors.Add('docs/docfx.json must apply the local "templates" override after "default" and "modern".')
    }

    foreach ($requiredText in $requiredDocFxMetadata) {
        if (-not $Content.Contains($requiredText, [StringComparison]::Ordinal)) {
            $Errors.Add("docs/docfx.json is missing required site metadata: $requiredText")
        }
    }

    if ($Content.Contains($legacyBrandSpelling, [StringComparison]::Ordinal)) {
        $Errors.Add("docs/docfx.json uses the legacy '$legacyBrandSpelling' spelling; use 'AsiBackbone' or the canonical project name.")
    }
}

function Test-HomepageFrontMatter {
    param([string]$Content, [System.Collections.Generic.List[string]]$Errors)

    $frontMatter = [regex]::Match($Content, '\A---\r?\n(?<value>[\s\S]*?)\r?\n---')
    if (-not $frontMatter.Success) {
        $Errors.Add('docs/index.md must start with YAML front matter.')
        return
    }

    $yaml = $frontMatter.Groups['value'].Value
    if ($yaml -notmatch '(?m)^title:\s*\S') {
        $Errors.Add('docs/index.md front matter must set a concise homepage title.')
    }

    $description = [regex]::Match($yaml, '(?m)^description:\s*(?<value>.+?)\s*$')
    if (-not $description.Success) {
        $Errors.Add('docs/index.md front matter must set a homepage description.')
    }
    else {
        $descriptionText = $description.Groups['value'].Value.Trim('"', "'")
        if ($descriptionText.Length -gt $maximumDescriptionLength) {
            $Errors.Add("docs/index.md description is $($descriptionText.Length) characters; keep it to $maximumDescriptionLength or fewer.")
        }
    }

    if ($yaml -notmatch '(?m)^_disableToc:\s*true\s*$') {
        $Errors.Add('docs/index.md front matter must set _disableToc: true so the homepage uses the full content width.')
    }
}

function Test-LayoutTemplate {
    param([string]$Content, [string]$PinnedDocFxVersion, [System.Collections.Generic.List[string]]$Errors)

    foreach ($requiredText in $requiredLayoutContent) {
        if (-not $Content.Contains($requiredText, [StringComparison]::Ordinal)) {
            $Errors.Add("docs/templates/layout/_master.tmpl is missing required markup: $requiredText")
        }
    }

    $templateVersion = [regex]::Match($Content, 'DocFX modern template v(?<version>\d+\.\d+\.\d+)')
    if (-not $templateVersion.Success) {
        $Errors.Add('docs/templates/layout/_master.tmpl must name the DocFX modern template version it is based on.')
    }
    elseif ($PinnedDocFxVersion -and $templateVersion.Groups['version'].Value -ne $PinnedDocFxVersion) {
        $Errors.Add("docs/templates/layout/_master.tmpl is based on DocFX v$($templateVersion.Groups['version'].Value) but .config/dotnet-tools.json pins v$PinnedDocFxVersion; re-align the override.")
    }
}

function Get-StructuredDataNodes {
    param([string]$Content)

    $script = [regex]::Match($Content, '<script type="application/ld\+json">(?<value>[\s\S]*?)</script>')
    if (-not $script.Success) {
        return $null
    }

    try {
        return @((ConvertFrom-Json -InputObject $script.Groups['value'].Value).'@graph')
    }
    catch {
        return $null
    }
}

function Test-BuiltHomepage {
    param([string]$Content, [System.Collections.Generic.List[string]]$Errors)

    Test-InOrder -Content $Content -Markers $requiredBuiltSectionIds -Name 'built index.html' -Errors $Errors
    if (-not $Content.Contains($designPhilosophyStatement, [StringComparison]::Ordinal)) {
        $Errors.Add('built index.html does not render the Design Philosophy statement.')
    }

    foreach ($requiredText in $requiredBuiltHomepageMetadata) {
        if (-not $Content.Contains($requiredText, [StringComparison]::Ordinal)) {
            $Errors.Add("built index.html is missing required metadata or markup: $requiredText")
        }
    }

    foreach ($forbiddenText in $forbiddenBuiltHomepageContent) {
        if ($Content.Contains($forbiddenText, [StringComparison]::Ordinal)) {
            $Errors.Add("built index.html must not contain: $forbiddenText")
        }
    }

    $skipLinkIndex = $Content.IndexOf('<a class="skip-link"', [StringComparison]::Ordinal)
    $headerIndex = $Content.IndexOf('<header', [StringComparison]::Ordinal)
    if ($skipLinkIndex -ge 0 -and $headerIndex -ge 0 -and $skipLinkIndex -gt $headerIndex) {
        $Errors.Add('built index.html must render the skip link before the header so it is the first focusable element.')
    }

    if ($Content.Contains('<script type="application/ld+json">', [StringComparison]::Ordinal)) {
        $nodes = Get-StructuredDataNodes $Content
        if ($null -eq $nodes) {
            $Errors.Add('built index.html structured data is not valid JSON with an @graph.')
        }
        else {
            foreach ($type in @('WebSite', 'Organization', 'WebPage')) {
                $node = $nodes | Where-Object { $_.'@type' -eq $type } | Select-Object -First 1
                if ($null -eq $node) {
                    $Errors.Add("built index.html structured data is missing a $type node.")
                }
                elseif ($type -ne 'Organization' -and $node.url -ne $siteBaseUrl) {
                    $Errors.Add("built index.html structured data $type url is '$($node.url)'; expected '$siteBaseUrl'.")
                }
            }
        }
    }
}

function Invoke-SelfTest {
    $validHomepage = ($requiredHomepageSections + $requiredHomepageContent) -join "`n"
    $validToc = ($ecosystemNavigation.GetEnumerator() | ForEach-Object { "    - name: $($_.Key)`n      href: $($_.Value)" }) -join "`n"
    $validFooter = '"_appFooter": "' + ($requiredFooterContent -join ' · ') + '",'
    $validStructuredData = '{"@context":"https://schema.org","@graph":[' +
        "{`"@type`":`"WebSite`",`"@id`":`"$($siteBaseUrl)#website`",`"url`":`"$siteBaseUrl`"}," +
        "{`"@type`":`"Organization`",`"@id`":`"$($siteBaseUrl)#publisher`",`"name`":`"AsiBackbone`",`"url`":`"https://github.com/AsiBackbone`"}," +
        "{`"@type`":`"WebPage`",`"@id`":`"$($siteBaseUrl)#webpage`",`"url`":`"$siteBaseUrl`"}]}"
    $validSite = (@(
        '<!DOCTYPE html>'
        '<html lang="en">'
        "<title>$expectedHomepageTitle</title>"
        '<meta name="description" content="A concise homepage description.">'
        "<link rel=`"canonical`" href=`"$siteBaseUrl`">"
        "<meta property=`"og:url`" content=`"$siteBaseUrl`">"
        "<meta property=`"og:image`" content=`"$socialImageUrl`">"
        "<script type=`"application/ld+json`">$validStructuredData</script>"
        '<a class="skip-link" href="#main-content">Skip to content</a>'
        '<header class="bg-body border-bottom"></header>'
        '<main class="container-xxl" id="main-content" tabindex="-1">'
    ) + $requiredBuiltSectionIds + $designPhilosophyStatement + '</main>') -join "`n"
    $validDocFx = (@('"template": [ "default", "modern", "templates" ],') + $requiredDocFxMetadata) -join ",`n"
    $validFrontMatter = "---`ntitle: `"Production-Oriented ASP.NET Core Baseline`"`ndescription: A concise homepage description.`n_disableAffix: true`n_disableToc: true`n---`n`n# Home"
    $validLayout = (@('{{!This file is based on the DocFX modern template v2.81.0.}}') + $requiredLayoutContent) -join "`n"
    $reorderedHomepage = (@($requiredHomepageSections[0..3]) + '## Design philosophy' + '## Related projects' + $requiredHomepageContent) -join "`n"

    $cases = @(
        @{ Name = 'valid homepage'; Check = { param($e) Test-Homepage $validHomepage $e }; Pass = $true }
        @{ Name = 'missing product fit'; Check = { param($e) Test-Homepage $validHomepage.Replace('## Is the template a fit?', '') $e }; Pass = $false }
        @{ Name = 'philosophy before related projects'; Check = { param($e) Test-Homepage $reorderedHomepage $e }; Pass = $false }
        @{ Name = 'title-case optional heading'; Check = { param($e) Test-Homepage $validHomepage.Replace('## At a glance', '## At a Glance') $e }; Pass = $false }
        @{ Name = 'sentence-case heading with proper nouns'; Check = { param($e) Test-Homepage ($validHomepage + "`n## Architecture decisions and API reference`n## ASP.NET Core and EF Core notes") $e }; Pass = $true }
        @{ Name = 'rewritten Learning description'; Check = { param($e) Test-Homepage $validHomepage.Replace($sharedProjectDescriptions[0], '<p>Education, not a dependency.</p>') $e }; Pass = $false }
        @{ Name = 'missing not-a-dependency statement'; Check = { param($e) Test-Homepage $validHomepage.Replace('Neither related project is a dependency of the template.', '') $e }; Pass = $false }
        @{ Name = 'valid navigation'; Check = { param($e) Test-Navigation $validToc $e }; Pass = $true }
        @{ Name = 'missing Community'; Check = { param($e) Test-Navigation $validToc.Replace('- name: Community', '- name: Discussions') $e }; Pass = $false }
        @{ Name = 'valid footer'; Check = { param($e) Test-Footer $validFooter $e }; Pass = $true }
        @{ Name = 'footer without license'; Check = { param($e) Test-Footer $validFooter.Replace('LICENSE.txt', 'README.md') $e }; Pass = $false }
        @{ Name = 'valid built homepage'; Check = { param($e) Test-BuiltHomepage $validSite $e }; Pass = $true }
        @{ Name = 'built homepage missing section'; Check = { param($e) Test-BuiltHomepage $validSite.Replace('id="related-projects"', '') $e }; Pass = $false }
        @{ Name = 'built homepage without lang'; Check = { param($e) Test-BuiltHomepage $validSite.Replace('<html lang="en">', '<html>') $e }; Pass = $false }
        @{ Name = 'built homepage with duplicated title'; Check = { param($e) Test-BuiltHomepage $validSite.Replace("<title>$expectedHomepageTitle</title>", '<title>.NET Core Application Template Documentation | .NET Core Application Template Documentation</title>') $e }; Pass = $false }
        @{ Name = 'built homepage without canonical'; Check = { param($e) Test-BuiltHomepage $validSite.Replace("<link rel=`"canonical`" href=`"$siteBaseUrl`">", '') $e }; Pass = $false }
        @{ Name = 'built homepage with wrong structured-data url'; Check = { param($e) Test-BuiltHomepage $validSite.Replace("`"url`":`"$siteBaseUrl`"}]}", '"url":"https://example.com/"}]}') $e }; Pass = $false }
        @{ Name = 'built homepage with table of contents'; Check = { param($e) Test-BuiltHomepage $validSite.Replace('<!DOCTYPE html>', '<meta name="docfx:tocrel" content="toc.html">') $e }; Pass = $false }
        @{ Name = 'built homepage with script-only skip link'; Check = { param($e) Test-BuiltHomepage $validSite.Replace('<a class="skip-link" href="#main-content">Skip to content</a>', '') $e }; Pass = $false }
        @{ Name = 'built homepage with legacy brand'; Check = { param($e) Test-BuiltHomepage $validSite.Replace('A concise homepage description.', "$legacyBrandSpelling documentation.") $e }; Pass = $false }
        @{ Name = 'valid DocFX metadata'; Check = { param($e) Test-DocFxMetadata $validDocFx $e }; Pass = $true }
        @{ Name = 'DocFX metadata without lang'; Check = { param($e) Test-DocFxMetadata $validDocFx.Replace('"_lang": "en"', '"_lang": ""') $e }; Pass = $false }
        @{ Name = 'DocFX metadata without template override'; Check = { param($e) Test-DocFxMetadata $validDocFx.Replace(', "templates"', '') $e }; Pass = $false }
        @{ Name = 'DocFX metadata with legacy brand'; Check = { param($e) Test-DocFxMetadata ($validDocFx + "`n`"_appTitle`": `"$legacyBrandSpelling Documentation`"") $e }; Pass = $false }
        @{ Name = 'valid homepage front matter'; Check = { param($e) Test-HomepageFrontMatter $validFrontMatter $e }; Pass = $true }
        @{ Name = 'front matter keeps table of contents'; Check = { param($e) Test-HomepageFrontMatter $validFrontMatter.Replace("_disableToc: true`n", '') $e }; Pass = $false }
        @{ Name = 'front matter without title'; Check = { param($e) Test-HomepageFrontMatter $validFrontMatter.Replace("title: `"Production-Oriented ASP.NET Core Baseline`"`n", '') $e }; Pass = $false }
        @{ Name = 'front matter with long description'; Check = { param($e) Test-HomepageFrontMatter $validFrontMatter.Replace('A concise homepage description.', ('x' * ($maximumDescriptionLength + 1))) $e }; Pass = $false }
        @{ Name = 'valid layout template'; Check = { param($e) Test-LayoutTemplate $validLayout '2.81.0' $e }; Pass = $true }
        @{ Name = 'layout template without skip link'; Check = { param($e) Test-LayoutTemplate $validLayout.Replace('<a class="skip-link" href="#main-content">Skip to content</a>', '') '2.81.0' $e }; Pass = $false }
        @{ Name = 'layout template behind pinned DocFX'; Check = { param($e) Test-LayoutTemplate $validLayout '2.82.0' $e }; Pass = $false }
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
if ($null -ne $homepage) {
    Test-Homepage $homepage $errors
    Test-HomepageFrontMatter $homepage $errors
}

$toc = Read-RequiredFile (Join-Path $repoRoot 'docs/toc.yml') $errors
if ($null -ne $toc) { Test-Navigation $toc $errors }

$docfx = Read-RequiredFile (Join-Path $repoRoot 'docs/docfx.json') $errors
if ($null -ne $docfx) {
    Test-Footer $docfx $errors
    Test-DocFxMetadata $docfx $errors
}

$pinnedDocFxVersion = $null
$toolManifest = Read-RequiredFile (Join-Path $repoRoot '.config/dotnet-tools.json') $errors
if ($null -ne $toolManifest) {
    $pinnedDocFxVersion = [string](ConvertFrom-Json -InputObject $toolManifest).tools.docfx.version
}

$layout = Read-RequiredFile (Join-Path $repoRoot 'docs/templates/layout/_master.tmpl') $errors
if ($null -ne $layout) { Test-LayoutTemplate $layout $pinnedDocFxVersion $errors }

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
