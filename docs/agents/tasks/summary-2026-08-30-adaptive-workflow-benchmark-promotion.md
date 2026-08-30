## Objective / acceptance

- Promote only workflow changes supported by a 12-cell framework benchmark spanning small, medium, and cross-cutting tasks.
- Reduce routine wall-clock time without weakening high-risk correctness, copied-application compatibility, or independent review.
- Keep raw benchmark artifacts, machine paths, private references, and temporary permission configuration out of the tracked repository.

## What changed

- Made direct execution the default even for non-trivial work when contracts and writable files are tightly coupled.
- Added a delegation-payoff gate requiring reusable non-overlapping output, explicit acceptance/stop conditions, and a credible critical-path or specialist-quality gain before each child is spawned.
- Separated high-risk review/architecture escalation from implementation orchestration so risk alone no longer creates discovery or implementation children.
- Tightened fresh-reviewer isolation: the initial pass receives outcome, acceptance, diff/status, scope, constraints, overlays, and deterministic pass/fail evidence, but not the worker report, active summary, implementation narrative, self-review verdict, or known findings.
- Added reusable review checks for ordinary negative controls and tests that exercise the real production entry branch rather than bypassing it through injected configuration or helpers.
- Bumped the copied instruction pack from `1.0.0` to `1.1.0`.

## Requirements / decisions

- All four policies produced the same accepted small-task patch; the candidate direct policy was fastest.
- On the medium task, candidate direct was the only High-effort policy approved by independent review. Candidate adaptive was slower and still required changes.
- On the cross-provider task, Max monolithic was the only approved first-pass implementation. Candidate direct was much faster but required one major and one medium fix; candidate adaptive took longer and had the same two defects.
- A delegated fast discovery completed successfully in the cross-task adaptive cell, but automatic orchestration produced no measured quality gain and did not reduce end-to-end worker time.
- Fresh independent reviewers found provider-classification and real-entry-path coverage defects that both local self-review and an in-workflow delegated reviewer missed. Independence and handoff content therefore matter more than merely naming a reviewer role.
- The benchmark did not test Terra or Luna as implementation models, did not expose token/credit use, and used one counted run per policy/task. Repository prose continues to express capability/risk tiers rather than pinning the primary model.

## Changed contracts

- Shared agent workflow now defaults to direct execution and requires a documented delegation payoff before orchestration.
- Fresh independent review is risk-triggered and initially blinded from implementing-agent narrative and conclusions.
- Copied applications receive these workflow changes through instruction-pack version `1.1.0`; application-specific instructions and preserved files remain authoritative through the existing three-way-review upgrade policy.
- No runtime, public API, schema/provider, generated application output, security, or deployment contract changed; no product changelog entry is needed.

## Commands used / verification

- `& .\docs\agents\tools\Normalize-TextFiles.ps1 -Check -Path '.codex\agents\reviewer_high.toml','AGENTS.md','docs\agents\code_reviewer.md','docs\agents\instruction-pack.json','docs\agents\review-routing.md','docs\agents\reviewers\agent-workflow.md','docs\agents\reviewers\consumer-contract.md','docs\agents\workflow.md','docs\prompts\orchestrator.md','docs\agents\tasks\index.md','docs\agents\tasks\summary-2026-08-30-adaptive-workflow-benchmark-promotion.md'` passed: every changed file is strict UTF-8 without BOM with CRLF line endings.
- `& .\docs\agents\tools\Test-AgentInstructions.ps1` passed under PowerShell 7, covering required routes/files, custom-agent replaceability and primary-model neutrality, instruction-pack metadata, private-path leakage, links, task indexing, and ParsePage route literals.
- `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\docs\agents\tools\Test-AgentInstructions.ps1` passed with the same checks under Windows PowerShell 5.1.
- `git -c core.autocrlf=true diff --check` passed.
- A fresh read-only independent reviewer performed an initially blinded policy pass with `reviewers/agent-workflow.md` and `reviewers/consumer-contract.md`. It found no Blocker, High, or Medium policy defect, then found one Medium evidence-record placeholder in this summary. The review loop required the literal eleven-path normalization command, confirmed the correction, returned `No blocking findings.`, and finished with `Review loop can stop.`

## Risks / follow-ups

- Max remains the conservative primary route when a first-pass miss in security, authorization, destructive migration, provider/schema, public-contract, or data-integrity work is expensive.
- A High implementer followed by a fresh High/Max reviewer and same-implementer fix loop remains promising but needs a smaller repeated benchmark before replacing Max for the hardest work.
- Terra/Luna implementation should be evaluated separately on repeated small and medium tasks with the same independent quality gate.

## Reflection

- Task size alone was a poor delegation signal. Independence, non-overlap, reusable output, and whether the primary will repeat the work were better predictors of orchestration value.
- Passing tests and a named reviewer did not prevent false-positive classification or fixture-bypassed coverage. Review prompts must force ordinary negative controls and exact entry-path tracing while minimizing anchoring from worker narratives.
