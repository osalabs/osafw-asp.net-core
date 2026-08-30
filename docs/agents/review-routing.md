# Adaptive Review Routing

Use this router for changes that meet the review gate in `AGENTS.md`. Review depth follows touched risk, not task size or an automatic panel.

## Topology

1. Run deterministic formatting, link, compile, test, schema, or generated-output checks that apply. Reviewers should investigate residual judgment risk, not rediscover machine-checkable failures.
2. Use `code_reviewer.md` as the broad integrator (independent when available) for requirements, correctness, project fit, verification, documentation, and the one final verdict.
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

When independent review is triggered and the capability exists, use a fresh separate agent with no access to the implementing agent's conversation history. When the `reviewer_high` profile/capability is available, use it. Its initial handoff contains only the requested outcome and acceptance criteria, final status/diff, bounded file scope, selected overlay(s), material constraints, and deterministic pass/fail evidence. Withhold the worker report, active summary, implementation narrative/rationale, self-review verdict, and suspected or previously known findings until the reviewer records its initial verdict. Deterministic successes are evidence, not proof that edge cases or the intended entry path were covered. The integrator may consult the withheld material afterward for adjudication. Do not launch one reviewer per file or suspected issue, and do not use `architect_max` as a routine reviewer.

When no independent capability is available, the implementing agent performs a deliberate second-pass review: set aside implementation intent, read `code_reviewer.md` and the selected overlay(s), inspect the final diff from the requested outcome outward, and disclose that the review was local. Unavailable delegation must not weaken the review criteria or block ordinary work.

## Adjudication rules

- One underlying defect appears once at the highest-impact location, with downstream effects summarized rather than repeated.
- A checklist question is not a finding. Require a concrete path/control flow, violated contract, impact, and smallest useful fix direction.
- Disagreement between reviewers is resolved from repository evidence or recorded as an explicit developer decision, not by majority vote.
- Reviewer-caught failures do not count as implementation-quality evidence for instruction evaluation. Track initial correctness separately from review recall.
- Finish with the exact integrator verdict and loop sentence defined in `code_reviewer.md`.
