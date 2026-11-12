[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string]$BundleDirectory,

    [Parameter(Mandatory = $true)]
    [string]$TagName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit,

    [string]$Repository = 'AsiBackbone/NetCoreApplicationTemplate',

    # The reusable workflow that signs the package. Its job_workflow_ref is the
    # Fulcio certificate identity even though Publish Stable Release calls it.
    [string]$WorkflowPath = '.github/workflows/publish-template-package.yml',

    [string]$WorkflowTrigger = 'push',

    # Optional template-package-manifest.json. When supplied, the package file
    # name and SHA-256 must match the validated build output before any
    # signature is checked.
    [string]$PackageManifestPath,

    # Exercise tamper, wrong-identity, and swapped-bundle cases that must fail.
    [switch]$IncludeNegativeChecks,

    [string]$CosignPath = 'cosign'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Fixed trust anchors. The issuer is deliberately not a parameter so callers
# cannot widen the accepted signer to another OIDC provider.
$trustedOidcIssuer = 'https://token.actions.githubusercontent.com'
$bundleSuffix = '.sigstore.json'
$bundleMediaTypePrefix = 'application/vnd.dev.sigstore.bundle'

function Get-Sha256Hex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-SigstoreSignerIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,

        [Parameter(Mandatory = $true)]
        [string]$WorkflowPath,

        [Parameter(Mandatory = $true)]
        [string]$TagName,

        [Parameter(Mandatory = $true)]
        [string]$SourceCommit,

        [Parameter(Mandatory = $true)]
        [string]$WorkflowTrigger
    )

    $workflowRef = "refs/tags/$TagName"

    return [ordered]@{
        certificateIdentity = "https://github.com/$Repository/$($WorkflowPath)@$workflowRef"
        certificateOidcIssuer = $trustedOidcIssuer
        workflowRepository = $Repository
        workflowRef = $workflowRef
        workflowSha = $SourceCommit.ToLowerInvariant()
        workflowTrigger = $WorkflowTrigger
    }
}

function Get-CosignVerifyArguments {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$Identity,

        [Parameter(Mandatory = $true)]
        [string]$BundlePath,

        [Parameter(Mandatory = $true)]
        [string]$BlobPath
    )

    # The --flag=value form keeps every argument self-contained.
    return @(
        'verify-blob',
        "--bundle=$BundlePath",
        "--certificate-identity=$($Identity.certificateIdentity)",
        "--certificate-oidc-issuer=$($Identity.certificateOidcIssuer)",
        "--certificate-github-workflow-repository=$($Identity.workflowRepository)",
        "--certificate-github-workflow-ref=$($Identity.workflowRef)",
        "--certificate-github-workflow-sha=$($Identity.workflowSha)",
        "--certificate-github-workflow-trigger=$($Identity.workflowTrigger)",
        $BlobPath
    )
}

function Invoke-CosignVerify {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$Identity,

        [Parameter(Mandatory = $true)]
        [string]$BundlePath,

        [Parameter(Mandatory = $true)]
        [string]$BlobPath
    )

    $arguments = Get-CosignVerifyArguments -Identity $Identity -BundlePath $BundlePath -BlobPath $BlobPath
    $global:LASTEXITCODE = $null
    $output = & $CosignPath @arguments 2>&1
    $exitCode = $LASTEXITCODE

    return [pscustomobject]@{
        Succeeded = ($exitCode -eq 0)
        ExitCode = $exitCode
        Output = (@($output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine)
    }
}

if ($TagName -notmatch '^v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Tag name '$TagName' must use the form vMAJOR.MINOR.PATCH with an optional prerelease suffix."
}

if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
    throw "Repository '$Repository' must use the owner/name form."
}

if ($WorkflowPath -notmatch '^\.github/workflows/[A-Za-z0-9_.-]+\.ya?ml$') {
    throw "Workflow path '$WorkflowPath' must name a workflow file under .github/workflows."
}

if ($WorkflowTrigger -notmatch '^[a-z_]+$') {
    throw "Workflow trigger '$WorkflowTrigger' is not a GitHub Actions event name."
}

$resolvedPackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$resolvedBundleDirectory = (Resolve-Path -LiteralPath $BundleDirectory).Path
$packageFiles = @(Get-ChildItem -LiteralPath $resolvedPackageDirectory -Filter '*.nupkg' -File | Sort-Object Name)

if ($packageFiles.Count -eq 0) {
    throw "No .nupkg signing subjects were found in '$resolvedPackageDirectory'."
}

$bundleFiles = @(Get-ChildItem -LiteralPath $resolvedBundleDirectory -File |
    Where-Object { $_.Name.EndsWith($bundleSuffix, [System.StringComparison]::Ordinal) } |
    Sort-Object Name)
$expectedBundleNames = @($packageFiles | ForEach-Object { $_.Name + $bundleSuffix })
$actualBundleNames = @($bundleFiles | ForEach-Object { $_.Name })
$missingBundles = @($expectedBundleNames | Where-Object { $_ -cnotin $actualBundleNames })
$orphanBundles = @($actualBundleNames | Where-Object { $_ -cnotin $expectedBundleNames })

if ($missingBundles.Count -ne 0) {
    throw "Sigstore bundle missing for package subject(s): $($missingBundles -join ', ')"
}

if ($orphanBundles.Count -ne 0) {
    throw "Sigstore bundle(s) without a matching package subject: $($orphanBundles -join ', ')"
}

foreach ($bundleFile in $bundleFiles) {
    if ($bundleFile.Length -eq 0) {
        throw "Sigstore bundle '$($bundleFile.Name)' is empty."
    }

    try {
        $bundleDocument = Get-Content -LiteralPath $bundleFile.FullName -Raw | ConvertFrom-Json
    }
    catch {
        throw "Sigstore bundle '$($bundleFile.Name)' is not valid JSON: $($_.Exception.Message)"
    }

    $mediaTypeProperty = $bundleDocument.PSObject.Properties['mediaType']

    if ($null -eq $mediaTypeProperty -or -not ([string]$mediaTypeProperty.Value).StartsWith($bundleMediaTypePrefix, [System.StringComparison]::Ordinal)) {
        throw "Sigstore bundle '$($bundleFile.Name)' does not declare a Sigstore protobuf bundle media type."
    }
}

if (-not [string]::IsNullOrWhiteSpace($PackageManifestPath)) {
    $packageManifest = Get-Content -LiteralPath $PackageManifestPath -Raw | ConvertFrom-Json
    $expectedPackageFile = [string]$packageManifest.package.packageFile
    $expectedPackageHash = ([string]$packageManifest.package.packageSha256).ToLowerInvariant()

    if ($packageFiles.Count -ne 1 -or $packageFiles[0].Name -cne $expectedPackageFile) {
        $actualNames = @($packageFiles | ForEach-Object { $_.Name }) -join ', '
        throw "Signing subject must be exactly '$expectedPackageFile' from template-package-manifest.json, but found: $actualNames"
    }

    $actualPackageHash = Get-Sha256Hex -Path $packageFiles[0].FullName

    if ($actualPackageHash -ne $expectedPackageHash) {
        throw "Package digest mismatch for '$expectedPackageFile'. template-package-manifest.json records $expectedPackageHash but the subject is $actualPackageHash."
    }
}

$identity = Get-SigstoreSignerIdentity `
    -Repository $Repository `
    -WorkflowPath $WorkflowPath `
    -TagName $TagName `
    -SourceCommit $SourceCommit `
    -WorkflowTrigger $WorkflowTrigger

foreach ($packageFile in $packageFiles) {
    $bundlePath = Join-Path $resolvedBundleDirectory ($packageFile.Name + $bundleSuffix)
    $result = Invoke-CosignVerify -Identity $identity -BundlePath $bundlePath -BlobPath $packageFile.FullName

    if (-not $result.Succeeded) {
        throw "Sigstore verification failed for '$($packageFile.Name)' (exit code $($result.ExitCode)).`n$($result.Output)"
    }

    Write-Host "Verified Sigstore bundle for '$($packageFile.Name)' against $($identity.certificateIdentity)."
}

if ($IncludeNegativeChecks) {
    $negativeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("asibackbone-sigstore-negative-{0}" -f [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $negativeRoot -Force | Out-Null

    try {
        $subject = $packageFiles[0]
        $subjectBundle = Join-Path $resolvedBundleDirectory ($subject.Name + $bundleSuffix)

        # 1. A modified subject must not verify with the original bundle.
        $tamperedPath = Join-Path $negativeRoot $subject.Name
        Copy-Item -LiteralPath $subject.FullName -Destination $tamperedPath
        [System.IO.File]::AppendAllText($tamperedPath, 'asibackbone-sigstore-tamper-check')
        $tamperResult = Invoke-CosignVerify -Identity $identity -BundlePath $subjectBundle -BlobPath $tamperedPath

        if ($tamperResult.Succeeded) {
            throw "Negative check failed: a tampered copy of '$($subject.Name)' verified against its Sigstore bundle."
        }

        # 2. A valid signature from an unexpected workflow identity must be rejected.
        $untrustedIdentity = Get-SigstoreSignerIdentity `
            -Repository $Repository `
            -WorkflowPath $WorkflowPath `
            -TagName $TagName `
            -SourceCommit $SourceCommit `
            -WorkflowTrigger $WorkflowTrigger
        $untrustedIdentity.certificateIdentity = "https://github.com/$Repository/$($WorkflowPath)@refs/heads/main"
        $untrustedIdentity.workflowRef = 'refs/heads/main'
        $identityResult = Invoke-CosignVerify -Identity $untrustedIdentity -BundlePath $subjectBundle -BlobPath $subject.FullName

        if ($identityResult.Succeeded) {
            throw "Negative check failed: '$($subject.Name)' verified for an untrusted workflow identity."
        }

        # 3. A bundle issued for a different subject must not verify.
        if ($packageFiles.Count -gt 1) {
            $otherBundle = Join-Path $resolvedBundleDirectory ($packageFiles[1].Name + $bundleSuffix)
            $swapResult = Invoke-CosignVerify -Identity $identity -BundlePath $otherBundle -BlobPath $subject.FullName

            if ($swapResult.Succeeded) {
                throw "Negative check failed: '$($subject.Name)' verified with the bundle issued for '$($packageFiles[1].Name)'."
            }
        }

        $checkedCases = if ($packageFiles.Count -gt 1) { 'tampered, untrusted-identity, and swapped-bundle cases' } else { 'tampered and untrusted-identity cases' }
        Write-Host "Sigstore negative checks rejected $checkedCases."
    }
    finally {
        if (Test-Path -LiteralPath $negativeRoot) {
            Remove-Item -LiteralPath $negativeRoot -Recurse -Force
        }
    }
}

Write-Host "Verified $($packageFiles.Count) Sigstore package bundle(s) for '$Repository' release '$TagName'."
