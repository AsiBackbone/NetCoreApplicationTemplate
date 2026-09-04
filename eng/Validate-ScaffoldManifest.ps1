[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ScaffoldRoot,

    [string]$ManifestPath = './eng/scaffold-manifest.default.json',

    [switch]$Generate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-RepositoryPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $repoRoot $Path
}

function Convert-ToManifestPath {
    param([string]$Path)

    return $Path.Replace([System.IO.Path]::DirectorySeparatorChar, '/').Replace([System.IO.Path]::AltDirectorySeparatorChar, '/')
}

function Test-ManifestPattern {
    param(
        [string]$Path,
        [string]$Pattern
    )

    $normalizedPath = Convert-ToManifestPath $Path
    $normalizedPattern = Convert-ToManifestPath $Pattern

    if ($normalizedPattern.EndsWith('/*', [System.StringComparison]::Ordinal)) {
        $prefix = $normalizedPattern.Substring(0, $normalizedPattern.Length - 1)
        return $normalizedPath.StartsWith($prefix, [System.StringComparison]::Ordinal)
    }

    return $normalizedPath -eq $normalizedPattern
}

function Get-ScaffoldFiles {
    param([string]$Root)

    return Get-ChildItem -LiteralPath $Root -Recurse -File -Force |
        Where-Object {
            $_.FullName -notmatch '[/\\](bin|obj)[/\\]' -and
            $_.FullName -notmatch '[/\\]Logs[/\\]'
        } |
        ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($Root, $_.FullName)
            Convert-ToManifestPath $relativePath
        } |
        Sort-Object
}

function Get-ScaffoldDirectories {
    param([string]$Root)

    return Get-ChildItem -LiteralPath $Root -Recurse -Directory -Force |
        Where-Object {
            $_.FullName -notmatch '[/\\](bin|obj)([/\\]|$)' -and
            $_.FullName -notmatch '[/\\]Logs([/\\]|$)'
        } |
        ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($Root, $_.FullName)
            Convert-ToManifestPath $relativePath
        } |
        Sort-Object
}

$resolvedScaffoldRoot = Resolve-RepositoryPath $ScaffoldRoot
$resolvedManifestPath = Resolve-RepositoryPath $ManifestPath

if (-not (Test-Path -LiteralPath $resolvedScaffoldRoot -PathType Container)) {
    Write-Error "Scaffold root was not found: $ScaffoldRoot"
    exit 1
}

$scaffoldFiles = @(Get-ScaffoldFiles $resolvedScaffoldRoot)
$scaffoldDirectories = @(Get-ScaffoldDirectories $resolvedScaffoldRoot)

if ($Generate) {
    $manifest = [ordered]@{
        '$schema' = './scaffold-manifest.schema.json'
        description = 'Approved default consumer scaffold surface for dotnet new netcoreapp-template.'
        templateName = Split-Path -Leaf $resolvedScaffoldRoot
        expectedFiles = $scaffoldFiles
        expectedDirectories = $scaffoldDirectories
        allowedFilePatterns = @()
        forbiddenPaths = @(
            '.github',
            '.template.config',
            '.template.content',
            'artifacts',
            'docs',
            'eng',
            'CHANGELOG.md',
            'CITATION.cff',
            'CODE_OF_CONDUCT.md',
            'CONTRIBUTING.md',
            'NetCoreApplicationTemplate.Template.csproj',
            'PACKAGE-README.md',
            'RELEASE.md',
            'SECURITY.md',
            'scripts'
        )
        forbiddenContentPatterns = @(
            [ordered]@{
                path = 'README.md'
                pattern = 'github.com/AsiBackbone/NetCoreApplicationTemplate/actions'
                description = 'repository maintainer workflow badges'
            },
            [ordered]@{
                path = 'README.md'
                pattern = 'Current release:'
                description = 'repository maintainer release block'
            }
        )
    }

    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resolvedManifestPath -Encoding UTF8
    Write-Host "Generated scaffold manifest at $resolvedManifestPath."
    exit 0
}

if (-not (Test-Path -LiteralPath $resolvedManifestPath -PathType Leaf)) {
    Write-Error "Scaffold manifest was not found: $ManifestPath"
    exit 1
}

$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json
$failures = [System.Collections.Generic.List[string]]::new()

$expectedFiles = @($manifest.expectedFiles | ForEach-Object { Convert-ToManifestPath $_ })
$expectedDirectories = @($manifest.expectedDirectories | ForEach-Object { Convert-ToManifestPath $_ })
$allowedFilePatterns = @($manifest.allowedFilePatterns | ForEach-Object { Convert-ToManifestPath $_ })
$forbiddenPaths = @($manifest.forbiddenPaths | ForEach-Object { Convert-ToManifestPath $_ })

foreach ($path in $expectedFiles) {
    if ($scaffoldFiles -notcontains $path) {
        $failures.Add("Expected scaffolded file was missing: $path")
    }
}

foreach ($path in $expectedDirectories) {
    if ($scaffoldDirectories -notcontains $path) {
        $failures.Add("Expected scaffolded directory was missing: $path")
    }
}

foreach ($path in $scaffoldFiles) {
    $isExpected = $expectedFiles -contains $path
    $isAllowedByPattern = $false

    foreach ($pattern in $allowedFilePatterns) {
        if (Test-ManifestPattern $path $pattern) {
            $isAllowedByPattern = $true
            break
        }
    }

    if (-not $isExpected -and -not $isAllowedByPattern) {
        $failures.Add("Unexpected scaffolded file was generated: $path")
    }
}

foreach ($path in $forbiddenPaths) {
    $fullPath = Join-Path $resolvedScaffoldRoot $path
    if (Test-Path -LiteralPath $fullPath) {
        $failures.Add("Maintainer-only path was scaffolded unexpectedly: $path")
    }
}

foreach ($check in @($manifest.forbiddenContentPatterns)) {
    $relativePath = Convert-ToManifestPath $check.path
    $fullPath = Join-Path $resolvedScaffoldRoot $relativePath

    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        $failures.Add("Forbidden content check file was not found: $relativePath")
        continue
    }

    $content = Get-Content -LiteralPath $fullPath -Raw
    if ($content -match $check.pattern) {
        $failures.Add("Forbidden content '$($check.description)' was found in $relativePath.")
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Scaffold manifest validation failed.'
    foreach ($failure in $failures) {
        Write-Host "::error::$failure"
        Write-Host "- $failure"
    }

    Write-Host ''
    Write-Host 'To intentionally update the manifest, generate a fresh scaffold from the packed template package and run:'
    Write-Host "./eng/Validate-ScaffoldManifest.ps1 -ScaffoldRoot '<generated-project-root>' -Generate"
    exit 1
}

Write-Host "Scaffold manifest validation passed for $resolvedScaffoldRoot."
