[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ScaffoldRoot,

    [Parameter(Mandatory = $true)]
    [ValidateSet('cookie', 'none')]
    [string]$AuthProvider,

    [Parameter(Mandatory = $true)]
    [ValidateSet('sqlite', 'sqlserver', 'none')]
    [string]$DbProvider
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-ToBoolean {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value,

        [Parameter(Mandatory = $true)]
        [string]$SettingName
    )

    if ($Value -is [bool]) {
        return $Value
    }

    $parsed = $false
    if ([bool]::TryParse([string]$Value, [ref]$parsed)) {
        return $parsed
    }

    throw "Generated setting '$SettingName' was not a Boolean value: '$Value'."
}

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [object]$Actual,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [object]$Expected,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ($Actual -ne $Expected) {
        throw "$Description Expected '$Expected' but found '$Actual'."
    }
}

$resolvedScaffoldRoot = (Resolve-Path -LiteralPath $ScaffoldRoot).Path
$projectName = Split-Path -Leaf $resolvedScaffoldRoot
$appsettingsPath = Join-Path $resolvedScaffoldRoot "src/$projectName.Web/appsettings.json"
$testRoot = Join-Path $resolvedScaffoldRoot "tests/$projectName.Web.Tests"
$migrationPath = Join-Path $resolvedScaffoldRoot "src/$projectName.Infrastructure/Data/Migrations"

if (-not (Test-Path -LiteralPath $appsettingsPath -PathType Leaf)) {
    throw "Generated appsettings.json was not found: $appsettingsPath"
}

if (-not (Test-Path -LiteralPath $testRoot -PathType Container)) {
    throw "Generated test project was not found: $testRoot"
}

$settings = Get-Content -LiteralPath $appsettingsPath -Raw | ConvertFrom-Json
$projectSettingsProperty = $settings.PSObject.Properties[$projectName]

if ($null -eq $projectSettingsProperty) {
    throw "Expected generated appsettings root '$projectName' was not found."
}

$projectSettings = $projectSettingsProperty.Value
$expectedAuthEnabled = $AuthProvider -eq 'cookie'
$authEnabled = Convert-ToBoolean `
    -Value $projectSettings.Authentication.Enabled `
    -SettingName "${projectName}:Authentication:Enabled"
$cookieEnabled = Convert-ToBoolean `
    -Value $projectSettings.Authentication.Cookie.Enabled `
    -SettingName "${projectName}:Authentication:Cookie:Enabled"
$authenticatedFallback = Convert-ToBoolean `
    -Value $projectSettings.Authorization.RequireAuthenticatedUserByDefault `
    -SettingName "${projectName}:Authorization:RequireAuthenticatedUserByDefault"

Assert-Equal `
    -Actual $authEnabled `
    -Expected $expectedAuthEnabled `
    -Description "${projectName}:Authentication:Enabled did not match authProvider=$AuthProvider."
Assert-Equal `
    -Actual $cookieEnabled `
    -Expected $expectedAuthEnabled `
    -Description "${projectName}:Authentication:Cookie:Enabled did not match authProvider=$AuthProvider."
Assert-Equal `
    -Actual $authenticatedFallback `
    -Expected $expectedAuthEnabled `
    -Description "${projectName}:Authorization:RequireAuthenticatedUserByDefault did not match authProvider=$AuthProvider."

$expectedDataProvider = switch ($DbProvider) {
    'sqlite' { 'Sqlite' }
    'sqlserver' { 'SqlServer' }
    'none' { 'None' }
}

$expectedConnectionStringName = switch ($DbProvider) {
    'sqlite' { 'ApplicationDatabase' }
    'sqlserver' { 'ApplicationSqlServer' }
    'none' { '' }
}

Assert-Equal `
    -Actual ([string]$projectSettings.DataAccess.Provider) `
    -Expected $expectedDataProvider `
    -Description "${projectName}:DataAccess:Provider did not match dbProvider=$DbProvider."
Assert-Equal `
    -Actual ([string]$projectSettings.DataAccess.ConnectionStringName) `
    -Expected $expectedConnectionStringName `
    -Description "${projectName}:DataAccess:ConnectionStringName did not match dbProvider=$DbProvider."

$migrationTests = @(
    Get-ChildItem `
        -LiteralPath $testRoot `
        -Filter '*MigrationTests.cs' `
        -File `
        -Recurse `
        -ErrorAction SilentlyContinue
)

$migrationReferences = @(
    Get-ChildItem `
        -LiteralPath $testRoot `
        -Filter '*.cs' `
        -File `
        -Recurse `
        -ErrorAction SilentlyContinue |
        Select-String -SimpleMatch "$projectName.Infrastructure.Data.Migrations"
)

switch ($DbProvider) {
    'sqlite' {
        if (-not (Test-Path -LiteralPath $migrationPath -PathType Container)) {
            throw 'SQLite scaffold did not include its migration history.'
        }

        $migrationFiles = @(
            Get-ChildItem `
                -LiteralPath $migrationPath `
                -Filter '*.cs' `
                -File `
                -Recurse `
                -ErrorAction SilentlyContinue
        )

        if ($migrationFiles.Count -eq 0) {
            throw 'SQLite scaffold migration directory did not contain migration source files.'
        }

        if ($migrationTests.Count -eq 0) {
            throw 'SQLite scaffold did not include migration-specific tests.'
        }

        if ($migrationReferences.Count -eq 0) {
            throw 'SQLite scaffold tests did not reference the generated migration namespace.'
        }
    }

    'sqlserver' {
        if (Test-Path -LiteralPath $migrationPath) {
            throw 'SQL Server scaffold must not include the SQLite migration history.'
        }

        if ($migrationTests.Count -ne 0) {
            throw "SQL Server scaffold must not include SQLite migration-specific tests: $($migrationTests.Name -join ', ')"
        }

        if ($migrationReferences.Count -ne 0) {
            throw 'SQL Server scaffold contains tests that reference the omitted SQLite migration namespace.'
        }
    }

    'none' {
        # The data-access-disabled contract is configuration-driven. The template
        # intentionally keeps the reusable EF Core source surface available for a
        # consumer that later enables or replaces persistence.
    }
}

Write-Host (
    "Template option validation passed for authProvider=$AuthProvider, " +
    "dbProvider=$DbProvider at $resolvedScaffoldRoot."
)
