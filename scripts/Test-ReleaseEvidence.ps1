[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ncat-release-evidence-{0}" -f [guid]::NewGuid().ToString('N'))
$packageSource = Join-Path $testRoot 'package-source'
$packageDirectory = Join-Path $testRoot 'package'
$packageEvidence = Join-Path $testRoot 'package-evidence'
$releaseEvidence = Join-Path $testRoot 'release-evidence'
$sourceCommit = 'a' * 40

try {
    New-Item -ItemType Directory -Path $packageSource, $packageDirectory, $releaseEvidence -Force | Out-Null
    $nuspec = @'
<?xml version="1.0" encoding="utf-8"?>
<package>
  <metadata>
    <id>NetCoreApplicationTemplate</id>
    <version>9.8.7</version>
    <authors>Test Maintainer</authors>
    <description>Release evidence fixture.</description>
    <license type="expression">MIT</license>
    <repository type="git" url="https://github.com/AsiBackbone/NetCoreApplicationTemplate" commit="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" />
    <dependencies />
  </metadata>
</package>
'@
    Set-Content -LiteralPath (Join-Path $packageSource 'NetCoreApplicationTemplate.nuspec') -Value $nuspec -Encoding utf8 -NoNewline

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $packagePath = Join-Path $packageDirectory 'NetCoreApplicationTemplate.9.8.7.nupkg'
    [System.IO.Compression.ZipFile]::CreateFromDirectory($packageSource, $packagePath)

    & (Join-Path $PSScriptRoot 'New-TemplatePackageSbom.ps1') `
        -PackageDirectory $packageDirectory `
        -OutputDirectory $packageEvidence `
        -TagName 'v9.8.7' `
        -SourceCommit $sourceCommit

    Copy-Item -Path (Join-Path $packageEvidence '*') -Destination $releaseEvidence
    Set-Content -LiteralPath (Join-Path $releaseEvidence 'netcoreapplicationtemplate-container-9.8.7.spdx.json') -Value '{"spdxVersion":"SPDX-2.3"}' -Encoding utf8 -NoNewline
    Set-Content -LiteralPath (Join-Path $releaseEvidence 'trivy-results.sarif') -Value '{"version":"2.1.0","runs":[]}' -Encoding utf8 -NoNewline
    Set-Content -LiteralPath (Join-Path $releaseEvidence 'netcoreapplicationtemplate-9.8.7-release-notes.md') -Value '# Test release' -Encoding utf8 -NoNewline

    & (Join-Path $PSScriptRoot 'New-ReleaseEvidenceManifest.ps1') `
        -EvidenceDirectory $releaseEvidence `
        -TagName 'v9.8.7' `
        -SourceCommit $sourceCommit `
        -ImageReference 'ghcr.io/asibackbone/netcoreapplicationtemplate' `
        -ImageDigest ('sha256:' + ('b' * 64))

    $releaseManifestPath = Join-Path $releaseEvidence 'release-evidence-manifest.json'
    $releaseManifest = Get-Content -LiteralPath $releaseManifestPath -Raw | ConvertFrom-Json

    if ($releaseManifest.releaseTag -ne 'v9.8.7' -or $releaseManifest.assets.Count -ne 6) {
        throw 'Release evidence manifest did not preserve the expected identity and six hashed component assets.'
    }

    if ($releaseManifest.assets[0].PSObject.Properties.Name -notcontains 'name') {
        throw 'Release evidence asset records do not expose their file name.'
    }

    if (-not (Test-Path -LiteralPath (Join-Path $releaseEvidence 'container-image-manifest.json') -PathType Leaf)) {
        throw 'Container image manifest was not generated.'
    }

    Set-Content -LiteralPath (Join-Path $releaseEvidence 'NetCoreApplicationTemplate.9.8.7.spdx.json') -Value '{"tampered":true}' -Encoding utf8 -NoNewline
    $tamperRejected = $false

    try {
        & (Join-Path $PSScriptRoot 'New-ReleaseEvidenceManifest.ps1') `
            -EvidenceDirectory $releaseEvidence `
            -TagName 'v9.8.7' `
            -SourceCommit $sourceCommit `
            -ImageReference 'ghcr.io/asibackbone/netcoreapplicationtemplate' `
            -ImageDigest ('sha256:' + ('b' * 64))
    }
    catch {
        $tamperRejected = $true
    }

    if (-not $tamperRejected) {
        throw 'Tampered package SBOM was not rejected.'
    }

    Write-Host 'Release evidence tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
