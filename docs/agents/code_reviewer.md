# Integrating Code Review Procedure

Use this as the broad final review for runtime source, schema, templates, scripts, tests, runtime-affecting configuration, or risky shared developer/agent workflow changes. `review-routing.md` decides whether specialist overlays also apply.

Treat review as an independent-intent quality gate: use a separate reviewer when available, otherwise perform the documented local second pass. Review the requested outcome and final changed work as a skeptical senior engineer. Do not edit unless the caller explicitly asks for fixes. Produce the repository's one adjudicated verdict.

## Inputs

- Read `AGENTS.md`, `review-routing.md`, and only the selected specialist overlay(s). Read ignored local instructions when present but never expose or commit them.
- For an independent first pass, do not read the active summary, worker report, implementation rationale, self-review verdict, or known/suspected finding list. Start from the outcome, acceptance criteria, final status/diff, current contracts, and bounded deterministic evidence. The integrator may supply withheld material after the initial verdict for adjudication. A local fallback should likewise inspect outcome and diff before reading its own implementation narrative.
- Inspect `git status --short`, diff/stat, and untracked files in task scope. Preserve and distinguish unrelated work.
- Read only nearby implementation, tests, schema, templates, and canonical topic docs needed to understand the changed contract. Run applicable deterministic checks before treating their results as review judgment.
- If specialists supplied candidate findings, validate each against current code, deduplicate the underlying defect, and resolve contradictory advice from evidence or an explicit developer decision.

## Review priorities

Check in this order, focusing depth where failure matters:

1. **Requirements/correctness:** Does real control flow and data shape implement the desired outcome, including important error, empty, retry, concurrency, positive-classification, and ordinary negative-control cases?
2. **Consumer contracts:** Are public APIs, routes/actions, templates/page-state/JSON, generated output, config/defaults, schema/provider, storage/frontend/email, and copied-app expectations preserved or deliberately migrated?
3. **Data/state integrity:** Are writes, predicates, transactions, defaults/nulls, related records, date/time, fresh schemas, additive updates, jobs/retries, and cleanup safe for each claimed provider?
4. **Security/privacy:** Apply `AGENTS.md` and any security overlay at the actual read/write/render/serve/tool boundary.
5. **Performance/resources:** Apply the performance overlay only to plausible repeated/hot paths; avoid speculative rewrites.
6. **Project fit/simplicity:** Does the change follow nearby osafw controller/model/template/config patterns and canonical docs with the fewest justified moving parts? Flag wrappers, test-only seams, duplicate branches, new defaults/casts, or restating comments only when they create real cost/risk.
7. **Tests/evidence:** Can recorded checks falsify behavior at the nearest practical public boundary? Do fixtures exercise the actual production entry branch rather than injecting configuration or helpers that bypass it? Are ordinary negative controls, important compile/provider/manual variants, or clean-state checks missing?
8. **Docs/upgrade/release:** Are canonical docs, examples, provider paths, migration guidance, and `docs/CHANGELOG.md` aligned, or is the no-update decision evidenced?

Consult the canonical specialist document rather than copying its rules: `docs/naming.md`, `crud.md`, `db.md`, `templates.md`, `dynamic.md`, `datetime.md`, `deploy.md`, and `assistant.md` as relevant.

## Adjudication

- Report one root defect once at the highest-impact location; summarize dependent effects.
- Require a tight location/control flow, evidence-based problem, impact, and smallest behavior-preserving fix direction. A checklist question, unsupported suspicion, or pure style preference is not a finding.
- Do not inflate severity to force optional cleanup. Keep product/business judgment questions separate rather than inventing policy.
- Do not repeat fixed findings unless the fix is incomplete.
- When no independent reviewer capability exists, perform the routed overlays as a deliberate local second pass and disclose that fact. The verdict format remains the same.

## Report format

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
- Deterministic checks/tests reviewed or run: ...
- Specialist overlays consulted: ...
- Review execution: independent or local fallback
- Residual risk: ...
```

Every finding identifies severity, a tight location, evidence, impact, and smallest useful fix direction. Include `Verification Reviewed` even with no findings.

## Severity and loop rules

- `Blocker`: likely security/privacy exposure, data corruption, deploy breakage, irreversible schema/update risk, or a core flow cannot work.
- `High`: likely production bug in an important flow, broken public/admin/template/data contract, missed authorization, or missing required migration.
- `Medium`: edge-case correctness bug, meaningful verification gap, documentation/compatibility drift, or avoidable maintenance risk that should be fixed before closeout.
- `Low`: optional cleanup, naming, or clarity improvement with no material correctness, security, compatibility, or maintenance risk.

Blocker, High, and Medium findings require another review/fix loop. Low observations do not. Finish with exactly `Review loop should continue.` when blocking findings remain, otherwise `Review loop can stop.`
