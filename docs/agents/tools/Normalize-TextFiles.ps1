[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
    [string[]]$Path,

    [switch]$Check
)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false, $true)
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
        $hasUnsupportedBom =
            ($bytes.Length -ge 4 -and $bytes[0] -eq 0x00 -and $bytes[1] -eq 0x00 -and $bytes[2] -eq 0xFE -and $bytes[3] -eq 0xFF) -or
            ($bytes.Length -ge 4 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE -and $bytes[2] -eq 0x00 -and $bytes[3] -eq 0x00) -or
            ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) -or
            ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF)

        try {
            if ($hasUnsupportedBom) {
                throw [System.Text.DecoderFallbackException]::new("UTF-16/UTF-32 BOM detected.")
            }
            $text = $utf8NoBom.GetString($bytes)
        }
        catch [System.Text.DecoderFallbackException] {
            $hadIssue = $true
            [pscustomobject]@{
                File = $filePath
                Utf8Valid = $false
                Utf8Bom = $hasBom
                BareLF = $null
                CROnly = $null
                Status = "invalid-encoding"
            }

            if (-not $Check) {
                throw "File is not strict UTF-8; refusing to normalize: $filePath"
            }
            continue
        }

        if ($hasBom -and $text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) {
            $text = $text.Substring(1)
        }
        $hasBareLf = [regex]::IsMatch($text, "(?<!`r)`n")
        $hasCrOnly = [regex]::IsMatch($text, "`r(?!`n)")
        $needsNormalization = $hasBom -or $hasBareLf -or $hasCrOnly
        $status = if ($needsNormalization) { "needs-normalization" } else { "ok" }

        [pscustomobject]@{
            File = $filePath
            Utf8Valid = $true
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
                    try {
                        [System.IO.File]::WriteAllText($filePath, $normalized, $utf8NoBom)
                    }
                    catch {
                        throw ("Failed to normalize {0}: {1}" -f $filePath, $_.Exception.Message)
                    }
                }
            }
        }
    }
}

if ($Check -and $hadIssue) {
    exit 1
}
