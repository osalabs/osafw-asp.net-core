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

function Get-OptInSearchPaths {
    param(
        [string]$OptInRoot,
        [string[]]$RequestedPaths
    )

    $resolvedRoot = Resolve-Path -LiteralPath $OptInRoot -ErrorAction SilentlyContinue
    if ($null -eq $resolvedRoot) {
        return @()
    }

    $rootPath = $resolvedRoot.Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $rootPrefix = $rootPath + [System.IO.Path]::DirectorySeparatorChar
    $result = foreach ($requestedPath in $RequestedPaths) {
        $resolvedRequestedPaths = Resolve-Path -LiteralPath $requestedPath -ErrorAction SilentlyContinue
        foreach ($resolvedRequested in $resolvedRequestedPaths) {
            $requestedFullPath = $resolvedRequested.Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
            $requestedPrefix = $requestedFullPath + [System.IO.Path]::DirectorySeparatorChar

            if ($requestedFullPath.Equals($rootPath, [System.StringComparison]::OrdinalIgnoreCase) -or
                $requestedFullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $requestedFullPath
            }
            elseif ($rootPath.StartsWith($requestedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $rootPath
            }
        }
    }

    return @($result | Sort-Object -Unique)
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

$commonRgArgs = @(
    "--line-number",
    "--hidden",
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

$primaryArgs = @($commonRgArgs)
$primaryArgs += @("--glob", "!docs/drafts/**")

if (-not $IncludeTaskHistory) {
    $primaryArgs += @("--glob", "!docs/agents/tasks/**")
}

$primaryArgs += @(
    "--glob", "!osafw-app/wwwroot/assets/lib/**",
    "--glob", "!**/node_modules/**",
    "--glob", "!**/*.min.js",
    "--glob", "!**/*.min.css",
    "--glob", "!**/*.map"
)

$searchPaths = foreach ($entry in $Path) {
    $entry -split "," | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_.Trim() }
}

if ($null -eq $searchPaths -or $searchPaths.Count -eq 0) {
    $searchPaths = @(".")
}

$primaryArgs += @("--", $Pattern)
$primaryArgs += $searchPaths

& $rg.Source @primaryArgs
$primaryExitCode = $LASTEXITCODE
if ($primaryExitCode -gt 1) {
    exit $primaryExitCode
}

$hasMatch = $primaryExitCode -eq 0

$draftSearchPaths = @(Get-OptInSearchPaths -OptInRoot "docs/drafts" -RequestedPaths $searchPaths)
if ($IncludeDrafts -and $draftSearchPaths.Count -gt 0) {
    # Search only the explicitly opted-in ignored draft tree. The primary search
    # still respects VCS/global ignores, so machine-local ignored files elsewhere
    # in the repository are not exposed by broad searches.
    $draftArgs = @($commonRgArgs)
    $draftArgs += @("--no-ignore", "--", $Pattern)
    $draftArgs += $draftSearchPaths

    & $rg.Source @draftArgs
    $draftExitCode = $LASTEXITCODE
    if ($draftExitCode -gt 1) {
        exit $draftExitCode
    }
    $hasMatch = $hasMatch -or $draftExitCode -eq 0
}

$vendorSearchPaths = @(Get-OptInSearchPaths -OptInRoot "osafw-app/wwwroot/assets/lib" -RequestedPaths $searchPaths)
if ($IncludeVendor -and $vendorSearchPaths.Count -gt 0) {
    # Vendor search is likewise limited to the explicitly opted-in vendor tree;
    # other ignored dependency or machine-local paths remain excluded.
    $vendorArgs = @($commonRgArgs)
    $vendorArgs += @("--no-ignore", "--", $Pattern)
    $vendorArgs += $vendorSearchPaths

    & $rg.Source @vendorArgs
    $vendorExitCode = $LASTEXITCODE
    if ($vendorExitCode -gt 1) {
        exit $vendorExitCode
    }
    $hasMatch = $hasMatch -or $vendorExitCode -eq 0
}

if ($hasMatch) {
    exit 0
}
exit 1
