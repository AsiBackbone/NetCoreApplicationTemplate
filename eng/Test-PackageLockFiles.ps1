[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Root = '.'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
$projectFiles = @(
    Get-ChildItem -LiteralPath $resolvedRoot -Filter '*.csproj' -File -Recurse |
        Where-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($resolvedRoot, $_.FullName)
            $pathSegments = $relativePath -split '[\\/]'
            'bin' -notin $pathSegments -and 'obj' -notin $pathSegments
        }
)

if ($projectFiles.Count -eq 0) {
    throw "No project files were found under '$resolvedRoot'."
}

$missingLockFiles = @(
    foreach ($projectFile in $projectFiles) {
        $lockFile = Join-Path -Path $projectFile.DirectoryName -ChildPath 'packages.lock.json'

        if (-not (Test-Path -LiteralPath $lockFile -PathType Leaf)) {
            [System.IO.Path]::GetRelativePath($resolvedRoot, $lockFile).Replace('\', '/')
        }
    }
)

if ($missingLockFiles.Count -gt 0) {
    throw "Missing NuGet lock files:`n$($missingLockFiles -join [Environment]::NewLine)"
}

Write-Host "Verified NuGet lock files for $($projectFiles.Count) project(s) under '$resolvedRoot'."
