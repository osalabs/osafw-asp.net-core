# Delegation Reference

Read only after the eligibility gate in [workflow.md](workflow.md) selects delegation, or when [review-routing.md](review-routing.md) requires profile selection. This file owns role/profile selection, packets, file ownership, and fallback; it does not authorize delegation beyond active runtime constraints.

| Bounded work | Role / condition |
| --- | --- |
| Read-only discovery | `discovery_fast`; optional `discovery_astra_low`, when its evidence replaces primary discovery. |
| Small implementation | `implementation_fast`; optional `implementation_astra_medium`, only for low-risk, well-specified work with a deterministic falsifying check and disjoint writable files. |
| Standard implementation | Default to `implementation_sol_high` for a well-defined independent packet with exclusive ownership and observable verification; unresolved material contract decisions stay with the primary. |
| Implementation escalation | Prefer `implementation_astra_xhigh` for a concrete specialist payoff: an exceptionally costly security/data/migration/compatibility miss, interacting high-risk contracts, or a substantive failed direct attempt. `implementation_astra_max` additionally requires the maximum-effort consequence gate in `docs/agents/review-routing.md`. `implementation_max` remains an alternative when explicit constraints or availability favor it and its own gate is satisfied. |
| Architecture or recovery decision | Prefer `architect_astra_xhigh` for a material architecture choice, high-risk adjudication, consequential ambiguity, or recovery after bounded attempts fail. `architect_astra_max` additionally requires the maximum-effort consequence gate in `docs/agents/review-routing.md`. `architect_max` remains an alternative when explicit constraints or availability favor it and its own gate is satisfied. |
| Independent review | Select the role and execution mode through `docs/agents/review-routing.md`. |

### Select a profile after selecting the role

- Honor explicit developer model, family, effort, and cost constraints first. Starting with a model is not a family-only restriction. Keep the developer-selected primary model unchanged.
- Select an available, role-appropriate profile by the packet's required quality, latency, cost, and consequences. Delegation may cross model generations in either direction without another confirmation when existing authority and constraints permit it. Use the primary model's family only as a tie-breaker when the task offers no stronger reason.
- Keep the fast discovery and small-implementation profiles as inexpensive helper defaults. Use their alternatives for a recorded task-specific reason or explicit developer preference. A new model or higher effort alone does not justify another agent.
- Inspect the active tool's profile settings, or its TOML file when needed. Names are handles; configured model, effort, sandbox, and instructions determine behavior. A pinned profile overrides spawn model/effort values, so choose the correct profile instead of trying to retarget a pinned role. A read-only reviewer is not an implementation worker.
- Do not invent model availability or infer quality equivalence from effort labels. If the preferred profile is unavailable, choose another available profile satisfying the same role, gate, and explicit constraints and disclose the substitution. For required independent review, a suitable fresh generic agent is the next fallback; missing named profiles do not waive available independence. Use a deliberate local pass only when no independent capability satisfies the live constraints, and disclose that limit. An explicitly required model remains a blocker for that delegated step.
- Record a short selection reason in the delegation packet; avoid per-task benchmark research. For developer starting-model advice or deliberate recalibration, consult the optional dated [model-selection note](model-selection.md).

These roles are optional capabilities, not correctness guarantees or automatic upgrades over the primary task. Durable workflow remains model-neutral; executable model/reasoning settings live in `.codex/agents/*.toml`. Dated selection advice is advisory and does not pin the primary model.

For qualifying orchestration, use `docs/prompts/orchestrator.md` as the plan and handoff template. Each packet supplies the objective/acceptance, context/exclusions, writable and read-only paths, verification/output, and stop conditions for ambiguity, contract expansion, unsafe state, or repeated failure.

Maintain a file lease list while workers run. Read-only scopes may overlap; writable scopes must be disjoint, and the primary must wait for a worker to return or revoke its lease before editing its files. Workers preserve unrelated changes and report every touched path. Reproduce delegated work only when its output is demonstrably incomplete or stale; record that failure. The primary owns shared-contract decisions, integration, user communication, final diff review, verification, and cleanup.
