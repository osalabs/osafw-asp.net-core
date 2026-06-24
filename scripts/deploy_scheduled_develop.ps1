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
# Optional: set to your app's deploy notification endpoint, for example "https://deploy.samplesite.tld/Notify".
$NotifyWebhook = ""
$DeployScriptName = [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath)

$DeployExitCode = 1
try {
    . (Join-Path $PSScriptRoot "deploy_core.ps1") -Check:$Check -Force:$Force -Pause:$Pause -Help:$Help
} catch {
    Write-Error $_
}
exit $DeployExitCode
