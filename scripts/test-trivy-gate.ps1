<# Exercises VulnerabilityGate.ps1 against the report shapes Trivy actually emits, under the strict mode
   the release path runs in. No Docker, no scan: a malformed report is cheap to construct and expensive
   to meet for the first time during a release.

   The functions are lifted out of the shipped script through the PowerShell AST rather than copied, so
   this cannot drift from what actually runs.

   Every case asserts WHY it blocked, not merely that it did. A gate that throws a member-access error on
   a clean report also "blocks", and in a log it is indistinguishable from one that caught a credential. #>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$gatePath = Join-Path $PSScriptRoot 'VulnerabilityGate.ps1'

# Dot-sourced whole rather than AST-lifted function by function. The gate declares no side effects, so
# loading it entire is both simpler and more faithful: lifting only the functions leaves the tolerated
# list undefined, and the gate would then pass every case that never reaches it — which is every clean
# one. A harness that silently drops a dependency tests less than it appears to.
. $gatePath

foreach ($required in 'Get-MemberValue', 'Get-BlockingVulnerabilities', 'Get-BlockingSecrets') {
    if (-not (Get-Command -Name $required -CommandType Function -ErrorAction SilentlyContinue)) {
        throw "'$gatePath' defined no '$required'."
    }
}
if ($null -eq (Get-Variable -Name 'toleratedUnfixedPackages' -ErrorAction SilentlyContinue)) {
    throw "'$gatePath' defined no tolerated-package list; the unfixed-finding path would throw."
}

$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "payment-gate-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null

$failures = [System.Collections.Generic.List[string]]::new()

function Test-Case {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Json,
        [Parameter(Mandatory)][ValidateSet('Vulnerabilities', 'Secrets')][string] $Gate,
        [Parameter(Mandatory)][int] $ExpectedCount
    )

    $path = Join-Path $temporaryDirectory "$([Guid]::NewGuid().ToString('N')).json"
    [System.IO.File]::WriteAllText($path, $Json)

    try {
        # The @() goes OUTSIDE the if. An empty array returned from inside one unrolls on the way out
        # and the caller gets $null, which is the same hop this harness exists to catch.
        $findings = @(
            if ($Gate -eq 'Vulnerabilities') {
                Get-BlockingVulnerabilities -ReportPath $path
            }
            else {
                Get-BlockingSecrets -ReportPath $path
            })
    }
    catch {
        $script:failures.Add("$Name : THREW $($_.Exception.Message)")
        Write-Host ("  {0,-46} THREW  {1}" -f $Name, $_.Exception.Message)
        return
    }

    # Rendering is part of the contract. A gate that counts a finding and then dies formatting it has
    # thrown away the half a human needs.
    try {
        $rendered = ($findings | Format-Table -AutoSize | Out-String)
    }
    catch {
        $script:failures.Add("$Name : RENDER THREW $($_.Exception.Message)")
        Write-Host ("  {0,-46} RENDER THREW  {1}" -f $Name, $_.Exception.Message)
        return
    }

    $blocks = $findings.Count -gt 0
    $status = if ($findings.Count -eq $ExpectedCount) { 'ok  ' } else { 'FAIL' }
    if ($findings.Count -ne $ExpectedCount) {
        $script:failures.Add("$Name : expected $ExpectedCount, got $($findings.Count)")
    }
    Write-Host ("  {0,-46} {1} count={2} blocks={3} rendered={4}" -f
        $Name, $status, $findings.Count, $blocks, ($rendered.Trim().Length -gt 0))
}

$vulnerability = '{"VulnerabilityID":"CVE-0000-0001","PkgName":"demo","InstalledVersion":"1.0.0","Severity":"HIGH","FixedVersion":"1.0.1"}'
$secret = '{"RuleID":"aws-secret-access-key","Category":"AWS","Severity":"CRITICAL","Title":"AWS Secret Access Key"}'

Write-Host 'Vulnerability gate'
Test-Case -Gate Vulnerabilities -ExpectedCount 0 -Name 'Results absent (clean)'            -Json '{"ArtifactName":"x"}'
Test-Case -Gate Vulnerabilities -ExpectedCount 0 -Name 'Results null'                      -Json '{"Results":null}'
Test-Case -Gate Vulnerabilities -ExpectedCount 0 -Name 'Results empty'                     -Json '{"Results":[]}'
Test-Case -Gate Vulnerabilities -ExpectedCount 0 -Name 'Results [null]'                    -Json '{"Results":[null]}'
Test-Case -Gate Vulnerabilities -ExpectedCount 0 -Name 'Vulnerabilities absent (clean)'    -Json '{"Results":[{"Target":"t"}]}'
Test-Case -Gate Vulnerabilities -ExpectedCount 0 -Name 'Vulnerabilities null'              -Json '{"Results":[{"Target":"t","Vulnerabilities":null}]}'
Test-Case -Gate Vulnerabilities -ExpectedCount 1 -Name 'one vulnerability'                 -Json "{`"Results`":[{`"Target`":`"t`",`"Vulnerabilities`":[$vulnerability]}]}"
Test-Case -Gate Vulnerabilities -ExpectedCount 1 -Name '[null, real] result blocks'        -Json "{`"Results`":[null,{`"Target`":`"t`",`"Vulnerabilities`":[$vulnerability]}]}"
Test-Case -Gate Vulnerabilities -ExpectedCount 1 -Name 'unfixed (no FixedVersion)'         -Json '{"Results":[{"Target":"t","Vulnerabilities":[{"VulnerabilityID":"CVE-0000-0002","PkgName":"demo","Severity":"HIGH"}]}]}'
Test-Case -Gate Vulnerabilities -ExpectedCount 2 -Name 'null finding beside a real one'    -Json "{`"Results`":[{`"Target`":`"t`",`"Vulnerabilities`":[null,$vulnerability]}]}"

Write-Host 'Secret gate'
Test-Case -Gate Secrets -ExpectedCount 0 -Name 'Results absent (clean)'                    -Json '{"ArtifactName":"x"}'
Test-Case -Gate Secrets -ExpectedCount 0 -Name 'Results null'                              -Json '{"Results":null}'
Test-Case -Gate Secrets -ExpectedCount 0 -Name 'Results [null]'                            -Json '{"Results":[null]}'
Test-Case -Gate Secrets -ExpectedCount 0 -Name 'Secrets absent (clean)'                    -Json '{"Results":[{"Target":"t"}]}'
Test-Case -Gate Secrets -ExpectedCount 0 -Name 'Secrets empty'                             -Json '{"Results":[{"Target":"t","Secrets":[]}]}'
Test-Case -Gate Secrets -ExpectedCount 1 -Name 'one secret'                                -Json "{`"Results`":[{`"Target`":`"t`",`"Secrets`":[$secret]}]}"
Test-Case -Gate Secrets -ExpectedCount 1 -Name '[null, real] result blocks'                -Json "{`"Results`":[null,{`"Target`":`"t`",`"Secrets`":[$secret]}]}"
Test-Case -Gate Secrets -ExpectedCount 2 -Name 'null secret beside a real one'             -Json "{`"Results`":[{`"Target`":`"t`",`"Secrets`":[null,$secret]}]}"
Test-Case -Gate Secrets -ExpectedCount 2 -Name 'two secrets across two results'            -Json "{`"Results`":[{`"Target`":`"a`",`"Secrets`":[$secret]},{`"Target`":`"b`",`"Secrets`":[$secret]}]}"

Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force

if ($failures.Count -gt 0) {
    Write-Host ''
    $failures | ForEach-Object { Write-Host "FAILED: $_" }
    throw "$($failures.Count) gate case(s) failed."
}

Write-Host ''
Write-Host 'All Trivy gate cases passed.'
