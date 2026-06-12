#Requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Check,
    [switch]$Force,
    [switch]$Pause,
    [switch]$Help
)

$EnvironmentName = "Develop"
$GitBranch = "develop"
$DeployName = "deploy-develop"
$DeployScriptName = [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath)

$DeployExitCode = 1
try {
    . (Join-Path $PSScriptRoot "deploy_core.ps1") -Check:$Check -Force:$Force -Pause:$Pause -Help:$Help
} catch {
    Write-Error $_
}
exit $DeployExitCode
