# Code Review Procedure

Use this procedure for final review of runtime source, schema, templates, scripts, tests, runtime-affecting configuration, or risky shared developer/agent workflow changes.

The reviewer is an independent quality gate. Review the requested outcome and final changed work as a skeptical senior engineer. Do not rewrite the implementation unless the caller explicitly asks for fixes.

## Inputs

- Read `AGENTS.md` as the sole repository workflow and cross-task guardrail authority.
- Read `docs/agents/local_instructions.md` when present, but never expose or commit its contents.
- Read the active task summary when one is required or supplied. Search `docs/agents/tasks/index.md` before opening other historical summaries.
- Inspect `git status --short`, the relevant diff/stat, and untracked task files in scope. Preserve unrelated worktree changes.
- Read only the nearby implementation, tests, schema, templates, and canonical topic docs needed to understand the changed contract. Use `docs/agents/tools/Search-Repo.ps1` for a justified broad search.

## Review Priorities

Check in this order, focusing depth where failure would matter:

1. Requirements and correctness: does the real control flow and data shape implement the requested behavior, including important error and empty cases?
2. Contracts: are routes, controller/action behavior, template/page-state shapes, JSON payloads, public APIs, labels/statuses, and compatibility expectations preserved or deliberately changed?
3. Data integrity and schema: are writes, predicates, transactions, defaults, nullable values, linked records, date/time handling, fresh schemas, and additive updates safe for every affected provider?
4. Security and privacy: apply all relevant `AGENTS.md` security/data-integrity guardrails at the actual read/write/render/serve boundary.
5. Performance and resource use: apply the `AGENTS.md` hot-path guardrails without proposing speculative or invasive optimization.
6. Project fit: does the change follow nearby osafw controller/model/template/config patterns and the canonical topic docs?
7. Simplicity: could the same behavior use fewer moving parts, less state, a narrower public/config surface, or an existing pattern? Flag shallow wrappers, test-only seams, duplicated branches, unjustified defaults/casts, and comments/docs that restate code only when they create real maintenance cost.
8. Tests and verification: can the recorded checks falsify the changed behavior at the nearest practical boundary? Are meaningful compile/provider/manual variants missing?
9. Documentation and upgrade impact: are affected canonical docs, provider paths, and `docs/CHANGELOG.md` updated, or is the no-update decision supported in the task summary?

For specialist contracts, consult the canonical document rather than copying its rules here: `docs/naming.md`, `crud.md`, `db.md`, `templates.md`, `dynamic.md`, `datetime.md`, `deploy.md`, and `assistant.md` where relevant.

## Report Format

Start with one verdict:

- `Changes required.` when at least one Blocker, High, or Medium finding remains.
- `No blocking findings.` when only Low observations remain or there are no issues.

Then use these sections as applicable:

```md
## Blocking Findings

### Medium - Provider update path is incomplete
- Location: `path/to/file:line`
- Problem: ...
- Impact: ...
- Fix direction: ...

## Non-Blocking Observations

### Low - Naming can be clearer
- Location: `path/to/file:line`
- Observation: ...
- Optional improvement: ...

## Verification Reviewed

- Diff/files reviewed: ...
- Tests reviewed or run: ...
- Residual risk: ...
```

Every finding must identify severity, a tight location, the evidence-based problem, impact, and the smallest useful fix direction. If there are no findings, still include `Verification Reviewed`.

## Severity and Loop Rules

- `Blocker`: likely security/privacy exposure, data corruption, deploy breakage, irreversible schema/update risk, or a core flow cannot work.
- `High`: likely production bug in an important flow, broken public/admin/template/data contract, missed authorization, or missing required migration.
- `Medium`: edge-case correctness bug, meaningful verification gap, documentation/compatibility drift, or avoidable maintenance risk that should be fixed before closeout.
- `Low`: optional cleanup, naming, or clarity improvement with no material correctness, security, compatibility, or maintenance risk.

Blocker, High, and Medium findings are blocking and require another review/fix loop. Low observations are non-blocking; do not continue the loop solely for them.

## Operating Rules

- Be specific and evidence-based. State assumptions and how to verify them.
- Prefer fewer high-signal findings over checklist recital. Do not report pure style preference.
- Name the smallest behavior-preserving fix and the contract it keeps. Do not request broad rewrites, speculative architecture, or deduplication that hides intentional differences or weakens security.
- Keep business/product judgment questions separate from findings rather than inventing policy.
- Do not repeat fixed findings unless the fix is incomplete.
- Finish with exactly `Review loop should continue.` when blocking findings remain, otherwise `Review loop can stop.`
