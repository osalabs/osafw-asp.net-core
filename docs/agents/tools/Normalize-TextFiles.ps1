[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Path,

    [switch]$Check
)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$hadIssue = $false

$expandedPaths = foreach ($entry in $Path) {
    $entry -split "," | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_.Trim() }
}

foreach ($inputPath in $expandedPaths) {
    $resolvedPaths = Resolve-Path -LiteralPath $inputPath -ErrorAction Stop

    foreach ($resolved in $resolvedPaths) {
        $filePath = $resolved.Path
        $bytes = [System.IO.File]::ReadAllBytes($filePath)
        $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        $text = [System.Text.Encoding]::UTF8.GetString($bytes)
        if ($hasBom -and $text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) {
            $text = $text.Substring(1)
        }
        $hasBareLf = [regex]::IsMatch($text, "(?<!`r)`n")
        $hasCrOnly = [regex]::IsMatch($text, "`r(?!`n)")
        $needsNormalization = $hasBom -or $hasBareLf -or $hasCrOnly
        $status = if ($needsNormalization) { "needs-normalization" } else { "ok" }

        [pscustomobject]@{
            File = $filePath
            Utf8Bom = $hasBom
            BareLF = $hasBareLf
            CROnly = $hasCrOnly
            Status = $status
        }

        if ($needsNormalization) {
            $hadIssue = $true

            if (-not $Check) {
                $normalized = $text -replace "`r`n|`r|`n", "`r`n"
                if ($PSCmdlet.ShouldProcess($filePath, "Normalize to UTF-8 without BOM and CRLF line endings")) {
                    [System.IO.File]::WriteAllText($filePath, $normalized, $utf8NoBom)
                }
            }
        }
    }
}

if ($Check -and $hadIssue) {
    exit 1
}
