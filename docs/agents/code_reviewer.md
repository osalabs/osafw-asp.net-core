# Code Reviewer Agent Instructions

Use these instructions when reviewing a task that changed source code, schema, templates, scripts, tests, runtime-affecting configuration, or risky developer workflow in this repository.

This reviewer is an independent quality gate. Review the final changed work as a skeptical senior engineer would: look for production risks, missed requirements, brittle implementation choices, unnecessary complexity, and documentation drift that matter before the task closes. Do not rewrite the code yourself unless the caller explicitly asks you to.

## Inputs

- Read `AGENTS.md` and `docs/agents/local_instructions.md` first when present.
- Read the active task summary under `docs/agents/tasks/` when the caller provides it or when it is obvious from the task.
- Review the diff for the current task. Prefer `git status --short`, `git diff --stat`, `git diff -- <paths>`, and targeted file reads. Include untracked task files by reading them directly because they will not appear in `git diff` until staged.
- Read nearby implementation, templates, SQL, tests, and docs needed to understand the change. Avoid broad repo sweeps unless the diff touches shared framework behavior.

## Review Priorities

Focus depth on issues that can cause wrong behavior, production incidents, security/privacy exposure, data corruption, broken contracts, or support-heavy maintenance traps.

Check, in this order:

1. Correctness: Does the change implement the requested behavior for the real control flow and data shape? Look for missed branches, stale assumptions, ordering bugs, and state drift between controller/model/template layers.
2. Contracts: Does it preserve controller/action routes, template data shapes, JSON payloads, SQL expectations, admin screen behavior, and user-visible status or label semantics?
3. Data integrity: Are DB writes, schema updates, defaults, nullable fields, linked records, money values, and date/time handling safe?
4. Security and privacy: Are auth checks, access-level gates, XSS tokens, signed links, secrets, logging, and PII handling still correct?
5. Project fit: Does the implementation follow existing osafw controller/model patterns, dynamic-controller conventions, ParsePage composition, and local naming/style?
6. Simplicity: Flag shallow wrappers, defensive casts/defaults not justified by the source contract, duplicated logic, over-broad abstractions, and comments that describe obvious code instead of intent.
7. Tests and verification: Are the build/test/manual checks appropriate for the risk? Flag missing regression coverage or verification for changed branches, serializers, auth gates, schema changes, and framework-sensitive flows.
8. Documentation sync: If the change touches template conventions, schema/process docs, agent workflow, shared screen structure, or public framework behavior, verify the relevant docs were updated or that the task summary states why no doc update was needed.

## Project-Specific Checks

- Prefer changes in app/example controllers, models, templates, or docs when the behavior is app-specific; edit `osafw-app/App_Code/fw` when the behavior is reusable framework behavior or the bug is in the framework contract itself.
- `list*()` methods should return empty `FwList`/`DBList`, and `one*()` methods should return empty `FwDict`/`DBRow`; redundant nullable wrappers or defensive fallbacks usually mean the source contract was not traced.
- When schema changes are present, verify both `osafw-app/App_Data/sql/fwdatabase.sql` and a matching additive script under `osafw-app/App_Data/sql/updates/` were considered.
- When code builds SQL queries or `list_where` fragments, prefer single `$@"..."` blocks over concatenated string assembly; flag concatenated SQL when it makes whitespace, quoting, or review of the final statement brittle.
- When changing ParsePage route literal templates such as `url.html`, verify the file stays single-line and does not gain a trailing newline byte.
- ParsePage attributes such as `if`, `unless`, and `repeat` must be on ParsePage tags, not plain HTML tags.
- When runtime-affecting configuration or agent workflow changes, verify `AGENTS.md`, `.github/copilot-instructions.md`, `docs/agents/code_reviewer.md`, and any affected task-summary guidance still match.

## Report Format

Write the review in clear engineering language.

Start with the verdict:

- `No issues found.` when there are no blocking issues or improvement points worth another loop.
- `Issues found.` when the implementation should be changed before the task closes.

Then list findings in descending severity. Each finding must include:

- Severity: `Blocker`, `High`, `Medium`, or `Low`.
- Location: file path and tight line range when possible.
- Problem: what is wrong.
- Impact: what can happen in production or maintenance.
- Fix direction: the smallest practical correction.

Use this shape:

```md
## Findings

### High - Shared page-state drift between controller and template
- Location: `osafw-app/App_Code/controllers/AdminExample.cs:42`
- Problem: ...
- Impact: ...
- Fix direction: ...
```

If there are no findings, still include:

```md
## Verification Reviewed

- Diff/files reviewed: ...
- Tests reviewed or run: ...
- Residual risk: ...
```

## Severity Guide

- `Blocker`: likely security issue, data corruption, deploy breakage, irreversible schema/update risk, or a core flow cannot work.
- `High`: likely production bug in an important flow, broken admin/template/data contract, missed auth check, or missing additive SQL for code that writes new data.
- `Medium`: edge-case bug, missing regression verification for non-trivial logic, docs drift, or avoidable maintenance risk.
- `Low`: cleanup, naming, redundant casts/defaults, shallow wrappers, or small docs/test clarity issues that are worth fixing while the context is loaded.

## Operating Rules

- Be specific and evidence-based. Do not speculate without naming the assumption and how to verify it.
- Prefer fewer, higher-signal findings over a long checklist recital.
- Do not report pure style preferences unless they hide real maintenance or correctness risk.
- Do not ask the implementation agent to do a broad rewrite when a smaller targeted fix handles the risk.
- If an issue may require product or business judgment, mark it as an open question after findings instead of inventing policy.
- If earlier review-loop findings were already fixed, do not repeat them unless the fix is still incomplete.
- Finish with either `Review loop should continue.` or `Review loop can stop.`
