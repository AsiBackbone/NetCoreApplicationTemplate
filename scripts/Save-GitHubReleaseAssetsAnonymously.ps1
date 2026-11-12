[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TagName,

    [Parameter(Mandatory = $true)]
    [string[]]$AssetName,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$Repository = 'AsiBackbone/NetCoreApplicationTemplate',

    [ValidateRange(1, 10)]
    [int]$MaxAttempts = 5,

    [ValidateRange(0, 60)]
    [int]$RetryDelaySeconds = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Downloads release assets from the public github.com download URLs without an
# Authorization header, exactly as a consumer without GitHub credentials would.

if ($TagName -notmatch '^v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Tag name '$TagName' must use the form vMAJOR.MINOR.PATCH with an optional prerelease suffix."
}

if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
    throw "Repository '$Repository' must use the owner/name form."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$resolvedOutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

foreach ($name in $AssetName) {
    if ([string]::IsNullOrWhiteSpace($name) -or $name -match '[\\/]' -or $name -in @('.', '..')) {
        throw "Release asset name '$name' must be a plain file name."
    }

    $uri = 'https://github.com/{0}/releases/download/{1}/{2}' -f $Repository, [uri]::EscapeDataString($TagName), [uri]::EscapeDataString($name)
    $destination = Join-Path $resolvedOutputDirectory $name

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            Invoke-WebRequest -Uri $uri -OutFile $destination -MaximumRedirection 5
            break
        }
        catch {
            if ($attempt -eq $MaxAttempts) {
                throw "Anonymous download of release asset '$name' from '$uri' failed: $($_.Exception.Message)"
            }

            Write-Host "Release asset '$name' is not downloadable yet; retrying ($attempt/$MaxAttempts)."
            Start-Sleep -Seconds $RetryDelaySeconds
        }
    }

    if ((Get-Item -LiteralPath $destination).Length -eq 0) {
        throw "Anonymous download of release asset '$name' produced an empty file."
    }

    Write-Host "Downloaded release asset '$name' anonymously."
}
