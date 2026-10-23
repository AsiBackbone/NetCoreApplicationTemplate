[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository,

    [string]$ControlsDirectory = (Join-Path -Path $PSScriptRoot -ChildPath '../eng/repository-controls'),

    [switch]$FailOnUnavailable,

    [string]$SummaryPath,

    [switch]$SelfTest
)

# Read-only drift audit for the GitHub-hosted repository controls described in
# eng/repository-controls. The script only issues HTTP GET requests through
# Invoke-GitHubGet and has no mode that changes repository settings. Changing a
# live control remains a deliberate maintainer action in the GitHub UI or API.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ScriptPath = $PSCommandPath
$script:GitHubApiVersion = '2026-03-10'
$script:AbsentMarker = '<absent>'
$script:ForbiddenRulesetManifestKeys = @('id', 'source', 'source_type', 'node_id', 'created_at', 'updated_at', 'current_user_can_bypass', '_links')

function Get-OptionalProperty {
    param(
        [AllowNull()]
        [object]$InputObject,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $InputObject) {
        return [pscustomobject]@{ Present = $false; Value = $null }
    }

    if ($InputObject -is [System.Collections.IDictionary]) {
        if ($InputObject.Contains($Name)) {
            return [pscustomobject]@{ Present = $true; Value = $InputObject[$Name] }
        }

        return [pscustomobject]@{ Present = $false; Value = $null }
    }

    if ($InputObject -is [System.Management.Automation.PSCustomObject]) {
        $property = $InputObject.PSObject.Properties[$Name]
        if ($null -ne $property) {
            return [pscustomobject]@{ Present = $true; Value = $property.Value }
        }
    }

    return [pscustomobject]@{ Present = $false; Value = $null }
}

function Get-Items {
    param(
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value) {
        return
    }

    if (Test-IsArray -Value $Value) {
        foreach ($item in $Value) {
            $item
        }

        return
    }

    $Value
}

function Test-IsObject {
    param(
        [AllowNull()]
        [object]$Value
    )

    return ($Value -is [System.Collections.IDictionary]) -or ($Value -is [System.Management.Automation.PSCustomObject])
}

function Test-IsArray {
    param(
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value -or $Value -is [string] -or (Test-IsObject -Value $Value)) {
        return $false
    }

    return $Value -is [System.Collections.IEnumerable]
}

function Get-PropertyNames {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    if ($Value -is [System.Collections.IDictionary]) {
        return @($Value.Keys | ForEach-Object { [string]$_ })
    }

    return @($Value.PSObject.Properties | ForEach-Object { $_.Name })
}

function Get-SortedText {
    param(
        [AllowEmptyCollection()]
        [string[]]$Values
    )

    $sorted = [string[]]@($Values | Where-Object { $null -ne $_ })
    [System.Array]::Sort($sorted, [System.StringComparer]::Ordinal)
    return , $sorted
}

function ConvertTo-CanonicalText {
    <#
    Produces an order-insensitive, comparable text form of a JSON-like value.
    When a shape is supplied, objects are projected onto the shape's property
    names so that properties GitHub adds to live responses are ignored, while a
    property the shape expects but the value lacks is rendered as absent.
    #>
    param(
        [AllowNull()]
        [object]$Value,

        [AllowNull()]
        [object]$Shape,

        [bool]$HasShape = $false
    )

    if ($null -eq $Value) {
        return 'null'
    }

    if ($Value -is [string]) {
        return (ConvertTo-Json -InputObject $Value -Compress)
    }

    if ($Value -is [bool]) {
        if ($Value) {
            return 'true'
        }

        return 'false'
    }

    if ($Value -is [System.ValueType]) {
        return [System.Convert]::ToString($Value, [System.Globalization.CultureInfo]::InvariantCulture)
    }

    if (Test-IsObject -Value $Value) {
        $useShape = $HasShape -and (Test-IsObject -Value $Shape)
        $names = if ($useShape) { Get-PropertyNames -Value $Shape } else { Get-PropertyNames -Value $Value }
        $parts = [System.Collections.Generic.List[string]]::new()

        foreach ($name in (Get-SortedText -Values $names)) {
            $property = Get-OptionalProperty -InputObject $Value -Name $name
            $nameText = ConvertTo-Json -InputObject $name -Compress

            if (-not $property.Present) {
                $parts.Add("${nameText}:$($script:AbsentMarker)")
                continue
            }

            $childShape = $null
            $childHasShape = $false
            if ($useShape) {
                $shapeProperty = Get-OptionalProperty -InputObject $Shape -Name $name
                $childShape = $shapeProperty.Value
                $childHasShape = $shapeProperty.Present
            }

            $parts.Add("${nameText}:$(ConvertTo-CanonicalText -Value $property.Value -Shape $childShape -HasShape $childHasShape)")
        }

        return '{' + ($parts -join ',') + '}'
    }

    if (Test-IsArray -Value $Value) {
        $elementShape = $null
        $hasElementShape = $false

        if ($HasShape -and (Test-IsArray -Value $Shape)) {
            $shapeObjects = @(Get-Items -Value $Shape | Where-Object { Test-IsObject -Value $_ })
            if ($shapeObjects.Count -gt 0) {
                $elementShape = [ordered]@{}
                foreach ($shapeObject in $shapeObjects) {
                    foreach ($name in (Get-PropertyNames -Value $shapeObject)) {
                        if (-not $elementShape.Contains($name)) {
                            $elementShape[$name] = (Get-OptionalProperty -InputObject $shapeObject -Name $name).Value
                        }
                    }
                }

                $hasElementShape = $true
            }
        }

        $items = [System.Collections.Generic.List[string]]::new()
        foreach ($item in (Get-Items -Value $Value)) {
            $items.Add((ConvertTo-CanonicalText -Value $item -Shape $elementShape -HasShape $hasElementShape))
        }

        return '[' + ((Get-SortedText -Values $items.ToArray()) -join ',') + ']'
    }

    return (ConvertTo-Json -InputObject ([string]$Value) -Compress)
}

function Test-DesiredValue {
    param(
        [AllowNull()]
        [object]$Desired,

        [AllowNull()]
        [object]$Actual
    )

    $expectedText = ConvertTo-CanonicalText -Value $Desired -Shape $Desired -HasShape $true
    $actualText = ConvertTo-CanonicalText -Value $Actual -Shape $Desired -HasShape $true
    return $expectedText -ceq $actualText
}

function New-ApiResult {
    param(
        [bool]$Available,

        [AllowNull()]
        [object]$Value,

        [string]$Reason = ''
    )

    return [pscustomobject]@{
        Available = $Available
        Value = $Value
        Reason = $Reason
    }
}

function New-ControlResult {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Control,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Required', 'Preferred')]
        [string]$Level,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Match', 'Drift', 'Missing', 'Unavailable')]
        [string]$Status,

        [string]$Expected = '',

        [string]$Actual = '',

        [string]$Detail = ''
    )

    return [pscustomobject]@{
        Control = $Control
        Level = $Level
        Status = $Status
        Expected = $Expected
        Actual = $Actual
        Detail = $Detail
    }
}

function Get-GitHubFailureClassification {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ExitCode,

        [AllowEmptyString()]
        [string]$ErrorText
    )

    if ($ExitCode -eq 0) {
        return 'Success'
    }

    if ($ErrorText -match '(?i)HTTP\s+(403|404)\b') {
        return 'Unavailable'
    }

    return 'Error'
}

function Invoke-GitHubGet {
    <#
    The only GitHub access path in this script. It always sends an explicit
    GET with no request body or fields, so it cannot modify repository state.
    HTTP 403/404 responses are reported as unavailable rather than compliant.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Endpoint
    )

    if ($Endpoint -notmatch '^repos/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(/[A-Za-z0-9_./-]+)?(\?[A-Za-z0-9_=&-]+)?$') {
        throw "Refusing unexpected GitHub API endpoint '$Endpoint'."
    }

    $stderrPath = [System.IO.Path]::GetTempFileName()

    try {
        $ghArguments = @(
            'api',
            '--method',
            'GET',
            '-H',
            'Accept: application/vnd.github+json',
            '-H',
            "X-GitHub-Api-Version: $($script:GitHubApiVersion)",
            $Endpoint
        )

        $output = @(& gh @ghArguments 2> $stderrPath)
        $exitCode = $LASTEXITCODE
        $errorText = ''
        if (Test-Path -LiteralPath $stderrPath) {
            $errorText = [string](Get-Content -LiteralPath $stderrPath -Raw)
        }

        switch (Get-GitHubFailureClassification -ExitCode $exitCode -ErrorText $errorText) {
            'Unavailable' {
                return New-ApiResult -Available $false -Value $null -Reason "GET $Endpoint was not permitted or not found: $($errorText.Trim())"
            }
            'Error' {
                $message = if ([string]::IsNullOrWhiteSpace($errorText)) { "exit code $exitCode" } else { $errorText.Trim() }
                throw "GitHub API GET '$Endpoint' failed: $message"
            }
        }

        $json = $output -join [System.Environment]::NewLine
        if ([string]::IsNullOrWhiteSpace($json)) {
            return New-ApiResult -Available $true -Value $null
        }

        return New-ApiResult -Available $true -Value (ConvertFrom-Json -InputObject $json -NoEnumerate)
    }
    finally {
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Assert-ControlManifest {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Settings,

        [Parameter(Mandatory = $true)]
        [object]$Ruleset
    )

    $errors = [System.Collections.Generic.List[string]]::new()

    if ((Get-OptionalProperty -InputObject $Settings -Name 'schemaVersion').Value -ne 1) {
        $errors.Add('repository-settings.json must declare schemaVersion 1.')
    }

    $repositoryName = [string](Get-OptionalProperty -InputObject $Settings -Name 'repository').Value
    if ($repositoryName -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
        $errors.Add('repository-settings.json must name the repository as owner/name.')
    }

    $securityAndAnalysis = (Get-OptionalProperty -InputObject $Settings -Name 'securityAndAnalysis').Value
    foreach ($levelName in @('required', 'preferred')) {
        $features = (Get-OptionalProperty -InputObject $securityAndAnalysis -Name $levelName).Value
        if ($null -eq $features) {
            continue
        }

        if (-not (Test-IsObject -Value $features)) {
            $errors.Add("securityAndAnalysis.$levelName must be an object.")
            continue
        }

        foreach ($feature in (Get-PropertyNames -Value $features)) {
            $status = (Get-OptionalProperty -InputObject $features -Name $feature).Value
            if ($status -notin @('enabled', 'disabled')) {
                $errors.Add("securityAndAnalysis.$levelName.$feature must be 'enabled' or 'disabled'.")
            }
        }
    }

    $repositorySettings = (Get-OptionalProperty -InputObject $Settings -Name 'repositorySettings').Value
    foreach ($levelName in @('required', 'preferred')) {
        $values = (Get-OptionalProperty -InputObject $repositorySettings -Name $levelName).Value
        if ($null -ne $values -and -not (Test-IsObject -Value $values)) {
            $errors.Add("repositorySettings.$levelName must be an object.")
        }
    }

    $environmentNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($environment in (Get-Items -Value (Get-OptionalProperty -InputObject $Settings -Name 'environments').Value)) {
        $name = [string](Get-OptionalProperty -InputObject $environment -Name 'name').Value
        if ([string]::IsNullOrWhiteSpace($name)) {
            $errors.Add('Every environment entry must have a name.')
            continue
        }

        if (-not $environmentNames.Add($name)) {
            $errors.Add("Environment '$name' is declared more than once.")
        }

        $level = (Get-OptionalProperty -InputObject $environment -Name 'level').Value
        if ($level -notin @('Required', 'Preferred')) {
            $errors.Add("Environment '$name' level must be 'Required' or 'Preferred'.")
        }

        if ((Get-OptionalProperty -InputObject $environment -Name 'requireReviewers').Value -isnot [bool]) {
            $errors.Add("Environment '$name' requireReviewers must be true or false.")
        }
    }

    foreach ($key in $script:ForbiddenRulesetManifestKeys) {
        if ((Get-OptionalProperty -InputObject $Ruleset -Name $key).Present) {
            $errors.Add("The ruleset manifest must stay portable and must not contain live-only property '$key'.")
        }
    }

    foreach ($key in @('name', 'target', 'enforcement')) {
        if ([string]::IsNullOrWhiteSpace([string](Get-OptionalProperty -InputObject $Ruleset -Name $key).Value)) {
            $errors.Add("The ruleset manifest must define '$key'.")
        }
    }

    if (-not (Test-IsObject -Value (Get-OptionalProperty -InputObject $Ruleset -Name 'conditions').Value)) {
        $errors.Add("The ruleset manifest must define 'conditions'.")
    }

    if (-not (Test-IsArray -Value (Get-OptionalProperty -InputObject $Ruleset -Name 'bypass_actors').Value)) {
        $errors.Add("The ruleset manifest must define 'bypass_actors' as an array, even when it is empty.")
    }

    $ruleTypes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $rules = @(Get-Items -Value (Get-OptionalProperty -InputObject $Ruleset -Name 'rules').Value)
    if ($rules.Count -eq 0) {
        $errors.Add('The ruleset manifest must define at least one rule.')
    }

    foreach ($rule in $rules) {
        $type = [string](Get-OptionalProperty -InputObject $rule -Name 'type').Value
        if ([string]::IsNullOrWhiteSpace($type)) {
            $errors.Add('Every ruleset rule must define a type.')
        }
        elseif (-not $ruleTypes.Add($type)) {
            $errors.Add("Ruleset rule type '$type' is declared more than once.")
        }
    }

    if ($errors.Count -gt 0) {
        throw ("Repository control manifest is invalid:`n- " + ($errors -join "`n- "))
    }
}

function Read-ControlManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolvedDirectory = (Resolve-Path -LiteralPath $Path).Path
    $settingsPath = Join-Path -Path $resolvedDirectory -ChildPath 'repository-settings.json'
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json

    $rulesetFile = [string](Get-OptionalProperty -InputObject $settings -Name 'rulesetFile').Value
    if ($rulesetFile -notmatch '^[A-Za-z0-9_.-]+\.json$') {
        throw "repository-settings.json must name a ruleset file in the same directory; found '$rulesetFile'."
    }

    $rulesetPath = Join-Path -Path $resolvedDirectory -ChildPath $rulesetFile
    $ruleset = Get-Content -LiteralPath $rulesetPath -Raw | ConvertFrom-Json

    Assert-ControlManifest -Settings $settings -Ruleset $ruleset

    return [pscustomobject]@{
        Settings = $settings
        Ruleset = $ruleset
        SettingsPath = $settingsPath
        RulesetPath = $rulesetPath
    }
}

function Test-AdministrativeView {
    <#
    A token can report admin permission yet still receive the non-administrator
    view of the API (GitHub App and proxied tokens do). Administrator-only data
    is authoritative only when GitHub also returned an administrator-only field,
    so an empty bypass list is never mistaken for a verified one.
    #>
    param(
        [AllowNull()]
        [object]$RepositoryInfo
    )

    $permissions = (Get-OptionalProperty -InputObject $RepositoryInfo -Name 'permissions').Value
    $reportsAdmin = (Get-OptionalProperty -InputObject $permissions -Name 'admin').Value -eq $true
    $securityView = Get-OptionalProperty -InputObject $RepositoryInfo -Name 'security_and_analysis'
    return $reportsAdmin -and $securityView.Present -and $null -ne $securityView.Value
}

function Get-LiveRepositoryState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,

        [Parameter(Mandatory = $true)]
        [string]$RulesetName
    )

    $repositoryResult = Invoke-GitHubGet -Endpoint "repos/$Repository"
    if (-not $repositoryResult.Available) {
        throw "Repository '$Repository' is not readable with the current credentials. $($repositoryResult.Reason)"
    }

    $repositoryInfo = $repositoryResult.Value
    $canAdminister = Test-AdministrativeView -RepositoryInfo $repositoryInfo

    $rulesets = Invoke-GitHubGet -Endpoint "repos/$Repository/rulesets?includes_parents=true&per_page=100"
    if ($rulesets.Available) {
        $details = [System.Collections.Generic.List[object]]::new()
        $detailFailure = $null

        foreach ($summary in (Get-Items -Value $rulesets.Value)) {
            if ([string](Get-OptionalProperty -InputObject $summary -Name 'name').Value -cne $RulesetName) {
                continue
            }

            $detail = Invoke-GitHubGet -Endpoint "repos/$Repository/rulesets/$((Get-OptionalProperty -InputObject $summary -Name 'id').Value)"
            if (-not $detail.Available) {
                $detailFailure = $detail
                break
            }

            $details.Add($detail.Value)
        }

        $rulesets = if ($null -ne $detailFailure) {
            $detailFailure
        }
        else {
            New-ApiResult -Available $true -Value $details.ToArray()
        }
    }

    $environments = Invoke-GitHubGet -Endpoint "repos/$Repository/environments?per_page=100"
    if ($environments.Available) {
        $environmentList = @(Get-Items -Value (Get-OptionalProperty -InputObject $environments.Value -Name 'environments').Value)
        $environments = New-ApiResult -Available $true -Value $environmentList
    }

    return [pscustomobject]@{
        Repository = $Repository
        RepositoryInfo = $repositoryInfo
        CanAdminister = $canAdminister
        Rulesets = $rulesets
        Environments = $environments
    }
}

function Format-RuleParameterValue {
    param(
        [AllowNull()]
        [object]$Value
    )

    $text = ConvertTo-CanonicalText -Value $Value
    if ($text.Length -gt 120) {
        return $text.Substring(0, 117) + '...'
    }

    return $text
}

function Get-StatusCheckKey {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Check
    )

    $context = [string](Get-OptionalProperty -InputObject $Check -Name 'context').Value
    $integration = Get-OptionalProperty -InputObject $Check -Name 'integration_id'
    if ($integration.Present -and $null -ne $integration.Value) {
        return "$context (app $($integration.Value))"
    }

    return $context
}

function Compare-RulesetControls {
    param(
        [Parameter(Mandatory = $true)]
        [object]$DesiredRuleset,

        [Parameter(Mandatory = $true)]
        [object]$Live
    )

    $name = [string]$DesiredRuleset.name
    $rulesets = $Live.Rulesets

    if (-not $rulesets.Available) {
        New-ControlResult -Control 'Ruleset presence' -Level Required -Status Unavailable -Expected $name -Detail "Rulesets could not be read. $($rulesets.Reason)"
        return
    }

    $matching = @(Get-Items -Value $rulesets.Value)
    if ($matching.Count -eq 0) {
        New-ControlResult -Control 'Ruleset presence' -Level Required -Status Missing -Expected $name -Actual 'not found' -Detail 'No ruleset with the committed name applies to the repository.'
        return
    }

    if ($matching.Count -gt 1) {
        New-ControlResult -Control 'Ruleset presence' -Level Required -Status Drift -Expected "one ruleset named '$name'" -Actual "$($matching.Count) rulesets" -Detail 'Duplicate rulesets make the effective policy ambiguous; consolidate them before trusting this audit.'
        return
    }

    $actual = $matching[0]
    New-ControlResult -Control 'Ruleset presence' -Level Required -Status Match -Expected $name -Actual "id $((Get-OptionalProperty -InputObject $actual -Name 'id').Value)"

    foreach ($field in @('target', 'enforcement')) {
        $desiredValue = [string](Get-OptionalProperty -InputObject $DesiredRuleset -Name $field).Value
        $actualProperty = Get-OptionalProperty -InputObject $actual -Name $field
        $actualValue = if ($actualProperty.Present) { [string]$actualProperty.Value } else { $script:AbsentMarker }
        $status = if ($desiredValue -ceq $actualValue) { 'Match' } else { 'Drift' }
        New-ControlResult -Control "Ruleset $field" -Level Required -Status $status -Expected $desiredValue -Actual $actualValue
    }

    $desiredConditions = $DesiredRuleset.conditions
    $actualConditions = (Get-OptionalProperty -InputObject $actual -Name 'conditions').Value
    $conditionsStatus = if (Test-DesiredValue -Desired $desiredConditions -Actual $actualConditions) { 'Match' } else { 'Drift' }
    New-ControlResult -Control 'Ruleset ref conditions' -Level Required -Status $conditionsStatus `
        -Expected (Format-RuleParameterValue -Value $desiredConditions) `
        -Actual (Format-RuleParameterValue -Value $actualConditions)

    $desiredBypass = @(Get-Items -Value $DesiredRuleset.bypass_actors)
    $expectedBypassText = if ($desiredBypass.Count -eq 0) { 'none' } else { Format-RuleParameterValue -Value $desiredBypass }
    $actualBypass = Get-OptionalProperty -InputObject $actual -Name 'bypass_actors'
    if (-not $Live.CanAdminister -or -not $actualBypass.Present) {
        New-ControlResult -Control 'Ruleset bypass actors' -Level Required -Status Unavailable -Expected $expectedBypassText `
            -Detail 'Bypass actors are authoritative only from the administrator view of the API, which this token did not receive. Rerun locally with an administrator gh login.'
    }
    else {
        $actualBypassItems = @(Get-Items -Value $actualBypass.Value)
        $bypassStatus = if (Test-DesiredValue -Desired $desiredBypass -Actual $actualBypassItems) { 'Match' } else { 'Drift' }
        $actualBypassText = if ($actualBypassItems.Count -eq 0) { 'none' } else { Format-RuleParameterValue -Value $actualBypassItems }
        New-ControlResult -Control 'Ruleset bypass actors' -Level Required -Status $bypassStatus -Expected $expectedBypassText -Actual $actualBypassText
    }

    $actualRules = @(Get-Items -Value (Get-OptionalProperty -InputObject $actual -Name 'rules').Value)
    $desiredTypes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

    foreach ($desiredRule in (Get-Items -Value $DesiredRuleset.rules)) {
        $type = [string]$desiredRule.type
        [void]$desiredTypes.Add($type)
        $control = "Rule: $type"
        $liveRule = @($actualRules | Where-Object { [string](Get-OptionalProperty -InputObject $_ -Name 'type').Value -ceq $type } | Select-Object -First 1)

        if ($liveRule.Count -eq 0) {
            New-ControlResult -Control $control -Level Required -Status Missing -Expected 'present' -Actual 'absent' -Detail "The live ruleset does not contain the '$type' rule."
            continue
        }

        $desiredParameters = Get-OptionalProperty -InputObject $desiredRule -Name 'parameters'
        if (-not $desiredParameters.Present) {
            New-ControlResult -Control $control -Level Required -Status Match -Expected 'present' -Actual 'present'
            continue
        }

        $actualParameters = (Get-OptionalProperty -InputObject $liveRule[0] -Name 'parameters').Value
        $differences = [System.Collections.Generic.List[string]]::new()
        $expectedSummary = 'committed parameters'
        $actualSummary = 'matching parameters'

        foreach ($parameterName in (Get-PropertyNames -Value $desiredParameters.Value)) {
            $desiredValue = (Get-OptionalProperty -InputObject $desiredParameters.Value -Name $parameterName).Value
            $actualParameter = Get-OptionalProperty -InputObject $actualParameters -Name $parameterName

            if ($parameterName -ceq 'required_status_checks') {
                $desiredChecks = @(Get-Items -Value $desiredValue | ForEach-Object { Get-StatusCheckKey -Check $_ })
                $actualChecks = @(Get-Items -Value $actualParameter.Value | ForEach-Object { Get-StatusCheckKey -Check $_ })
                $expectedSummary = "$($desiredChecks.Count) required checks"
                $actualSummary = "$($actualChecks.Count) required checks"

                if (-not $actualParameter.Present) {
                    $differences.Add('required_status_checks is absent.')
                    continue
                }

                $missingChecks = @($desiredChecks | Where-Object { $_ -cnotin $actualChecks })
                $unexpectedChecks = @($actualChecks | Where-Object { $_ -cnotin $desiredChecks })
                if ($missingChecks.Count -gt 0) {
                    $differences.Add("Missing required checks: $((Get-SortedText -Values $missingChecks) -join '; ').")
                }

                if ($unexpectedChecks.Count -gt 0) {
                    $differences.Add("Unexpected required checks: $((Get-SortedText -Values $unexpectedChecks) -join '; ').")
                }

                continue
            }

            if (-not $actualParameter.Present) {
                $differences.Add("$parameterName is absent (expected $(Format-RuleParameterValue -Value $desiredValue)).")
                continue
            }

            if (-not (Test-DesiredValue -Desired $desiredValue -Actual $actualParameter.Value)) {
                $differences.Add("$parameterName expected $(Format-RuleParameterValue -Value $desiredValue) but found $(Format-RuleParameterValue -Value $actualParameter.Value).")
            }
        }

        if ($differences.Count -eq 0) {
            New-ControlResult -Control $control -Level Required -Status Match -Expected $expectedSummary -Actual $actualSummary
        }
        else {
            if ($actualSummary -ceq 'matching parameters') {
                $actualSummary = "$($differences.Count) difference(s)"
            }

            New-ControlResult -Control $control -Level Required -Status Drift -Expected $expectedSummary -Actual $actualSummary -Detail ($differences -join ' ')
        }
    }

    $additionalTypes = @($actualRules |
        ForEach-Object { [string](Get-OptionalProperty -InputObject $_ -Name 'type').Value } |
        Where-Object { -not $desiredTypes.Contains($_) })

    if ($additionalTypes.Count -gt 0) {
        New-ControlResult -Control 'Ruleset additional rules' -Level Preferred -Status Drift -Expected 'none' -Actual ((Get-SortedText -Values $additionalTypes) -join ', ') `
            -Detail 'The live ruleset has rules the manifest does not record. Add them to the manifest if they are intended.'
    }
    else {
        New-ControlResult -Control 'Ruleset additional rules' -Level Preferred -Status Match -Expected 'none' -Actual 'none'
    }
}

function Compare-SecurityControls {
    param(
        [Parameter(Mandatory = $true)]
        [object]$DesiredSettings,

        [Parameter(Mandatory = $true)]
        [object]$Live
    )

    $desiredSecurity = (Get-OptionalProperty -InputObject $DesiredSettings -Name 'securityAndAnalysis').Value
    $liveSecurity = Get-OptionalProperty -InputObject $Live.RepositoryInfo -Name 'security_and_analysis'

    foreach ($level in @('Required', 'Preferred')) {
        $features = (Get-OptionalProperty -InputObject $desiredSecurity -Name $level.ToLowerInvariant()).Value
        if ($null -eq $features) {
            continue
        }

        foreach ($feature in (Get-PropertyNames -Value $features)) {
            $expected = [string](Get-OptionalProperty -InputObject $features -Name $feature).Value
            $control = "Security: $feature"

            if (-not $liveSecurity.Present -or $null -eq $liveSecurity.Value) {
                New-ControlResult -Control $control -Level $level -Status Unavailable -Expected $expected `
                    -Detail 'security_and_analysis is not returned to this token. Repository-administrator access is required.'
                continue
            }

            $liveFeature = Get-OptionalProperty -InputObject $liveSecurity.Value -Name $feature
            $liveStatus = (Get-OptionalProperty -InputObject $liveFeature.Value -Name 'status').Value
            if (-not $liveFeature.Present -or $null -eq $liveStatus) {
                New-ControlResult -Control $control -Level $level -Status Unavailable -Expected $expected `
                    -Detail 'GitHub did not report this feature. It may be unsupported for this plan or hidden from this token.'
                continue
            }

            $status = if ([string]$liveStatus -ceq $expected) { 'Match' } else { 'Drift' }
            New-ControlResult -Control $control -Level $level -Status $status -Expected $expected -Actual ([string]$liveStatus)
        }
    }
}

function Compare-RepositorySettingControls {
    param(
        [Parameter(Mandatory = $true)]
        [object]$DesiredSettings,

        [Parameter(Mandatory = $true)]
        [object]$Live
    )

    $desiredRepositorySettings = (Get-OptionalProperty -InputObject $DesiredSettings -Name 'repositorySettings').Value

    foreach ($level in @('Required', 'Preferred')) {
        $values = (Get-OptionalProperty -InputObject $desiredRepositorySettings -Name $level.ToLowerInvariant()).Value
        if ($null -eq $values) {
            continue
        }

        foreach ($setting in (Get-PropertyNames -Value $values)) {
            $expected = (Get-OptionalProperty -InputObject $values -Name $setting).Value
            $expectedText = ConvertTo-CanonicalText -Value $expected
            $actual = Get-OptionalProperty -InputObject $Live.RepositoryInfo -Name $setting
            $control = "Setting: $setting"

            if (-not $actual.Present -or $null -eq $actual.Value) {
                New-ControlResult -Control $control -Level $level -Status Unavailable -Expected $expectedText `
                    -Detail 'GitHub did not return this setting to the current token. Repository-administrator access is required.'
                continue
            }

            $status = if (Test-DesiredValue -Desired $expected -Actual $actual.Value) { 'Match' } else { 'Drift' }
            New-ControlResult -Control $control -Level $level -Status $status -Expected $expectedText -Actual (ConvertTo-CanonicalText -Value $actual.Value)
        }
    }
}

function Compare-EnvironmentControls {
    param(
        [Parameter(Mandatory = $true)]
        [object]$DesiredSettings,

        [Parameter(Mandatory = $true)]
        [object]$Live
    )

    foreach ($desiredEnvironment in (Get-Items -Value (Get-OptionalProperty -InputObject $DesiredSettings -Name 'environments').Value)) {
        $name = [string]$desiredEnvironment.name
        $level = [string]$desiredEnvironment.level
        $requireReviewers = [bool]$desiredEnvironment.requireReviewers
        $expected = if ($requireReviewers) { 'exists with required reviewers' } else { 'exists' }
        $control = "Environment: $name"

        if (-not $Live.Environments.Available) {
            New-ControlResult -Control $control -Level $level -Status Unavailable -Expected $expected -Detail "Environments could not be read. $($Live.Environments.Reason)"
            continue
        }

        $liveEnvironment = @(Get-Items -Value $Live.Environments.Value |
            Where-Object { [string](Get-OptionalProperty -InputObject $_ -Name 'name').Value -eq $name } |
            Select-Object -First 1)

        if ($liveEnvironment.Count -eq 0) {
            New-ControlResult -Control $control -Level $level -Status Missing -Expected $expected -Actual 'not found'
            continue
        }

        if (-not $requireReviewers) {
            New-ControlResult -Control $control -Level $level -Status Match -Expected $expected -Actual 'exists'
            continue
        }

        $reviewerRules = @(Get-Items -Value (Get-OptionalProperty -InputObject $liveEnvironment[0] -Name 'protection_rules').Value |
            Where-Object { [string](Get-OptionalProperty -InputObject $_ -Name 'type').Value -ceq 'required_reviewers' })
        $reviewerCount = 0
        foreach ($rule in $reviewerRules) {
            $reviewerCount += @(Get-Items -Value (Get-OptionalProperty -InputObject $rule -Name 'reviewers').Value).Count
        }

        if ($reviewerCount -eq 0) {
            New-ControlResult -Control $control -Level $level -Status Drift -Expected $expected -Actual 'no required reviewers' `
                -Detail 'Deployments to this environment are not held for deliberate maintainer approval.'
            continue
        }

        New-ControlResult -Control $control -Level $level -Status Match -Expected $expected -Actual "$reviewerCount required reviewer(s)"
    }
}

function Compare-RepositoryControls {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Controls,

        [Parameter(Mandatory = $true)]
        [object]$Live
    )

    Compare-RulesetControls -DesiredRuleset $Controls.Ruleset -Live $Live
    Compare-SecurityControls -DesiredSettings $Controls.Settings -Live $Live
    Compare-RepositorySettingControls -DesiredSettings $Controls.Settings -Live $Live
    Compare-EnvironmentControls -DesiredSettings $Controls.Settings -Live $Live
}

function Get-AuditOutcome {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Results,

        [bool]$FailOnUnavailable = $false
    )

    $blocking = @($Results | Where-Object {
            $_.Level -eq 'Required' -and (
                ($_.Status -in @('Drift', 'Missing')) -or
                ($FailOnUnavailable -and $_.Status -eq 'Unavailable'))
        })
    $warnings = @($Results | Where-Object { $_.Level -eq 'Preferred' -and $_.Status -ne 'Match' })
    $unavailable = @($Results | Where-Object { $_.Status -eq 'Unavailable' })

    return [pscustomobject]@{
        Passed = $blocking.Count -eq 0
        Blocking = $blocking
        Warnings = $warnings
        Unavailable = $unavailable
    }
}

function ConvertTo-MarkdownCell {
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Text
    )

    if ([string]::IsNullOrEmpty($Text)) {
        return ''
    }

    return (($Text -replace '\r?\n', ' ') -replace '\|', '\|')
}

function Format-AuditMarkdown {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Results,

        [Parameter(Mandatory = $true)]
        [object]$Outcome,

        [bool]$FailOnUnavailable = $false,

        [string]$GeneratedAt = ([DateTimeOffset]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ', [System.Globalization.CultureInfo]::InvariantCulture))
    )

    $unavailableMode = if ($FailOnUnavailable) { 'fail the audit' } else { 'reported, not failing' }
    $resultText = if ($Outcome.Passed) { 'Passed' } else { "Failed ($($Outcome.Blocking.Count) blocking finding(s))" }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('## Repository security-control audit')
    $lines.Add('')
    $lines.Add("- Repository: ``$Repository``")
    $lines.Add("- Generated (UTC): $GeneratedAt")
    $lines.Add('- Manifest: `eng/repository-controls/`')
    $lines.Add("- Unavailable controls: $unavailableMode")
    $lines.Add("- Result: **$resultText**")
    $lines.Add("- Counts: $(@($Results | Where-Object Status -eq 'Match').Count) match, $(@($Results | Where-Object Status -eq 'Drift').Count) drift, $(@($Results | Where-Object Status -eq 'Missing').Count) missing, $($Outcome.Unavailable.Count) unavailable")
    $lines.Add('')
    $lines.Add('This audit is read-only. It never changes GitHub settings; unavailable controls are unverified, not compliant.')
    $lines.Add('')
    $lines.Add('| Control | Level | Status | Expected | Actual | Detail |')
    $lines.Add('|:---|:---|:---|:---|:---|:---|')

    foreach ($result in $Results) {
        $cells = @(
            $result.Control,
            $result.Level,
            $result.Status,
            $result.Expected,
            $result.Actual,
            $result.Detail
        ) | ForEach-Object { ConvertTo-MarkdownCell -Text ([string]$_) }

        $lines.Add('| ' + ($cells -join ' | ') + ' |')
    }

    return ($lines -join [System.Environment]::NewLine) + [System.Environment]::NewLine
}

function Write-AuditResults {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Results
    )

    foreach ($result in $Results) {
        $line = '[{0}] {1} ({2})' -f $result.Status, $result.Control, $result.Level
        if (-not [string]::IsNullOrWhiteSpace($result.Detail)) {
            $line += ": $($result.Detail)"
        }

        if ($result.Level -eq 'Preferred' -and $result.Status -ne 'Match') {
            Write-Warning $line
        }
        else {
            Write-Host $line
        }
    }
}

function Copy-JsonValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    return (ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $Value -Depth 50) -NoEnumerate)
}

function New-SelfTestLiveState {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Controls
    )

    $ruleset = Copy-JsonValue -Value $Controls.Ruleset
    $ruleset | Add-Member -NotePropertyName 'id' -NotePropertyValue 42
    $ruleset | Add-Member -NotePropertyName 'source_type' -NotePropertyValue 'Repository'
    $ruleset | Add-Member -NotePropertyName 'node_id' -NotePropertyValue 'RRS_selftest'
    $ruleset | Add-Member -NotePropertyName 'current_user_can_bypass' -NotePropertyValue 'never'
    $pullRequestRule = @($ruleset.rules | Where-Object type -eq 'pull_request')[0]
    $pullRequestRule.parameters | Add-Member -NotePropertyName 'future_parameter_added_by_github' -NotePropertyValue $true
    $pullRequestRule.parameters.dismissal_restriction | Add-Member -NotePropertyName 'future_nested_key' -NotePropertyValue 'ignored'
    $codeScanningRule = @($ruleset.rules | Where-Object type -eq 'code_scanning')[0]
    $codeScanningRule.parameters.code_scanning_tools[0] | Add-Member -NotePropertyName 'future_element_key' -NotePropertyValue 'ignored'

    $repositoryInfo = ConvertFrom-Json -InputObject @'
{
  "full_name": "AsiBackbone/NetCoreApplicationTemplate",
  "permissions": { "admin": true, "pull": true },
  "delete_branch_on_merge": true,
  "allow_squash_merge": true,
  "security_and_analysis": {
    "secret_scanning": { "status": "enabled" },
    "secret_scanning_push_protection": { "status": "enabled" },
    "dependabot_security_updates": { "status": "enabled" },
    "secret_scanning_validity_checks": { "status": "enabled" },
    "secret_scanning_non_provider_patterns": { "status": "disabled" }
  }
}
'@

    $environments = ConvertFrom-Json -NoEnumerate -InputObject @'
[
  {
    "name": "github-pages",
    "protection_rules": [ { "type": "branch_policy" } ]
  },
  {
    "name": "template-package-publish",
    "protection_rules": [
      { "type": "required_reviewers", "prevent_self_review": false, "reviewers": [ { "type": "User", "reviewer": { "login": "maintainer" } } ] },
      { "type": "branch_policy" }
    ]
  }
]
'@

    return [pscustomobject]@{
        Repository = 'AsiBackbone/NetCoreApplicationTemplate'
        RepositoryInfo = $repositoryInfo
        CanAdminister = $true
        Rulesets = New-ApiResult -Available $true -Value @($ruleset)
        Environments = New-ApiResult -Available $true -Value $environments
    }
}

function Get-SelfTestRule {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Live,

        [Parameter(Mandatory = $true)]
        [string]$Type
    )

    return @($Live.Rulesets.Value[0].rules | Where-Object type -eq $Type)[0]
}

function Assert-SelfTest {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-SelfTestResult {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Results,

        [Parameter(Mandatory = $true)]
        [string]$Control
    )

    $result = @($Results | Where-Object Control -eq $Control)
    Assert-SelfTest -Condition ($result.Count -eq 1) -Message "Expected exactly one result for '$Control' but found $($result.Count)."
    return $result[0]
}

function Assert-ReadOnlySource {
    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($script:ScriptPath, [ref]$tokens, [ref]$parseErrors)
    Assert-SelfTest -Condition (@($parseErrors).Count -eq 0) -Message 'The audit script must parse without errors.'

    $parameterNames = @($ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
    Assert-SelfTest -Condition ('Apply' -notin $parameterNames) -Message 'The audit script must not expose an Apply parameter.'

    $commands = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] }, $true))
    $commandNames = @($commands | ForEach-Object {
            $first = $_.CommandElements[0]
            $name = if ($first -is [System.Management.Automation.Language.StringConstantExpressionAst]) { $first.Value } else { '' }
            [pscustomobject]@{ Name = $name; Ast = $_ }
        })

    $webCommands = @($commandNames | Where-Object { $_.Name -in @('Invoke-RestMethod', 'Invoke-WebRequest', 'irm', 'iwr', 'curl', 'wget') })
    Assert-SelfTest -Condition ($webCommands.Count -eq 0) -Message 'The audit script must reach GitHub only through Invoke-GitHubGet.'

    $ghCommands = @($commandNames | Where-Object { $_.Name -in @('gh', 'gh.exe') } | ForEach-Object Ast)
    Assert-SelfTest -Condition ($ghCommands.Count -eq 1) -Message "The audit script must invoke gh from exactly one place; found $($ghCommands.Count)."

    $owner = $ghCommands[0].Parent
    while ($null -ne $owner -and $owner -isnot [System.Management.Automation.Language.FunctionDefinitionAst]) {
        $owner = $owner.Parent
    }

    Assert-SelfTest -Condition ($null -ne $owner -and $owner.Name -eq 'Invoke-GitHubGet') -Message 'gh must only be invoked from Invoke-GitHubGet.'

    $wrapperStrings = @($owner.FindAll({ param($node) $node -is [System.Management.Automation.Language.StringConstantExpressionAst] }, $true) | ForEach-Object { $_.Value })
    $forbiddenArguments = @('--input', '--field', '--raw-field', '-f', '-F', '-X', 'POST', 'PUT', 'PATCH', 'DELETE')
    $forbiddenFound = @($wrapperStrings | Where-Object { $_ -cin $forbiddenArguments })
    Assert-SelfTest -Condition ($forbiddenFound.Count -eq 0) -Message "Invoke-GitHubGet must not contain write arguments: $($forbiddenFound -join ', ')."
    Assert-SelfTest -Condition (('--method' -cin $wrapperStrings) -and ('GET' -cin $wrapperStrings)) -Message 'Invoke-GitHubGet must send an explicit GET method.'
}

function Invoke-SelfTest {
    $controls = Read-ControlManifest -Path $ControlsDirectory
    $cases = [System.Collections.Generic.List[object]]::new()

    $cases.Add(@{ Name = 'committed manifest matches identical live state, ignoring extra live keys'; Test = {
                $results = @(Compare-RepositoryControls -Controls $controls -Live (New-SelfTestLiveState -Controls $controls))
                $nonMatching = @($results | Where-Object Status -ne 'Match')
                Assert-SelfTest -Condition ($nonMatching.Count -eq 0) -Message "Unexpected non-matching controls: $(($nonMatching | ForEach-Object Control) -join ', ')."
                Assert-SelfTest -Condition (Get-AuditOutcome -Results $results -FailOnUnavailable $true).Passed -Message 'A matching state must pass even with -FailOnUnavailable.'
                Assert-SelfTest -Condition (@($results | Where-Object Control -like 'Rule: *').Count -eq @($controls.Ruleset.rules).Count) -Message 'Every committed rule must produce a result.'
            }
        })

    $cases.Add(@{ Name = 'missing required status check is drift'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $rule = Get-SelfTestRule -Live $live -Type 'required_status_checks'
                $rule.parameters.required_status_checks = @($rule.parameters.required_status_checks | Where-Object context -ne 'Template options (none + sqlserver)')
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                $result = Get-SelfTestResult -Results $results -Control 'Rule: required_status_checks'
                Assert-SelfTest -Condition ($result.Status -eq 'Drift') -Message 'A removed required check must be drift.'
                Assert-SelfTest -Condition ($result.Detail -like '*Missing required checks: Template options (none + sqlserver)*') -Message "Drift detail must name the missing check; got '$($result.Detail)'."
                Assert-SelfTest -Condition (-not (Get-AuditOutcome -Results $results).Passed) -Message 'Required-check drift must fail the audit.'
            }
        })

    $cases.Add(@{ Name = 'unexpected required status check is drift'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $rule = Get-SelfTestRule -Live $live -Type 'required_status_checks'
                $rule.parameters.required_status_checks = @($rule.parameters.required_status_checks) + [pscustomobject]@{ context = 'Renamed job'; integration_id = 15368 }
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Rule: required_status_checks'
                Assert-SelfTest -Condition ($result.Status -eq 'Drift' -and $result.Detail -like '*Unexpected required checks: Renamed job*') -Message "Unexpected check must be reported; got '$($result.Detail)'."
            }
        })

    $cases.Add(@{ Name = 'merge-method drift is detected'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                (Get-SelfTestRule -Live $live -Type 'pull_request').parameters.allowed_merge_methods = @('merge', 'squash')
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                $result = Get-SelfTestResult -Results $results -Control 'Rule: pull_request'
                Assert-SelfTest -Condition ($result.Status -eq 'Drift' -and $result.Detail -like '*allowed_merge_methods*') -Message "Merge-method drift must be reported; got '$($result.Detail)'."
                Assert-SelfTest -Condition (-not (Get-AuditOutcome -Results $results).Passed) -Message 'Merge-method drift must fail the audit.'
            }
        })

    $cases.Add(@{ Name = 'merge-method order is not drift'; Test = {
                $modified = Copy-JsonValue -Value $controls
                (@($modified.Ruleset.rules | Where-Object type -eq 'pull_request')[0]).parameters.allowed_merge_methods = @('squash', 'rebase')
                $live = New-SelfTestLiveState -Controls $controls
                (Get-SelfTestRule -Live $live -Type 'pull_request').parameters.allowed_merge_methods = @('rebase', 'squash')
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $modified -Live $live) -Control 'Rule: pull_request'
                Assert-SelfTest -Condition ($result.Status -eq 'Match') -Message 'Array order must not create drift.'
            }
        })

    $cases.Add(@{ Name = 'missing pull-request parameter is drift'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                (Get-SelfTestRule -Live $live -Type 'pull_request').parameters.PSObject.Properties.Remove('require_code_owner_review')
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Rule: pull_request'
                Assert-SelfTest -Condition ($result.Status -eq 'Drift' -and $result.Detail -like '*require_code_owner_review is absent*') -Message "Absent parameter must be drift; got '$($result.Detail)'."
            }
        })

    $cases.Add(@{ Name = 'removed force-push protection is missing'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.Rulesets.Value[0].rules = @($live.Rulesets.Value[0].rules | Where-Object type -ne 'non_fast_forward')
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                Assert-SelfTest -Condition ((Get-SelfTestResult -Results $results -Control 'Rule: non_fast_forward').Status -eq 'Missing') -Message 'A removed rule must be missing.'
                Assert-SelfTest -Condition (-not (Get-AuditOutcome -Results $results).Passed) -Message 'A missing rule must fail the audit.'
            }
        })

    $cases.Add(@{ Name = 'code-scanning threshold drift is detected'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                (Get-SelfTestRule -Live $live -Type 'code_scanning').parameters.code_scanning_tools[0].security_alerts_threshold = 'critical'
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Rule: code_scanning'
                Assert-SelfTest -Condition ($result.Status -eq 'Drift') -Message 'A weakened code-scanning threshold must be drift.'
            }
        })

    $cases.Add(@{ Name = 'disabled enforcement is drift'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.Rulesets.Value[0].enforcement = 'evaluate'
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Ruleset enforcement'
                Assert-SelfTest -Condition ($result.Status -eq 'Drift' -and $result.Actual -eq 'evaluate') -Message 'Evaluate-only enforcement must be drift.'
            }
        })

    $cases.Add(@{ Name = 'changed branch target is drift'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.Rulesets.Value[0].conditions.ref_name.include = @('~DEFAULT_BRANCH', 'refs/heads/main')
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Ruleset ref conditions'
                Assert-SelfTest -Condition ($result.Status -eq 'Drift') -Message 'Changed ref conditions must be drift.'
            }
        })

    $cases.Add(@{ Name = 'added bypass actor is drift'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.Rulesets.Value[0].bypass_actors = @([pscustomobject]@{ actor_id = 5; actor_type = 'RepositoryRole'; bypass_mode = 'always' })
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Ruleset bypass actors'
                Assert-SelfTest -Condition ($result.Status -eq 'Drift') -Message 'An unrecorded bypass actor must be drift.'
            }
        })

    $cases.Add(@{ Name = 'bypass actors without administrator token are unavailable'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.CanAdminister = $false
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Ruleset bypass actors'
                Assert-SelfTest -Condition ($result.Status -eq 'Unavailable') -Message 'Bypass actors read without administrator access must be unavailable, not compliant.'

                $live = New-SelfTestLiveState -Controls $controls
                $live.Rulesets.Value[0].PSObject.Properties.Remove('bypass_actors')
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Ruleset bypass actors'
                Assert-SelfTest -Condition ($result.Status -eq 'Unavailable') -Message 'An omitted bypass_actors property must be unavailable.'
            }
        })

    $cases.Add(@{ Name = 'administrator view requires administrator-only fields'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                Assert-SelfTest -Condition (Test-AdministrativeView -RepositoryInfo $live.RepositoryInfo) -Message 'An admin token with security_and_analysis must be an administrator view.'

                $live.RepositoryInfo.PSObject.Properties.Remove('security_and_analysis')
                Assert-SelfTest -Condition (-not (Test-AdministrativeView -RepositoryInfo $live.RepositoryInfo)) -Message 'admin=true without administrator-only fields must not be trusted.'

                $live = New-SelfTestLiveState -Controls $controls
                $live.RepositoryInfo.permissions.admin = $false
                Assert-SelfTest -Condition (-not (Test-AdministrativeView -RepositoryInfo $live.RepositoryInfo)) -Message 'A non-admin token must not be an administrator view.'

                $live.RepositoryInfo.PSObject.Properties.Remove('permissions')
                Assert-SelfTest -Condition (-not (Test-AdministrativeView -RepositoryInfo $live.RepositoryInfo)) -Message 'A response without permissions must not be an administrator view.'
            }
        })

    $cases.Add(@{ Name = 'additional live rule is a preferred warning'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.Rulesets.Value[0].rules = @($live.Rulesets.Value[0].rules) + [pscustomobject]@{ type = 'required_signatures' }
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                $result = Get-SelfTestResult -Results $results -Control 'Ruleset additional rules'
                $outcome = Get-AuditOutcome -Results $results
                Assert-SelfTest -Condition ($result.Status -eq 'Drift' -and $result.Level -eq 'Preferred') -Message 'An extra rule must be a preferred-level finding.'
                Assert-SelfTest -Condition ($outcome.Passed -and $outcome.Warnings.Count -eq 1) -Message 'An extra rule must warn without failing.'
            }
        })

    $cases.Add(@{ Name = 'missing ruleset fails'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.Rulesets = New-ApiResult -Available $true -Value @()
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                Assert-SelfTest -Condition ((Get-SelfTestResult -Results $results -Control 'Ruleset presence').Status -eq 'Missing') -Message 'A missing ruleset must be reported as missing.'
                Assert-SelfTest -Condition (-not (Get-AuditOutcome -Results $results).Passed) -Message 'A missing ruleset must fail the audit.'
            }
        })

    $cases.Add(@{ Name = 'duplicate rulesets fail'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.Rulesets = New-ApiResult -Available $true -Value @($live.Rulesets.Value[0], (Copy-JsonValue -Value $live.Rulesets.Value[0]))
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                Assert-SelfTest -Condition ((Get-SelfTestResult -Results $results -Control 'Ruleset presence').Status -eq 'Drift') -Message 'Duplicate rulesets must be drift.'
            }
        })

    $cases.Add(@{ Name = 'unreadable rulesets are unavailable, not compliant'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.Rulesets = New-ApiResult -Available $false -Value $null -Reason 'HTTP 403'
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                $result = Get-SelfTestResult -Results $results -Control 'Ruleset presence'
                Assert-SelfTest -Condition ($result.Status -eq 'Unavailable') -Message 'Unreadable rulesets must be unavailable.'
                Assert-SelfTest -Condition ((Get-AuditOutcome -Results $results -FailOnUnavailable $false).Passed) -Message 'Unavailable controls must not fail the scheduled audit.'
                Assert-SelfTest -Condition (-not (Get-AuditOutcome -Results $results -FailOnUnavailable $true).Passed) -Message 'Unavailable controls must fail with -FailOnUnavailable.'
            }
        })

    $cases.Add(@{ Name = 'hidden security_and_analysis is unavailable'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.RepositoryInfo.PSObject.Properties.Remove('security_and_analysis')
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                $securityResults = @($results | Where-Object Control -like 'Security: *')
                Assert-SelfTest -Condition ($securityResults.Count -eq 4 -and @($securityResults | Where-Object Status -ne 'Unavailable').Count -eq 0) -Message 'Every security feature must be unavailable when GitHub omits security_and_analysis.'
                Assert-SelfTest -Condition ((Get-AuditOutcome -Results $results).Passed) -Message 'Hidden security features must not fail without -FailOnUnavailable.'
                Assert-SelfTest -Condition (-not (Get-AuditOutcome -Results $results -FailOnUnavailable $true).Passed) -Message 'Hidden required security features must fail with -FailOnUnavailable.'
            }
        })

    $cases.Add(@{ Name = 'unreported security feature is unavailable'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.RepositoryInfo.security_and_analysis.PSObject.Properties.Remove('dependabot_security_updates')
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Security: dependabot_security_updates'
                Assert-SelfTest -Condition ($result.Status -eq 'Unavailable') -Message 'An unreported feature must be unavailable.'
            }
        })

    $cases.Add(@{ Name = 'disabled push protection fails'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.RepositoryInfo.security_and_analysis.secret_scanning_push_protection.status = 'disabled'
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                Assert-SelfTest -Condition ((Get-SelfTestResult -Results $results -Control 'Security: secret_scanning_push_protection').Status -eq 'Drift') -Message 'Disabled push protection must be drift.'
                Assert-SelfTest -Condition (-not (Get-AuditOutcome -Results $results).Passed) -Message 'Disabled push protection must fail the audit.'
            }
        })

    $cases.Add(@{ Name = 'preferred validity-check drift warns without failing'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.RepositoryInfo.security_and_analysis.secret_scanning_validity_checks.status = 'disabled'
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                $outcome = Get-AuditOutcome -Results $results -FailOnUnavailable $true
                Assert-SelfTest -Condition ((Get-SelfTestResult -Results $results -Control 'Security: secret_scanning_validity_checks').Status -eq 'Drift') -Message 'Disabled validity checks must be drift.'
                Assert-SelfTest -Condition ($outcome.Passed -and $outcome.Warnings.Count -eq 1) -Message 'Preferred drift must warn without failing.'
            }
        })

    $cases.Add(@{ Name = 'branch cleanup setting drift and unavailability'; Test = {
                $live = New-SelfTestLiveState -Controls $controls
                $live.RepositoryInfo.delete_branch_on_merge = $false
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Setting: delete_branch_on_merge'
                Assert-SelfTest -Condition ($result.Status -eq 'Drift') -Message 'Disabled branch cleanup must be drift.'

                $live.RepositoryInfo.PSObject.Properties.Remove('delete_branch_on_merge')
                $result = Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control 'Setting: delete_branch_on_merge'
                Assert-SelfTest -Condition ($result.Status -eq 'Unavailable') -Message 'An omitted setting must be unavailable.'
            }
        })

    $cases.Add(@{ Name = 'publishing environment drift, missing, and unavailable'; Test = {
                $control = 'Environment: template-package-publish'

                $live = New-SelfTestLiveState -Controls $controls
                (@($live.Environments.Value | Where-Object name -eq 'template-package-publish')[0]).protection_rules = @([pscustomobject]@{ type = 'branch_policy' })
                $results = @(Compare-RepositoryControls -Controls $controls -Live $live)
                Assert-SelfTest -Condition ((Get-SelfTestResult -Results $results -Control $control).Status -eq 'Drift') -Message 'An environment without reviewers must be drift.'
                Assert-SelfTest -Condition (-not (Get-AuditOutcome -Results $results).Passed) -Message 'A publishing environment without reviewers must fail the audit.'

                $live = New-SelfTestLiveState -Controls $controls
                $live.Environments = New-ApiResult -Available $true -Value @($live.Environments.Value | Where-Object name -ne 'template-package-publish')
                Assert-SelfTest -Condition ((Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control $control).Status -eq 'Missing') -Message 'A deleted environment must be missing.'

                $live = New-SelfTestLiveState -Controls $controls
                $live.Environments = New-ApiResult -Available $false -Value $null -Reason 'HTTP 404'
                Assert-SelfTest -Condition ((Get-SelfTestResult -Results @(Compare-RepositoryControls -Controls $controls -Live $live) -Control $control).Status -eq 'Unavailable') -Message 'Unreadable environments must be unavailable.'
            }
        })

    $cases.Add(@{ Name = 'manifest validation rejects non-portable or ambiguous manifests'; Test = {
                $withId = Copy-JsonValue -Value $controls.Ruleset
                $withId | Add-Member -NotePropertyName 'id' -NotePropertyValue 1
                $rejected = $false
                try { Assert-ControlManifest -Settings $controls.Settings -Ruleset $withId } catch { $rejected = $true }
                Assert-SelfTest -Condition $rejected -Message 'A ruleset manifest with a live id must be rejected.'

                $duplicate = Copy-JsonValue -Value $controls.Ruleset
                $duplicate.rules = @($duplicate.rules) + [pscustomobject]@{ type = 'deletion' }
                $rejected = $false
                try { Assert-ControlManifest -Settings $controls.Settings -Ruleset $duplicate } catch { $rejected = $true }
                Assert-SelfTest -Condition $rejected -Message 'Duplicate rule types must be rejected.'

                $badSettings = Copy-JsonValue -Value $controls.Settings
                $badSettings.schemaVersion = 2
                $rejected = $false
                try { Assert-ControlManifest -Settings $badSettings -Ruleset $controls.Ruleset } catch { $rejected = $true }
                Assert-SelfTest -Condition $rejected -Message 'An unknown schema version must be rejected.'
            }
        })

    $cases.Add(@{ Name = 'GitHub failures are classified without assuming compliance'; Test = {
                Assert-SelfTest -Condition ((Get-GitHubFailureClassification -ExitCode 0 -ErrorText '') -eq 'Success') -Message 'Exit code 0 must succeed.'
                Assert-SelfTest -Condition ((Get-GitHubFailureClassification -ExitCode 1 -ErrorText 'gh: Not Found (HTTP 404)') -eq 'Unavailable') -Message 'HTTP 404 must be unavailable.'
                Assert-SelfTest -Condition ((Get-GitHubFailureClassification -ExitCode 1 -ErrorText 'gh: Resource not accessible by integration (HTTP 403)') -eq 'Unavailable') -Message 'HTTP 403 must be unavailable.'
                Assert-SelfTest -Condition ((Get-GitHubFailureClassification -ExitCode 1 -ErrorText 'gh: Bad credentials (HTTP 401)') -eq 'Error') -Message 'HTTP 401 must be an error.'
                Assert-SelfTest -Condition ((Get-GitHubFailureClassification -ExitCode 4 -ErrorText 'To get started with GitHub CLI, please run: gh auth login') -eq 'Error') -Message 'An unauthenticated gh must be an error.'
            }
        })

    $cases.Add(@{ Name = 'audit source is read-only'; Test = {
                Assert-ReadOnlySource

                $script:CapturedGhArguments = $null
                function gh {
                    $script:CapturedGhArguments = @($args)
                    $global:LASTEXITCODE = 0
                    '{"self_test":true}'
                }

                $response = Invoke-GitHubGet -Endpoint 'repos/AsiBackbone/NetCoreApplicationTemplate'
                $captured = @($script:CapturedGhArguments)
                Assert-SelfTest -Condition ($captured.Count -ge 3 -and $captured[0] -ceq 'api' -and $captured[1] -ceq '--method' -and $captured[2] -ceq 'GET') -Message "gh must be called as 'api --method GET'; got '$($captured -join ' ')'."
                Assert-SelfTest -Condition (@($captured | Where-Object { $_ -cin @('--input', '--field', '--raw-field', '-f', '-F', '-X') }).Count -eq 0) -Message 'gh must not receive body or field arguments.'
                Assert-SelfTest -Condition ($response.Available -and $response.Value.self_test -eq $true) -Message 'Invoke-GitHubGet must parse a successful response.'

                $rejected = $false
                try { Invoke-GitHubGet -Endpoint 'graphql' | Out-Null } catch { $rejected = $true }
                Assert-SelfTest -Condition $rejected -Message 'Endpoints outside repos/{owner}/{name} must be rejected.'
            }
        })

    $cases.Add(@{ Name = 'markdown evidence is a single escaped table'; Test = {
                $results = @(New-ControlResult -Control 'Rule: a|b' -Level Required -Status Drift -Expected 'x' -Actual 'y' -Detail "line one`nline two")
                $markdown = Format-AuditMarkdown -Repository 'AsiBackbone/NetCoreApplicationTemplate' -Results $results -Outcome (Get-AuditOutcome -Results $results) -GeneratedAt '2026-01-01T00:00:00Z'
                Assert-SelfTest -Condition ($markdown -like '*| Rule: a\|b | Required | Drift | x | y | line one line two |*') -Message 'Markdown cells must escape pipes and newlines.'
                Assert-SelfTest -Condition ($markdown -like '*Result: **Failed (1 blocking finding(s))***') -Message 'Markdown must state the failing result.'
            }
        })

    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($case in $cases) {
        try {
            & $case.Test
        }
        catch {
            $failures.Add("Self-test '$($case.Name)' failed: $($_.Exception.Message)")
        }
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
        exit 1
    }

    Write-Host "Repository security-control audit self-test passed ($($cases.Count) cases)."
    exit 0
}

if ($SelfTest) {
    Invoke-SelfTest
}

$controls = Read-ControlManifest -Path $ControlsDirectory

if ([string]::IsNullOrWhiteSpace($Repository)) {
    $Repository = [string]$controls.Settings.repository
}

if ($null -eq (Get-Command -Name gh -CommandType Application -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required. Install it and authenticate with gh auth login, or set GH_TOKEN.'
}

$live = Get-LiveRepositoryState -Repository $Repository -RulesetName ([string]$controls.Ruleset.name)
$results = @(Compare-RepositoryControls -Controls $controls -Live $live)
$outcome = Get-AuditOutcome -Results $results -FailOnUnavailable $FailOnUnavailable.IsPresent
$markdown = Format-AuditMarkdown -Repository $Repository -Results $results -Outcome $outcome -FailOnUnavailable $FailOnUnavailable.IsPresent

Write-AuditResults -Results $results

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $markdown -Encoding utf8
}

if (-not [string]::IsNullOrWhiteSpace($SummaryPath)) {
    $summaryDirectory = Split-Path -Path $SummaryPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($summaryDirectory)) {
        New-Item -ItemType Directory -Path $summaryDirectory -Force | Out-Null
    }

    Set-Content -LiteralPath $SummaryPath -Value $markdown -Encoding utf8
}

if ($outcome.Unavailable.Count -gt 0) {
    $unavailableAction = if ($FailOnUnavailable) { 'These block the audit because -FailOnUnavailable was specified.' } else { 'Rerun with an administrator gh login and -FailOnUnavailable for release-readiness evidence.' }
    Write-Host "$($outcome.Unavailable.Count) control(s) could not be verified with the current credentials. $unavailableAction"
}

if (-not $outcome.Passed) {
    Write-Host "Repository security-control audit failed with $($outcome.Blocking.Count) blocking finding(s):"
    foreach ($finding in $outcome.Blocking) {
        Write-Host "- [$($finding.Status)] $($finding.Control): $($finding.Detail)"
    }

    exit 1
}

Write-Host "Repository security-control audit passed for '$Repository' ($($results.Count) controls checked, $($outcome.Warnings.Count) warning(s))."
