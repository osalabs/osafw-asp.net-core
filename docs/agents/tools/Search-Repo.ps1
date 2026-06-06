[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Pattern,

    [Parameter(Position = 1)]
    [string]$Path = ".",

    [switch]$IncludeDrafts,

    [switch]$IncludeTaskHistory
)

$rg = Get-Command rg -ErrorAction SilentlyContinue
if ($null -eq $rg) {
    throw "Search-Repo.ps1 requires ripgrep (`rg`) on PATH."
}

$args = @(
    "--line-number",
    "--hidden",
    "--no-ignore",
    "--glob", "!.git/**",
    "--glob", "!.vs/**",
    "--glob", "!codex-security-scans/**",
    "--glob", "!artifacts/**",
    "--glob", "!docs/agents/artifacts/**",
    "--glob", "!osafw-app/App_Data/db/**",
    "--glob", "!osafw-app/upload/**",
    "--glob", "!**/bin/**",
    "--glob", "!**/obj/**",
    "--glob", "!*.log"
)

if (-not $IncludeDrafts) {
    $args += @("--glob", "!docs/drafts/**")
}

if (-not $IncludeTaskHistory) {
    $args += @("--glob", "!docs/agents/tasks/**")
}

$args += @("--", $Pattern, $Path)

& $rg.Source @args
exit $LASTEXITCODE
