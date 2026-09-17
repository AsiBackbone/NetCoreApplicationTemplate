[CmdletBinding()]
param(
    [string] $CoverageFile = "./artifacts/coverage-report/Cobertura.xml",
    [string] $ConfigFile = "./eng/security-critical-coverage.json",

    # The repository-wide line coverage gate. Security-critical files are a stricter gate, so no effective
    # per-file line floor may be lower than this value. Zero disables the check for local diagnostic runs.
    [ValidateRange(0, 100)]
    [double] $RepositoryLineCoverageThreshold = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-RepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $normalized = $Path.Replace("\", "/")

    if ($normalized.StartsWith("./", [StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }

    return $normalized.TrimStart("/")
}

function Convert-RateToPercentage {
    param(
        [AllowNull()]
        [string] $Rate
    )

    if ([string]::IsNullOrWhiteSpace($Rate)) {
        return 100.0
    }

    $parsed = [double]::Parse(
        $Rate,
        [System.Globalization.CultureInfo]::InvariantCulture)

    return [math]::Round($parsed * 100, 2)
}

function Get-ConfiguredThreshold {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject] $FileConfiguration,

        [Parameter(Mandatory = $true)]
        [string] $PropertyName,

        [Parameter(Mandatory = $true)]
        [double] $DefaultValue
    )

    $property = $FileConfiguration.PSObject.Properties[$PropertyName]

    if ($null -ne $property -and $null -ne $property.Value) {
        return [double] $property.Value
    }

    return $DefaultValue
}

if (-not (Test-Path -LiteralPath $CoverageFile -PathType Leaf)) {
    Write-Error "Coverage file was not found: $CoverageFile"
    exit 1
}

if (-not (Test-Path -LiteralPath $ConfigFile -PathType Leaf)) {
    Write-Error "Security-critical coverage configuration was not found: $ConfigFile"
    exit 1
}

[xml] $coverage = Get-Content -LiteralPath $CoverageFile -Raw
$config = Get-Content -LiteralPath $ConfigFile -Raw | ConvertFrom-Json

$defaultMinimumLineCoverage = [double] $config.defaultMinimumLineCoverage
$defaultMinimumBranchCoverage = [double] $config.defaultMinimumBranchCoverage
$protectedFiles = @($config.files)

if ($protectedFiles.Count -eq 0) {
    Write-Error "Security-critical coverage configuration does not contain any protected files."
    exit 1
}

$classes = @($coverage.SelectNodes("//class"))

if ($classes.Count -eq 0) {
    Write-Error "Coverage file does not contain any class-level coverage entries."
    exit 1
}

$failures = New-Object System.Collections.Generic.List[string]

Write-Host "Security-critical coverage gate"
Write-Host "--------------------------------"

if ($RepositoryLineCoverageThreshold -gt 0) {
    Write-Host ("Repository line coverage gate: {0:N2}%" -f $RepositoryLineCoverageThreshold)

    if ($defaultMinimumLineCoverage -lt $RepositoryLineCoverageThreshold) {
        $failures.Add(
            "defaultMinimumLineCoverage $defaultMinimumLineCoverage% is below the repository line coverage gate of $RepositoryLineCoverageThreshold%. Security-critical floors must not be weaker than the repository gate.")
    }
}

foreach ($protectedFile in $protectedFiles) {
    $expectedPath = Normalize-RepositoryPath -Path ([string] $protectedFile.path)

    $matchingClasses = @(
        $classes | Where-Object {
            $actualPath = Normalize-RepositoryPath -Path $_.GetAttribute("filename")

            $actualPath -eq $expectedPath -or
                $actualPath.EndsWith("/$expectedPath", [StringComparison]::Ordinal)
        }
    )

    if ($matchingClasses.Count -eq 0) {
        $failures.Add("Missing coverage entry for protected file '$expectedPath'.")
        continue
    }

    $minimumLineCoverage = Get-ConfiguredThreshold `
        -FileConfiguration $protectedFile `
        -PropertyName "minimumLineCoverage" `
        -DefaultValue $defaultMinimumLineCoverage

    $minimumBranchCoverage = Get-ConfiguredThreshold `
        -FileConfiguration $protectedFile `
        -PropertyName "minimumBranchCoverage" `
        -DefaultValue $defaultMinimumBranchCoverage

    if ($RepositoryLineCoverageThreshold -gt 0 -and $minimumLineCoverage -lt $RepositoryLineCoverageThreshold) {
        $failures.Add(
            "Protected file '$expectedPath' has a configured line floor of $minimumLineCoverage%, below the repository line coverage gate of $RepositoryLineCoverageThreshold%. Security-critical floors must not be weaker than the repository gate.")
    }

    if ($minimumBranchCoverage -lt $defaultMinimumBranchCoverage) {
        $failures.Add(
            "Protected file '$expectedPath' has a configured branch floor of $minimumBranchCoverage%, below defaultMinimumBranchCoverage of $defaultMinimumBranchCoverage%. Raise the file floor or change the default deliberately.")
    }

    $lineCoverage = @(
        $matchingClasses | ForEach-Object {
            Convert-RateToPercentage -Rate $_.GetAttribute("line-rate")
        }
    ) | Measure-Object -Minimum | Select-Object -ExpandProperty Minimum

    $branchCoverage = @(
        $matchingClasses | ForEach-Object {
            Convert-RateToPercentage -Rate $_.GetAttribute("branch-rate")
        }
    ) | Measure-Object -Minimum | Select-Object -ExpandProperty Minimum

    $status = if ($lineCoverage -ge $minimumLineCoverage -and $branchCoverage -ge $minimumBranchCoverage) {
        "PASS"
    }
    else {
        "FAIL"
    }

    Write-Host (
        "{0}: {1} | line {2:N2}% >= {3:N2}% | branch {4:N2}% >= {5:N2}%" -f
            $status,
            $expectedPath,
            $lineCoverage,
            $minimumLineCoverage,
            $branchCoverage,
            $minimumBranchCoverage)

    if ($lineCoverage -lt $minimumLineCoverage) {
        $failures.Add(
            "Protected file '$expectedPath' has line coverage $lineCoverage%, below required $minimumLineCoverage%.")
    }

    if ($branchCoverage -lt $minimumBranchCoverage) {
        $failures.Add(
            "Protected file '$expectedPath' has branch coverage $branchCoverage%, below required $minimumBranchCoverage%.")
    }
}

if ($failures.Count -gt 0) {
    Write-Error (
        "Security-critical coverage gate failed:`n - " +
        ($failures -join "`n - "))
    exit 1
}

Write-Host "Security-critical coverage gate passed."
