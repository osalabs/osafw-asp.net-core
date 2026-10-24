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
    param([string]$Name, [string[]]$Arguments, [switch]$WithoutGit)
    $info = [System.Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $script:shellPath
    $toolPath = if ([IO.Path]::IsPathRooted($Name)) { $Name } else { Join-Path $PSScriptRoot $Name }
    $argv = @("-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", $toolPath) + $Arguments
    $info.Arguments = ($argv | ForEach-Object { ConvertTo-FpfArgument $_ }) -join " "
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = $script:FpfUtf8
    if ($WithoutGit) { $info.EnvironmentVariables["PATH"] = Join-Path $script:testRoot "no-executables" }
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
    param([string]$Name, [string[]]$Arguments, [int]$ExpectedExit = 0, [switch]$WithoutGit)
    return Complete-FpfTestTool (Start-FpfTestTool $Name $Arguments -WithoutGit:$WithoutGit) $ExpectedExit
}

function Get-FpfFixtureStamp {
    param([string]$Root)
    return (@(Get-FpfTreePaths $Root | Sort-Object | ForEach-Object {
        $ioPath = Get-FpfIoPath $_
        if (-not ([IO.File]::GetAttributes($ioPath) -band [IO.FileAttributes]::Directory)) {
            $_ + "|" + [IO.FileInfo]::new($ioPath).Length + "|" + [IO.File]::GetLastWriteTimeUtc($ioPath).Ticks + "|" + (Get-FpfBytesHash (Read-FpfBytes $_))
        }
    }) -join "`n")
}

function Remove-FpfFixturePath {
    param([string]$Path)
    $resolved = [IO.Path]::GetFullPath($Path)
    $allowed = [IO.Path]::GetFullPath($script:testRoot).TrimEnd([char[]]"\/") + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw "Fixture cleanup escaped its task root." }
    foreach ($path in @(Get-FpfTreePaths $resolved)) {
        $ioPath = Get-FpfIoPath $path
        if ([IO.Directory]::Exists($ioPath)) { [IO.Directory]::Delete($ioPath, $false) }
        else { [IO.File]::SetAttributes($ioPath, [IO.FileAttributes]::Normal); [IO.File]::Delete($ioPath) }
    }
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
    $legacyPublications = @(
        "Engineering DPF Suite/README.md",
        "Engineering DPF Suite/SYSTEMS-ENGINEERING-PRINCIPLES-FRAMEWORK.md",
        "Foundational Thinking DPF Suite/README.md",
        "Foundational Thinking DPF Suite/COMPUTATIONAL-THINKING-DPF.md"
    )
    foreach ($path in @($script:FpfRequired) + $legacyPublications) {
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
    $localStatus = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Action", "Status"))
    $bad = Invoke-FpfTestTool "Sync-Fpf.ps1" ($baseArgs + @("-Force"))
    Assert-FpfTest ($localStatus.AcceptedRevision -eq $revision2 -and $bad.CheckResult -eq "failed" -and $bad.Notice -like "*identity*" -and $adoptionBefore -ceq [IO.File]::ReadAllText((Join-Path $app ".codex-local/fpf/adoption.json"))) "local snapshots remain readable but refresh rejects changed repository identity without rewriting adoption"
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

    # Current-layout snapshots and a change report larger than one page.
    $modernSource = Join-Path $testRoot "current source"
    $modernApp = Join-Path $testRoot "current application"
    New-FpfFixtureApp $modernSource
    New-FpfFixtureApp $modernApp
    foreach ($path in @($script:FpfRequired) + @("FPF-LIBRARY-REFERENCE.md", "DPFs/README.md")) {
        Write-FpfFixture (Join-Path $modernSource $path) ("# " + $path + "`n## M.1 - Fixture method`nCurrent layout condition.`n")
    }
    for ($number = 1; $number -le 65; $number++) {
        Write-FpfFixture (Join-Path $modernSource ("DPFs/Fixture Suite/Case-" + $number + ".md")) ("# Case`n## P." + $number + " - Read`nCurrent layout body.`n")
    }
    [void](Invoke-FpfFixtureGit $modernSource @("add", "."))
    [void](Invoke-FpfFixtureGit $modernSource @("commit", "-m", "Publish current layout"))
    $modernPin = Invoke-FpfFixtureGit $modernSource @("rev-parse", "HEAD")
    $modernArgs = @("-RepositoryRoot", $modernApp)
    $modern = Invoke-FpfTestTool "Sync-Fpf.ps1" ($modernArgs + @("-SourcePath", $modernSource))
    Assert-FpfTest ($modern.CandidateRevision -eq $modernPin -and $modern.Changes.Truncated) "current DPF layout is indexed recursively without retired publications"
    $seenChanges = [Collections.Generic.List[string]]::new()
    $pageOffset = 0
    do {
        $page = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernArgs + @("-Action", "List", "-Revision", $modernPin, "-ChangesFrom", "empty", "-Offset", [string]$pageOffset, "-MaxLines", "17", "-MaxChars", "5000"))
        Assert-FpfTest ($page.Revision -eq $modernPin -and $page.ChangesFrom -eq "empty" -and $page.Items.Count -le 17) "change continuation preserves both pins and item bounds"
        foreach ($item in $page.Items) { $seenChanges.Add(($item | ConvertTo-Json -Compress)) }
        $pageOffset = $page.NextOffset
    } while ($null -ne $pageOffset)
    Assert-FpfTest ($seenChanges.Count -eq $page.Total -and @($seenChanges | Select-Object -Unique).Count -eq $page.Total) "change pagination returns every file and pattern exactly once"
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernArgs + @("-Action", "List", "-Revision", $modernPin, "-ChangesFrom", "empty", "-Path", "USING-FPF.md")) 1
    Assert-FpfTest ($bad.Status -eq "error") "change listing rejects a conflicting publication filter"
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" ($modernArgs + @("-Action", "Accept", "-Revision", $modernPin, "-ReviewNote", "Synthetic current-layout review."))
    $modernCache = Join-Path $modernApp ".codex-local/fpf"
    $modernContext = Get-FpfContext $modernApp
    $modernStatePath = $modernContext.SourceStatePath
    $snapshotPath = Join-Path $modernCache ("snapshots/" + $modernPin)
    $modernReadArgs = $modernArgs + @("-Revision", $modernPin)
    $referenceRead = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Path", "USING-FPF.md"))
    $parkedObjects = Join-Path $testRoot "parked object store"
    [IO.Directory]::Move($modernContext.GitDirectory, $parkedObjects)
    $beforeReads = Get-FpfFixtureStamp $modernCache
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" ($modernArgs + @("-Action", "Status")) -WithoutGit
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" ($modernArgs + @("-Action", "Select", "-Revision", $modernPin)) -WithoutGit
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" ($modernArgs + @("-Offline")) -WithoutGit
    $null = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Action", "List")) -WithoutGit
    $null = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Action", "Search", "-Query", "condition", "-Path", "USING-FPF.md")) -WithoutGit
    $localRead = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Path", "USING-FPF.md")) -WithoutGit
    Assert-FpfTest ($localRead.Content -ceq $referenceRead.Content -and $beforeReads -ceq (Get-FpfFixtureStamp $modernCache)) "all six ordinary CLI paths work without Git or objects and preserve hashes and mtimes"
    [IO.Directory]::Move($parkedObjects, $modernContext.GitDirectory)

    $usingPath = Join-Path $snapshotPath "USING-FPF.md"
    Write-FpfFixture $usingPath "# Damaged local text`n"
    $unrelatedRead = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Path", "DPFs/Fixture Suite/Case-1.md")) -WithoutGit
    $beforeBadRead = Get-FpfFixtureStamp $modernCache
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Path", "USING-FPF.md")) 1 -WithoutGit
    Assert-FpfTest ($unrelatedRead.Content -like "*Current layout body*" -and $bad.Notice -like "*pinned Git blob*" -and $beforeBadRead -ceq (Get-FpfFixtureStamp $modernCache)) "scoped reads verify only accessed publications and never repair corruption"
    $bad = Invoke-FpfTestTool "Sync-Fpf.ps1" ($modernArgs + @("-Action", "Status")) 1 -WithoutGit
    Assert-FpfTest ($bad.Status -eq "error") "status validates the complete selected snapshot"
    $beforeRepair = Read-FpfJson $modernStatePath
    $repair = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
    Assert-FpfTest ($repair.CheckResult -eq "throttled" -and [DateTimeOffset]$repair.LastAttemptUtc -eq [DateTimeOffset]$beforeRepair.LastAttemptUtc) "writable Ensure repairs local publications without bypassing the fetch throttle"
    $indexPath = Get-FpfIndexPath $modernContext $modernPin
    [IO.File]::AppendAllText($indexPath, " ", $script:FpfUtf8)
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Action", "List")) 1 -WithoutGit
    Assert-FpfTest ($bad.Notice -like "*checksum*") "schema-valid index damage is detected without Git"
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
    $indexRecovered = Get-FpfManifest $modernContext $modernPin -VerifyFiles
    Assert-FpfTest ($indexRecovered.IndexVersion -eq 2) "only writable Ensure reconstructs a damaged index"

    # Construct the prior on-disk format from real pinned objects.
    $legacyIndex = Get-FpfManifest $modernContext $modernPin
    $legacyIndex.PSObject.Properties.Remove("IndexVersion")
    foreach ($file in $legacyIndex.Files) { $file.PSObject.Properties.Remove("Bytes") }
    Write-FpfJson (Join-Path $modernCache ("indexes/" + $modernPin + ".json")) $legacyIndex
    $legacyState = Read-FpfJson $modernStatePath
    foreach ($property in @("IndexVersion", "IndexHashes", "RetentionRevision", "PreviousRevision", "EditionOrder", "PendingAcceptanceOldRevision", "CleanupPending")) { $legacyState.PSObject.Properties.Remove($property) }
    Write-FpfJson $modernStatePath $legacyState
    Remove-FpfFixturePath (Join-Path $modernCache "snapshots")
    Remove-FpfFixturePath (Join-Path $modernCache "indexes/v2")
    $adoptionPath = Join-Path $modernCache "adoption.json"
    $adoptionBeforeMigration = [IO.File]::ReadAllText($adoptionPath)
    $legacyStamp = Get-FpfFixtureStamp $modernCache
    $legacyStatus = Invoke-FpfTestTool "Sync-Fpf.ps1" ($modernArgs + @("-Action", "Status")) -WithoutGit
    $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Path", "USING-FPF.md")) 1 -WithoutGit
    Assert-FpfTest ($legacyStatus.MigrationRequired -and $bad.Notice -like "*migration*" -and $legacyStamp -ceq (Get-FpfFixtureStamp $modernCache)) "legacy inspection reports migration without writes or Git"
    [void](Invoke-FpfGit -GitDirectory $modernContext.GitDirectory -Arguments @("update-ref", "-d", "refs/fpf/snapshots/$modernPin"))
    $realGit = (Get-Item -LiteralPath function:Invoke-FpfGit).ScriptBlock
    function Invoke-FpfGit {
        param([string[]]$Arguments, [string]$GitDirectory, [int]$TimeoutSeconds = 45, [switch]$AllowFailure, [byte[]]$InputBytes, [switch]$Binary)
        if ($Arguments[0] -eq "update-ref") { throw "Synthetic migration interruption after index publication." }
        & $script:realGit @PSBoundParameters
    }
    $wasInterrupted = $false
    try { Restore-FpfSnapshots $modernContext (Read-FpfJson $modernStatePath) }
    catch { $wasInterrupted = $_.Exception.Message -like "*Synthetic migration interruption*" }
    finally { Set-Item -LiteralPath function:Invoke-FpfGit -Value $realGit }
    $partialState = Read-FpfJson $modernStatePath
    Assert-FpfTest ($wasInterrupted -and (Get-FpfProperty $partialState "IndexVersion" 0) -ne 2 -and -not (Get-FpfProperty (Get-FpfProperty $partialState "IndexHashes") $modernPin) -and $adoptionBeforeMigration -ceq [IO.File]::ReadAllText($adoptionPath)) "interrupted migration cannot publish a receipt before its protective ref"
    # Also recover the receipt-first interruption possible in an earlier writer.
    $partialHashes = [pscustomobject]@{}
    Set-FpfProperty $partialHashes $modernPin (Get-FpfBytesHash (Read-FpfBytes (Get-FpfIndexPath $modernContext $modernPin)))
    Set-FpfProperty $partialState "IndexHashes" $partialHashes
    Write-FpfJson $modernStatePath $partialState
    $migration = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
    $migratedRead = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Path", "USING-FPF.md"))
    Assert-FpfTest (-not $migration.MigrationRequired -and $migration.CheckResult -eq "throttled" -and $migration.AcceptedRevision -eq $modernPin -and $migratedRead.Content -ceq $referenceRead.Content -and $adoptionBeforeMigration -ceq [IO.File]::ReadAllText($adoptionPath)) "writable migration preserves adoption and same-pin content without fetching"
    Assert-FpfTest (-not [IO.File]::Exists((Join-Path $modernCache ("indexes/" + $modernPin + ".json")))) "successful migration retires obsolete indexes"
    $retainedObject = Invoke-FpfGit -GitDirectory $modernContext.GitDirectory -Arguments @("cat-file", "-e", $modernPin) -AllowFailure
    Assert-FpfTest ($retainedObject.ExitCode -eq 0) "interrupted receipt publication restores its missing ref before object collection"
    Write-FpfFixture (Join-Path $snapshotPath "USING-FPF.md") "Damaged after migration.`n"
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
    $repairedAfterMigration = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Path", "USING-FPF.md"))
    Assert-FpfTest ($repairedAfterMigration.Content -ceq $referenceRead.Content) "retained objects still support local repair after interrupted migration and cleanup"

    $retentionWorktree = Join-Path $testRoot "retention worktree"
    [void](Invoke-FpfFixtureGit $modernApp @("worktree", "add", "--detach", $retentionWorktree, "HEAD"))
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $retentionWorktree, "-Action", "Accept", "-Revision", $modernPin, "-ReviewNote", "Synthetic shared adoption.")
    $otherAdoptionPath = Join-Path $retentionWorktree ".codex-local/fpf/adoption.json"
    $otherAdoptionBefore = [IO.File]::ReadAllText($otherAdoptionPath)
    $latestWorktree = Join-Path $testRoot "latest acceptance worktree"
    [void](Invoke-FpfFixtureGit $modernApp @("worktree", "add", "--detach", $latestWorktree, "HEAD"))
    $editions = [Collections.Generic.List[string]]::new()
    for ($number = 1; $number -le 5; $number++) {
        Write-FpfFixture (Join-Path $modernSource "USING-FPF.md") ("# Usage edition " + $number + "`n## Conditions`nPreserve the selected edition.`n")
        [void](Invoke-FpfFixtureGit $modernSource @("add", "."))
        [void](Invoke-FpfFixtureGit $modernSource @("commit", "-m", ("Edition " + $number)))
        $edition = Invoke-FpfFixtureGit $modernSource @("rev-parse", "HEAD")
        $editions.Add($edition)
        $updateArgs = if ($number -eq 5) { @("-RepositoryRoot", $retentionWorktree) } else { $modernArgs }
        $updated = Invoke-FpfTestTool "Sync-Fpf.ps1" ($updateArgs + @("-Force"))
        Assert-FpfTest ($updated.CheckResult -eq "checked" -and @(Get-ChildItem -LiteralPath (Join-Path $modernCache "snapshots") -Directory).Count -le 3) "writable refresh retains at most three editions"
        if ($number -eq 1) {
            $null = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $latestWorktree, "-Action", "Accept", "-Revision", $edition, "-ReviewNote", "Synthetic newer shared acceptance.")
        }
        if ($number -eq 2) {
            # Fail the real adoption replace after publishing shared acceptance.
            $before = [IO.File]::ReadAllText($adoptionPath)
            $adoptionBlock = [IO.File]::Open($adoptionPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
            try {
                $failedAcceptance = Invoke-FpfTestTool "Sync-Fpf.ps1" ($modernArgs + @("-Action", "Accept", "-Revision", $edition, "-ReviewNote", "Synthetic interrupted adoption.")) 1
            }
            finally { $adoptionBlock.Dispose() }
            $pending = Read-FpfJson $modernStatePath
            Assert-FpfTest ($failedAcceptance.Status -eq "error" -and $pending.PendingAcceptanceOldRevision -eq $modernPin -and $before -ceq [IO.File]::ReadAllText($adoptionPath)) "failed adoption protects the calling checkout pin even when shared acceptance differs"
            $null = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $retentionWorktree)
            $survivors = @(Get-FpfRetainedRevisions (Read-FpfJson $modernStatePath))
            Assert-FpfTest ($survivors.Count -eq 3 -and $modernPin -in $survivors -and $editions[0] -in $survivors -and $before -ceq [IO.File]::ReadAllText($adoptionPath)) "interrupted acceptance protects prior adoption and the most recent other edition within three slots"
            $null = Invoke-FpfTestTool "Sync-Fpf.ps1" ($modernArgs + @("-Action", "Accept", "-Revision", $edition, "-ReviewNote", "Synthetic retention review."))
            # Durable state after adoption but before clearing the pending receipt.
            $pending = Read-FpfJson $modernStatePath
            Set-FpfProperty $pending "PendingAcceptanceOldRevision" $modernPin
            Write-FpfJson $modernStatePath $pending
            $null = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
            $resumed = Read-FpfJson $modernStatePath
            Assert-FpfTest (-not $resumed.PendingAcceptanceOldRevision -and $resumed.PreviousRevision -eq $editions[0]) "completed acceptance reconciles its receipt and retains the most recent other edition"
        }
        if ($number -eq 4) {
            $pending = Read-FpfJson $modernStatePath
            Set-FpfProperty $pending "RetentionRevision" $edition
            Set-FpfProperty $pending "PendingAcceptanceOldRevision" $editions[1]
            Write-FpfJson $modernStatePath $pending
        }
        if ($number -eq 5) {
            $resumed = Read-FpfJson $modernStatePath
            $survivors = @(Get-FpfRetainedRevisions $resumed)
            $stillAdopted = Read-FpfJson $adoptionPath
            Assert-FpfTest (-not $resumed.PendingAcceptanceOldRevision -and $survivors.Count -eq 3 -and $resumed.RetentionRevision -eq $editions[3] -and $resumed.PreviousRevision -eq $editions[2] -and $stillAdopted.AcceptedRevision -eq $editions[1]) "another checkout can refresh after interrupted acceptance without exceeding retention or rewriting adoption"
            $null = Invoke-FpfTestTool "Sync-Fpf.ps1" ($modernArgs + @("-Action", "Accept", "-Revision", $edition, "-ReviewNote", "Synthetic retention review."))
        }
    }
    $lastPin = $editions[$editions.Count - 1]
    $current = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
    $expired = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernReadArgs + @("-Path", "USING-FPF.md")) 1 -WithoutGit
    $otherStatus = Invoke-FpfTestTool "Sync-Fpf.ps1" @("-RepositoryRoot", $retentionWorktree, "-Action", "Status") -WithoutGit
    $expiredObject = Invoke-FpfGit -GitDirectory $modernContext.GitDirectory -Arguments @("cat-file", "-e", $modernPin) -AllowFailure
    Assert-FpfTest ($expired.Notice -like "*expired*" -and $otherStatus.UnavailableAcceptedRevision -eq $modernPin -and $otherAdoptionBefore -ceq [IO.File]::ReadAllText($otherAdoptionPath)) "expired worktree pins fail without substitution or rewriting another checkout's adoption"
    Assert-FpfTest ($expiredObject.ExitCode -ne 0 -and -not [IO.Directory]::Exists($snapshotPath) -and -not [IO.File]::Exists($indexPath)) "retention reclaims expired snapshots indexes and Git objects"
    foreach ($pin in @(Get-FpfRetainedRevisions (Read-FpfJson $modernStatePath))) {
        $null = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernArgs + @("-Revision", $pin, "-Path", "USING-FPF.md")) -WithoutGit
    }
    Assert-FpfTest ($true) "every retained edition remains readable without Git"

    # Reintroduce an old owned ref and orphan snapshot, then interrupt object collection.
    [void](Invoke-FpfGit -GitDirectory $modernContext.GitDirectory -Arguments @("fetch", "--depth=1", $modernSource, ($modernPin + ":refs/fpf/snapshots/" + $modernPin)))
    $orphanPath = Join-Path $modernCache ("snapshots/" + $modernPin)
    Write-FpfFixture (Join-Path $orphanPath "orphan.md") "# Task-owned interrupted export`n"
    Write-FpfFixture (Join-Path $modernCache "keep-user-note.txt") "Unfamiliar files stay untouched.`n"
    $realGit = (Get-Item -LiteralPath function:Invoke-FpfGit).ScriptBlock
    function Invoke-FpfGit {
        param([string[]]$Arguments, [string]$GitDirectory, [int]$TimeoutSeconds = 45, [switch]$AllowFailure, [byte[]]$InputBytes, [switch]$Binary)
        if ($Arguments -contains "gc") { throw "Synthetic interruption before object collection." }
        & $script:realGit @PSBoundParameters
    }
    $wasInterrupted = $false
    try { Remove-FpfExpiredEditions $modernContext (Read-FpfJson $modernStatePath) -CollectObjects }
    catch { $wasInterrupted = $_.Exception.Message -like "*Synthetic interruption*" }
    finally { Set-Item -LiteralPath function:Invoke-FpfGit -Value $realGit }
    $interruptedState = Read-FpfJson $modernStatePath
    Assert-FpfTest ($wasInterrupted -and $interruptedState.CleanupPending -and -not [IO.Directory]::Exists($orphanPath)) "interrupted cleanup persists intent after file and ref removal"
    $retry = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
    $expiredObject = Invoke-FpfGit -GitDirectory $modernContext.GitDirectory -Arguments @("cat-file", "-e", $modernPin) -AllowFailure
    Assert-FpfTest (-not $retry.CleanupPending -and $retry.CheckResult -eq "throttled" -and [DateTimeOffset]$retry.LastAttemptUtc -eq [DateTimeOffset]$interruptedState.LastAttemptUtc -and $expiredObject.ExitCode -ne 0) "cleanup retries object collection inside the fetch throttle"
    Assert-FpfTest ([IO.File]::Exists((Join-Path $modernCache "keep-user-note.txt"))) "cleanup preserves unfamiliar files"

    $outside = Join-Path $testRoot "redirect destination"
    Write-FpfFixture (Join-Path $outside "USING-FPF.md") "PRIVATE-FIXTURE-SENTINEL`n"
    $redirect = Join-Path $modernCache ("snapshots/" + ("f" * 40))
    [void](New-Item -ItemType Junction -Path $redirect -Target $outside)
    try {
        $blockedCleanup = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
        Assert-FpfTest ($blockedCleanup.CheckResult -eq "failed" -and $blockedCleanup.CleanupPending -and [IO.File]::Exists((Join-Path $outside "USING-FPF.md"))) "cleanup rejects redirected expired directories before touching their targets"
    }
    finally { [IO.Directory]::Delete($redirect, $false) }
    $recoveredCleanup = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
    Assert-FpfTest (-not $recoveredCleanup.CleanupPending -and -not $recoveredCleanup.Notice) "successful cleanup clears its own failure without waiting for a network refresh"
    $wrongIndexType = Join-Path $modernCache ("indexes/v2/" + ("e" * 40) + ".json")
    Write-FpfFixture (Join-Path $wrongIndexType "preserve.txt") "Unexpected directory is not an index.`n"
    $badType = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
    Assert-FpfTest ($badType.CheckResult -eq "failed" -and [IO.File]::Exists((Join-Path $wrongIndexType "preserve.txt"))) "cleanup refuses unexpected target types without deleting their contents"
    Remove-FpfFixturePath $wrongIndexType
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
    $liveSnapshot = Join-Path $modernCache ("snapshots/" + $lastPin)
    $savedSnapshot = Join-Path $testRoot "saved live snapshot"
    [IO.Directory]::Move($liveSnapshot, $savedSnapshot)
    [void](New-Item -ItemType Junction -Path $liveSnapshot -Target $outside)
    try {
        $bad = Invoke-FpfTestTool "Read-Fpf.ps1" ($modernArgs + @("-Revision", $lastPin, "-Path", "USING-FPF.md")) 1 -WithoutGit
        Assert-FpfTest ($bad.Notice -like "*symlink or junction*" -and $script:lastFpfOutput -notlike "*PRIVATE-FIXTURE-SENTINEL*") "publication redirection fails before content disclosure"
    }
    finally {
        [IO.Directory]::Delete($liveSnapshot, $false)
        [IO.Directory]::Move($savedSnapshot, $liveSnapshot)
    }
    [void](Invoke-FpfGit -GitDirectory $modernContext.GitDirectory -Arguments @("config", "--local", "gc.recentObjectsHook", "must-not-execute"))
    $unsafeState = Read-FpfJson $modernStatePath
    Set-FpfProperty $unsafeState "CleanupPending" $true
    Write-FpfJson $modernStatePath $unsafeState
    $blockedConfig = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs
    Assert-FpfTest ($blockedConfig.CheckResult -eq "failed" -and $blockedConfig.CleanupPending -and $blockedConfig.Notice -like "*executable Git configuration*") "cleanup refuses executable Git configuration"
    [void](Invoke-FpfGit -GitDirectory $modernContext.GitDirectory -Arguments @("config", "--local", "--unset", "gc.recentObjectsHook"))
    $null = Invoke-FpfTestTool "Sync-Fpf.ps1" $modernArgs


    $succeeded = $true
}
finally {
    if ($succeeded -and -not $KeepArtifacts) {
        $allowed = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../../artifacts")).TrimEnd([char[]]"\/") + [System.IO.Path]::DirectorySeparatorChar
        $resolved = [System.IO.Path]::GetFullPath($testRoot)
        if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw "Refusing cleanup outside the test artifacts directory." }
        Assert-FpfDirectory $resolved
        foreach ($path in @(Get-FpfTreePaths $resolved)) {
            $ioPath = Get-FpfIoPath $path
            if ([IO.Directory]::Exists($ioPath)) { [IO.Directory]::Delete($ioPath, $false) }
            else { [IO.File]::SetAttributes($ioPath, [IO.FileAttributes]::Normal); [IO.File]::Delete($ioPath) }
        }
    }
    else { Write-Output "Fixture evidence retained: $testRoot" }
}
Write-Output "Passed $passed FPF assertions on PowerShell $($PSVersionTable.PSVersion)."
