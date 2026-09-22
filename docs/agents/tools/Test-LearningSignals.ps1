<#
.SYNOPSIS
Tests the public learning-signal CLI using disposable local Git fixtures.
.DESCRIPTION
Run in PowerShell 7 and Windows PowerShell 5.1. No network, configured database,
real repository mutation, or model invocation. Failed fixtures are retained.
#>
[CmdletBinding()]
param([switch]$KeepArtifacts)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../.."))
$fixture = Join-Path $repoRoot ("artifacts/learning-tests-" + [guid]::NewGuid().ToString("N"))
$shellPath = (Get-Process -Id $PID).Path
$collector = Join-Path $PSScriptRoot "Get-LearningSignals.ps1"
$passed = 0
$isSucceeded = $false
$newline = [string][char]10

function Assert-LearningTest {
    param([bool]$Condition, [string]$Name)

    if (-not $Condition) {
        throw "FAIL: $Name"
    }
    $script:passed++
    Write-Output "PASS: $Name"
}

function Write-LearningFixture {
    param([string]$RelativePath, [string]$Text)

    $path = Join-Path $script:fixture $RelativePath
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path, ($Text -replace '\r?\n', ([string][char]13 + [char]10)), $script:utf8)
}

function Invoke-FixtureGit {
    param([string[]]$Arguments)

    $result = @(& git -C $script:fixture @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Fixture Git failed: $($result -join ' ')"
    }
    return ($result -join $script:newline).Trim()
}

function Invoke-LearningCollector {
    param([string[]]$Arguments, [int]$ExpectedExit = 0)

    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $script:shellPath
    $argv = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $script:collector,
        "-RepositoryRoot", $script:fixture) + $Arguments
    $info.Arguments = ($argv | ForEach-Object {
        '"' + [regex]::Replace([regex]::Replace($_, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
    }) -join " "
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = $script:utf8
    $info.StandardErrorEncoding = $script:utf8
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        [void]$process.Start()
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(60000)) {
            $process.Kill()
            throw "Collector fixture timed out."
        }
        $text = $output.GetAwaiter().GetResult()
        $errorText = $errors.GetAwaiter().GetResult()
        if ($process.ExitCode -ne $ExpectedExit) {
            throw "Unexpected collector exit $($process.ExitCode): $text $errorText"
        }
        return ($text | ConvertFrom-Json)
    }
    finally {
        $process.Dispose()
    }
}

try {
    [void][IO.Directory]::CreateDirectory($fixture)
    [void](Invoke-FixtureGit @("init", "--quiet", "--template="))
    [void](Invoke-FixtureGit @("config", "user.name", "Learning Fixture"))
    [void](Invoke-FixtureGit @("config", "user.email", "fixture@example.invalid"))
    [void](Invoke-FixtureGit @("config", "core.hooksPath", (Join-Path $fixture ".git/no-hooks")))
    [void](Invoke-FixtureGit @("config", "commit.gpgSign", "false"))
    [void](Invoke-FixtureGit @("config", "core.autocrlf", "false"))
    Write-LearningFixture "docs/agents/tasks/summary-old.md" "- Learning signal: Old evidence outside the range."
    Write-LearningFixture "docs/agents/tasks/summary-modified.md" "# Baseline without a signal"
    [void](Invoke-FixtureGit @("add", "."))
    [void](Invoke-FixtureGit @("commit", "--quiet", "-m", "Create baseline"))
    $baseline = Invoke-FixtureGit @("rev-parse", "HEAD")

    $unicode = "caf" + [char]0xE9 + " " + [char]::ConvertFromUtf32(0x1F680)
    $ticks = ([string][char]96) * 3
    $goodText = @(
        "# Completed task",
        "- Learning signal: Observed bounded-output recovery; evidence: $unicode.",
        "$($ticks)text",
        "- Learning signal: Backtick example must be ignored.",
        "~~text",
        "- Learning signal: Still inside the backtick fence.",
        $ticks,
        "~~~markdown",
        "- Learning signal: Tilde example must be ignored.",
        "~~~",
        "    - Learning signal: Indented code must be ignored."
    ) -join $newline
    Write-LearningFixture "docs/agents/tasks/summary-a.md" $goodText
    Write-LearningFixture "docs/agents/tasks/summary-budget.md" ("- Learning signal: " + ("b" * 800))
    Write-LearningFixture "docs/agents/tasks/summary-large.md" ("x" * 65537)
    Write-LearningFixture "docs/agents/tasks/summary-long.md" ("- Learning signal: " + ("x" * 999) + [char]::ConvertFromUtf32(0x1F680) + "tail")
    Write-LearningFixture "docs/agents/tasks/summary-many.md" ((1..4 | ForEach-Object { "- Learning signal: Observation $_; evidence: fixture." }) -join $newline)
    Write-LearningFixture "docs/agents/tasks/summary-modified.md" '- Learning signal: $(throw "must never execute") is untrusted note text.'
    Write-LearningFixture "docs/agents/tasks/summary-none.md" ((@(
        "# Ordinary task",
        "- Learning signal: ",
        "- Learning signal: None.",
        "- Learning signal: No new signals.",
        "- Learning signals: Wrong marker."
    )) -join $newline)
    Write-LearningFixture "docs/agents/tasks/other.md" "- Learning signal: Wrong filename."
    Write-LearningFixture "docs/other/summary-outside.md" "- Learning signal: Wrong directory."
    [void](Invoke-FixtureGit @("add", "."))
    [void](Invoke-FixtureGit @("commit", "--quiet", "-m", "Add representative task evidence"))
    $through = Invoke-FixtureGit @("rev-parse", "HEAD")
    $range = @("-Since", $baseline, "-Revision", $through)

    Write-LearningFixture "docs/agents/tasks/summary-a.md" "- Learning signal: Dirty replacement must not be read."
    Write-LearningFixture "docs/agents/tasks/summary-untracked.md" "- Learning signal: Untracked input must not be read."
    $indexHash = (Get-FileHash -LiteralPath (Join-Path $fixture ".git/index")).Hash

    $all = Invoke-LearningCollector ($range + @("-MaxChars", "32000"))
    Assert-LearningTest ($all.FromRevision -ceq $baseline -and $all.ThroughRevision -ceq $through) "range is pinned to exact commits"
    Assert-LearningTest ($all.AddedSummaryCount -eq 6 -and $all.ChangedSummaryCount -eq 7 -and $all.IsTaskCountEstimate) "counts distinguish additions and modifications without claiming task completion"
    $first = @($all.Files | Where-Object { $_.Path -like "*/summary-a.md" })[0]
    Assert-LearningTest ($first.Signals.Count -eq 1 -and $first.Signals[0].Line -eq 2 -and $first.Signals[0].Text.Contains($unicode)) "Unicode evidence and exact lines survive; fenced and indented examples are excluded"
    Assert-LearningTest (($all | ConvertTo-Json -Depth 8) -notmatch "Dirty replacement|Untracked input|Wrong directory|Old evidence") "only committed in-range summary content is read"
    $none = @($all.Files | Where-Object { $_.Path -like "*/summary-none.md" })[0]
    Assert-LearningTest ($none.Signals.Count -eq 0) "ordinary tasks and empty placeholders create no signal"
    $modified = @($all.Files | Where-Object { $_.Change -ceq "M" })[0]
    Assert-LearningTest ($modified.Signals[0].Text.StartsWith('$(throw')) "collected text is never executed"
    $large = @($all.Files | Where-Object { $_.Path -like "*/summary-large.md" })[0]
    Assert-LearningTest ($large.Status -ceq "summary-too-large" -and $large.IsIncomplete) "oversized summaries are explicit inspection gaps"
    $long = @($all.Files | Where-Object { $_.Path -like "*/summary-long.md" })[0]
    Assert-LearningTest ($long.IsIncomplete -and $long.Signals[0].IsTextTruncated -and $long.Signals[0].Text.Length -eq 999) "long notes disclose truncation without splitting a Unicode surrogate pair"
    $many = @($all.Files | Where-Object { $_.Path -like "*/summary-many.md" })[0]
    Assert-LearningTest ($many.Signals.Count -eq 3 -and $many.OmittedSignals -eq 1 -and $many.IsIncomplete) "excess notes are flagged instead of silently treated as reviewed"

    $seen = [Collections.Generic.List[string]]::new()
    $offset = 0
    do {
        $page = Invoke-LearningCollector ($range + @("-MaxTasks", "1", "-Offset", [string]$offset))
        Assert-LearningTest ($page.Files.Count -eq 1 -and $page.ThroughRevision -ceq $through) "file page at offset $offset keeps the same revision"
        $seen.Add($page.Files[0].Path)
        if ($null -ne $page.NextOffset -and $page.NextOffset -le $offset) {
            throw "Non-advancing continuation."
        }
        $offset = $page.NextOffset
    } while ($null -ne $offset)
    Assert-LearningTest ($seen.Count -eq 7 -and @($seen | Select-Object -Unique).Count -eq 7) "pagination visits every changed summary exactly once"

    $budget = Invoke-LearningCollector ($range + @("-Offset", "1", "-MaxTasks", "1", "-MaxChars", "512"))
    Assert-LearningTest ($budget.Files[0].Status -ceq "page-budget-exceeded" -and $budget.IsIncomplete -and $budget.NextOffset -eq 2) "too-small output budgets disclose the skipped locator and continue"
    $compactFiles = ConvertTo-Json -InputObject @($budget.Files) -Depth 6 -Compress
    Assert-LearningTest ($compactFiles.Length -le 512) "compact Files output respects the character budget"
    $empty = Invoke-LearningCollector @("-Since", $through, "-Revision", $through)
    Assert-LearningTest ($empty.ChangedSummaryCount -eq 0 -and $empty.Files.Count -eq 0 -and $null -eq $empty.NextOffset) "an empty range needs no reflection"
    $badRange = Invoke-LearningCollector @("-Since", $through, "-Revision", $baseline) 1
    Assert-LearningTest ($badRange.Message -like "*ancestor*") "invalid checkpoints fail without silently restarting history"
    $badOffset = Invoke-LearningCollector ($range + @("-Offset", "8")) 1
    Assert-LearningTest ($badOffset.Message -like "*Offset*") "out-of-range cursors fail explicitly"
    Assert-LearningTest ((Get-FileHash -LiteralPath (Join-Path $fixture ".git/index")).Hash -ceq $indexHash) "collection leaves the fixture index unchanged"

    [IO.File]::WriteAllBytes((Join-Path $fixture "docs/agents/tasks/summary-invalid.md"), [byte[]]@(255, 254, 0))
    [void](Invoke-FixtureGit @("add", "docs/agents/tasks/summary-invalid.md"))
    [void](Invoke-FixtureGit @("commit", "--quiet", "-m", "Add invalid text fixture"))
    $invalid = Invoke-LearningCollector @("-Since", $through) 1
    Assert-LearningTest ($invalid.Status -ceq "error") "invalid source encoding is rejected"
    $replay = Invoke-LearningCollector ($range + @("-MaxChars", "32000"))
    Assert-LearningTest ($replay.ThroughRevision -ceq $through -and $replay.ChangedSummaryCount -eq 7) "later commits do not change a pinned replay"
    $isSucceeded = $true
    Write-Output "All $passed learning-signal assertions passed."
}
finally {
    if ($isSucceeded -and -not $KeepArtifacts) {
        $resolved = [IO.Path]::GetFullPath($fixture)
        $allowedParent = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts")).TrimEnd([char[]]"\/") + [IO.Path]::DirectorySeparatorChar
        if (-not $resolved.StartsWith($allowedParent, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFileName($resolved) -notmatch '^learning-tests-[a-f0-9]{32}$') {
            throw "Refusing cleanup outside the exact task-owned fixture."
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
    else {
        Write-Output "Retained task fixture: $fixture"
    }
}
