[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Path = (Join-Path -Path $PSScriptRoot -ChildPath '../dependency-check-suppressions.xml'),

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $PackageVersionsPath = (Join-Path -Path $PSScriptRoot -ChildPath '../Directory.Packages.props'),

    [Parameter()]
    [ValidateRange(0, 3650)]
    [int] $ReviewWindowDays = 30,

    [Parameter()]
    [switch] $FailOnReviewWindow
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-SuppressionIdentity {
    param(
        [Parameter(Mandatory)]
        [System.Xml.XmlElement] $Suppression
    )

    $vulnerabilityNode = $Suppression.SelectSingleNode("*[local-name()='vulnerabilityName']")
    $packageUrlNode = $Suppression.SelectSingleNode("*[local-name()='packageUrl']")
    $vulnerability = if ($null -ne $vulnerabilityNode -and -not [string]::IsNullOrWhiteSpace($vulnerabilityNode.InnerText)) {
        $vulnerabilityNode.InnerText.Trim()
    }
    else {
        '<unknown vulnerability>'
    }
    $package = if ($null -ne $packageUrlNode -and -not [string]::IsNullOrWhiteSpace($packageUrlNode.InnerText)) {
        $packageUrlNode.InnerText.Trim()
    }
    else {
        '<unknown package>'
    }

    return "$vulnerability / $package"
}

function Write-ValidationMessage {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('warning', 'error')]
        [string] $Level,

        [Parameter(Mandatory)]
        [string] $Message,

        [Parameter(Mandatory)]
        [string] $FilePath
    )

    if ($env:GITHUB_ACTIONS -eq 'true') {
        $escapedMessage = $Message.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
        Write-Host "::$Level file=$FilePath::$escapedMessage"
        return
    }

    if ($Level -eq 'warning') {
        Write-Warning $Message
    }
    else {
        Write-Host "ERROR: $Message"
    }
}

$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
[xml] $document = Get-Content -LiteralPath $resolvedPath -Raw
$suppressions = @($document.SelectNodes("/*[local-name()='suppressions']/*[local-name()='suppress']"))

$resolvedPackageVersionsPath = (Resolve-Path -LiteralPath $PackageVersionsPath).Path
[xml] $packageVersionsDocument = Get-Content -LiteralPath $resolvedPackageVersionsPath -Raw
$packageVersions = @{}

foreach ($packageVersionNode in @($packageVersionsDocument.SelectNodes("//*[local-name()='PackageVersion']"))) {
    $packageName = $packageVersionNode.GetAttribute('Include').Trim()
    $packageVersion = $packageVersionNode.GetAttribute('Version').Trim()

    if (-not [string]::IsNullOrWhiteSpace($packageName) -and -not [string]::IsNullOrWhiteSpace($packageVersion)) {
        $packageVersions[$packageName] = $packageVersion
    }
}

if ($suppressions.Count -eq 0) {
    throw "No dependency-check suppressions were found in '$resolvedPath'."
}

$now = [DateTimeOffset]::UtcNow
$reviewThreshold = $now.AddDays($ReviewWindowDays)
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$exemptions = 0

foreach ($suppression in $suppressions) {
    $identity = Get-SuppressionIdentity -Suppression $suppression
    $notesNode = $suppression.SelectSingleNode("*[local-name()='notes']")
    $notes = if ($null -ne $notesNode) { $notesNode.InnerText.Trim() } else { '' }

    if ([string]::IsNullOrWhiteSpace($notes)) {
        $errors.Add("${identity}: suppression must include non-empty <notes> explaining the review rationale.")
        continue
    }

    $packageUrlNode = $suppression.SelectSingleNode("*[local-name()='packageUrl']")
    if ($null -ne $packageUrlNode -and $packageUrlNode.GetAttribute('regex') -eq 'true') {
        $exactPackageUrl = [regex]::Match(
            $packageUrlNode.InnerText.Trim(),
            '^\^pkg:nuget/(?<package>.+)@(?<version>[^@]+)\$$')

        if ($exactPackageUrl.Success) {
            $suppressedPackage = [regex]::Unescape($exactPackageUrl.Groups['package'].Value)
            $suppressedVersion = [regex]::Unescape($exactPackageUrl.Groups['version'].Value)

            if (-not $packageVersions.ContainsKey($suppressedPackage)) {
                $errors.Add("${identity}: exact NuGet suppression targets package '$suppressedPackage', which is not centrally pinned in '$resolvedPackageVersionsPath'. Remove the stale suppression or add the reviewed current package version.")
            }
            elseif ($packageVersions[$suppressedPackage] -ne $suppressedVersion) {
                $errors.Add("${identity}: exact NuGet suppression targets version '$suppressedVersion', but '$suppressedPackage' is centrally pinned to '$($packageVersions[$suppressedPackage])'. Review the finding and update or remove the stale suppression.")
            }
        }
    }

    $until = $suppression.GetAttribute('until').Trim()
    $expiryExemption = [regex]::Match($notes, '(?im)^\s*Expiry-Exempt:\s*(?<reason>\S.+)$')

    if ([string]::IsNullOrWhiteSpace($until)) {
        if (-not $expiryExemption.Success) {
            $errors.Add("${identity}: suppression is missing an 'until' expiry. Add an expiry or document an exemption in <notes> as 'Expiry-Exempt: <reason>'.")
            continue
        }

        $exemptions++
        Write-Host "Validated expiry exemption for ${identity}: $($expiryExemption.Groups['reason'].Value.Trim())"
        continue
    }

    $expiry = [DateTimeOffset]::MinValue
    $dateStyles = [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal
    $parsed = [DateTimeOffset]::TryParse(
        $until,
        [System.Globalization.CultureInfo]::InvariantCulture,
        $dateStyles,
        [ref] $expiry)

    if (-not $parsed) {
        $errors.Add("${identity}: suppression expiry '$until' is not a valid date/time value.")
        continue
    }

    $expiry = $expiry.ToUniversalTime()
    $expiryDisplay = $expiry.ToString('yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture)

    if ($expiry -le $now) {
        $errors.Add("${identity}: suppression expired on $expiryDisplay. Review the finding now and remove, replace, or deliberately renew the suppression with documented rationale.")
        continue
    }

    if ($expiry -le $reviewThreshold) {
        $reviewDate = $expiry.AddDays(-$ReviewWindowDays).ToString('yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture)
        $message = "${identity}: suppression expires on $expiryDisplay; the $ReviewWindowDays-day review window opened on $reviewDate. Review the finding before expiry."

        if ($FailOnReviewWindow) {
            $errors.Add($message)
        }
        else {
            $warnings.Add($message)
        }
    }
}

$repositoryRelativePath = [System.IO.Path]::GetFileName($resolvedPath)

foreach ($warning in $warnings) {
    Write-ValidationMessage -Level warning -Message $warning -FilePath $repositoryRelativePath
}

foreach ($validationError in $errors) {
    Write-ValidationMessage -Level error -Message $validationError -FilePath $repositoryRelativePath
}

if ($errors.Count -gt 0) {
    throw "Dependency-check suppression validation failed with $($errors.Count) error(s)."
}

Write-Host "Validated $($suppressions.Count) dependency-check suppression(s): $exemptions expiry exemption(s), $($warnings.Count) review warning(s), no expired suppressions."
