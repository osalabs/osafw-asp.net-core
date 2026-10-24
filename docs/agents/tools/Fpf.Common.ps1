# Shared implementation for the FPF helpers. No upstream code is executed.
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:FpfUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$script:FpfUpstream = "https://github.com/ailev/FPF.git"
$script:FpfRequired = @(
    "FPF-Spec.md", "Readme.md", "USING-FPF.md", "LICENSE", "LICENSING.md"
)
$script:FpfIndexVersion = 2
$script:FpfFileLimit = 64MB

function Get-FpfIoPath {
    param([string]$Path)
    $full = [IO.Path]::GetFullPath($Path)
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or $full.StartsWith("\\?\")) { return $full }
    if ($full.StartsWith("\\")) { return "\\?\UNC\" + $full.Substring(2) }
    return "\\?\" + $full
}

function Get-FpfProperty {
    param($Object, [string]$Name, $Default = $null)
    if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) { return $Object.$Name }
    return $Default
}

function Set-FpfProperty {
    param($Object, [string]$Name, $Value)
    $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value -Force
}

function Get-FpfBytesHash {
    param([byte[]]$Bytes, [switch]$GitBlob)
    $algorithm = if ($GitBlob) { [Security.Cryptography.SHA1]::Create() } else { [Security.Cryptography.SHA256]::Create() }
    try {
        if ($GitBlob) {
            $prefix = [Text.Encoding]::ASCII.GetBytes("blob " + $Bytes.Length + [char]0)
            [void]$algorithm.TransformBlock($prefix, 0, $prefix.Length, $prefix, 0)
        }
        [void]$algorithm.TransformFinalBlock($Bytes, 0, $Bytes.Length)
        return ([BitConverter]::ToString($algorithm.Hash)).Replace("-", "").ToLowerInvariant()
    }
    finally { $algorithm.Dispose() }
}

function Read-FpfBytes {
    param([string]$Path, [long]$Limit = $script:FpfFileLimit)
    Assert-FpfDirectory $Path
    $ioPath = Get-FpfIoPath $Path
    if ([IO.File]::GetAttributes($ioPath) -band [IO.FileAttributes]::Directory) { throw "FPF input has an unsupported type." }
    $stream = [IO.File]::Open($ioPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        if ($stream.Length -gt $Limit) { throw "FPF input exceeds its size bound." }
        $bytes = [byte[]]::new([int]$stream.Length)
        $offset = 0
        while ($offset -lt $bytes.Length) {
            $count = $stream.Read($bytes, $offset, $bytes.Length - $offset)
            if ($count -eq 0) { throw "FPF input changed while being read." }
            $offset += $count
        }
        return ,$bytes
    }
    finally { $stream.Dispose() }
}

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
    param([string[]]$Arguments, [string]$GitDirectory, [int]$TimeoutSeconds = 45, [switch]$AllowFailure,
        [byte[]]$InputBytes, [switch]$Binary)
    if ($null -ne $InputBytes -and $InputBytes.Length -gt 1MB) { throw "FPF Git batch input exceeds its bound." }
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command git -ErrorAction Stop).Source
    $nullFile = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { "NUL" } else { "/dev/null" }
    $prefix = @("-c", "credential.helper=", "-c", "core.hooksPath=$nullFile", "-c", "core.fsmonitor=false", "-c", "gc.auto=0",
        "-c", "maintenance.auto=false", "-c", "http.followRedirects=false",
        "-c", "protocol.allow=never", "-c", "protocol.https.allow=always", "-c", "protocol.file.allow=always")
    if ($GitDirectory) { $prefix += "--git-dir=$GitDirectory" }
    $start.Arguments = (($prefix + $Arguments | ForEach-Object { ConvertTo-FpfArgument $_ }) -join " ")
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = $null -ne $InputBytes
    foreach ($key in @($start.EnvironmentVariables.Keys)) {
        if ([string]$key -match '^GIT_(DIR|WORK_TREE|INDEX_FILE|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|CONFIG.*)$') {
            $start.EnvironmentVariables.Remove([string]$key)
        }
    }
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
        if ($null -ne $InputBytes) {
            if ($InputBytes.Length -gt 1MB) { throw "FPF Git batch input exceeds its bound." }
            $process.StandardInput.BaseStream.Write($InputBytes, 0, $InputBytes.Length)
            $process.StandardInput.Close()
        }
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill() } catch { }
            throw "Git operation timed out; the accepted FPF revision was not changed."
        }
        [void]$outputTask.GetAwaiter().GetResult()
        $errorText = $errorTask.GetAwaiter().GetResult()
        if ($buffer.Length -gt 128MB) { throw "FPF Git output exceeds its bound." }
        $bytes = $buffer.ToArray()
        $result = [pscustomobject]@{
            ExitCode = $process.ExitCode
            Output = $(if (-not $Binary) { $script:FpfUtf8.GetString($bytes) } else { $null })
            Bytes = $bytes
            Error = $(if ($errorText.Length -gt 8192) { $errorText.Substring(0, 8192) } else { $errorText })
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
        $attributes = $null
        try { $attributes = [IO.File]::GetAttributes((Get-FpfIoPath $cursor)) }
        catch [IO.FileNotFoundException] { }
        catch [IO.DirectoryNotFoundException] { }
        if ($null -ne $attributes) {
            if ($attributes -band [System.IO.FileAttributes]::ReparsePoint) {
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
    Assert-FpfDirectory $Path
    if (-not [System.IO.File]::Exists($Path)) { return $null }
    $value = $script:FpfUtf8.GetString((Read-FpfBytes $Path)) | ConvertFrom-Json
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
    Assert-FpfDirectory $Path
    $temporary = $Path + "." + [guid]::NewGuid().ToString("N") + ".tmp"
    try {
        $json = ($Value | ConvertTo-Json -Depth 12) -replace "\r?\n", "`r`n"
        [System.IO.File]::WriteAllText((Get-FpfIoPath $temporary), $json + "`r`n", $script:FpfUtf8)
        Move-FpfAtomicFile $temporary $Path
    }
    catch { throw ("FPF atomic state write failed for " + [IO.Path]::GetFileName($Path) + ": " + $_.Exception.Message) }
    finally { if ([System.IO.File]::Exists((Get-FpfIoPath $temporary))) { [System.IO.File]::Delete((Get-FpfIoPath $temporary)) } }
}

function Move-FpfAtomicFile {
    param([string]$Temporary, [string]$Path)
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        try {
            Assert-FpfDirectory $Path
            if ([IO.File]::Exists((Get-FpfIoPath $Path))) { [IO.File]::Replace((Get-FpfIoPath $Temporary), (Get-FpfIoPath $Path), [Management.Automation.Language.NullString]::Value) }
            else { [IO.File]::Move((Get-FpfIoPath $Temporary), (Get-FpfIoPath $Path)) }
            return
        }
        catch {
            $cause = $_.Exception
            while ($null -ne $cause.InnerException) { $cause = $cause.InnerException }
            # Windows scanners/readers can briefly prevent ReplaceFile. Retry the
            # atomic operation only; never delete the destination as a workaround.
            if ($attempt -eq 9 -or ($cause.HResult -band 0xffff) -notin @(32, 33, 1175)) { throw }
            Start-Sleep -Milliseconds (50 * ($attempt + 1))
        }
    }
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
    Assert-FpfDirectory $root
    $gitPath = Join-Path $root ".git"
    Assert-FpfDirectory $gitPath
    if ([IO.File]::Exists($gitPath)) {
        $gitText = $script:FpfUtf8.GetString((Read-FpfBytes $gitPath 65536)).Trim()
        if ($gitText -notmatch '^gitdir: (.+)$') { throw "Invalid checkout Git-directory pointer." }
        $gitPath = $Matches[1]
        if (-not [IO.Path]::IsPathRooted($gitPath)) { $gitPath = Join-Path $root $gitPath }
        $gitPath = [IO.Path]::GetFullPath($gitPath)
    }
    Assert-FpfDirectory $gitPath
    $commonFile = Join-Path $gitPath "commondir"
    Assert-FpfDirectory $commonFile
    if ([IO.File]::Exists($commonFile)) {
        $commonPath = $script:FpfUtf8.GetString((Read-FpfBytes $commonFile 65536)).Trim()
        if (-not [IO.Path]::IsPathRooted($commonPath)) { $commonPath = Join-Path $gitPath $commonPath }
        $commonPath = [IO.Path]::GetFullPath($commonPath).TrimEnd([char[]]"\/")
        Assert-FpfDirectory $commonPath
        if ([IO.Path]::GetFileName($commonPath) -ne ".git") { throw "Unsupported checkout common directory." }
        $shared = Join-Path ([IO.Path]::GetDirectoryName($commonPath)) ".codex-local/fpf"
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
    Assert-FpfDirectory $Context.GitDirectory
    $configPath = Join-Path $Context.GitDirectory "config"
    $configText = $script:FpfUtf8.GetString((Read-FpfBytes $configPath 65536))
    if ($configText -match '(?im)^\s*\[\s*(include|includeIf|filter|credential|url)\b' -or
        $configText -match '(?im)^\s*(recentObjectsHook|repackFilter|repackFilterTo|alternateRefsCommand|sshCommand|gitProxy|fsmonitor|worktreeConfig)\s*=') {
        throw "FPF cache refuses external or executable Git configuration."
    }
    $bare = Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("rev-parse", "--is-bare-repository")
    $origin = Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("config", "--local", "--get", "remote.origin.url")
    if ($bare.Output.Trim() -ne "true" -or $origin.Output.Trim() -cne $Source) {
        throw "FPF cache repository identity does not match its recorded source."
    }
    foreach ($relative in @("objects", "objects/info", "objects/info/alternates", "objects/info/http-alternates")) {
        Assert-FpfDirectory (Join-Path $Context.GitDirectory $relative)
    }
    if ([System.IO.File]::Exists((Join-Path $Context.GitDirectory "objects/info/alternates")) -or
        [System.IO.File]::Exists((Join-Path $Context.GitDirectory "objects/info/http-alternates"))) {
        throw "FPF cache must own its Git objects; alternate object directories are unsupported."
    }
}

function Assert-FpfRevision {
    param([string]$Revision)
    if ($Revision -cnotmatch '^[0-9a-f]{40}$') { throw "Revision must be a complete lowercase 40-character commit SHA." }
}

function Get-FpfManifest {
    param($Context, [string]$Revision, [switch]$VerifyFiles)
    Assert-FpfRevision $Revision
    $source = Read-FpfJson $Context.SourceStatePath
    $hashes = Get-FpfProperty $source "IndexHashes"
    $checksum = Get-FpfProperty $hashes $Revision
    if (-not $checksum) {
        $legacy = Join-Path $Context.CacheRoot ("indexes/" + $Revision + ".json")
        Assert-FpfDirectory $legacy
        if ([IO.File]::Exists($legacy)) { throw "FPF cache migration requires writable Ensure; read-only commands never repair it." }
        throw "Revision is not a validated snapshot in this cache; the pin may have expired."
    }
    $indexPath = Get-FpfIndexPath $Context $Revision
    $bytes = Read-FpfBytes $indexPath
    if ((Get-FpfBytesHash $bytes) -cne $checksum) { throw "FPF index checksum mismatch; use writable Ensure for recovery." }
    $manifest = $script:FpfUtf8.GetString($bytes) | ConvertFrom-Json
    if ($manifest.SchemaVersion -ne 1 -or $manifest.IndexVersion -ne $script:FpfIndexVersion -or
        $manifest.Revision -cne $Revision -or $manifest.Source -cne $source.Source) {
        throw "FPF index identity does not match the selected source."
    }
    $seen = @{}
    foreach ($file in $manifest.Files) {
        Assert-FpfPublicationPath $file.Path
        Assert-FpfRevision $file.Blob
        if ($seen.ContainsKey($file.Path)) { throw "Ambiguous publication paths in FPF index." }
        $seen[$file.Path] = $true
        if ($file.Bytes -lt 1 -or $file.Bytes -gt $script:FpfFileLimit -or $file.Lines -lt 1) { throw "Invalid FPF file metadata." }
        if ($VerifyFiles) { [void](Get-FpfPublicationBytes $Context $Revision $file) }
    }
    return $manifest
}

function Get-FpfIndexPath {
    param($Context, [string]$Revision)
    Assert-FpfRevision $Revision
    return Join-Path $Context.CacheRoot ("indexes/v2/" + $Revision + ".json")
}

function Assert-FpfPublicationPath {
    param([string]$Path)
    if (-not $Path -or $Path -match '(^/|\\|(^|/)\.\.?(/|$)|[:\x00-\x1f])') { throw "Path must be a relative publication path from the source catalog." }
    foreach ($part in $Path.Split("/")) {
        if (-not $part -or $part -match '[. ]$|[<>"|?*]' -or $part -match '^(?i:CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)') {
            throw "Publication path has an unsafe or ambiguous filesystem spelling."
        }
    }
}

function Get-FpfPublicationBytes {
    param($Context, [string]$Revision, $File)
    Assert-FpfRevision $Revision
    Assert-FpfPublicationPath $File.Path
    $path = Join-Path $Context.CacheRoot ("snapshots/" + $Revision + "/" + $File.Path)
    $bytes = Read-FpfBytes $path
    if ($bytes.Length -ne $File.Bytes -or (Get-FpfBytesHash $bytes -GitBlob) -cne $File.Blob) {
        throw "FPF publication differs from its pinned Git blob; use writable Ensure for recovery."
    }
    return ,$bytes
}

function Get-FpfText {
    param($Context, [string]$Revision, [string]$Path, $Manifest = $null)
    if ($null -eq $Manifest) { $Manifest = Get-FpfManifest $Context $Revision }
    $files = @($Manifest.Files | Where-Object { $_.Path -ceq $Path })
    if ($files.Count -ne 1) { throw "Publication path is missing or ambiguous; use List to find its exact spelling." }
    return $script:FpfUtf8.GetString((Get-FpfPublicationBytes $Context $Revision $files[0]))
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
    Assert-FpfRepository $Context $Source
    $actual = (Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("rev-parse", "--verify", "$Revision^{commit}")).Output.Trim()
    if ($actual -cne $Revision) { throw "Cached commit identity does not match the selected revision." }
    $tree = (Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("ls-tree", "-r", "-l", "-z", "--full-tree", $Revision)).Output
    $entries = [Collections.Generic.List[object]]::new()
    $seen = @{}
    foreach ($entry in $tree.Split([char]0)) {
        if (-not $entry) { continue }
        if ($entry -notmatch '^(?<mode>100644|100755) blob (?<blob>[0-9a-f]{40})\s+(?<size>[0-9]+)\t(?<path>.+)$') {
            throw "Unsupported Git tree entry; inspect the candidate before changing the integration."
        }
        $path = $Matches["path"]; $blob = $Matches["blob"]; $size = [long]$Matches["size"]
        if ($path -notmatch '(?i)(\.md$|(^|/)LICENSE$)') { continue }
        Assert-FpfPublicationPath $path
        if ($seen.ContainsKey($path)) { throw "Publication paths alias on a supported filesystem." }
        if ($size -lt 1 -or $size -gt $script:FpfFileLimit) { throw "Publication is empty or exceeds the supported size." }
        $seen[$path] = $true
        $entries.Add([pscustomobject]@{ Path = $path; Blob = $blob; Bytes = $size })
    }
    foreach ($required in $script:FpfRequired) {
        if (@($entries | Where-Object { $_.Path -ceq $required }).Count -ne 1) { throw "Required publication missing or ambiguous: $required" }
    }
    if (@($entries | Where-Object { $_.Path.StartsWith("DPFs/") }).Count -gt 0) {
        foreach ($required in @("FPF-LIBRARY-REFERENCE.md", "DPFs/README.md")) {
            if (@($entries | Where-Object { $_.Path -ceq $required }).Count -ne 1) { throw "Required publication missing or ambiguous: $required" }
        }
    }
    $files = [System.Collections.Generic.List[object]]::new()
    $offset = 0
    while ($offset -lt $entries.Count) {
        $batch = [Collections.Generic.List[object]]::new(); $totalBytes = 0L
        while ($offset -lt $entries.Count -and $batch.Count -lt 1024) {
            $entry = $entries[$offset]
            if ($batch.Count -gt 0 -and $totalBytes + $entry.Bytes -gt 32MB) { break }
            $batch.Add($entry); $totalBytes += $entry.Bytes; $offset++
        }
        $inputBytes = [Text.Encoding]::ASCII.GetBytes((@($batch | ForEach-Object { $_.Blob }) -join "`n") + "`n")
        $result = Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("cat-file", "--batch") -InputBytes $inputBytes -Binary
        $cursor = 0
        foreach ($entry in $batch) {
            $headerEnd = [Array]::IndexOf($result.Bytes, [byte]10, $cursor)
            if ($headerEnd -lt $cursor -or $headerEnd - $cursor -gt 128) { throw "Invalid Git batch header." }
            $header = [Text.Encoding]::ASCII.GetString($result.Bytes, $cursor, $headerEnd - $cursor)
            if ($header -cne ($entry.Blob + " blob " + $entry.Bytes)) { throw "Git batch identity or size mismatch." }
            $cursor = $headerEnd + 1
            if ($cursor + $entry.Bytes -ge $result.Bytes.Length -or $result.Bytes[$cursor + $entry.Bytes] -ne 10) { throw "Truncated Git batch body." }
            $bytes = [byte[]]::new([int]$entry.Bytes)
            [Array]::Copy($result.Bytes, $cursor, $bytes, 0, $bytes.Length)
            $cursor += $bytes.Length + 1
            if ((Get-FpfBytesHash $bytes -GitBlob) -cne $entry.Blob) { throw "Git blob content does not match its pinned identity." }
            $text = $script:FpfUtf8.GetString($bytes)
            if ([string]::IsNullOrWhiteSpace($text)) { throw "Publication is empty." }
            $lines = @($text -split "`n")
            $headings = @(Get-FpfHeadings $lines)
            $target = Join-Path $Context.CacheRoot ("snapshots/" + $Revision + "/" + $entry.Path)
            Write-FpfBytes $target $bytes
            $files.Add([pscustomobject]@{ Path = $entry.Path; Blob = $entry.Blob; Bytes = $entry.Bytes; Lines = $lines.Count; Headings = $headings })
        }
        if ($cursor -ne $result.Bytes.Length) { throw "Unexpected trailing Git batch output." }
    }
    foreach ($required in $script:FpfRequired) {
        $file = @($files | Where-Object { $_.Path -ceq $required })
        if ($file.Count -ne 1) { throw "Required publication missing or ambiguous: $required" }
        if ($required -ne "LICENSE" -and @($file[0].Headings).Count -eq 0) { throw "Publication has no usable headings: $required" }
    }
    foreach ($path in @("FPF-Spec.md")) {
        $file = $files | Where-Object { $_.Path -ceq $path }
        if (@($file.Headings | Where-Object { $_.Id }).Count -eq 0) { throw "Publication has no recognizable pattern bodies: $path" }
    }
    return [pscustomobject]@{ SchemaVersion = 1; IndexVersion = $script:FpfIndexVersion; Revision = $Revision; Source = $Source; Files = $files.ToArray() }
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
        FileCount = $changes.Count; Files = @($orderedChanges)
        PatternCount = $patternChanges.Count; Patterns = $patternChanges.ToArray()
    }
}

function Write-FpfBytes {
    param([string]$Path, [byte[]]$Bytes)
    Assert-FpfDirectory $Path
    [void][IO.Directory]::CreateDirectory((Get-FpfIoPath ([IO.Path]::GetDirectoryName($Path))))
    $temporary = $Path + "." + [guid]::NewGuid().ToString("N") + ".tmp"
    try {
        [IO.File]::WriteAllBytes((Get-FpfIoPath $temporary), $Bytes)
        Move-FpfAtomicFile $temporary $Path
    }
    finally { if ([IO.File]::Exists((Get-FpfIoPath $temporary))) { [IO.File]::Delete((Get-FpfIoPath $temporary)) } }
}

function Save-FpfSnapshot {
    param($Context, $State, [string]$Revision)
    $manifest = New-FpfManifest $Context $Revision $State.Source
    $path = Get-FpfIndexPath $Context $Revision
    Assert-FpfDirectory $path
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    Write-FpfJson $path $manifest
    $hashes = Get-FpfProperty $State "IndexHashes"
    if ($null -eq $hashes) {
        $hashes = [pscustomobject]@{}
        Set-FpfProperty $State "IndexHashes" $hashes
    }
    $order = @(Get-FpfProperty $State "EditionOrder" @($hashes.PSObject.Properties | ForEach-Object { $_.Name }))
    if ($Revision -notin $order) { $order += $Revision }
    Set-FpfProperty $State "EditionOrder" $order
    Set-FpfProperty $hashes $Revision (Get-FpfBytesHash (Read-FpfBytes $path))
    # Publish reachability before the receipt that makes this edition selectable.
    [void](Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("update-ref", "refs/fpf/snapshots/$Revision", $Revision))
    Write-FpfJson $Context.SourceStatePath $State
}

function Restore-FpfSnapshots {
    param($Context, $State)
    $pins = [Collections.Generic.List[string]]::new()
    $hashes = Get-FpfProperty $State "IndexHashes"
    if ($null -ne $hashes) {
        foreach ($property in $hashes.PSObject.Properties) { $pins.Add($property.Name) }
    }
    $isMigration = (Get-FpfProperty $State "IndexVersion" 0) -ne $script:FpfIndexVersion
    if ($isMigration -and [IO.File]::Exists((Join-Path $Context.GitDirectory "HEAD"))) {
        Assert-FpfRepository $Context $State.Source
        $oldFolder = Join-Path $Context.CacheRoot "indexes"
        Assert-FpfDirectory $oldFolder
        if ([IO.Directory]::Exists($oldFolder)) {
            foreach ($item in @(Get-ChildItem -LiteralPath $oldFolder -File | Sort-Object LastWriteTimeUtc, Name)) {
                if ($item.Name -cmatch '^([0-9a-f]{40})\.json$') { $pins.Add($Matches[1]) }
            }
        }
        $refs = (Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("for-each-ref", "--format=%(objectname)", "refs/fpf/snapshots/")).Output
        foreach ($pin in @($refs -split "\r?\n")) {
            if ($pin) { Assert-FpfRevision $pin; $pins.Add($pin) }
        }
    }
    if ($State.CandidateRevision) { $pins.Add($State.CandidateRevision) }
    # Legacy caches had only checkout-local adoption. Seed shared retention from
    # the most recent accessible calling/primary adoption, without rewriting either.
    if ($null -eq $State.PSObject.Properties["RetentionRevision"]) {
        $adoptions = @($Context.Adoption, (Read-FpfJson (Join-Path $Context.CacheRoot "adoption.json"))) |
            Where-Object { $null -ne $_ -and $_.CacheRoot -eq $Context.CacheRoot -and $_.AcceptedRevision } |
            Sort-Object AcceptedAtUtc -Descending
        $retained = if (@($adoptions).Count -gt 0) { @($adoptions)[0].AcceptedRevision } else { $null }
        Set-FpfProperty $State "RetentionRevision" $retained
    }
    $isAcceptanceCompleted = $null -ne (Get-FpfProperty $State "PendingAcceptanceOldRevision") -and
        $null -ne $Context.Adoption -and $Context.Adoption.CacheRoot -eq $Context.CacheRoot -and
        $Context.Adoption.AcceptedRevision -ceq (Get-FpfProperty $State "RetentionRevision")
    if ($isAcceptanceCompleted) { Set-FpfProperty $State "PendingAcceptanceOldRevision" $null }
    foreach ($pin in @((Get-FpfProperty $State "RetentionRevision"), (Get-FpfProperty $State "PreviousRevision"),
            (Get-FpfProperty $State "PendingAcceptanceOldRevision"))) {
        if ($pin) { $pins.Add($pin) }
    }
    $seen = @{}
    foreach ($pin in $pins) {
        Assert-FpfRevision $pin
        if ($seen.ContainsKey($pin)) { continue }
        $seen[$pin] = $true
        try { [void](Get-FpfManifest $Context $pin -VerifyFiles) }
        catch { Save-FpfSnapshot $Context $State $pin }
    }
    Set-FpfProperty $State "IndexVersion" $script:FpfIndexVersion
    if ($isMigration -or $isAcceptanceCompleted) { Write-FpfJson $Context.SourceStatePath $State }
}

function Get-FpfRetainedRevisions {
    param($State)
    $keep = [Collections.Generic.List[string]]::new()
    foreach ($pin in @((Get-FpfProperty $State "RetentionRevision"), $State.CandidateRevision)) {
        if ($pin -and -not $keep.Contains($pin)) { Assert-FpfRevision $pin; $keep.Add($pin) }
    }
    $hashes = Get-FpfProperty $State "IndexHashes"
    $order = @(Get-FpfProperty $State "EditionOrder" @())
    if ($null -ne $hashes) {
        foreach ($property in $hashes.PSObject.Properties) {
            if ($property.Name -notin $order) { $order += $property.Name }
        }
    }
    $previous = $null
    foreach ($pin in $order) {
        if (-not $keep.Contains($pin) -and (Get-FpfProperty $hashes $pin)) { $previous = $pin }
    }
    Set-FpfProperty $State "PreviousRevision" $previous
    if ($previous) { Assert-FpfRevision $previous; $keep.Add($previous) }
    $pending = Get-FpfProperty $State "PendingAcceptanceOldRevision"
    if ($pending) {
        Assert-FpfRevision $pending
        if (-not $keep.Contains($pending) -and $keep.Count -lt 3) { $keep.Add($pending) }
        else { Set-FpfProperty $State "PendingAcceptanceOldRevision" $null }
    }
    return $keep.ToArray()
}

function Get-FpfTreePaths {
    param([string]$Path)
    # Check every child before descending; recursive enumeration can follow junctions.
    Assert-FpfDirectory $Path
    if ([IO.File]::GetAttributes((Get-FpfIoPath $Path)) -band [IO.FileAttributes]::Directory) {
        foreach ($child in [IO.DirectoryInfo]::new((Get-FpfIoPath $Path)).GetFileSystemInfos()) {
            Get-FpfTreePaths (Join-Path $Path $child.Name)
        }
    }
    [IO.Path]::GetFullPath($Path)
}

function Remove-FpfExpiredEditions {
    param($Context, $State, [switch]$CollectObjects)
    $beforePrevious = Get-FpfProperty $State "PreviousRevision"
    $beforePending = Get-FpfProperty $State "PendingAcceptanceOldRevision"
    $keep = @(Get-FpfRetainedRevisions $State)
    $targets = [Collections.Generic.List[string]]::new()
    foreach ($relative in @("snapshots", "indexes", "indexes/v2")) {
        $folder = Join-Path $Context.CacheRoot $relative
        Assert-FpfDirectory $folder
        if (-not [IO.Directory]::Exists($folder)) { continue }
        foreach ($item in @(Get-ChildItem -LiteralPath $folder -Force)) {
            $pin = $null
            if ($relative -eq "snapshots" -and $item.Name -cmatch '^[0-9a-f]{40}$') { $pin = $item.Name }
            elseif ($relative -ne "snapshots" -and $item.Name -cmatch '^([0-9a-f]{40})\.json$') { $pin = $Matches[1] }
            if ($pin -and ($relative -eq "indexes" -or $pin -notin $keep)) {
                if (($relative -eq "snapshots") -ne $item.PSIsContainer) { throw "FPF cleanup target has an unexpected file type." }
                $targets.Add($item.FullName)
            }
        }
    }
    $hashes = Get-FpfProperty $State "IndexHashes"
    $stale = @(if ($null -ne $hashes) { $hashes.PSObject.Properties | Where-Object { $_.Name -notin $keep } })
    if ($targets.Count -eq 0 -and $stale.Count -eq 0 -and -not $CollectObjects -and -not (Get-FpfProperty $State "CleanupPending" $false)) {
        $hadError = $null -ne (Get-FpfProperty $State "CleanupError")
        if ($hadError) { Set-FpfProperty $State "CleanupError" $null }
        if ($hadError -or $beforePrevious -ne (Get-FpfProperty $State "PreviousRevision") -or
            $beforePending -ne (Get-FpfProperty $State "PendingAcceptanceOldRevision")) { Write-FpfJson $Context.SourceStatePath $State }
        return
    }
    Set-FpfProperty $State "CleanupPending" $true
    Write-FpfJson $Context.SourceStatePath $State
    foreach ($pin in $keep) { [void](Get-FpfManifest $Context $pin -VerifyFiles) }
    Assert-FpfRepository $Context $State.Source
    [void]@(Get-FpfTreePaths $Context.GitDirectory)
    foreach ($pin in $keep) {
        # Recover an older interrupted receipt before removing any temporary refs.
        [void](Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("update-ref", "refs/fpf/snapshots/$pin", $pin))
    }
    $paths = [Collections.Generic.List[string]]::new()
    $rootPrefix = [IO.Path]::GetFullPath($Context.CacheRoot).TrimEnd([char[]]"\/") + [IO.Path]::DirectorySeparatorChar
    foreach ($target in $targets) {
        if (-not [IO.Path]::GetFullPath($target).StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing FPF cleanup outside the cache."
        }
        foreach ($path in @(Get-FpfTreePaths $target)) { $paths.Add($path) }
    }
    $refs = (Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("for-each-ref", "--format=%(refname)", "refs/fpf/snapshots/")).Output
    foreach ($ref in @($refs -split "\r?\n")) {
        if (-not $ref) { continue }
        $pin = $ref.Substring("refs/fpf/snapshots/".Length)
        Assert-FpfRevision $pin
        if ($pin -notin $keep) { [void](Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("update-ref", "-d", $ref)) }
    }
    foreach ($ref in @("refs/fpf/incoming", "refs/remotes/origin/main")) {
        [void](Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @("update-ref", "-d", $ref))
    }
    $fetchHead = Join-Path $Context.GitDirectory "FETCH_HEAD"
    Assert-FpfDirectory $fetchHead
    if ([IO.File]::Exists($fetchHead)) { [IO.File]::Delete($fetchHead) }
    foreach ($path in $paths) {
        Assert-FpfDirectory $path
        if ([IO.Directory]::Exists((Get-FpfIoPath $path))) { [IO.Directory]::Delete((Get-FpfIoPath $path), $false) }
        else { [IO.File]::Delete((Get-FpfIoPath $path)) }
    }
    foreach ($property in $stale) { $hashes.PSObject.Properties.Remove($property.Name) }
    Set-FpfProperty $State "EditionOrder" @(Get-FpfProperty $State "EditionOrder" @() | Where-Object { $_ -in $keep })
    Write-FpfJson $Context.SourceStatePath $State
    [void](Invoke-FpfGit -GitDirectory $Context.GitDirectory -Arguments @(
        "-c", "gc.reflogExpire=now", "-c", "gc.reflogExpireUnreachable=now", "-c", "gc.autoDetach=false", "gc", "--prune=now", "--quiet") -TimeoutSeconds 90)
    Set-FpfProperty $State "CleanupPending" $false
    Set-FpfProperty $State "CleanupError" $null
    Write-FpfJson $Context.SourceStatePath $State
}

function Get-FpfChangePage {
    param($Previous, $Candidate, [string]$RepositoryRoot, [int]$Offset = 0, [int]$MaxLines = 120, [int]$MaxChars = 12000)
    $changes = Get-FpfChanges $Previous $Candidate $RepositoryRoot
    $all = [Collections.Generic.List[object]]::new()
    foreach ($file in $changes.Files) {
        $all.Add([pscustomobject]@{ Type = "File"; Path = $file.Path; Kind = $file.Kind; Attention = $file.Attention; ProfileAffected = $file.ProfileAffected })
    }
    foreach ($pattern in $changes.Patterns) {
        $all.Add([pscustomobject]@{ Type = "Pattern"; Path = $pattern.Path; PatternId = $pattern.PatternId; Kind = $pattern.Kind })
    }
    $items = [Collections.Generic.List[object]]::new(); $characters = 0
    for ($i = $Offset; $i -lt $all.Count; $i++) {
        $cost = ($all[$i] | ConvertTo-Json -Compress).Length
        if ($items.Count -ge $MaxLines -or $characters + $cost -gt $MaxChars) { break }
        $items.Add($all[$i]); $characters += $cost
    }
    if ($items.Count -eq 0 -and $Offset -lt $all.Count) { throw "One change item exceeds MaxChars; increase the page budget." }
    $next = if ($Offset + $items.Count -lt $all.Count) { $Offset + $items.Count } else { $null }
    $from = if ($null -eq $Previous) { "empty" } else { $Previous.Revision }
    return [pscustomobject]@{
        Revision = $Candidate.Revision; ChangesFrom = $from
        FileCount = $changes.FileCount; PatternCount = $changes.PatternCount
        Files = @($items | Where-Object { $_.Type -eq "File" }); Patterns = @($items | Where-Object { $_.Type -eq "Pattern" })
        Items = $items.ToArray(); Total = $all.Count; Offset = $Offset
        Truncated = $null -ne $next; NextOffset = $next
        NextCall = $(if ($null -ne $next) { "-Action List -Revision $($Candidate.Revision) -ChangesFrom $from -Offset $next -MaxLines $MaxLines -MaxChars $MaxChars" } else { $null })
    }
}
