<#
.SYNOPSIS
Searches or reads a bounded part of one validated, commit-pinned FPF snapshot.
.DESCRIPTION
Read-only: never fetches, accepts revisions, executes upstream files, or emits an entire
publication without a bounded page. Pattern and heading lookup excludes fenced examples.
#>
[CmdletBinding()]
param(
    [ValidateSet("List", "Search", "Read")][string]$Action = "Read",
    [Parameter(Mandatory = $true)][string]$Revision,
    [string]$Query,
    [string]$Path,
    [string]$PatternId,
    [string]$Heading,
    [ValidateRange(0, 2147483647)][int]$StartLine = 0,
    [ValidateRange(1, 2147483647)][int]$StartColumn = 1,
    [ValidateRange(0, 2147483647)][int]$Offset = 0,
    [ValidateRange(1, 500)][int]$MaxLines = 120,
    [ValidateRange(128, 32000)][int]$MaxChars = 12000,
    [string]$RepositoryRoot,
    [switch]$LocalCache
)
. (Join-Path $PSScriptRoot "Fpf.Common.ps1")
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

try {
    $context = Get-FpfContext $RepositoryRoot -LocalCache:$LocalCache
    $manifest = Get-FpfManifest $context $Revision
    $files = @($manifest.Files)
    if ($Path) {
        $files = @($files | Where-Object { $_.Path -ceq $Path })
        if ($files.Count -ne 1) { throw "Publication path is missing or ambiguous; use List to find its exact spelling." }
    }
    $accepted = $null -ne $context.Adoption -and $context.Adoption.AcceptedRevision -ceq $Revision
    $result = [ordered]@{ Revision = $Revision; Source = $manifest.Source; IsAccepted = $accepted; Action = $Action }
    if ($Action -ne "Read") {
        if ($PatternId -or $Heading -or $StartLine -ne 0 -or $StartColumn -ne 1) { throw "Pattern, heading and line selectors apply only to Read." }
        if ($Action -eq "Search" -and [string]::IsNullOrWhiteSpace($Query)) { throw "Search requires a nonempty literal Query." }
        if ($Action -eq "List" -and $Query) { throw "Query applies only to Search." }
        $hits = [System.Collections.Generic.List[object]]::new()
        foreach ($file in $files) {
            if ($Action -eq "List") {
                if ($Path) {
                    foreach ($h in $file.Headings) { $hits.Add([pscustomobject]@{ Path = $file.Path; Line = $h.Line; EndLine = $h.EndLine; Level = $h.Level; PatternId = $h.Id; Title = $h.Title }) }
                }
                else { $hits.Add([pscustomobject]@{ Path = $file.Path; Lines = $file.Lines; HeadingCount = @($file.Headings).Count }) }
            }
            else {
                $lines = @((Get-FpfText $context $Revision $file.Path) -split "`n")
                for ($i = 0; $i -lt $lines.Count; $i++) {
                    $position = $lines[$i].IndexOf($Query, [StringComparison]::OrdinalIgnoreCase)
                    if ($position -ge 0) {
                        $begin = [Math]::Max(0, $position - 60)
                        $length = [Math]::Min(240, $lines[$i].Length - $begin)
                        $hits.Add([pscustomobject]@{ Path = $file.Path; Line = $i + 1; Text = $lines[$i].Substring($begin, $length).TrimEnd("`r") })
                    }
                }
            }
        }
        $items = [System.Collections.Generic.List[object]]::new()
        $characters = 0
        for ($i = $Offset; $i -lt $hits.Count; $i++) {
            $cost = ($hits[$i] | ConvertTo-Json -Compress).Length
            if ($items.Count -ge $MaxLines -or $characters + $cost -gt $MaxChars) { break }
            $items.Add($hits[$i]); $characters += $cost
        }
        if ($items.Count -eq 0 -and $Offset -lt $hits.Count) { throw "One catalog/search item exceeds MaxChars; increase the page budget." }
        $result["Items"] = $items.ToArray()
        $result["Total"] = $hits.Count
        $result["NextOffset"] = $(if ($Offset + $items.Count -lt $hits.Count) { $Offset + $items.Count } else { $null })
        $result["Truncated"] = $null -ne $result["NextOffset"]
    }
    else {
        if ($Query -or $Offset) { throw "Query and Offset apply only to Search/List." }
        if ($PatternId -and $Heading) { throw "Choose PatternId or Heading, not both." }
        $start = 1; $end = 0; $selectedFile = $null
        if ($PatternId -or $Heading) {
            $matchesFound = [System.Collections.Generic.List[object]]::new()
            foreach ($file in $files) {
                foreach ($h in $file.Headings) {
                    if (($PatternId -and $h.Id -ceq $PatternId) -or ($Heading -and $h.Title -ceq $Heading)) {
                        $matchesFound.Add([pscustomobject]@{ File = $file; Heading = $h })
                    }
                }
            }
            if ($matchesFound.Count -ne 1) {
                $locations = @($matchesFound | Select-Object -First 5 | ForEach-Object { "$($_.File.Path):$($_.Heading.Line)" }) -join "; "
                throw "Selector matched $($matchesFound.Count) headings. Specify an exact Path/Heading or inspect List. $locations"
            }
            $selectedFile = $matchesFound[0].File
            $start = $matchesFound[0].Heading.Line
            $end = $matchesFound[0].Heading.EndLine
        }
        else {
            if (-not $Path) { throw "Read requires Path, PatternId or Heading." }
            $selectedFile = $files[0]
            $end = $selectedFile.Lines
            if ($Path -ceq "FPF-Spec.md" -and $StartLine -eq 0) { throw "Core file reads require an explicit StartLine or a pattern/heading selector." }
        }
        if ($StartLine -gt 0) {
            if ($StartLine -lt $start -or $StartLine -gt $end) { throw "StartLine is outside the selected source section." }
            $start = $StartLine
        }
        $lines = @((Get-FpfText $context $Revision $selectedFile.Path) -split "`n")
        if ($end -gt $lines.Count -or $start -lt 1) { throw "The cached index does not match the selected source." }
        $builder = [System.Text.StringBuilder]::new()
        $lineIndex = $start - 1
        $column = $StartColumn - 1
        $emittedLines = 0
        while ($lineIndex -lt $end -and $emittedLines -lt $MaxLines -and $builder.Length -lt $MaxChars) {
            $line = $lines[$lineIndex].TrimEnd("`r")
            if ($lineIndex + 1 -lt $end) { $line += "`n" }
            if ($column -gt $line.Length) { throw "StartColumn is outside the selected line." }
            $take = [Math]::Min($line.Length - $column, $MaxChars - $builder.Length)
            if ($take -gt 0 -and $column + $take -lt $line.Length -and [char]::IsHighSurrogate($line[$column + $take - 1])) { $take-- }
            if ($take -eq 0 -and $column -lt $line.Length) { break }
            [void]$builder.Append($line.Substring($column, $take))
            $column += $take
            $emittedLines++
            if ($column -lt $line.Length) { break }
            $lineIndex++; $column = 0
        }
        $result["Path"] = $selectedFile.Path
        $result["PatternId"] = $PatternId
        $result["Heading"] = $Heading
        $result["StartLine"] = $start
        $result["StartColumn"] = $StartColumn
        $result["SectionEndLine"] = $end
        $result["Content"] = $builder.ToString()
        $result["Truncated"] = $lineIndex -lt $end
        $result["NextLine"] = $(if ($lineIndex -lt $end) { $lineIndex + 1 } else { $null })
        $result["NextColumn"] = $(if ($lineIndex -lt $end) { $column + 1 } else { $null })
        $result["SourceUrl"] = $(if ($manifest.Source -ceq $script:FpfUpstream) {
            $encodedPath = ($selectedFile.Path.Split("/") | ForEach-Object { [Uri]::EscapeDataString($_) }) -join "/"
            "https://github.com/ailev/FPF/blob/$Revision/$encodedPath#L$start"
        } else { $null })
    }
    [pscustomobject]$result | ConvertTo-Json -Depth 8
}
catch {
    [pscustomobject]@{ Status = "error"; Notice = $_.Exception.Message } | ConvertTo-Json -Compress
    exit 1
}
