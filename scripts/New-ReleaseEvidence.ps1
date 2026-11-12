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

    # Directory holding the exact .nupkg and its <package-file>.sigstore.json
    # bundle as downloaded from the published GitHub Release. Required unless
    # -Backfill is set.
    [string]$TemplatePackageDirectory,

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

$oidcIssuer = 'https://token.actions.githubusercontent.com'
$workflowRef = "refs/tags/$TagName"
$normalizedSourceCommit = $SourceCommit.ToLowerInvariant()

function New-SignerIdentity {
    param([Parameter(Mandatory = $true)][string]$WorkflowFile)

    return [ordered]@{
        certificateIdentity = "https://github.com/$Repository/.github/workflows/$($WorkflowFile)@$workflowRef"
        certificateOidcIssuer = $oidcIssuer
        workflowRepository = $Repository
        workflowRef = $workflowRef
        workflowSha = $normalizedSourceCommit
        workflowTrigger = 'push'
    }
}

function Get-IdentityFlags {
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$Identity)

    return (
        "--certificate-identity $($Identity.certificateIdentity)" +
        " --certificate-oidc-issuer $($Identity.certificateOidcIssuer)" +
        " --certificate-github-workflow-repository $($Identity.workflowRepository)" +
        " --certificate-github-workflow-ref $($Identity.workflowRef)" +
        " --certificate-github-workflow-sha $($Identity.workflowSha)" +
        " --certificate-github-workflow-trigger $($Identity.workflowTrigger)")
}

# The exact template package and its Sigstore bundle, downloaded from the
# published GitHub Release, must be the bytes recorded by the package build.
$templatePackageAssets = [ordered]@{}
$packageSignature = $null

if (-not $Backfill) {
    if ([string]::IsNullOrWhiteSpace($TemplatePackageDirectory)) {
        throw 'TemplatePackageDirectory is required unless Backfill is set.'
    }

    $resolvedTemplatePackageDirectory = (Resolve-Path -LiteralPath $TemplatePackageDirectory).Path
    $packageFileName = [string]$packageManifest.package.packageFile
    $bundleFileName = "$packageFileName.sigstore.json"
    $releasePackagePath = Join-Path $resolvedTemplatePackageDirectory $packageFileName
    $releaseBundlePath = Join-Path $resolvedTemplatePackageDirectory $bundleFileName

    if (-not (Test-Path -LiteralPath $releasePackagePath -PathType Leaf)) {
        throw "Template package '$packageFileName' was not found in '$resolvedTemplatePackageDirectory'."
    }

    if (-not (Test-Path -LiteralPath $releaseBundlePath -PathType Leaf)) {
        throw "Sigstore bundle missing for template package '$packageFileName'."
    }

    $releasePackageHash = Get-Sha256Hex -Path $releasePackagePath
    if ($releasePackageHash -ne ([string]$packageManifest.package.packageSha256).ToLowerInvariant()) {
        throw "Template package digest mismatch. template-package-manifest.json records $($packageManifest.package.packageSha256) but the release package is $releasePackageHash."
    }

    try {
        $bundleDocument = Get-Content -LiteralPath $releaseBundlePath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Sigstore bundle '$bundleFileName' is not valid JSON: $($_.Exception.Message)"
    }

    $bundleMediaTypeProperty = $bundleDocument.PSObject.Properties['mediaType']
    if ($null -eq $bundleMediaTypeProperty -or -not ([string]$bundleMediaTypeProperty.Value).StartsWith('application/vnd.dev.sigstore.bundle', [System.StringComparison]::Ordinal)) {
        throw "Sigstore bundle '$bundleFileName' does not declare a Sigstore protobuf bundle media type."
    }

    $templatePackageAssets[$packageFileName] = @('application/zip', 'template-package', $releasePackagePath)
    $templatePackageAssets[$bundleFileName] = @([string]$bundleMediaTypeProperty.Value, 'template-package-sigstore-bundle', $releaseBundlePath)

    $packageIdentity = New-SignerIdentity -WorkflowFile 'publish-template-package.yml'
    $packageSignature = [ordered]@{
        type = 'Sigstore Cosign keyless blob signature (Sigstore protobuf bundle)'
        subject = 'The exact .nupkg build package attached to this GitHub Release'
        packageFile = $packageFileName
        packageSha256 = $releasePackageHash
        bundleFile = $bundleFileName
        bundleSha256 = Get-Sha256Hex -Path $releaseBundlePath
        certificateIdentity = $packageIdentity.certificateIdentity
        certificateOidcIssuer = $packageIdentity.certificateOidcIssuer
        workflowRepository = $packageIdentity.workflowRepository
        workflowRef = $packageIdentity.workflowRef
        workflowSha = $packageIdentity.workflowSha
        workflowTrigger = $packageIdentity.workflowTrigger
        verificationCommand = "cosign verify-blob --bundle $bundleFileName $(Get-IdentityFlags -Identity $packageIdentity) $packageFileName"
        boundaries = @(
            'Covers only the .nupkg bytes attached to this GitHub Release. NuGet.org repository-signs packages on ingestion, so the NuGet.org copy has a different digest and is not covered by this bundle.',
            'Is not a NuGet author signature; NuGet author signing remains deferred under ADR-0005.',
            'Does not cover the OCI image, which has its own digest signature.'
        )
    }
}

$digest = $ImageDigest.ToLowerInvariant()
$digestReference = "$($ImageReference.TrimEnd('@'))@$digest"
$containerIdentity = New-SignerIdentity -WorkflowFile 'publish-container.yml'
$containerManifest = [ordered]@{
    schemaVersion = 2
    repository = $Repository
    releaseTag = $TagName
    sourceCommit = $normalizedSourceCommit
    imageReference = $ImageReference
    imageDigest = $digest
    digestReference = $digestReference
    signature = [ordered]@{
        type = 'Sigstore Cosign keyless signature'
        subject = 'The published OCI image digest; mutable tags are not signed subjects'
        certificateIdentity = $containerIdentity.certificateIdentity
        certificateOidcIssuer = $containerIdentity.certificateOidcIssuer
        workflowRepository = $containerIdentity.workflowRepository
        workflowRef = $containerIdentity.workflowRef
        workflowSha = $containerIdentity.workflowSha
        workflowTrigger = $containerIdentity.workflowTrigger
        verificationCommand = "cosign verify $(Get-IdentityFlags -Identity $containerIdentity) $digestReference"
    }
    provenance = [ordered]@{
        type = 'GitHub artifact attestation'
        verificationCommand = "gh attestation verify oci://$digestReference --repo $Repository"
    }
}

$containerManifestPath = Join-Path $resolvedEvidenceDirectory 'container-image-manifest.json'
$containerManifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $containerManifestPath -Encoding utf8 -NoNewline
$requiredAssets['container-image-manifest.json'] = @('application/json', 'container-identity-and-verification')

$assetRecords = @(
    foreach ($assetName in $requiredAssets.Keys) {
        $assetPath = Join-Path $resolvedEvidenceDirectory $assetName
        [ordered]@{
            name = $assetName
            sha256 = Get-Sha256Hex -Path $assetPath
            mediaType = $requiredAssets[$assetName][0]
            purpose = $requiredAssets[$assetName][1]
        }
    }

    foreach ($assetName in $templatePackageAssets.Keys) {
        [ordered]@{
            name = $assetName
            sha256 = Get-Sha256Hex -Path $templatePackageAssets[$assetName][2]
            mediaType = $templatePackageAssets[$assetName][0]
            purpose = $templatePackageAssets[$assetName][1]
        }
    }
)

$manifest = [ordered]@{
    schemaVersion = 2
    repository = $Repository
    releaseTag = $TagName
    releaseVersion = $version
    sourceCommit = $SourceCommit.ToLowerInvariant()
    createdUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    backfill = [bool]$Backfill
    backfillNote = $(if ([string]::IsNullOrWhiteSpace($BackfillNote)) { $null } else { $BackfillNote })
    assets = @($assetRecords)
    templatePackage = $packageManifest.package
    templatePackageSignature = $packageSignature
    containerImage = [ordered]@{
        reference = $ImageReference
        digest = $digest
        digestReference = $digestReference
    }
    trustSignals = [ordered]@{
        nugetPackageSigning = 'NuGet author signing deferred; see docs/adr/0005-defer-nuget-package-signing.md'
        packageSigning = $(if ($Backfill) { 'not retroactively created' } else { 'Cosign keyless blob signature over the exact GitHub Release .nupkg; not a NuGet author signature and not the NuGet.org repository-signed copy' })
        containerSigning = 'Cosign keyless signature over the published OCI digest'
        packageProvenance = $(if ($Backfill) { 'not retroactively created; see backfillNote and template-package-manifest.json' } else { 'GitHub attestations for the exact .nupkg and package evidence files' })
        containerProvenance = 'GitHub attestation for the published OCI subject digest'
        sourceLink = 'NuGet package repository/commit metadata; not a signature'
        checksums = 'SHA-256 values in this manifest and component manifests'
        vulnerabilityScan = 'Trivy SARIF for the released container image'
    }
    verification = [ordered]@{
        package = $(if ($Backfill) { $null } else { "gh attestation verify <downloaded-NetCoreApplicationTemplate.nupkg> --repo $Repository" })
        packageSignature = $(if ($null -eq $packageSignature) { $null } else { $packageSignature.verificationCommand })
        packageSbom = $(if ($Backfill) { $null } else { "gh attestation verify ./NetCoreApplicationTemplate.$version.spdx.json --repo $Repository" })
        containerSignature = $containerManifest.signature.verificationCommand
        containerProvenance = $containerManifest.provenance.verificationCommand
    }
}

$manifestPath = Join-Path $resolvedEvidenceDirectory 'release-evidence-manifest.json'
$manifest | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $manifestPath -Encoding utf8 -NoNewline

Write-Host "Generated release evidence manifest for '$TagName' with $($assetRecords.Count) hashed asset(s)."
