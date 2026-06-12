#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$DeployScript = "",
    [string]$TaskName = "",
    [int]$EveryMinutes = 10,
    [string]$WorkingDirectory = "",
    [string]$RunAsUser = "",
    [switch]$UseSystem,
    [switch]$AllowManualScript,
    [switch]$Check,
    [switch]$Help
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-FullSetupPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path).TrimEnd("\")
}

function ConvertTo-TaskTitle {
    param([Parameter(Mandatory = $true)][string]$ScriptName)

    $name = $ScriptName -replace "^deploy_scheduled_", ""
    $name = $name -replace "^deploy_", ""
    $name = $name -replace "[-_]+", " "
    return (Get-Culture).TextInfo.ToTitleCase($name)
}

function Show-SetupHelp {
    Write-Host "Usage: setup_deploy_scheduled_task.ps1 [-DeployScript <path>] [-TaskName <name>] [-EveryMinutes 10] [-RunAsUser <user>] [-UseSystem] [-Check]"
    Write-Host ""
    Write-Host "Registers or updates a Windows Scheduled Task for a repo-contained deploy_scheduled_*.ps1 script."
    Write-Host "Default deploy script: scripts\deploy_scheduled_develop.ps1"
    Write-Host "-Check prints inferred values without registering a task."
    Write-Host "-AllowManualScript permits non-scheduled script names, such as deploy_production.ps1."
}

if ($Help) {
    Show-SetupHelp
    exit 0
}

if ($EveryMinutes -lt 1) {
    throw "EveryMinutes must be 1 or greater."
}

if ([string]::IsNullOrWhiteSpace($DeployScript)) {
    $DeployScript = Join-Path $PSScriptRoot "deploy_scheduled_develop.ps1"
}

$DeployScript = Get-FullSetupPath $DeployScript
if (-not (Test-Path -LiteralPath $DeployScript)) {
    throw "Deploy script was not found: $DeployScript"
}

$extension = [System.IO.Path]::GetExtension($DeployScript).ToLowerInvariant()
$scriptBaseName = [System.IO.Path]::GetFileNameWithoutExtension($DeployScript)
if (-not $AllowManualScript) {
    if ($extension -ne ".ps1" -or $scriptBaseName -notlike "deploy_scheduled_*") {
        throw "Refusing to schedule '$DeployScript'. Use a deploy_scheduled_*.ps1 profile or pass -AllowManualScript intentionally."
    }
}

if ([string]::IsNullOrWhiteSpace($TaskName)) {
    $TaskName = "OSAFW Deploy - " + (ConvertTo-TaskTitle $scriptBaseName)
}

if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
    $WorkingDirectory = Split-Path -Parent $DeployScript
}
$WorkingDirectory = Get-FullSetupPath $WorkingDirectory
if (-not (Test-Path -LiteralPath $WorkingDirectory)) {
    throw "WorkingDirectory was not found: $WorkingDirectory"
}

$execute = ""
$argument = ""
switch ($extension) {
    ".ps1" {
        $execute = "powershell.exe"
        $argument = "-NoProfile -ExecutionPolicy Bypass -File `"$DeployScript`""
    }
    ".bat" {
        if (-not $AllowManualScript) {
            throw "Batch deploy scripts require -AllowManualScript."
        }
        $execute = "cmd.exe"
        $argument = "/c `"$DeployScript`""
    }
    ".cmd" {
        if (-not $AllowManualScript) {
            throw "Command deploy scripts require -AllowManualScript."
        }
        $execute = "cmd.exe"
        $argument = "/c `"$DeployScript`""
    }
    default {
        throw "DeployScript must be a .ps1 file, or a .bat/.cmd file with -AllowManualScript."
    }
}

if ($Check) {
    Write-Host "Task name: $TaskName"
    Write-Host "Deploy script: $DeployScript"
    Write-Host "Working directory: $WorkingDirectory"
    Write-Host "Execute: $execute"
    Write-Host "Argument: $argument"
    Write-Host "Interval: every $EveryMinutes minutes"
    Write-Host "Multiple instances: IgnoreNew"
    Write-Host "Mode: check only, no Scheduled Task changes made."
    exit 0
}

if (-not (Test-IsAdministrator)) {
    throw "Run this setup script from an elevated PowerShell prompt."
}

$action = New-ScheduledTaskAction `
    -Execute $execute `
    -Argument $argument `
    -WorkingDirectory $WorkingDirectory

$trigger = New-ScheduledTaskTrigger `
    -Once `
    -At ((Get-Date).AddMinutes(1)) `
    -RepetitionInterval (New-TimeSpan -Minutes $EveryMinutes) `
    -RepetitionDuration (New-TimeSpan -Days 3650)

$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries

$description = "Runs the OSAFW repo-contained scheduled deployment script every $EveryMinutes minutes. The deploy script also has stale-lock protection."

$existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
$verb = "Created"
if ($existing) {
    $verb = "Updated"
}

if ($UseSystem) {
    Write-Warning "Using SYSTEM is convenient, but Git SSH/HTTPS credentials must also be configured for LocalSystem. A dedicated deploy account is usually better."
    $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
    $task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description $description
    Register-ScheduledTask -TaskName $TaskName -InputObject $task -Force | Out-Null
} else {
    if ([string]::IsNullOrWhiteSpace($RunAsUser)) {
        $RunAsUser = "$env:USERDOMAIN\$env:USERNAME"
    }

    Write-Host "Registering task '$TaskName' to run as $RunAsUser."
    Write-Host "Use a dedicated deploy account when possible. It needs Git credentials and Modify rights to the repo, publish target, temp, and osafw-app\App_Data\logs."
    $credential = Get-Credential -UserName $RunAsUser -Message "Credentials for Scheduled Task '$TaskName'"
    $passwordPtr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($credential.Password)
    try {
        $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPtr)
        $principal = New-ScheduledTaskPrincipal -UserId $credential.UserName -LogonType Password -RunLevel Highest
        $task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description $description
        Register-ScheduledTask -TaskName $TaskName -InputObject $task -User $credential.UserName -Password $password -Force | Out-Null
    } finally {
        if ($passwordPtr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPtr)
        }
    }
}

Write-Host "$verb Scheduled Task '$TaskName'."
Write-Host "Script: $DeployScript"
Write-Host "Interval: every $EveryMinutes minutes"
Write-Host "Multiple instances: IgnoreNew"
