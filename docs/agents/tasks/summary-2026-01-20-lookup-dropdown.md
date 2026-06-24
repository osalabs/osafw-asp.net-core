## What changed
- Added dropdown-capable append/prepend buttons for dynamic showform input groups, including modal-driven lookup add/edit flow and demo config updates.
- Extended modal handling to support lookup saves and auto-refresh related select options, simplified lookup attrs to use data-fw-lookup only.
- Documented dropdown button configuration and lookup modal usage in dynamic controller docs.
- Added modal layout support with header/footer and wired modal requests to use it; suppressed headers for DemoDicts showform modal layout.

## Commands that worked (build/test/run)
- Not run (not requested).

## Pitfalls - fixes
- Ensured lookup modal submissions request JSON and reuse existing form error handling to avoid full HTML reloads.

## Decisions - why
- Used modal JSON responses with lookup_label for iname so the parent select can update reliably after add/edit.
- Forced modal layout selection via a request flag so modal content renders with consistent header/footer without full-page chrome.

## Heuristics (keep terse)
- Prefer dropdown configuration on input-group append/prepend to keep dynamic forms declarative.
