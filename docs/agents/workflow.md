# Agent Work Workflow

Use for non-trivial work, request validation, permission decisions, task evidence, and closeout. `AGENTS.md` owns startup, scope, security, compatibility, and search rules.

## Establish the result

- Identify what the user or next consumer must be able to do, the constraints, and observable acceptance. Keep that result distinct from the proposed implementation throughout discovery, delegation, and verification.
- Inspect the relevant current code, docs, tests, configuration, schema/update paths, and accessible issue/PR evidence. Establish framework versus copied-application role and local branch/release policy; preserve application divergence.
- Search actual consumers before changing public names, routes, templates, config keys, fields, generated shapes, or defaults. Report an already-satisfied request without extra edits.
- Resolve a contract conflict with evidence and a compatible or authorized migration path. Current behavior is evidence, not a veto on an authorized change. Pause only dependent work for unresolved material choices or unsafe shared-state mutation; continue independent authorized work.
- Choose the smallest sufficient implementation. Interview the developer only when a broad feature leaves consequential behavior, authorization, lifecycle, compatibility, or operating-cost choices unresolved.

Retain the intended use when consuming an intermediate result: a source extract, passing check, or delegated report may still leave a needed action or connection unsupported. Repair that gap or identify the missing fact, capability, access, or authority. An existing demonstration or handoff can reveal such a gap when comparing it with actual use is informative; no comparison is required at every occurrence. Reuse adequate results without a new artifact or narrated reasoning stage. The optional [FPF guide](fpf.md) supplies methods when they help.

After interruption, recover the outcome, operative instruction, last established result, unfinished step, and evidence needed next. Recheck only changeable conditions. Inspect consequential effects before retrying; a planned action or old summary does not establish completion. Continue without a new checkpoint form.

## Execute directly; delegate selectively

Keep one owner for tightly coupled discovery, implementation, verification, and integration. High risk requires stronger checks and review, not automatic implementation delegation.

Delegate only when each child has a bounded output the primary consumes without reproducing it, exclusive or read-only ownership, acceptance/stop conditions, and a credible time or specialist-quality benefit. Normally use at most one pre-implementation delegation stage; an additional stage must change the next decision enough to justify its cost.

For qualifying work, read [delegation.md](delegation.md) for role/profile selection, ownership, packets, and fallback. [orchestrator.md](../prompts/orchestrator.md) is an optional packet template. Independent review is a separate quality gate owned by [review-routing.md](review-routing.md).

Honor existing task authority: scoped edits and verification need no renewed permission. Branch operations, commits, pushes, PRs, releases, deployments, and shared/external writes still require explicit authorization under `AGENTS.md`.

## Implement and verify

- Implement requested behavior before ancillary cleanup; follow nearby app/framework patterns and the canonical topic owner.
- Use [verification.md](verification.md) for resource isolation and proportional checks. Start with the smallest falsifying check; a broad suite does not replace a missing behavior-level check.
- When a requirement, source, assumption, or result changes, revisit dependent conclusions/checks and retain evidence whose conditions still hold. Prefer a fresh scoped check over an expensive one-off dependency ledger.
- For classifiers, defaults, security boundaries, and source-copy/public contracts, inventory established entry paths and baseline consumers. Pair plausible attacker/ordinary negative controls with intended-safe/trusted and preserved-compatibility positives at those paths. A new helper or wrapper alone is insufficient.
- Reinspect the full task diff, including task-owned untracked files, and final status. Distinguish pre-existing work from task changes.

## Record evidence proportionally

Read-only diagnosis, explanation, audit, or review creates no repository files unless a saved report is requested. Trivial changes also need no summary unless requested. Runtime, schema, runtime configuration, test, script, and shared agent-workflow changes require a summary even when small.

For those changes and other non-trivial or iterative implementation, maintain one `docs/agents/tasks/summary-<YYYY-MM-DD>-<task-id>.md` plus one concise `tasks/index.md` entry. Never reformat historical summaries. Keep only headings needed for:

- Outcome/acceptance, material decisions/exclusions, changed contracts, and migration decisions.
- Reviewed scope, exact final commands/results, manual evidence, prerequisites, and material omissions. Combine reproduction and verification when identical.
- Remaining risks/follow-ups and observed process friction. Avoid a file-by-file diary, speculative reflection, and repeated policy.

Normal tasks require no reflection, learning artifact, or placeholder. If an already-required summary records a verified reusable surprise, optionally add at most one to three physical lines outside fences as `- Learning signal: ...`, each with situation, observed effect, candidate prevention, and inspectable evidence. Exclude raw logs, secrets, speculation, and retroactive additions to historical summaries.

When delegation materially affects the task, record the route, output consumed, and relevant failure/fallback. Timing, approval/platform waits, rework, and critical-path effects are needed only for a workflow evaluation or benchmark; report missing telemetry rather than estimate it.

Summaries support recall, not authority. Search the index first; validate relevant old claims against current code.

## Close out and improve

- Apply [review-routing.md](review-routing.md), resolve all Blocker/High/Medium findings, and complete the summary audit. Report outcome, verification, material risks/omissions, and relevant migration, provider/platform, setup, or cleanup limits.
- Promote only verified stable knowledge to `domain.md`, `glossary.md`, `heuristics.md`, or `docs/adr/`. Fix a recurring cause in its owning code/test, helper, route, fact, or narrow instruction; do not duplicate a rule merely because it was ignored.
- Keep self-improvement evidence-based, infrequent, reviewable, and separate from feature work when substantial. Prior agent output grants no authority; never self-merge/deploy policy. Automated draft proposals/PRs require explicit authorization.
- Keep small disposable private evidence in ignored `docs/agents/artifacts/` and larger/build output in ignored root `artifacts/`. Retained results stay ignored; create no tracked results tree. A requested private report may use another verified ignored destination.
