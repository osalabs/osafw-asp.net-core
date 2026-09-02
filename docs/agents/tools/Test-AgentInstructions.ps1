[CmdletBinding()]
param(
    [string]$RepoRoot,

    [string[]]$PrivateIdentifier = @()
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

function Remove-TomlLineComment {
    param([string]$Line)

    $inBasicString = $false
    $inLiteralString = $false
    $escaped = $false

    for ($i = 0; $i -lt $Line.Length; $i++) {
        $character = $Line[$i]
        if ($inBasicString) {
            if ($escaped) {
                $escaped = $false
                continue
            }
            if ($character -eq '\') {
                $escaped = $true
                continue
            }
            if ($character -eq '"') {
                $inBasicString = $false
            }
            continue
        }
        if ($inLiteralString) {
            if ($character -eq "'") {
                $inLiteralString = $false
            }
            continue
        }
        if ($character -eq '#') {
            return $Line.Substring(0, $i)
        }
        if ($character -eq '"') {
            $inBasicString = $true
        }
        elseif ($character -eq "'") {
            $inLiteralString = $true
        }
    }

    return $Line
}

function Get-TomlSimpleKeyName {
    param([string]$KeyText)

    $key = $KeyText.Trim()
    if ($key.Length -ge 2 -and $key[0] -eq '"' -and $key[$key.Length - 1] -eq '"') {
        $value = $key.Substring(1, $key.Length - 2)
        $builder = [System.Text.StringBuilder]::new()
        for ($i = 0; $i -lt $value.Length; $i++) {
            $character = $value[$i]
            if ($character -ne '\') {
                [void]$builder.Append($character)
                continue
            }
            if ($i + 1 -ge $value.Length) {
                throw "Invalid TOML basic-key escape."
            }
            $i++
            $escape = $value[$i]
            switch -CaseSensitive ($escape) {
                'b' { [void]$builder.Append([char]0x0008) }
                't' { [void]$builder.Append([char]0x0009) }
                'n' { [void]$builder.Append([char]0x000A) }
                'f' { [void]$builder.Append([char]0x000C) }
                'r' { [void]$builder.Append([char]0x000D) }
                '"' { [void]$builder.Append('"') }
                '\' { [void]$builder.Append('\') }
                'u' {
                    if ($i + 4 -ge $value.Length) {
                        throw "Invalid TOML Unicode key escape."
                    }
                    $hex = $value.Substring($i + 1, 4)
                    if ($hex -notmatch '^[0-9A-Fa-f]{4}$') {
                        throw "Invalid TOML Unicode key escape."
                    }
                    [void]$builder.Append([char]::ConvertFromUtf32([Convert]::ToInt32($hex, 16)))
                    $i += 4
                }
                'U' {
                    if ($i + 8 -ge $value.Length) {
                        throw "Invalid TOML Unicode key escape."
                    }
                    $hex = $value.Substring($i + 1, 8)
                    if ($hex -notmatch '^[0-9A-Fa-f]{8}$') {
                        throw "Invalid TOML Unicode key escape."
                    }
                    [void]$builder.Append([char]::ConvertFromUtf32([Convert]::ToInt32($hex, 16)))
                    $i += 8
                }
                default { throw "Invalid TOML basic-key escape." }
            }
        }
        $key = $builder.ToString()
    }
    elseif ($key.Length -ge 2 -and $key[0] -eq "'" -and $key[$key.Length - 1] -eq "'") {
        $key = $key.Substring(1, $key.Length - 2)
    }
    return $key
}

function Get-TomlInlineTableEntries {
    param([string]$Value)

    $entries = [System.Collections.Generic.List[string]]::new()
    $entryStart = 1
    $braceDepth = 1
    $bracketDepth = 0
    $inBasicString = $false
    $inLiteralString = $false
    $escaped = $false

    for ($i = 1; $i -lt $Value.Length; $i++) {
        $character = $Value[$i]
        if ($inBasicString) {
            if ($escaped) {
                $escaped = $false
                continue
            }
            if ($character -eq '\') {
                $escaped = $true
                continue
            }
            if ($character -eq '"') {
                $inBasicString = $false
            }
            continue
        }
        if ($inLiteralString) {
            if ($character -eq "'") {
                $inLiteralString = $false
            }
            continue
        }

        switch ($character) {
            '"' { $inBasicString = $true }
            "'" { $inLiteralString = $true }
            '{' { $braceDepth++ }
            '}' {
                $braceDepth--
                if ($braceDepth -eq 0) {
                    $entries.Add($Value.Substring($entryStart, $i - $entryStart))
                    break
                }
            }
            '[' { $bracketDepth++ }
            ']' { $bracketDepth-- }
            ',' {
                if ($braceDepth -eq 1 -and $bracketDepth -eq 0) {
                    $entries.Add($Value.Substring($entryStart, $i - $entryStart))
                    $entryStart = $i + 1
                }
            }
        }

        if ($braceDepth -eq 0) {
            break
        }
    }

    return @($entries)
}

function Test-TomlHasUnescapedBasicStringDelimiter {
    param([string]$Text)

    $escaped = $false
    for ($i = 0; $i -lt $Text.Length; $i++) {
        $character = $Text[$i]
        if ($escaped) {
            $escaped = $false
            continue
        }
        if ($character -eq '\') {
            $escaped = $true
            continue
        }
        if (
            $character -eq '"' -and
            $i + 2 -lt $Text.Length -and
            $Text[$i + 1] -eq '"' -and
            $Text[$i + 2] -eq '"'
        ) {
            return $true
        }
    }

    return $false
}

function Get-DisallowedCodexProjectConfigPins {
    param([string]$Text)

    $pins = [System.Collections.Generic.List[string]]::new()
    $section = "root"
    $multilineStringDelimiter = $null
    $simpleKeyPattern = '(?:"(?:\\.|[^"])*"|''[^'']*''|[A-Za-z0-9_-]+)'

    foreach ($rawLine in [regex]::Split($Text, "`r`n|`n|`r")) {
        if ($null -ne $multilineStringDelimiter) {
            $hasClosingDelimiter = if ($multilineStringDelimiter -eq '"""') {
                Test-TomlHasUnescapedBasicStringDelimiter $rawLine
            }
            else {
                $rawLine.IndexOf($multilineStringDelimiter, [System.StringComparison]::Ordinal) -ge 0
            }
            if ($hasClosingDelimiter) {
                $multilineStringDelimiter = $null
            }
            continue
        }

        $code = Remove-TomlLineComment $rawLine
        $trimmed = $code.Trim()
        if ($trimmed.Length -eq 0) {
            continue
        }
        $tableHeader = [regex]::Match($trimmed, "^\[\s*(?<table>$simpleKeyPattern)\s*\]\s*$")
        if ($tableHeader.Success) {
            $tableName = Get-TomlSimpleKeyName $tableHeader.Groups['table'].Value
            $section = if ($tableName -ceq "agents") { "agents" } else { "other" }
            continue
        }
        if ($trimmed -match '^\[(?:\[[^\]]+\]\]|[^\]]+\])\s*$') {
            $section = "other"
            continue
        }

        $dottedAssignment = [regex]::Match(
            $code,
            "^\s*(?<table>$simpleKeyPattern)\s*\.\s*(?<key>$simpleKeyPattern)\s*="
        )
        if ($section -eq "root" -and $dottedAssignment.Success) {
            $tableName = Get-TomlSimpleKeyName $dottedAssignment.Groups['table'].Value
            $keyName = Get-TomlSimpleKeyName $dottedAssignment.Groups['key'].Value
            if ($tableName -ceq "agents" -and @("default_subagent_model", "default_subagent_reasoning_effort") -ccontains $keyName) {
                $pins.Add("agents.$keyName")
            }
        }

        $assignment = [regex]::Match($code, "^\s*(?<key>$simpleKeyPattern)\s*=")
        $value = $null
        if ($dottedAssignment.Success -or $assignment.Success) {
            $equalsIndex = $code.IndexOf('=')
            $value = $code.Substring($equalsIndex + 1).TrimStart()
        }
        if ($assignment.Success) {
            $keyName = Get-TomlSimpleKeyName $assignment.Groups['key'].Value
            if ($section -eq "root" -and @("model", "model_reasoning_effort") -ccontains $keyName) {
                $pins.Add($keyName)
            }
            elseif ($section -eq "agents" -and @("default_subagent_model", "default_subagent_reasoning_effort") -ccontains $keyName) {
                $pins.Add("agents.$keyName")
            }

            if ($section -eq "root" -and $keyName -ceq "agents" -and $value.StartsWith("{")) {
                foreach ($inlineEntry in @(Get-TomlInlineTableEntries $value)) {
                    $inlineKeyMatch = [regex]::Match($inlineEntry, "^\s*(?<key>$simpleKeyPattern)\s*=")
                    if (-not $inlineKeyMatch.Success) {
                        continue
                    }
                    $inlineKeyName = Get-TomlSimpleKeyName $inlineKeyMatch.Groups['key'].Value
                    if (@("default_subagent_model", "default_subagent_reasoning_effort") -ccontains $inlineKeyName) {
                        $pins.Add("agents.$inlineKeyName")
                    }
                }
            }

        }
        if ($null -ne $value) {
            foreach ($delimiter in @('"""', "'''")) {
                if ($value.StartsWith($delimiter)) {
                    $remainder = $value.Substring($delimiter.Length)
                    $hasClosingDelimiter = if ($delimiter -eq '"""') {
                        Test-TomlHasUnescapedBasicStringDelimiter $remainder
                    }
                    else {
                        $remainder.IndexOf($delimiter, [System.StringComparison]::Ordinal) -ge 0
                    }
                    if (-not $hasClosingDelimiter) {
                        $multilineStringDelimiter = $delimiter
                    }
                    break
                }
            }
        }
    }

    return @($pins | Select-Object -Unique)
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
    "docs/agents/tools/Normalize-TextFiles.ps1",
    "docs/agents/tools/Search-Repo.ps1",
    "docs/agents/tools/Test-AgentInstructions.ps1",
    ".codex/agents/discovery_fast.toml",
    ".codex/agents/implementation_fast.toml",
    ".codex/agents/implementation_max.toml",
    ".codex/agents/reviewer_high.toml",
    ".codex/agents/reviewer_max.toml",
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
    implementation_max = @("docs/agents/workflow.md", "docs/prompts/orchestrator.md")
    reviewer_high = @("docs/agents/workflow.md", "docs/agents/review-routing.md", "docs/prompts/orchestrator.md")
    reviewer_max = @("docs/agents/workflow.md", "docs/agents/review-routing.md", "docs/prompts/orchestrator.md")
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
$codexProjectConfigPath = Get-RepoPath ".codex/config.toml"
if ((Test-Path -LiteralPath $codexProjectConfigPath) -and -not $textFiles.Contains($codexProjectConfigPath)) {
    $textFiles.Add($codexProjectConfigPath)
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
    ".codex/agents/implementation_max.toml" = "implementation_max"
    ".codex/agents/reviewer_high.toml" = "reviewer_high"
    ".codex/agents/reviewer_max.toml" = "reviewer_max"
    ".codex/agents/architect_max.toml" = "architect_max"
}
$profileSandboxModes = @{
    discovery_fast = "read-only"
    implementation_fast = "workspace-write"
    implementation_max = "workspace-write"
    reviewer_high = "read-only"
    reviewer_max = "read-only"
    architect_max = "read-only"
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
    $sandboxMatch = [regex]::Match($text, '(?m)^sandbox_mode\s*=\s*"(?<mode>[^"]+)"\s*$')
    $roleName = $profileFiles[$relativePath]
    if (-not $sandboxMatch.Success -or $sandboxMatch.Groups['mode'].Value -ne $profileSandboxModes[$roleName]) {
        Add-Failure "Custom-agent profile sandbox does not match its stable role boundary: $relativePath"
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

$configPolicyCases = @(
    [pscustomobject]@{
        Name = "model-neutral agents table"
        Text = "[agents]`r`nmax_concurrent_threads_per_session = 8`r`ninterrupt_message = true`r`n"
        ExpectedPins = @()
    },
    [pscustomobject]@{
        Name = "model-neutral dotted agents key"
        Text = "agents.max_concurrent_threads_per_session = 6`r`n"
        ExpectedPins = @()
    },
    [pscustomobject]@{
        Name = "comments and multiline values"
        Text = "# model = `"ignored`"`r`nnotes = `"`"`"`r`nmodel = `"also ignored`"`r`n`"`"`"`r`n"
        ExpectedPins = @()
    },
    [pscustomobject]@{
        Name = "escaped multiline basic-string delimiter"
        Text = 'developer_instructions = """' + "`r`n" + 'example = \"""' + "`r`n" + 'model = "inside instructions"' + "`r`n" + '"""' + "`r`n"
        ExpectedPins = @()
    },
    [pscustomobject]@{
        Name = "dotted multiline value then primary pin"
        Text = "metadata.note = `"`"`"`r`nmodel = `"ignored`"`r`n`"`"`"`r`nmodel = `"pinned`"`r`n"
        ExpectedPins = @("model")
    },
    [pscustomobject]@{
        Name = "primary model pin"
        Text = "model = `"pinned`"`r`n"
        ExpectedPins = @("model")
    },
    [pscustomobject]@{
        Name = "quoted primary reasoning pin"
        Text = "`"model_reasoning_effort`" = `"high`"`r`n"
        ExpectedPins = @("model_reasoning_effort")
    },
    [pscustomobject]@{
        Name = "agents table model default"
        Text = "[agents]`r`ndefault_subagent_model = `"pinned`"`r`n"
        ExpectedPins = @("agents.default_subagent_model")
    },
    [pscustomobject]@{
        Name = "dotted agents reasoning default"
        Text = "agents.default_subagent_reasoning_effort = `"high`"`r`n"
        ExpectedPins = @("agents.default_subagent_reasoning_effort")
    },
    [pscustomobject]@{
        Name = "inline agents model default"
        Text = "agents = { max_concurrent_threads_per_session = 4, default_subagent_model = `"pinned`" }`r`n"
        ExpectedPins = @("agents.default_subagent_model")
    },
    [pscustomobject]@{
        Name = "inline text and nested values mentioning defaults"
        Text = "agents = { note = `", default_subagent_model = text only`", values = [{ default_subagent_reasoning_effort = `"nested`" }], max_concurrent_threads_per_session = 4 }`r`n"
        ExpectedPins = @()
    },
    [pscustomobject]@{
        Name = "case-sensitive unrelated keys"
        Text = "Model = `"not a Codex model key`"`r`nAgents.default_subagent_model = `"not an agents key`"`r`n"
        ExpectedPins = @()
    },
    [pscustomobject]@{
        Name = "escaped primary model pin"
        Text = "`"mo\u0064el`" = `"pinned`"`r`n"
        ExpectedPins = @("model")
    },
    [pscustomobject]@{
        Name = "escaped agents table model default"
        Text = "[`"a\u0067ents`"]`r`ndefault_subagent_model = `"pinned`"`r`n"
        ExpectedPins = @("agents.default_subagent_model")
    },
    [pscustomobject]@{
        Name = "escaped dotted agents reasoning default"
        Text = "`"a\u0067ents`".default_subagent_reasoning_effort = `"high`"`r`n"
        ExpectedPins = @("agents.default_subagent_reasoning_effort")
    },
    [pscustomobject]@{
        Name = "escaped inline agents model default"
        Text = "agents = { `"default_subagent_\u006dodel`" = `"pinned`" }`r`n"
        ExpectedPins = @("agents.default_subagent_model")
    }
)
foreach ($case in $configPolicyCases) {
    $actualSignature = @(Get-DisallowedCodexProjectConfigPins $case.Text | Sort-Object) -join "|"
    $expectedSignature = @($case.ExpectedPins | Sort-Object) -join "|"
    if ($actualSignature -ne $expectedSignature) {
        Add-Failure "Codex project config policy self-test failed '$($case.Name)': expected '$expectedSignature', got '$actualSignature'."
    }
}
if (-not ($failures | Where-Object { $_ -like 'Codex project config policy self-test*' })) {
    Add-Pass "Codex project config policy accepts model-neutral controls and rejects primary or project-wide agent model pins."
}

if (Test-Path -LiteralPath $codexProjectConfigPath) {
    $projectConfigText = Read-StrictUtf8 $codexProjectConfigPath
    foreach ($pin in @(Get-DisallowedCodexProjectConfigPins $projectConfigText)) {
        Add-Failure "Codex project config contains a disallowed model/reasoning pin: $pin"
    }
}
if (-not ($failures | Where-Object { $_ -match 'Custom-agent profile|semantic workflow|Codex project config' })) {
    Add-Pass "Custom-agent profiles contain replaceable role settings; durable workflow and project configuration remain primary-model-neutral."
}

$supplementalSummaryAuditFiles = @(
    "docs/agents/code_reviewer.md",
    "docs/agents/review-routing.md",
    ".codex/agents/reviewer_high.toml",
    ".codex/agents/reviewer_max.toml"
)
foreach ($relativePath in $supplementalSummaryAuditFiles) {
    $text = Read-StrictUtf8 (Get-RepoPath $relativePath)
    if ($text -notmatch '(?i)initial verdict' -or $text -notmatch '(?i)changed active summar') {
        Add-Failure "Review policy does not require a post-verdict audit of changed active summaries: $relativePath"
    }
}
if (-not ($failures | Where-Object { $_ -like 'Review policy does not require*' })) {
    Add-Pass "Independent-review policy requires changed active summaries to be audited after the initial verdict."
}

$instructionPack = $null
try {
    $instructionPack = (Read-StrictUtf8 $instructionPackPath) | ConvertFrom-Json
    if ([int]$instructionPack.schemaVersion -ne 1 -or [string]::IsNullOrWhiteSpace([string]$instructionPack.packVersion) -or @($instructionPack.files).Count -eq 0) {
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
    $managedPaths = @($instructionPack.files | ForEach-Object { [string]$_.path })
    foreach ($requiredManagedPath in @(
        "docs/agents/tools/Normalize-TextFiles.ps1",
        "docs/agents/tools/Search-Repo.ps1",
        "docs/agents/tools/Test-AgentInstructions.ps1"
    )) {
        if ($managedPaths -notcontains $requiredManagedPath) {
            Add-Failure "Instruction-pack metadata does not manage required helper: $requiredManagedPath"
        }
    }
    $validationCommands = @($instructionPack.validation | ForEach-Object { [string]$_ })
    foreach ($expectedCommand in @(
        "pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1",
        "pwsh -NoProfile -File docs/agents/tools/Normalize-TextFiles.ps1 -Check <changed-pack-files>",
        "git diff --check"
    )) {
        if ($validationCommands -notcontains $expectedCommand) {
            Add-Failure "Instruction-pack validation command is missing: $expectedCommand"
        }
    }
    $windowsPowerShell51Fallback = @($instructionPack.windowsPowerShell51Fallback | ForEach-Object { [string]$_ })
    foreach ($expectedCommand in @(
        "powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1",
        "powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Normalize-TextFiles.ps1 -Check <changed-pack-files>"
    )) {
        if ($windowsPowerShell51Fallback -notcontains $expectedCommand) {
            Add-Failure "Instruction-pack Windows PowerShell 5.1 fallback is missing: $expectedCommand"
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
$managedPortableFiles = @()
if ($null -ne $instructionPack) {
    $managedPortableFiles = @(
        $instructionPack.files |
            ForEach-Object { [string]$_.path } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
}
$portableSharedFiles = @($policyFiles + $managedPortableFiles | Select-Object -Unique)
$privateIdentifiers = @($PrivateIdentifier | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
foreach ($relativePath in $portableSharedFiles) {
    $text = Read-StrictUtf8 (Get-RepoPath $relativePath)
    if ($text -match '(?i)C:\\Users\\|C:\\DOCS_PROJ\\') {
        Add-Failure "Portable shared file contains a private machine path: $relativePath"
    }
    foreach ($identifier in $privateIdentifiers) {
        if ($text.IndexOf($identifier, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Add-Failure "Portable shared file contains a supplied private identifier: $relativePath"
        }
    }
}
if ($null -ne $instructionPack -and -not ($failures | Where-Object { $_ -match '^Portable shared file contains .*private' })) {
    Add-Pass "Portable shared policy files, including every instruction-pack managed file, contain no private machine paths or supplied private identifiers."
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
