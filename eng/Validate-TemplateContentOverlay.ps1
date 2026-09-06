<#
.SYNOPSIS
    Validates that .template.content overlay files stay structurally aligned with the src files they replace.

.DESCRIPTION
    The template packs .template.content over src/ when a project is scaffolded, so an overlay file shadows its src
    counterpart entirely. A setting added to src/ and not to the overlay is therefore absent from every generated
    project, and nothing fails: the overlay is still valid JSON and the missing key falls back to a code default.

    This script compares the JSON key structure of each overlay file with its src counterpart and reports keys present
    in one and not the other. Values are compared too, except at sites where the overlay holds a template token, since
    those are substituted during scaffolding and are expected to differ.

.PARAMETER RepositoryRoot
    Repository root. Defaults to the parent of this script's directory.

.EXAMPLE
    ./eng/Validate-TemplateContentOverlay.ps1
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

# Tokens the template engine substitutes during scaffolding. An overlay value equal to one of these is expected to
# differ from src, so only the key's presence is checked at that site.
$templateTokens = @(
    'TemplateAuthEnabled',
    'TemplateDataProvider',
    'TemplateDataConnectionStringName'
)

function Get-JsonLeaf {
    param(
        [Parameter(Mandatory = $true)][AllowNull()]$Node,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Path,
        [Parameter(Mandatory = $true)][System.Collections.Specialized.OrderedDictionary]$Accumulator
    )

    if ($null -eq $Node) {
        $Accumulator[$Path] = '<null>'
        return
    }

    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Node.PSObject.Properties) {
            $childPath = if ([string]::IsNullOrEmpty($Path)) { $property.Name } else { "$Path.$($property.Name)" }
            Get-JsonLeaf -Node $property.Value -Path $childPath -Accumulator $Accumulator
        }

        return
    }

    if ($Node -is [System.Collections.IEnumerable] -and $Node -isnot [string]) {
        $index = 0
        foreach ($item in $Node) {
            Get-JsonLeaf -Node $item -Path "$Path[$index]" -Accumulator $Accumulator
            $index++
        }

        return
    }

    $Accumulator[$Path] = [string]$Node
}

function Get-JsonLeafMap {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)

    $accumulator = [ordered]@{}
    $content = Get-Content -LiteralPath $LiteralPath -Raw
    $document = $content | ConvertFrom-Json

    Get-JsonLeaf -Node $document -Path '' -Accumulator $accumulator

    return $accumulator
}

$overlayRoot = Join-Path $RepositoryRoot '.template.content'
if (-not (Test-Path -LiteralPath $overlayRoot -PathType Container)) {
    Write-Error "Template content overlay directory was not found at '$overlayRoot'."
    exit 1
}

$failures = [System.Collections.Generic.List[string]]::new()
$comparedCount = 0

$overlayFiles = Get-ChildItem -LiteralPath $overlayRoot -Recurse -File -Filter '*.json'
foreach ($overlayFile in $overlayFiles) {
    $relativePath = $overlayFile.FullName.Substring($overlayRoot.Length).TrimStart('\', '/')
    $sourcePath = Join-Path $RepositoryRoot $relativePath

    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        # Overlay-only files such as nuget.config have no src counterpart and are intentionally scaffold-specific.
        continue
    }

    $comparedCount++

    $overlayLeaves = Get-JsonLeafMap -LiteralPath $overlayFile.FullName
    $sourceLeaves = Get-JsonLeafMap -LiteralPath $sourcePath

    foreach ($key in $sourceLeaves.Keys) {
        if (-not $overlayLeaves.Contains($key)) {
            $failures.Add("$relativePath is missing '$key', which exists in src. Generated projects will not receive it.")
        }
    }

    foreach ($key in $overlayLeaves.Keys) {
        if (-not $sourceLeaves.Contains($key)) {
            $failures.Add("$relativePath declares '$key', which no longer exists in src.")
            continue
        }

        $overlayValue = $overlayLeaves[$key]
        if ($templateTokens -contains $overlayValue) {
            continue
        }

        $sourceValue = $sourceLeaves[$key]
        if ($overlayValue -cne $sourceValue) {
            $failures.Add("$relativePath value for '$key' is '$overlayValue' but src has '$sourceValue'.")
        }
    }
}

if ($comparedCount -eq 0) {
    Write-Error 'No overlay file had a src counterpart to compare against. The overlay layout may have changed.'
    exit 1
}

if ($failures.Count -gt 0) {
    Write-Host "Template content overlay validation failed with $($failures.Count) issue(s):"
    foreach ($failure in $failures) {
        Write-Host "  - $failure"
    }

    Write-Error 'Template content overlay is out of sync with src.'
    exit 1
}

Write-Host "Template content overlay validation passed for $comparedCount file(s)."
