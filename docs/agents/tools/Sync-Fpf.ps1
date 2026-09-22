<#
.SYNOPSIS
Installs or checks the ignored FPF source cache, or records a reviewed edition.
.DESCRIPTION
Ensure may fetch once per 24 hours. Status, Select, and Ensure -Offline are read-only.
Accept records the caller's review; the script does not judge semantic compatibility.
#>
[CmdletBinding()]
param(
    [ValidateSet("Ensure", "Status", "Accept", "Select")][string]$Action = "Ensure",
    [string]$Revision,
    [string]$ReviewNote,
    [string]$RepositoryRoot,
    [string]$SourcePath,
    [switch]$Force,
    [switch]$Offline,
    [switch]$LocalCache
)
. (Join-Path $PSScriptRoot "Fpf.Common.ps1")
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$checkoutLock = $null; $cacheLock = $null
try {
    if (($Offline -or $Force -or $SourcePath) -and $Action -ne "Ensure") { throw "Offline, Force and SourcePath apply only to Ensure." }
    if ($Offline -and ($Force -or $SourcePath)) { throw "Offline only inspects existing state; omit Force and SourcePath." }
    if ($Action -in @("Accept", "Select")) { Assert-FpfRevision $Revision }
    elseif ($Revision -or $ReviewNote) { throw "Revision and ReviewNote apply to explicit selection or acceptance." }
    if ($Action -eq "Accept" -and ([string]::IsNullOrWhiteSpace($ReviewNote) -or $ReviewNote.Length -gt 2000)) {
        throw "Accept requires a nonempty ReviewNote of at most 2000 characters from the completed compatibility review."
    }
    $context = Get-FpfContext $RepositoryRoot -LocalCache:$LocalCache
    $checkResult = "not-requested"
    if ($Action -eq "Accept" -or ($Action -eq "Ensure" -and -not $Offline)) {
        [void][System.IO.Directory]::CreateDirectory($context.LocalRoot)
        $checkoutLock = Open-FpfLock (Join-Path $context.LocalRoot "checkout.lock")
        # Another caller may have established the fallback/adoption while this one waited.
        $context = Get-FpfContext $RepositoryRoot -LocalCache:$LocalCache
        try {
            [void][System.IO.Directory]::CreateDirectory($context.CacheRoot)
            $cacheLock = Open-FpfLock (Join-Path $context.CacheRoot "source.lock")
        }
        catch [System.UnauthorizedAccessException] {
            if ($context.CacheRoot -eq $context.LocalRoot) { throw }
            $context = Get-FpfContext $RepositoryRoot -LocalCache
            [void][System.IO.Directory]::CreateDirectory($context.CacheRoot)
            $cacheLock = Open-FpfLock (Join-Path $context.CacheRoot "source.lock")
        }
        $adoption = Read-FpfJson $context.AdoptionPath
        if ($null -eq $adoption) {
            $adoption = [pscustomobject]@{
                SchemaVersion = 1; CacheRoot = $context.CacheRoot; AcceptedRevision = $null
                AcceptedPolicyHash = $null; AcceptedAtUtc = $null; ReviewNote = $null
            }
        }
        # The target cache is only staged here. Persist its root after validation,
        # preserving the prior usable root if installation or acceptance fails.
        $context.Adoption = $adoption
    }
    $source = Read-FpfJson $context.SourceStatePath
    if ($Action -eq "Ensure" -and -not $Offline) {
        if ($null -eq $source) {
            if (Test-Path -LiteralPath $context.GitDirectory) { throw "Unowned corpus.git exists; do not overwrite it." }
            $source = [pscustomobject]@{
                SchemaVersion = 1; Source = (Get-FpfSource $SourcePath)
                LastAttemptUtc = $null; LastSuccessUtc = $null
                CandidateRevision = $null; RejectedRevision = $null; LastError = $null
            }
            Write-FpfJson $context.SourceStatePath $source
        }
        elseif ($SourcePath -and $source.Source -cne (Get-FpfSource $SourcePath)) {
            throw "This cache is bound to a different source. Use a separate repository/cache for that source."
        }
        $now = [DateTimeOffset]::UtcNow
        $due = $true
        if ($source.LastAttemptUtc) {
            $age = $now - [DateTimeOffset]::Parse($source.LastAttemptUtc, [Globalization.CultureInfo]::InvariantCulture)
            # A future timestamp triggers a check rather than indefinitely disabling refresh.
            $due = $age.TotalHours -ge 24 -or $age.TotalSeconds -lt 0
        }
        if ($Force -or $due) {
            $source.LastAttemptUtc = $now.ToString("o")
            Write-FpfJson $context.SourceStatePath $source
            $fetchedRevision = $null
            try {
                if (-not [System.IO.File]::Exists((Join-Path $context.GitDirectory "HEAD"))) {
                    if ($source.CandidateRevision) { throw "The existing FPF repository is incomplete; preserve it for repair." }
                    [void](Invoke-FpfGit -Arguments @("init", "--bare", "--template=", $context.GitDirectory))
                    [void](Invoke-FpfGit -GitDirectory $context.GitDirectory -Arguments @("config", "--local", "remote.origin.url", $source.Source))
                }
                Assert-FpfRepository $context $source.Source
                [void](Invoke-FpfGit -GitDirectory $context.GitDirectory -Arguments @(
                    "-c", "fetch.fsckObjects=true", "fetch", "--depth=1", "--no-tags", "--no-recurse-submodules",
                    $source.Source, "+refs/heads/main:refs/fpf/incoming"))
                $fetchedRevision = (Invoke-FpfGit -GitDirectory $context.GitDirectory -Arguments @("rev-parse", "refs/fpf/incoming^{commit}")).Output.Trim()
                Assert-FpfRevision $fetchedRevision
                $manifestPath = Join-Path $context.CacheRoot ("indexes/" + $fetchedRevision + ".json")
                if (-not [System.IO.File]::Exists($manifestPath)) {
                    $manifest = New-FpfManifest $context $fetchedRevision $source.Source
                    [void][System.IO.Directory]::CreateDirectory((Join-Path $context.CacheRoot "indexes"))
                    Write-FpfJson $manifestPath $manifest
                }
                else { [void](Get-FpfManifest $context $fetchedRevision) }
                # Keep objects reachable for task pins and offline replay of accepted older revisions.
                [void](Invoke-FpfGit -GitDirectory $context.GitDirectory -Arguments @("update-ref", "refs/fpf/snapshots/$fetchedRevision", $fetchedRevision))
                $source.CandidateRevision = $fetchedRevision
                $source.RejectedRevision = $null
                $source.LastSuccessUtc = [DateTimeOffset]::UtcNow.ToString("o")
                $source.LastError = $null
                $checkResult = "checked"
            }
            catch {
                $source.LastError = $_.Exception.Message
                $source.RejectedRevision = $fetchedRevision
                $checkResult = "failed"
            }
            Write-FpfJson $context.SourceStatePath $source
        }
        else { $checkResult = "throttled" }
    }
    elseif ($Offline) { $checkResult = "offline" }

    $policyHash = Get-FpfPolicyHash $context.RepositoryRoot
    $accepted = $null; $candidate = $null; $selected = $null; $unavailableAccepted = $null
    if ($null -ne $context.Adoption -and $context.Adoption.AcceptedRevision) {
        $acceptedIndex = Join-Path $context.CacheRoot ("indexes/" + $context.Adoption.AcceptedRevision + ".json")
        if ([System.IO.File]::Exists($acceptedIndex)) { $accepted = Get-FpfManifest $context $context.Adoption.AcceptedRevision }
        else { $unavailableAccepted = $context.Adoption.AcceptedRevision }
    }
    if ($null -ne $source -and $source.CandidateRevision) { $candidate = Get-FpfManifest $context $source.CandidateRevision }
    if ($Action -eq "Ensure" -and -not $Offline -and $checkResult -ne "failed" -and
        $null -ne $candidate -and $null -eq $unavailableAccepted) {
        $context.Adoption.CacheRoot = $context.CacheRoot
        Write-FpfJson $context.AdoptionPath $context.Adoption
    }
    if ($Action -eq "Accept") {
        if ($null -eq $candidate -or $Revision -cne $candidate.Revision) {
            throw "The reviewed revision is no longer the current validated candidate; inspect Status before accepting."
        }
        # Revalidate the actual source with the current helper before recording adoption.
        $candidate = New-FpfManifest $context $Revision $source.Source
        Write-FpfJson (Join-Path $context.CacheRoot ("indexes/" + $Revision + ".json")) $candidate
        $context.Adoption.CacheRoot = $context.CacheRoot
        $context.Adoption.AcceptedRevision = $Revision
        $context.Adoption.AcceptedPolicyHash = $policyHash
        $context.Adoption.AcceptedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        $context.Adoption.ReviewNote = $ReviewNote.Trim()
        Write-FpfJson $context.AdoptionPath $context.Adoption
        $accepted = $candidate
        $unavailableAccepted = $null
    }
    if ($Action -eq "Select") { $selected = Get-FpfManifest $context $Revision }
    elseif ($null -ne $accepted) { $selected = $accepted }
    $policyChanged = $null -ne $accepted -and $context.Adoption.AcceptedPolicyHash -cne $policyHash
    $reviewRequired = $null -ne $candidate -and ($null -eq $accepted -or $candidate.Revision -cne $accepted.Revision -or $policyChanged)
    $status = if ($Action -eq "Select") { "selected" } elseif ($Action -eq "Accept") { "accepted" }
        elseif ($reviewRequired) { "needs-review" } elseif ($null -eq $accepted) { "unavailable" }
        elseif ($null -ne $source -and $source.LastError) { "stale" } else { "ready" }
    $changes = if ($reviewRequired) { Get-FpfChanges $accepted $candidate $context.RepositoryRoot } else { $null }
    [pscustomobject]@{
        Status = $status; CheckResult = $checkResult; CacheRoot = $context.CacheRoot
        CacheTransitionPending = ($null -ne $context.Adoption -and $context.Adoption.CacheRoot -ne $context.CacheRoot)
        Source = $(if ($null -ne $source) { $source.Source } else { $script:FpfUpstream })
        AcceptedRevision = $(if ($null -ne $accepted) { $accepted.Revision } else { $null })
        UnavailableAcceptedRevision = $unavailableAccepted
        CandidateRevision = $(if ($null -ne $candidate) { $candidate.Revision } else { $null })
        SelectedRevision = $(if ($null -ne $selected) { $selected.Revision } else { $null })
        SelectedIsAccepted = ($null -ne $selected -and $null -ne $accepted -and $selected.Revision -ceq $accepted.Revision)
        RejectedRevision = $(if ($null -ne $source) { $source.RejectedRevision } else { $null })
        LastAttemptUtc = $(if ($null -ne $source) { $source.LastAttemptUtc } else { $null })
        LastSuccessUtc = $(if ($null -ne $source) { $source.LastSuccessUtc } else { $null })
        PolicyHash = $policyHash; PolicyChanged = $policyChanged; ReviewRequired = $reviewRequired
        Changes = $changes
        Notice = $(if ($null -ne $source) { $source.LastError } else { "No local FPF source is available." })
    } | ConvertTo-Json -Depth 8
    if ($checkResult -eq "failed" -and $null -eq $accepted -and $null -eq $candidate) { exit 1 }
}
catch {
    [pscustomobject]@{ Status = "error"; Notice = $_.Exception.Message } | ConvertTo-Json -Compress
    exit 1
}
finally {
    if ($null -ne $cacheLock) { $cacheLock.Dispose() }
    if ($null -ne $checkoutLock) { $checkoutLock.Dispose() }
}
