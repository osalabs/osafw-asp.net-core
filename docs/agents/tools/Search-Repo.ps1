$Pattern = $null
$Path = New-Object System.Collections.Generic.List[string]
$IncludeDrafts = $false
$IncludeTaskHistory = $false
$IncludeVendor = $false

function Test-SearchRepoSwitch {
    param([string]$Value)

    $known = @(
        "-Pattern",
        "-Path",
        "-IncludeDrafts",
        "-IncludeTaskHistory",
        "-IncludeVendor"
    )

    return $known -contains $Value
}

$tokens = @($args)
for ($i = 0; $i -lt $tokens.Count; $i++) {
    $token = $tokens[$i]

    switch ($token) {
        "-Pattern" {
            if ($i + 1 -ge $tokens.Count) {
                throw "Missing value after -Pattern."
            }
            $i++
            $Pattern = $tokens[$i]
            continue
        }
        "-Path" {
            while ($i + 1 -lt $tokens.Count -and -not (Test-SearchRepoSwitch $tokens[$i + 1])) {
                $i++
                $Path.Add($tokens[$i])
            }
            continue
        }
        "-IncludeDrafts" {
            $IncludeDrafts = $true
            continue
        }
        "-IncludeTaskHistory" {
            $IncludeTaskHistory = $true
            continue
        }
        "-IncludeVendor" {
            $IncludeVendor = $true
            continue
        }
        default {
            if ($null -eq $Pattern) {
                $Pattern = $token
            }
            else {
                $Path.Add($token)
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($Pattern)) {
    throw "Usage: Search-Repo.ps1 -Pattern <regex> [-Path <path> [more paths...]] [-IncludeDrafts] [-IncludeTaskHistory] [-IncludeVendor]"
}

$rg = Get-Command rg -ErrorAction SilentlyContinue
if ($null -eq $rg) {
    throw "Search-Repo.ps1 requires ripgrep (`rg`) on PATH."
}

$args = @(
    "--line-number",
    "--hidden",
    "--no-ignore",
    "--max-columns", "500",
    "--max-columns-preview",
    "--glob", "!.git/**",
    "--glob", "!.vs/**",
    "--glob", "!codex-security-scans/**",
    "--glob", "!artifacts/**",
    "--glob", "!docs/agents/artifacts/**",
    "--glob", "!docs/agents/tmp/**",
    "--glob", "!test-results/**",
    "--glob", "!osafw-app/App_Data/db/**",
    "--glob", "!osafw-app/App_Data/logs/**",
    "--glob", "!osafw-app/upload/**",
    "--glob", "!**/bin/**",
    "--glob", "!**/obj/**",
    "--glob", "!*.code-workspace",
    "--glob", "!**/*.code-workspace",
    "--glob", "!*.sublime-project",
    "--glob", "!**/*.sublime-project",
    "--glob", "!*.sublime-workspace",
    "--glob", "!**/*.sublime-workspace",
    "--glob", "!*.log",
    "--glob", "!**/*.log"
)

if (-not $IncludeDrafts) {
    $args += @("--glob", "!docs/drafts/**")
}

if (-not $IncludeTaskHistory) {
    $args += @("--glob", "!docs/agents/tasks/**")
}

if (-not $IncludeVendor) {
    $args += @(
        "--glob", "!osafw-app/wwwroot/assets/lib/**",
        "--glob", "!**/node_modules/**",
        "--glob", "!**/*.min.js",
        "--glob", "!**/*.min.css",
        "--glob", "!**/*.map"
    )
}

$searchPaths = foreach ($entry in $Path) {
    $entry -split "," | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_.Trim() }
}

if ($null -eq $searchPaths -or $searchPaths.Count -eq 0) {
    $searchPaths = @(".")
}

$args += @("--", $Pattern)
$args += $searchPaths

& $rg.Source @args
exit $LASTEXITCODE
