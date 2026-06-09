## What changed
- Rebuilt `docs/CHANGELOG.md` into dated breaking-upgrade sections covering commits since 2025-06-01.
- Confirmed the concise future-agent rule exists in `AGENTS.md` and `.github/copilot-instructions.md`, requiring dated changelog entries for end-user-app breaking changes.
- Confirmed `docs/agents/code_reviewer.md` includes a check so review loops verify changelog coverage for breaking end-user-app upgrade changes.
- Confirmed `AGENTS.md` and `.github/copilot-instructions.md` are byte-for-byte synced.

## Scope reviewed
- Read machine-local guidance in `docs/agents/local_instructions.md` and used `docs/README.md` as the documentation entry point.
- Reviewed `docs/CHANGELOG.md`, `AGENTS.md`, `.github/copilot-instructions.md`, and `docs/agents/code_reviewer.md`.
- Reviewed commit history from 2025-06-01 through 2026-06-06 with emphasis on public APIs, schema/update scripts, templates/includes, routes, security defaults, storage keys/URLs, config/compile symbols, JS/CSS assets, and framework behavior likely to affect end-user apps.
- Targeted commit diffs included the 2025-06-17 upgrade rollup, per-user datetime/timezone, middleware removal, collection migration, typed model pipeline, config refactor, page-header/template moves, Chart.js/ECharts, Vue store refactor, FwCron scheduling, feature compile symbols, lookup active-row filtering, ParsePage recursion/numeric formatting, reports, security hardening, markdown, dynamic attachment/link authorization, and S3 attachment changes.

## Commands used / verification
- `git status --short`
- `git log --since=2025-06-01 --date=short --pretty=format:"%h %ad %s"`
- `git log --since=2025-06-01 --diff-filter=DR --summary -- osafw-app AGENTS.md docs .github`
- Targeted `git show --unified=0 --format=medium <commit> -- <paths>` for breaking-change candidates.
- `rg -n "Unreleased|Breaking|Changed" docs\CHANGELOG.md`
- `rg -n "select2|bootstrap_select|selectpicker" osafw-app\App_Data\template osafw-app\wwwroot\assets\js docs`
- CRLF normalization/sync for edited docs and AGENTS/Copilot instructions.
- `git diff --check -- docs\CHANGELOG.md AGENTS.md .github\copilot-instructions.md docs\agents\code_reviewer.md docs\agents\tasks\summary-2026-06-06-breaking-changelog.md` - passed.
- Line-ending check for edited files - CRLF-only and no UTF-8 BOM.
- AGENTS/Copilot comparison - no differences.
- Reviewer loop using `docs/agents/code_reviewer.md` - sub-agent first pass found one low stale-status issue in this task summary; fixed. Second pass found no issues and said the review loop can stop.
- Final `git status --short` showed `docs/CHANGELOG.md` as the only tracked task diff; this task summary is untracked, and unrelated pre-existing untracked files were left untouched.

## Decisions - why
- Organized `docs/CHANGELOG.md` by commit date rather than keeping `Unreleased`, because the user needs upgrade planning by when each breaking change entered the framework.
- Listed only changes that can require app code/template/config/data/schema/build changes, not every commit or every visual/internal cleanup.
- Kept conservative notes for template/CSS/frontend asset changes when copied app overrides are likely to depend on those contracts.
- Updated `AGENTS.md` instead of adding a separate heuristic because this is a recurring task-closing requirement that future agents should see in the primary workflow.
- By final verification, the instruction updates were already present in the current HEAD/worktree and did not remain as tracked diffs, while `docs/CHANGELOG.md` remained the tracked file changed by this task.

## Pitfalls - fixes
- Several commits were broad rollups; used targeted file diffs and deletion/rename summaries to separate real upgrade breaks from internal refactors.
- Some changes were later partially reverted or superseded, such as bootstrap-select returning after Select2 work; recorded only the final upgrade-relevant contract.
- `AGENTS.md` sync requires byte-for-byte copy to `.github/copilot-instructions.md`; handled after editing and line-ending normalization.
- `AGENTS.md` also contained a concurrent XML-docs instruction wording change; preserved it and synced it to `.github/copilot-instructions.md` per repo instructions rather than reverting another workspace change.
- `docs/agents/code_reviewer.md` already had an unrelated one-line XML-docs wording change in the worktree; kept it and added the changelog-review check nearby.

## Risks / follow-ups
- Changelog classification is manual. Some UI/CSS entries are intentionally conservative for apps with copied templates or custom overrides.
- No runtime/build tests are needed for docs-only changes. Highest residual risk is missed historical breaking changes from broad rollup commits.

## Heuristics (keep terse)
- Added no separate `docs/agents/heuristics.md`, domain, glossary, or ADR entries. The stable future rule was added to `AGENTS.md`.

## Testing instructions
N/A - docs/instructions only.

## Reflection
The slowest part was classifying broad rollup commits without an existing dated breaking-change ledger. Future tasks that touch public framework contracts should add the changelog entry in the same commit/task instead of reconstructing intent from history. Sub-agent review was preferred by instructions, but if no suitable sub-agent tool is available, self-review with `docs/agents/code_reviewer.md` is an acceptable fallback for this docs/instruction-only change.
