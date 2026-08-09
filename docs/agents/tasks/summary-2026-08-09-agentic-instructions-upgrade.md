## What changed

- Replaced the broad root policy with a shorter `AGENTS.md` router and task-routed workflow, verification/isolation, adaptive review, and specialist overlay documents.
- Made the guidance explicitly support both canonical framework development and copied real-application repositories without assuming the same branch protection, release process, or absence of app customizations.
- Retained `CLAUDE.md` as the active thin pointer. Removed the unused Copilot mirror and its stale project-file item.
- Adapted the PPG reviewer approach into one integrating verdict plus triggered agent-workflow, consumer-contract, performance, security-boundary, and state-integrity overlays with a local no-subagent fallback.
- Clarified SQL Server as production-primary/schema authority, SQLite as optional embedded and preferred disposable provider-neutral isolation, MySQL as optional without assumed parity, and OLE/ODBC/MS Access as mainly import/compatibility surfaces.
- Added resource-level worktree guidance for source/build/NuGet/test output, ports/processes/IDE locks, provider databases, config/secrets, browser/uploads, workers/queues, containers, and external accounts.
- Updated framework-upgrade, instruction-upgrade, reflection, PR-review, orchestration, and docs-consistency prompts for authority boundaries, copied-app upgrades, sequential `FwUpdates`, progressive disclosure, and adaptive review.
- Added deterministic instruction validation and corrected the documented upload path to `/osafw-app/upload`.
- Installed ignored machine-local FPF instructions/helper in the worktree and primary checkout, persisted the ignored implementation plan under `docs/drafts/`, and refreshed the shared validated cache.
- Follow-up text review removed migration/session narration from active guidance, reduced `CLAUDE.md` to its import, and kept historical rationale in this summary rather than future agent context.

## Scope reviewed

- Root/nested native instruction discovery, Claude/Copilot guidance, ignored local conventions, prompt catalog, task-history routing, reviewer/tool docs, hooks/helpers, and documentation ownership.
- Project/solution target frameworks and conditional packages/symbols, provider schemas/update discovery, `FwUpdates` filename/load/apply order, Windows/IIS deployment docs, tests, Git history, ignored artifact paths, and absence of GitHub Actions/package generation.
- Public/source-copy compatibility surfaces: APIs/overrides, controller/actions/routes, ParsePage templates/includes/page state, configuration/defaults/symbols, generated/scaffolded output, schema/provider behavior, frontend/storage/security defaults, upgrade docs, and changelog rules.
- Targeted FPF patterns A.15.2, C.24, E.19, and G.11 from the June 2026 cache (`f916341a8b3711b847c8a701f8b71e0ee155d9e5517be3ed36363fa6621ccf2b`) informed plan-versus-execution evidence and bounded refresh/currentness design; FPF was not treated as repository authority.

## Requirements / decisions

- Keep tracked policy public, portable, model/tool-neutral, and value-free. Machine paths and optional FPF behavior remain ignored/local.
- Keep one always-loaded owner and route detail only when a task needs it. Do not preserve compatibility mirrors without an active workflow.
- Accept short prompts when repository/issue context supplies the outcome; require a focused interview only for unresolved material product, authorization, data, compatibility, or operational choices.
- Diagnose/review requests remain read-only; implementation does not imply branch/commit/push/PR/release/deploy/database/external-write authority.
- Prefer one integrator review. Add at most the risk-triggered overlays, deduplicate candidates, and issue one adjudicated verdict.
- Use additive compatibility/shims when cheap, immediate behavior changes for necessary security fixes, and explicit changelog/migration treatment for other breaking/behavioral changes; decide any deprecation window case by case.
- Leave existing task-summary logs/status wording unchanged. Validate deterministic index references without interpreting history or unrelated untracked summaries.
- Use lightweight representative walkthroughs for this rollout; defer a full baseline/candidate benchmark until a broader rewrite or sufficient held-out work justifies it.

## Changed contracts

- Shared agent workflow/discovery, review routing, verification, prompt, summary-schema guidance, and local-reference handling changed.
- Stable repository/domain documentation now records the verified dual source-copy mode, current `net10.0` project targets, Windows/IIS primary deployment, and provider roles.
- No runtime C# API, route/template, schema/update, configuration value/default, package version, generated output, or security behavior changed. The project-file edit only removes a stale documentation item.
- `docs/CHANGELOG.md` was not changed because this task introduces no downstream application runtime or upgrade break. README/database wording changes correct or clarify existing behavior.

## Commands used / verification

- `docs/agents/tools/Test-AgentInstructions.ps1` passed under the current PowerShell and Windows PowerShell 5.1: required routes, exact Claude import without duplicated guidance, local links, private-path leakage, exact tracked-summary indexing, strict UTF-8/CRLF, and no-newline ParsePage `url.html` literals.
- The ignored FPF helper passed missing-cache refresh, conditional `not-modified`, 24-hour cache hit, concurrent callers (`refreshed` + serialized `unchanged`), network-failure and invalid-candidate last-known-good fallback, invalid-candidate failure without a cache, hash/metadata, atomic-temp cleanup, and Windows PowerShell 5.1 checks. The shared cache refreshed to August 2026, SHA-256 `691692b7a638874e4ea66acd8df6e18bf22d6ffcac01a96d7ff7c93a8a330891`.
- `dotnet build osafw-app/osafw-app.csproj -p:OutDir=<ignored task-owned artifacts path>` succeeded: 0 warnings, 0 errors.
- `git diff --check` passed. Targeted searches removed stale mirror/session-transition language from active guidance; migration rationale remains only in this task record and the ignored implementation plan.
- Bounded inspection verified `FwUpdates.loadUpdates()` sorts update filenames, records unseen scripts in that order, and `applyPending()` applies pending rows sequentially by id.
- Final review used the integrating procedure plus `reviewers/agent-workflow.md` as the documented local fallback. Verdict: `No blocking findings.` `Review loop can stop.`

Representative prompt walkthroughs:

| Task | Expected routing/gate | Result |
| --- | --- | --- |
| Ordinary Dynamic/CRUD null-handling fix | Root + nearest CRUD/dynamic/naming contract, focused behavior test, integrator only | Routed without loading specialist/history context. |
| Public `FwModel` method rename or config-default change | `workflow.md`, public/source-copy verification, compatibility/changelog decision, consumer-contract overlay | Existing caller/override/app upgrade evidence is required before change. |
| Attachment mutation with database link changes | Security and state-integrity overlays, parent authorization/POST token, SQL Server behavior; SQLite only for provider-neutral isolation | Exactly two justified overlays, one integrator verdict; no optional delegation assumption. |
| Windows/IIS release documentation for a breaking compile-symbol change | Canonical deploy/config docs, docs/release verification, copied-app migration/changelog, consumer overlay only because a real contract changed | Documentation-only evidence stays proportional while downstream migration remains explicit. |

## Testing instructions

- Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1` after changing shared agent guidance, prompts, reviewer routes, or summary indexing.
- Build `osafw-app/osafw-app.csproj`; use a unique absolute ignored `OutDir` under `artifacts/assistant_<task>/` when IDE/IIS output may be locked.
- For a machine-local FPF-relevant task, follow the ignored `docs/agents/local_instructions.md` refresh command, then use only targeted headings/pattern ranges.
- No runtime/database/browser test is required for this docs/workflow-only change.

## Risks / follow-ups

- No downstream application compatibility fixture exists; the copied-app upgrade prompt and walkthrough are process evidence, not a real app regression run.
- MySQL fresh-schema parity remains a separate task. No provider database was mutated or tested here.
- There is no GitHub Actions gate. The new validator is local and must be invoked by a developer/agent until a future explicit automation decision.
- The FPF helper/reference is intentionally ignored and machine-local, so public contributors neither receive its private path nor depend on it. A valid cache is retained on refresh failure.
- Full held-out implementation benchmarking was intentionally deferred; deterministic helper success is not claimed as proof of better code quality.

## Knowledge-promotion candidates

- Dual repository/application mode, source-copy distribution, current target frameworks, Windows/IIS primacy, and provider roles were promoted to `docs/agents/domain.md` and `docs/db.md`.
- No new glossary term, heuristic, or ADR was warranted.

## Reflection

- Exhaustive targeted prompt searches were valuable: they found stale mirror and direct-review wording outside the four initially obvious prompts.
- Testing under Windows PowerShell 5.1 caught default-parameter and hashing API assumptions that a current-shell-only check missed; shared Windows helpers should continue receiving that compatibility pass.
- A concise deterministic router/index validator provides clear safety evidence without claiming semantic correctness or rewriting historical task state.
- Developer follow-up correctly distinguished operational instructions from implementation-history explanations; future instruction work should apply that filter before adding defensive text for a migration that is already complete.
