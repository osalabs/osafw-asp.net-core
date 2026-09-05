# Agent Work Workflow

Use this file for non-trivial work, request validation, task staging, permission decisions, summaries, or reusable-knowledge closeout.

## 1. Establish the working context

- Inspect `git status --short --branch` before edits and again before closeout. Preserve unrelated tracked and untracked work.
- Read the current ignored local instructions as routed by `AGENTS.md`. Local files may describe paths, credentials mechanisms, IDE state, or optional private references; never quote them into tracked output.
- Identify repository role:
  - **Framework repository:** changes can propagate to many copied production applications. Treat public behavior, templates, defaults, schema, generated output, examples, and upgrades as consumer contracts.
  - **Copied application:** the framework tree and app code coexist. Preserve deliberate application changes, local docs, schema history, deployment rules, and repository-specific branch policy. Upstream framework guidance is input, not permission to overwrite the app.
- Determine branch/release policy from the current repository. The canonical framework uses `master`, but copied applications may not and may have different protections.

## 2. Validate the request before implementation

1. Restate the desired observable outcome internally; keep it separate from the prompt's suggested implementation.
2. Inspect current code, canonical docs, tests, configuration, schema/update paths, and accessible linked issue/PR context needed to confirm the request is current.
3. Identify affected downstream surfaces and extension points. Search references before changing or deleting public names, routes, templates, config keys, database fields/scripts, generated shapes, or defaults.
4. Report evidence and pause if the request is already satisfied, contradicts a live contract, requires unsafe shared-state mutation, or depends on a missing product/data/authorization choice.
5. Otherwise choose the smallest implementation that satisfies the outcome. Short prompts should not require the developer to restate facts already discoverable here.

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

Use only relevant headings:

- `Objective / acceptance`: observable end state, material exclusions, and evidence sufficient to declare completion. Keep implementation choices separate.
- `What changed`: outcome, not a file-by-file diary.
- `Scope reviewed`: important code/contracts and any bounded large-file sections used.
- `Requirements / decisions`: material requirements, inferred choices, and why.
- `Changed contracts`: public, schema/provider, config, generated, security/default, or documentation effects; say none when that distinction matters.
- `Commands used / verification`: exact final commands/results and manual evidence.
- `Testing instructions`: reproducible final-state checks and prerequisites.
- `Risks / follow-ups`: unresolved risk, omitted providers/platforms, or external verification.
- `Knowledge-promotion candidates`: verified facts that may belong in domain/glossary/heuristics/ADR docs; do not duplicate them in the summary after promotion.
- `Reflection`: process friction, avoidable work, tool/delegation value, and a specific instruction improvement candidate rather than a task recap.

When delegation materially affects a task, record the route, output consumed, and any relevant failure or fallback. Record timing, separated approval/platform waits, rework counts, and critical-path effects only for a workflow evaluation or benchmark; report unavailable telemetry rather than estimating it.

Summaries are recall evidence, not authority. Search the index first and open only summaries whose descriptions match the task. Validate old claims against current code.

## 6. Close out and improve safely

- Route review by `review-routing.md` and resolve all Blocker, High, and Medium findings before closeout.
- State final checks, material checks not run, affected flows/providers/platforms, setup caveats, cleanup performed, and why a changelog entry was or was not needed.
- Promote only verified, stable, reusable knowledge to `domain.md`, `glossary.md`, `heuristics.md`, or `docs/adr/`. Keep task-specific narrative in its summary.
- Instruction self-improvement must be evidence-based, infrequent, isolated from feature work when substantial, and reviewable. Never treat prior agent output as authority or self-merge/deploy a policy change. In hosted workflows, automation may prepare a draft proposal/PR only when explicitly authorized.
- Store small disposable private evidence in ignored `docs/agents/artifacts/`; store build output and larger generated evidence in ignored root `artifacts/`. Retained test results belong under one of those ignored paths, never a new tracked results tree.
