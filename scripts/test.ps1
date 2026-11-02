<# Runs isolated framework tests. No application connection settings are used. #>
[CmdletBinding()]
param(
    [ValidateSet('Fast', 'Browser', 'SqlServer', 'Full')][string]$Profile = 'Fast',
    [string]$Filter = '',
    [string]$SqlServer = '(localdb)\MSSQLLocalDB'
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runRoot = Join-Path $repo ('artifacts/test-runs/' + [guid]::NewGuid().ToString('N'))
$project = Join-Path $repo 'osafw-tests/osafw-tests.csproj'
$previousEnabled = $env:FW_TEST_SQLSERVER
$previousServer = $env:FW_TEST_SQLSERVER_SERVER
$failures = 0
$fastFilter = 'TestCategory!=Browser&TestCategory!=SqlServer'
$stages = switch ($Profile) {
    'Fast' { @('Fast') }
    'Browser' { @('Browser') }
    'SqlServer' { @('SqlServer') }
    'Full' { @('Default', 'Fast', 'Browser', 'SqlServer') }
}
try {
    foreach ($stage in $stages) {
        $selection = switch ($stage) {
            'Browser' { 'TestCategory=Browser' }
            'SqlServer' { 'TestCategory=SqlServer' }
            default { $fastFilter }
        }
        if ($Filter) { $selection = "($selection)&($Filter)" }
        $isSqlite = $stage -ne 'Default' -and $stage -ne 'SqlServer'
        $variant = if ($isSqlite) { 'sqlite' } else { 'default' }
        $outDir = (Join-Path $runRoot "$variant/bin") + [IO.Path]::DirectorySeparatorChar
        $results = Join-Path $runRoot $stage
        $env:FW_TEST_SQLSERVER = if ($stage -eq 'SqlServer') { '1' } else { '0' }
        $env:FW_TEST_SQLSERVER_SERVER = $SqlServer
        Write-Host "Running $stage ($variant). Selection: $selection"
        if ($stage -eq 'Fast' -or $stage -eq 'Default') { Write-Host 'Excluded groups: Browser, SqlServer' }
        else { Write-Host "Excluded groups: all except $stage" }
        $watch = [Diagnostics.Stopwatch]::StartNew()
        & dotnet build $project "-p:EnableSqliteTests=$($isSqlite.ToString().ToLowerInvariant())" "-p:OutDir=$outDir" --verbosity quiet
        if ($LASTEXITCODE -ne 0) { $failures++; continue }
        Write-Host "Build/restore: $($watch.Elapsed.TotalSeconds.ToString('F2')) seconds"
        $watch.Restart()
        & dotnet test $project --no-build --no-restore "-p:EnableSqliteTests=$($isSqlite.ToString().ToLowerInvariant())" "-p:OutDir=$outDir" --filter $selection --logger 'trx;LogFileName=results.trx' --results-directory $results --verbosity quiet
        $testExit = $LASTEXITCODE
        Write-Host "Test process: $($watch.Elapsed.TotalSeconds.ToString('F2')) seconds"
        $resultFile = Join-Path $results 'results.trx'
        if (-not (Test-Path -LiteralPath $resultFile)) { Write-Warning 'No test results produced.'; $failures++; continue }
        [xml]$report = Get-Content -LiteralPath $resultFile -Raw
        $counts = $report.TestRun.ResultSummary.Counters
        Write-Host "Executed: $($counts.executed); passed: $($counts.passed); failed: $($counts.failed); unavailable: $($counts.notExecuted)"
        if ($testExit -ne 0 -or [int]$counts.executed -eq 0 -or [int]$counts.notExecuted -gt 0) { $failures++ }
    }
}
finally {
    $env:FW_TEST_SQLSERVER = $previousEnabled
    $env:FW_TEST_SQLSERVER_SERVER = $previousServer
    Write-Host "Results: $runRoot"
}
if ($failures -gt 0) { exit 1 }
exit 0
