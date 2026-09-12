[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvidenceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$TagName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit,

    [Parameter(Mandatory = $true)]
    [string]$ImageReference,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^sha256:[0-9a-fA-F]{64}$')]
    [string]$ImageDigest,

    [switch]$Backfill,

    [string]$BackfillNote,

    [string]$Repository = 'AsiBackbone/NetCoreApplicationTemplate'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

if ($TagName -notmatch '^v(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)$') {
    throw "Tag name '$TagName' must use vMAJOR.MINOR.PATCH with an optional prerelease suffix."
}

$version = $Matches['version']

if ($Repository -notmatch '^[^/]+/[^/]+$') {
    throw "Repository '$Repository' must use owner/name form."
}

if ($Backfill -and [string]::IsNullOrWhiteSpace($BackfillNote)) {
    throw 'BackfillNote is required when Backfill is set.'
}

$resolvedEvidenceDirectory = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$requiredAssets = [ordered]@{
    "netcoreapplicationtemplate-container-$version.spdx.json" = @('application/spdx+json', 'container-sbom')
    "NetCoreApplicationTemplate.$version.spdx.json" = @('application/spdx+json', 'template-package-sbom')
    'template-package-manifest.json' = @('application/json', 'template-package-hash-map')
    "netcoreapplicationtemplate-$version-release-notes.md" = @('text/markdown', 'release-notes')
    'trivy-results.sarif' = @('application/sarif+json', 'container-vulnerability-scan')
}

foreach ($assetName in $requiredAssets.Keys) {
    $assetPath = Join-Path $resolvedEvidenceDirectory $assetName
    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
        throw "Required release evidence file was not found: $assetName"
    }
}

$containerSbom = Get-Content -LiteralPath (Join-Path $resolvedEvidenceDirectory "netcoreapplicationtemplate-container-$version.spdx.json") -Raw | ConvertFrom-Json
if ($containerSbom.spdxVersion -ne 'SPDX-2.3') {
    throw 'Container SBOM is not an SPDX 2.3 JSON document.'
}

$packageSbom = Get-Content -LiteralPath (Join-Path $resolvedEvidenceDirectory "NetCoreApplicationTemplate.$version.spdx.json") -Raw | ConvertFrom-Json
if ($packageSbom.spdxVersion -ne 'SPDX-2.3' -or $packageSbom.packages[0].name -ne 'NetCoreApplicationTemplate' -or $packageSbom.packages[0].versionInfo -ne $version) {
    throw 'Template package SBOM identity does not match the release.'
}

$sarif = Get-Content -LiteralPath (Join-Path $resolvedEvidenceDirectory 'trivy-results.sarif') -Raw | ConvertFrom-Json
if ($sarif.version -ne '2.1.0') {
    throw 'Trivy evidence is not a SARIF 2.1.0 document.'
}

$packageManifestPath = Join-Path $resolvedEvidenceDirectory 'template-package-manifest.json'
$packageManifest = Get-Content -LiteralPath $packageManifestPath -Raw | ConvertFrom-Json

if ($packageManifest.package.version -ne $version) {
    throw "Template package version '$($packageManifest.package.version)' does not match release '$version'."
}

if (-not [string]::IsNullOrWhiteSpace([string]$packageManifest.releaseTag) -and $packageManifest.releaseTag -ne $TagName) {
    throw "Template package manifest tag '$($packageManifest.releaseTag)' does not match '$TagName'."
}

if (-not [string]::IsNullOrWhiteSpace([string]$packageManifest.sourceCommit) -and $packageManifest.sourceCommit -ne $SourceCommit.ToLowerInvariant()) {
    throw 'Template package manifest source commit does not match the release source commit.'
}

$packageSbomPath = Join-Path $resolvedEvidenceDirectory ([string]$packageManifest.package.sbomFile)
$actualPackageSbomHash = Get-Sha256Hex -Path $packageSbomPath
if ($actualPackageSbomHash -ne ([string]$packageManifest.package.sbomSha256).ToLowerInvariant()) {
    throw 'Template package SBOM hash does not match template-package-manifest.json.'
}

$digest = $ImageDigest.ToLowerInvariant()
$digestReference = "$($ImageReference.TrimEnd('@'))@$digest"
$workflowIdentity = "https://github.com/$Repository/.github/workflows/publish-container.yml@refs/tags/$TagName"
$containerManifest = [ordered]@{
    schemaVersion = 1
    repository = $Repository
    releaseTag = $TagName
    sourceCommit = $SourceCommit.ToLowerInvariant()
    imageReference = $ImageReference
    imageDigest = $digest
    digestReference = $digestReference
    signature = [ordered]@{
        type = 'Sigstore Cosign keyless signature'
        verificationCommand = "cosign verify --certificate-identity $workflowIdentity --certificate-oidc-issuer https://token.actions.githubusercontent.com $digestReference"
    }
    provenance = [ordered]@{
        type = 'GitHub artifact attestation'
        verificationCommand = "gh attestation verify oci://$digestReference --repo $Repository"
    }
}

$containerManifestPath = Join-Path $resolvedEvidenceDirectory 'container-image-manifest.json'
$containerManifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $containerManifestPath -Encoding utf8 -NoNewline
$requiredAssets['container-image-manifest.json'] = @('application/json', 'container-identity-and-verification')

$assetRecords = foreach ($assetName in $requiredAssets.Keys) {
    $assetPath = Join-Path $resolvedEvidenceDirectory $assetName
    [ordered]@{
        name = $assetName
        sha256 = Get-Sha256Hex -Path $assetPath
        mediaType = $requiredAssets[$assetName][0]
        purpose = $requiredAssets[$assetName][1]
    }
}

$manifest = [ordered]@{
    schemaVersion = 1
    repository = $Repository
    releaseTag = $TagName
    releaseVersion = $version
    sourceCommit = $SourceCommit.ToLowerInvariant()
    createdUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    backfill = [bool]$Backfill
    backfillNote = $(if ([string]::IsNullOrWhiteSpace($BackfillNote)) { $null } else { $BackfillNote })
    assets = @($assetRecords)
    templatePackage = $packageManifest.package
    containerImage = [ordered]@{
        reference = $ImageReference
        digest = $digest
        digestReference = $digestReference
    }
    trustSignals = [ordered]@{
        nugetPackageSigning = 'deferred; see docs/adr/0005-defer-nuget-package-signing.md'
        containerSigning = 'Cosign keyless signature over the published OCI digest'
        packageProvenance = $(if ($Backfill) { 'not retroactively created; see backfillNote and template-package-manifest.json' } else { 'GitHub attestations for the exact .nupkg and package evidence files' })
        containerProvenance = 'GitHub attestation for the published OCI subject digest'
        sourceLink = 'NuGet package repository/commit metadata; not a signature'
        checksums = 'SHA-256 values in this manifest and component manifests'
        vulnerabilityScan = 'Trivy SARIF for the released container image'
    }
    verification = [ordered]@{
        package = $(if ($Backfill) { $null } else { "gh attestation verify <downloaded-NetCoreApplicationTemplate.nupkg> --repo $Repository" })
        packageSbom = $(if ($Backfill) { $null } else { "gh attestation verify ./NetCoreApplicationTemplate.$version.spdx.json --repo $Repository" })
        containerSignature = $containerManifest.signature.verificationCommand
        containerProvenance = $containerManifest.provenance.verificationCommand
    }
}

$manifestPath = Join-Path $resolvedEvidenceDirectory 'release-evidence-manifest.json'
$manifest | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $manifestPath -Encoding utf8 -NoNewline

Write-Host "Generated release evidence manifest for '$TagName' with $($assetRecords.Count) hashed asset(s)."
