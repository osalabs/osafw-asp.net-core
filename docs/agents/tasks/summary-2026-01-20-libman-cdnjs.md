## What changed
- Updated `osafw-app/libman.json` to set `defaultProvider` to `cdnjs`.
- Adjusted file paths for libraries kept on cdnjs where layout differs from unpkg (`bootstrap`, `jquery`, `echarts`, `turndown`, `vue`, `pinia`).
- Added per-library `provider: unpkg` overrides for libraries not available/compatible on cdnjs (multiple Vue ecosystem libs, markdown-it plugins, select2, etc.).
- For `@vue/devtools-*`, added explicit `files` entries to restore the `dist/index.js` files referenced by the import map.

## Commands that worked (build/test/run)
- `Set-Location osafw-app; npx libman restore`

## Pitfalls - fixes
- PowerShell doesn't accept `&&` here; used `Set-Location ...; <command>`.
- cdnjs package layouts differ from unpkg (no `dist/` in several packages); updated `files` paths accordingly.
- cdnjs doesn't host many npm-style/scoped packages used by the app; used per-library `provider: unpkg` overrides.
- `dotnet build` currently fails due to pre-existing test error (`FormUtils.getIdFromAutocomplete` missing) unrelated to LibMan changes.

## Decisions - why
- Kept `defaultProvider` as `cdnjs` to satisfy the request, but selectively overrode to `unpkg` to ensure `libman restore` succeeds and the app's expected local file layout remains intact.

## Heuristics (keep terse)
- When switching LibMan providers, run `libman restore` once and fix errors iteratively; cdnjs often differs in file paths vs npm/unpkg.
