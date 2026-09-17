# Local PDF assets

## Objective / acceptance

Render reports with explicitly approved local styles, images and fonts while blocking arbitrary files and external requests and cleaning task-owned output on failure.

## What changed

Added opt-in `local_assets_root` routing through an offline browser context with scripts, service workers and downloads disabled. A dedicated resolver confines approved static types to the selected directory, rejects linked paths, and bounds per-resource and total bytes. Reports select a local print layout when enabled. Image/font readiness and print media are applied before rendering. In local asset mode, PDF output is staged before replacement, and browser-download temporary files are removed after transfer.

## Changed contracts

Existing signatures and the default print layout remain. The new mode requires trusted server-side configuration and public static assets with local URLs. In local asset mode, failed output does not overwrite an existing destination. Legacy callers continue writing directly to the destination. Canonical report documentation covers limits, header/footer restrictions, setup and cleanup. No schema changes or breaking changelog entry are required.

## Commands used / verification

- Installed matching Chromium into a task-owned ignored browser directory using the test output's `playwright.ps1 install chromium --no-shell` script.
- With `PLAYWRIGHT_BROWSERS_PATH` set to that directory: `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~PdfLocalAssetTests|FullyQualifiedName~ConvUtilsTests' --verbosity quiet`: 6 passed, none skipped.
- Tests exercise the actual HTML renderer and FwReportsBase render entry, local CSS/SVG and lazy-image loading, missing font/image failures, blocked loopback requests with no accepted connection, preserved destination bytes, temporary-file absence, legacy rendering, and policy failures for external/path/type/size violations.

## Risks / follow-ups

No application database or external asset server was used. Browser tests require installed matching Chromium; ordinary test runs mark them inconclusive when unavailable. Asset directory contents must remain trusted during rendering. Chromium print header/footer resources are self-contained; browser visual comparison and non-Windows validation were not run.

## Review correction

Independent review found that browser-side stylesheet rejection and CSS background-image decode failures could bypass the original readiness check. Rendering now records failed browser requests, checks active stylesheet availability, and probes requested CSS images before replacing the destination. Decode probes use connected hidden images and bounded readiness polling because document scripts are disabled. A failed decode preserves the prior PDF and removes the temporary output.

Final targeted command: `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~PdfLocalAssetTests|FullyQualifiedName~ConvUtilsTests' --verbosity quiet` with the task-owned matching Chromium cache selected by PLAYWRIGHT_BROWSERS_PATH: 6 passed, none skipped. Added public-renderer controls cover a blocked file stylesheet, corrupt CSS background image, valid CSS SVG background, and prior-byte preservation. Independent probing also verified a valid local WOFF2 font. Earlier intermediate decode probes failed under disabled scripts and were replaced by the final polling implementation.

A second review found that report CSS with !important could make diagnostic image nodes visible. Successful probes are now removed before PDF generation; failure disposes the browser context. The final six-test run passed again, including a paired PDF page-count regression proving diagnostic images cannot add pages under overriding report CSS. Text and diff checks remain clean.

## Lean review follow-up (2026-09-09)

Synced with master after the approved framework updates. Restricted staging and replacement to opt-in local asset rendering so legacy callers retain in-place writes and existing file access behavior. Clarified that trusted configuration includes Site Admin-managed custom report options. Added a real-renderer regression with an existing open reader that permits writes but not deletion; it observes the new PDF through the same file handle.

With matching task-owned Chromium selected by `PLAYWRIGHT_BROWSERS_PATH`, `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~PdfLocalAssetTests|FullyQualifiedName~ConvUtilsTests' --verbosity quiet` passed 7 tests, none skipped. The existing local-mode failure and prior-output preservation controls passed. No live application database or external asset server was used.

## Feedback follow-up (2026-09-16)

Restored `Utils.cleanupTmpFiles()` after browser PDF downloads so later requests can remove framework temporary files older than one hour, including crash leftovers. Immediate download and staged-PDF deletions now share a best-effort helper; deletion failures do not fail downloads or mask rendering errors. The age-based sweep is also guarded against enumeration/access failures. Staged files outside the framework temporary directory are not swept by this utility.

Removed the single-use `PdfLocalAssets` class. `ConvUtils` now owns the path/type checks and link rejection, while a local reader in `html2pdf` holds the per-render byte budget. Concurrent reads use an atomic counter; the 10 MiB per-resource and 50 MiB per-render limits are unchanged. Added blank lines between logical blocks, expanded dense try/catch and browser scripts, and added a short explanation of `TPL_EXPORT_PDF_LOCAL`. The prior compact formatting was an implementation choice, not a repository requirement.

Final verification with matching Chromium in the task-owned browser directory:

- `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~PdfLocalAssetTests|FullyQualifiedName~ConvUtilsTests' --verbosity quiet`: 10 passed, none skipped.
- Asset-policy checks now use the public renderer instead of the removed internal class. A paired test accepts combined asset reads below the total budget and rejects reads above it while preserving the prior PDF.
- Windows download tests enter `parsePagePdf` and `fileResponse`, including a response holding the PDF open without delete sharing. The response succeeds; old unlocked files are removed, locked files survive without errors, and a later sweep removes them after release. TMP/TEMP are temporarily directed into the fixture's unique directory and restored; no shared temp directory is swept by these tests.
- Existing legacy in-place rendering, local styles/images/fonts failure controls, blocked external connections, destination preservation, and diagnostic-image pagination checks still pass.
- No live database, application server, or external asset server was used. Matching Chromium is required for the policy tests; Windows file-sharing and creation-time controls are Windows-only. Visual PDF comparison and non-Windows execution remain unperformed.

Fresh independent review used `reviewer_high` with security-boundary and state-integrity overlays, followed by an active-summary/index audit; neither found blocking issues. Final integrator verdict: No blocking findings. Review loop can stop. UTF-8/no-BOM/CRLF and diff checks passed. The additive master synchronization resolved only the task-index conflict, preserving both entries. No breaking changelog entry is needed for this unmerged feature follow-up.
