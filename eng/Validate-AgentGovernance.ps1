[CmdletBinding()]
param(
    [string]$PackagePath,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
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

function Get-AgentGovernanceErrors {
param(
    [Parameter(Mandatory)]
    [string]$Root,
    [string]$Package
)

$errors = [System.Collections.Generic.List[string]]::new()
$agentDirectory = Join-Path $Root '.github/agents'
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

$agentsPath = Join-Path $Root 'AGENTS.md'
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

$guidePath = Join-Path $Root 'docs/articles/repository-agent-assurance.md'
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

$templateConfigPath = Join-Path $Root '.template.config/template.json'
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

$scaffoldManifestPath = Join-Path $Root 'eng/scaffold-manifest.default.json'
if (-not (Test-Path -LiteralPath $scaffoldManifestPath -PathType Leaf)) {
    $errors.Add('Missing default scaffold manifest.')
}
else {
    $scaffoldManifest = Get-Content -LiteralPath $scaffoldManifestPath -Raw | ConvertFrom-Json
    foreach ($requiredForbiddenPath in @('.github', 'AGENTS.md')) {
        if (@($scaffoldManifest.forbiddenPaths) -notcontains $requiredForbiddenPath) {
            $errors.Add("Default scaffold manifest must forbid repository agent guidance: $requiredForbiddenPath")
        }
    }
}

$packageProjectPath = Join-Path $Root 'eng/NetCoreApplicationTemplate.Template.csproj'
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

if (-not [string]::IsNullOrWhiteSpace($Package)) {
    $resolvedPackagePath = (Resolve-Path -LiteralPath $Package).Path
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

return @($errors)
}

function Invoke-AgentGovernanceSelfTest {
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "ncat-agent-governance-$([guid]::NewGuid())"
    $script:assertions = 0
    $script:fixtureNumber = 0
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    function New-GovernanceFixture {
        $script:fixtureNumber++
        $root = Join-Path $fixtureRoot "fixture-$script:fixtureNumber"
        New-Item -ItemType Directory -Path (Join-Path $root '.github/agents') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $root 'docs/articles') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $root '.template.config') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $root 'eng') -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $repositoryRoot 'AGENTS.md') -Destination $root
        Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs/articles/repository-agent-assurance.md') -Destination (Join-Path $root 'docs/articles')
        Copy-Item -LiteralPath (Join-Path $repositoryRoot '.template.config/template.json') -Destination (Join-Path $root '.template.config')
        Copy-Item -LiteralPath (Join-Path $repositoryRoot 'eng/NetCoreApplicationTemplate.Template.csproj') -Destination (Join-Path $root 'eng')
        Copy-Item -LiteralPath (Join-Path $repositoryRoot 'eng/scaffold-manifest.default.json') -Destination (Join-Path $root 'eng')
        foreach ($profile in $profiles) {
            Copy-Item -LiteralPath (Join-Path $repositoryRoot ".github/agents/$($profile.File)") -Destination (Join-Path $root '.github/agents')
        }
        return $root
    }

    function New-TestPackage {
        param(
            [Parameter(Mandatory)]
            [string]$Root,
            [Parameter(Mandatory)]
            [string]$EntryPath
        )

        $path = Join-Path $Root 'fixture.nupkg'
        $archive = [System.IO.Compression.ZipFile]::Open($path, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            [void]$archive.CreateEntry($EntryPath)
        }
        finally {
            $archive.Dispose()
        }
        return $path
    }

    function Assert-RejectedFixture {
        param(
            [Parameter(Mandatory)]
            [scriptblock]$Mutation,
            [Parameter(Mandatory)]
            [string]$ExpectedError
        )

        $root = New-GovernanceFixture
        & $Mutation $root
        $actualErrors = @(Get-AgentGovernanceErrors -Root $root)
        $script:assertions++
        if (-not ($actualErrors | Where-Object { $_.Contains($ExpectedError, [System.StringComparison]::OrdinalIgnoreCase) })) {
            throw "Self-test expected an error containing '$ExpectedError', but received: $($actualErrors -join ' | ')"
        }
    }

    try {
        $validRoot = New-GovernanceFixture
        $validPackage = New-TestPackage -Root $validRoot -EntryPath 'content/README.md'
        $validErrors = @(Get-AgentGovernanceErrors -Root $validRoot -Package $validPackage)
        $script:assertions++
        if ($validErrors.Count -ne 0) {
            throw "The unmodified self-test fixture must be valid: $($validErrors -join ' | ')"
        }

        Assert-RejectedFixture -ExpectedError 'Missing custom-agent profile' -Mutation {
            param($root)
            Remove-Item -LiteralPath (Join-Path $root '.github/agents/security-assurance.agent.md')
        }

        Assert-RejectedFixture -ExpectedError 'must start with YAML frontmatter' -Mutation {
            param($root)
            $path = Join-Path $root '.github/agents/quality-assurance.agent.md'
            $content = Get-Content -LiteralPath $path -Raw
            Set-Content -LiteralPath $path -Value ([regex]::Replace($content, '\A---', 'malformed-frontmatter', 1))
        }

        Assert-RejectedFixture -ExpectedError 'must declare exactly tools' -Mutation {
            param($root)
            $path = Join-Path $root '.github/agents/release-assurance.agent.md'
            $content = Get-Content -LiteralPath $path -Raw
            Set-Content -LiteralPath $path -Value $content.Replace('tools: ["read", "search"]', 'tools: ["read", "search", "edit"]')
        }

        Assert-RejectedFixture -ExpectedError 'contains forbidden tool or MCP configuration: mcp-servers:' -Mutation {
            param($root)
            $path = Join-Path $root '.github/agents/security-assurance.agent.md'
            Add-Content -LiteralPath $path -Value "`nmcp-servers:`n  external: enabled"
        }

        Assert-RejectedFixture -ExpectedError 'AGENTS.md is missing required text: Repository governance is maintainer-only content' -Mutation {
            param($root)
            $path = Join-Path $root 'AGENTS.md'
            $content = Get-Content -LiteralPath $path -Raw
            Set-Content -LiteralPath $path -Value $content.Replace('Repository governance is maintainer-only content', 'Repository guidance')
        }

        Assert-RejectedFixture -ExpectedError 'template.json must exclude repository agent guidance: AGENTS.md' -Mutation {
            param($root)
            $path = Join-Path $root '.template.config/template.json'
            $config = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            $primarySource = @($config.sources | Where-Object { $_.source -eq './' })[0]
            $primarySource.exclude = @($primarySource.exclude | Where-Object { $_ -ne 'AGENTS.md' })
            $config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $path
        }

        Assert-RejectedFixture -ExpectedError 'Default scaffold manifest must forbid repository agent guidance: .github' -Mutation {
            param($root)
            $path = Join-Path $root 'eng/scaffold-manifest.default.json'
            $manifest = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            $manifest.forbiddenPaths = @($manifest.forbiddenPaths | Where-Object { $_ -ne '.github' })
            $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path
        }

        Assert-RejectedFixture -ExpectedError 'Template package project includes repository-only agent guidance' -Mutation {
            param($root)
            $path = Join-Path $root 'eng/NetCoreApplicationTemplate.Template.csproj'
            $content = Get-Content -LiteralPath $path -Raw
            $injected = '  <ItemGroup><Content Include="$(TemplateRepositoryRoot)/AGENTS.md" Pack="true" PackagePath="content/" /></ItemGroup>'
            Set-Content -LiteralPath $path -Value $content.Replace('</Project>', "$injected`n</Project>")
        }

        $packageRoot = New-GovernanceFixture
        $forbiddenPackage = New-TestPackage -Root $packageRoot -EntryPath 'content/AGENTS.md'
        $packageErrors = @(Get-AgentGovernanceErrors -Root $packageRoot -Package $forbiddenPackage)
        $script:assertions++
        if (-not ($packageErrors | Where-Object { $_ -like '*Packed template contains repository-only agent guidance*' })) {
            throw "Self-test expected the forbidden package entry to be rejected, but received: $($packageErrors -join ' | ')"
        }

        Write-Host "Agent governance validator self-test passed ($script:assertions assertions)."
    }
    finally {
        if (Test-Path -LiteralPath $fixtureRoot) {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
    }
}

if ($SelfTest) {
    Invoke-AgentGovernanceSelfTest
    exit 0
}

$errors = @(Get-AgentGovernanceErrors -Root $repositoryRoot -Package $PackagePath)
if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error -Message $_ -ErrorAction Continue }
    exit 1
}

$packageMessage = if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    'Static package and scaffold boundaries validated.'
}
else {
    'Packed template boundary validated.'
}
Write-Host "Validated $($profiles.Count) read-only assurance profiles. $packageMessage"
