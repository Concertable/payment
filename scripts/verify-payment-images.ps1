<# Builds every Payment deployable to an image archive, scans the archive, and records what it found.

   The archive is the release candidate. Nothing downstream rebuilds: publish-images.yml loads these
   exact tarballs and pushes them, so the bytes Trivy scanned are the bytes that ship. A rebuild in the
   publish job would scan one image and ship another. #>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $ArchiveDirectory = 'artifacts/images',
    [string] $EvidenceDirectory = 'artifacts/evidence',
    [string] $ResultPath = 'artifacts/images/payment-images.json',
    [string] $BuildVersion,
    [string] $TrivyImage = 'aquasec/trivy:0.74.0@sha256:62b1e65e8869bc4b4c6aa4fa2b21595256c7c2f6018a9d9ad61caf87187c1969',
    # Cold, Trivy re-downloads its database and re-analyses every layer: measured here at 10m30s and a
    # deadline, against ~100s warm for the same archive. CI passes a path it caches between runs.
    [string] $TrivyCacheDirectory = 'artifacts/trivy-cache',
    [string[]] $Severity = @('HIGH', 'CRITICAL'),
    # Trivy's own default ceiling is too low for these images: auth's scan of one took 11m16s on an idle
    # machine and died on the default. Raising it costs nothing on a fast run.
    [string] $TrivyTimeout = '30m',
    [switch] $SkipScan
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'VulnerabilityGate.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot

$revision = (& git -C $repositoryRoot rev-parse HEAD)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($revision)) {
    throw 'Could not resolve the Payment source revision.'
}
$revision = $revision.Trim()

if ([string]::IsNullOrWhiteSpace($BuildVersion)) {
    $BuildVersion = "0.0.0-local.$($revision.Substring(0, 12))"
}

$targets = @(
    [ordered]@{ Name = 'payment-web'; Repository = 'payment-web'; Project = 'src/Concertable.Payment.Web/Concertable.Payment.Web.csproj'; Tag = $revision; LatestTag = 'latest' }
    [ordered]@{ Name = 'payment-workers'; Repository = 'payment-workers'; Project = 'src/Concertable.Payment.Workers/Concertable.Payment.Workers.csproj'; Tag = $revision; LatestTag = 'latest' }
    [ordered]@{ Name = 'payment-web-e2e'; Repository = 'payment-web'; Project = 'tests/E2ETests/Concertable.Payment.E2ETests.Web/Concertable.Payment.E2ETests.Web.csproj'; Tag = "e2e-$revision"; LatestTag = 'e2e-latest' }
    [ordered]@{ Name = 'payment-workers-e2e'; Repository = 'payment-workers'; Project = 'tests/E2ETests/Concertable.Payment.E2ETests.Workers/Concertable.Payment.E2ETests.Workers.csproj'; Tag = "e2e-$revision"; LatestTag = 'e2e-latest' }
)

function Resolve-UnderRoot {
    param([Parameter(Mandatory)][string] $Path)

    $full = if ([System.IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $repositoryRoot $Path }
    New-Item -ItemType Directory -Path $full -Force | Out-Null
    return (Resolve-Path -LiteralPath $full).Path
}

$resolvedArchiveDirectory = Resolve-UnderRoot -Path $ArchiveDirectory
$resolvedEvidenceDirectory = Resolve-UnderRoot -Path $EvidenceDirectory
$resolvedTrivyCacheDirectory = Resolve-UnderRoot -Path $TrivyCacheDirectory
$resolvedResultPath = if ([System.IO.Path]::IsPathRooted($ResultPath)) { $ResultPath } else { Join-Path $repositoryRoot $ResultPath }

function Invoke-TrivyScan {
    param(
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][string] $Description,
        [Parameter(Mandatory)][string] $OutputFile
    )

    # --exit-code is deliberately never passed. Trivy exits 1 for a fatal error as readily as for a
    # finding — auth hit `semaphore acquire: context deadline exceeded` after 11m16s on an idle machine
    # and it read as a secret detection — so the exit code alone cannot tell a verdict from a crash.
    # The report file is what separates them: a real finding writes it, the fatal path never does.
    if (Test-Path -LiteralPath $OutputFile) {
        Remove-Item -LiteralPath $OutputFile -Force
    }

    & docker @Arguments
    $trivyExit = $LASTEXITCODE

    # Presence is not validity. A scanner killed for disk after --output has created the file but before
    # it writes leaves it present and zero-byte, which would otherwise pass this check and then parse to
    # nothing downstream — an interrupted scan reading as a clean one.
    if (-not (Test-Path -LiteralPath $OutputFile -PathType Leaf) -or (Get-Item -LiteralPath $OutputFile).Length -eq 0) {
        throw "$Description produced no usable report (Trivy exited $trivyExit). That is an infrastructure failure, not a clean scan and not a finding — check the Trivy output above for a FATAL line, and raise -TrivyTimeout if it mentions a deadline."
    }

    if ($trivyExit -ne 0) {
        throw "$Description exited $trivyExit while still writing a report. Treat as a tool failure and investigate before trusting the report."
    }
}

$results = [System.Collections.Generic.List[object]]::new()

foreach ($target in $targets) {
    $archivePath = Join-Path $resolvedArchiveDirectory "$($target.Name).tar"
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    # ContainerRegistry defaults to ghcr.io in Directory.Build.props, so a PublishContainer invocation
    # without ContainerArchiveOutputPath pushes to a real registry instead of writing a file.
    & dotnet publish (Join-Path $repositoryRoot $target.Project) `
        --configuration $Configuration `
        -t:PublishContainer `
        -p:ContainerRepository="concertable/$($target.Repository)" `
        -p:ContainerImageTag="verification-$($target.Tag)" `
        -p:ContainerArchiveOutputPath=$archivePath `
        -p:MinVerVersionOverride=$BuildVersion
    if ($LASTEXITCODE -ne 0) {
        throw "Payment image publish failed for '$($target.Name)' with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf) -or (Get-Item -LiteralPath $archivePath).Length -eq 0) {
        throw "Payment image publish produced no archive for '$($target.Name)'."
    }

    $entry = [ordered]@{
        name = $target.Name
        repository = "concertable/$($target.Repository)"
        tag = "verification-$($target.Tag)"
        archive = [System.IO.Path]::GetFileName($archivePath)
        revision = $target.Tag
        latest = $target.LatestTag
        version = $BuildVersion
        scanned = $false
    }

    if (-not $SkipScan) {
        $reportName = "$($target.Name).trivy.json"
        $secretReportName = "$($target.Name).secrets.json"
        $sbomName = "$($target.Name).cdx.json"

        $trivyBase = @(
            'run', '--rm',
            '--volume', "${resolvedArchiveDirectory}:/scan:ro",
            '--volume', "${resolvedEvidenceDirectory}:/evidence",
            '--volume', "${resolvedTrivyCacheDirectory}:/root/.cache/trivy",
            $TrivyImage,
            'image', '--input', "/scan/$($entry.archive)",
            '--timeout', $TrivyTimeout,
            '--no-progress')

        Invoke-TrivyScan -Description "Vulnerability scan of '$($target.Name)'" `
            -OutputFile (Join-Path $resolvedEvidenceDirectory $reportName) `
            -Arguments ($trivyBase + @(
                '--scanners', 'vuln',
                '--severity', ($Severity -join ','),
                '--format', 'json',
                '--output', "/evidence/$reportName"))

        $blocking = @(Get-BlockingVulnerabilities -ReportPath (Join-Path $resolvedEvidenceDirectory $reportName))
        if ($blocking.Count -gt 0) {
            $blocking | Format-Table -AutoSize | Out-String | Write-Host
            throw "Payment image '$($target.Name)' carries $($blocking.Count) blocking vulnerability finding(s) at $($Severity -join ',')."
        }

        Invoke-TrivyScan -Description "Secret scan of '$($target.Name)'" `
            -OutputFile (Join-Path $resolvedEvidenceDirectory $secretReportName) `
            -Arguments ($trivyBase + @(
                '--scanners', 'secret',
                '--format', 'json',
                '--output', "/evidence/$secretReportName"))

        $secrets = @(Get-BlockingSecrets -ReportPath (Join-Path $resolvedEvidenceDirectory $secretReportName))
        if ($secrets.Count -gt 0) {
            $secrets | Format-Table -AutoSize | Out-String | Write-Host
            throw "Payment image '$($target.Name)' carries $($secrets.Count) secret finding(s)."
        }

        Invoke-TrivyScan -Description "SBOM generation for '$($target.Name)'" `
            -OutputFile (Join-Path $resolvedEvidenceDirectory $sbomName) `
            -Arguments ($trivyBase + @(
                '--format', 'cyclonedx',
                '--output', "/evidence/$sbomName"))

        $sbom = Get-Content -Raw -LiteralPath (Join-Path $resolvedEvidenceDirectory $sbomName) | ConvertFrom-Json
        $bomFormat = Get-MemberValue -Source $sbom -Name 'bomFormat'
        $components = @(Get-MemberValue -Source $sbom -Name 'components')
        if ($bomFormat -ne 'CycloneDX' -or $components.Count -eq 0) {
            throw "SBOM validation failed for '$($target.Name)': format '$bomFormat', $($components.Count) components."
        }

        $entry.scanned = $true
        $entry.report = $reportName
        $entry.sbom = $sbomName
        $entry.sbomComponents = $components.Count
    }

    $results.Add([pscustomobject] $entry)
}

$manifest = [ordered]@{
    revision = $revision
    version = $BuildVersion
    configuration = $Configuration
    severity = $Severity
    trivyImage = $TrivyImage
    images = $results
}

[System.IO.File]::WriteAllText(
    $resolvedResultPath,
    ($manifest | ConvertTo-Json -Depth 8),
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Verified Payment images for revision ${revision}: $(($targets | ForEach-Object { $_.Name }) -join ', ')."
