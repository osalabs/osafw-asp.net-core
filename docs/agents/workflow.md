# Agent Work Workflow

Use this file for non-trivial work, request validation, task staging, permission decisions, summaries, or reusable-knowledge closeout.

## 1. Establish the working context

Follow the startup and local-instruction route in `AGENTS.md`; recheck status before closeout. The role determines what must survive the change:

- Identify repository role:
  - **Framework repository:** changes can propagate to many copied production applications. Treat public behavior, templates, defaults, schema, generated output, examples, and upgrades as consumer contracts.
  - **Copied application:** the framework tree and app code coexist. Preserve deliberate application changes, local docs, schema history, deployment rules, and repository-specific branch policy. Upstream framework guidance is input, not permission to overwrite the app.
- Determine branch/release policy from the current repository. The canonical framework uses `master`, but copied applications may not and may have different protections.

## 2. Validate the request before implementation

1. Restate the desired observable outcome internally; keep it separate from the prompt's suggested implementation. Make a lightweight pass over assumptions, possible conflicts, and the evidence needed to accept the result.
2. Inspect current code, canonical docs, tests, configuration, schema/update paths, and accessible linked issue/PR context needed to confirm the request is current. Treat unverified assumptions as questions or limits, not facts.
3. Identify affected downstream surfaces and extension points. Search references before changing or deleting public names, routes, templates, config keys, database fields/scripts, generated shapes, or defaults.
4. Report an already-satisfied request without extra edits. For a contract conflict, identify the evidence and a compatible or explicitly authorized migration path. Pause only work that depends on unresolved product/data/authorization choices or unsafe shared-state mutation; continue independent authorized work. Current implementation is evidence of behavior, not a veto on an authorized contract change.
5. Otherwise choose the smallest implementation that satisfies the outcome. Short prompts should not require the developer to restate facts already discoverable here.

This is an internal reasoning check, not a required artifact or a reason to add process. Consult the optional [FPF/DPF guide](fpf.md) only when its source material could materially change framing, a decision, or verification; routine fixes and mechanical work need no FPF lookup.

Use a focused developer interview only for a broad feature with unresolved choices that materially affect user behavior, authorization, data ownership/lifecycle, public compatibility, or operational cost. Routine fixes and well-specified enhancements should proceed without interview ceremony.

## 3. Size and stage the work

Keep work in the primary task when one owner can coherently discover, implement, verify, and integrate it, including non-trivial changes with tightly coupled contracts or files. High risk requires stronger verification and review; it does not automatically require implementation delegation.

Delegate only when each child will produce a bounded output the primary consumes rather than reproduces, with exclusive or read-only ownership, acceptance/stop conditions, and a credible time or specialist-quality benefit. Normally this means independently useful workstreams that can overlap, or an architecture/recovery decision that changes the next step. Stay direct when ownership overlaps or the primary would repeat the work. Normally use at most one pre-implementation delegation stage; add another only when its distinct output changes the next decision and justifies the delay. Independent review is a separate quality gate governed by `docs/agents/review-routing.md`.

| Bounded work | Role / condition |
| --- | --- |
| Read-only discovery | `discovery_fast`; optional `discovery_astra_low`, when its evidence replaces primary discovery. |
| Small implementation | `implementation_fast`; optional `implementation_astra_medium`, only for low-risk, well-specified work with a deterministic falsifying check and disjoint writable files. |
| Standard implementation | `implementation_sol_high`, for a well-defined independent packet with exclusive ownership and observable verification; unresolved material contract decisions stay with the primary. |
| Implementation escalation | `implementation_max` or `implementation_astra_xhigh`, only for a concrete specialist payoff: an exceptionally costly security/data/migration/compatibility miss, interacting high-risk contracts, or a substantive failed direct attempt. `implementation_astra_max` additionally requires the maximum-effort consequence gate in `docs/agents/review-routing.md`. |
| Architecture or recovery decision | `architect_max` or `architect_astra_xhigh`, only for a material architecture choice, high-risk adjudication, consequential ambiguity, or recovery after bounded attempts fail. `architect_astra_max` additionally requires the maximum-effort consequence gate in `docs/agents/review-routing.md`. |
| Independent review | Select the role and execution mode through `docs/agents/review-routing.md`. |

### Select a profile after selecting the role

- Honor explicit developer model, family, effort, and cost constraints first. Starting with a model is not a family-only restriction. Keep the developer-selected primary model unchanged.
- Select an available, role-appropriate profile by the packet's required quality, latency, cost, and consequences. Delegation may cross model generations in either direction without another confirmation when existing authority and constraints permit it. Use the primary model's family only as a tie-breaker when the task offers no stronger reason.
- Keep the fast discovery and small-implementation profiles as inexpensive helper defaults. Use their alternatives for a recorded task-specific reason or explicit developer preference. A new model or higher effort alone does not justify another agent.
- Inspect the active tool's profile settings, or its TOML file when needed. Names are handles; configured model, effort, sandbox, and instructions determine behavior. A pinned profile overrides spawn model/effort values, so choose the correct profile instead of trying to retarget a pinned role. A read-only reviewer is not an implementation worker.
- Do not invent model availability or infer quality equivalence from effort labels. If the preferred profile is unavailable, choose another available profile satisfying the same role, gate, and explicit constraints and disclose the substitution. If none qualifies, perform the stage locally with the same evidence and stop conditions; disclose the fallback and any loss of review independence. An explicitly required model remains a blocker for that delegated step.
- Record a short selection reason in the delegation packet; avoid per-task benchmark research. For developer starting-model advice or deliberate recalibration, consult the optional dated [model-selection note](model-selection.md).

These roles are optional capabilities, not correctness guarantees or automatic upgrades over the primary task. Durable workflow remains model-neutral; executable model/reasoning settings live in `.codex/agents/*.toml`. Dated selection advice is advisory and does not pin the primary model.

For qualifying orchestration, use `docs/prompts/orchestrator.md` as the plan and handoff template. Each packet supplies the objective/acceptance, context/exclusions, writable and read-only paths, verification/output, and stop conditions for ambiguity, contract expansion, unsafe state, or repeated failure.

Maintain a file lease list while workers run. Read-only scopes may overlap; writable scopes must be disjoint, and the primary must wait for a worker to return or revoke its lease before editing its files. Workers preserve unrelated changes and report every touched path. Reproduce delegated work only when its output is demonstrably incomplete or stale; record that failure. The primary owns shared-contract decisions, integration, user communication, final diff review, verification, and cleanup.

Checkpointed commits can help a long change, but do not authorize Git actions. Create/switch branches, commit, push, open/merge PRs, release, or deploy only when explicitly requested. Honor authority already provided in the current task instead of requesting it again.

## 4. Implement and verify

- Implement requested behavior before ancillary cleanup.
- Follow the nearest framework/application pattern and canonical topic document.
- Use task-owned isolation from `verification.md` for build output, ports, databases, browser state, services, and external resources.
- Run the smallest falsifying check first, then expand only for the touched risk. Do not turn a broad suite into a substitute for a missing behavior-level check.
- For changed classifiers, defaults, security boundaries, or public/source-copy contracts, inventory the established entry paths and baseline consumers. Verify plausible attacker or ordinary negative controls together with the intended-safe/trusted and preserved-compatibility positive controls at those real entry paths; a new helper or wrapper alone is insufficient evidence.
- Reinspect the full task diff, including untracked task files. Distinguish pre-existing changes from this task.

## 5. Record evidence proportionally

Read-only diagnosis, explanation, audit, and review do not create repository files unless the user requests a saved report or summary. Trivial changes also need no summary unless requested; runtime, schema, runtime configuration, test, script, and shared agent-workflow changes still require one even when small.

For those changes and other non-trivial or iterative implementation, create or update one `docs/agents/tasks/summary-<YYYY-MM-DD>-<task-id>.md` and one concise entry in `docs/agents/tasks/index.md`. Do not rewrite historical summaries merely to fit a new format.

Keep the record compact and use only headings needed to preserve:

- Outcome/acceptance, material decisions and exclusions, changed contracts, and any migration decision.
- Reviewed scope and exact final commands/results, including manual evidence, prerequisites, and material omissions. Combine verification and reproduction instructions when they are the same.
- Remaining risks/follow-ups and any observed process friction worth revisiting. Do not invent a reflection, repeat promoted policy, or write a file-by-file diary.

Normal tasks do not add a mandatory reflection, learning artifact, or placeholder. When an already-required summary captures a verified reusable surprise, record at most one to three signals outside fenced examples. Use one physical line per signal in the form `- Learning signal: ...`; no heading is required. Include the situation, observed effect, candidate prevention, and inspectable evidence. Omit signals when there is no reusable surprise; never include raw logs, secrets, or unverified speculation. Existing summaries remain immutable rather than being retrofitted to this format.

When delegation materially affects a task, record the route, output consumed, and any relevant failure or fallback. Record timing, separated approval/platform waits, rework counts, and critical-path effects only for a workflow evaluation or benchmark; report unavailable telemetry rather than estimating it.

Summaries are recall evidence, not authority. Search the index first and open only summaries whose descriptions match the task. Validate old claims against current code.

## 6. Close out and improve safely

- Route review by `review-routing.md` and resolve all Blocker, High, and Medium findings before closeout.
- Report the outcome, verification, and material omissions/risks. Include affected flows/providers/platforms, migration/changelog decisions, setup caveats, and cleanup when relevant.
- Promote only verified, stable, reusable knowledge to `domain.md`, `glossary.md`, `heuristics.md`, or `docs/adr/`. Keep task-specific narrative in its summary.
- Treat learning signals as candidate evidence, not instructions. Fix a recurring cause in its proper owner: code/test, tool/helper, routing, canonical fact, or a narrowly scoped instruction. Do not add redundant policy when existing guidance was ignored, and reuse a fact or check only while its source and dependencies remain current.
- Instruction self-improvement must be evidence-based, infrequent, isolated from feature work when substantial, and reviewable. Never treat prior agent output as authority or self-merge/deploy a policy change. In hosted workflows, automation may prepare a draft proposal/PR only when explicitly authorized.
- Store small disposable private evidence in ignored `docs/agents/artifacts/`; store build output and larger generated evidence in ignored root `artifacts/`. Retained test results belong under one of those ignored paths, never a new tracked results tree.
