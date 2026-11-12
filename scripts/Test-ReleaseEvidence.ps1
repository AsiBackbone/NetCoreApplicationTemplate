[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ncat-release-evidence-{0}" -f [guid]::NewGuid().ToString('N'))
$packageSource = Join-Path $testRoot 'package-source'
$packageDirectory = Join-Path $testRoot 'package'
$packageEvidence = Join-Path $testRoot 'package-evidence'
$releaseEvidence = Join-Path $testRoot 'release-evidence'
$unbundledPackage = Join-Path $testRoot 'unbundled-package'
$alteredPackage = Join-Path $testRoot 'altered-package'
$sourceCommit = 'a' * 40

try {
    New-Item -ItemType Directory -Path $packageSource, $packageDirectory, $releaseEvidence, $unbundledPackage, $alteredPackage -Force | Out-Null
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

    # The package directory mirrors the .nupkg and Sigstore bundle downloaded
    # from the published GitHub Release.
    $bundleFileName = 'NetCoreApplicationTemplate.9.8.7.nupkg.sigstore.json'
    $bundlePath = Join-Path $packageDirectory $bundleFileName
    Set-Content -LiteralPath $bundlePath -Value '{"mediaType":"application/vnd.dev.sigstore.bundle.v0.3+json"}' -Encoding utf8 -NoNewline
    $packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $bundleHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Copy-Item -LiteralPath $packagePath -Destination $unbundledPackage
    Copy-Item -LiteralPath $bundlePath -Destination $alteredPackage
    Set-Content -LiteralPath (Join-Path $alteredPackage 'NetCoreApplicationTemplate.9.8.7.nupkg') -Value 'altered package' -Encoding utf8 -NoNewline
    Set-Content -LiteralPath (Join-Path $releaseEvidence 'netcoreapplicationtemplate-container-9.8.7.spdx.json') -Value '{"spdxVersion":"SPDX-2.3"}' -Encoding utf8 -NoNewline
    Set-Content -LiteralPath (Join-Path $releaseEvidence 'trivy-results.sarif') -Value '{"version":"2.1.0","runs":[]}' -Encoding utf8 -NoNewline
    Set-Content -LiteralPath (Join-Path $releaseEvidence 'netcoreapplicationtemplate-9.8.7-release-notes.md') -Value '# Test release' -Encoding utf8 -NoNewline

    & (Join-Path $PSScriptRoot 'New-ReleaseEvidence.ps1') `
        -EvidenceDirectory $releaseEvidence `
        -TagName 'v9.8.7' `
        -SourceCommit $sourceCommit `
        -ImageReference 'ghcr.io/asibackbone/netcoreapplicationtemplate' `
        -ImageDigest ('sha256:' + ('b' * 64)) `
        -TemplatePackageDirectory $packageDirectory

    $releaseManifestPath = Join-Path $releaseEvidence 'release-evidence-manifest.json'
    $releaseManifest = Get-Content -LiteralPath $releaseManifestPath -Raw | ConvertFrom-Json

    if ($releaseManifest.releaseTag -ne 'v9.8.7' -or $releaseManifest.assets.Count -ne 8) {
        throw 'Release evidence manifest did not preserve the expected identity and eight hashed assets.'
    }

    $packageAsset = @($releaseManifest.assets | Where-Object { $_.name -ceq 'NetCoreApplicationTemplate.9.8.7.nupkg' })
    $bundleAsset = @($releaseManifest.assets | Where-Object { $_.name -ceq $bundleFileName })

    if ($packageAsset.Count -ne 1 -or $packageAsset[0].sha256 -ne $packageHash -or $packageAsset[0].purpose -cne 'template-package') {
        throw 'Release evidence manifest did not record the exact template package asset.'
    }

    if ($bundleAsset.Count -ne 1 -or $bundleAsset[0].sha256 -ne $bundleHash -or $bundleAsset[0].purpose -cne 'template-package-sigstore-bundle') {
        throw 'Release evidence manifest did not record the template package Sigstore bundle asset.'
    }

    $packageSignature = $releaseManifest.templatePackageSignature
    $expectedPackageIdentity = 'https://github.com/AsiBackbone/NetCoreApplicationTemplate/.github/workflows/publish-template-package.yml@refs/tags/v9.8.7'

    if ($packageSignature.certificateIdentity -cne $expectedPackageIdentity -or
        $packageSignature.certificateOidcIssuer -cne 'https://token.actions.githubusercontent.com' -or
        $packageSignature.workflowRef -cne 'refs/tags/v9.8.7' -or
        $packageSignature.workflowSha -cne $sourceCommit -or
        $packageSignature.workflowTrigger -cne 'push' -or
        $packageSignature.packageSha256 -ne $packageHash -or
        $packageSignature.bundleSha256 -ne $bundleHash) {
        throw 'Release evidence manifest did not bind the template package Sigstore signature to the exact package, bundle, and signer identity.'
    }

    $containerImageManifest = Get-Content -LiteralPath (Join-Path $releaseEvidence 'container-image-manifest.json') -Raw | ConvertFrom-Json
    $expectedContainerIdentity = 'https://github.com/AsiBackbone/NetCoreApplicationTemplate/.github/workflows/publish-container.yml@refs/tags/v9.8.7'

    if ($containerImageManifest.signature.certificateIdentity -cne $expectedContainerIdentity -or
        $containerImageManifest.signature.verificationCommand -notlike "cosign verify --certificate-identity $expectedContainerIdentity *@sha256:$('b' * 64)") {
        throw 'Container image manifest did not record the exact image signer identity and digest verification command.'
    }

    foreach ($case in @(
        @{ Directory = $unbundledPackage; Pattern = 'Sigstore bundle missing*'; Description = 'Template package without a Sigstore bundle' },
        @{ Directory = $alteredPackage; Pattern = 'Template package digest mismatch*'; Description = 'Release package differing from the built package' }
    )) {
        $rejected = $false

        try {
            & (Join-Path $PSScriptRoot 'New-ReleaseEvidence.ps1') `
                -EvidenceDirectory $releaseEvidence `
                -TagName 'v9.8.7' `
                -SourceCommit $sourceCommit `
                -ImageReference 'ghcr.io/asibackbone/netcoreapplicationtemplate' `
                -ImageDigest ('sha256:' + ('b' * 64)) `
                -TemplatePackageDirectory $case.Directory
        }
        catch {
            $rejected = $_.Exception.Message -like $case.Pattern
        }

        if (-not $rejected) {
            throw "$($case.Description) was not rejected."
        }
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
        & (Join-Path $PSScriptRoot 'New-ReleaseEvidence.ps1') `
            -EvidenceDirectory $releaseEvidence `
            -TagName 'v9.8.7' `
            -SourceCommit $sourceCommit `
            -ImageReference 'ghcr.io/asibackbone/netcoreapplicationtemplate' `
            -ImageDigest ('sha256:' + ('b' * 64)) `
        -TemplatePackageDirectory $packageDirectory
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
