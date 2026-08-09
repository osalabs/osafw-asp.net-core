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

- **Small/routine:** one scoped implementation and focused verification pass.
- **Non-trivial:** maintain a short plan with one active step, keep tightly coupled integration local, and use checkpoints for independently falsifiable stages.
- **High-risk/cross-cutting:** define compatibility/security/data gates before editing and consider review checkpoints before the final integrated review.

Checkpointed commits can make a long change easier to review or roll back, but a useful commit shape does not authorize committing. Create/switch branches, commit, push, open/merge PRs, release, or deploy only when explicitly requested.

Optional delegation is capability-conditional. Use it only for bounded, independent work with clear expected output and file ownership. In a shared worktree, tell workers to preserve others' changes and report touched paths. The primary agent retains integration, final diff review, verification, and cleanup. If delegation is unavailable, run the same routed checks locally.

## 4. Implement and verify

- Implement requested behavior before ancillary cleanup.
- Follow the nearest framework/application pattern and canonical topic document.
- Use task-owned isolation from `verification.md` for build output, ports, databases, browser state, services, and external resources.
- Run the smallest falsifying check first, then expand only for the touched risk. Do not turn a broad suite into a substitute for a missing behavior-level check.
- Reinspect the full task diff, including untracked task files. Distinguish pre-existing changes from this task.

## 5. Record evidence proportionally

Create or update one `docs/agents/tasks/summary-<YYYY-MM-DD>-<task-id>.md` when the prompt requires it or the work is non-trivial, iterative, runtime/schema/config/test/script-affecting, or changes shared agent workflow. Add/update one concise line in `docs/agents/tasks/index.md`. Do not rewrite historical summaries merely to fit a new format.

Use only relevant headings:

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
