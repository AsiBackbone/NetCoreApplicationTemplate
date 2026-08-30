[CmdletBinding()]
param(
    [string]$ExpectedVersion,
    [string]$TagName,
    [string]$PackageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$failures = [System.Collections.Generic.List[string]]::new()
$repoRoot = Split-Path -Parent $PSScriptRoot

function Add-Failure {
    param([string]$Message)

    $script:failures.Add($Message)
}

function Resolve-RepositoryPath {
    param([string]$RelativePath)

    return Join-Path $repoRoot $RelativePath
}

function Get-RequiredFileText {
    param([string]$RelativePath)

    $path = Resolve-RepositoryPath $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Failure "Required file was not found: $RelativePath"
        return ''
    }

    return Get-Content -LiteralPath $path -Raw
}

function Get-ProjectProperty {
    param(
        [string]$RelativePath,
        [string]$PropertyName,
        [bool]$Required = $true
    )

    $path = Resolve-RepositoryPath $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Failure "Required project file was not found: $RelativePath"
        return $null
    }

    [xml]$project = Get-Content -LiteralPath $path -Raw
    $nodes = @($project.Project.PropertyGroup.ChildNodes | Where-Object {
        $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq $PropertyName
    })

    if ($nodes.Count -eq 0) {
        if ($Required) {
            Add-Failure "Property '$PropertyName' was not found in $RelativePath."
        }

        return $null
    }

    return $nodes[0].InnerText.Trim()
}

function Resolve-Version {
    param(
        [string]$VersionPrefix,
        [string]$VersionSuffix
    )

    if ([string]::IsNullOrWhiteSpace($VersionSuffix)) {
        return $VersionPrefix
    }

    return "$VersionPrefix-$VersionSuffix"
}

function Assert-Equal {
    param(
        [string]$Actual,
        [string]$Expected,
        [string]$Description
    )

    if ($Actual -ne $Expected) {
        Add-Failure "$Description expected '$Expected' but found '$Actual'."
    }
}

function Assert-Matches {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Description
    )

    if ($Text -notmatch $Pattern) {
        Add-Failure "$Description was not found or did not match expected version '$ExpectedVersion'."
    }
}

function Get-RegexGroupValue {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$GroupName,
        [string]$Description,
        [bool]$Required = $true
    )

    $match = [regex]::Match($Text, $Pattern)
    if (-not $match.Success) {
        if ($Required) {
            Add-Failure "$Description was not found."
        }

        return $null
    }

    return $match.Groups[$GroupName].Value
}

$directoryVersionPrefix = Get-ProjectProperty 'Directory.Build.props' 'VersionPrefix'
$directoryVersionSuffix = Get-ProjectProperty 'Directory.Build.props' 'VersionSuffix' $false
$directoryVersionSuffix = if ($null -eq $directoryVersionSuffix) { '' } else { $directoryVersionSuffix }
$resolvedDirectoryVersion = Resolve-Version $directoryVersionPrefix $directoryVersionSuffix

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $ExpectedVersion = $resolvedDirectoryVersion
}

if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$') {
    Add-Failure "Expected version '$ExpectedVersion' is not a supported semantic version. Use MAJOR.MINOR.PATCH with an optional prerelease suffix."
}

Assert-Equal $resolvedDirectoryVersion $ExpectedVersion 'Directory.Build.props resolved version'
Assert-Equal (Get-ProjectProperty 'Directory.Build.props' 'AssemblyVersion') "$directoryVersionPrefix.0" 'Directory.Build.props AssemblyVersion'
Assert-Equal (Get-ProjectProperty 'Directory.Build.props' 'FileVersion') "$directoryVersionPrefix.0" 'Directory.Build.props FileVersion'

$templateVersionPrefix = Get-ProjectProperty 'NetCoreApplicationTemplate.Template.csproj' 'VersionPrefix'
$templateVersionSuffix = Get-ProjectProperty 'NetCoreApplicationTemplate.Template.csproj' 'VersionSuffix' $false
$templateVersionSuffix = if ($null -eq $templateVersionSuffix) { '' } else { $templateVersionSuffix }
$templateResolvedVersion = Resolve-Version $templateVersionPrefix $templateVersionSuffix

Assert-Equal $templateResolvedVersion $ExpectedVersion 'NetCoreApplicationTemplate.Template.csproj resolved package version'

$templateVersion = Get-ProjectProperty 'NetCoreApplicationTemplate.Template.csproj' 'Version' $false
if (-not [string]::IsNullOrWhiteSpace($templateVersion)) {
    $allowedVersionValues = @($ExpectedVersion, '$(VersionPrefix)', '$(Version)')
    if ($allowedVersionValues -notcontains $templateVersion) {
        Add-Failure "NetCoreApplicationTemplate.Template.csproj Version should be '$ExpectedVersion' or derive from VersionPrefix, but found '$templateVersion'."
    }
}

$templatePackageVersion = Get-ProjectProperty 'NetCoreApplicationTemplate.Template.csproj' 'PackageVersion' $false
if (-not [string]::IsNullOrWhiteSpace($templatePackageVersion)) {
    $allowedPackageVersionValues = @($ExpectedVersion, '$(VersionPrefix)', '$(Version)')
    if ($allowedPackageVersionValues -notcontains $templatePackageVersion) {
        Add-Failure "NetCoreApplicationTemplate.Template.csproj PackageVersion should be '$ExpectedVersion' or derive from VersionPrefix, but found '$templatePackageVersion'."
    }
}

$escapedVersion = [regex]::Escape($ExpectedVersion)
$escapedReleaseTag = [regex]::Escape('`v' + $ExpectedVersion + '`')
$escapedPackageId = [regex]::Escape('NetCoreApplicationTemplate')

$readme = Get-RequiredFileText 'README.md'

# The README "Current Release" block represents the latest *published* GitHub
# release, not the version currently being prepared. publish-release.yml updates
# this block after a release is published, so pre-release branches may
# intentionally carry a newer package version than the latest published tag.
$publishedReleaseMatch = [regex]::Match(
    $readme,
    'Current release:\s*__\[Release (?<labelVersion>\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?)\]\([^\)]*/releases/tag/v(?<urlVersion>\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?)\)__'
)

if (-not $publishedReleaseMatch.Success) {
    Add-Failure 'README current-release block was not found or was malformed.'
}
else {
    $publishedLabelVersion = $publishedReleaseMatch.Groups['labelVersion'].Value
    $publishedUrlVersion = $publishedReleaseMatch.Groups['urlVersion'].Value
    Assert-Equal $publishedUrlVersion $publishedLabelVersion 'README current-release URL version'

    $publishedTagMatch = [regex]::Match(
        $readme,
        '(?m)^Tag:\s*`v(?<tagVersion>\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?)`\s*

$packageReadme = Get-RequiredFileText 'PACKAGE-README.md'
Assert-Matches $packageReadme "$escapedPackageId\.$escapedVersion\.nupkg" 'PACKAGE-README package install example'

$citation = Get-RequiredFileText 'CITATION.cff'
$citationVersion = Get-RegexGroupValue $citation '(?m)^version:\s*["'']?(?<version>[^"''\r\n]+)["'']?\s*$' 'version' 'CITATION.cff version metadata'
if ($null -ne $citationVersion) {
    Assert-Equal $citationVersion $ExpectedVersion 'CITATION.cff version metadata'
}

$changelog = Get-RequiredFileText 'CHANGELOG.md'
$changelogMatch = [regex]::Match($changelog, '(?m)^##\s+(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?)\s+-\s+\d{4}-\d{2}-\d{2}\s*$')
if ($changelogMatch.Success) {
    Assert-Equal $changelogMatch.Groups['version'].Value $ExpectedVersion 'CHANGELOG latest released version heading'
}
else {
    Add-Failure 'CHANGELOG latest released version heading was not found.'
}

if (-not [string]::IsNullOrWhiteSpace($TagName)) {
    $tagMatch = [regex]::Match($TagName, '^v(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?)$')
    if (-not $tagMatch.Success) {
        Add-Failure "Tag '$TagName' is not a supported release tag. Use vMAJOR.MINOR.PATCH with an optional prerelease suffix."
    }
    else {
        Assert-Equal $tagMatch.Groups['version'].Value $ExpectedVersion 'Git release tag version'
    }
}

if (-not [string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $packageDirectoryPath = Resolve-RepositoryPath $PackageDirectory
    if (-not (Test-Path -LiteralPath $packageDirectoryPath -PathType Container)) {
        Add-Failure "Package directory was not found: $PackageDirectory"
    }
    else {
        $packages = @(Get-ChildItem -LiteralPath $packageDirectoryPath -Filter '*.nupkg' -File)
        $expectedPackageName = "NetCoreApplicationTemplate.$ExpectedVersion.nupkg"
        $matchingPackages = @($packages | Where-Object { $_.Name -eq $expectedPackageName })
        $driftedPackages = @($packages | Where-Object { $_.Name -ne $expectedPackageName })

        if ($matchingPackages.Count -eq 0) {
            Add-Failure "No generated package named '$expectedPackageName' was found in $PackageDirectory."
        }

        foreach ($package in $driftedPackages) {
            Add-Failure "Generated package filename '$($package.Name)' does not match expected version '$ExpectedVersion'."
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Version consistency validation failed.'
    foreach ($failure in $failures) {
        Write-Host "::error::$failure"
        Write-Host "- $failure"
    }

    exit 1
}

Write-Host "Version consistency validation passed for version $ExpectedVersion."

    )

    if (-not $publishedTagMatch.Success) {
        Add-Failure 'README current-release tag was not found or was malformed.'
    }
    else {
        Assert-Equal $publishedTagMatch.Groups['tagVersion'].Value $publishedLabelVersion 'README current-release tag version'
    }
}

Assert-Matches $readme "$escapedPackageId\.$escapedVersion\.nupkg" 'README package install example'
Assert-Matches $readme "Version $escapedVersion\. Zenodo\. MIT License\." 'README citation version'

$packageReadme = Get-RequiredFileText 'PACKAGE-README.md'
Assert-Matches $packageReadme "$escapedPackageId\.$escapedVersion\.nupkg" 'PACKAGE-README package install example'

$citation = Get-RequiredFileText 'CITATION.cff'
$citationVersion = Get-RegexGroupValue $citation '(?m)^version:\s*["'']?(?<version>[^"''\r\n]+)["'']?\s*$' 'version' 'CITATION.cff version metadata'
if ($null -ne $citationVersion) {
    Assert-Equal $citationVersion $ExpectedVersion 'CITATION.cff version metadata'
}

$changelog = Get-RequiredFileText 'CHANGELOG.md'
$changelogMatch = [regex]::Match($changelog, '(?m)^##\s+(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?)\s+-\s+\d{4}-\d{2}-\d{2}\s*$')
if ($changelogMatch.Success) {
    Assert-Equal $changelogMatch.Groups['version'].Value $ExpectedVersion 'CHANGELOG latest released version heading'
}
else {
    Add-Failure 'CHANGELOG latest released version heading was not found.'
}

if (-not [string]::IsNullOrWhiteSpace($TagName)) {
    $tagMatch = [regex]::Match($TagName, '^v(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?)$')
    if (-not $tagMatch.Success) {
        Add-Failure "Tag '$TagName' is not a supported release tag. Use vMAJOR.MINOR.PATCH with an optional prerelease suffix."
    }
    else {
        Assert-Equal $tagMatch.Groups['version'].Value $ExpectedVersion 'Git release tag version'
    }
}

if (-not [string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $packageDirectoryPath = Resolve-RepositoryPath $PackageDirectory
    if (-not (Test-Path -LiteralPath $packageDirectoryPath -PathType Container)) {
        Add-Failure "Package directory was not found: $PackageDirectory"
    }
    else {
        $packages = @(Get-ChildItem -LiteralPath $packageDirectoryPath -Filter '*.nupkg' -File)
        $expectedPackageName = "NetCoreApplicationTemplate.$ExpectedVersion.nupkg"
        $matchingPackages = @($packages | Where-Object { $_.Name -eq $expectedPackageName })
        $driftedPackages = @($packages | Where-Object { $_.Name -ne $expectedPackageName })

        if ($matchingPackages.Count -eq 0) {
            Add-Failure "No generated package named '$expectedPackageName' was found in $PackageDirectory."
        }

        foreach ($package in $driftedPackages) {
            Add-Failure "Generated package filename '$($package.Name)' does not match expected version '$ExpectedVersion'."
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Version consistency validation failed.'
    foreach ($failure in $failures) {
        Write-Host "::error::$failure"
        Write-Host "- $failure"
    }

    exit 1
}

Write-Host "Version consistency validation passed for version $ExpectedVersion."
