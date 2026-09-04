## Objective / acceptance

- Simplify the shared agent instructions, resolve conflicting requirements, and keep them usable across developer-selected model generations.
- Preserve repository security, copied-application compatibility, independent review for meaningful risk, and existing role model/reasoning/sandbox settings.
- Deliver on a separate branch with a PR to `master`, as requested. Runtime application changes and model performance calibration are outside this task.

## What changed / rule disposition

| Rule | Disposition and surviving owner |
| --- | --- |
| Delegation eligibility, roles, stage limits, ownership, fallback | Consolidated in `docs/agents/workflow.md`; root guidance routes there and `docs/prompts/orchestrator.md` supplies the plan/packet template. |
| Reviewer selection and independent/local eligibility | Conflicting availability wording removed; `docs/agents/review-routing.md` owns the decision. |
| Isolated initial review and subsequent summary audit | Preserved in the review router; reviewer profiles and `code_reviewer.md` reference that procedure. Review criteria, severity, and the final verdict remain in `code_reviewer.md`. |
| Task summaries | Clarified in the workflow: read-only work creates no repository files without a requested saved report; trivial edits are exempt, while the named implementation surfaces still require summaries even when small. |
| Delegation evidence | Ordinary task records retain route/output/failure/fallback evidence; detailed timing and rework accounting are reserved for workflow evaluation or benchmarks. |
| Instruction validation | Role names are checked in their canonical owners; consumers must reference those owners. The shared-policy model-name check covers numeric GPT generations instead of one generation. |
| Existing role settings and project safeguards | Intentionally retained. Role profiles are optional capabilities, not an automatic upgrade over the primary task's selected model. |

Instruction pack version is `1.3.1`. Copied applications continue to use the existing three-way adaptation policy and preserve app-owned configuration. No application API, schema/provider, template, generated output, deployment, or security behavior changed; no product changelog entry is needed.

## Commands used / verification

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` - passed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1` - passed.
- `pwsh -NoProfile -File artifacts/assistant_agent_instruction_cleanup/Verify-InstructionCleanup.ps1` - 18 fixture checks passed, nine under each shell. Cases cover canonical references with existing profiles, an alternate profile model, rejected shared-policy pins for three generations, a missing canonical role, broken orchestration/reviewer references, and removal of the canonical summary-audit procedure.
- Changed text was normalized to strict UTF-8 without BOM and CRLF; `git diff --check` passed.
- Diff inspection confirmed model, reasoning, and sandbox settings are unchanged in all custom-agent profiles.

The fixture harness invokes the actual validator with `-RepoRoot` against a disposable Git repository containing the relevant current tracked files. It restores each mutated file between cases and removes only its verified task-owned fixture directory. The small harness remains in the ignored artifact directory; it is not part of the copied instruction pack.

## Representative routing walkthroughs

- Read-only audit: inspect and report; create no summary or other repository file unless a saved result is requested.
- Localized runtime fix: direct implementation, focused falsifying verification, required summary, and local review permitted by the router's low-risk exception.
- Tightly coupled public/security change: keep implementation direct, exercise established entry paths and preserved positive/negative controls, and obtain fresh independent review with the triggered overlay.
- Documentation typo: direct edit and relevant text/link checks; no summary unless requested. Shared workflow changes still require a summary and independent review when available.
- Unavailable delegation: perform the required stage locally with the same evidence and stop conditions and disclose the limitation.
- Copied-application instruction upgrade: adapt managed paths while preserving app divergence and app-owned configuration; update pack metadata only after target validation.

## Review / risks

- Implementation remained in the primary task. A fresh `reviewer_high` with the agent-workflow overlay reviewed all 11 non-summary changed files against the baseline, independently reran the validator under both shells, and returned `No blocking findings.` The same reviewer's supplemental summary/index audit returned no candidates.
- Application builds, database tests, and model performance benchmarks were not run because this change affects instruction text and its validator. Fixture acceptance demonstrates validator behavior, not model quality, speed, or tool availability in another harness.
- Rollback: revert the cleanup commit or restore the previous instruction pack through the same three-way adaptation process in copied applications.
