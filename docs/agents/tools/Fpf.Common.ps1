# Shared implementation for the FPF helpers. No upstream code is executed.
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:FpfUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$script:FpfUpstream = "https://github.com/ailev/FPF.git"
$script:FpfRequired = @(
    "FPF-Spec.md", "Readme.md", "USING-FPF.md", "LICENSE", "LICENSING.md",
    "Engineering DPF Suite/README.md", "Engineering DPF Suite/ENGINEERING-DPF-SUITE-REFERENCE.md",
    "Engineering DPF Suite/SYSTEMS-ENGINEERING-PRINCIPLES-FRAMEWORK.md",
    "Engineering DPF Suite/METHOD-ENGINEERING-PRINCIPLES-FRAMEWORK.md",
    "Engineering DPF Suite/PROBLEM-STRUCTURING-AND-DECISION-SUPPORT-PRINCIPLES-FRAMEWORK.md",
    "Foundational Thinking DPF Suite/README.md",
    "Foundational Thinking DPF Suite/FOUNDATIONAL-THINKING-DPF-SUITE-REFERENCE.md",
    "Foundational Thinking DPF Suite/COMPUTATIONAL-THINKING-DPF.md"
)

function Get-FpfHash {
    param([string]$Text)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($script:FpfUtf8.GetBytes($Text)))).Replace("-", "").ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

function ConvertTo-FpfArgument {
    param([AllowEmptyString()][string]$Value)
    # Quote Windows native argv, including embedded quotes and trailing backslashes.
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Invoke-FpfGit {
    param([string[]]$Arguments, [string]$GitDirectory, [int]$TimeoutSeconds = 45, [switch]$AllowFailure)
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command git -ErrorAction Stop).Source
    $prefix = @("-c", "credential.helper=", "-c", "core.hooksPath=NUL", "-c", "gc.auto=0",
        "-c", "maintenance.auto=false", "-c", "http.followRedirects=false",
        "-c", "protocol.allow=never", "-c", "protocol.https.allow=always", "-c", "protocol.file.allow=always")
    if ($GitDirectory) { $prefix += "--git-dir=$GitDirectory" }
    $start.Arguments = (($prefix + $Arguments | ForEach-Object { ConvertTo-FpfArgument $_ }) -join " ")
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($key in @($start.EnvironmentVariables.Keys)) {
        if ([string]$key -match '^GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|CONFIG.*)$') {
            $start.EnvironmentVariables.Remove([string]$key)
        }
    }
    $nullFile = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { "NUL" } else { "/dev/null" }
    $start.EnvironmentVariables["GIT_CONFIG_NOSYSTEM"] = "1"
    $start.EnvironmentVariables["GIT_CONFIG_GLOBAL"] = $nullFile
    $start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0"
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $start
    $buffer = [System.IO.MemoryStream]::new()
    try {
        [void]$process.Start()
        $outputTask = $process.StandardOutput.BaseStream.CopyToAsync($buffer)
        $errorTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill() } catch { }
            throw "Git operation timed out; the accepted FPF revision was not changed."
        }
        [void]$outputTask.GetAwaiter().GetResult()
        $errorText = $errorTask.GetAwaiter().GetResult()
        $result = [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = $script:FpfUtf8.GetString($buffer.ToArray())
            Error = $errorText
        }
        if ($result.ExitCode -ne 0 -and -not $AllowFailure) {
            throw "Git operation '$($Arguments[0])' failed (exit $($result.ExitCode))."
        }
        return $result
    }
    finally { $buffer.Dispose(); $process.Dispose() }
}

function Assert-FpfDirectory {
    param([string]$Path)
    $cursor = [System.IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw "FPF cache paths must not traverse a symlink or junction."
            }
        }
        $parent = [System.IO.Path]::GetDirectoryName($cursor)
        if ($parent -eq $cursor) { break }
        $cursor = $parent
    }
}

function Read-FpfJson {
    param([string]$Path)
    if (-not [System.IO.File]::Exists($Path)) { return $null }
    $value = $script:FpfUtf8.GetString([System.IO.File]::ReadAllBytes($Path)) | ConvertFrom-Json
    if ($null -eq $value -or $null -eq $value.PSObject.Properties["SchemaVersion"] -or $value.SchemaVersion -ne 1) {
        throw "Unsupported or corrupt FPF state. Preserve the cache and inspect it before repair."
    }
    # PowerShell 7 may deserialize ISO timestamps into local DateTime values;
    # Windows PowerShell retains strings. Keep persisted and displayed state in UTC.
    foreach ($property in $value.PSObject.Properties) {
        if ($property.Name.EndsWith("Utc") -and ($property.Value -is [DateTime] -or $property.Value -is [DateTimeOffset])) {
            $property.Value = $property.Value.ToUniversalTime().ToString("o")
        }
    }
    return $value
}

function Write-FpfJson {
    param([string]$Path, $Value)
    Assert-FpfDirectory ([System.IO.Path]::GetDirectoryName($Path))
    $temporary = $Path + "." + [guid]::NewGuid().ToString("N") + ".tmp"
    try {
        $json = ($Value | ConvertTo-Json -Depth 12) -replace "\r?\n", "`r`n"
        [System.IO.File]::WriteAllText($temporary, $json + "`r`n", $script:FpfUtf8)
        if ([System.IO.File]::Exists($Path)) { [System.IO.File]::Replace($temporary, $Path, [System.Management.Automation.Language.NullString]::Value) }
        else { [System.IO.File]::Move($temporary, $Path) }
    }
    finally { if ([System.IO.File]::Exists($temporary)) { [System.IO.File]::Delete($temporary) } }
}

function Open-FpfLock {
    param([string]$Path, [int]$TimeoutSeconds = 30)
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    do {
        try { return [System.IO.File]::Open($Path, "OpenOrCreate", "ReadWrite", "None") }
        catch [System.IO.IOException] {
            if ($timer.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw "FPF cache is busy; retry later or use its accepted revision read-only." }
            Start-Sleep -Milliseconds 100
        }
    } while ($true)
}

function Get-FpfContext {
    param([string]$RepositoryRoot, [switch]$LocalCache)
    if (-not $RepositoryRoot) { $RepositoryRoot = Join-Path $PSScriptRoot "../../.." }
    $root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]"\/")
    if (-not [System.IO.Directory]::Exists($root)) { throw "RepositoryRoot must be an existing directory." }
    $local = Join-Path $root ".codex-local/fpf"
    $shared = $local
    $common = Invoke-FpfGit -Arguments @("-C", $root, "rev-parse", "--path-format=absolute", "--git-common-dir") -AllowFailure
    if ($common.ExitCode -eq 0) {
        $commonPath = $common.Output.Trim()
        if ([System.IO.Path]::GetFileName($commonPath) -eq ".git") {
            $shared = Join-Path ([System.IO.Path]::GetDirectoryName($commonPath)) ".codex-local/fpf"
        }
    }
    Assert-FpfDirectory $local
    $adoptionPath = Join-Path $local "adoption.json"
    $adoption = Read-FpfJson $adoptionPath
    $cache = $shared
    if ($LocalCache -or ($null -ne $adoption -and $adoption.CacheRoot -eq $local)) { $cache = $local }
    if ($null -ne $adoption -and $adoption.CacheRoot -notin @($local, $shared)) {
        throw "Adoption state points outside this repository's permitted FPF caches."
    }
    Assert-FpfDirectory $cache
    return [pscustomobject]@{
        RepositoryRoot = $root; LocalRoot = $local; CacheRoot = $cache
        AdoptionPath = $adoptionPath; Adoption = $adoption
        SourceStatePath = (Join-Path $cache "source.json")
        GitDirectory = (Join-Path $cache "corpus.git")
    }
}

function Get-FpfPolicyHash {
    param([string]$RepositoryRoot)
    $parts = foreach ($relative in @("docs/agents/fpf.md", "docs/agents/fpf-profile.md", "docs/agents/fpf-app.md",
            "docs/agents/tools/Fpf.Common.ps1", "docs/agents/tools/Sync-Fpf.ps1", "docs/agents/tools/Read-Fpf.ps1")) {
        $path = Join-Path $RepositoryRoot $relative
        if ([System.IO.File]::Exists($path)) { $relative + "`n" + $script:FpfUtf8.GetString([System.IO.File]::ReadAllBytes($path)) }
    }
    return Get-FpfHash ($parts -join "`n")
}

function Get-FpfSource {
    param([string]$SourcePath)
    if (-not $SourcePath) { return $script:FpfUpstream }
    $path = [System.IO.Path]::GetFullPath($SourcePath)
    if (-not [System.IO.Directory]::Exists($path)) { throw "SourcePath must name an explicitly selected local Git repository." }
    return ([Uri]($path.TrimEnd([char[]]"\/") + "/")).AbsoluteUri.TrimEnd("/")
}

function Assert-FpfRepository {
    param($Context, [string]$Source)
    $bare = Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("rev-parse", "--is-bare-repository")
    $origin = Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("config", "--local", "--get", "remote.origin.url")
    if ($bare.Output.Trim() -ne "true" -or $origin.Output.Trim() -cne $Source) {
        throw "FPF cache repository identity does not match its recorded source."
    }
    if ([System.IO.File]::Exists((Join-Path $Context.GitDirectory "objects/info/alternates"))) {
        throw "FPF cache must own its Git objects; alternate object directories are unsupported."
    }
}

function Assert-FpfRevision {
    param([string]$Revision)
    if ($Revision -cnotmatch '^[0-9a-f]{40}$') { throw "Revision must be a complete lowercase 40-character commit SHA." }
}

function Get-FpfManifest {
    param($Context, [string]$Revision)
    Assert-FpfRevision $Revision
    $manifest = Read-FpfJson (Join-Path $Context.CacheRoot ("indexes/" + $Revision + ".json"))
    if ($null -eq $manifest -or $manifest.Revision -cne $Revision) { throw "Revision is not a validated snapshot in this cache." }
    Assert-FpfRepository $Context $manifest.Source
    $actual = Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("rev-parse", "--verify", "$Revision^{commit}")
    if ($actual.Output.Trim() -cne $Revision) { throw "Cached commit identity does not match the selected revision." }
    return $manifest
}

function Get-FpfText {
    param($Context, [string]$Revision, [string]$Path)
    if ($Path -match '(^/|\\|(^|/)\.\.?(/|$)|[:\x00-\x1f])') { throw "Path must be a relative publication path from the source catalog." }
    return (Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("show", ($Revision + ":" + $Path))).Output
}

function Get-FpfHeadings {
    param([string[]]$Lines)
    $headings = [System.Collections.Generic.List[object]]::new()
    $fence = ""; $fenceLength = 0
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i].TrimEnd("`r")
        $fm = [regex]::Match($line, '^ {0,3}(?<fence>`{3,}|~{3,})')
        if ($fence) {
            if ($fm.Success -and $fm.Groups["fence"].Value[0] -eq $fence[0] -and
                $fm.Groups["fence"].Length -ge $fenceLength -and $line.Substring($fm.Length).Trim().Length -eq 0) { $fence = "" }
            continue
        }
        if ($fm.Success) { $fence = $fm.Groups["fence"].Value; $fenceLength = $fence.Length; continue }
        $hm = [regex]::Match($line, '^(?<marks>#{1,6})[ \t]+(?<title>.+?)(?:[ \t]+#+)?[ \t]*$')
        if (-not $hm.Success) { continue }
        $level = $hm.Groups["marks"].Length
        $title = $hm.Groups["title"].Value
        $id = $null
        if ($level -eq 2 -and $title -cmatch '^([A-Z][A-Za-z0-9]*(?:\.[A-Za-z0-9_-]+)+)(?:\s+[-\u2013\u2014]|\s*$)') { $id = $Matches[1] }
        $headings.Add([pscustomobject]@{ Level = $level; Title = $title; Line = $i + 1; EndLine = $Lines.Count; Id = $id; Hash = $null })
    }
    $stack = [System.Collections.Generic.Stack[object]]::new()
    foreach ($heading in $headings) {
        while ($stack.Count -gt 0 -and $stack.Peek().Level -ge $heading.Level) { $stack.Pop().EndLine = $heading.Line - 1 }
        $stack.Push($heading)
    }
    foreach ($heading in $headings) {
        if ($heading.Id) { $heading.Hash = Get-FpfHash (($Lines[($heading.Line - 1)..($heading.EndLine - 1)]) -join "`n") }
    }
    return $headings.ToArray()
}

function New-FpfManifest {
    param($Context, [string]$Revision, [string]$Source)
    Assert-FpfRevision $Revision
    $tree = (Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("ls-tree", "-r", "-z", "--full-tree", $Revision)).Output
    $files = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $tree.Split([char]0)) {
        if (-not $entry) { continue }
        if ($entry -notmatch '^(?<mode>100644|100755) blob (?<blob>[0-9a-f]{40})\t(?<path>.+)$') {
            throw "Unsupported Git tree entry; inspect the candidate before changing the integration."
        }
        $path = $Matches["path"]; $blob = $Matches["blob"]
        if ($path -notmatch '(?i)(\.md$|(^|/)LICENSE$)') { continue }
        $text = Get-FpfText $Context $Revision $path
        if ([string]::IsNullOrWhiteSpace($text)) { throw "Publication is empty: $path" }
        $lines = @($text -split "`n")
        $headings = @(Get-FpfHeadings $lines)
        $files.Add([pscustomobject]@{ Path = $path; Blob = $blob; Lines = $lines.Count; Headings = $headings })
    }
    foreach ($required in $script:FpfRequired) {
        $file = @($files | Where-Object { $_.Path -ceq $required })
        if ($file.Count -ne 1) { throw "Required publication missing or ambiguous: $required" }
        if ($required -ne "LICENSE" -and @($file[0].Headings).Count -eq 0) { throw "Publication has no usable headings: $required" }
    }
    foreach ($path in @("FPF-Spec.md",
            "Engineering DPF Suite/SYSTEMS-ENGINEERING-PRINCIPLES-FRAMEWORK.md",
            "Engineering DPF Suite/METHOD-ENGINEERING-PRINCIPLES-FRAMEWORK.md",
            "Engineering DPF Suite/PROBLEM-STRUCTURING-AND-DECISION-SUPPORT-PRINCIPLES-FRAMEWORK.md",
            "Foundational Thinking DPF Suite/COMPUTATIONAL-THINKING-DPF.md")) {
        $file = $files | Where-Object { $_.Path -ceq $path }
        if (@($file.Headings | Where-Object { $_.Id }).Count -eq 0) { throw "Publication has no recognizable pattern bodies: $path" }
    }
    return [pscustomobject]@{ SchemaVersion = 1; Revision = $Revision; Source = $Source; Files = $files.ToArray() }
}

function Get-FpfChanges {
    param($Previous, $Candidate, [string]$RepositoryRoot)
    $profileText = ""
    foreach ($name in @("fpf-profile.md", "fpf-app.md")) {
        $profilePath = Join-Path $RepositoryRoot ("docs/agents/" + $name)
        if ([IO.File]::Exists($profilePath)) { $profileText += $script:FpfUtf8.GetString([IO.File]::ReadAllBytes($profilePath)) + " " }
    }
    $profileIds = @{}
    foreach ($match in [regex]::Matches($profileText, '(?<![A-Za-z0-9_.])([A-Z][A-Za-z0-9]*(?:\.[A-Za-z0-9_-]+)+)(?![A-Za-z0-9_.])')) {
        $profileIds[$match.Value] = $true
    }
    $changes = [System.Collections.Generic.List[object]]::new()
    $oldFiles = @{}; $newFiles = @{}
    if ($null -ne $Previous) { foreach ($file in $Previous.Files) { $oldFiles[$file.Path] = $file } }
    foreach ($file in $Candidate.Files) { $newFiles[$file.Path] = $file }
    foreach ($path in @(@($oldFiles.Keys) + @($newFiles.Keys) | Sort-Object -Unique)) {
        $old = $oldFiles[$path]; $new = $newFiles[$path]
        if ($null -ne $old -and $null -ne $new -and $old.Blob -eq $new.Blob) { continue }
        $kind = if ($null -eq $old) { "added" } elseif ($null -eq $new) { "removed" } else { "modified" }
        $attention = if ($path -match '(^|/)(LICENSE|LICENSING\.md)$') { "licensing" }
            elseif ($path -match '(^|/)(USING-FPF\.md|Readme\.md|README\.md)$') { "instructions-or-navigation" }
            else { "publication" }
        $profileAffected = $script:FpfRequired -ccontains $path -or $profileText.Contains($path)
        foreach ($file in @($old, $new)) {
            if ($null -ne $file) {
                foreach ($heading in $file.Headings) {
                    if ($heading.Id -and $profileIds.ContainsKey($heading.Id)) { $profileAffected = $true }
                }
            }
        }
        $changes.Add([pscustomobject]@{ Path = $path; Kind = $kind; Attention = $attention; ProfileAffected = $profileAffected })
    }
    $patternChanges = [System.Collections.Generic.List[object]]::new()
    $orderedChanges = @($changes | Sort-Object @{ Expression = {
        if ($_.Attention -eq "licensing") { 0 } elseif ($_.Attention -eq "instructions-or-navigation") { 1 } elseif ($_.ProfileAffected) { 2 } else { 3 }
    } }, Path)
    foreach ($change in $orderedChanges) {
        $oldIds = @{}; $newIds = @{}
        if ($oldFiles.ContainsKey($change.Path)) { foreach ($h in $oldFiles[$change.Path].Headings) { if ($h.Id) { $oldIds[$h.Id] = $h.Hash } } }
        if ($newFiles.ContainsKey($change.Path)) { foreach ($h in $newFiles[$change.Path].Headings) { if ($h.Id) { $newIds[$h.Id] = $h.Hash } } }
        foreach ($id in @(@($oldIds.Keys) + @($newIds.Keys) | Sort-Object -Unique)) {
            if ($oldIds[$id] -ne $newIds[$id]) {
                $kind = if (-not $oldIds.ContainsKey($id)) { "added" } elseif (-not $newIds.ContainsKey($id)) { "removed" } else { "modified" }
                $patternChanges.Add([pscustomobject]@{ Path = $change.Path; PatternId = $id; Kind = $kind })
            }
        }
    }
    return [pscustomobject]@{
        FileCount = $changes.Count; Files = @($orderedChanges | Select-Object -First 40)
        PatternCount = $patternChanges.Count; Patterns = @($patternChanges | Select-Object -First 40)
        Truncated = ($changes.Count -gt 40 -or $patternChanges.Count -gt 40)
    }
}
