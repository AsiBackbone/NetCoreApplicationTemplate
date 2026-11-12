[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Exercises scripts/Assert-SigstorePackageBundle.ps1 and
# scripts/Assert-SigstoreContainerSignature.ps1 without network access or a
# Sigstore signing identity. Stub verifiers stand in for cosign: they accept a
# subject only when its digest and every pinned identity claim match the values
# recorded in the fixture, mirroring the checks cosign performs.

$packageAssertPath = Join-Path $PSScriptRoot 'Assert-SigstorePackageBundle.ps1'
$containerAssertPath = Join-Path $PSScriptRoot 'Assert-SigstoreContainerSignature.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ncat-sigstore-verification-{0}" -f [guid]::NewGuid().ToString('N'))
$repository = 'AsiBackbone/NetCoreApplicationTemplate'
$tagName = 'v9.8.7'
$sourceCommit = 'c' * 40
$issuer = 'https://token.actions.githubusercontent.com'
$packageIdentity = "https://github.com/$repository/.github/workflows/publish-template-package.yml@refs/tags/$tagName"
$containerIdentity = "https://github.com/$repository/.github/workflows/publish-container.yml@refs/tags/$tagName"
$imageName = 'ghcr.io/asibackbone/netcoreapplicationtemplate'
$signedDigest = 'sha256:' + ('d' * 64)
$otherDigest = 'sha256:' + ('e' * 64)

$parseArguments = @'
$options = @{}
$positional = [System.Collections.Generic.List[string]]::new()

foreach ($argument in $args) {
    $text = [string]$argument

    if ($text -match '^--(?<name>[a-z-]+)=(?<value>.*)$') {
        $options[$Matches['name']] = $Matches['value']
    }
    else {
        $positional.Add($text)
    }
}

function Test-Claims {
    param($Signature)

    $claims = [ordered]@{
        'certificate-identity' = $Signature.testCertificateIdentity
        'certificate-oidc-issuer' = $Signature.testIssuer
        'certificate-github-workflow-repository' = $Signature.testWorkflowRepository
        'certificate-github-workflow-ref' = $Signature.testWorkflowRef
        'certificate-github-workflow-sha' = $Signature.testWorkflowSha
        'certificate-github-workflow-trigger' = $Signature.testWorkflowTrigger
    }

    foreach ($claim in $claims.Keys) {
        if (-not $options.ContainsKey($claim) -or $options[$claim] -cne [string]$claims[$claim]) {
            return $false
        }
    }

    return $true
}
'@

$blobVerifier = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
'@ + "`n" + $parseArguments + "`n" + @'
if ($positional.Count -ne 2 -or $positional[0] -cne 'verify-blob' -or -not $options.ContainsKey('bundle')) {
    Write-Output 'stub: unexpected invocation'
    exit 2
}

$bundle = Get-Content -LiteralPath $options['bundle'] -Raw | ConvertFrom-Json
$blobHash = (Get-FileHash -LiteralPath $positional[1] -Algorithm SHA256).Hash.ToLowerInvariant()

if ($blobHash -ne $bundle.testSubjectSha256) {
    Write-Output 'Error: provided artifact digest does not match the bundle'
    exit 1
}

if (-not (Test-Claims $bundle)) {
    Write-Output 'Error: no matching certificate identity found'
    exit 1
}

Write-Output 'Verified OK'
exit 0
'@

$imageVerifier = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
'@ + "`n" + $parseArguments + "`n" + @'
if ($positional.Count -ne 2 -or $positional[0] -cne 'verify' -or $options['output'] -cne 'json') {
    Write-Output 'stub: unexpected invocation'
    exit 2
}

$signature = Get-Content -LiteralPath $env:NCAT_TEST_IMAGE_SIGNATURE -Raw | ConvertFrom-Json
$requestedDigest = $positional[1].Substring($positional[1].IndexOf('@') + 1)

if ($requestedDigest -cne $signature.testSignedDigest) {
    Write-Output 'Error: no signatures found'
    exit 1
}

if (-not (Test-Claims $signature)) {
    Write-Output 'Error: no matching signatures: none of the expected identities matched'
    exit 1
}

$payload = @(
    [ordered]@{
        critical = [ordered]@{
            identity = [ordered]@{ 'docker-reference' = $positional[1].Substring(0, $positional[1].IndexOf('@')) }
            image = [ordered]@{ 'docker-manifest-digest' = $signature.testPayloadDigest }
            type = 'cosign container image signature'
        }
        optional = $null
    }
)

Write-Output (ConvertTo-Json -InputObject $payload -Depth 6 -Compress)
exit 0
'@

$permissiveBlobVerifier = @'
Write-Output 'Verified OK'
exit 0
'@

$permissiveImageVerifier = @'
$reference = [string]$args[-1]
$digest = $reference.Substring($reference.IndexOf('@') + 1)
Write-Output ('[{"critical":{"identity":{"docker-reference":"x"},"image":{"docker-manifest-digest":"' + $digest + '"},"type":"cosign container image signature"},"optional":null}]')
exit 0
'@

function Get-Sha256Hex {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function New-PackageFixture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [string]$SignerIdentity = $packageIdentity,

        [string]$SignerRef = "refs/tags/$tagName"
    )

    $root = Join-Path $testRoot $Name
    $packages = Join-Path $root 'packages'
    $bundles = Join-Path $root 'bundles'
    New-Item -ItemType Directory -Path $packages, $bundles -Force | Out-Null

    $packageFile = 'NetCoreApplicationTemplate.9.8.7.nupkg'
    $packagePath = Join-Path $packages $packageFile
    Set-Content -LiteralPath $packagePath -Value 'template package' -Encoding utf8 -NoNewline
    $packageHash = Get-Sha256Hex -Path $packagePath

    [ordered]@{
        mediaType = 'application/vnd.dev.sigstore.bundle.v0.3+json'
        testSubjectSha256 = $packageHash
        testCertificateIdentity = $SignerIdentity
        testIssuer = $issuer
        testWorkflowRepository = $repository
        testWorkflowRef = $SignerRef
        testWorkflowSha = $sourceCommit
        testWorkflowTrigger = 'push'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bundles "$packageFile.sigstore.json") -Encoding utf8 -NoNewline

    # Non-bundle evidence files share the directory in the release workflow.
    Set-Content -LiteralPath (Join-Path $bundles 'NetCoreApplicationTemplate.9.8.7.spdx.json') -Value '{"spdxVersion":"SPDX-2.3"}' -Encoding utf8 -NoNewline

    $packageManifestPath = Join-Path $root 'template-package-manifest.json'
    [ordered]@{
        schemaVersion = 1
        package = [ordered]@{
            packageId = 'NetCoreApplicationTemplate'
            version = '9.8.7'
            packageFile = $packageFile
            packageSha256 = $packageHash
        }
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $packageManifestPath -Encoding utf8 -NoNewline

    return [pscustomobject]@{
        Packages = $packages
        Bundles = $bundles
        PackageFile = $packageFile
        PackageManifest = $packageManifestPath
    }
}

function Invoke-PackageAssert {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Fixture,

        [Parameter(Mandatory = $true)]
        [string]$Verifier,

        [switch]$UsePackageManifest
    )

    $arguments = @{
        PackageDirectory = $Fixture.Packages
        BundleDirectory = $Fixture.Bundles
        TagName = $tagName
        SourceCommit = $sourceCommit
        Repository = $repository
        CosignPath = $Verifier
        IncludeNegativeChecks = $true
    }

    if ($UsePackageManifest) {
        $arguments['PackageManifestPath'] = $Fixture.PackageManifest
    }

    & $packageAssertPath @arguments
}

function Set-ImageSignature {
    param(
        [string]$SignerIdentity = $containerIdentity,
        [string]$SignerRef = "refs/tags/$tagName",
        [string]$PayloadDigest = $signedDigest
    )

    $path = Join-Path $testRoot ("image-signature-{0}.json" -f [guid]::NewGuid().ToString('N'))
    [ordered]@{
        testSignedDigest = $signedDigest
        testPayloadDigest = $PayloadDigest
        testCertificateIdentity = $SignerIdentity
        testIssuer = $issuer
        testWorkflowRepository = $repository
        testWorkflowRef = $SignerRef
        testWorkflowSha = $sourceCommit
        testWorkflowTrigger = 'push'
    } | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding utf8 -NoNewline
    $env:NCAT_TEST_IMAGE_SIGNATURE = $path
}

function Invoke-ContainerAssert {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Verifier,

        [string]$Digest = $signedDigest,

        [string]$Reference
    )

    if ([string]::IsNullOrWhiteSpace($Reference)) {
        $Reference = "$imageName@$Digest"
    }

    & $containerAssertPath `
        -DigestReference $Reference `
        -TagName $tagName `
        -SourceCommit $sourceCommit `
        -Repository $repository `
        -CosignPath $Verifier `
        -IncludeNegativeChecks
}

function Assert-Rejected {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedMessagePattern,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    $rejected = $false

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notlike $ExpectedMessagePattern) {
            throw "$Description failed with an unexpected message: $($_.Exception.Message)"
        }

        $rejected = $true
    }

    if (-not $rejected) {
        throw "$Description was not rejected."
    }

    Write-Host "Rejected as expected: $Description"
}

$previousImageSignature = $env:NCAT_TEST_IMAGE_SIGNATURE

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    $blobStub = Join-Path $testRoot 'stub-cosign-blob.ps1'
    $imageStub = Join-Path $testRoot 'stub-cosign-image.ps1'
    $permissiveBlobStub = Join-Path $testRoot 'permissive-cosign-blob.ps1'
    $permissiveImageStub = Join-Path $testRoot 'permissive-cosign-image.ps1'
    Set-Content -LiteralPath $blobStub -Value $blobVerifier -Encoding utf8
    Set-Content -LiteralPath $imageStub -Value $imageVerifier -Encoding utf8
    Set-Content -LiteralPath $permissiveBlobStub -Value $permissiveBlobVerifier -Encoding utf8
    Set-Content -LiteralPath $permissiveImageStub -Value $permissiveImageVerifier -Encoding utf8

    # Template package bundle.
    $valid = New-PackageFixture -Name 'valid'
    Invoke-PackageAssert -Fixture $valid -Verifier $blobStub -UsePackageManifest

    $missing = New-PackageFixture -Name 'missing-bundle'
    Remove-Item -LiteralPath (Join-Path $missing.Bundles "$($missing.PackageFile).sigstore.json")
    Assert-Rejected -Description 'Missing package Sigstore bundle' -ExpectedMessagePattern 'Sigstore bundle missing*' -Action {
        Invoke-PackageAssert -Fixture $missing -Verifier $blobStub
    }

    $missingPackage = New-PackageFixture -Name 'missing-package'
    Remove-Item -LiteralPath (Join-Path $missingPackage.Packages $missingPackage.PackageFile)
    Assert-Rejected -Description 'Missing template package' -ExpectedMessagePattern 'No .nupkg signing subjects*' -Action {
        Invoke-PackageAssert -Fixture $missingPackage -Verifier $blobStub
    }

    $nonBundle = New-PackageFixture -Name 'non-bundle'
    Set-Content -LiteralPath (Join-Path $nonBundle.Bundles "$($nonBundle.PackageFile).sigstore.json") -Value '{"signature":"legacy"}' -Encoding utf8 -NoNewline
    Assert-Rejected -Description 'Bundle without a Sigstore bundle media type' -ExpectedMessagePattern '*does not declare a Sigstore protobuf bundle media type*' -Action {
        Invoke-PackageAssert -Fixture $nonBundle -Verifier $blobStub
    }

    $changedPackage = New-PackageFixture -Name 'changed-package'
    Set-Content -LiteralPath (Join-Path $changedPackage.Packages $changedPackage.PackageFile) -Value 'replaced package' -Encoding utf8 -NoNewline
    Assert-Rejected -Description 'Package digest differing from template-package-manifest.json' -ExpectedMessagePattern 'Package digest mismatch*' -Action {
        Invoke-PackageAssert -Fixture $changedPackage -Verifier $blobStub -UsePackageManifest
    }

    $tampered = New-PackageFixture -Name 'tampered-package'
    Set-Content -LiteralPath (Join-Path $tampered.Packages $tampered.PackageFile) -Value 'tampered package' -Encoding utf8 -NoNewline
    Assert-Rejected -Description 'Package modified after signing' -ExpectedMessagePattern 'Sigstore verification failed*' -Action {
        Invoke-PackageAssert -Fixture $tampered -Verifier $blobStub
    }

    $untrustedPackage = New-PackageFixture -Name 'untrusted-package-signer' `
        -SignerIdentity "https://github.com/$repository/.github/workflows/ci.yml@refs/heads/main" `
        -SignerRef 'refs/heads/main'
    Assert-Rejected -Description 'Package signed by an untrusted workflow identity' -ExpectedMessagePattern 'Sigstore verification failed*' -Action {
        Invoke-PackageAssert -Fixture $untrustedPackage -Verifier $blobStub
    }

    $permissivePackage = New-PackageFixture -Name 'permissive-blob-verifier'
    Assert-Rejected -Description 'Blob verifier that accepts every input' -ExpectedMessagePattern 'Negative check failed*' -Action {
        Invoke-PackageAssert -Fixture $permissivePackage -Verifier $permissiveBlobStub
    }

    # OCI image digest signature.
    Set-ImageSignature
    Invoke-ContainerAssert -Verifier $imageStub

    Assert-Rejected -Description 'Unsigned image digest' -ExpectedMessagePattern 'Sigstore verification failed*' -Action {
        Invoke-ContainerAssert -Verifier $imageStub -Digest $otherDigest
    }

    Assert-Rejected -Description 'Mutable image tag instead of a digest' -ExpectedMessagePattern '*DigestReference*' -Action {
        Invoke-ContainerAssert -Verifier $imageStub -Reference "${imageName}:9.8.7"
    }

    Set-ImageSignature -SignerIdentity "https://github.com/$repository/.github/workflows/ci.yml@refs/heads/main" -SignerRef 'refs/heads/main'
    Assert-Rejected -Description 'Image signed by an untrusted workflow identity' -ExpectedMessagePattern 'Sigstore verification failed*' -Action {
        Invoke-ContainerAssert -Verifier $imageStub
    }

    Set-ImageSignature -PayloadDigest $otherDigest
    Assert-Rejected -Description 'Verified payload naming another digest' -ExpectedMessagePattern 'A verified signature payload names digest*' -Action {
        Invoke-ContainerAssert -Verifier $imageStub
    }

    Assert-Rejected -Description 'Image verifier that accepts every identity' -ExpectedMessagePattern 'Negative check failed*' -Action {
        Invoke-ContainerAssert -Verifier $permissiveImageStub
    }

    Write-Host 'Sigstore verification tests passed.'
}
finally {
    $env:NCAT_TEST_IMAGE_SIGNATURE = $previousImageSignature

    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
