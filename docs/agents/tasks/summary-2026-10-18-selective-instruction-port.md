# Selective Instruction Evaluation Port

Date: 2026-10-18

## Outcome and scope

Instruction pack 1.13.1 adds evaluator-owned execution-trial guidance, explicit source/reader checks for non-code reviews, actionable partial-import recovery notes, and a brief lesson-revalidation clarification. Existing owners hold the details; the documentation map routes to the expanded scenarios.

The public reference was [last-agents.md v7.0.0](https://github.com/osalabs/last-agents.md/blob/4d53a27d8587a506ee63c7ea0b5ddb2e5d092474/AGENTS.md) and its [evaluation guide](https://github.com/osalabs/last-agents.md/blob/4d53a27d8587a506ee63c7ea0b5ddb2e5d092474/EVALUATION.md). Concepts were adapted to the existing instruction structure. Automatic bootstrap, extra document owners, blanket review loops, and replacement FPF access were excluded. Baseline: `fbca49f37967e8550189e458a0c4ec7dcf259364` (pack 1.13.0).

This is documentation and pack-metadata maintenance. FPF tooling/cache contracts, review routing and severity gates, model/profile settings, application code, helper/test scripts, CI, notification ownership, copied-app customizations, and historical summaries remain unchanged. No runtime migration or breaking-upgrade changelog entry is needed. The user authorized commit and push after verification and independent review.

## Verification and evidence

- **Passed:** `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` and `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`; all 14 groups passed on PowerShell 7.6.5 and Windows PowerShell 5.1.26100.9711. Coverage includes local links/routes, pack metadata, encoding, privacy, and the existing review handoff.
- **Passed:** changed-file strict UTF-8 without BOM/CRLF checks through `Normalize-TextFiles.ps1 -Check`, complete diff inspection, and `git diff --check`. Final publication checks include this summary and its index entry; private scan terms are supplied transiently rather than stored in files.
- **Passed, routing walkthrough:** compared the baseline and candidate for read-only work, no-setup edits, inert instruction templates, nested component rules, partial imports, supported/unsupported documentation claims, and lesson reuse. Recovery notes preserve completed work and keep unresolved required imports partial; existing review gates and optional setup remain controlling.
- **Passed, candidate execution trial:** a fresh `implementation_fast` agent completed a template-title change and component documentation addition, a small typo correction, then a related addition under different nested heading rules. Component-local `Select-String -LiteralPath guide.md -Pattern '^## '` / `'^### '` checks and content/encoding assertions passed. The agent also used Python for disposable text assertions/edits; no dependency or helper was added to the repository.
- **Passed, candidate review trial:** a separate fresh `reviewer_high` inspected an operator guide against its supplied release contract using the candidate review criteria. It identified both planted Medium defects (automatic retries and released PDF output), with source evidence, while leaving the supported CSV/download-window guidance unflagged. The expected verdict was changes required for that deliberately defective fixture; it is separate from review of this port.
- **Passed, evaluator inspection:** SHA-256 comparisons over the ten-file disposable project showed exactly the three authorized documents changed and seven protected files unchanged. No extra files, embedded setup effects, or replacement task records appeared. The trial directory was ignored and task-owned.
- **Not required:** application builds, databases, FPF/cache fixtures, instruction-validator fixtures, and learning-helper fixtures; their implementation contracts are unchanged.

Trials ran on Windows in fresh profile-selected contexts without author history. Task/file boundaries and normal project/reviewer routes were supplied explicitly; expected findings and the evaluation procedure were withheld. Network availability was not exercised. The baseline received a routing comparison, not an execution trial. Agent-reported commands plus evaluator file inspection support these bounded results; timing/token telemetry, broad effectiveness, Unix behavior, and live interrupted-import execution were not measured. Recovery received a walkthrough only.

## Review and follow-up

Fresh `reviewer_high` reviewed the six policy/metadata files against the baseline with the agent-workflow overlay, before receiving this summary. Its initial verdict was no blocking findings; it also independently reran both instruction validators and text/diff checks. Supplemental summary/index accuracy and privacy audit completed with no findings.

Retain the refinements while ordinary work uses them without extra routine ceremony. Revisit if an actual partial import cannot resume from its recorded state, artifact review misses a source contradiction, or evaluation scaffolding leaks into normal work. Any broader effectiveness claim needs comparable baseline/candidate execution and an additional case not used to tune these instructions.
