[CmdletBinding()]
param(
    [string]$MatrixPath = (Join-Path $PSScriptRoot 'template-option-matrix.json'),
    [string]$TemplatePath = (Join-Path $PSScriptRoot '../.template.config/template.json'),
    [string]$DocumentationPath = (Join-Path $PSScriptRoot '../docs/articles/template-option-matrix.md'),
    [switch]$EmitGitHubMatrix
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ChoiceValues {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Template,

        [Parameter(Mandatory = $true)]
        [string]$SymbolName
    )

    $symbol = $Template.symbols.PSObject.Properties[$SymbolName]
    if ($null -eq $symbol) {
        throw "Template symbol '$SymbolName' was not found."
    }

    return @($symbol.Value.choices | ForEach-Object { [string]$_.choice })
}

function Assert-SameSet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Actual,

        [Parameter(Mandatory = $true)]
        [string[]]$Expected,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $actualSorted = @($Actual | Sort-Object -Unique)
    $expectedSorted = @($Expected | Sort-Object -Unique)
    if (($actualSorted -join '|') -ne ($expectedSorted -join '|')) {
        throw "$Description Expected '$($expectedSorted -join ', ')' but found '$($actualSorted -join ', ')'."
    }
}

$matrix = Get-Content -LiteralPath $MatrixPath -Raw | ConvertFrom-Json
$template = Get-Content -LiteralPath $TemplatePath -Raw | ConvertFrom-Json

if ([int]$matrix.schemaVersion -ne 1) {
    throw "Unsupported template option matrix schema version '$($matrix.schemaVersion)'."
}

$templateAuthProviders = Get-ChoiceValues -Template $template -SymbolName 'authProvider'
$templateDbProviders = Get-ChoiceValues -Template $template -SymbolName 'dbProvider'
$matrixAuthProviders = @($matrix.authProviders | ForEach-Object { [string]$_ })
$matrixDbProviders = @($matrix.dbProviders | ForEach-Object { [string]$_ })

Assert-SameSet -Actual $matrixAuthProviders -Expected $templateAuthProviders `
    -Description 'authProvider matrix values do not match template.json.'
Assert-SameSet -Actual $matrixDbProviders -Expected $templateDbProviders `
    -Description 'dbProvider matrix values do not match template.json.'

$expectedKeys = @(
    foreach ($authProvider in $templateAuthProviders) {
        foreach ($dbProvider in $templateDbProviders) {
            "$authProvider|$dbProvider"
        }
    }
)
$actualKeys = @(
    $matrix.combinations | ForEach-Object {
        "$($_.authProvider)|$($_.dbProvider)"
    }
)

Assert-SameSet -Actual $actualKeys -Expected $expectedKeys `
    -Description 'The matrix must contain the complete authProvider x dbProvider cross product.'

if ($actualKeys.Count -ne (@($actualKeys | Sort-Object -Unique)).Count) {
    throw 'The template option matrix contains duplicate combinations.'
}

$requiredProperties = @(
    'authProvider',
    'dbProvider',
    'authenticationEnabled',
    'authenticatedFallback',
    'dataProvider',
    'connectionStringName',
    'migrationMode'
)

foreach ($combination in $matrix.combinations) {
    foreach ($propertyName in $requiredProperties) {
        if ($null -eq $combination.PSObject.Properties[$propertyName]) {
            throw "Combination '$($combination.authProvider)|$($combination.dbProvider)' is missing '$propertyName'."
        }
    }
}

$documentation = Get-Content -LiteralPath $DocumentationPath -Raw
foreach ($combination in $matrix.combinations) {
    $expectedRowPrefix = "| ``$($combination.authProvider)`` | ``$($combination.dbProvider)`` |"
    if (-not $documentation.Contains($expectedRowPrefix, [StringComparison]::Ordinal)) {
        throw "Documentation is missing matrix row '$expectedRowPrefix'."
    }
}

if ($EmitGitHubMatrix) {
    $githubMatrix = @(
        $matrix.combinations | ForEach-Object {
            [ordered]@{
                authProvider = [string]$_.authProvider
                dbProvider = [string]$_.dbProvider
            }
        }
    )
    Write-Output ($githubMatrix | ConvertTo-Json -Compress -Depth 4)
    exit 0
}

Write-Host "Template option matrix validation passed for $($actualKeys.Count) combinations."
