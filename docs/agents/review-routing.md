# Adaptive Review Routing

Use this router for changes that meet the review gate in `AGENTS.md`. Review depth follows touched risk, not task size or an automatic panel.

## Topology

1. Run deterministic formatting, link, compile, test, schema, or generated-output checks that apply. Reviewers should investigate residual judgment risk, not rediscover machine-checkable failures.
2. Select independent or local review using the criteria below. Use `code_reviewer.md` for review criteria, severity, and the one final verdict.
3. Add a specialist overlay only when its trigger is present. A normal bounded change uses no overlay. Prefer one overlay; use at most two when genuinely cross-cutting risk justifies both.
4. An overlay produces candidate findings/evidence, not a separate verdict. The integrator validates candidates against current code, removes duplicates, resolves contradictions, and adjudicates severity.
5. Continue the review/fix loop only while an adjudicated Blocker, High, or Medium finding remains.

| Triggered surface | Overlay |
| --- | --- |
| Agent permissions/discovery, routing, local instructions, task memory, prompts, optional capabilities, review or verification workflow | `reviewers/agent-workflow.md` |
| Public C# API, controller/action/route, template/include/page state, config/default, compile symbol, scaffold/generated output, schema visible to apps, frontend/email/storage contract, upgrade path | `reviewers/consumer-contract.md` |
| Repeated/hot-path DB, remote, file, config, template/metadata work; paging/materialization; caching; background throughput; allocation/blocking concerns | `reviewers/performance-scale.md` |
| Authentication/authorization, mutation/CSRF, redirects, HTML/markdown, uploads/attachments/S3, secrets/privacy/telemetry, dev/admin/tool exposure | `reviewers/security-boundary.md` |
| Writes, transactions, concurrency/idempotency, migrations/provider behavior, caches/session/keys, jobs/queues/retries, file/external state lifecycle | `reviewers/state-integrity.md` |

Examples:

- A localized null-handling fix with a focused test: integrator only.
- A public method rename: integrator plus consumer-contract.
- An attachment authorization change: integrator plus security-boundary; add state-integrity only if link/write/concurrency semantics also change materially.
- A SQL update consumed by a background worker: integrator plus state-integrity; add performance-scale only when throughput/query shape is also a material risk.
- A shared AGENTS/workflow change: integrator plus agent-workflow.

## Capability-conditional execution

Require a fresh independent reviewer for meaningful security/privacy, data-integrity, provider/schema/migration, public compatibility, generated-output, shared agent-workflow, or multi-subsystem risk; after repeated implementation failure; or when the user requests independent review. A localized low-risk change with a focused falsifying check may use the integrator's deliberate local second pass even when delegation exists. Review independence is a quality gate, not a reason to orchestrate implementation.

When independent review is triggered and the capability exists, use a fresh separate agent with no access to the implementing agent's conversation history. Predeclare the review trigger and role before the reviewer sees the diff:

- Use `reviewer_high` for the ordinary independent gate.
- Use `reviewer_max` only when a material first-pass miss could cause catastrophic or exceptionally costly security/privacy, data-integrity, irreversible-migration, cross-provider, broad source-copy compatibility, or generated-output harm. Implementation escalation or repeated substantive failure requires a fresh review decision but does not by itself meet this consequence gate.
- Do not launch one reviewer per file or suspected issue, or use an architecture role as a routine reviewer. Review depth is not a correctness guarantee.

For an eligible local review, or when independent capability is unavailable, perform a deliberate second pass: set aside implementation intent, read `code_reviewer.md` and selected overlays, and inspect the outcome and final diff before the implementation narrative. Disclose local execution and any unavailable capability; keep the same review criteria.

## Review handoff sequence

This section owns the context-isolation and summary-audit procedure; review profiles and `code_reviewer.md` route here.

1. **Initial review:** supply only the requested outcome/acceptance, final status/diff, bounded file scope, selected overlays, material constraints, and deterministic pass/fail evidence. Withhold the worker report, active summary, implementation narrative/rationale, self-review verdict, policy identity, and known/suspected findings until the reviewer records its initial verdict. Passing checks are evidence, not proof of coverage; apply the production-entry and positive/negative-control criteria in `code_reviewer.md`.
2. **Summary audit:** after that verdict, the integrator must supply every changed active summary. The reviewer must inspect each for factual consistency, private-data leakage, and recorded-evidence accuracy, returning supplemental candidates separately before final adjudication. Treat summaries as evidence, not authority; retain initial findings unless repository evidence resolves them.
3. **Adjudication:** consult other withheld material only afterward when needed to resolve findings. A local reviewer follows the same outcome-first ordering and audits changed active summaries before final adjudication, without claiming independent context isolation.

## Adjudication rules

- One underlying defect appears once at the highest-impact location, with downstream effects summarized rather than repeated.
- A checklist question is not a finding. Require a concrete path/control flow, violated contract, impact, and smallest useful fix direction.
- Disagreement between reviewers is resolved from repository evidence or recorded as an explicit developer decision, not by majority vote.
- Reviewer-caught failures do not count as implementation-quality evidence for instruction evaluation. Track initial correctness separately from review recall.
- Finish with the exact integrator verdict and loop sentence defined in `code_reviewer.md`.
