# Calculated List Columns

## Objective / acceptance

Add an explicit Dynamic/Vue list contract for virtual columns calculated after database reads. Selected calculated columns load only declared source dependencies, never enter ordinary SQL selection or search/filter paths, preserve approved database-backed sorting, remain available to list and export output, and do not expose calculation-only source fields in Vue JSON. Preserve existing list behavior when the metadata is absent.

## What changed

- Added normalized `list_calculated_fields` controller metadata. The map is calculated field to dependency string/list; an empty list declares a dependency-free field. Unsafe names and calculated fields absent from `view_list_map` are ignored.
- Added the key to canonical generated controller-config ordering.
- Removed calculated names from automatic sort maps, keyword search, legacy/typed column filters, search hints, and SQL projections. Explicit `list_sortmap` entries remain authoritative for database-backed calculated-column ordering.
- Built list projections from visible stored fields, dependencies for selected calculated fields, and the model id. Dynamic lists retain their established `SELECT *` behavior unless calculated metadata opts them into the dependency-aware projection.
- Kept calculation in existing controller/model row-shaping extension points. Vue removes only source fields added for calculation after all row-shaping overrides run, including for exports.
- Added Vue page state and store defaults for calculated field names so the shared header disables calculated-column filtering without exposing server-side dependency metadata.

## Scope reviewed

Reviewed `FwController` list config, sort, keyword/advanced search, projection, user-view, header, and export paths; `FwDynamicController` list flow and typed column filters; `FwVueController` init/list/export/JSON shaping; generated config ordering; shared Vue store/header consumers; current calculated-row controller/model extension patterns; canonical Dynamic/Vue docs; and existing list/filter/controller tests.

## Requirements / decisions

- Calculation continues through `getListRows()` overrides in both Dynamic and Vue controllers. Vue additionally supports `FwModel.filterForJson()`; classic Dynamic lists do not invoke that hook. Existing row authorization and trusted renderer behavior remain unchanged.
- Dependency metadata remains server-side. The Vue state contains calculated field names only.
- Dependencies use simple identifier names. This supports stored fields and subquery aliases without accepting SQL expressions as projection metadata.
- A calculated field is automatically non-sortable. An explicit `list_sortmap` entry can safely map its UI name to a stored field or existing approved ordering expression.

## Changed contracts

`list_calculated_fields` is an additive Dynamic/Vue `config.json` contract. Vue initial page state includes `list_calculated_fields` as normalized names only when controller or legacy store configuration supplies it. Absent metadata leaves client-only state unchanged. Apps that opt in should declare every non-visible stored field required by their calculation and populate calculated values in an established row-shaping override.

## Commands used / verification

- Final focused command: `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~CalculatedDynamicListTests|FullyQualifiedName~FwVueControllerTests|FullyQualifiedName~FwDynamicControllerColumnFilterTests|FullyQualifiedName~FwControllerBehaviorTests' --verbosity quiet` - 47 passed, none skipped.
- Classic Dynamic controls enter `IndexAction`, verify the actual SELECT projection, run the controller calculation hook, assert the displayed cell, and export CSV. Both a source dependency and an empty dependency array are covered. Vue controls cover pruning, search/filter/sort behavior, legacy metadata, and absent configuration.
- The dependent Vue PR was synchronized with the simplified parser and passed its 75-test backend/browser filter, including 24 actual offline browser cases. That cross-branch integration evidence is recorded in its own active summary; this branch's 47-test command is server-side.
- Earlier pre-correction focused/broad commands passed 44/721 tests. Those historical counts are not final-state broad-suite evidence.
- Tests use rejecting/recording fake databases. No configured database, mail, or live service was used.

## Testing instructions

Run the final focused command above. No browser installation or application database is required for it. Browser integration prerequisites and the exact dependent-branch command are recorded with the Vue interaction change.
## Risks / follow-ups

- Metadata validates identifier syntax and calculated-name membership in `view_list_map`; the configured `list_view` remains the authority for whether a syntactically valid hidden dependency exists. A missing stored field fails through the normal database query error path.
- This branch verifies server list/search/export boundaries. Browser execution is separate integration evidence from the dependent Vue branch, not a standalone browser run on this branch.
- Independent consumer-contract review is routed to the integrating owner before merge.

## Knowledge-promotion candidates

The reusable configuration and extension-point contract was promoted to `docs/dynamic.md`; no further promotion is needed.

## Reflection

The existing shared Vue header contained a calculated-field check without a matching server contract. Tracing history and current row-shaping hooks early avoided inventing a second calculation API. A useful workflow improvement would be to require incomplete frontend state keys to name their expected server payload shape in an adjacent comment or canonical document.

## Review correction

Independent review found that the legacy source-to-calculated adapter was applied ambiguously to the new top-level map while the actual old store configuration was overwritten. Top-level parsing is now strict. The legacy adapter applies only to store.list_calculated_fields when the explicit top-level key is absent; absent server metadata does not overwrite client-only state. Added the unknown-key/known-visible-value negative control and initial-page-state legacy/absent configuration controls.

Final correction command: `dotnet test osafw-tests/osafw-tests.csproj --no-restore --filter 'FullyQualifiedName~FwVueControllerTests|FullyQualifiedName~FwDynamicControllerColumnFilterTests' --logger 'console;verbosity=normal'`: 37 passed. Earlier 44/721 counts apply to the pre-correction broader filters; they were not repeated after this bounded correction. Browser state merging remains inspected rather than browser-tested in this packet.

## Lean review follow-up (2026-09-09)

Synced with master after the approved framework updates. Reduced new top-level metadata to a calculated-name-to-dependencies map, removing definition lists, aliases, and name-only forms. Dependency strings and arrays remain supported, including empty arrays. The existing inverse Vue store map adapter and absent-metadata client-state behavior remain intact. Canonical documentation now identifies `getListRows()` as the common calculation hook and `filterForJson()` as Vue-only.

The parser correction first passed 45 focused checks. Independent review then identified missing classic Dynamic entry coverage; the final 47-test command above includes both new IndexAction/export cases and all prior focused controls. No production change was needed for those cases.
