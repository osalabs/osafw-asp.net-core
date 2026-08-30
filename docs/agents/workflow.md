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

- **Direct path (default):** keep work in the primary task whenever one owner can coherently discover, implement, verify, and integrate the outcome. This includes non-trivial and cross-cutting changes whose contracts or writable files are tightly coupled. High security, data, provider, compatibility, or workflow risk requires stronger verification and review, not automatic implementation delegation. Do not add orchestration ceremony merely because source code, tests, or several related files change.
- **Orchestrated path:** load and follow `docs/prompts/orchestrator.md` only when staged control is needed and the delegation-payoff test passes. Before spawning, identify a bounded output that the primary will consume rather than reproduce, exclusive or read-only ownership, explicit acceptance/stop conditions, and a credible latency or specialist-quality gain. Normally this means at least two independently useful workstreams that can overlap or a material ambiguity/repeated-failure recovery stage that changes the next decision. If ownership overlaps, the work is tightly coupled, or the primary must repeat the discovery/integration, stay direct. Fresh independent review is a separate quality gate and may remain sequential.
- **Escalated path:** use `architect_max` only for a material architecture choice, a high-risk adjudication, unresolved ambiguity that changes the result, or recovery after bounded attempts failed, and only when that profile/capability is available. It is not the default for ordinary non-trivial work; perform the same bounded decision analysis locally when unavailable.

Checkpointed commits can make a long change easier to review or roll back, but a useful commit shape does not authorize committing. Create/switch branches, commit, push, open/merge PRs, release, or deploy only when explicitly requested.

Optional delegation is capability-conditional and must pass the payoff test for each child: use `discovery_fast` only when its bounded evidence replaces primary discovery, `implementation_fast` only for routine well-specified implementation in a small disjoint file set, `reviewer_high` only for the fresh quality gate triggered by `review-routing.md`, and `architect_max` only for the escalated path above. Do not spawn because a profile exists, route a sequential child whose output will be recomputed, or delegate tightly coupled contract decisions. Keep final integration in the primary task. If a named profile or delegation is unavailable, execute the required stage locally with the same packet, evidence, and stop conditions. Custom-agent model and reasoning selections live only in `.codex/agents/*.toml`; do not pin or replace the primary task's model in repository guidance.

Every delegation packet must be bounded and contain:

- objective and observable acceptance criteria;
- relevant evidence/context and explicit exclusions;
- exclusive writable file ownership plus any read-only paths;
- required verification and expected output format;
- stop/escalation conditions, including ambiguity, contract expansion, unsafe state, or repeated failure.

Maintain a file lease list while workers run. Read-only work may overlap; writers may run concurrently only when their writable paths are disjoint. Do not assign two workers overlapping files, and do not edit a leased file in the primary task until its worker returns or the lease is revoked. Workers preserve unrelated changes and report every touched path. The primary agent retains integration, final diff review, verification, and cleanup. If custom agents or delegation are unavailable, execute the same packeted stages sequentially in the primary task and disclose the local fallback.

## 4. Implement and verify

- Implement requested behavior before ancillary cleanup.
- Follow the nearest framework/application pattern and canonical topic document.
- Use task-owned isolation from `verification.md` for build output, ports, databases, browser state, services, and external resources.
- Run the smallest falsifying check first, then expand only for the touched risk. Do not turn a broad suite into a substitute for a missing behavior-level check.
- Reinspect the full task diff, including untracked task files. Distinguish pre-existing changes from this task.

## 5. Record evidence proportionally

Create or update one `docs/agents/tasks/summary-<YYYY-MM-DD>-<task-id>.md` when the prompt requires it or the work is non-trivial, iterative, runtime/schema/config/test/script-affecting, or changes shared agent workflow. Add/update one concise line in `docs/agents/tasks/index.md`. Do not rewrite historical summaries merely to fit a new format.

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

Summaries are recall evidence, not authority. Search the index first and open only summaries whose descriptions match the task. Validate old claims against current code.

## 6. Close out and improve safely

- Route review by `review-routing.md` and resolve all Blocker, High, and Medium findings before closeout.
- State final checks, material checks not run, affected flows/providers/platforms, setup caveats, cleanup performed, and why a changelog entry was or was not needed.
- Promote only verified, stable, reusable knowledge to `domain.md`, `glossary.md`, `heuristics.md`, or `docs/adr/`. Keep task-specific narrative in its summary.
- Instruction self-improvement must be evidence-based, infrequent, isolated from feature work when substantial, and reviewable. Never treat prior agent output as authority or self-merge/deploy a policy change. In hosted workflows, automation may prepare a draft proposal/PR only when explicitly authorized.
- Store small disposable private evidence in ignored `docs/agents/artifacts/`; store build output and larger generated evidence in ignored root `artifacts/`. Retained test results belong under one of those ignored paths, never a new tracked results tree.
