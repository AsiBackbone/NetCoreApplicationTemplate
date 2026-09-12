[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TagName,

    [Parameter(Mandatory = $true)]
    [string]$EvidenceDirectory,

    [string]$Repository = 'AsiBackbone/NetCoreApplicationTemplate'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedEvidenceDirectory = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$expectedAssetNames = @(Get-ChildItem -LiteralPath $resolvedEvidenceDirectory -File | Sort-Object Name | Select-Object -ExpandProperty Name)

if ($expectedAssetNames.Count -eq 0) {
    throw "No expected release assets were found in '$resolvedEvidenceDirectory'."
}

$releaseJson = & gh release view $TagName --repo $Repository --json assets 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect GitHub release '$TagName' in '$Repository': $releaseJson"
}

$release = $releaseJson | ConvertFrom-Json
$actualAssetNames = @($release.assets | ForEach-Object { [string]$_.name })
$missingAssetNames = @($expectedAssetNames | Where-Object { $_ -notin $actualAssetNames })

if ($missingAssetNames.Count -ne 0) {
    throw "GitHub release '$TagName' is missing required assets: $($missingAssetNames -join ', ')"
}

foreach ($assetName in $expectedAssetNames) {
    $asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    $downloadUrl = [string]$asset.url

    if ([string]::IsNullOrWhiteSpace($downloadUrl)) {
        throw "GitHub release asset '$assetName' does not expose a browser download URL."
    }

    try {
        $response = Invoke-WebRequest -Uri $downloadUrl -Method Head -MaximumRedirection 5 -ErrorAction Stop
        if ($response.StatusCode -ne 200) {
            throw "HTTP $($response.StatusCode)"
        }
    }
    catch {
        throw "GitHub release asset '$assetName' is not anonymously retrievable from '$downloadUrl': $($_.Exception.Message)"
    }
}

Write-Host "Verified $($expectedAssetNames.Count) durable, anonymously retrievable release asset(s) on '$Repository' release '$TagName'."
