[CmdletBinding()]
param(
    # Immutable image reference: <registry>/<name>@sha256:<64 hex>. A mutable
    # tag is rejected so verification is always tied to the published digest.
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9][a-z0-9._/-]*@sha256:[0-9a-f]{64}$')]
    [string]$DigestReference,

    [Parameter(Mandatory = $true)]
    [string]$TagName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit,

    [string]$Repository = 'AsiBackbone/NetCoreApplicationTemplate',

    # The reusable workflow that signs the image digest.
    [string]$WorkflowPath = '.github/workflows/publish-container.yml',

    [string]$WorkflowTrigger = 'push',

    # Exercise untrusted-identity cases that must fail.
    [switch]$IncludeNegativeChecks,

    [string]$CosignPath = 'cosign'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Fixed trust anchor. The issuer is deliberately not a parameter so callers
# cannot widen the accepted signer to another OIDC provider.
$trustedOidcIssuer = 'https://token.actions.githubusercontent.com'

function Get-SigstoreSignerIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkflowPath,

        [Parameter(Mandatory = $true)]
        [string]$WorkflowRef
    )

    return [ordered]@{
        certificateIdentity = "https://github.com/$Repository/$($WorkflowPath)@$WorkflowRef"
        certificateOidcIssuer = $trustedOidcIssuer
        workflowRepository = $Repository
        workflowRef = $WorkflowRef
        workflowSha = $SourceCommit.ToLowerInvariant()
        workflowTrigger = $WorkflowTrigger
    }
}

function Invoke-CosignVerify {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IDictionary]$Identity
    )

    # The --flag=value form keeps every argument self-contained.
    $arguments = @(
        'verify',
        "--certificate-identity=$($Identity.certificateIdentity)",
        "--certificate-oidc-issuer=$($Identity.certificateOidcIssuer)",
        "--certificate-github-workflow-repository=$($Identity.workflowRepository)",
        "--certificate-github-workflow-ref=$($Identity.workflowRef)",
        "--certificate-github-workflow-sha=$($Identity.workflowSha)",
        "--certificate-github-workflow-trigger=$($Identity.workflowTrigger)",
        '--output=json',
        $DigestReference
    )

    $global:LASTEXITCODE = $null
    $output = @(& $CosignPath @arguments 2>&1)
    $exitCode = $LASTEXITCODE

    # Native stderr lines arrive as ErrorRecords; the verified payloads are on stdout.
    $standardOutput = @($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] } | ForEach-Object { [string]$_ })
    $allOutput = @($output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine

    return [pscustomobject]@{
        Succeeded = ($exitCode -eq 0)
        ExitCode = $exitCode
        StandardOutput = ($standardOutput -join [Environment]::NewLine)
        Output = $allOutput
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

$expectedDigest = $DigestReference.Substring($DigestReference.IndexOf('@') + 1)
$identity = Get-SigstoreSignerIdentity -WorkflowPath $WorkflowPath -WorkflowRef "refs/tags/$TagName"
$result = Invoke-CosignVerify -Identity $identity

if (-not $result.Succeeded) {
    throw "Sigstore verification failed for '$DigestReference' (exit code $($result.ExitCode)).`n$($result.Output)"
}

try {
    $payloads = @($result.StandardOutput | ConvertFrom-Json)
}
catch {
    throw "Cosign verification output for '$DigestReference' is not JSON: $($_.Exception.Message)"
}

if ($payloads.Count -eq 0) {
    throw "Cosign returned no verified signature payloads for '$DigestReference'."
}

foreach ($payload in $payloads) {
    $signedDigest = [string]$payload.critical.image.'docker-manifest-digest'

    if ($signedDigest -cne $expectedDigest) {
        throw "A verified signature payload names digest '$signedDigest' instead of the published digest '$expectedDigest'."
    }
}

Write-Host "Verified Sigstore signature for '$DigestReference' against $($identity.certificateIdentity)."

if ($IncludeNegativeChecks) {
    $untrustedIdentities = @(
        (Get-SigstoreSignerIdentity -WorkflowPath $WorkflowPath -WorkflowRef 'refs/heads/main'),
        (Get-SigstoreSignerIdentity -WorkflowPath '.github/workflows/ci.yml' -WorkflowRef "refs/tags/$TagName")
    )

    foreach ($untrustedIdentity in $untrustedIdentities) {
        $negativeResult = Invoke-CosignVerify -Identity $untrustedIdentity

        if ($negativeResult.Succeeded) {
            throw "Negative check failed: '$DigestReference' verified for untrusted identity '$($untrustedIdentity.certificateIdentity)'."
        }
    }

    Write-Host 'Sigstore negative checks rejected untrusted workflow-ref and workflow-file identities.'
}

# Expected Cosign failures from the negative checks leave PowerShell's global
# native-command exit code nonzero even though every assertion passed. Clear it
# before returning so a successful `pwsh -File` invocation reports success to
# GitHub Actions and other callers.
$global:LASTEXITCODE = 0
