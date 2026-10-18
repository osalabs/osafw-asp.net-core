<#
.SYNOPSIS
Refreshes the ignored FPF source cache or records a reviewed edition.
.DESCRIPTION
Only writable Ensure fetches, migrates, repairs or prunes. Status, Select and
Ensure -Offline use local files without Git or writes. Accept validates local files.
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
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

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
    $isEnsure = $Action -eq "Ensure" -and -not $Offline
    $checkResult = if ($Offline) { "offline" } else { "not-requested" }
    if ($Action -eq "Accept" -or $isEnsure) {
        [void][IO.Directory]::CreateDirectory($context.LocalRoot)
        $checkoutLock = Open-FpfLock (Join-Path $context.LocalRoot "checkout.lock")
        $context = Get-FpfContext $RepositoryRoot -LocalCache:$LocalCache
        try {
            [void][IO.Directory]::CreateDirectory($context.CacheRoot)
            $cacheLock = Open-FpfLock (Join-Path $context.CacheRoot "source.lock")
        }
        catch [UnauthorizedAccessException] {
            if ($context.CacheRoot -eq $context.LocalRoot) { throw }
            $context = Get-FpfContext $RepositoryRoot -LocalCache
            [void][IO.Directory]::CreateDirectory($context.CacheRoot)
            $cacheLock = Open-FpfLock (Join-Path $context.CacheRoot "source.lock")
        }
        if ($null -eq $context.Adoption) {
            $context.Adoption = [pscustomobject]@{
                SchemaVersion = 1; CacheRoot = $context.CacheRoot; AcceptedRevision = $null
                AcceptedPolicyHash = $null; AcceptedAtUtc = $null; ReviewNote = $null
            }
        }
    }
    $source = Read-FpfJson $context.SourceStatePath
    if ($isEnsure) {
        if ($null -eq $source) {
            if (Test-Path -LiteralPath $context.GitDirectory) { throw "Unowned corpus.git exists; do not overwrite it." }
            $source = [pscustomobject]@{
                SchemaVersion = 1; Source = (Get-FpfSource $SourcePath)
                LastAttemptUtc = $null; LastSuccessUtc = $null
                CandidateRevision = $null; RejectedRevision = $null; LastError = $null
                IndexHashes = [pscustomobject]@{}
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
            $due = $age.TotalHours -ge 24 -or $age.TotalSeconds -lt 0
        }
        $didFetch = $false
        $fetchedRevision = $null
        try {
            # Recovery is local and may be necessary even inside the fetch throttle.
            Restore-FpfSnapshots $context $source
            if ($Force -or $due) {
                $source.LastAttemptUtc = $now.ToString("o")
                Write-FpfJson $context.SourceStatePath $source
                if (-not [IO.File]::Exists((Join-Path $context.GitDirectory "HEAD"))) {
                    if ($source.CandidateRevision) { throw "The existing FPF repository is incomplete; preserve it for repair." }
                    [void](Invoke-FpfGit -Arguments @("init", "--bare", "--template=", $context.GitDirectory))
                    [void](Invoke-FpfGit -GitDirectory $context.GitDirectory -Arguments @("config", "--local", "remote.origin.url", $source.Source))
                }
                Assert-FpfRepository $context $source.Source
                $didFetch = $true
                [void](Invoke-FpfGit -GitDirectory $context.GitDirectory -Arguments @(
                    "-c", "fetch.fsckObjects=true", "fetch", "--depth=1", "--no-tags", "--no-recurse-submodules",
                    $source.Source, "+refs/heads/main:refs/fpf/incoming"))
                $fetchedRevision = (Invoke-FpfGit -GitDirectory $context.GitDirectory -Arguments @("rev-parse", "refs/fpf/incoming^{commit}")).Output.Trim()
                Assert-FpfRevision $fetchedRevision
                try { [void](Get-FpfManifest $context $fetchedRevision -VerifyFiles) }
                catch { Save-FpfSnapshot $context $source $fetchedRevision }
                $order = @(Get-FpfProperty $source "EditionOrder" @() | Where-Object { $_ -cne $fetchedRevision })
                Set-FpfProperty $source "EditionOrder" @($order + $fetchedRevision)
                $source.CandidateRevision = $fetchedRevision
                $source.RejectedRevision = $null
                $source.LastSuccessUtc = [DateTimeOffset]::UtcNow.ToString("o")
                $source.LastError = $null
                $checkResult = "checked"
                Write-FpfJson $context.SourceStatePath $source
            }
            else { $checkResult = "throttled" }
        }
        catch {
            $source.LastError = $_.Exception.Message
            $source.RejectedRevision = $fetchedRevision
            $checkResult = "failed"
            Write-FpfJson $context.SourceStatePath $source
        }
        try {
            if ([IO.File]::Exists((Join-Path $context.GitDirectory "HEAD"))) {
                Remove-FpfExpiredEditions $context $source -CollectObjects:$didFetch
            }
        }
        catch {
            Set-FpfProperty $source "CleanupError" $_.Exception.Message
            $checkResult = "failed"
            Write-FpfJson $context.SourceStatePath $source
        }
    }

    $policyHash = Get-FpfPolicyHash $context.RepositoryRoot
    $accepted = $null; $candidate = $null; $selected = $null; $unavailableAccepted = $null
    $isMigrationRequired = $null -ne $source -and (Get-FpfProperty $source "IndexVersion" 0) -ne $script:FpfIndexVersion
    if ($null -ne $context.Adoption -and $context.Adoption.AcceptedRevision) {
        $pin = $context.Adoption.AcceptedRevision
        $hashes = Get-FpfProperty $source "IndexHashes"
        if (-not $isMigrationRequired -and (Get-FpfProperty $hashes $pin)) { $accepted = Get-FpfManifest $context $pin -VerifyFiles }
        else { $unavailableAccepted = $pin }
    }
    if ($null -ne $source -and $source.CandidateRevision -and -not $isMigrationRequired) {
        $candidate = Get-FpfManifest $context $source.CandidateRevision -VerifyFiles
    }
    if ($isEnsure -and $checkResult -ne "failed" -and $null -ne $candidate -and $null -eq $unavailableAccepted -and
        (-not [IO.File]::Exists($context.AdoptionPath) -or $context.Adoption.CacheRoot -ne $context.CacheRoot)) {
        $context.Adoption.CacheRoot = $context.CacheRoot
        Write-FpfJson $context.AdoptionPath $context.Adoption
    }
    if ($Action -eq "Accept") {
        if ($null -eq $candidate -or $Revision -cne $candidate.Revision) {
            throw "The reviewed revision is no longer the current validated candidate; inspect Status before accepting."
        }
        # Temporarily protect interrupted adoption without replacing the most recent
        # other edition. Later refresh still obeys the cache-wide three-edition cap.
        $oldAdoption = if ($null -ne $accepted -and $context.Adoption.CacheRoot -eq $context.CacheRoot -and
            $accepted.Revision -cne $Revision) { $accepted.Revision } else { $null }
        Set-FpfProperty $source "PendingAcceptanceOldRevision" $oldAdoption
        Set-FpfProperty $source "RetentionRevision" $Revision
        Write-FpfJson $context.SourceStatePath $source
        $context.Adoption.CacheRoot = $context.CacheRoot
        $context.Adoption.AcceptedRevision = $Revision
        $context.Adoption.AcceptedPolicyHash = $policyHash
        $context.Adoption.AcceptedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        $context.Adoption.ReviewNote = $ReviewNote.Trim()
        Write-FpfJson $context.AdoptionPath $context.Adoption
        Set-FpfProperty $source "PendingAcceptanceOldRevision" $null
        Write-FpfJson $context.SourceStatePath $source
        $accepted = $candidate
        $unavailableAccepted = $null
    }
    if ($Action -eq "Select") { $selected = Get-FpfManifest $context $Revision -VerifyFiles }
    elseif ($null -ne $accepted) { $selected = $accepted }
    $policyChanged = $null -ne $accepted -and $context.Adoption.AcceptedPolicyHash -cne $policyHash
    $reviewRequired = $null -ne $candidate -and ($null -eq $accepted -or $candidate.Revision -cne $accepted.Revision -or $policyChanged)
    $status = if ($Action -eq "Select") { "selected" } elseif ($Action -eq "Accept") { "accepted" }
        elseif ($reviewRequired) { "needs-review" } elseif ($null -eq $accepted) { "unavailable" }
        elseif ($null -ne $source -and ($source.LastError -or (Get-FpfProperty $source "CleanupError"))) { "stale" } else { "ready" }
    $changes = if ($reviewRequired) { Get-FpfChangePage $accepted $candidate $context.RepositoryRoot } else { $null }
    [pscustomobject]@{
        Status = $status; CheckResult = $checkResult; CacheRoot = $context.CacheRoot
        CacheTransitionPending = ($null -ne $context.Adoption -and $context.Adoption.CacheRoot -ne $context.CacheRoot)
        Source = $(if ($null -ne $source) { $source.Source } else { $script:FpfUpstream })
        AcceptedRevision = $(if ($null -ne $accepted) { $accepted.Revision } else { $null })
        UnavailableAcceptedRevision = $unavailableAccepted
        CandidateRevision = $(if ($null -ne $source) { $source.CandidateRevision } else { $null })
        SelectedRevision = $(if ($null -ne $selected) { $selected.Revision } else { $null })
        SelectedIsAccepted = ($null -ne $selected -and $null -ne $accepted -and $selected.Revision -ceq $accepted.Revision)
        RejectedRevision = $(if ($null -ne $source) { $source.RejectedRevision } else { $null })
        LastAttemptUtc = $(if ($null -ne $source) { $source.LastAttemptUtc } else { $null })
        LastSuccessUtc = $(if ($null -ne $source) { $source.LastSuccessUtc } else { $null })
        PolicyHash = $policyHash; PolicyChanged = $policyChanged; ReviewRequired = $reviewRequired
        IndexVersion = (Get-FpfProperty $source "IndexVersion" 1); MigrationRequired = $isMigrationRequired
        CleanupPending = (Get-FpfProperty $source "CleanupPending" $false)
        Changes = $changes
        Notice = $(if ($isMigrationRequired) { "FPF cache migration requires writable Ensure." }
            elseif (Get-FpfProperty $source "CleanupError") { Get-FpfProperty $source "CleanupError" }
            elseif ($null -ne $source) { $source.LastError } else { "No local FPF source is available." })
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
