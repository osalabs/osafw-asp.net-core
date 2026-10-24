<#
.SYNOPSIS
Tests instruction inventory and protected-path handling in an isolated Git fixture.
#>
[CmdletBinding()]
param([switch]$KeepArtifacts)
. (Join-Path $PSScriptRoot "Fpf.Common.ps1")
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../.."))
$fixtureRoot = Join-Path $repoRoot ("artifacts/instruction-fixtures-" + [guid]::NewGuid().ToString("N"))
$shellPath = (Get-Process -Id $PID).Path
$passed = 0
$isComplete = $false

function Write-InstructionFixture {
    param([string]$Relative, [string]$Content)
    $path = Join-Path $script:fixtureRoot $Relative
    Assert-FpfDirectory $path
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path, ($Content -replace "\r?\n", "`r`n"), $script:FpfUtf8)
}

function Invoke-InstructionFixtureGit {
    param([string[]]$Arguments)
    [void](Invoke-FpfGit -Arguments (@("-C", $script:fixtureRoot) + $Arguments))
}

function Assert-InstructionFixture {
    param([bool]$Condition, [string]$Name)
    if (-not $Condition) { throw "FAIL: $Name" }
    $script:passed++
    Write-Output "PASS: $Name"
}

function Invoke-InstructionValidator {
    param([int]$ExpectedExit = 0, [string]$PrivateIdentifier)
    $arguments = @("-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File",
        (Join-Path $PSScriptRoot "Test-AgentInstructions.ps1"), "-RepoRoot", $script:fixtureRoot)
    if ($PrivateIdentifier) { $arguments += @("-PrivateIdentifier", $PrivateIdentifier) }
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $script:shellPath
    $info.Arguments = ($arguments | ForEach-Object { ConvertTo-FpfArgument $_ }) -join " "
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(90000)) { $process.Kill(); throw "Instruction fixture timed out." }
        $exitCode = $process.ExitCode
        $text = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
    }
    finally { $process.Dispose() }
    if ($exitCode -ne $ExpectedExit) { throw "Validator returned $exitCode instead of $ExpectedExit : $text" }
    return $text
}

function Save-InstructionFixtureManifest {
    param($Manifest)
    Write-InstructionFixture "docs/agents/instruction-pack.json" (($Manifest | ConvertTo-Json -Depth 12) + "`n")
}

try {
    [void][IO.Directory]::CreateDirectory($fixtureRoot)
    Invoke-InstructionFixtureGit @("init", "-b", "main", "--template=")
    $manifestText = [IO.File]::ReadAllText((Join-Path $repoRoot "docs/agents/instruction-pack.json"))
    $manifest = $manifestText | ConvertFrom-Json
    foreach ($entry in $manifest.files) {
        $source = Join-Path $repoRoot $entry.path
        $destination = Join-Path $fixtureRoot $entry.path
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
        [IO.File]::Copy($source, $destination)
    }
    foreach ($name in @("domain", "glossary", "heuristics")) { Write-InstructionFixture ("docs/agents/" + $name + ".md") "# Application-owned fixture`n" }
    # Supply only history filenames/index text; do not read historical bodies.
    $taskIndex = [IO.File]::ReadAllText((Join-Path $repoRoot "docs/agents/tasks/index.md"))
    Write-InstructionFixture "docs/agents/tasks/index.md" $taskIndex
    foreach ($match in [regex]::Matches($taskIndex, 'summary-[a-zA-Z0-9_-]+\.md')) {
        Write-InstructionFixture ("docs/agents/tasks/" + $match.Value) "# Historical fixture placeholder`n"
    }
    # Local-link validation needs existing destinations, not application contents.
    foreach ($entry in @($manifest.files | Where-Object { $_.path.EndsWith(".md") })) {
        $file = Join-Path $fixtureRoot $entry.path
        foreach ($match in [regex]::Matches([IO.File]::ReadAllText($file), '(?<!!)\[[^\]]+\]\((?<target>[^)]+)\)')) {
            $target = $match.Groups["target"].Value.Trim().Trim("<", ">")
            if ($target -match '^(?i:https?://|mailto:|#|/)') { continue }
            $target = [Uri]::UnescapeDataString(($target -split "#", 2)[0])
            if (-not $target) { continue }
            $destination = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($file)) $target))
            if (-not $destination.StartsWith($fixtureRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Fixture link escapes its root." }
            if (Test-Path -LiteralPath $destination) { continue }
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
            if ($target.EndsWith("/")) { [void][IO.Directory]::CreateDirectory($destination) }
            else { [IO.File]::WriteAllText($destination, "# Link-target fixture`r`n", $script:FpfUtf8) }
        }
    }
    Invoke-InstructionFixtureGit @("add", ".")
    $null = Invoke-InstructionValidator
    Assert-InstructionFixture $true "copied instruction pack passes with preserved application-owned destinations"

    foreach ($relative in @("docs/agents/domain.md", "docs/agents/glossary.md", "docs/agents/heuristics.md",
            "docs/agents/local_instructions.md", "docs/agents/fpf-app.md", "docs/agents/tools/unrelated-untracked.ps1",
            ".codex/agents/personal-profile.toml")) {
        Write-InstructionFixture $relative ""
        [IO.File]::WriteAllBytes((Join-Path $fixtureRoot $relative), [byte[]]@(0xc3, 0x28))
    }
    $null = Invoke-InstructionValidator
    Assert-InstructionFixture $true "protected knowledge private context personal profiles and unrelated untracked content are not read"

    $newPath = "docs/agents/tools/tests/unowned-fixture.ps1"
    Write-InstructionFixture $newPath "# New tracked helper fixture`n"
    Invoke-InstructionFixtureGit @("add", "--", $newPath)
    $failure = Invoke-InstructionValidator 1
    Assert-InstructionFixture ($failure -like "*omits tracked instruction/helper/test file*") "tracked nested helpers and tests require manifest ownership"
    [IO.File]::Delete((Join-Path $fixtureRoot $newPath))
    $null = Invoke-InstructionValidator
    Assert-InstructionFixture $true "a working-tree deletion can retire its manifest ownership in the same change"
    Invoke-InstructionFixtureGit @("rm", "--cached", "--", $newPath)

    $modified = $manifestText | ConvertFrom-Json
    $modified.files = @($modified.files | Where-Object { $_.path -ne "docs/agents/tools/Search-Repo.ps1" })
    Save-InstructionFixtureManifest $modified
    $failure = Invoke-InstructionValidator 1
    Assert-InstructionFixture ($failure -like "*omits tracked instruction/helper/test file: docs/agents/tools/Search-Repo.ps1*") "removing a live managed helper cannot evade the inventory"
    Write-InstructionFixture "docs/agents/instruction-pack.json" $manifestText

    $modified = $manifestText | ConvertFrom-Json
    $modified.files += [pscustomobject]@{ path = "docs/agents/tools/unrelated-untracked.ps1"; upgradeMode = "merge" }
    Save-InstructionFixtureManifest $modified
    $failure = Invoke-InstructionValidator 1
    Assert-InstructionFixture ($failure -like "*UTF-8*") "a deliberately declared untracked helper is validated"
    Write-InstructionFixture "docs/agents/tools/unrelated-untracked.ps1" "# Explicit active helper`n"
    $null = Invoke-InstructionValidator
    Assert-InstructionFixture $true "a valid explicitly declared new helper passes without staging"
    Write-InstructionFixture "docs/agents/instruction-pack.json" $manifestText

    $modified = $manifestText | ConvertFrom-Json
    $modified.files += [pscustomobject]@{ path = "docs/agents/fpf-app.md"; upgradeMode = "merge" }
    Save-InstructionFixtureManifest $modified
    $failure = Invoke-InstructionValidator 1
    Assert-InstructionFixture ($failure -like "*must preserve*" -and $failure -notlike "*not strict UTF-8*") "manifest cannot take ownership of a protected application profile or read its contents"
    Write-InstructionFixture "docs/agents/instruction-pack.json" $manifestText

    foreach ($alias in @('docs\agents\fpf-app.md', 'docs//agents/local_instructions.md', 'docs/agents/fpf-app.md.', 'docs/agents/FPF-AP~1.MD')) {
        $modified = $manifestText | ConvertFrom-Json
        $modified.files += [pscustomobject]@{ path = $alias; upgradeMode = "merge" }
        Save-InstructionFixtureManifest $modified
        $failure = Invoke-InstructionValidator 1
        Assert-InstructionFixture ($failure -like "*canonical*" -and $failure -notlike "*not strict UTF-8*") "manifest aliases are rejected before protected content access"
    }
    Write-InstructionFixture "docs/agents/instruction-pack.json" $manifestText

    $outside = Join-Path $fixtureRoot "outside-managed"
    [void][IO.Directory]::CreateDirectory($outside)
    [IO.File]::WriteAllText((Join-Path $outside "escape.ps1"), "SYNTHETIC-PRIVATE-CONTENT", $script:FpfUtf8)
    $redirect = Join-Path $fixtureRoot "docs/agents/tools/redirected"
    [void](New-Item -ItemType Junction -Path $redirect -Target $outside)
    try {
        $modified = $manifestText | ConvertFrom-Json
        $modified.files += [pscustomobject]@{ path = "docs/agents/tools/redirected/escape.ps1"; upgradeMode = "merge" }
        Save-InstructionFixtureManifest $modified
        $failure = Invoke-InstructionValidator 1
        Assert-InstructionFixture ($failure -like "*symlink or junction*" -and $failure -notlike "*SYNTHETIC-PRIVATE-CONTENT*") "managed redirected paths are rejected before content access"
    }
    finally { [IO.Directory]::Delete($redirect, $false) }
    Write-InstructionFixture "docs/agents/instruction-pack.json" $manifestText
    $guidePath = Join-Path $fixtureRoot "docs/agents/workflow.md"
    $guide = [IO.File]::ReadAllText($guidePath)
    Write-InstructionFixture "docs/agents/workflow.md" ($guide + "`nSynthetic-private-marker`n")
    $failure = Invoke-InstructionValidator 1 "Synthetic-private-marker"
    Assert-InstructionFixture ($failure -like "*supplied private identifier*" -and $failure -notlike "*Synthetic-private-marker*") "private-identifier checks accept transient inputs and report paths without echoing values"
    Write-InstructionFixture "docs/agents/workflow.md" $guide
    $null = Invoke-InstructionValidator
    Assert-InstructionFixture $true "restored fixture passes after negative controls"
    $isComplete = $true
}
finally {
    if ($isComplete -and -not $KeepArtifacts) {
        $allowed = Join-Path $repoRoot "artifacts"
        if (-not [IO.Path]::GetFullPath($fixtureRoot).StartsWith($allowed + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe fixture cleanup root." }
        [void]@(Get-FpfTreePaths $fixtureRoot)
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
    else { Write-Output "Fixture evidence retained: $fixtureRoot" }
}
Write-Output "Passed $passed instruction fixture assertions on PowerShell $($PSVersionTable.PSVersion)."
