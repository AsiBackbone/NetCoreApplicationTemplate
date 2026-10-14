[CmdletBinding()]
param(
    [string]$WorkflowDirectory,
    [switch]$SelfTest
)

# Fails when a DocFX build in this repository's GitHub Actions workflows does not treat warnings as errors.
# Every pull-request, release, and publication build must stay strict (AsiBackbone/.github
# LANDING-PAGE-STANDARD.md, Strict DocFX builds; AsiBackbone/.github#20).

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

# Workflows that must build DocFX with --warningsAsErrors: pull-request link validation and publication.
$requiredStrictWorkflows = @(
    'link-validation.yml'
    'publish-docs.yml'
)

function Get-DocFxBuildCommands {
    param([AllowEmptyString()][string]$Content)

    # Join shell (\) and PowerShell (`) line continuations so a flag on a continuation line is seen.
    $logicalLines = [System.Collections.Generic.List[string]]::new()
    $pending = ''
    foreach ($line in ($Content -split "`r?`n")) {
        if ($line -match '^\s*#') {
            continue
        }

        if ($line -match '(\\|`)\s*$') {
            $pending += ($line -replace '(\\|`)\s*$', ' ')
            continue
        }

        $logicalLines.Add($pending + $line)
        $pending = ''
    }

    if ($pending) {
        $logicalLines.Add($pending)
    }

    foreach ($logicalLine in $logicalLines) {
        if ($logicalLine -match '(?i)\bdocfx(?:\s+--)?(?:\s+build)?\s+\S*docfx\.json\b') {
            [regex]::Replace($logicalLine.Trim(), '^-?\s*run:\s*', '')
        }
    }
}

function Test-StrictDocFxCommand {
    param([string]$Command)

    return $Command -match '(?<!\S)--warningsAsErrors(?![\w-])' -and
        $Command -notmatch '(?i)(?<!\S)--warningsAsErrors(?:\s+|=)false\b'
}

function Test-WorkflowStrictness {
    param(
        [string]$Name,
        [AllowEmptyString()][string]$Content,
        [bool]$Required,
        [System.Collections.Generic.List[string]]$Errors
    )

    $commands = @(Get-DocFxBuildCommands $Content)
    foreach ($command in $commands) {
        if (-not (Test-StrictDocFxCommand $command)) {
            $Errors.Add("$Name builds DocFX without failing on warnings; add --warningsAsErrors: $command")
        }
    }

    if ($Required -and $commands.Count -eq 0) {
        $Errors.Add("$Name must build DocFX with --warningsAsErrors, but no DocFX build was found.")
    }
}

function Invoke-SelfTest {
    $strict = "      - name: Build DocFX site`n        run: dotnet tool run docfx -- docs/docfx.json --warningsAsErrors"
    $cases = @(
        @{ Name = 'strict build'; Content = $strict; Required = $true; Pass = $true }
        @{ Name = 'non-strict build'; Content = $strict.Replace(' --warningsAsErrors', ''); Required = $true; Pass = $false }
        @{ Name = 'flag explicitly disabled'; Content = $strict.Replace('--warningsAsErrors', '--warningsAsErrors false'); Required = $true; Pass = $false }
        @{ Name = 'flag on a continuation line'; Content = "        run: |`n          dotnet tool run docfx -- docs/docfx.json \`n            --warningsAsErrors"; Required = $true; Pass = $true }
        @{ Name = 'flag on a PowerShell continuation line'; Content = "        run: |`n          dotnet tool run docfx -- docs/docfx.json ``n            --warningsAsErrors"; Required = $true; Pass = $true }
        @{ Name = 'required workflow without a build'; Content = "      - name: Test`n        run: dotnet test"; Required = $true; Pass = $false }
        @{ Name = 'path filter is not a build'; Content = "    paths:`n      - 'docs/docfx.json'"; Required = $false; Pass = $true }
        @{ Name = 'commented-out build is ignored'; Content = "      # run: dotnet tool run docfx -- docs/docfx.json`n$strict"; Required = $true; Pass = $true }
        @{ Name = 'one strict and one non-strict build'; Content = "$strict`n        run: dotnet tool run docfx docs/docfx.json"; Required = $true; Pass = $false }
        @{ Name = 'similar flag name'; Content = $strict.Replace('--warningsAsErrors', '--warningsAsErrorsList'); Required = $true; Pass = $false }
    )

    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($case in $cases) {
        $caseErrors = [System.Collections.Generic.List[string]]::new()
        Test-WorkflowStrictness -Name 'fixture.yml' -Content $case.Content -Required $case.Required -Errors $caseErrors
        if (($caseErrors.Count -eq 0) -ne $case.Pass) {
            $expected = if ($case.Pass) { 'success' } else { 'failure' }
            $failures.Add("Self-test '$($case.Name)' expected $expected but got $($caseErrors.Count) error(s): $($caseErrors -join ' ')")
        }
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
        exit 1
    }

    Write-Host 'Strict DocFX build validator self-test passed.'
    exit 0
}

if ($SelfTest) {
    Invoke-SelfTest
}

if (-not $WorkflowDirectory) {
    $WorkflowDirectory = Join-Path $repoRoot '.github/workflows'
}

$errors = [System.Collections.Generic.List[string]]::new()
$workflowFiles = @(Get-ChildItem -LiteralPath $WorkflowDirectory -File | Where-Object { $_.Extension -in '.yml', '.yaml' })
$buildCount = 0

foreach ($requiredWorkflow in $requiredStrictWorkflows) {
    if (-not ($workflowFiles | Where-Object { $_.Name -eq $requiredWorkflow })) {
        $errors.Add("Required DocFX workflow is missing: .github/workflows/$requiredWorkflow")
    }
}

foreach ($workflowFile in $workflowFiles) {
    $content = Get-Content -LiteralPath $workflowFile.FullName -Raw
    $buildCount += @(Get-DocFxBuildCommands $content).Count
    Test-WorkflowStrictness -Name ".github/workflows/$($workflowFile.Name)" -Content $content -Required ($requiredStrictWorkflows -contains $workflowFile.Name) -Errors $errors
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Host "All $buildCount DocFX workflow build(s) fail on warnings."
