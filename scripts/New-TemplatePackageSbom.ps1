[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$TagName,

    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit,

    [switch]$GeneratedAfterRelease,

    [string]$GenerationNote,

    [string]$Repository = 'AsiBackbone/NetCoreApplicationTemplate'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function ConvertTo-SpdxIdPart {
    param([Parameter(Mandatory = $true)][string]$Value)

    $valuePart = [regex]::Replace($Value, '[^A-Za-z0-9.-]+', '-').Trim('-')
    return $(if ([string]::IsNullOrWhiteSpace($valuePart)) { 'unknown' } else { $valuePart })
}

function Get-NuspecDocument {
    param([Parameter(Mandatory = $true)][string]$PackagePath)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)

    try {
        $entry = $archive.Entries |
            Where-Object { $_.FullName -like '*.nuspec' -and $_.FullName -notlike '*/*' } |
            Select-Object -First 1

        if ($null -eq $entry) {
            throw "No root .nuspec entry was found in package '$PackagePath'."
        }

        $stream = $entry.Open()
        try {
            $reader = [System.IO.StreamReader]::new($stream)
            try {
                return [xml]$reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-ElementValue {
    param(
        [Parameter(Mandatory = $true)][System.Xml.XmlNode]$Parent,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $node = $Parent.SelectSingleNode("*[local-name()='$Name']")
    return $(if ($null -eq $node) { $null } else { $node.InnerText.Trim() })
}

function Get-AttributeValue {
    param(
        [System.Xml.XmlNode]$Node,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $Node -or $null -eq $Node.Attributes) {
        return $null
    }

    $attribute = $Node.Attributes.GetNamedItem($Name)
    return $(if ($null -eq $attribute) { $null } else { ([string]$attribute.Value).Trim() })
}

if ($Repository -notmatch '^[^/]+/[^/]+$') {
    throw "Repository '$Repository' must use owner/name form."
}

$tagVersion = $null
if (-not [string]::IsNullOrWhiteSpace($TagName)) {
    if ($TagName -notmatch '^v(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)$') {
        throw "Tag name '$TagName' must use vMAJOR.MINOR.PATCH with an optional prerelease suffix."
    }

    $tagVersion = $Matches['version']
}

if ($GeneratedAfterRelease -and [string]::IsNullOrWhiteSpace($GenerationNote)) {
    throw 'GenerationNote is required when GeneratedAfterRelease is set.'
}

$resolvedPackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$packageFiles = @(Get-ChildItem -LiteralPath $resolvedPackageDirectory -Filter '*.nupkg' -File)

if ($packageFiles.Count -ne 1) {
    throw "Expected exactly one .nupkg in '$resolvedPackageDirectory', found $($packageFiles.Count)."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$resolvedOutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

if (@(Get-ChildItem -LiteralPath $resolvedOutputDirectory -Force).Count -ne 0) {
    throw "SBOM output directory must be empty: $resolvedOutputDirectory"
}

$packageFile = $packageFiles[0]
$nuspec = Get-NuspecDocument -PackagePath $packageFile.FullName
$metadata = $nuspec.SelectSingleNode("//*[local-name()='metadata']")

if ($null -eq $metadata) {
    throw "No nuspec metadata was found in '$($packageFile.FullName)'."
}

$packageId = Get-ElementValue -Parent $metadata -Name 'id'
$packageVersion = Get-ElementValue -Parent $metadata -Name 'version'
$licenseExpression = Get-ElementValue -Parent $metadata -Name 'license'
$authors = Get-ElementValue -Parent $metadata -Name 'authors'
$description = Get-ElementValue -Parent $metadata -Name 'description'
$repositoryNode = $metadata.SelectSingleNode("*[local-name()='repository']")
$packageRepositoryUrl = Get-AttributeValue -Node $repositoryNode -Name 'url'
$packageRepositoryCommit = Get-AttributeValue -Node $repositoryNode -Name 'commit'

if ($packageId -ne 'NetCoreApplicationTemplate') {
    throw "Expected package id 'NetCoreApplicationTemplate', found '$packageId'."
}

if ([string]::IsNullOrWhiteSpace($packageVersion)) {
    throw 'The NuGet package version is missing.'
}

if (-not [string]::IsNullOrWhiteSpace($SourceCommit)) {
    if ([string]::IsNullOrWhiteSpace($packageRepositoryCommit)) {
        throw 'The NuGet package does not contain repository commit metadata.'
    }

    if ($packageRepositoryCommit -ne $SourceCommit.ToLowerInvariant()) {
        throw "Package repository commit '$packageRepositoryCommit' does not match source commit '$SourceCommit'."
    }
}

if ($null -ne $tagVersion -and $packageVersion -ne $tagVersion) {
    throw "Package version '$packageVersion' does not match tag '$TagName'."
}

if ([string]::IsNullOrWhiteSpace($licenseExpression)) {
    $licenseExpression = 'NOASSERTION'
}

$packageSha256 = Get-Sha256Hex -Path $packageFile.FullName
$createdUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$packageSpdxId = 'SPDXRef-Package-{0}' -f (ConvertTo-SpdxIdPart -Value $packageId)
$spdxPackages = [System.Collections.Generic.List[object]]::new()
$relationships = [System.Collections.Generic.List[object]]::new()
$dependencies = @($metadata.SelectNodes(".//*[local-name()='dependency']"))

$spdxPackages.Add([ordered]@{
    name = $packageId
    SPDXID = $packageSpdxId
    versionInfo = $packageVersion
    downloadLocation = 'NOASSERTION'
    filesAnalyzed = $false
    licenseConcluded = 'NOASSERTION'
    licenseDeclared = $licenseExpression
    copyrightText = 'NOASSERTION'
    summary = $description
    supplier = $(if ([string]::IsNullOrWhiteSpace($authors)) { 'NOASSERTION' } else { "Person: $authors" })
    checksums = @([ordered]@{ algorithm = 'SHA256'; checksumValue = $packageSha256 })
    externalRefs = @([ordered]@{
        referenceCategory = 'PACKAGE-MANAGER'
        referenceType = 'purl'
        referenceLocator = "pkg:nuget/$packageId@$packageVersion"
    })
})

$relationships.Add([ordered]@{
    spdxElementId = 'SPDXRef-DOCUMENT'
    relationshipType = 'DESCRIBES'
    relatedSpdxElement = $packageSpdxId
})

foreach ($dependency in $dependencies) {
    $dependencyId = Get-AttributeValue -Node $dependency -Name 'id'
    $dependencyVersion = Get-AttributeValue -Node $dependency -Name 'version'

    if ([string]::IsNullOrWhiteSpace($dependencyId)) {
        continue
    }

    $dependencySpdxId = 'SPDXRef-Dependency-{0}' -f (ConvertTo-SpdxIdPart -Value "$dependencyId-$dependencyVersion")
    $externalRefs = @()
    if (-not [string]::IsNullOrWhiteSpace($dependencyVersion)) {
        $externalRefs = @([ordered]@{
            referenceCategory = 'PACKAGE-MANAGER'
            referenceType = 'purl'
            referenceLocator = "pkg:nuget/$dependencyId@$dependencyVersion"
        })
    }

    $spdxPackages.Add([ordered]@{
        name = $dependencyId
        SPDXID = $dependencySpdxId
        versionInfo = $(if ([string]::IsNullOrWhiteSpace($dependencyVersion)) { 'NOASSERTION' } else { $dependencyVersion })
        downloadLocation = 'NOASSERTION'
        filesAnalyzed = $false
        licenseConcluded = 'NOASSERTION'
        licenseDeclared = 'NOASSERTION'
        copyrightText = 'NOASSERTION'
        supplier = 'NOASSERTION'
        externalRefs = $externalRefs
    })

    $relationships.Add([ordered]@{
        spdxElementId = $packageSpdxId
        relationshipType = 'DEPENDS_ON'
        relatedSpdxElement = $dependencySpdxId
    })
}

$document = [ordered]@{
    spdxVersion = 'SPDX-2.3'
    dataLicense = 'CC0-1.0'
    SPDXID = 'SPDXRef-DOCUMENT'
    name = "NuGet package SBOM - $packageId $packageVersion"
    documentNamespace = "https://github.com/$Repository/sbom/nuget/$packageId/$packageVersion/$packageSha256"
    creationInfo = [ordered]@{
        created = $createdUtc
        creators = @(
            'Tool: New-TemplatePackageSbom.ps1',
            "Organization: $Repository"
        )
    }
    packages = @($spdxPackages.ToArray())
    relationships = @($relationships.ToArray())
}

$sbomFileName = "$packageId.$packageVersion.spdx.json"
$sbomPath = Join-Path $resolvedOutputDirectory $sbomFileName
$document | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $sbomPath -Encoding utf8 -NoNewline

$manifest = [ordered]@{
    schemaVersion = 1
    repository = $Repository
    releaseTag = $TagName
    sourceCommit = $(if ([string]::IsNullOrWhiteSpace($SourceCommit)) { $null } else { $SourceCommit.ToLowerInvariant() })
    createdUtc = $createdUtc
    generatedAfterRelease = [bool]$GeneratedAfterRelease
    generationNote = $(if ([string]::IsNullOrWhiteSpace($GenerationNote)) { $null } else { $GenerationNote })
    package = [ordered]@{
        packageId = $packageId
        version = $packageVersion
        packageFile = $packageFile.Name
        packageSha256 = $packageSha256
        repositoryUrl = $packageRepositoryUrl
        repositoryCommit = $packageRepositoryCommit
        sbomFile = $sbomFileName
        sbomSha256 = Get-Sha256Hex -Path $sbomPath
        dependencyCount = $dependencies.Count
    }
}

$manifestPath = Join-Path $resolvedOutputDirectory 'template-package-manifest.json'
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8 -NoNewline

Write-Host "Generated template-package SBOM '$sbomFileName' and manifest for $packageId $packageVersion."
