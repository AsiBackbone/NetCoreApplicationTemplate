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

    [switch]$SelfTest
)

# Summarizes an OWASP ZAP traditional-json report for the package-generated NCAT application (#767) and enforces the
# loopback-only scan boundary. Alerts are report-only during rollout stage 1: they are counted and listed but never fail
# the script. The script fails only when the report is missing or malformed, or when the report or exported URL list
# contains any URL outside -AllowedOrigin. -UrlListPath is the plain URL list written by the ZAP export job (type url).
# When -CanaryUrl is set, the script also fails unless rule -CanaryPluginId raised an alert on that URL. The canary
# proves the passive scanner actually analyzed traffic, so an empty report cannot be mistaken for a clean application.

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

function Get-ZapDastResult {
    param(
        [Parameter(Mandatory = $true)][string]$ReportJson,
        [AllowEmptyCollection()][string[]]$UrlList = @(),
        [Parameter(Mandatory = $true)][string]$Origin,
        [string]$CanaryUrl,
        [string]$CanaryPluginId = '10024'
    )

    $canaryDetected = $false

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
            $instances = @(Get-PropertyValue -InputObject $alert -Name 'instances') | Where-Object { $null -ne $_ }
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
            }

            $countValue = Get-PropertyValue -InputObject $alert -Name 'count'
            $instanceCount = if ($null -ne $countValue -and "$countValue" -match '^\d+$') { [int]$countValue } else { @($instances).Count }

            $counts[$riskName]++
            $alerts.Add([pscustomobject]@{
                    RiskCode  = [int]$riskCode
                    Risk      = $riskName
                    PluginId  = $pluginId
                    Name      = [string](Get-PropertyValue -InputObject $alert -Name 'name')
                    Instances = $instanceCount
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
    }
}

function ConvertTo-ZapDastMarkdown {
    param(
        [Parameter(Mandatory = $true)][object]$Result,
        [Parameter(Mandatory = $true)][string]$Origin
    )

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.AppendLine('### OWASP ZAP DAST (report-only)')
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

    [void]$builder.AppendLine('- Enforcement: none. Alerts are reported for triage and do not fail the job.')
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('| Risk | Alert types |')
    [void]$builder.AppendLine('|:---|---:|')
    foreach ($entry in $Result.Counts.GetEnumerator()) {
        [void]$builder.AppendLine("| $($entry.Key) | $($entry.Value) |")
    }

    if ($Result.Alerts.Count -gt 0) {
        [void]$builder.AppendLine()
        [void]$builder.AppendLine('| Risk | Rule | Alert | Instances |')
        [void]$builder.AppendLine('|:---|:---|:---|---:|')
        foreach ($alert in $Result.Alerts) {
            $name = ($alert.Name -replace '\|', '\|') -replace '[\r\n]+', ' '
            [void]$builder.AppendLine("| $($alert.Risk) | $($alert.PluginId) | $name | $($alert.Instances) |")
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

$result = Get-ZapDastResult `
    -ReportJson (Get-Content -LiteralPath $ReportPath -Raw) `
    -UrlList @(Get-Content -LiteralPath $UrlListPath) `
    -Origin $AllowedOrigin `
    -CanaryUrl $CanaryUrl `
    -CanaryPluginId $CanaryPluginId

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
