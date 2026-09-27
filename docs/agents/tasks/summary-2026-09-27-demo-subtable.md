# Demo-owned Vue subtable

## Outcome

- Restored the unchanged subtable implementation to `admin/demosvue/index/vue/subtable_demos_items.html` using `git mv -f`. The original demo path and its earlier history remain intact; no history rewrite is needed. Git's rewrite/rename detection reports a 100% move from the temporary common location.
- Removed the virtual-controller demo wrapper and its component include. Generic `subtable_<field>` dispatch remains available for application-owned components.
- Replaced the sharing assertion with a ParsePage-rendered bundle check: DemosVue contains the demo component, while the virtual bundle retains generic controls without demo names, lookups, or endpoints. Canonical documentation records the component registration point.
- The removed common component path was introduced within this unreleased PR. The original demo entry path is retained, so no breaking-change log entry is added.

## Verification and review

- `dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<checkout>/artifacts/assistant_demo_subtable/build/default/ --filter FullyQualifiedName~VueInteractionBrowserTests` passed all 74 cases, with zero failures/skips or compilation warnings, on the Vue branch. Offline browser assets and isolated build output were used; the live app, database, and user browser state were untouched.
- The restored component's Git object hash matches the former common component exactly. `git diff HEAD -B -M --summary` recognizes the 100% move; `git diff HEAD --check` and strict UTF-8/no-BOM/CRLF checks passed.
- A local second-pass review with the consumer-contract overlay found no blocking findings. This is a localized template ownership cleanup with rendered-boundary and existing browser coverage; independent review was not required. The summary was audited against the diff and test result.
- Existing application-specific custom subtable templates were not exercised in a live application. No provider or schema behavior changes.
