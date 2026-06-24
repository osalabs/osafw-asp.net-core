#Requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Check,
    [switch]$Force,
    [switch]$Pause,
    [switch]$Help
)

# Shared deploy engine for repo-contained deploy profile scripts.
# Run one of the profile scripts instead of invoking this file directly.
# This script intentionally does not run "git clean"; git reset --hard preserves untracked runtime files.

$ErrorActionPreference = "Stop"

function ConvertTo-DeploySlug {
    param([Parameter(Mandatory = $true)][string]$Value)

    $slug = $Value.ToLowerInvariant() -replace "[^a-z0-9]+", "-"
    return $slug.Trim("-")
}

function Get-DeployVariable {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [object]$DefaultValue = $null
    )

    $variable = Get-Variable -Name $Name -Scope Script -ErrorAction SilentlyContinue
    if ($null -eq $variable) {
        return $DefaultValue
    }
    if ($null -eq $variable.Value) {
        return $DefaultValue
    }
    if (($variable.Value -is [string]) -and [string]::IsNullOrWhiteSpace($variable.Value)) {
        return $DefaultValue
    }

    return $variable.Value
}

function Get-FullDeployPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path).TrimEnd("\")
}

function Join-FullDeployPath {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$ChildPath
    )

    if ([System.IO.Path]::IsPathRooted($ChildPath)) {
        return Get-FullDeployPath $ChildPath
    }

    return Get-FullDeployPath (Join-Path $BasePath $ChildPath)
}

function Test-DriveRootPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $root = [System.IO.Path]::GetPathRoot($Path).TrimEnd("\")
    return $Path.TrimEnd("\").Equals($root, [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-PathInsideOrEqual {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ParentPath
    )

    $normalizedPath = (Get-FullDeployPath $Path) + "\"
    $normalizedParent = (Get-FullDeployPath $ParentPath) + "\"
    return $normalizedPath.StartsWith($normalizedParent, [System.StringComparison]::OrdinalIgnoreCase)
}

function ConvertTo-RepoRelativePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = Get-FullDeployPath $Path
    $repoRootWithSlash = (Get-FullDeployPath $script:RepoRoot) + "\"
    if (-not ($fullPath + "\").StartsWith($repoRootWithSlash, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path must be under repo root: $fullPath"
    }

    return $fullPath.Substring($repoRootWithSlash.Length)
}

function ConvertTo-NotificationPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    $fullPath = Get-FullDeployPath $Path
    $repoRootWithSlash = (Get-FullDeployPath $script:RepoRoot) + "\"
    if (($fullPath + "\").StartsWith($repoRootWithSlash, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($repoRootWithSlash.Length)
    }

    return Split-Path -Leaf $fullPath
}

function Get-InferredEnvironmentName {
    param([string]$ScriptName)

    if ($ScriptName -match "production") {
        return "Production"
    }
    if ($ScriptName -match "staging") {
        return "Staging"
    }
    if ($ScriptName -match "develop") {
        return "Develop"
    }

    return "Develop"
}

function Get-InferredGitBranch {
    param(
        [string]$ScriptName,
        [string]$EnvironmentName
    )

    if ($ScriptName -match "production" -or $EnvironmentName -eq "Production") {
        return "master"
    }
    if ($ScriptName -match "staging" -or $EnvironmentName -eq "Staging") {
        return "staging"
    }
    if ($ScriptName -match "develop" -or $EnvironmentName -eq "Develop") {
        return "develop"
    }

    return ConvertTo-DeploySlug $EnvironmentName
}

function Initialize-DeploySettings {
    $script:CoreScriptPath = Get-FullDeployPath $PSCommandPath
    $script:ScriptsFolder = Split-Path -Parent $script:CoreScriptPath
    $script:RepoRoot = Get-FullDeployPath (Join-Path $script:ScriptsFolder "..")
    $script:AppRoot = Join-FullDeployPath $script:RepoRoot "osafw-app"

    $script:DeployScriptName = Get-DeployVariable "DeployScriptName" ([System.IO.Path]::GetFileNameWithoutExtension($script:CoreScriptPath))
    $script:EnvironmentName = Get-DeployVariable "EnvironmentName" (Get-InferredEnvironmentName $script:DeployScriptName)
    $script:GitRemote = Get-DeployVariable "GitRemote" "origin"
    $script:GitBranch = Get-DeployVariable "GitBranch" (Get-InferredGitBranch $script:DeployScriptName $script:EnvironmentName)
    $script:DeployName = Get-DeployVariable "DeployName" ("deploy-" + (ConvertTo-DeploySlug $script:EnvironmentName))
    $script:AppPoolName = Get-DeployVariable "AppPoolName" $script:DeployName

    $configuredProjectFile = Get-DeployVariable "ProjectFile" "osafw-app\osafw-app.csproj"
    $script:ProjectFile = Join-FullDeployPath $script:RepoRoot $configuredProjectFile
    $script:ProjectFileRelative = ConvertTo-RepoRelativePath $script:ProjectFile

    $script:TargetFolder = Join-FullDeployPath $script:RepoRoot (Get-DeployVariable "TargetFolder" "osafw-app\bin\Release\net10.0\publish")
    $script:LogFolder = Join-FullDeployPath $script:RepoRoot (Get-DeployVariable "LogFolder" "osafw-app\App_Data\logs")
    $script:StateFolder = $script:LogFolder

    $script:LogMaxBytes = [long](Get-DeployVariable "LogMaxBytes" 5242880)
    $script:LockStaleMinutes = [int](Get-DeployVariable "LockStaleMinutes" 60)
    $script:NotifyWebhook = Get-DeployVariable "NotifyWebhook" ""

    $tempRoot = Get-DeployVariable "TempRoot" $env:TEMP
    if ([string]::IsNullOrWhiteSpace($tempRoot)) {
        $tempRoot = [System.IO.Path]::GetTempPath()
    }
    $script:BuildWorktree = Join-FullDeployPath $tempRoot (Get-DeployVariable "BuildWorktreeName" "$script:DeployName-worktree")
    $script:PublishFolder = Join-FullDeployPath $tempRoot (Get-DeployVariable "PublishFolderName" "$script:DeployName-publish")

    $script:OfflineSource = Get-DeployVariable "OfflineSource" "wwwroot\offline.htm"
    $script:RobocopyExtraArgs = @(Get-DeployVariable "RobocopyExtraArgs" @("/R:3", "/W:3", "/NFL", "/NDL", "/NP"))
    $script:RobocopyExcludeDirs = @(Get-DeployVariable "RobocopyExcludeDirs" @("App_Data\db", "App_Data\logs", "upload", "wwwroot\upload"))

    $script:LogFile = Join-Path $script:LogFolder "$script:DeployName.log"
    $script:StatusFile = Join-Path $script:LogFolder "$script:DeployName.status.json"
    $script:StateFile = Join-Path $script:LogFolder "$script:DeployName.last_successful_commit"
    $script:LockDir = Join-Path $script:LogFolder "$script:DeployName.lock"

    $script:ExitCode = 0
    $script:ErrorMessage = ""
    $script:LockHeld = $false
    $script:AppOfflinePlaced = $false
    $script:CopyStarted = $false
    $script:RollbackOk = $false
    $script:TargetCommit = ""
    $script:LastSuccessCommit = ""
    $script:PreDeployHead = ""
    $script:SkipRemainingWork = $false
}

function Show-DeployHelp {
    Write-Host "Usage: $script:DeployScriptName.ps1 [-Check] [-Force] [-Pause] [-Help]"
    Write-Host ""
    Write-Host "Runs a repo-contained OSAFW deployment profile."
    Write-Host "Environment: $script:EnvironmentName"
    Write-Host "Branch: $script:GitRemote/$script:GitBranch"
    Write-Host "Repo root: $script:RepoRoot"
    Write-Host "Target: $script:TargetFolder"
    Write-Host "Log file: $script:LogFile"
    Write-Host ""
    Write-Host "-Check validates tools, paths, branch resolution, and write access without deploying."
    Write-Host "-Force redeploys even when the target commit matches the last successful commit."
}

function Initialize-DeployLogging {
    if (-not (Test-Path -LiteralPath $script:LogFolder)) {
        New-Item -ItemType Directory -Path $script:LogFolder -Force | Out-Null
    }

    if ((Test-Path -LiteralPath $script:LogFile) -and (Get-Item -LiteralPath $script:LogFile).Length -ge $script:LogMaxBytes) {
        $rotatedLog = Join-Path $script:LogFolder "$script:DeployName.1.log"
        if (Test-Path -LiteralPath $rotatedLog) {
            Remove-Item -LiteralPath $rotatedLog -Force
        }
        Move-Item -LiteralPath $script:LogFile -Destination $rotatedLog -Force
    }
}

function Write-DeployLog {
    param([string]$Message)

    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Write-Host $line
    try {
        Add-Content -LiteralPath $script:LogFile -Value $line -Encoding UTF8
    } catch {
        Write-Host ("[{0}] Unable to write deploy log: {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $_.Exception.Message)
    }
}

function Write-DeployStatus {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [string]$Phase = "",
        [string]$ErrorText = ""
    )

    try {
        $payload = [ordered]@{
            status = $Status
            phase = $Phase
            error = $ErrorText
            environment = $script:EnvironmentName
            deployName = $script:DeployName
            appPool = $script:AppPoolName
            branch = $script:GitBranch
            remote = $script:GitRemote
            targetCommit = $script:TargetCommit
            lastSuccessCommit = $script:LastSuccessCommit
            machine = $env:COMPUTERNAME
            repoRoot = $script:RepoRoot
            appRoot = $script:AppRoot
            targetFolder = $script:TargetFolder
            logFile = $script:LogFile
            updatedUtc = (Get-Date).ToUniversalTime().ToString("o")
        }
        $payload | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $script:StatusFile -Encoding UTF8
    } catch {
        Write-DeployLog "Unable to write deploy status and continuing: $($_.Exception.Message)"
    }
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList,
        [switch]$IgnoreExitCode
    )

    Write-DeployLog ("RUN: {0} {1}" -f $FilePath, ($ArgumentList -join " "))
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = & $FilePath @ArgumentList 2>&1
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    foreach ($line in $output) {
        Write-DeployLog ($line.ToString())
    }

    if (-not $IgnoreExitCode -and $code -ne 0) {
        throw "$FilePath failed with exit code $code."
    }

    return $code
}

function Assert-DeployPreflight {
    if (-not (Test-Path -LiteralPath (Join-Path $script:RepoRoot ".git"))) {
        throw "Repo root is not a git repository: $script:RepoRoot"
    }
    if (-not (Test-Path -LiteralPath $script:ProjectFile)) {
        throw "Project file was not found: $script:ProjectFile"
    }
    if (-not (Test-Path -LiteralPath $script:AppRoot)) {
        throw "App root was not found: $script:AppRoot"
    }
    if ([string]::IsNullOrWhiteSpace($script:TargetFolder)) {
        throw "Target folder is empty."
    }
    if ([string]::IsNullOrWhiteSpace($script:PublishFolder)) {
        throw "Publish folder is empty."
    }
    if ([string]::IsNullOrWhiteSpace($script:BuildWorktree)) {
        throw "Build worktree is empty."
    }
    if ([string]::IsNullOrWhiteSpace($script:DeployName)) {
        throw "Deploy name is empty."
    }

    $repoRootFull = Get-FullDeployPath $script:RepoRoot
    $appRootFull = Get-FullDeployPath $script:AppRoot
    $targetFolderFull = Get-FullDeployPath $script:TargetFolder
    $publishFolderFull = Get-FullDeployPath $script:PublishFolder
    $buildWorktreeFull = Get-FullDeployPath $script:BuildWorktree

    if ($targetFolderFull.Equals($repoRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Target folder must not equal repo root."
    }
    if ($targetFolderFull.Equals($appRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Target folder must not equal app root."
    }
    if ($publishFolderFull.Equals($targetFolderFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Publish folder must not equal target folder."
    }
    if ($buildWorktreeFull.Equals($repoRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Build worktree must not equal repo root."
    }
    if ($buildWorktreeFull.Equals($targetFolderFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Build worktree must not equal target folder."
    }
    foreach ($path in @($targetFolderFull, $publishFolderFull, $buildWorktreeFull)) {
        if (Test-DriveRootPath $path) {
            throw "Temporary or target paths must not point at a drive root: $path"
        }
    }
    foreach ($path in @($publishFolderFull, $buildWorktreeFull)) {
        if ($path.IndexOf($script:DeployName, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Temporary path must include deploy name token '$script:DeployName': $path"
        }
    }
    foreach ($protected in @(
        (Join-Path $script:AppRoot "App_Data\db"),
        (Join-Path $script:AppRoot "App_Data\logs"),
        (Join-Path $script:AppRoot "upload")
    )) {
        if (Test-PathInsideOrEqual $targetFolderFull $protected) {
            throw "Target folder points at protected runtime data: $protected"
        }
    }

    foreach ($command in @("git", "dotnet", "robocopy")) {
        if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
            throw "$command was not found in PATH."
        }
    }
}

function Get-ExistingParentPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $candidate = Get-FullDeployPath $Path
    while (-not (Test-Path -LiteralPath $candidate)) {
        $parent = Split-Path -Parent $candidate
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $candidate) {
            throw "No existing parent path found for: $Path"
        }
        $candidate = $parent
    }

    return $candidate
}

function Test-DeployWriteAccess {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $existingPath = Get-ExistingParentPath $Path
    $probe = Join-Path $existingPath "$script:DeployName.$PID.check.tmp"
    Set-Content -LiteralPath $probe -Value "deploy check $(Get-Date -Format o)" -Encoding ASCII
    Remove-Item -LiteralPath $probe -Force
    Write-DeployLog "Write check passed for ${Label}: $existingPath"
}

function Assert-DeployWriteAccess {
    Test-DeployWriteAccess $script:LogFolder "log folder"
    Test-DeployWriteAccess $script:BuildWorktree "temporary build worktree parent"
    Test-DeployWriteAccess $script:PublishFolder "temporary publish parent"
    Test-DeployWriteAccess $script:TargetFolder "target folder or parent"
}

function Acquire-DeployLock {
    try {
        New-Item -ItemType Directory -Path $script:LockDir -ErrorAction Stop | Out-Null
        $script:LockHeld = $true
        Set-Content -LiteralPath (Join-Path $script:LockDir "started.txt") -Value (Get-Date).ToString("s") -Encoding ASCII
        Write-DeployLog "Acquired deploy lock: $script:LockDir"
        return $true
    } catch {
        if (-not (Test-Path -LiteralPath $script:LockDir)) {
            throw "Unable to acquire deploy lock: $script:LockDir"
        }

        $age = (Get-Date) - (Get-Item -LiteralPath $script:LockDir).LastWriteTime
        if ($age.TotalMinutes -lt $script:LockStaleMinutes) {
            Write-DeployLog "Another deploy run is active; exiting without work."
            return $false
        }

        Write-DeployLog "Removing stale deploy lock: $script:LockDir"
        Remove-Item -LiteralPath $script:LockDir -Recurse -Force
        New-Item -ItemType Directory -Path $script:LockDir -ErrorAction Stop | Out-Null
        $script:LockHeld = $true
        Set-Content -LiteralPath (Join-Path $script:LockDir "started.txt") -Value (Get-Date).ToString("s") -Encoding ASCII
        return $true
    }
}

function Release-DeployLock {
    if ($script:LockHeld -and (Test-Path -LiteralPath $script:LockDir)) {
        Remove-Item -LiteralPath $script:LockDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    $script:LockHeld = $false
}

function Resolve-TargetCommit {
    param([switch]$Fetch)

    if ($Fetch) {
        Write-DeployLog "Fetching $script:GitRemote/$script:GitBranch."
        Invoke-LoggedCommand "git" @("-C", $script:RepoRoot, "fetch", "--prune", $script:GitRemote) | Out-Null
    } else {
        Write-DeployLog "Resolving $script:GitRemote/$script:GitBranch without fetch."
    }

    $commitOutput = & git -C $script:RepoRoot rev-parse --verify "$script:GitRemote/$script:GitBranch" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to resolve $script:GitRemote/$script:GitBranch. $commitOutput"
    }
    $script:TargetCommit = ($commitOutput | Select-Object -First 1).ToString().Trim()

    $headOutput = & git -C $script:RepoRoot rev-parse --verify HEAD 2>&1
    if ($LASTEXITCODE -eq 0) {
        $script:PreDeployHead = ($headOutput | Select-Object -First 1).ToString().Trim()
    }

    if (Test-Path -LiteralPath $script:StateFile) {
        $script:LastSuccessCommit = (Get-Content -LiteralPath $script:StateFile -TotalCount 1).Trim()
    }
}

function Remove-BuildWorktree {
    if (Test-Path -LiteralPath $script:BuildWorktree) {
        Invoke-LoggedCommand "git" @("-C", $script:RepoRoot, "worktree", "remove", "--force", $script:BuildWorktree) -IgnoreExitCode | Out-Null
        if (Test-Path -LiteralPath $script:BuildWorktree) {
            Remove-Item -LiteralPath $script:BuildWorktree -Recurse -Force
        }
    }
}

function Prepare-BuildWorktree {
    Write-DeployLog "Preparing temporary build worktree: $script:BuildWorktree"
    Remove-BuildWorktree
    Invoke-LoggedCommand "git" @("-C", $script:RepoRoot, "worktree", "prune") | Out-Null
    Invoke-LoggedCommand "git" @("-C", $script:RepoRoot, "worktree", "add", "--force", "--detach", $script:BuildWorktree, $script:TargetCommit) | Out-Null
}

function Publish-ToTemp {
    Write-DeployLog "Publishing $script:ProjectFileRelative to $script:PublishFolder."
    if (Test-Path -LiteralPath $script:PublishFolder) {
        Remove-Item -LiteralPath $script:PublishFolder -Recurse -Force
    }
    New-Item -ItemType Directory -Path $script:PublishFolder -Force | Out-Null

    $buildProjectFile = Join-Path $script:BuildWorktree $script:ProjectFileRelative
    Invoke-LoggedCommand "dotnet" @("publish", $buildProjectFile, "--configuration", "Release", "/p:EnvironmentName=$script:EnvironmentName", "-o", $script:PublishFolder) | Out-Null

    $buildProjectDir = Split-Path -Parent $buildProjectFile
    $offlineSourceFile = Join-Path $buildProjectDir $script:OfflineSource
    $publishOfflineFile = Join-Path $script:PublishFolder "app_offline.htm"
    if (Test-Path -LiteralPath $offlineSourceFile) {
        Copy-Item -LiteralPath $offlineSourceFile -Destination $publishOfflineFile -Force
    } else {
        Set-Content -LiteralPath $publishOfflineFile -Value "Deployment in progress. Please retry in a few minutes." -Encoding ASCII
    }
}

function Remove-AppOffline {
    $offlineTarget = Join-Path $script:TargetFolder "app_offline.htm"
    if (Test-Path -LiteralPath $offlineTarget) {
        Remove-Item -LiteralPath $offlineTarget -Force
    }
    if (Test-Path -LiteralPath $offlineTarget) {
        throw "Unable to remove app_offline.htm from target folder."
    }
    $script:AppOfflinePlaced = $false
}

function Deploy-PublishedOutput {
    Write-DeployLog "Placing app_offline.htm."
    if (-not (Test-Path -LiteralPath $script:TargetFolder)) {
        New-Item -ItemType Directory -Path $script:TargetFolder -Force | Out-Null
    }

    Copy-Item -LiteralPath (Join-Path $script:PublishFolder "app_offline.htm") -Destination (Join-Path $script:TargetFolder "app_offline.htm") -Force
    $script:AppOfflinePlaced = $true
    Start-Sleep -Seconds 5

    Write-DeployLog "Resetting live repository to $script:TargetCommit."
    Invoke-LoggedCommand "git" @("-C", $script:RepoRoot, "reset", "--hard", $script:TargetCommit) | Out-Null

    Write-DeployLog "Copying published files to $script:TargetFolder."
    $script:CopyStarted = $true
    $robocopyArgs = @($script:PublishFolder, $script:TargetFolder, "/MIR") + $script:RobocopyExtraArgs
    if ($script:RobocopyExcludeDirs.Count -gt 0) {
        $robocopyArgs += @("/XD") + $script:RobocopyExcludeDirs
    }
    $robocopyCode = Invoke-LoggedCommand "robocopy" $robocopyArgs -IgnoreExitCode
    if ($robocopyCode -ge 8) {
        throw "robocopy failed with exit code $robocopyCode."
    }

    Remove-AppOffline
}

function Rollback-LiveRepo {
    $script:RollbackOk = $false
    $rollbackCommit = $script:LastSuccessCommit
    if ([string]::IsNullOrWhiteSpace($rollbackCommit)) {
        $rollbackCommit = $script:PreDeployHead
    }
    if ([string]::IsNullOrWhiteSpace($rollbackCommit)) {
        Write-DeployLog "No rollback commit is available."
        return
    }

    Write-DeployLog "Rolling live repository back to $rollbackCommit."
    Invoke-LoggedCommand "git" @("-C", $script:RepoRoot, "reset", "--hard", $rollbackCommit) | Out-Null
    $script:RollbackOk = $true
}

function Send-DeployNotification {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [string]$ErrorText = ""
    )

    if ([string]::IsNullOrWhiteSpace($script:NotifyWebhook)) {
        return
    }

    try {
        $payload = [ordered]@{
            appPool = $script:AppPoolName
            environment = $script:EnvironmentName
            deployName = $script:DeployName
            status = $Status
            error = $ErrorText
            branch = $script:GitBranch
            commit = $script:TargetCommit
            machine = $env:COMPUTERNAME
            repoRoot = Split-Path -Leaf $script:RepoRoot
            appRoot = ConvertTo-NotificationPath $script:AppRoot
            targetFolder = ConvertTo-NotificationPath $script:TargetFolder
            logFile = ConvertTo-NotificationPath $script:LogFile
            statusFile = ConvertTo-NotificationPath $script:StatusFile
        }
        Invoke-RestMethod -Uri $script:NotifyWebhook -Method Post -ContentType "application/json" -Body ($payload | ConvertTo-Json -Compress) -TimeoutSec 10 | Out-Null
    } catch {
        Write-DeployLog "Notification failed and was ignored: $($_.Exception.Message)"
    }
}

function Invoke-DeployCheck {
    Write-DeployLog "== Deploy check started: $script:DeployName =="
    Write-DeployLog "Repo root: $script:RepoRoot"
    Write-DeployLog "App root: $script:AppRoot"
    Write-DeployLog "Project file: $script:ProjectFile"
    Write-DeployLog "Target folder: $script:TargetFolder"
    Assert-DeployPreflight
    Resolve-TargetCommit
    Assert-DeployWriteAccess
    Write-DeployLog "Resolved target commit: $script:TargetCommit"
    Write-DeployLog "Deploy check passed."
    Write-DeployStatus "check-ok" "check"
}

Initialize-DeploySettings
if ($Help) {
    Show-DeployHelp
    $script:DeployExitCode = 0
    return
}

try {
    Initialize-DeployLogging
    if ($Check) {
        Invoke-DeployCheck
        $script:ExitCode = 0
        $script:DeployExitCode = 0
        return
    }

    Write-DeployLog "== Deploy script started: $script:DeployName =="
    Write-DeployStatus "running" "start"

    if (-not (Acquire-DeployLock)) {
        Write-DeployStatus "skipped" "locked"
        $script:SkipRemainingWork = $true
    }

    if (-not $script:SkipRemainingWork) {
        Assert-DeployPreflight
        Assert-DeployWriteAccess
        Resolve-TargetCommit -Fetch

        if (-not $Force -and $script:TargetCommit.Equals($script:LastSuccessCommit, [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-DeployLog "No changes found. $script:GitRemote/$script:GitBranch is already deployed at $script:TargetCommit."
            Write-DeployStatus "skipped" "no-change"
            $script:SkipRemainingWork = $true
        }
    }

    if (-not $script:SkipRemainingWork) {
        Write-DeployLog "Target commit: $script:TargetCommit"
        Write-DeployStatus "running" "build"
        Prepare-BuildWorktree
        Publish-ToTemp
        Write-DeployStatus "running" "copy"
        Deploy-PublishedOutput
        Set-Content -LiteralPath $script:StateFile -Value $script:TargetCommit -Encoding ASCII
        Write-DeployLog "Deploy succeeded at $script:TargetCommit."
        Write-DeployStatus "success" "complete"
        Send-DeployNotification "success"
    }
} catch {
    $script:ExitCode = 1
    $script:ErrorMessage = $_.Exception.Message
    Write-DeployLog "ERROR: $script:ErrorMessage"

    if ($script:AppOfflinePlaced) {
        if (-not $script:CopyStarted) {
            try {
                Rollback-LiveRepo
                if ($script:RollbackOk) {
                    Remove-AppOffline
                } else {
                    Write-DeployLog "Rollback failed or no rollback commit was available; leaving app_offline.htm in place."
                }
            } catch {
                Write-DeployLog "Rollback failed: $($_.Exception.Message)"
                Write-DeployLog "Leaving app_offline.htm in place."
            }
        } else {
            Write-DeployLog "Copy started before failure; leaving app_offline.htm in place so the next scheduled run can retry safely."
        }
    }

    Write-DeployStatus "error" "error" $script:ErrorMessage
    if (-not $Check) {
        Send-DeployNotification "error" $script:ErrorMessage
    }
} finally {
    if ($script:LockHeld) {
        try {
            Remove-BuildWorktree
        } catch {
            Write-DeployLog "Temporary worktree cleanup failed and was ignored: $($_.Exception.Message)"
        }
        Release-DeployLock
    }

    Write-DeployLog "== Deploy script finished with exit code $script:ExitCode. =="
    if ($Pause) {
        Read-Host "Press Enter to exit"
    }
}

$script:DeployExitCode = $script:ExitCode
return
