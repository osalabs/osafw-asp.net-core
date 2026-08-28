[CmdletBinding()]
param(
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Join-Path $PSScriptRoot "..\..\.."
}

$root = (Resolve-Path -LiteralPath $RepoRoot).Path
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$failures = [System.Collections.Generic.List[string]]::new()
$passes = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
    param([string]$Message)
    $failures.Add($Message)
}

function Add-Pass {
    param([string]$Message)
    $passes.Add($Message)
}

function Get-RepoPath {
    param([string]$RelativePath)
    return Join-Path $root ($RelativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
}

function Read-StrictUtf8 {
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    if ($hasBom) {
        throw "UTF-8 BOM detected"
    }
    try {
        return $utf8.GetString($bytes)
    }
    catch [System.Text.DecoderFallbackException] {
        throw "not strict UTF-8"
    }
}

$requiredFiles = @(
    "AGENTS.md",
    "CLAUDE.md",
    "docs/README.md",
    "docs/agents/workflow.md",
    "docs/agents/verification.md",
    "docs/agents/review-routing.md",
    "docs/agents/code_reviewer.md",
    "docs/agents/reviewers/agent-workflow.md",
    "docs/agents/reviewers/consumer-contract.md",
    "docs/agents/reviewers/performance-scale.md",
    "docs/agents/reviewers/security-boundary.md",
    "docs/agents/reviewers/state-integrity.md",
    "docs/agents/domain.md",
    "docs/agents/glossary.md",
    "docs/agents/heuristics.md",
    "docs/agents/mcp.md",
    "docs/agents/tasks/index.md",
    "docs/agents/instruction-pack.json",
    ".codex/agents/discovery_fast.toml",
    ".codex/agents/implementation_fast.toml",
    ".codex/agents/reviewer_high.toml",
    ".codex/agents/architect_max.toml"
)

foreach ($relativePath in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Get-RepoPath $relativePath))) {
        Add-Failure "Required instruction route is missing: $relativePath"
    }
}
if ($failures.Count -eq 0) {
    Add-Pass "Required instruction and routing files exist."
}

$agentsText = Read-StrictUtf8 (Get-RepoPath "AGENTS.md")
$claudeText = Read-StrictUtf8 (Get-RepoPath "CLAUDE.md")
$docsMapText = Read-StrictUtf8 (Get-RepoPath "docs/README.md")
$reviewRoutingText = Read-StrictUtf8 (Get-RepoPath "docs/agents/review-routing.md")

if ($claudeText -ne "@AGENTS.md`r`n") {
    Add-Failure "CLAUDE.md must contain only the @AGENTS.md import."
}
else {
    Add-Pass "CLAUDE.md imports AGENTS.md without duplicate guidance."
}

$rootRoutes = @(
    "docs/agents/workflow.md",
    "docs/agents/verification.md",
    "docs/agents/review-routing.md",
    "docs/agents/code_reviewer.md",
    "docs/agents/mcp.md",
    "docs/agents/tasks/index.md"
)
foreach ($route in $rootRoutes) {
    if ($agentsText.IndexOf($route, [System.StringComparison]::Ordinal) -lt 0) {
        Add-Failure "AGENTS.md does not route to $route."
    }
}

$mapRoutes = @(
    "agents/workflow.md",
    "agents/verification.md",
    "agents/review-routing.md",
    "agents/code_reviewer.md",
    "agents/reviewers/"
)
foreach ($route in $mapRoutes) {
    if ($docsMapText.IndexOf($route, [System.StringComparison]::Ordinal) -lt 0) {
        Add-Failure "docs/README.md does not route to $route."
    }
}

$overlays = @(
    "reviewers/agent-workflow.md",
    "reviewers/consumer-contract.md",
    "reviewers/performance-scale.md",
    "reviewers/security-boundary.md",
    "reviewers/state-integrity.md"
)
foreach ($overlay in $overlays) {
    if ($reviewRoutingText.IndexOf($overlay, [System.StringComparison]::Ordinal) -lt 0) {
        Add-Failure "Review routing does not reference $overlay."
    }
}
if ($failures.Count -eq 0) {
    Add-Pass "Root, documentation-map, and reviewer routes are connected."
}

$roleRoutes = @{
    discovery_fast = @("docs/agents/workflow.md", "docs/prompts/orchestrator.md")
    implementation_fast = @("docs/agents/workflow.md", "docs/prompts/orchestrator.md")
    reviewer_high = @("docs/agents/workflow.md", "docs/agents/review-routing.md", "docs/prompts/orchestrator.md")
    architect_max = @("docs/agents/workflow.md", "docs/agents/review-routing.md", "docs/prompts/orchestrator.md")
}
foreach ($role in $roleRoutes.Keys) {
    foreach ($relativePath in $roleRoutes[$role]) {
        $text = Read-StrictUtf8 (Get-RepoPath $relativePath)
        if ($text.IndexOf($role, [System.StringComparison]::Ordinal) -lt 0) {
            Add-Failure "Durable role route is missing '$role': $relativePath"
        }
    }
}
if (-not ($failures | Where-Object { $_ -like 'Durable role route is missing*' })) {
    Add-Pass "Durable routing names every project custom-agent role and retains local fallback."
}

$textFiles = [System.Collections.Generic.List[string]]::new()
foreach ($relativePath in @("AGENTS.md", "CLAUDE.md", "docs/README.md", "docs/agents/tasks/index.md")) {
    $path = Get-RepoPath $relativePath
    if (Test-Path -LiteralPath $path) {
        $textFiles.Add($path)
    }
}
foreach ($directory in @("docs/agents", "docs/agents/reviewers", "docs/agents/tools", "docs/prompts")) {
    $path = Get-RepoPath $directory
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }
    $recurse = $directory -ne "docs/agents"
    Get-ChildItem -LiteralPath $path -File -Recurse:$recurse |
        Where-Object {
            ($_.Extension -eq ".md" -or $_.Extension -eq ".ps1") -and
            $_.Name -ne "local_instructions.md"
        } |
        ForEach-Object {
            if (-not $textFiles.Contains($_.FullName)) {
                $textFiles.Add($_.FullName)
            }
        }
}
foreach ($directory in @(".codex/agents")) {
    $path = Get-RepoPath $directory
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }
    Get-ChildItem -LiteralPath $path -File -Recurse |
        Where-Object { $_.Extension -in @(".md", ".ps1", ".json", ".toml") } |
        ForEach-Object {
            if (-not $textFiles.Contains($_.FullName)) {
                $textFiles.Add($_.FullName)
            }
        }
}
$instructionPackPath = Get-RepoPath "docs/agents/instruction-pack.json"
if ((Test-Path -LiteralPath $instructionPackPath) -and -not $textFiles.Contains($instructionPackPath)) {
    $textFiles.Add($instructionPackPath)
}

foreach ($path in $textFiles) {
    try {
        $text = Read-StrictUtf8 $path
    }
    catch {
        Add-Failure "${path}: $($_.Exception.Message)"
        continue
    }
    if ([regex]::IsMatch($text, "(?<!`r)`n")) {
        Add-Failure "$path contains LF not preceded by CR."
    }
    if ([regex]::IsMatch($text, "`r(?!`n)")) {
        Add-Failure "$path contains a CR not followed by LF."
    }
}
if (-not ($failures | Where-Object { $_ -match 'UTF-8|BOM|preceded by CR|followed by LF' })) {
    Add-Pass "Active instruction, routing, prompt, and helper text is strict UTF-8 without BOM and CRLF."
}

$profileFiles = [ordered]@{
    ".codex/agents/discovery_fast.toml" = "discovery_fast"
    ".codex/agents/implementation_fast.toml" = "implementation_fast"
    ".codex/agents/reviewer_high.toml" = "reviewer_high"
    ".codex/agents/architect_max.toml" = "architect_max"
}
foreach ($relativePath in $profileFiles.Keys) {
    $text = Read-StrictUtf8 (Get-RepoPath $relativePath)
    foreach ($key in @("name", "description", "model", "model_reasoning_effort", "sandbox_mode", "developer_instructions")) {
        if ($text -notmatch ("(?m)^" + [regex]::Escape($key) + "\s*=")) {
            Add-Failure "Custom-agent profile is missing '$key': $relativePath"
        }
    }
    $nameMatch = [regex]::Match($text, '(?m)^name\s*=\s*"(?<name>[^"]+)"\s*$')
    if (-not $nameMatch.Success -or $nameMatch.Groups['name'].Value -ne $profileFiles[$relativePath]) {
        Add-Failure "Custom-agent profile name does not match its stable role id: $relativePath"
    }
}
$semanticPolicyFiles = @(
    "AGENTS.md",
    "docs/agents/workflow.md",
    "docs/agents/review-routing.md",
    "docs/prompts/orchestrator.md",
    "docs/prompts/agent_upgrade.md"
)
foreach ($relativePath in $semanticPolicyFiles) {
    $text = Read-StrictUtf8 (Get-RepoPath $relativePath)
    if ($text -match '(?i)gpt-5\.|model_reasoning_effort\s*=') {
        Add-Failure "Durable semantic workflow contains a model/reasoning pin: $relativePath"
    }
}
if (Test-Path -LiteralPath (Get-RepoPath ".codex/config.toml")) {
    Add-Failure "Primary/project-wide model configuration is not allowed in .codex/config.toml."
}
if (-not ($failures | Where-Object { $_ -match 'Custom-agent profile|semantic workflow|config\.toml' })) {
    Add-Pass "Custom-agent profiles contain replaceable role settings; durable workflow and the primary task remain unpinned."
}

try {
    $instructionPack = (Read-StrictUtf8 $instructionPackPath) | ConvertFrom-Json
    if ([int]$instructionPack.schemaVersion -ne 1 -or [string]::IsNullOrWhiteSpace([string]$instructionPack.packVersion)) {
        Add-Failure "Instruction-pack metadata has an unsupported schema or missing version."
    }
    foreach ($entry in @($instructionPack.files)) {
        if ([string]::IsNullOrWhiteSpace([string]$entry.path) -or [string]::IsNullOrWhiteSpace([string]$entry.upgradeMode)) {
            Add-Failure "Instruction-pack entry is missing path or upgradeMode."
            continue
        }
        if (-not (Test-Path -LiteralPath (Get-RepoPath ([string]$entry.path)) -PathType Leaf)) {
            Add-Failure "Instruction-pack managed path is missing: $($entry.path)"
        }
    }
}
catch {
    Add-Failure "Instruction-pack metadata is invalid JSON: $($_.Exception.Message)"
}
if (-not ($failures | Where-Object { $_ -match '^Instruction-pack' })) {
    Add-Pass "Versioned instruction-pack metadata resolves every managed file."
}

$policyFiles = @(
    "AGENTS.md",
    "CLAUDE.md",
    "docs/README.md",
    "docs/agents/workflow.md",
    "docs/agents/verification.md",
    "docs/agents/review-routing.md",
    "docs/agents/code_reviewer.md",
    "docs/agents/domain.md",
    "docs/agents/glossary.md",
    "docs/agents/heuristics.md",
    "docs/agents/mcp.md",
    "docs/agents/reviewers/agent-workflow.md",
    "docs/agents/reviewers/consumer-contract.md",
    "docs/agents/reviewers/performance-scale.md",
    "docs/agents/reviewers/security-boundary.md",
    "docs/agents/reviewers/state-integrity.md"
)
foreach ($relativePath in $policyFiles) {
    $text = Read-StrictUtf8 (Get-RepoPath $relativePath)
    if ($text -match '(?i)C:\\Users\\|C:\\DOCS_PROJ\\') {
        Add-Failure "Tracked policy contains a private machine path: $relativePath"
    }
}
if (-not ($failures | Where-Object { $_ -like 'Tracked policy contains a private machine path:*' })) {
    Add-Pass "Tracked shared policy contains no known private machine paths."
}

$linkFiles = $policyFiles | Where-Object { $_ -ne "AGENTS.md" -and $_ -ne "CLAUDE.md" }
foreach ($relativePath in $linkFiles) {
    $filePath = Get-RepoPath $relativePath
    $baseDirectory = [System.IO.Path]::GetDirectoryName($filePath)
    $text = Read-StrictUtf8 $filePath
    foreach ($match in [regex]::Matches($text, '(?<!!)\[[^\]]+\]\((?<target>[^)]+)\)')) {
        $target = $match.Groups['target'].Value.Trim()
        if ($target.StartsWith('<') -and $target.EndsWith('>')) {
            $target = $target.Substring(1, $target.Length - 2)
        }
        if ($target -match '^(?i:https?://|mailto:|#|/)') {
            continue
        }
        $pathPart = ($target -split '#', 2)[0]
        if ([string]::IsNullOrWhiteSpace($pathPart)) {
            continue
        }
        $decoded = [System.Uri]::UnescapeDataString($pathPart)
        $resolvedTarget = [System.IO.Path]::GetFullPath((Join-Path $baseDirectory $decoded))
        if (-not (Test-Path -LiteralPath $resolvedTarget)) {
            Add-Failure "Broken local link in ${relativePath}: $target"
        }
    }
}
if (-not ($failures | Where-Object { $_ -like 'Broken local link*' })) {
    Add-Pass "Local links in shared agent policy resolve."
}

$tasksDirectory = Get-RepoPath "docs/agents/tasks"
$indexText = Read-StrictUtf8 (Join-Path $tasksDirectory "index.md")
$indexMatches = [regex]::Matches($indexText, '`(?<name>summary-[^`/\\]+\.md)`')
$indexNames = @($indexMatches | ForEach-Object { $_.Groups['name'].Value })
$trackedSummaryPaths = @(& git -C $root ls-files -- "docs/agents/tasks/summary-*.md" 2>$null)
if ($LASTEXITCODE -ne 0) {
    Add-Failure "git ls-files failed while validating tracked task-summary index coverage."
    $trackedSummaryPaths = @()
}

foreach ($group in $indexNames | Group-Object) {
    if ($group.Count -ne 1) {
        Add-Failure "Task index references $($group.Name) $($group.Count) times."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $tasksDirectory $group.Name))) {
        Add-Failure "Task index references a missing summary: $($group.Name)"
    }
}
foreach ($summaryPath in $trackedSummaryPaths) {
    $summaryName = [System.IO.Path]::GetFileName($summaryPath)
    $count = @($indexNames | Where-Object { $_ -eq $summaryName }).Count
    if ($count -ne 1) {
        Add-Failure "Tracked task summary must have exactly one index entry: $summaryName (found $count)."
    }
}
if (-not ($failures | Where-Object { $_ -match '^Task index|^Tracked task summary|task-summary index coverage' })) {
    Add-Pass "Task index entries resolve and tracked summaries are indexed exactly once; unrelated untracked summaries and status wording were not interpreted."
}

$trackedUrlFiles = @(& git -C $root ls-files -- "*/url.html" 2>$null)
if ($LASTEXITCODE -ne 0) {
    Add-Failure "git ls-files failed while validating ParsePage url.html literals."
}
else {
    foreach ($relativePath in $trackedUrlFiles) {
        $bytes = [System.IO.File]::ReadAllBytes((Get-RepoPath $relativePath))
        if ($bytes -contains 0x0A -or $bytes -contains 0x0D) {
            Add-Failure "ParsePage route literal contains a newline byte: $relativePath"
        }
    }
}
if (-not ($failures | Where-Object { $_ -match 'url\.html|git ls-files' })) {
    Add-Pass "Tracked ParsePage url.html route literals are single-line with no newline byte."
}

foreach ($message in $passes) {
    Write-Host "PASS: $message"
}
foreach ($message in $failures) {
    Write-Host "FAIL: $message" -ForegroundColor Red
}

if ($failures.Count -gt 0) {
    exit 1
}

Write-Host "Agent instruction validation passed."
