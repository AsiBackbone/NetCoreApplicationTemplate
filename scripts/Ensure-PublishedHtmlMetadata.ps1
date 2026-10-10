[CmdletBinding()]
param(
    [string] $SiteDirectory,
    [string[]] $NoIndexPathPrefix = @(),
    [switch] $SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-PlainText {
    param([Parameter(Mandatory = $true)][string] $Value)

    $withoutTags = [regex]::Replace($Value, '<[^>]+>', ' ')
    $decoded = [System.Net.WebUtility]::HtmlDecode($withoutTags)
    return [regex]::Replace($decoded, '\s+', ' ').Trim()
}

function Limit-Description {
    param(
        [Parameter(Mandatory = $true)][string] $Value,
        [int] $MaximumLength = 160
    )

    if ($Value.Length -le $MaximumLength) {
        return $Value
    }

    $candidate = $Value.Substring(0, $MaximumLength - 1)
    $lastSpace = $candidate.LastIndexOf(' ')
    if ($lastSpace -ge 80) {
        $candidate = $candidate.Substring(0, $lastSpace)
    }
    else {
        $candidate = $candidate.Substring(0, $MaximumLength - 1)
    }

    return $candidate.TrimEnd(' ', ',', ';', ':', '-', [char]0x2013, [char]0x2014) + [char]0x2026
}

function Get-MetaContent {
    param(
        [Parameter(Mandatory = $true)][string] $Html,
        [Parameter(Mandatory = $true)][string] $Selector
    )

    $tag = [regex]::Match(
        $Html,
        ('<meta\b(?=[^>]*\b{0}\s*=\s*["'']description["''])[^>]*>' -f [regex]::Escape($Selector)),
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $tag.Success) {
        return $null
    }

    $content = [regex]::Match(
        $tag.Value,
        'content\s*=\s*["''](?<value>[^"'']*)["'']',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $content.Success) {
        return $null
    }

    return [System.Net.WebUtility]::HtmlDecode($content.Groups['value'].Value).Trim()
}

function Get-PageDescription {
    param([Parameter(Mandatory = $true)][string] $Html)

    $existing = Get-MetaContent -Html $Html -Selector 'name'
    if (-not [string]::IsNullOrWhiteSpace($existing)) {
        return Limit-Description -Value $existing
    }

    $article = [regex]::Match(
        $Html,
        '<article\b[^>]*>(?<value>[\s\S]*?)</article>',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $firstParagraph = $null
    if ($article.Success) {
        $paragraphs = [regex]::Matches(
            $article.Groups['value'].Value,
            '<p\b[^>]*>(?<value>[\s\S]*?)</p>',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        foreach ($paragraph in $paragraphs) {
            $plain = ConvertTo-PlainText -Value $paragraph.Groups['value'].Value
            if (-not [string]::IsNullOrWhiteSpace($plain)) {
                if ($null -eq $firstParagraph) {
                    $firstParagraph = $plain
                }
                if ($plain.Length -ge 60) {
                    return Limit-Description -Value $plain
                }
            }
        }
    }

    if ($null -ne $firstParagraph) {
        return Limit-Description -Value $firstParagraph
    }

    $title = [regex]::Match(
        $Html,
        '<title\b[^>]*>(?<value>[\s\S]*?)</title>',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($title.Success) {
        $plain = ConvertTo-PlainText -Value $title.Groups['value'].Value
        if (-not [string]::IsNullOrWhiteSpace($plain)) {
            return Limit-Description -Value $plain
        }
    }

    if ($Html -match '(?i)<meta\b(?=[^>]*\bhttp-equiv\s*=\s*["'']refresh["''])[^>]*>') {
        return 'Redirecting to the canonical documentation page.'
    }

    throw 'HTML page has no usable description source or title.'
}

function Test-RedirectPage {
    param([Parameter(Mandatory = $true)][string] $Html)

    return $Html -match '(?i)<meta\b(?=[^>]*\bhttp-equiv\s*=\s*["'']refresh["''])[^>]*>'
}

function Add-ToHead {
    param(
        [Parameter(Mandatory = $true)][string] $Html,
        [Parameter(Mandatory = $true)][string] $Markup
    )

    $closingHead = [regex]::Match(
        $Html,
        '</head>',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $closingHead.Success) {
        throw 'HTML page does not contain a closing head element.'
    }

    $newline = if ($Html.Contains([Environment]::NewLine)) { [Environment]::NewLine } else { "\n" }
    return $Html.Insert($closingHead.Index, "      $Markup$newline")
}

function Test-NoIndexPath {
    param(
        [Parameter(Mandatory = $true)][string] $RelativePath,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $Prefixes
    )

    foreach ($prefixValue in $Prefixes) {
        $prefix = $prefixValue.Replace('\', '/').Trim('/')
        if ($prefix.Length -gt 0 -and
            ($RelativePath.Equals($prefix, [System.StringComparison]::OrdinalIgnoreCase) -or
             $RelativePath.StartsWith("$prefix/", [System.StringComparison]::OrdinalIgnoreCase))) {
            return $true
        }
    }

    return $false
}

function Update-PublishedHtmlMetadata {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $NoIndexPrefixes
    )

    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $descriptionAdded = 0
    $noIndexAdded = 0
    $pages = @(Get-ChildItem -LiteralPath $resolvedRoot -Filter '*.html' -File -Recurse)
    if ($pages.Count -eq 0) {
        throw "No HTML pages were found under '$resolvedRoot'."
    }

    $documents = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
    foreach ($page in $pages) {
        $html = [System.IO.File]::ReadAllText($page.FullName)
        if ($html -notmatch '(?i)<head\b') {
            if ($html -match '(?i)<html\b') {
                throw "HTML document has no head element: $($page.FullName)"
            }

            continue
        }
        $documents.Add($page)

        $description = Get-PageDescription -Html $html
        $encoded = [System.Net.WebUtility]::HtmlEncode($description)

        if ($html -notmatch '(?i)<meta\b(?=[^>]*\bname\s*=\s*["'']description["''])[^>]*>') {
            $html = Add-ToHead -Html $html -Markup "<meta name=`"description`" content=`"$encoded`">"
            $descriptionAdded++
        }
        if ($html -notmatch '(?i)<meta\b(?=[^>]*\bproperty\s*=\s*["'']og:description["''])[^>]*>') {
            $html = Add-ToHead -Html $html -Markup "<meta property=`"og:description`" content=`"$encoded`">"
        }
        if ($html -notmatch '(?i)<meta\b(?=[^>]*\bname\s*=\s*["'']twitter:description["''])[^>]*>') {
            $html = Add-ToHead -Html $html -Markup "<meta name=`"twitter:description`" content=`"$encoded`">"
        }

        $relativePath = [System.IO.Path]::GetRelativePath($resolvedRoot, $page.FullName).Replace('\', '/')
        if ((Test-NoIndexPath -RelativePath $relativePath -Prefixes $NoIndexPrefixes) -or
            (Test-RedirectPage -Html $html)) {
            if ($html -notmatch '(?i)<meta\b(?=[^>]*\bname\s*=\s*["'']robots["''])[^>]*\bnoindex\b[^>]*>') {
                $html = Add-ToHead -Html $html -Markup '<meta name="robots" content="noindex">'
                $noIndexAdded++
            }
        }

        [System.IO.File]::WriteAllText(
            $page.FullName,
            $html,
            [System.Text.UTF8Encoding]::new($false))
    }

    if ($documents.Count -eq 0) {
        throw "No complete HTML documents were found under '$resolvedRoot'."
    }

    foreach ($page in $documents) {
        $html = [System.IO.File]::ReadAllText($page.FullName)
        if ($html -notmatch '(?i)<meta\b(?=[^>]*\bname\s*=\s*["'']description["''])[^>]*>') {
            throw "Published page is missing a description: $($page.FullName)"
        }

        $relativePath = [System.IO.Path]::GetRelativePath($resolvedRoot, $page.FullName).Replace('\', '/')
        if (((Test-NoIndexPath -RelativePath $relativePath -Prefixes $NoIndexPrefixes) -or
             (Test-RedirectPage -Html $html)) -and
            $html -notmatch '(?i)<meta\b(?=[^>]*\bname\s*=\s*["'']robots["''])[^>]*\bnoindex\b[^>]*>') {
            throw "Generated report page is missing noindex: $($page.FullName)"
        }
    }

    Write-Host "Validated metadata on $($documents.Count) HTML document(s); added $descriptionAdded description(s) and $noIndexAdded noindex directive(s)."
}

function Invoke-SelfTest {
    $testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("published-html-metadata-{0}" -f [guid]::NewGuid().ToString('N'))
    try {
        $coverageRoot = Join-Path $testRoot 'coverage'
        New-Item -ItemType Directory -Path $coverageRoot -Force | Out-Null
        [System.IO.File]::WriteAllText(
            (Join-Path $testRoot 'missing.html'),
            '<!doctype html><html><head><title>Fallback page | Docs</title></head><body><article><h1>Fallback page</h1><p>Useful <strong>summary</strong> &amp; details.</p></article></body></html>')
        [System.IO.File]::WriteAllText(
            (Join-Path $testRoot 'existing.html'),
            '<!doctype html><html><head><title>Existing</title><meta name="description" content="Keep this description."></head><body></body></html>')
        [System.IO.File]::WriteAllText(
            (Join-Path $testRoot 'long.html'),
            ('<!doctype html><html><head><title>Long</title></head><body><article><p>{0}</p></article></body></html>' -f ('Long description text ' * 20)))
        [System.IO.File]::WriteAllText(
            (Join-Path $testRoot 'status-first.html'),
            '<!doctype html><html><head><title>Decision record</title></head><body><article><p>Accepted</p><p>This substantive context explains the decision and gives searchers enough information to understand the page.</p></article></body></html>')
        [System.IO.File]::WriteAllText(
            (Join-Path $coverageRoot 'index.html'),
            '<!doctype html><html><head><title>Coverage report</title></head><body></body></html>')
        [System.IO.File]::WriteAllText(
            (Join-Path $testRoot 'toc.html'),
            '<nav>Generated navigation fragment</nav>')
        [System.IO.File]::WriteAllText(
            (Join-Path $testRoot 'redirect.html'),
            '<!doctype html><html><head><meta http-equiv="refresh" content="0;URL=''destination.html''"></head><body></body></html>')

        Update-PublishedHtmlMetadata -Root $testRoot -NoIndexPrefixes @('coverage')
        $missing = [System.IO.File]::ReadAllText((Join-Path $testRoot 'missing.html'))
        $existing = [System.IO.File]::ReadAllText((Join-Path $testRoot 'existing.html'))
        $coverage = [System.IO.File]::ReadAllText((Join-Path $coverageRoot 'index.html'))
        $long = [System.IO.File]::ReadAllText((Join-Path $testRoot 'long.html'))
        $statusFirst = [System.IO.File]::ReadAllText((Join-Path $testRoot 'status-first.html'))
        $fragment = [System.IO.File]::ReadAllText((Join-Path $testRoot 'toc.html'))
        $redirect = [System.IO.File]::ReadAllText((Join-Path $testRoot 'redirect.html'))

        if ($missing -notmatch 'name="description" content="Useful summary &amp; details\."' -or
            $missing -notmatch 'property="og:description"' -or
            $missing -notmatch 'name="twitter:description"') {
            throw 'Fallback description metadata was not generated correctly.'
        }
        if (([regex]::Matches($existing, 'name="description"')).Count -ne 1 -or
            $existing -notmatch 'content="Keep this description\."') {
            throw 'An authored description was not preserved.'
        }
        if ($coverage -notmatch 'name="robots" content="noindex"' -or
            $coverage -notmatch 'name="description"') {
            throw 'Generated-report metadata was not applied.'
        }
        $longDescription = Get-MetaContent -Html $long -Selector 'name'
        if ($longDescription.Length -gt 160 -or -not $longDescription.EndsWith([char]0x2026)) {
            throw 'Long descriptions were not truncated to 160 characters or fewer.'
        }
        if ($statusFirst -notmatch 'content="This substantive context explains the decision') {
            throw 'A short status paragraph was selected ahead of substantive content.'
        }
        if ($fragment -ne '<nav>Generated navigation fragment</nav>') {
            throw 'HTML fragments must remain unchanged.'
        }
        if ($redirect -notmatch 'name="robots" content="noindex"' -or
            $redirect -notmatch 'content="Redirecting to the canonical documentation page\."') {
            throw 'Redirect metadata was not applied.'
        }

        $beforeSecondRun = Get-FileHash -LiteralPath (Join-Path $testRoot 'missing.html') -Algorithm SHA256
        Update-PublishedHtmlMetadata -Root $testRoot -NoIndexPrefixes @('coverage')
        $afterSecondRun = Get-FileHash -LiteralPath (Join-Path $testRoot 'missing.html') -Algorithm SHA256
        if ($beforeSecondRun.Hash -ne $afterSecondRun.Hash) {
            throw 'Metadata processing is not idempotent.'
        }

        Write-Host 'Published HTML metadata self-test passed.'
    }
    finally {
        if (Test-Path -LiteralPath $testRoot) {
            Remove-Item -LiteralPath $testRoot -Recurse -Force
        }
    }
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

if ([string]::IsNullOrWhiteSpace($SiteDirectory)) {
    throw 'SiteDirectory is required unless SelfTest is specified.'
}

Update-PublishedHtmlMetadata -Root $SiteDirectory -NoIndexPrefixes $NoIndexPathPrefix
