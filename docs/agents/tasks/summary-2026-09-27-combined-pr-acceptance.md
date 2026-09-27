# Combined PR review and acceptance

## Outcome

PR #298 now targets `master` and includes both interaction and form-issue branches. PR #297 was closed without deleting its branch. Reviewed the combined diff from `origin/master` before consulting historical summaries.

- Retained the existing save coordinator: its snapshots, per-form queues and per-tab failures protect distinct behavior. Removed unused Pinia wiring from the form group component.
- Both demo controllers now demonstrate errors, warnings and mixed feedback using the Title values documented in `docs/dynamic.md`. Messages are translated, and warnings do not claim persistence. These examples validate submitted parent values on every save, including Vue requests originating on another tab.
- Live testing found manual screen changes could leave a failed Vue form. Added explicit leave confirmation, restoration of the current URL when history navigation is declined, and a browser unload warning for unresolved failures. Confirmed departure remains possible. Declined history navigation replaces the traversed entry; it does not reconstruct the full history stack.
- No new schema, CSS, public C# API or upgrade migration change in this follow-up. Earlier combined-branch migration notes remain in the upgrade changelog.

## Verification

Used isolated build output under ignored `artifacts/assistant_combined_pr/build/default/`, with locally restored browser assets. Visual Studio MCP rebuilt the application with zero failed projects and launched it without debugging to avoid stopping on expected validation exceptions.

- `dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute task output>`: 943 passed, none failed/skipped.
- Final affected browser/validation selection, `--filter 'FullyQualifiedName~VueInteractionBrowserTests|FullyQualifiedName~OptionalValidation'`: 90 passed. This preceded the final history/unload additions.
- After those additions, `--filter 'FullyQualifiedName~FailedTabSaveSurvivesOtherTabSuccessUntilItsOwnSuccessfulRetry'`: six passed, covering structured, generic, form-wide, transport, authorization and server failures through real store actions. Cancel preserves the draft; declined history restores the URL; unload warns; confirmed departure clears warning eligibility.
- Optional SQLite build with `-p:DefineConstants=isSQLite` and filter `FullyQualifiedName~SQLiteDBTests.SQLiteSchemaScripts_CreateFreshFrameworkDatabase|FullyQualifiedName~SQLiteDBTests.SQLiteUserViewWidthsUpdate_AddsColumnToLegacySchema|FullyQualifiedName~VueInteractionBackendTests`: 18 passed. Fresh and additive schema checks used disposable SQLite files. Restored the default dependency graph afterward.
- Live Chrome: created disposable records in both demos; verified Code is writable on creation and read-only afterward; captured error-only, warning-only and mixed feedback; warning-only persistence survived reload; errors blocked saves. Verified Vue summary links focus the target field. Six original screenshots and a comparison gallery are retained in ignored task artifacts.
- The live leave confirmation appeared, but the browser tool stalled on native-dialog focus handling. User cancellation is pending; final live navigation checks and removal of the two disposable records remain pending. No existing records were edited. No new database migration was applied.
- UTF-8 without BOM, CRLF and `git diff --check` passed. SQL Server live form saves were exercised; no fresh SQL Server migration, MySQL, Linux/IIS deployment or downstream application upgrade was run in this follow-up.

## Review

A fresh `reviewer_high` reviewed the combined runtime diff using consumer-contract and state-integrity overlays, then the incremental demo and navigation changes. Final verdict: no blocking findings. The active-summary audit found no supplemental factual, privacy or evidence issues.

- Learning signal: Removing apparently redundant `disabled` attributes from anchors broke two browser tests because `site.css` uses `a[disabled]` to suppress pointer interaction. Restored the original bindings and reran the focused checks (six passed); inspect CSS consumers before removing non-native element attributes.
