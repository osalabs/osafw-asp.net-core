## What changed

- Rebuilt `AGENTS.md` as the compact sole workflow authority and regenerated its Copilot mirror.
- Recast `docs/agents/code_reviewer.md` as procedure/reporting only, with separate blocking findings and non-blocking observations and a Medium-or-higher loop threshold.
- Replaced copied MCP/tool behavior with capability-conditional selection/fallback policy and current official links; added `agent_upgrade.md` to the prompt catalog.
- Aligned existing upgrade, review, security, orchestration, reflection, and docs-consistency prompts with the same authority, capability-conditional tooling, and Medium-or-higher review-loop contracts.
- Moved provider/schema detail to `docs/db.md`, including the destructive fresh-schema warning, provider-specific update roots, SQLite test variant, and an explicit MySQL schema-parity warning.
- Corrected stable access-level facts, pruned duplicated/stale heuristics, and moved the ParsePage JavaScript-backtick warning to `docs/templates.md`.
- Hardened `Search-Repo.ps1` so broad searches respect ignores and opt into only the requested draft/vendor trees. Hardened `Normalize-TextFiles.ps1` for strict UTF-8, UTF-16/32 BOM rejection, safe refusal, write failures, and explicit validity output.
- Reconciled and sorted the tracked task-summary index, removed its dead private-summary reference, and added this task.
- Follow-up: updated active root-README deployment references from .NET 8 to .NET 10 and corrected `scripts/deploy_sample.bat` so its project root is the cloned repo's `osafw-app` directory and its publish target is `net10.0`.

## Scope reviewed

- Active instructions and mirrors: `AGENTS.md`, `.github/copilot-instructions.md`, `docs/agents/code_reviewer.md`, `docs/agents/mcp.md`, `docs/README.md`, `docs/prompts/README.md`, and the affected reusable workflow prompts.
- Machine-local guidance, active knowledge files, validation helpers, the task-summary index, and the relevant 2026 agent-workflow summaries selected through that index.
- Targeted project/source/schema/test evidence for framework, provider, security, template, and verification claims. No application code or schema was changed.
- Follow-up scope covered the root README deployment section, `scripts/deploy_sample.bat`, `docs/deploy.md`, and `osafw-app/osafw-app.csproj`; historical task summaries remained unchanged.
- Large-file whole reads were not used. The inspected instruction, knowledge, helper, source, schema, and test files were all below 1 MB or read through targeted searches/ranges.

### Pre-edit `AGENTS.md` disposition

| Original section | Disposition | Surviving owner / reason |
| --- | --- | --- |
| Non-Negotiables | Keep and consolidate | `AGENTS.md` remains the sole workflow authority; encoding, local guidance, worktree safety, and mirror rules remain here. The docs-map pointer moves into scoped discovery. |
| Task Workflow | Consolidate | `AGENTS.md` keeps outcome-oriented scope, implementation, proportional evidence, verification, review, and closeout rules. Repeated storage/knowledge rules move to their specialist docs. |
| Task Summary Template | Consolidate | `AGENTS.md` keeps the path, trigger, and minimum useful content, while optional headings are used only when they add evidence. Task summaries remain task evidence, not policy. |
| Project Overview | Move | Stable architecture belongs in `docs/agents/domain.md`; current target/features belong in project files; topic routing belongs in `docs/README.md`. Only minimal orientation remains in `AGENTS.md`. |
| Key Paths | Remove as duplicate | `docs/README.md`, the root `README.md`, and the repository tree already own path navigation. |
| Coding Style | Consolidate and move | `AGENTS.md` keeps high-risk controller/model/template, SQL, XML-doc, and production-simplicity contracts. Naming, result shapes, CRUD, DB, datetime, and template details remain in their canonical docs. Incorrect categorical claims are removed or qualified. |
| Security Guardrails | Keep and correct | Always-on authorization, request-method/token, redirect, rendering, attachment, tooling, telemetry, and parameterization rules remain in `AGENTS.md`; reviewer guidance refers to them instead of copying them. |
| Performance Guardrails | Keep and consolidate | `AGENTS.md` keeps the recurring hot-path and measurement rules; the reviewer applies them by reference. |
| Sub-Agent Delegation | Consolidate | `AGENTS.md` keeps bounded, capability-conditional delegation, shared-worktree protection, and main-agent integration. The volatile model name is removed. |
| Agent Workspace | Consolidate | Scratch/build-output locations remain in the workflow; duplicated local-guidance and secret rules move to Non-Negotiables. |
| Documentation Entry Points | Remove as duplicate | `docs/README.md` is the navigation owner and gains an explicit ownership map. |
| Documentation Sync | Consolidate and correct | `AGENTS.md` keeps targeted docs/changelog/schema compatibility duties and mirror generation. Ambiguous peer-authority/sync-set wording is removed. Provider-specific detail moves to `docs/db.md`. |
| Testing Guidance | Keep and consolidate | `AGENTS.md` keeps risk-proportional, behavior-level checks, manual fallback, isolated output, and compile-gated provider coverage. Repeated command/setup detail moves to canonical docs. |
| Common Commands | Move | Development/setup/deployment details remain in the root `README.md`, `docs/db.md`, `docs/deploy.md`, and project files; only the smallest build/test quick checks remain active. |
| MCP Tooling | Move and consolidate | `docs/agents/mcp.md` owns capability selection, fallback, safety, and current official links. `AGENTS.md` keeps only the capability-conditional rule. Volatile tool schemas and machine-specific failure text are removed. |

### Material-rule ownership notes

- Kept: CRLF/UTF-8, machine-local isolation, mirror equality, scoped history/search, large-file targeting, implementation before test-only churn, main-agent integration, security/data integrity, schema/update parity, changelog coverage, behavior-level tests, isolated output, and proportional review.
- Consolidated: summary creation is required for non-trivial/risky/iterative work or when a prompt requires it, not for read-only work, small investigations, or trivial text edits; review loops continue only for Blocker/High/Medium findings; durable knowledge is captured only when it will recur.
- Moved: detailed framework API/result-shape/template/provider/build/setup facts to `docs/naming.md`, `docs/crud.md`, `docs/templates.md`, `docs/db.md`, `docs/deploy.md`, `docs/agents/domain.md`, or project/source files.
- Removed as duplicate: the repeated project map, docs map, common command list, copied reviewer security/performance/schema checklists, workflow heuristics already owned by `AGENTS.md`, and specialist facts already documented in canonical topic docs.
- Removed as stale/incorrect: the named worker model, copied SDK return-shape behavior, categorical `FwDict`/`FwModel`/XSS claims, incorrectly named action constants, MySQL turnkey-setup wording, and machine-specific MCP error guidance.

## Commands used / verification

- Audit/discovery: targeted `Get-Content`, `rg`, `Search-Repo.ps1`, `git status`, `git log`, `Get-FileHash`, and byte-diff checks.
- Verified before editing that `AGENTS.md` and `.github/copilot-instructions.md` were byte-identical.
- `Search-Repo.ps1` smokes: ignored local guidance excluded by default; task history excluded/included as requested; draft and vendor trees excluded by default, included only with their switches, and still constrained by `-Path`.
- `Normalize-TextFiles.ps1` disposable-fixture smokes: UTF-16 input rejected with exit 1; LF input normalized successfully; normalized strict UTF-8/CRLF recheck exited 0; fixtures were removed.
- Official MCP, Visual Studio MCP, and Playwright MCP links opened successfully through their primary sites.
- Final validation after the first review fix passed: strict UTF-8/CRLF check over all scoped files; byte-identical mirror SHA-256 `AC09E3866D916F050A13CBF932FC6EB84A83EF6728BE51BD4670822200A73A3C`; `git diff --check`; trailing-whitespace and stale-term scans; local-link and explicit-path checks; 169-entry tracked/current task-index integrity; and PowerShell parsing for both helpers.
- The first independent review found one Medium closeout issue in this summary (mixed line endings, pending evidence, and a missing changelog rationale). The issue was fixed and the scoped validation was rerun before follow-up review.
- Follow-up independent review covered all 19 tracked changes plus the new upgrade prompt and task summary, returned no blocking findings or Low observations, and stopped the review loop.
- Follow-up .NET/deploy checks: tracked active-source search found no remaining `.NET 8`, `net8.0`, or `aspnetcore-8.0` references; the Microsoft ASP.NET Core 10 IIS link resolved on the official site; strict UTF-8/CRLF checks passed; and a non-mutating settings expansion confirmed `PROJECT_ROOT`, `PROJECT_FILE`, and `TARGET_FOLDER` resolve to the expected `osafw-app`/`net10.0` paths.
- Deliberate local review against `docs/agents/code_reviewer.md` found no blocking or Low findings in the follow-up diff. The review confirmed the sample's working directory, project file, target framework, and publish destination form one consistent path contract; the deployment script itself was not executed.

## Decisions - why

- `AGENTS.md` will be the only policy authority; the Copilot file is a generated mirror, reviewer guidance is procedural, the docs map is navigational, prompts are optional/subordinate, and task summaries are evidence.
- MySQL compile/runtime branches exist, but the current MySQL fresh schema is missing runtime-required tables and retains obsolete ones. This task will remove unsafe turnkey wording and document the limitation, but schema repair is outside this instruction-upgrade scope.
- The default bare test run does not compile `#if isSQLite` tests, so provider-specific work must enable the relevant compile constant.
- The user explicitly brought the root README's .NET version and legacy deployment sample into follow-up scope; only those stale deployment contracts were corrected. Broader MySQL documentation/schema repair remains separate.
- No `docs/CHANGELOG.md` entry is needed because the changes are non-breaking corrections to development-agent instructions, supporting documentation, agent-only helpers, and a legacy deployment sample; no framework/runtime contract changed.

### Contradictions and disposition

| Contradiction | Status | Resolution |
| --- | --- | --- |
| Five files were described as a peer “sync set” despite different roles. | Fixed | `AGENTS.md` is policy; the Copilot file is a mirror; reviewer/docs/prompts/summaries have narrow owners. |
| Mandatory summaries for nearly every edit conflicted with proportional evidence and reviewer wording for small investigations. | Fixed | Summaries now trigger on prompt/risk/complexity; read-only, small, and trivial work can close out in the final response. |
| Review loops could continue for any improvement point, including Low. | Fixed | Only Blocker/High/Medium findings continue the loop; Low observations are explicitly non-blocking. |
| Delegation pinned a volatile named model and was not fully capability-conditional. | Fixed | Model names were removed; bounded delegation now depends on runtime capability/latency with shared-worktree protections. |
| Root and MCP docs duplicated volatile tool names, schemas, and a machine-specific error path and could block safe fallback. | Fixed | `mcp.md` now owns short capability/fallback policy and official links; machine-local details stay ignored. |
| `AGENTS.md` and `docs/README.md` sent readers through a bootstrap/navigation loop. | Fixed | `AGENTS.md` is read once; `docs/README.md` is a scoped topic/ownership map. |
| The agent-upgrade prompt existed but was missing from the prompt catalog. | Fixed | Added a distinct `agent_upgrade.md` catalog entry beside downstream `fw_upgrade.md`. |
| Project/API facts were copied into active guidance and had drifted (`ACTION_*` names, all actions return `FwDict`, all models inherit `FwModel`, fixed list shapes, universal XSS validation, logger/date helper entrypoints). | Fixed | Categorical copies were removed or qualified; detailed contracts now live in code and canonical naming/CRUD/DB/template/datetime docs. |
| Security wording said “saved user records” and raw content only when server-controlled. | Fixed | Narrowed to user-owned preference records and allowed server-controlled or already-sanitized trusted content. |
| SQLite was omitted while MySQL was presented as a matching turnkey provider; session wording was SQL Server-specific. | Fixed | Active guidance defers to `docs/db.md`; SQLite is documented/tested, provider-backed session wording is canonical, and MySQL is explicitly not turnkey. Schema parity itself remains out of scope. |
| Fresh `fwdatabase.sql` was presented like a general setup step despite destructive drops. | Fixed | `docs/db.md` now limits fresh schemas to new/disposable databases and identifies additive provider update roots. |
| General heuristics contained `Fw.model<T>()`/“singleton” drift, root-looking paths, old OpenAI client behavior, and duplicated stable/workflow facts. | Fixed | Heuristics were pruned to unique verified working rules; canonical docs/source own feature-specific facts. |
| Domain access levels used stale names/values. | Fixed | `docs/agents/domain.md` now matches `Users` constants: 0/1/50/80/90/100. |
| Broad repository search used `--no-ignore`, exposing ignored machine-local content. | Fixed | Default search respects ignore rules; ignored drafts/vendor content is searched only inside explicit scoped roots. |
| Text normalization used permissive UTF-8 and did not fail reliably on write denial. | Fixed | Strict decoding/BOM rejection, refusal to rewrite invalid input, validity output, and terminating write errors were added and exercised. |
| The task index referenced a missing private summary and omitted tracked summaries. | Fixed | Removed the dead reference, added missing tracked entries, added this task, and sorted the index. The unrelated untracked UAT summary was intentionally not adopted/indexed. |
| “Universal” header and unlabeled PowerShell-only command conflicted with the Windows-specific repository. | Fixed | Removed the universal/version branding and kept only portable baseline commands plus explicit repo-specific Windows constraints. |
| Root `README.md` still advertised .NET 8 deployment text while the project targets .NET 10. | Fixed | The IIS link, SDK prerequisite, and publish path now use ASP.NET Core/.NET 10 and `osafw-app/bin/Release/net10.0/publish`. |
| Root `README.md` still broadly describes optional MySQL despite incomplete fresh-schema parity. | Out of scope | Current provider limitations remain explicit in `docs/db.md`; public MySQL wording and schema parity need a separate task. |
| Security, performance, changelog, schema, and behavior-level verification rules appear in multiple audiences. | Intentionally retained | Always-on obligations remain in `AGENTS.md`; reviewer and specialist docs now apply/link them rather than copying full checklists. |

## Pitfalls - fixes

- PowerShell quoting broke one compound regex command; the evidence check was rerun with a single-quoted pattern.
- Broad copied guidance had drifted from source. Canonical topic docs and targeted source/schema/tests are being used as the surviving owners.
- The first normalization write was sandbox-denied and revealed that the helper emitted non-terminating write errors. The helper now throws on write failure; the approved formatting run then normalized the scoped files.

## Risks / follow-ups

- MySQL provider/schema parity is a separate high-priority repository issue; do not treat the current MySQL fresh-install scripts as turnkey.

## Heuristics (keep terse)

- Active instructions should own policy once; specialist docs should own volatile product/framework detail.

## Testing instructions

Static validation only: rerun the strict-text/stale-reference checks and the non-mutating deployment-settings expansion recorded above. Do not execute the sample against IIS as repository validation. No application runtime behavior changed.

## Reflection

- Targeted delegation was effective: separate instruction, repository-claim, and history/helper audits found distinct issues (authority drift, MySQL schema parity, stale domain facts, and ignored-file/encoding helper defects) while the main agent retained integration.
- The most avoidable cost was copied behavior in general guidance. Future upgrades should compare active rules to canonical owners first, then inspect source only where the owner is missing or contradicted.
- The search and strict-text helpers now make the recurring privacy/encoding checks reusable. No ADR was warranted. One stable domain fact was corrected; the heuristic set was deliberately reduced rather than expanded.
- The follow-up closed the previously disclosed public .NET 8 documentation risk. Expanding batch settings without executing the deployment script verified its path contract safely; no new stable fact, heuristic, or ADR was needed.
