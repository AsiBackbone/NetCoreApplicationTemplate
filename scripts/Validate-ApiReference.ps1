[CmdletBinding()]
param(
    [string]$SiteDirectory,
    [switch]$SelfTest
)

# Validates that the generated API reference documents exactly the production namespaces under src/.
# Test projects and ignored release or scaffold artifacts must never reach the API model, and every
# namespace that declares a public type in src/ must remain documented.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

$publicTypePattern = '(?m)^\s*public\s+(?:(?:static|sealed|abstract|partial|readonly|unsafe|ref)\s+)*(?:class|interface|struct|enum|delegate|record(?:\s+(?:class|struct))?)\s'
$namespacePattern = '(?m)^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]?'

function Get-SourceNamespaces {
    param([hashtable]$SourceFiles)

    # Collects the namespaces of files that declare at least one public type.
    $namespaces = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    foreach ($content in $SourceFiles.Values) {
        $namespaceMatch = [regex]::Match($content, $namespacePattern)
        if ($namespaceMatch.Success -and [regex]::IsMatch($content, $publicTypePattern)) {
            [void]$namespaces.Add($namespaceMatch.Groups[1].Value)
        }
    }

    return , $namespaces
}

function Get-TocNamespaces {
    param([string]$TocJson)

    $namespaces = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    $pending = [System.Collections.Generic.Stack[object]]::new()
    $pending.Push((ConvertFrom-Json -InputObject $TocJson))

    while ($pending.Count -gt 0) {
        $node = $pending.Pop()
        if ($node.PSObject.Properties['type'] -and $node.type -eq 'Namespace') {
            [void]$namespaces.Add($node.name)
        }

        if ($node.PSObject.Properties['items']) {
            foreach ($child in $node.items) { $pending.Push($child) }
        }
    }

    return , $namespaces
}

function Test-ApiNamespaces {
    param(
        [System.Collections.Generic.SortedSet[string]]$Generated,
        [System.Collections.Generic.SortedSet[string]]$Expected,
        [System.Collections.Generic.List[string]]$Errors
    )

    if ($Generated.Count -eq 0) {
        $Errors.Add('The generated API reference does not contain any namespaces.')
        return
    }

    foreach ($namespace in $Generated) {
        if (-not $Expected.Contains($namespace)) {
            $Errors.Add("The API reference documents '$namespace', which is not a production namespace under src/. Test projects and artifacts must stay out of DocFX metadata.")
        }
    }

    foreach ($namespace in $Expected) {
        if (-not $Generated.Contains($namespace)) {
            $Errors.Add("The API reference is missing production namespace '$namespace'.")
        }
    }
}

function Invoke-SelfTest {
    $sources = @{
        'Web/Options/Options.cs' = "namespace ProjectTemplate.Web.Options;`n`npublic sealed class AppOptions { }"
        'Web/Services/Service.cs' = "namespace ProjectTemplate.Web.Services`n{`n    public static partial class ServiceExtensions { }`n}"
        'Web/Models/Record.cs' = "namespace ProjectTemplate.Web.Models;`n`npublic sealed record ErrorModel(string Id);"
        'Web/Internal/Helper.cs' = "namespace ProjectTemplate.Web.Internal;`n`ninternal static class Helper { }"
    }
    $expected = Get-SourceNamespaces $sources

    function New-Toc {
        param([string[]]$Names)
        $items = @($Names | ForEach-Object { @{ name = $_; type = 'Namespace'; items = @(@{ name = 'Type'; type = 'Class' }) } })
        return (@{ items = $items } | ConvertTo-Json -Depth 8)
    }

    $production = @('ProjectTemplate.Web.Models', 'ProjectTemplate.Web.Options', 'ProjectTemplate.Web.Services')

    $cases = @(
        @{ Name = 'source parsing excludes internal-only namespaces'; Check = { param($e) if ($expected.Count -ne 3 -or $expected.Contains('ProjectTemplate.Web.Internal')) { $e.Add('unexpected source namespaces') } }; Pass = $true }
        @{ Name = 'production namespaces only'; Check = { param($e) Test-ApiNamespaces (Get-TocNamespaces (New-Toc $production)) $expected $e }; Pass = $true }
        @{ Name = 'test namespace present'; Check = { param($e) Test-ApiNamespaces (Get-TocNamespaces (New-Toc ($production + 'ProjectTemplate.Web.Tests'))) $expected $e }; Pass = $false }
        @{ Name = 'scaffold artifact namespace present'; Check = { param($e) Test-ApiNamespaces (Get-TocNamespaces (New-Toc ($production + 'ContosoSecurityPortal.Web.Options'))) $expected $e }; Pass = $false }
        @{ Name = 'production namespace missing'; Check = { param($e) Test-ApiNamespaces (Get-TocNamespaces (New-Toc $production[0..1])) $expected $e }; Pass = $false }
        @{ Name = 'empty API reference'; Check = { param($e) Test-ApiNamespaces (Get-TocNamespaces '{"items":[]}') $expected $e }; Pass = $false }
    )

    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($case in $cases) {
        $caseErrors = [System.Collections.Generic.List[string]]::new()
        & $case.Check $caseErrors
        if (($caseErrors.Count -eq 0) -ne $case.Pass) {
            $expectedOutcome = if ($case.Pass) { 'success' } else { 'failure' }
            $failures.Add("Self-test '$($case.Name)' expected $expectedOutcome but got $($caseErrors.Count) error(s).")
        }
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
        exit 1
    }

    Write-Host 'API reference validator self-test passed.'
    exit 0
}

if ($SelfTest) {
    Invoke-SelfTest
}

if (-not $SiteDirectory) {
    Write-Error 'Specify -SiteDirectory with the built DocFX site, or -SelfTest.'
    exit 1
}

$errors = [System.Collections.Generic.List[string]]::new()
$siteRoot = if ([System.IO.Path]::IsPathRooted($SiteDirectory)) { $SiteDirectory } else { Join-Path $repoRoot $SiteDirectory }

foreach ($requiredFile in @('api/index.html', 'api/toc.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $siteRoot $requiredFile) -PathType Leaf)) {
        $errors.Add("Generated API reference file is missing: $requiredFile")
    }
}

if ($errors.Count -eq 0) {
    $sourceFiles = @{}
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Filter '*.cs' -File -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        ForEach-Object { $sourceFiles[$_.FullName] = Get-Content -LiteralPath $_.FullName -Raw }

    $expected = Get-SourceNamespaces $sourceFiles
    $generated = Get-TocNamespaces (Get-Content -LiteralPath (Join-Path $siteRoot 'api/toc.json') -Raw)
    Test-ApiNamespaces $generated $expected $errors
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Host "API reference documents exactly the $($generated.Count) production namespaces declared under src/."
