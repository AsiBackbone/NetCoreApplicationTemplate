[CmdletBinding()]
param(
    [string]$ReportPath,

    [string]$UrlListPath,

    [ValidatePattern('^https?://[^/?#]+$')]
    [string]$AllowedOrigin = 'http://localhost:8080',

    [string]$SummaryPath,

    [string]$CanaryUrl,

    [ValidatePattern('^\d+$')]
    [string]$CanaryPluginId = '10024',

    [ValidateSet('None', 'Low', 'Medium', 'High')]
    [string]$FailOnRisk = 'None',

    [string]$SuppressionsPath,

    [switch]$SelfTest
)

# Summarizes an OWASP ZAP traditional-json report for the package-generated NCAT application (#767) and enforces the
# loopback-only scan boundary. The script fails when the report is missing or malformed, or when the report or exported
# URL list contains any URL outside -AllowedOrigin. -UrlListPath is the plain URL list written by the ZAP export job
# (type url). When -CanaryUrl is set, the script also fails unless rule -CanaryPluginId raised an alert on that URL. The
# canary proves the passive scanner actually analyzed traffic, so an empty report cannot be mistaken for a clean
# application.
#
# Alert gate: with -FailOnRisk set to Low, Medium or High, the script fails when any alert at or above that risk has an
# instance that no suppression covers. Alerts below the threshold are always reported and never fail. -SuppressionsPath
# names a JSON file of narrow suppressions. Each one names one rule and one anchored URL pattern, with a rationale, an
# owner and a review condition. With -FailOnRisk None (the default) alerts are report-only.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RiskNames = [ordered]@{
    '3' = 'High'
    '2' = 'Medium'
    '1' = 'Low'
    '0' = 'Informational'
}

function Test-InAllowedOrigin {
    param(
        [AllowEmptyString()][string]$Url,
        [Parameter(Mandatory = $true)][string]$Origin
    )

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return $false
    }

    # Scheme and host are case-insensitive, so the origin comparison ignores case consistently. Only the part after
    # the origin (path, query, fragment) may be case-sensitive, and it is never compared here.
    $trimmed = $Url.Trim()
    if ([string]::Equals($trimmed, $Origin, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    foreach ($separator in @('/', '?', '#')) {
        if ($trimmed.StartsWith($Origin + $separator, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Get-PropertyValue {
    param(
        [AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function ConvertTo-ZapSuppressionList {
    param(
        [Parameter(Mandatory = $true)][string]$SuppressionsJson,
        [Parameter(Mandatory = $true)][string]$Origin
    )

    # A suppression must start with the literal authorized origin and a literal path character, and must not match
    # arbitrary paths. These probes stand in for "any other URL under the origin".
    $requiredPrefix = '^' + [System.Text.RegularExpressions.Regex]::Escape($Origin) + '/'
    $breadthProbes = @(
        "$Origin/",
        "$Origin/ncat-suppression-breadth-probe",
        "$Origin/ncat-suppression-breadth-probe/nested?probe=1",
        "$Origin/Zz9-breadth-probe"
    )

    try {
        $document = $SuppressionsJson | ConvertFrom-Json -Depth 16
    }
    catch {
        throw "Alert suppressions file is not valid JSON: $($_.Exception.Message)"
    }

    $entries = Get-PropertyValue -InputObject $document -Name 'suppressions'
    if ($null -eq $document -or $null -eq $document.PSObject.Properties['suppressions']) {
        throw "Alert suppressions file must contain a 'suppressions' array."
    }

    $list = [System.Collections.Generic.List[object]]::new()
    $index = 0
    foreach ($entry in @($entries)) {
        $label = "Suppression #$index"
        if ($null -eq $entry -or $entry -isnot [System.Management.Automation.PSCustomObject]) {
            throw "$label must be an object with pluginId, urlPattern, rationale, owner, and reviewCondition."
        }

        $values = @{}
        foreach ($field in @('pluginId', 'urlPattern', 'rationale', 'owner', 'reviewCondition')) {
            $value = [string](Get-PropertyValue -InputObject $entry -Name $field)
            if ([string]::IsNullOrWhiteSpace($value)) {
                throw "$label is missing required field '$field'."
            }

            $values[$field] = $value.Trim()
        }

        if ($values.pluginId -notmatch '^\d+$') {
            throw "$label has non-numeric pluginId '$($values.pluginId)'."
        }

        # Narrow by construction: anchored at both ends, rooted at the literal authorized origin, and naming exactly one
        # literal URL path. Only the query string, after an escaped \?, may use regular-expression syntax.
        $pattern = $values.urlPattern
        if (-not $pattern.StartsWith('^') -or -not $pattern.EndsWith('$') -or $pattern.EndsWith('\$')) {
            throw "$label urlPattern must be anchored with ^ and `$: '$pattern'."
        }

        if (-not $pattern.StartsWith($requiredPrefix, [System.StringComparison]::Ordinal) -or
            $pattern.Length -le $requiredPrefix.Length -or
            $pattern[$requiredPrefix.Length] -notmatch '[A-Za-z0-9_~-]') {
            throw "$label urlPattern must start with the literal origin '$requiredPrefix' followed by a literal path character: '$pattern'."
        }

        $body = $pattern.Substring($requiredPrefix.Length, $pattern.Length - $requiredPrefix.Length - 1)
        $queryStart = $body.IndexOf('\?', [System.StringComparison]::Ordinal)
        $pathPart = if ($queryStart -ge 0) { $body.Substring(0, $queryStart) } else { $body }
        if ($pathPart -notmatch '^(?:[A-Za-z0-9_~%/-]|\\[.\-])+$') {
            throw "$label urlPattern must name one literal URL path: only letters, digits, '/', '_', '~', '%', '-', and escaped '\.' or '\-' are allowed before an optional '\?' query part: '$pattern'."
        }

        # Alternation would let a second branch escape the literal path, so an unescaped | is never allowed.
        if ($pattern -match '(?<!\\)(?:\\\\)*\|') {
            throw "$label urlPattern must not use alternation (|): '$pattern'."
        }

        try {
            $regex = [System.Text.RegularExpressions.Regex]::new(
                $pattern,
                [System.Text.RegularExpressions.RegexOptions]::CultureInvariant,
                [TimeSpan]::FromSeconds(1))
        }
        catch {
            throw "$label urlPattern is not a valid regular expression: $($_.Exception.Message)"
        }

        foreach ($probe in $breadthProbes) {
            if ($regex.IsMatch($probe)) {
                throw "$label urlPattern is too broad: it matches '$probe'. Name a specific URL path: '$pattern'."
            }
        }

        $list.Add([pscustomobject]@{
                Index           = $index
                PluginId        = $values.pluginId
                UrlPattern      = $pattern
                Regex           = $regex
                Rationale       = $values.rationale
                Owner           = $values.owner
                ReviewCondition = $values.reviewCondition
            })
        $index++
    }

    return , $list.ToArray()
}

function Get-ZapDastResult {
    param(
        [Parameter(Mandatory = $true)][string]$ReportJson,
        [AllowEmptyCollection()][string[]]$UrlList = @(),
        [Parameter(Mandatory = $true)][string]$Origin,
        [string]$CanaryUrl,
        [string]$CanaryPluginId = '10024',
        [ValidateSet('None', 'Low', 'Medium', 'High')][string]$FailOnRisk = 'None',
        [AllowEmptyCollection()][object[]]$Suppressions = @()
    )

    $canaryDetected = $false
    $failOnRiskCode = switch ($FailOnRisk) {
        'Low' { 1 }
        'Medium' { 2 }
        'High' { 3 }
        default { [int]::MaxValue }
    }

    $suppressionMatches = @{}
    foreach ($suppression in $Suppressions) {
        $suppressionMatches[$suppression.Index] = 0
    }

    $gateViolations = [System.Collections.Generic.List[object]]::new()

    try {
        $report = $ReportJson | ConvertFrom-Json -Depth 64
    }
    catch {
        throw "ZAP report is not valid JSON: $($_.Exception.Message)"
    }

    $sites = Get-PropertyValue -InputObject $report -Name 'site'
    if ($null -eq $sites) {
        throw 'ZAP report does not contain a site list.'
    }

    $scopeViolations = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
    $alerts = [System.Collections.Generic.List[object]]::new()
    $counts = [ordered]@{}
    foreach ($riskName in $script:RiskNames.Values) {
        $counts[$riskName] = 0
    }

    foreach ($site in @($sites)) {
        $siteName = [string](Get-PropertyValue -InputObject $site -Name '@name')
        if (-not (Test-InAllowedOrigin -Url $siteName -Origin $Origin)) {
            [void]$scopeViolations.Add("report site: $siteName")
        }

        foreach ($alert in @(Get-PropertyValue -InputObject $site -Name 'alerts')) {
            if ($null -eq $alert) {
                continue
            }

            $riskCode = [string](Get-PropertyValue -InputObject $alert -Name 'riskcode')
            $riskName = $script:RiskNames[$riskCode]
            if ($null -eq $riskName) {
                throw "ZAP alert '$(Get-PropertyValue -InputObject $alert -Name 'name')' has unknown riskcode '$riskCode'."
            }

            $pluginId = [string](Get-PropertyValue -InputObject $alert -Name 'pluginid')
            $alertName = [string](Get-PropertyValue -InputObject $alert -Name 'name')
            $instances = @(@(Get-PropertyValue -InputObject $alert -Name 'instances') | Where-Object { $null -ne $_ })
            $suppressedCount = 0
            foreach ($instance in $instances) {
                $uri = [string](Get-PropertyValue -InputObject $instance -Name 'uri')
                if (-not (Test-InAllowedOrigin -Url $uri -Origin $Origin)) {
                    [void]$scopeViolations.Add("alert instance: $uri")
                }

                if (-not [string]::IsNullOrWhiteSpace($CanaryUrl) -and
                    $pluginId -eq $CanaryPluginId -and
                    [string]::Equals($uri.Trim(), $CanaryUrl.Trim(), [System.StringComparison]::OrdinalIgnoreCase)) {
                    $canaryDetected = $true
                }

                foreach ($suppression in $Suppressions) {
                    if ($suppression.PluginId -eq $pluginId -and $suppression.Regex.IsMatch($uri.Trim())) {
                        $suppressionMatches[$suppression.Index]++
                        $suppressedCount++
                        break
                    }
                }
            }

            $countValue = Get-PropertyValue -InputObject $alert -Name 'count'
            $instanceCount = if ($null -ne $countValue -and "$countValue" -match '^\d+$') { [int]$countValue } else { $instances.Count }

            # Only listed instances can be shown to be suppressed. Any instance the count reports but the report does not
            # list (or an alert with no listed instances at all) is treated as unsuppressed.
            $totalInstances = [Math]::Max([Math]::Max($instanceCount, $instances.Count), 1)
            $unsuppressedCount = $totalInstances - $suppressedCount

            if ([int]$riskCode -ge $failOnRiskCode -and $unsuppressedCount -gt 0) {
                $gateViolations.Add([pscustomobject]@{
                        Risk         = $riskName
                        PluginId     = $pluginId
                        Name         = $alertName
                        Unsuppressed = $unsuppressedCount
                    })
            }

            $counts[$riskName]++
            $alerts.Add([pscustomobject]@{
                    RiskCode   = [int]$riskCode
                    Risk       = $riskName
                    PluginId   = $pluginId
                    Name       = $alertName
                    Instances  = $instanceCount
                    Suppressed = $suppressedCount
                })
        }
    }

    foreach ($line in $UrlList) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        if (-not (Test-InAllowedOrigin -Url $line -Origin $Origin)) {
            [void]$scopeViolations.Add("requested URL: $($line.Trim())")
        }
    }

    $sortedAlerts = @($alerts | Sort-Object -Property @{ Expression = 'RiskCode'; Descending = $true }, PluginId, Name)

    return [pscustomobject]@{
        ZapVersion        = [string](Get-PropertyValue -InputObject $report -Name '@version')
        Counts            = $counts
        Alerts            = $sortedAlerts
        RequestedUrlCount = @($UrlList | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
        ScopeViolations   = @($scopeViolations)
        CanaryUrl         = $CanaryUrl
        CanaryPluginId    = $CanaryPluginId
        CanaryDetected    = $canaryDetected
        FailOnRisk        = $FailOnRisk
        GateViolations    = @($gateViolations)
        SuppressionCount  = @($Suppressions).Count
        UnusedSuppressions = @($Suppressions | Where-Object { $suppressionMatches[$_.Index] -eq 0 })
    }
}

function ConvertTo-ZapDastMarkdown {
    param(
        [Parameter(Mandatory = $true)][object]$Result,
        [Parameter(Mandatory = $true)][string]$Origin
    )

    $enforcing = $Result.FailOnRisk -ne 'None'
    $builder = [System.Text.StringBuilder]::new()
    if ($enforcing) {
        [void]$builder.AppendLine("### OWASP ZAP DAST (fails on unsuppressed $($Result.FailOnRisk) or higher)")
    }
    else {
        [void]$builder.AppendLine('### OWASP ZAP DAST (report-only)')
    }
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("- Target: ``$Origin`` (package-generated application, loopback only)")
    if (-not [string]::IsNullOrWhiteSpace($Result.ZapVersion)) {
        [void]$builder.AppendLine("- ZAP version: $($Result.ZapVersion)")
    }

    [void]$builder.AppendLine("- Requested URLs: $($Result.RequestedUrlCount)")
    if ($Result.ScopeViolations.Count -eq 0) {
        [void]$builder.AppendLine('- Scope guard: passed (no URL outside the authorized origin)')
    }
    else {
        [void]$builder.AppendLine("- Scope guard: **failed** ($($Result.ScopeViolations.Count) out-of-scope URL(s))")
    }

    if (-not [string]::IsNullOrWhiteSpace($Result.CanaryUrl)) {
        if ($Result.CanaryDetected) {
            [void]$builder.AppendLine("- Passive-scan canary: detected (rule $($Result.CanaryPluginId) on the canary URL)")
        }
        else {
            [void]$builder.AppendLine("- Passive-scan canary: **missing** (rule $($Result.CanaryPluginId) did not fire on the canary URL)")
        }
    }

    if (-not $enforcing) {
        [void]$builder.AppendLine('- Alert gate: off. Alerts are reported for triage and do not fail the job.')
    }
    elseif ($Result.GateViolations.Count -eq 0) {
        [void]$builder.AppendLine("- Alert gate: passed (no unsuppressed $($Result.FailOnRisk) or higher alert; lower risks are report-only)")
    }
    else {
        [void]$builder.AppendLine("- Alert gate: **failed** ($($Result.GateViolations.Count) alert type(s) at $($Result.FailOnRisk) or higher with unsuppressed instances)")
    }

    $unusedCount = @($Result.UnusedSuppressions).Count
    [void]$builder.AppendLine("- Suppressions: $($Result.SuppressionCount) configured, $unusedCount unused")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('| Risk | Alert types |')
    [void]$builder.AppendLine('|:---|---:|')
    foreach ($entry in $Result.Counts.GetEnumerator()) {
        [void]$builder.AppendLine("| $($entry.Key) | $($entry.Value) |")
    }

    if ($Result.Alerts.Count -gt 0) {
        [void]$builder.AppendLine()
        [void]$builder.AppendLine('| Risk | Rule | Alert | Instances | Suppressed |')
        [void]$builder.AppendLine('|:---|:---|:---|---:|---:|')
        foreach ($alert in $Result.Alerts) {
            $name = ($alert.Name -replace '\|', '\|') -replace '[\r\n]+', ' '
            [void]$builder.AppendLine("| $($alert.Risk) | $($alert.PluginId) | $name | $($alert.Instances) | $($alert.Suppressed) |")
        }
    }

    if ($Result.GateViolations.Count -gt 0) {
        [void]$builder.AppendLine()
        [void]$builder.AppendLine('Alerts failing the gate:')
        [void]$builder.AppendLine()
        foreach ($violation in $Result.GateViolations) {
            $name = ($violation.Name -replace '[\r\n]+', ' ')
            [void]$builder.AppendLine("- $($violation.Risk) rule $($violation.PluginId) ($name): $($violation.Unsuppressed) unsuppressed instance(s)")
        }
    }

    if ($unusedCount -gt 0) {
        [void]$builder.AppendLine()
        [void]$builder.AppendLine('Unused suppressions (review and remove when the review condition is met):')
        [void]$builder.AppendLine()
        foreach ($suppression in $Result.UnusedSuppressions) {
            [void]$builder.AppendLine("- rule $($suppression.PluginId) ``$($suppression.UrlPattern)`` (owner: $($suppression.Owner))")
        }
    }

    if ($Result.ScopeViolations.Count -gt 0) {
        [void]$builder.AppendLine()
        [void]$builder.AppendLine('Out-of-scope URLs:')
        [void]$builder.AppendLine()
        foreach ($violation in $Result.ScopeViolations) {
            [void]$builder.AppendLine("- ``$violation``")
        }
    }

    return $builder.ToString()
}

function Invoke-SelfTest {
    $origin = 'http://localhost:8080'
    $failures = [System.Collections.Generic.List[string]]::new()

    function Assert-True {
        param([bool]$Condition, [string]$Message)
        if (-not $Condition) {
            $failures.Add($Message)
        }
    }

    Assert-True (Test-InAllowedOrigin -Url 'http://localhost:8080' -Origin $origin) 'Bare origin must be in scope.'
    Assert-True (Test-InAllowedOrigin -Url 'http://localhost:8080/health/live' -Origin $origin) 'Origin path must be in scope.'
    Assert-True (Test-InAllowedOrigin -Url 'http://localhost:8080?x=1' -Origin $origin) 'Origin query must be in scope.'
    Assert-True (-not (Test-InAllowedOrigin -Url 'http://localhost:80801/' -Origin $origin)) 'Port prefix must be out of scope.'
    Assert-True (-not (Test-InAllowedOrigin -Url 'http://localhost:8080.example.com/' -Origin $origin)) 'Host prefix must be out of scope.'
    Assert-True (-not (Test-InAllowedOrigin -Url 'https://localhost:8080/' -Origin $origin)) 'Scheme change must be out of scope.'
    Assert-True (-not (Test-InAllowedOrigin -Url 'https://login.example.com/authorize' -Origin $origin)) 'External host must be out of scope.'
    Assert-True (-not (Test-InAllowedOrigin -Url '' -Origin $origin)) 'Empty URL must be out of scope.'
    Assert-True (Test-InAllowedOrigin -Url 'HTTP://LOCALHOST:8080' -Origin $origin) 'Bare origin must match regardless of case.'
    Assert-True (Test-InAllowedOrigin -Url 'HTTP://LocalHost:8080/Account/Login' -Origin $origin) 'Origin path must match regardless of origin case.'
    Assert-True (-not (Test-InAllowedOrigin -Url 'HTTP://LOCALHOST:80801' -Origin $origin)) 'Case-insensitive matching must not admit a port prefix.'

    $cleanReport = @'
{
  "@programName": "ZAP",
  "@version": "2.17.0",
  "site": [
    {
      "@name": "http://localhost:8080",
      "alerts": [
        { "pluginid": "10049", "name": "Storable and Cacheable Content", "riskcode": "0", "count": "2",
          "instances": [ { "uri": "http://localhost:8080/" }, { "uri": "http://localhost:8080/health/live" } ] },
        { "pluginid": "10038", "name": "Content Security Policy | Test", "riskcode": "2", "count": "1",
          "instances": [ { "uri": "http://localhost:8080/Account/Login" } ] }
      ]
    }
  ]
}
'@
    $clean = Get-ZapDastResult -ReportJson $cleanReport -UrlList @('http://localhost:8080/', 'http://localhost:8080/robots.txt', '') -Origin $origin
    Assert-True ($clean.ScopeViolations.Count -eq 0) 'Clean report must have no scope violations.'
    Assert-True ($clean.Counts['Medium'] -eq 1) 'Clean report must count one Medium alert type.'
    Assert-True ($clean.Counts['Informational'] -eq 1) 'Clean report must count one Informational alert type.'
    Assert-True ($clean.Counts['High'] -eq 0) 'Clean report must count zero High alert types.'
    Assert-True ($clean.Alerts[0].Risk -eq 'Medium') 'Alerts must be ordered by descending risk.'
    Assert-True ($clean.RequestedUrlCount -eq 2) 'Blank URL-list lines must be ignored.'
    $markdown = ConvertTo-ZapDastMarkdown -Result $clean -Origin $origin
    Assert-True ($markdown.Contains('Scope guard: passed')) 'Clean summary must report the scope guard as passed.'
    Assert-True ($markdown.Contains('Content Security Policy \| Test')) 'Summary must escape table pipes in alert names.'

    $leakyReport = @'
{
  "site": [
    { "@name": "http://localhost:8080", "alerts": [
        { "pluginid": "1", "name": "x", "riskcode": "1", "instances": [ { "uri": "https://idp.example.com/cb" } ] } ] },
    { "@name": "https://idp.example.com", "alerts": [] }
  ]
}
'@
    $leaky = Get-ZapDastResult -ReportJson $leakyReport -UrlList @('https://cdn.example.net/x.js') -Origin $origin
    Assert-True ($leaky.ScopeViolations.Count -eq 3) "Leaky report must report 3 scope violations, found $($leaky.ScopeViolations.Count)."
    Assert-True ((ConvertTo-ZapDastMarkdown -Result $leaky -Origin $origin).Contains('Scope guard: **failed**')) 'Leaky summary must report failure.'

    foreach ($invalid in @('not json', '{ "@version": "2.17.0" }', '{ "site": [ { "@name": "http://localhost:8080", "alerts": [ { "riskcode": "9" } ] } ] }')) {
        $threw = $false
        try {
            [void](Get-ZapDastResult -ReportJson $invalid -Origin $origin)
        }
        catch {
            $threw = $true
        }

        Assert-True $threw "Malformed report must throw: $invalid"
    }

    $empty = Get-ZapDastResult -ReportJson '{ "site": [ { "@name": "http://localhost:8080", "alerts": [] } ] }' -Origin $origin
    Assert-True ($empty.Alerts.Count -eq 0) 'Report without alerts must produce no alert rows.'

    $canaryUrl = 'http://localhost:8080/Account/Login?password=zap-passive-canary'
    $canaryReport = @'
{
  "site": [ { "@name": "http://localhost:8080", "alerts": [
    { "pluginid": "10024", "name": "Information Disclosure - Sensitive Information in URL", "riskcode": "0",
      "instances": [ { "uri": "http://localhost:8080/Account/Login?password=zap-passive-canary" } ] } ] } ]
}
'@
    $withCanary = Get-ZapDastResult -ReportJson $canaryReport -Origin $origin -CanaryUrl $canaryUrl
    Assert-True $withCanary.CanaryDetected 'Canary alert on the canary URL must be detected.'
    Assert-True ((ConvertTo-ZapDastMarkdown -Result $withCanary -Origin $origin).Contains('Passive-scan canary: detected')) 'Summary must report a detected canary.'

    $silent = Get-ZapDastResult -ReportJson '{ "site": [ { "@name": "http://localhost:8080", "alerts": [] } ] }' -Origin $origin -CanaryUrl $canaryUrl
    Assert-True (-not $silent.CanaryDetected) 'An empty report must not satisfy the canary.'
    Assert-True ((ConvertTo-ZapDastMarkdown -Result $silent -Origin $origin).Contains('Passive-scan canary: **missing**')) 'Summary must report a missing canary.'

    $wrongRule = Get-ZapDastResult -ReportJson ($canaryReport -replace '"10024"', '"10049"') -Origin $origin -CanaryUrl $canaryUrl
    Assert-True (-not $wrongRule.CanaryDetected) 'A different rule on the canary URL must not satisfy the canary.'

    $wrongUrl = Get-ZapDastResult -ReportJson ($canaryReport -replace 'zap-passive-canary', 'other') -Origin $origin -CanaryUrl $canaryUrl
    Assert-True (-not $wrongUrl.CanaryDetected) 'The canary rule on another URL must not satisfy the canary.'

    Assert-True (-not ((ConvertTo-ZapDastMarkdown -Result $empty -Origin $origin).Contains('Passive-scan canary'))) 'Summary must omit the canary line when no canary is configured.'

    # Alert gate. Mirrors the insecure-headers regression fixture: Medium CSP and anti-clickjacking alerts.
    $gateReport = @'
{
  "site": [ { "@name": "http://localhost:8080", "alerts": [
    { "pluginid": "10038", "name": "Content Security Policy (CSP) Header Not Set", "riskcode": "2", "count": "2",
      "instances": [ { "uri": "http://localhost:8080/Account/Login" }, { "uri": "http://localhost:8080/robots.txt" } ] },
    { "pluginid": "10021", "name": "X-Content-Type-Options Header Missing", "riskcode": "1", "count": "1",
      "instances": [ { "uri": "http://localhost:8080/css/landing.css" } ] },
    { "pluginid": "10024", "name": "Information Disclosure - Sensitive Information in URL", "riskcode": "0", "count": "1",
      "instances": [ { "uri": "http://localhost:8080/Account/Login?password=zap-passive-canary" } ] } ] } ]
}
'@
    $reportOnly = Get-ZapDastResult -ReportJson $gateReport -Origin $origin
    Assert-True ($reportOnly.GateViolations.Count -eq 0) 'FailOnRisk None must never produce gate violations.'
    Assert-True ((ConvertTo-ZapDastMarkdown -Result $reportOnly -Origin $origin).Contains('Alert gate: off')) 'Report-only summary must say the gate is off.'

    $mediumGate = Get-ZapDastResult -ReportJson $gateReport -Origin $origin -FailOnRisk Medium
    Assert-True ($mediumGate.GateViolations.Count -eq 1) "An unsuppressed Medium alert must fail a Medium gate (found $($mediumGate.GateViolations.Count))."
    Assert-True ($mediumGate.GateViolations[0].PluginId -eq '10038' -and $mediumGate.GateViolations[0].Unsuppressed -eq 2) 'The Medium gate must report both unsuppressed CSP instances.'
    $mediumMarkdown = ConvertTo-ZapDastMarkdown -Result $mediumGate -Origin $origin
    Assert-True ($mediumMarkdown.Contains('Alert gate: **failed**') -and $mediumMarkdown.Contains('Alerts failing the gate:')) 'A failing gate must be visible in the summary.'

    $highGate = Get-ZapDastResult -ReportJson $gateReport -Origin $origin -FailOnRisk High
    Assert-True ($highGate.GateViolations.Count -eq 0) 'A Medium alert must not fail a High gate.'

    $highReport = $gateReport -replace '"pluginid": "10038", "name": "Content Security Policy \(CSP\) Header Not Set", "riskcode": "2"', '"pluginid": "40012", "name": "Cross Site Scripting (Reflected)", "riskcode": "3"'
    Assert-True ($highReport -ne $gateReport) 'The High fixture must replace the Medium alert.'
    $highOnHigh = Get-ZapDastResult -ReportJson $highReport -Origin $origin -FailOnRisk High
    Assert-True ($highOnHigh.GateViolations.Count -eq 1 -and $highOnHigh.GateViolations[0].Risk -eq 'High' -and $highOnHigh.GateViolations[0].Unsuppressed -eq 2) 'An unsuppressed High alert must fail a High gate.'
    $highOnMedium = Get-ZapDastResult -ReportJson $highReport -Origin $origin -FailOnRisk Medium
    Assert-True ($highOnMedium.GateViolations.Count -eq 1 -and $highOnMedium.GateViolations[0].PluginId -eq '40012') 'An unsuppressed High alert must fail the Medium gate used in CI.'
    $lowGate = Get-ZapDastResult -ReportJson $gateReport -Origin $origin -FailOnRisk Low
    Assert-True ($lowGate.GateViolations.Count -eq 2) 'A Low gate must also catch the Low alert but never Informational ones.'

    $narrow = ConvertTo-ZapSuppressionList -Origin $origin -SuppressionsJson @'
{ "suppressions": [
  { "pluginId": "10038", "urlPattern": "^http://localhost:8080/robots\\.txt$", "rationale": "Plain-text file; CSP does not apply.",
    "owner": "maintainers", "reviewCondition": "Revisit if robots.txt becomes HTML." },
  { "pluginId": "10098", "urlPattern": "^http://localhost:8080/unused$", "rationale": "Unused example.",
    "owner": "maintainers", "reviewCondition": "Remove when unused." } ] }
'@
    $partial = Get-ZapDastResult -ReportJson $gateReport -Origin $origin -FailOnRisk Medium -Suppressions $narrow
    Assert-True ($partial.GateViolations.Count -eq 1 -and $partial.GateViolations[0].Unsuppressed -eq 1) 'A suppression covering one instance must leave the other instance failing the gate.'
    Assert-True (@($partial.UnusedSuppressions).Count -eq 1 -and $partial.UnusedSuppressions[0].PluginId -eq '10098') 'Suppressions that match nothing must be reported as unused.'
    Assert-True ((ConvertTo-ZapDastMarkdown -Result $partial -Origin $origin).Contains('| Medium | 10038 | Content Security Policy (CSP) Header Not Set | 2 | 1 |')) 'The summary must show suppressed instance counts.'

    $both = ConvertTo-ZapSuppressionList -Origin $origin -SuppressionsJson @'
{ "suppressions": [
  { "pluginId": "10038", "urlPattern": "^http://localhost:8080/robots\\.txt$", "rationale": "Fixture only.",
    "owner": "maintainers", "reviewCondition": "Self-test fixture." },
  { "pluginId": "10038", "urlPattern": "^http://localhost:8080/Account/Login$", "rationale": "Fixture only.",
    "owner": "maintainers", "reviewCondition": "Self-test fixture." } ] }
'@
    $covered = Get-ZapDastResult -ReportJson $gateReport -Origin $origin -FailOnRisk Medium -Suppressions $both
    Assert-True ($covered.GateViolations.Count -eq 0) 'An alert whose every instance is suppressed must pass the gate.'

    $wrongPlugin = ConvertTo-ZapSuppressionList -Origin $origin -SuppressionsJson '{ "suppressions": [ { "pluginId": "10020", "urlPattern": "^http://localhost:8080/Account/Login$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }'
    $otherRule = Get-ZapDastResult -ReportJson $gateReport -Origin $origin -FailOnRisk Medium -Suppressions $wrongPlugin
    Assert-True ($otherRule.GateViolations.Count -eq 1) 'A suppression for a different rule must not suppress the alert.'

    $noInstances = Get-ZapDastResult -ReportJson '{ "site": [ { "@name": "http://localhost:8080", "alerts": [ { "pluginid": "40012", "name": "x", "riskcode": "3", "count": "1" } ] } ] }' -Origin $origin -FailOnRisk Medium -Suppressions $both
    Assert-True ($noInstances.GateViolations.Count -eq 1) 'An alert without listed instances must not be treated as suppressed.'

    $truncated = Get-ZapDastResult -ReportJson ($gateReport -replace '"riskcode": "2", "count": "2"', '"riskcode": "2", "count": "3"') -Origin $origin -FailOnRisk Medium -Suppressions $both
    Assert-True ($truncated.GateViolations.Count -eq 1 -and $truncated.GateViolations[0].Unsuppressed -eq 1) 'Instances counted but not listed must be treated as unsuppressed even when every listed instance is suppressed.'

    $queryScoped = ConvertTo-ZapSuppressionList -Origin $origin -SuppressionsJson '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/Account/Login\\?ReturnUrl=.*$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }'
    Assert-True (@($queryScoped).Count -eq 1) 'A literal path with a regular-expression query part must be accepted.'
    Assert-True ($queryScoped[0].Regex.IsMatch('http://localhost:8080/Account/Login?ReturnUrl=%2Frobots.txt')) 'A query-scoped suppression must match its path with any query value.'
    Assert-True (-not $queryScoped[0].Regex.IsMatch('http://localhost:8080/Account/LoginX?ReturnUrl=1')) 'A query-scoped suppression must not match a different path.'

    $emptyList = ConvertTo-ZapSuppressionList -Origin $origin -SuppressionsJson '{ "suppressions": [] }'
    Assert-True (@($emptyList).Count -eq 0) 'An empty suppressions array must load as an empty list.'

    $invalidSuppressions = @(
        'not json',
        '{ }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/x$", "rationale": "r", "owner": "o" } ] }',
        '{ "suppressions": [ { "pluginId": "abc", "urlPattern": "^http://localhost:8080/x$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "http://localhost:8080/x", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^.*$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^.*/x$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/x(y$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        # Null or non-object entries.
        '{ "suppressions": [ null ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/x$", "rationale": "r", "owner": "o", "reviewCondition": "c" }, null ] }',
        '{ "suppressions": [ "^http://localhost:8080/x$" ] }',
        '{ "suppressions": null }',
        # Catch-alls rooted at the authorized origin, and patterns for any other origin.
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/.*$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/(.*)$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/[^?]*$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/\\w+$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/Z?.*$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/n.*$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^https?://localhost:8080/x$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://example.com/x$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        # Prefix catch-alls and other non-literal paths that dodge the fixed probes.
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/a.*$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/Account.*$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/Account/[A-Za-z]+$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/Account/Login|css/x$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/css/x.css$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/x\\$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/x\\?y|^http://localhost:8080/A.*$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }',
        '{ "suppressions": [ { "pluginId": "10038", "urlPattern": "^http://localhost:8080/x\\?(y|z)$", "rationale": "r", "owner": "o", "reviewCondition": "c" } ] }'
    )
    foreach ($invalid in $invalidSuppressions) {
        $threw = $false
        try {
            [void](ConvertTo-ZapSuppressionList -Origin $origin -SuppressionsJson $invalid)
        }
        catch {
            $threw = $true
        }

        Assert-True $threw "Invalid suppressions must throw: $invalid"
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Error -Message $_ -ErrorAction Continue }
        throw "Write-ZapDastSummary self-test failed with $($failures.Count) failure(s)."
    }

    Write-Host 'Write-ZapDastSummary self-test passed.'
}

if ($SelfTest) {
    Invoke-SelfTest
    return
}

if ([string]::IsNullOrWhiteSpace($ReportPath) -or -not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
    throw "ZAP JSON report was not found: '$ReportPath'."
}

if ([string]::IsNullOrWhiteSpace($UrlListPath) -or -not (Test-Path -LiteralPath $UrlListPath -PathType Leaf)) {
    throw "ZAP URL export was not found: '$UrlListPath'."
}

$suppressions = @()
if (-not [string]::IsNullOrWhiteSpace($SuppressionsPath)) {
    if (-not (Test-Path -LiteralPath $SuppressionsPath -PathType Leaf)) {
        throw "Alert suppressions file was not found: '$SuppressionsPath'."
    }

    $suppressions = ConvertTo-ZapSuppressionList -Origin $AllowedOrigin -SuppressionsJson (Get-Content -LiteralPath $SuppressionsPath -Raw)
}

$result = Get-ZapDastResult `
    -ReportJson (Get-Content -LiteralPath $ReportPath -Raw) `
    -UrlList @(Get-Content -LiteralPath $UrlListPath) `
    -Origin $AllowedOrigin `
    -CanaryUrl $CanaryUrl `
    -CanaryPluginId $CanaryPluginId `
    -FailOnRisk $FailOnRisk `
    -Suppressions $suppressions

$summary = ConvertTo-ZapDastMarkdown -Result $result -Origin $AllowedOrigin
Write-Host $summary

if (-not [string]::IsNullOrWhiteSpace($SummaryPath)) {
    Add-Content -LiteralPath $SummaryPath -Value $summary -Encoding utf8
}

if ($result.ScopeViolations.Count -gt 0) {
    throw "ZAP requested $($result.ScopeViolations.Count) URL(s) outside the authorized origin '$AllowedOrigin'."
}

if (-not [string]::IsNullOrWhiteSpace($CanaryUrl) -and -not $result.CanaryDetected) {
    throw "Passive-scan canary was not detected: rule $CanaryPluginId raised no alert on '$CanaryUrl'. The passive scanner may not have analyzed the traffic, so this report cannot be treated as clean."
}

if ($result.GateViolations.Count -gt 0) {
    $names = ($result.GateViolations | ForEach-Object { "$($_.Risk) $($_.PluginId)" }) -join ', '
    throw "ZAP reported $($result.GateViolations.Count) unsuppressed alert type(s) at $FailOnRisk or higher: $names. Fix the application, or record a narrow suppression in .zap/alert-suppressions.json with a rationale, owner, and review condition."
}
