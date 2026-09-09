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
