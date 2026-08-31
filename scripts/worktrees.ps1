[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Position = 0)]
    [ValidateSet('audit', 'close', 'retire')]
    [string] $Command = 'audit',
    [string] $Worktree,
    [int] $PullRequest,
    [switch] $PlanManaged,
    [string] $ExpectedHead,
    [string] $EvidenceCommit,
    [string] $Reason,
    [switch] $NoFetch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptCheckout = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$persistentBranches = @('Chore/TechDebt')

function Run {
    param([string] $Program, [string[]] $Arguments, [switch] $AllowFailure)
    $oldPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        # No ".exe": Windows resolves it through PATHEXT, and hard-coding it makes every call fail on Linux.
        $executable = (Get-Command $Program -CommandType Application -ErrorAction Stop).Source
        $output = @(& $executable @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldPreference
    }
    $text = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "$Program $($Arguments -join ' ') failed ($exitCode): $text"
    }
    [pscustomobject]@{ ExitCode = $exitCode; Text = $text.Trim() }
}

$commonDirectory = (Run git @(
    '-C', $scriptCheckout, 'rev-parse', '--path-format=absolute', '--git-common-dir'
)).Text
$repositoryRoot = [IO.Path]::GetDirectoryName($commonDirectory)

function Git {
    param([string[]] $Arguments, [switch] $AllowFailure)
    Run git (@('-C', $repositoryRoot) + $Arguments) -AllowFailure:$AllowFailure
}

function Gh {
    param([string[]] $Arguments)
    Run gh $Arguments
}

function Canonical {
    param([string] $Path)
    [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path).Path).TrimEnd('\', '/')
}

function SamePath {
    param([string] $Left, [string] $Right)
    [string]::Equals(
        ([IO.Path]::GetFullPath($Left).TrimEnd('\', '/')),
        ([IO.Path]::GetFullPath($Right).TrimEnd('\', '/')),
        [StringComparison]::OrdinalIgnoreCase)
}

function DefaultBranch {
    $result = Git @('symbolic-ref', '--quiet', '--short', 'refs/remotes/origin/HEAD') -AllowFailure
    if ($result.ExitCode -eq 0 -and $result.Text.StartsWith('origin/')) {
        return $result.Text.Substring(7)
    }
    'main'
}

function MainCheckout {
    $commonDirectory = (Git @('rev-parse', '--path-format=absolute', '--git-common-dir')).Text
    [IO.Path]::GetDirectoryName($commonDirectory)
}

function Fetch {
    Git @('fetch', 'origin', '--prune') | Out-Null
}

function Inventory {
    $items = @()
    $item = $null
    foreach ($line in ((Git @('worktree', 'list', '--porcelain')).Text -split "\r?\n")) {
        if ($line.StartsWith('worktree ')) {
            if ($null -ne $item) { $items += [pscustomobject]$item }
            $item = [ordered]@{
                Path = $line.Substring(9)
                Head = ''
                Branch = ''
                Detached = $false
                Prunable = $false
            }
        }
        elseif ($null -ne $item -and $line.StartsWith('HEAD ')) { $item.Head = $line.Substring(5) }
        elseif ($null -ne $item -and $line.StartsWith('branch refs/heads/')) { $item.Branch = $line.Substring(18) }
        elseif ($null -ne $item -and $line -eq 'detached') { $item.Detached = $true }
        elseif ($null -ne $item -and $line.StartsWith('prunable')) { $item.Prunable = $true }
        elseif ($null -ne $item -and [string]::IsNullOrWhiteSpace($line)) {
            $items += [pscustomobject]$item
            $item = $null
        }
    }
    if ($null -ne $item) { $items += [pscustomobject]$item }
    @($items)
}

function PullRequests {
    $json = (Gh @(
        'pr', 'list', '--state', 'all', '--limit', '1000',
        '--json', 'number,state,headRefName,headRefOid,url'
    )).Text
    if (-not $json) { return @() }
    @($json | ConvertFrom-Json)
}

function IsAncestor {
    param([string] $Ancestor, [string] $Descendant)
    (Git @('merge-base', '--is-ancestor', $Ancestor, $Descendant) -AllowFailure).ExitCode -eq 0
}

function Dirty {
    param([string] $Path)
    $result = Run git @('-C', $Path, 'status', '--porcelain', '--untracked-files=all') -AllowFailure
    if ($result.ExitCode -ne 0) { return 'UNREADABLE' }
    $result.Text
}

function CaseCollisions {
    $local = (Git @('for-each-ref', '--format=%(refname:short)', 'refs/heads')).Text -split "\r?\n"
    $remote = (Git @('for-each-ref', '--format=%(refname:short)', 'refs/remotes/origin')).Text -split "\r?\n" |
        Where-Object { $_.StartsWith('origin/') -and $_ -ne 'origin/HEAD' } |
        ForEach-Object { $_.Substring(7) }
    $result = @{}
    foreach ($group in @($local + $remote | Where-Object { $_ } | Sort-Object -Unique |
        Group-Object { $_.ToLowerInvariant() } |
        Where-Object { @($_.Group | Sort-Object -Unique).Count -gt 1 })) {
        $names = @($group.Group | Sort-Object -Unique)
        foreach ($name in $names) { $result[$name.ToLowerInvariant()] = $names -join ', ' }
    }
    $result
}

function IsPersistent {
    param([string] $Branch)
    foreach ($name in $persistentBranches) {
        if ([string]::Equals($Branch, $name, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    $false
}

function Orphans {
    param([object[]] $Worktrees)
    $registered = @{}
    foreach ($item in $Worktrees) {
        if (Test-Path -LiteralPath $item.Path) { $registered[(Canonical $item.Path).ToLowerInvariant()] = $true }
    }
    $mainCheckout = MainCheckout
    $roots = @((Join-Path $mainCheckout '.worktrees'), "$mainCheckout.worktrees") |
        Where-Object { Test-Path -LiteralPath $_ }
    $result = @()
    foreach ($root in $roots) {
        $firstLevel = @(Get-ChildItem -LiteralPath $root -Directory -Force -ErrorAction SilentlyContinue)
        $candidates = @($firstLevel)
        foreach ($directory in $firstLevel) {
            $candidates += @(Get-ChildItem -LiteralPath $directory.FullName -Directory -Force -ErrorAction SilentlyContinue)
        }
        foreach ($directory in $candidates) {
            $marker = (Test-Path -LiteralPath (Join-Path $directory.FullName '.git')) -or
                ((Test-Path -LiteralPath (Join-Path $directory.FullName 'api')) -and
                 (Test-Path -LiteralPath (Join-Path $directory.FullName 'app')) -and
                 ((Test-Path -LiteralPath (Join-Path $directory.FullName 'AGENTS.md')) -or
                  (Test-Path -LiteralPath (Join-Path $directory.FullName 'README.md')) -or
                  (Test-Path -LiteralPath (Join-Path $directory.FullName 'ARCHITECTURE.md'))))
            if ($marker) {
                $path = Canonical $directory.FullName
                if (-not $registered.ContainsKey($path.ToLowerInvariant())) { $result += $path }
            }
        }
    }
    @($result | Sort-Object -Unique)
}

function AssertTarget {
    param([string] $Path)
    if (-not $Path) { throw '-Worktree is required.' }
    $target = Canonical $Path
    $matches = @((Inventory) | Where-Object { SamePath $_.Path $target })
    if ($matches.Count -ne 1) { throw "Worktree is not registered exactly once: $target" }
    $item = $matches[0]
    if (SamePath $target (MainCheckout)) { throw 'The main checkout can never be removed.' }
    if ($item.Detached -or -not $item.Branch) { throw "Detached worktree requires manual review: $target" }
    if ($item.Prunable) { throw "Worktree is already prunable: $target" }
    if (IsPersistent $item.Branch) { throw "Persistent worktree cannot be removed: $($item.Branch)" }
    $current = Canonical (Get-Location).Path
    if ([string]::Equals($current, $target, [StringComparison]::OrdinalIgnoreCase) -or
        $current.StartsWith($target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Run this command from a different checkout.'
    }
    $dirty = Dirty $target
    if ($dirty) { throw ("Worktree is not clean: $target" + [Environment]::NewLine + $dirty) }
    $collisions = CaseCollisions
    $key = $item.Branch.ToLowerInvariant()
    if ($collisions.ContainsKey($key)) { throw "Case-colliding refs: $($collisions[$key])" }
    $item
}

function AssertLedger {
    param([object] $Item)
    $plans = Join-Path (Canonical $Item.Path) 'plans'
    if (-not (Test-Path -LiteralPath $plans)) {
        throw "Plan-managed close requires the owning worktree's plans directory: $plans"
    }
    $branchPattern = '(?m)^- Branch:\s*`?' + [regex]::Escape($Item.Branch) + '`?\s*$'
    $worktreePattern = '(?m)^- Worktree:\s*`?' + [regex]::Escape((Canonical $Item.Path)) + '`?\s*$'
    $matches = @(Get-ChildItem -LiteralPath $plans -Recurse -File -Filter '*_PROGRESS.md' |
        Where-Object {
            $text = [IO.File]::ReadAllText($_.FullName)
            $text -match $branchPattern -and $text -match $worktreePattern
        })
    if ($matches.Count -ne 1) {
        throw "Plan-managed close requires exactly one owning ledger for $($Item.Branch) at $($Item.Path); found $($matches.Count)."
    }
}

function RemoveTarget {
    param([object] $Item, [string] $Evidence)
    $target = Canonical $Item.Path
    $branch = $Item.Branch
    if (-not $PSCmdlet.ShouldProcess($target, "Remove $branch using $Evidence")) {
        Write-Host "VALIDATED: $branch at $($Item.Head)"
        return
    }
    $codex = Join-Path $target '.Codex'
    if (Test-Path -LiteralPath $codex) {
        $entry = Get-Item -LiteralPath $codex -Force
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            Remove-Item -LiteralPath $codex -Force
        }
        else {
            $pending = New-Object 'System.Collections.Generic.Queue[string]'
            $pending.Enqueue($codex)
            while ($pending.Count -gt 0) {
                $directory = $pending.Dequeue()
                foreach ($child in @(Get-ChildItem -LiteralPath $directory -Force)) {
                    if (($child.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                        Remove-Item -LiteralPath $child.FullName -Force
                    }
                    elseif ($child.PSIsContainer) {
                        $pending.Enqueue($child.FullName)
                    }
                }
            }
        }
    }
    $removed = Git @('worktree', 'remove', '--force', $target) -AllowFailure
    if ($removed.ExitCode -ne 0) {
        if (@((Inventory) | Where-Object { SamePath $_.Path $target }).Count -gt 0) {
            throw "git worktree remove failed: $($removed.Text)"
        }
    }
    if (Test-Path -LiteralPath $target) {
        # The \\?\ long-path prefix is Windows-only; on Linux it would be read as part of the filename.
        $extended =
            if ([IO.Path]::DirectorySeparatorChar -ne '\') { $target }
            elseif ($target.StartsWith('\\?\')) { $target }
            else { "\\?\$target" }
        Remove-Item -LiteralPath $extended -Recurse -Force
    }
    if ((Git @('show-ref', '--verify', '--quiet', "refs/heads/$branch") -AllowFailure).ExitCode -eq 0) {
        Git @('branch', '-D', '--', $branch) | Out-Null
    }
    if ((Git @('ls-remote', '--exit-code', '--heads', 'origin', "refs/heads/$branch") -AllowFailure).ExitCode -eq 0) {
        Git @('push', 'origin', '--delete', $branch) | Out-Null
    }
    if (Test-Path -LiteralPath $target) { throw "Folder remains: $target" }
    if (@((Inventory) | Where-Object { SamePath $_.Path $target }).Count -gt 0) { throw "Registration remains: $target" }
    if ((Git @('show-ref', '--verify', '--quiet', "refs/heads/$branch") -AllowFailure).ExitCode -eq 0) {
        throw "Local branch remains: $branch"
    }
    if ((Git @('ls-remote', '--exit-code', '--heads', 'origin', "refs/heads/$branch") -AllowFailure).ExitCode -eq 0) {
        throw "Remote branch remains: $branch"
    }
    Write-Host "REMOVED: $branch"
}

function Audit {
    if (-not $NoFetch) { Fetch }
    $defaultRef = "origin/$(DefaultBranch)"
    $worktrees = Inventory
    $prs = PullRequests
    $collisions = CaseCollisions
    $rows = @()
    foreach ($item in $worktrees) {
        $state = ''
        $detail = ''
        $dirty = if (Test-Path -LiteralPath $item.Path) { Dirty $item.Path } else { 'UNREADABLE' }
        if (SamePath $item.Path (MainCheckout)) { $state = 'MAIN' }
        elseif (IsPersistent $item.Branch) { $state = 'PERSISTENT' }
        elseif ($item.Prunable) { $state = 'PRUNABLE' }
        elseif ($item.Detached -or -not $item.Branch) { $state = 'DETACHED_UNSAFE' }
        elseif ($dirty) { $state = 'DIRTY' }
        elseif ($collisions.ContainsKey($item.Branch.ToLowerInvariant())) {
            $state = 'CASE_COLLISION'
            $detail = $collisions[$item.Branch.ToLowerInvariant()]
        }
        else {
            $branchPrs = @($prs | Where-Object {
                [string]::Equals($_.headRefName, $item.Branch, [StringComparison]::Ordinal)
            } | Sort-Object number -Descending)
            if ($branchPrs.Count -eq 0) {
                if (IsAncestor $item.Head $defaultRef) { $state = 'FULLY_MERGED' }
                else {
                    $cherry = Git @('cherry', $defaultRef, $item.Branch) -AllowFailure
                    $unique = @($cherry.Text -split "\r?\n" | Where-Object { $_.StartsWith('+') }).Count -gt 0
                    $state = if ($cherry.ExitCode -eq 0 -and -not $unique) { 'SQUASH_GHOST' } else { 'UNMERGED_NO_PR' }
                }
            }
            else {
                $pr = $branchPrs[0]
                $detail = "#$($pr.number)"
                if (-not [string]::Equals($item.Head, $pr.headRefOid, [StringComparison]::OrdinalIgnoreCase)) {
                    $state = "$($pr.state)_PR_DIFFERENT_HEAD"
                }
                elseif ($pr.state -eq 'MERGED' -and (IsAncestor $item.Head $defaultRef)) { $state = 'MERGED' }
                elseif ($pr.state -eq 'MERGED') { $state = 'MERGED_NOT_IN_MAIN' }
                elseif ($pr.state -eq 'OPEN') { $state = 'OPEN_PR' }
                else { $state = 'CLOSED_UNMERGED' }
            }
        }
        $rows += [pscustomobject]@{
            State = $state
            Branch = if ($item.Branch) { $item.Branch } else { '(detached)' }
            PR = $detail
            Path = $item.Path
        }
    }
    $rows | Sort-Object State, Branch | Format-Table -AutoSize
    $orphanFolders = @(Orphans $worktrees)
    if ($orphanFolders.Count -gt 0) {
        Write-Host ''
        Write-Host 'ORPHAN FOLDERS (report only):'
        $orphanFolders | ForEach-Object { Write-Host "  $_" }
    }
    Write-Host ''
    Write-Host 'Audit never deletes. Use close for an exact merged PR or retire for an explicitly superseded no-PR branch.'
}

function Close {
    if ($PullRequest -le 0) { throw '-PullRequest is required for close.' }
    Fetch
    $item = AssertTarget $Worktree
    $defaultRef = "origin/$(DefaultBranch)"
    $pr = (Gh @(
        'pr', 'view', $PullRequest.ToString(),
        '--json', 'number,state,headRefName,headRefOid,url'
    )).Text | ConvertFrom-Json
    if (-not [string]::Equals($pr.headRefName, $item.Branch, [StringComparison]::Ordinal)) {
        throw "PR #$PullRequest belongs to $($pr.headRefName), not $($item.Branch)."
    }
    if ($pr.state -ne 'MERGED') { throw "PR #$PullRequest is $($pr.state), not MERGED." }
    if (-not [string]::Equals($item.Head, $pr.headRefOid, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Worktree HEAD differs from PR head: $($item.Head) vs $($pr.headRefOid)."
    }
    if (-not (IsAncestor $item.Head $defaultRef)) { throw "PR head is not contained by $defaultRef." }
    if ($PlanManaged) { AssertLedger $item }
    RemoveTarget $item "merged PR #$PullRequest"
}

function Retire {
    if (-not $ExpectedHead -or -not $EvidenceCommit -or -not $Reason) {
        throw 'retire requires -ExpectedHead, -EvidenceCommit, and -Reason.'
    }
    Fetch
    $item = AssertTarget $Worktree
    $expected = (Git @('rev-parse', "$ExpectedHead^{commit}")).Text
    if (-not [string]::Equals($item.Head, $expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Worktree HEAD differs from expected head: $($item.Head) vs $expected."
    }
    $evidence = (Git @('rev-parse', "$EvidenceCommit^{commit}")).Text
    $defaultRef = "origin/$(DefaultBranch)"
    if (-not (IsAncestor $evidence $defaultRef)) { throw "Evidence is not durable on $defaultRef." }
    $open = (Gh @('pr', 'list', '--state', 'open', '--head', $item.Branch, '--json', 'number,url')).Text |
        ConvertFrom-Json
    if (@($open).Count -gt 0) { throw "Branch has an open PR: $($item.Branch)" }
    RemoveTarget $item "retirement $evidence ($Reason)"
}

switch ($Command) {
    'audit' { Audit }
    'close' { Close }
    'retire' { Retire }
}
