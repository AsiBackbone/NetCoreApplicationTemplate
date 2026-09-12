[CmdletBinding(DefaultParameterSetName = 'Project')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Solution')]
    [string]$SolutionPath,

    [Parameter(Mandatory = $true, ParameterSetName = 'Project')]
    [string]$ProjectPath,

    [string]$Configuration = 'Release',

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [string]$Include,

    [string]$Exclude = '[*.Tests]*',

    [int]$LineThreshold = -1,

    [int]$BranchThreshold = -1,

    [switch]$NoBuild,

    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Resolve-RepositoryPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
}

function Get-TestProjectsFromSolution {
    param([Parameter(Mandatory = $true)][string]$Path)

    [xml]$solution = Get-Content -LiteralPath $Path -Raw
    $projects = @(
        $solution.SelectNodes('//Project[@Path]') |
            ForEach-Object { [string]$_.Path } |
            Where-Object { $_ -like 'tests/*' -and $_ -like '*.csproj' }
    )

    if ($projects.Count -eq 0) {
        throw ('No test projects were found in solution: ' + $Path)
    }

    return $projects
}

function Invoke-TestProjectCoverage {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ResultsDirectory
    )

    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

    # .NET 10 MTP argument parsing on Unix can misclassify rooted option values
    # as switch-like tokens. Invoke from the repository root and pass relative
    # paths so the same command line is portable across Windows and Linux.
    $projectArgument = [System.IO.Path]::GetRelativePath($RepositoryRoot, $Path)
    $resultsDirectoryArgument = [System.IO.Path]::GetRelativePath($RepositoryRoot, $ResultsDirectory)

    $arguments = @(
        'test',
        '--project',
        $projectArgument,
        '--configuration',
        $Configuration,
        '--verbosity',
        'normal',
        '--results-directory',
        $resultsDirectoryArgument
    )

    if ($NoRestore) { $arguments += '--no-restore' }
    if ($NoBuild) { $arguments += '--no-build' }

    # Keep Coverlet extension switches out of the dotnet-test parser. Supply
    # them through the MSBuild property so the test application receives them
    # after project discovery under Microsoft.Testing.Platform.
    $coverletArguments = @(
        '--coverlet',
        '--coverlet-output-format',
        'cobertura',
        '--coverlet-file-prefix',
        $projectName,
        '--coverlet-exclude-assemblies-without-sources',
        'MissingAll'
    )

    if (-not [string]::IsNullOrWhiteSpace($Include)) {
        $coverletArguments += @('--coverlet-include', $Include)
    }
    if (-not [string]::IsNullOrWhiteSpace($Exclude)) {
        $coverletArguments += @('--coverlet-exclude', $Exclude)
    }

    $testingPlatformCommandLineArguments = $coverletArguments -join ' '
    $arguments += "--property:TestingPlatformCommandLineArguments=$testingPlatformCommandLineArguments"

    Write-Host ('Running MTP coverage for ' + $Path)
    Push-Location $RepositoryRoot
    try {
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw ('Tests failed for ' + $Path + ' with exit code ' + $LASTEXITCODE + '.')
        }
    }
    finally {
        Pop-Location
    }
}

function Assert-CoverageThreshold {
    param([Parameter(Mandatory = $true)][string]$CoveragePath)

    [xml]$coverage = Get-Content -LiteralPath $CoveragePath -Raw
    $root = $coverage.DocumentElement
    if ($null -eq $root -or $root.Name -ne 'coverage') {
        throw ('Coverage report does not contain a Cobertura coverage root: ' + $CoveragePath)
    }

    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    $numberStyles = [System.Globalization.NumberStyles]::Float
    $lineRateText = $root.GetAttribute('line-rate')
    $branchRateText = $root.GetAttribute('branch-rate')

    if ([string]::IsNullOrWhiteSpace($lineRateText)) {
        throw ('Cobertura report is missing the line-rate attribute: ' + $CoveragePath)
    }

    if ([string]::IsNullOrWhiteSpace($branchRateText)) {
        throw ('Cobertura report is missing the branch-rate attribute: ' + $CoveragePath)
    }

    $lineRateValue = 0.0
    $branchRateValue = 0.0

    if (-not [double]::TryParse($lineRateText, $numberStyles, $culture, [ref]$lineRateValue)) {
        throw ('Cobertura line-rate is not a valid invariant-culture number: ' + $lineRateText)
    }

    if (-not [double]::TryParse($branchRateText, $numberStyles, $culture, [ref]$branchRateValue)) {
        throw ('Cobertura branch-rate is not a valid invariant-culture number: ' + $branchRateText)
    }

    $lineRate = $lineRateValue * 100.0
    $branchRate = $branchRateValue * 100.0
    Write-Host ('Line coverage: {0:N2}%' -f $lineRate)
    Write-Host ('Branch coverage: {0:N2}%' -f $branchRate)

    if ($LineThreshold -ge 0 -and $lineRate -lt $LineThreshold) {
        throw ('Line coverage {0:N2}% is below the required threshold of {1}%.' -f $lineRate, $LineThreshold)
    }
    if ($BranchThreshold -ge 0 -and $branchRate -lt $BranchThreshold) {
        throw ('Branch coverage {0:N2}% is below the required threshold of {1}%.' -f $branchRate, $BranchThreshold)
    }
}

$outputRootAbsolute = Resolve-RepositoryPath -Path $OutputRoot
if (Test-Path -LiteralPath $outputRootAbsolute) {
    Remove-Item -LiteralPath $outputRootAbsolute -Recurse -Force
}
New-Item -ItemType Directory -Path $outputRootAbsolute -Force | Out-Null

$testProjects = if ($PSCmdlet.ParameterSetName -eq 'Solution') {
    Get-TestProjectsFromSolution -Path (Resolve-RepositoryPath -Path $SolutionPath)
}
else {
    @($ProjectPath)
}

foreach ($testProject in $testProjects) {
    $projectAbsolute = Resolve-RepositoryPath -Path $testProject
    if (-not (Test-Path -LiteralPath $projectAbsolute)) {
        throw ('Test project was not found: ' + $testProject)
    }

    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectAbsolute)
    $projectOutput = Join-Path $outputRootAbsolute $projectName
    Invoke-TestProjectCoverage -Path $projectAbsolute -ResultsDirectory $projectOutput
}

$reports = @(
    Get-ChildItem -Path $outputRootAbsolute -Filter '*.cobertura*.xml' -File -Recurse |
        Sort-Object FullName
)
if ($reports.Count -eq 0) {
    throw ('No Cobertura coverage reports were generated under ' + $outputRootAbsolute + '.')
}

$finalCoveragePath = Join-Path $outputRootAbsolute 'coverage.cobertura.xml'
if ($reports.Count -eq 1) {
    Copy-Item -LiteralPath $reports[0].FullName -Destination $finalCoveragePath -Force
}
else {
    $mergeDirectory = Join-Path $outputRootAbsolute 'merged'
    New-Item -ItemType Directory -Path $mergeDirectory -Force | Out-Null
    $reportList = ($reports | ForEach-Object { $_.FullName }) -join ';'

    & dotnet reportgenerator ('-reports:' + $reportList) ('-targetdir:' + $mergeDirectory) '-reporttypes:Cobertura'
    if ($LASTEXITCODE -ne 0) {
        throw ('ReportGenerator failed with exit code ' + $LASTEXITCODE + '.')
    }

    $mergedCoveragePath = Join-Path $mergeDirectory 'Cobertura.xml'
    if (-not (Test-Path -LiteralPath $mergedCoveragePath)) {
        throw ('Merged Cobertura report was not generated: ' + $mergedCoveragePath)
    }
    Copy-Item -LiteralPath $mergedCoveragePath -Destination $finalCoveragePath -Force
}

Assert-CoverageThreshold -CoveragePath $finalCoveragePath
Write-Host $finalCoveragePath
