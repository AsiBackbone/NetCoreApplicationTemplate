[CmdletBinding()]
param(
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$agentDirectory = Join-Path $repositoryRoot '.github/agents'
$profiles = @(
    @{ File = 'security-assurance.agent.md'; Scope = 'Production error handling' },
    @{ File = 'quality-assurance.agent.md'; Scope = 'generated-project behavior' },
    @{ File = 'release-assurance.agent.md'; Scope = 'Template `.nupkg` contents' }
)

$requiredProfileText = @(
    'target: github-copilot',
    'disable-model-invocation: true',
    'user-invocable: true',
    '## Prohibited actions',
    'Do not edit files',
    'execute commands',
    'access secrets',
    'approve work',
    '## Review output',
    'severity',
    'file/line evidence',
    'actionable',
    'Uncertainties',
    'validation not performed',
    'Never fabricate'
)

$errors = [System.Collections.Generic.List[string]]::new()
$expectedFiles = @($profiles | ForEach-Object { $_.File })
$actualFiles = @(
    Get-ChildItem -LiteralPath $agentDirectory -File -Filter '*.agent.md' |
        ForEach-Object { $_.Name }
)

foreach ($unexpected in @($actualFiles | Where-Object { $_ -notin $expectedFiles })) {
    $errors.Add("Unexpected custom-agent profile is not governed by this validator: $unexpected")
}

foreach ($profile in $profiles) {
    $path = Join-Path $agentDirectory $profile.File
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $errors.Add("Missing custom-agent profile: $($profile.File)")
        continue
    }

    $content = Get-Content -LiteralPath $path -Raw
    $frontmatter = [regex]::Match(
        $content,
        '\A---\r?\n(?<yaml>.*?)\r?\n---\r?\n',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $frontmatter.Success) {
        $errors.Add("$($profile.File) must start with YAML frontmatter.")
    }
    else {
        $toolLines = @(
            $frontmatter.Groups['yaml'].Value -split '\r?\n' |
                Where-Object { $_ -match '^tools:' }
        )
        if ($toolLines.Count -ne 1 -or $toolLines[0] -cne 'tools: ["read", "search"]') {
            $errors.Add("$($profile.File) must declare exactly tools: [`"read`", `"search`"].")
        }
    }

    foreach ($required in $requiredProfileText + $profile.Scope) {
        if (-not $content.Contains($required, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("$($profile.File) is missing required text: $required")
        }
    }

    foreach ($forbidden in @('mcp-servers:', '"edit"', '"execute"', '"web"', '"agent"', 'tools: ["*"]')) {
        if ($content.Contains($forbidden, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("$($profile.File) contains forbidden tool or MCP configuration: $forbidden")
        }
    }
}

$agentsPath = Join-Path $repositoryRoot 'AGENTS.md'
if (-not (Test-Path -LiteralPath $agentsPath -PathType Leaf)) {
    $errors.Add('Missing root AGENTS.md.')
}
else {
    $agents = Get-Content -LiteralPath $agentsPath -Raw
    foreach ($required in @(
        'global.json',
        'Directory.Build.props',
        'migration tracker [#748]',
        'dotnet restore NetCoreApplicationTemplate.slnx --locked-mode',
        'dotnet build NetCoreApplicationTemplate.slnx --configuration Release --no-restore',
        'dotnet test --solution NetCoreApplicationTemplate.slnx --configuration Release --no-build --no-restore',
        'Repository governance is maintainer-only content',
        'ReferenceApplication',
        'read-only reviewers',
        'must not edit files',
        'fabricate test results'
    )) {
        if (-not $agents.Contains($required, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("AGENTS.md is missing required text: $required")
        }
    }
}

$guidePath = Join-Path $repositoryRoot 'docs/articles/repository-agent-assurance.md'
if (-not (Test-Path -LiteralPath $guidePath -PathType Leaf)) {
    $errors.Add('Missing repository assurance guide.')
}
else {
    $guide = Get-Content -LiteralPath $guidePath -Raw
    foreach ($required in @(
        'Example safe prompts',
        'Prohibited-action handling',
        'confirm the actual session',
        'configuration validation, not a live Copilot permission test',
        'Security Assurance',
        'Quality Assurance',
        'Release Assurance',
        'ReferenceApplication',
        'Learning#534'
    )) {
        if (-not $guide.Contains($required, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("Repository assurance guide is missing required text: $required")
        }
    }
}

$templateConfigPath = Join-Path $repositoryRoot '.template.config/template.json'
$templateConfig = Get-Content -LiteralPath $templateConfigPath -Raw | ConvertFrom-Json
$primarySource = @($templateConfig.sources | Where-Object { $_.source -eq './' })
if ($primarySource.Count -ne 1) {
    $errors.Add('template.json must contain exactly one primary repository source.')
}
else {
    foreach ($requiredExclusion in @('.github/**', 'AGENTS.md')) {
        if (@($primarySource[0].exclude) -notcontains $requiredExclusion) {
            $errors.Add("template.json must exclude repository agent guidance: $requiredExclusion")
        }
    }
}

$packageProjectPath = Join-Path $repositoryRoot 'eng/NetCoreApplicationTemplate.Template.csproj'
[xml]$packageProject = Get-Content -LiteralPath $packageProjectPath -Raw
foreach ($item in $packageProject.Project.ItemGroup.Content) {
    $include = [string]$item.Include
    if ($include.Contains('AGENTS.md', [System.StringComparison]::OrdinalIgnoreCase) -or
        $include.Contains('.github', [System.StringComparison]::OrdinalIgnoreCase) -or
        $include.Contains('docs/', [System.StringComparison]::OrdinalIgnoreCase) -or
        $include.Contains('docs\', [System.StringComparison]::OrdinalIgnoreCase)) {
        $errors.Add("Template package project includes repository-only agent guidance: $include")
    }
}

if (-not [string]::IsNullOrWhiteSpace($PackagePath)) {
    $resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackagePath)
    try {
        foreach ($entry in $archive.Entries) {
            $entryPath = $entry.FullName.Replace('\', '/')
            if ($entryPath -match '(^|/)AGENTS\.md$' -or
                $entryPath -match '(^|/)\.github/agents/' -or
                $entryPath -match 'repository-agent-assurance' -or
                $entryPath -match 'Validate-AgentGovernance') {
                $errors.Add("Packed template contains repository-only agent guidance: $entryPath")
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

$packageMessage = if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    'Static package and scaffold boundaries validated.'
}
else {
    'Packed template boundary validated.'
}
Write-Host "Validated $($profiles.Count) read-only assurance profiles. $packageMessage"
