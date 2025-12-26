## What changed
- Added a reusable autosave status partial and applied it to all form headers/actions (including Vue panes) using the unified fw-autosave-status classes.
- Simplified fw.js autosave status rendering to use the shared spinner markup, badge titles for timestamps, and throttled updates.
- Simplified autosave badge styling to lean on Bootstrap badges with minimal custom CSS.

## Commands that worked (build/test/run)
- Not run (not requested)

## Pitfalls - fixes
- Throttled autosave error toasts to avoid noisy repeat alerts while still surfacing failures.

## Decisions - why
- Used only fw-autosave-status/-global classes to keep targets consistent and avoid dual-class maintenance.
- Moved timestamps into badge titles to reduce visual noise while keeping context available on hover.

## Heuristics (keep terse)
- When surfacing background status, debounce identical DOM/html updates to prevent jitter during rapid event bursts.
