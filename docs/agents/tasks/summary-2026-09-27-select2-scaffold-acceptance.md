# Select2, generated demos, and acceptance follow-up

## Outcome

- Moved Select2 styling from its include into `site.css`, using Bootstrap theme variables for selection, dropdown, search, options, and focus states. Bumped `SITE_VERSION` to refresh cached assets.
- Marked the two demo-only `Validate()` overrides with `DemoOnly` regions. The shared generator excludes these regions, keeping runnable examples out of generated application controllers. Manual-copy documentation describes the same boundary.
- Live acceptance found Vue initialization overwrote a requested form tab. Reading the tab before the initial data load preserves valid deep links and still rejects unknown tabs.
- Kept save coordination intact: snapshots, form ownership, originating tabs, and retained failures protect distinct behavior. Optional small cleanup remains in unused sample props/events and empty lifecycle hooks; no new abstraction is warranted.

## Verification

- Full default suite: 947 passed, zero failed/skipped, using isolated output and local browser assets under ignored task artifacts. This includes the new two-case generator test and two-case startup test.
- One additional regression through the real quick-edit pane with list-screen ID zero passed. Typing and blur autosave the loaded record and preserve the pane. An initial live-save diagnosis was disproved; no quick-edit runtime change was made.
- Visual Studio MCP application build: zero failed projects. Live app used the new asset version.
- Chrome dark Select2: selection, dropdown, and search have dark backgrounds and readable text; search and selection work.
- Dynamic subtable: required lookup feedback, successful save, GET reload persistence, and deletion of the task child.
- Vue subtable: compact summary without temporary row identifiers, link focuses the lookup, positioned tooltip, assigned child ID, later edits survive reload without duplication, and deletion succeeds. Relations remains selected on reload.
- Vue list: first resize preserves other widths; keyboard and pointer resizing, Ctrl-double-click auto-fit, reload persistence, and unwrapped row controls passed. Filter bump retains its position and 44-by-12 size; hiding filters recovers 146 pixels and survives reload.
- Named view creation/restoration passed. Restored original columns before removing the task-only backup; the pre-existing view remains. Quick edit refreshed the list while preserving the open pane and focus.
- Removed both disposable parents and task children through the application. Filtered list showed no task parents. Cleared temporary search and restored visible filters.
- Earlier error-only, warning-only, mixed feedback, and creation-time writable Code captures remain in the ignored gallery; added Select2, subtable, and list captures. Earlier native navigation confirmation was dismissed; automated tests remain the evidence for all six failure categories and both navigation outcomes.
- No migration was needed. Earlier SQLite checks remain applicable; no provider code changed. No MySQL, production deployment, downstream upgrade, or exhaustive manual race/authorization matrix was run. Light-theme Select2 was not separately exercised live. Chrome also logged the existing string-to-Boolean `isLeft` prop warning; it was not expanded into this fix.

## Review

Independent consumer-contract review resolved stylesheet specificity and formatting findings. Generated output and tab initialization were reviewed without adding a parser or state abstraction. Final summary/index audit found no factual, evidence, or privacy issues. Final verdict: no blocking findings; the review loop can stop.
