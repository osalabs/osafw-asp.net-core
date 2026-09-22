<#
.SYNOPSIS
Collects explicit learning signals from bounded, committed task summaries.
.DESCRIPTION
Read-only. Requires an ancestor commit range; ignores working-tree and untracked
content, fenced examples, and empty placeholders. Summary counts are an advisory
proxy, not proof of completed substantive tasks. Never executes collected text.
MaxChars bounds compact Files JSON; revision/count/continuation metadata is extra.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-fA-F0-9]{40}([a-fA-F0-9]{24})?$')]
    [string]$Since,

    [string]$Revision = "HEAD",
    [string]$RepositoryRoot,
    [ValidateRange(0, 2147483647)][int]$Offset = 0,
    [ValidateRange(1, 100)][int]$MaxTasks = 20,
    [ValidateRange(512, 32000)][int]$MaxChars = 6000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$MAX_SUMMARY_BYTES = 65536
$MAX_SIGNALS_PER_SUMMARY = 3
$MAX_SIGNAL_CHARS = 1000
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$originalOutputEncoding = [Console]::OutputEncoding
[Console]::OutputEncoding = $utf8

function ConvertTo-LearningGitArgument {
    param([string]$Value)

    # Quote argv for ProcessStartInfo.Arguments on Windows PowerShell 5.1 too.
    if ($Value -notmatch '[\s"]' -and $Value.Length -gt 0) {
        return $Value
    }

    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Invoke-LearningGit {
    param([string[]]$Arguments, [switch]$IsNonzeroAllowed)

    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $script:gitPath
    $argv = @("-C", $script:root) + $Arguments
    $info.Arguments = ($argv | ForEach-Object { ConvertTo-LearningGitArgument $_ }) -join " "
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = $script:utf8
    $info.StandardErrorEncoding = $script:utf8
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $reader = $null

    try {
        [void]$process.Start()
        # Disable BOM detection: a UTF-16 blob must not masquerade as valid UTF-8.
        $reader = [IO.StreamReader]::new($process.StandardOutput.BaseStream, $script:utf8, $false)
        $output = $reader.ReadToEndAsync()
        $errorOutput = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill()
            throw "Git read timed out."
        }

        $text = $output.GetAwaiter().GetResult()
        [void]$errorOutput.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0 -and -not $IsNonzeroAllowed) {
            throw "Git could not read the requested repository, commit, or path."
        }

        return [pscustomobject]@{ Text = $text; ExitCode = $process.ExitCode }
    }
    finally {
        if ($null -ne $reader) {
            $reader.Dispose()
        }
        $process.Dispose()
    }
}

function Get-SummarySignals {
    param([string]$Text)

    $signals = [Collections.Generic.List[object]]::new()
    $lines = [regex]::Split($Text, '\r\n|\n|\r')
    $fenceCharacter = ""
    $fenceLength = 0
    $omitted = 0

    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        $fence = [regex]::Match($line, '^ {0,3}(`{3,}|~{3,})(.*)$')
        if ($fence.Success) {
            $run = $fence.Groups[1].Value
            $tail = $fence.Groups[2].Value
            if ($fenceLength -gt 0) {
                if ($run.Substring(0, 1) -ceq $fenceCharacter -and
                    $run.Length -ge $fenceLength -and [string]::IsNullOrWhiteSpace($tail)) {
                    $fenceLength = 0
                }
            }
            elseif ($run[0] -ne [char]96 -or $tail.IndexOf([char]96) -lt 0) {
                $fenceCharacter = $run.Substring(0, 1)
                $fenceLength = $run.Length
            }
            continue
        }

        if ($fenceLength -gt 0) {
            continue
        }

        $match = [regex]::Match($line, '^ {0,3}- Learning signal:[ \t]*(\S.*)$')
        if (-not $match.Success) {
            continue
        }

        $value = $match.Groups[1].Value.Trim()
        if ($value -match '^(?i:none|n/a|no (new )?signals?)[.!]?$') {
            continue
        }

        if ($signals.Count -ge $script:MAX_SIGNALS_PER_SUMMARY) {
            $omitted++
            continue
        }

        $isTruncated = $value.Length -gt $script:MAX_SIGNAL_CHARS
        if ($isTruncated) {
            $value = $value.Substring(0, $script:MAX_SIGNAL_CHARS)
            if ([char]::IsHighSurrogate($value[$value.Length - 1])) {
                $value = $value.Substring(0, $value.Length - 1)
            }
        }

        $signals.Add([pscustomobject]@{
            Line = $i + 1
            Text = $value
            IsTextTruncated = $isTruncated
        })
    }

    return [pscustomobject]@{ Signals = $signals.ToArray(); OmittedSignals = $omitted }
}

try {
    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = Join-Path $PSScriptRoot "../../.."
    }

    $root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $gitPath = (Get-Command git -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $from = (Invoke-LearningGit @("rev-parse", "--verify", "--end-of-options", "$Since^{commit}")).Text.Trim()
    $through = (Invoke-LearningGit @("rev-parse", "--verify", "--end-of-options", "$Revision^{commit}")).Text.Trim()
    $ancestor = Invoke-LearningGit @("merge-base", "--is-ancestor", $from, $through) -IsNonzeroAllowed
    if ($ancestor.ExitCode -ne 0) {
        throw "Since must be an ancestor of Revision. Select a valid reviewed checkpoint; do not silently reset it."
    }

    $diff = (Invoke-LearningGit @("diff", "--name-status", "-z", "--no-renames", "--diff-filter=AM",
        $from, $through, "--", "docs/agents/tasks")).Text
    $tokens = $diff.Split([char]0)
    $changes = [Collections.Generic.List[object]]::new()

    for ($i = 0; $i + 1 -lt $tokens.Length; $i += 2) {
        $path = $tokens[$i + 1]
        if ($path -cmatch '^docs/agents/tasks/summary-[^/\\\x00-\x1f]+\.md$') {
            $changes.Add([pscustomobject]@{ Path = $path; Change = $tokens[$i] })
        }
    }

    if ($Offset -gt $changes.Count) {
        throw "Offset exceeds the number of changed summaries in this pinned range."
    }

    $files = [Collections.Generic.List[object]]::new()
    $characters = 2
    $next = $Offset
    $isIncomplete = $false
    $signalCount = 0

    while ($next -lt $changes.Count -and $files.Count -lt $MaxTasks) {
        $change = $changes[$next]
        $entry = [ordered]@{
            Path = $change.Path
            Change = $change.Change
            Status = "read"
            IsIncomplete = $false
            Signals = @()
        }

        $object = $through + ":" + $change.Path
        $size = [long](Invoke-LearningGit @("cat-file", "-s", $object)).Text.Trim()
        if ($size -gt $MAX_SUMMARY_BYTES) {
            $entry.Status = "summary-too-large"
            $entry.IsIncomplete = $true
            $entry["Bytes"] = $size
        }
        else {
            $sourceText = (Invoke-LearningGit @("cat-file", "blob", $object)).Text
            $extracted = Get-SummarySignals $sourceText
            $entry.Signals = @($extracted.Signals)
            if ($extracted.OmittedSignals -gt 0) {
                $entry["OmittedSignals"] = $extracted.OmittedSignals
                $entry.IsIncomplete = $true
            }
            if (@($entry.Signals | Where-Object { $_.IsTextTruncated }).Count -gt 0) {
                $entry.IsIncomplete = $true
            }
        }

        $cost = ($entry | ConvertTo-Json -Depth 6 -Compress).Length + 1
        if ($characters + $cost -gt $MaxChars) {
            if ($files.Count -gt 0) {
                break
            }

            $entry = [ordered]@{
                Path = $change.Path
                Change = $change.Change
                Status = "page-budget-exceeded"
                IsIncomplete = $true
                Signals = @()
            }
            $cost = ($entry | ConvertTo-Json -Compress).Length + 1
            if ($characters + $cost -gt $MaxChars) {
                throw "A summary locator exceeds MaxChars; increase the output budget."
            }
        }

        $files.Add([pscustomobject]$entry)
        $characters += $cost
        $signalCount += @($entry.Signals).Count
        $isIncomplete = $isIncomplete -or $entry.IsIncomplete
        $next++
    }

    [pscustomobject]@{
        FromRevision = $from
        ThroughRevision = $through
        AddedSummaryCount = @($changes | Where-Object { $_.Change -ceq "A" }).Count
        ChangedSummaryCount = $changes.Count
        IsTaskCountEstimate = $true
        Offset = $Offset
        Files = $files.ToArray()
        SignalsInPage = $signalCount
        IsIncomplete = $isIncomplete
        NextOffset = $(if ($next -lt $changes.Count) { $next } else { $null })
    } | ConvertTo-Json -Depth 7
}
catch {
    [pscustomobject]@{ Status = "error"; Message = $_.Exception.Message } | ConvertTo-Json -Compress
    exit 1
}
finally {
    [Console]::OutputEncoding = $originalOutputEncoding
}
