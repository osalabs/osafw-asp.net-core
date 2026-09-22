<#
.SYNOPSIS
Exercises the real FPF CLI entrypoints against task-owned, offline Git fixtures.
.DESCRIPTION
Run with Windows PowerShell 5.1 and PowerShell 7. No production repository, database,
network source or user configuration is modified. Failed fixtures are retained under artifacts.
#>
[CmdletBinding()]
param([switch]$KeepArtifacts)
. (Join-Path $PSScriptRoot "Fpf.Common.ps1")
$testRoot = Join-Path ([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../.."))) ("artifacts/fpf-tests-" + [guid]::NewGuid().ToString("N"))
$shellPath = (Get-Process -Id $PID).Path
$passed = 0; $succeeded = $false

function Assert-FpfTest {
    param([bool]$Condition, [string]$Name)
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:passed++
    Write-Output "PASS: $Name"
}
function Write-FpfFixture {
    param([string]$Path, [string]$Text)
    [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Path))
    [System.IO.File]::WriteAllText($Path, ($Text -replace "\r?\n", "`r`n"), $script:FpfUtf8)
}
function Invoke-FpfFixtureGit {
    param([string]$Root, [string[]]$Arguments)
    return (Invoke-FpfGit -Arguments (@("-C", $Root) + $Arguments)).Output.Trim()
}
function Start-FpfTestTool {
    param([string]$Name, [string[]]$Arguments)
    $info = [System.Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $script:shellPath
    $toolPath = if ([IO.Path]::IsPathRooted($Name)) { $Name } else { Join-Path $PSScriptRoot $Name }
    $argv = @("-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", $toolPath) + $Arguments
    $info.Arguments = ($argv | ForEach-Object { ConvertTo-FpfArgument $_ }) -join " "
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = $script:FpfUtf8
    $process = [System.Diagnostics.Process]::new(); $process.StartInfo = $info
    [void]$process.Start()
    return [pscustomobject]@{ Process = $process; Output = $process.StandardOutput.ReadToEndAsync(); Error = $process.StandardError.ReadToEndAsync() }
}
function Complete-FpfTestTool {
    param($Run, [int]$ExpectedExit = 0)
    try {
        if (-not $Run.Process.WaitForExit(90000)) { $Run.Process.Kill(); throw "Fixture helper timed out." }
        $output = $Run.Output.GetAwaiter().GetResult()
        $errorOutput = $Run.Error.GetAwaiter().GetResult()
        if ($Run.Process.ExitCode -ne $ExpectedExit) { throw "Unexpected helper exit $($Run.Process.ExitCode): $output $errorOutput" }
        $script:lastFpfOutput = $output
        return ($output | ConvertFrom-Json)
    }
    finally { $Run.Process.Dispose() }
}
function Invoke-FpfTestTool {
    param([string]$Name, [string[]]$Arguments, [int]$ExpectedExit = 0)
    return Complete-FpfTestTool (Start-FpfTestTool $Name $Arguments) $ExpectedExit
}
function New-FpfFixtureApp {
    param([string]$Root)
    [void][System.IO.Directory]::CreateDirectory($Root)
    [void](Invoke-FpfFixtureGit $Root @("init", "-b", "main", "--template="))
    [void](Invoke-FpfFixtureGit $Root @("config", "user.name", "FPF Fixture"))
    [void](Invoke-FpfFixtureGit $Root @("config", "user.email", "fixture@example.invalid"))
    Write-FpfFixture (Join-Path $Root ".gitignore") "/.codex-local/`n"
    Write-FpfFixture (Join-Path $Root "AGENTS.md") "# Application-owned rules`nPreserve this application's business behavior.`n"
    Write-FpfFixture (Join-Path $Root "docs/agents/fpf-app.md") "# Local profile`nApplication-specific terminology stays local.`n"
    [void](Invoke-FpfFixtureGit $Root @("add", "."))
    [void](Invoke-FpfFixtureGit $Root @("commit", "-m", "Create isolated fixture"))
}

try {
    [void][System.IO.Directory]::CreateDirectory($testRoot)
    $upstream = Join-Path $testRoot "upstream source"
    New-FpfFixtureApp $upstream
    foreach ($path in $script:FpfRequired) {
        $text = "# $path`n`n## X.1 - Fixture method`n### X.1:1 - Problem frame`nFixture condition.`n### X.1:4 - Solution`nOriginal fixture result.`n### X.1:End`n"
        if ($path -eq "LICENSE") { $text = "Fixture license text; synthetic test data.`n" }
        Write-FpfFixture (Join-Path $upstream $path) $text
    }
    $core = "# First Principles Framework (FPF) - Core Conceptual Specification`n# Table of Contents`n"
    $core += ('```text' + "`n" + '## A.999 - Fenced example, not a pattern' + "`n" + '```' + "`n")
    $core += "## A.1 - Actual pattern`n### A.1:1 - Problem frame`nA bounded fixture question.`n"
    $core += "### A.1:4 - Solution`n" + ("x" * 257) + [char]0x03a9 + [char]::ConvertFromUtf32(0x1f600) + "`nKeep the next line.`n### A.1:End`n"
    $core += "## A.2 - Following pattern`nDo not include this in A.1.`n### A.2:End`n"
    Write-FpfFixture (Join-Path $upstream "FPF-Spec.md") $core
    Write-FpfFixture (Join-Path $upstream "Narrativization-and-Narrative-Studies-Principles-Framework.md") "# Independent publication`n## NSTD.1 - Explain`nOriginal independent result.`n"
    [void](Invoke-FpfFixtureGit $upstream @("add", "."))
    [void](Invoke-FpfFixtureGit $upstream @("commit", "-m", "Publish synthetic corpus"))
    $revision1 = Invoke-FpfFixtureGit $upstream @("rev-parse", "HEAD")
    $distributedApp = Join-Path $testRoot "distributed customized app"
    New-FpfFixtureApp $distributedApp
    $distributedRules = [IO.File]::ReadAllText((Join-Path $distributedApp "AGENTS.md"))
    $distributedProfile = [IO.File]::ReadAllText((Join-Path $distributedApp "docs/agents/fpf-app.md"))
    Write-FpfFixture (Join-Path $distributedApp ".gitignore") "/application-private/`n"
    # Apply only the new pack routes/files; application-owned content is a merge input.
    $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../.."))
    $pack = Get-Content -LiteralPath (Join-Path $repoRoot "docs/agents/instruction-pack.json") -Raw | ConvertFrom-Json
    $copiedFiles = @($pack.files | Where-Object { $_.path -match '^docs/agents/(fpf(-profile)?\.md|tools/(Fpf\.Common|Sync-Fpf|Read-Fpf|Test-Fpf)\.ps1)$' })
    foreach ($entry in $copiedFiles) {
        $target = Join-Path $distributedApp $entry.path
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        [IO.File]::Copy((Join-Path $repoRoot $entry.path), $target)
    }
    Write-FpfFixture (Join-Path $distributedApp "AGENTS.md") ($distributedRules + "Optional FPF reference: docs/agents/fpf.md`n")
    Write-FpfFixture (Join-Path $distributedApp ".gitignore") "/application-private/`n/.codex-local/`n"
    $distributed = Invoke-FpfTestTool (Join-Path $distributedApp "docs/agents/tools/Sync-Fpf.ps1") @("-SourcePath", $upstream)
    $distributedRead = Invoke-FpfTestTool (Join-Path $distributedApp "docs/agents/tools/Read-Fpf.ps1") @("-Revision", $revision1, "-PatternId", "A.1")
    Assert-FpfTest ($copiedFiles.Count -eq 6 -and $distributed.CandidateRevision -eq $revision1 -and $distributedRead.Content -like "*Actual pattern*") "distributed helpers install and read from their own copied-application location"
    Assert-FpfTest ([IO.File]::ReadAllText((Join-Path $distributedApp "AGENTS.md")).StartsWith($distributedRules) -and [IO.File]::ReadAllText((Join-Path $distributedApp "docs/agents/fpf-app.md")) -ceq $distributedProfile -and [IO.File]::ReadAllText((Join-Path $distributedApp ".gitignore")).Contains("/application-private/")) "targeted instruction-pack additions preserve custom rules, domain profile and unrelated ignore rules"
    $distributedStatus = Invoke-FpfFixtureGit $distributedApp @("status", "--short", "--untracked-files=all")
    Assert-FpfTest ($distributedStatus -notmatch '\.codex-local') "a copied application cannot accidentally include the ignored source corpus in Git status"
    $app = Join-Path $testRoot "copied application"
    New-FpfFixtureApp $app
    $baseArgs = @("-RepositoryRoot", $app)
    $fresh = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Action", "Status"))
    $offline = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Offline"))
    Assert-FpfTest ($fresh.Status -eq "unavailable" -and $offline.Status -eq "unavailable" -and -not (Test-Path (Join-Path $app ".codex-local"))) "fresh Status and Offline do not create cache state"

    $firstRun = Start-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-SourcePath", $upstream))
    $secondRun = Start-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-SourcePath", $upstream))
    $first = Complete-FpfTestTool $firstRun
    $second = Complete-FpfTestTool $secondRun
    $checks = @($first.CheckResult, $second.CheckResult)
    Assert-FpfTest (($checks -contains "checked") -and ($checks -contains "throttled")) "concurrent installation serializes into one check"
    Assert-FpfTest ($first.CandidateRevision -eq $revision1 -and $null -eq $first.AcceptedRevision -and $first.ReviewRequired) "fresh valid corpus remains a review candidate"
    $readArgs = $baseArgs + @("-Revision", $revision1)
    $catalog = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-Action", "List"))
    Assert-FpfTest (@($catalog.Items | Where-Object { $_.Path -like "Narrativization*" }).Count -eq 1) "independent publications accompany both suites and Core"
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-PatternId", "A.999")) 1
    Assert-FpfTest ($bad.Notice -like "*matched 0*") "fenced headings cannot impersonate patterns"
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-PatternId", "X.1")) 1
    Assert-FpfTest ($bad.Notice -like "*matched*headings*") "duplicate pattern IDs require disambiguation"
    $domain = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-PatternId", "X.1", "-Path", "Engineering DPF Suite/SYSTEMS-ENGINEERING-PRINCIPLES-FRAMEWORK.md"))
    Assert-FpfTest ($domain.Content -like "*Original fixture result*") "a publication path resolves an ambiguous domain pattern"
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-Path", "../AGENTS.md")) 1
    Assert-FpfTest ($bad.Status -eq "error") "read paths cannot escape the publication catalog"
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-Path", "FPF-Spec.md")) 1
    Assert-FpfTest ($bad.Notice -like "*explicit StartLine*") "Core file reads require an explicit range or selector"

    $whole = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-PatternId", "A.1"))
    $page = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-PatternId", "A.1", "-MaxChars", "128", "-MaxLines", "2"))
    $joined = $page.Content; $pages = 1
    while ($page.Truncated) {
        Assert-FpfTest ($page.Content.Length -le 128 -and $pages -lt 20) "source page respects its character budget and advances"
        $page = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-PatternId", "A.1", "-MaxChars", "128", "-MaxLines", "2", "-StartLine", [string]$page.NextLine, "-StartColumn", [string]$page.NextColumn))
        $joined += $page.Content; $pages++
    }
    Assert-FpfTest ($joined -ceq $whole.Content -and $joined -notlike "*Following pattern*" -and $joined.Contains([char]0x03a9)) "pagination preserves Unicode, line breaks and pattern boundaries"
    $search1 = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-Action", "Search", "-Query", "fixture", "-MaxLines", "2"))
    $search2 = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-Action", "Search", "-Query", "fixture", "-MaxLines", "2", "-Offset", [string]$search1.NextOffset))
    Assert-FpfTest ($search1.Items.Count -eq 2 -and $search2.Items.Count -eq 2 -and $search2.NextOffset -gt $search1.NextOffset) "literal search uses bounded continuation"

    $bad = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Action", "Accept", "-Revision", $revision1)) 1
    Assert-FpfTest ($bad.Notice -like "*ReviewNote*") "adoption requires a recorded review"
    $accepted1 = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Action", "Accept", "-Revision", $revision1, "-ReviewNote", "Reviewed synthetic corpus conditions."))
    Assert-FpfTest ($accepted1.AcceptedRevision -eq $revision1 -and -not $accepted1.ReviewRequired) "reviewed candidate is adopted only for this checkout"
    Assert-FpfTest ([DateTimeOffset]$accepted1.LastAttemptUtc -eq [DateTimeOffset]$first.LastAttemptUtc -and ($script:lastFpfOutput -match '"LastAttemptUtc":\s*"[^"]+(?:Z|\+00:00)"')) "UTC source timestamps preserve their instant across state reads and shell versions"
    $statusBefore = [System.IO.File]::ReadAllText((Join-Path $app ".codex-local/fpf/adoption.json"))
    $selected = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Action", "Select", "-Revision", $revision1))
    $null = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-PatternId", "A.1"))
    Assert-FpfTest ($selected.SelectedRevision -eq $revision1 -and $statusBefore -ceq [System.IO.File]::ReadAllText((Join-Path $app ".codex-local/fpf/adoption.json"))) "selection and reads do not rewrite adoption state"
    Write-FpfFixture (Join-Path $app "docs/agents/fpf-app.md") "# Revised application-specific interpretation`n"
    $policy = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Action", "Status"))
    Assert-FpfTest ($policy.PolicyChanged -and $policy.ReviewRequired -and $policy.AcceptedRevision -eq $revision1) "changed app profile requests review without discarding the accepted source"

    Write-FpfFixture (Join-Path $upstream "USING-FPF.md") "# Revised upstream usage`n## Updated entry`nReview this change before using it.`n"
    Write-FpfFixture (Join-Path $upstream "Engineering DPF Suite/SYSTEMS-ENGINEERING-PRINCIPLES-FRAMEWORK.md") "# Changed publication`n## SYSE.2 - New pattern`n### SYSE.2:4 - Solution`nChanged result.`n"
    [void](Invoke-FpfFixtureGit $upstream @("add", ".")); [void](Invoke-FpfFixtureGit $upstream @("commit", "-m", "Change source instructions and pattern identities"))
    $revision2 = Invoke-FpfFixtureGit $upstream @("rev-parse", "HEAD")
    $throttled = Invoke-FpfTestTool "Sync-Fpf.ps1" $baseArgs
    Assert-FpfTest ($throttled.CheckResult -eq "throttled" -and $throttled.CandidateRevision -eq $revision1) "daily throttle does not fetch on every task"
    $pending = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Force"))
    Assert-FpfTest ($pending.CandidateRevision -eq $revision2 -and $pending.AcceptedRevision -eq $revision1 -and $pending.ReviewRequired) "material refresh is deferred until reviewed"
    Assert-FpfTest (@($pending.Changes.Files | Where-Object { $_.Attention -eq "instructions-or-navigation" }).Count -gt 0 -and @($pending.Changes.Patterns | Where-Object { $_.Kind -eq "removed" }).Count -gt 0) "review report exposes instruction and pattern-structure changes"
    $old = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-Path", "Engineering DPF Suite/SYSTEMS-ENGINEERING-PRINCIPLES-FRAMEWORK.md", "-PatternId", "X.1"))
    Assert-FpfTest ($old.Content -like "*Original fixture result*") "an active task keeps its old revision through refresh"
    $bad = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Action", "Accept", "-Revision", $revision1, "-ReviewNote", "Stale review")) 1
    Assert-FpfTest ($bad.Status -eq "error") "a stale candidate approval cannot overwrite the newer candidate"
    $accepted2 = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Action", "Accept", "-Revision", $revision2, "-ReviewNote", "Reviewed changed usage and pattern routes."))
    Assert-FpfTest ($accepted2.AcceptedRevision -eq $revision2) "compatible update can be accepted after review"
    Write-FpfFixture (Join-Path $upstream "USING-FPF.md") ""
    [void](Invoke-FpfFixtureGit $upstream @("add", ".")); [void](Invoke-FpfFixtureGit $upstream @("commit", "-m", "Publish invalid fixture"))
    $invalid = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Force"))
    Assert-FpfTest ($invalid.CheckResult -eq "failed" -and $invalid.AcceptedRevision -eq $revision2 -and $invalid.RejectedRevision) "invalid candidate retains the last accepted snapshot"
    $sourceState = Join-Path $app ".codex-local/fpf/source.json"
    Write-FpfFixture ($sourceState + ".interrupted.tmp") "{incomplete"
    $status = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Action", "Status"))
    Assert-FpfTest ($status.AcceptedRevision -eq $revision2) "an interrupted temporary state write is not consumed"
    $movedSource = Join-Path $testRoot "unavailable source"
    Assert-FpfDirectory $upstream
    [System.IO.Directory]::Move($upstream, $movedSource)
    $failed = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Force"))
    $offline = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Offline"))
    Assert-FpfTest ($failed.CheckResult -eq "failed" -and $offline.AcceptedRevision -eq $revision2) "unavailable source and offline mode retain accepted data"

    $worktree = Join-Path $testRoot "linked worktree"
    [void](Invoke-FpfFixtureGit $app @("worktree", "add", "--detach", $worktree, "HEAD"))
    $worktreeStatus = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-Action", "Status")
    Assert-FpfTest ($worktreeStatus.CacheRoot -eq $status.CacheRoot -and $null -eq $worktreeStatus.AcceptedRevision -and $worktreeStatus.CandidateRevision -eq $revision2) "linked worktrees share source but not adoption"
    $worktreeAdopt = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-Action", "Accept", "-Revision", $revision2, "-ReviewNote", "Reviewed for independent worktree profile.")
    Assert-FpfTest ($worktreeAdopt.AcceptedRevision -eq $revision2) "worktree adoption is independent and needs no source fetch"
    $local = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-Action", "Status", "-LocalCache")
    Assert-FpfTest ($local.Status -eq "unavailable" -and $local.UnavailableAcceptedRevision -eq $revision2) "local fallback discloses unavailable accepted objects without losing the recorded revision"
    $stillShared = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-Action", "Status")
    Assert-FpfTest ($stillShared.AcceptedRevision -eq $revision2) "read-only fallback inspection preserves the established shared adoption"
    $sharedAdoptionPath = Join-Path $worktree ".codex-local/fpf/adoption.json"
    $sharedAdoptionBefore = [IO.File]::ReadAllText($sharedAdoptionPath)
    $failedAccept = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-Action", "Accept", "-LocalCache", "-Revision", $revision2, "-ReviewNote", "There is no candidate here.") 1
    $afterFailedAccept = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-Action", "Status")
    Assert-FpfTest ($failedAccept.Status -eq "error" -and $afterFailedAccept.AcceptedRevision -eq $revision2 -and [IO.File]::ReadAllText($sharedAdoptionPath) -ceq $sharedAdoptionBefore) "failed local acceptance preserves the prior shared adoption byte for byte"
    # Restore the task-owned source; its current head still has an invalid publication.
    [System.IO.Directory]::Move($movedSource, $upstream)
    $failedLocal = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-LocalCache", "-SourcePath", $upstream) 1
    $afterFailedLocal = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-Action", "Status")
    $afterFailedLocalRead = Invoke-FpfTestTool "Read-Fpf.ps1" @("-RepositoryRoot", $worktree, "-Revision", $revision2, "-PatternId", "SYSE.2")
    Assert-FpfTest ($failedLocal.CheckResult -eq "failed" -and $failedLocal.CacheTransitionPending -and $afterFailedLocal.AcceptedRevision -eq $revision2 -and $afterFailedLocalRead.Content -like "*Changed result*" -and [IO.File]::ReadAllText($sharedAdoptionPath) -ceq $sharedAdoptionBefore) "failed local installation retains the shared root and readable accepted source"
    [void](Invoke-FpfFixtureGit $upstream @("reset", "--hard", $revision1))
    $stagedLocal = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-LocalCache", "-Force")
    Assert-FpfTest ($stagedLocal.CandidateRevision -eq $revision1 -and $stagedLocal.CacheTransitionPending -and [IO.File]::ReadAllText($sharedAdoptionPath) -ceq $sharedAdoptionBefore) "a valid local candidate cannot strand an accepted revision absent from that cache"
    $staleLocal = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-Action", "Accept", "-LocalCache", "-Revision", $revision2, "-ReviewNote", "This is not the staged candidate.") 1
    Assert-FpfTest ($staleLocal.Status -eq "error" -and [IO.File]::ReadAllText($sharedAdoptionPath) -ceq $sharedAdoptionBefore) "stale local acceptance does not persist the staged cache root"
    $reviewedWorktree = Join-Path $testRoot "reviewed local transition"
    [void](Invoke-FpfFixtureGit $app @("worktree", "add", "--detach", $reviewedWorktree, "HEAD"))
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $reviewedWorktree, "-Action", "Accept", "-Revision", $revision2, "-ReviewNote", "Start with the shared accepted revision.")
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $reviewedWorktree, "-LocalCache", "-SourcePath", $upstream)
    $reviewedLocal = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $reviewedWorktree, "-Action", "Accept", "-LocalCache", "-Revision", $revision1, "-ReviewNote", "Reviewed this explicitly seeded local edition.")
    $reviewedStatus = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $reviewedWorktree, "-Action", "Status")
    Assert-FpfTest ($reviewedLocal.AcceptedRevision -eq $revision1 -and -not $reviewedLocal.CacheTransitionPending -and $null -eq $reviewedLocal.UnavailableAcceptedRevision -and $reviewedStatus.CacheRoot -eq $reviewedLocal.CacheRoot) "successful local acceptance atomically adopts the reviewed revision and staged cache root"
    [void](Invoke-FpfFixtureGit $upstream @("reset", "--hard", $revision2))
    $localInstall = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-LocalCache", "-Force", "-SourcePath", $upstream)
    Assert-FpfTest ($localInstall.CacheRoot -ne $status.CacheRoot -and $localInstall.AcceptedRevision -eq $revision2) "local fallback can recover the previously reviewed exact source from the explicit local seed"
    $remembered = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $worktree, "-Action", "Status")
    Assert-FpfTest ($remembered.CacheRoot -eq $localInstall.CacheRoot) "subsequent invocations remember the worktree-local fallback"
    $appRules = [IO.File]::ReadAllText((Join-Path $app "AGENTS.md"))
    $appProfile = [IO.File]::ReadAllText((Join-Path $app "docs/agents/fpf-app.md"))
    $ignored = Invoke-FpfFixtureGit $app @("check-ignore", ".codex-local/fpf/source.json")
    Assert-FpfTest ($ignored -and $appRules.Contains("Application-owned") -and $appProfile.Contains("Revised application-specific")) "cache remains ignored and app-owned rules survive installation and refresh"
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-Heading", "Missing exact heading")) 1
    Assert-FpfTest ($bad.Notice -like "*matched 0*") "missing headings fail without substitution"
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($baseArgs + @("-Revision", ("0" * 40), "-PatternId", "A.1")) 1
    Assert-FpfTest ($bad.Notice -like "*validated snapshot*") "unknown revisions cannot fall back to current source"
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($readArgs + @("-PatternId", "A.1", "-StartLine", "999999")) 1
    Assert-FpfTest ($bad.Notice -like "*outside*") "continuation cannot escape the selected pattern"
    $dailyState = Read-FpfJson $sourceState
    $dailyState.LastAttemptUtc = [DateTimeOffset]::UtcNow.AddHours(-25).ToString("o")
    Write-FpfJson $sourceState $dailyState
    $daily = Invoke-FpfTestTool "Sync-Fpf.ps1" $baseArgs
    Assert-FpfTest ($daily.CheckResult -eq "checked" -and $daily.LastSuccessUtc -ne $dailyState.LastSuccessUtc) "a relevant use after 24 hours refreshes without Force"
    [IO.File]::WriteAllBytes((Join-Path $upstream "USING-FPF.md"), [byte[]]@(0xc3, 0x28))
    [void](Invoke-FpfFixtureGit $upstream @("add", ".")); [void](Invoke-FpfFixtureGit $upstream @("commit", "-m", "Invalid source encoding"))
    $invalidUtf8 = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Force"))
    Assert-FpfTest ($invalidUtf8.CheckResult -eq "failed" -and $invalidUtf8.AcceptedRevision -eq $revision2) "invalid UTF-8 cannot replace accepted source"
    $adoptionBefore = [IO.File]::ReadAllText((Join-Path $app ".codex-local/fpf/adoption.json"))
    $bareGit = Join-Path $app ".codex-local/fpf/corpus.git"
    $sourceBefore = Read-FpfJson $sourceState
    [void](Invoke-FpfGit -GitDirectory $bareGit -Arguments @("config", "--local", "remote.origin.url", "https://example.invalid/not-fpf.git"))
    $bad = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Action", "Status")) 1
    Assert-FpfTest ($bad.Notice -like "*identity*" -and $adoptionBefore -ceq [IO.File]::ReadAllText((Join-Path $app ".codex-local/fpf/adoption.json"))) "repository identity mismatch cannot silently consume or rewrite adoption"
    [void](Invoke-FpfGit -GitDirectory $bareGit -Arguments @("config", "--local", "remote.origin.url", $sourceBefore.Source))
    $unavailableApp = Join-Path $testRoot "unavailable first use"
    New-FpfFixtureApp $unavailableApp
    $emptySource = Join-Path $testRoot "empty source"
    [void][IO.Directory]::CreateDirectory($emptySource)
    $missingFirst = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $unavailableApp, "-SourcePath", $emptySource) 1
    $missingRetry = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $unavailableApp)
    Assert-FpfTest ($missingFirst.CheckResult -eq "failed" -and $missingRetry.CheckResult -eq "throttled" -and $null -eq $missingRetry.AcceptedRevision) "unavailable first-use sources are disclosed and failures are throttled"
    # Exercise source serialization from distinct checkout locks.
    $primaryRun = Start-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Force"))
    $otherWorktree = Join-Path $testRoot "second linked worktree"
    [void](Invoke-FpfFixtureGit $app @("worktree", "add", "--detach", $otherWorktree, "HEAD"))
    $worktreeRun = Start-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $otherWorktree)
    $primaryResult = Complete-FpfTestTool $primaryRun
    $worktreeResult = Complete-FpfTestTool $worktreeRun
    Assert-FpfTest ($primaryResult.AcceptedRevision -eq $revision2 -and $worktreeResult.CandidateRevision -eq $revision2 -and $null -eq $worktreeResult.AcceptedRevision) "concurrent worktrees preserve independent adoption through a failed source update"
    $succeeded = $true
}
finally {
    if ($succeeded -and -not $KeepArtifacts) {
        $allowed = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../../artifacts")).TrimEnd([char[]]"\/") + [System.IO.Path]::DirectorySeparatorChar
        $resolved = [System.IO.Path]::GetFullPath($testRoot)
        if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw "Refusing cleanup outside the test artifacts directory." }
        Assert-FpfDirectory $resolved
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    else { Write-Output "Fixture evidence retained: $testRoot" }
}
Write-Output "Passed $passed FPF assertions on PowerShell $($PSVersionTable.PSVersion)."
