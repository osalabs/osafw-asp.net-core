# Self-improvement: complete text-check scope - 2026-11-10

## Outcome and evidence

Published the previously reviewed FPF update as `d2f2f577e8dc8d1bb553747dfa7d17133ac9f5f6` on `origin/master`, then performed a separate manual maintenance cycle.

Instruction pack `1.13.11` adds a short, executable verification recipe for task-owned new text files. It explains which Git check covers unstaged, staged and untracked files; keeps the index unchanged; and distinguishes a clean added-file exit from an access error. Existing helpers and runtime code are unchanged.

The observed gap occurred during publication: the new FPF summary passed the encoding/CRLF check, while ordinary `git diff --check` did not examine it. After staging, `git diff --cached --check` caught an extra blank line at EOF. It was corrected before the published commit. A value-free local reproduction confirms the distinction; this is a check-scope gap rather than a normalizer defect.

Used the existing [manual reflection process](../../prompts/agent_reflection.md) with the committed range `90e87dc7cafee8cdb51a06197c117e280c6e728b..d2f2f577e8dc8d1bb553747dfa7d17133ac9f5f6`. The collector reported one added summary, six changed summaries, zero explicit learning signals, no incomplete input and no next page. Five changes were attribution-only edits to older summaries; the counts do not represent six newly completed tasks. The published FPF summary and the current publication/check results supply the relevant direct evidence.

```powershell
pwsh -NoProfile -File docs/agents/tools/Get-LearningSignals.ps1 -Since 90e87dc7cafee8cdb51a06197c117e280c6e728b -Revision d2f2f577e8dc8d1bb553747dfa7d17133ac9f5f6 -MaxTasks 20 -MaxChars 6000
```

## Selected repair and limits

[verification.md](../verification.md#text-checks-and-diff-scope) owns the new recipe. Native Git already supplies the needed check, so no wrapper, validator interface, normalizer behavior change or automatic scan is needed. The new guidance complements the existing requirement to inspect the full task diff, including task-owned untracked work.

The source-reading handoffs in the preceding FPF task were also examined. `fpf.md` already requires continuation through truncated output, and both discovery profiles stop when their packet acceptance criteria are answered. The incomplete handoffs do not establish that another instruction, a model-default change or a tool rewrite would prevent recurrence. Those changes are deferred; revisit if a representative matched task identifies a reproducible failure in the supplied instructions, tools or role selection. Existing bounded packet and fallback rules remain applicable.

FPF Library `C.39.WI` and `C.42`, in `FPF-Spec.md` at `b223469ecafb9735c1d9bac220671e3336618339`, supported choosing the narrow check-scope repair and preserving adequate existing rules. Source conditions were read in the preceding review and reused while still current; see [FPF guidance and attribution](../fpf.md#attribution-and-verification). No library refresh or policy acceptance is required because the FPF guide, profile and helpers did not change.

Expected maintenance cost is one short section and a native read-only command when a task delivers a new text file. No per-task reflection, scheduler, automatic publication or model setting was added. The check does not replace link, content, privacy, build or behavior checks. Unix behavior was not exercised.

## Verification

A task-owned probe in ignored `docs/agents/artifacts/self-improvement-20261110-whitespace/` exercised the final command through PowerShell 7 and Windows PowerShell 5.1. Each shell ran all six cases under inherited settings, disabled automatic CRLF conversion with whitespace checks disabled, and automatic CRLF conversion with strict CR handling. The final per-invocation whitespace rules override those defaults. `probe.ps1` and `results.json` retain the value-free checks for review.

| Input | Exit code in both shells | Diagnostics | Result |
| --- | ---: | --- | --- |
| Clean CRLF Markdown | 1 | None | Accepted |
| Empty file | 1 | None | Accepted |
| One-line URL literal without a trailing newline | 1 | None | Accepted |
| Extra blank line at EOF | 3 | EOF warning | Rejected |
| Trailing whitespace | 3 | Whitespace warning | Rejected |
| Missing file | 1 | Access error | Rejected |

Both shells classified all six cases correctly under all three setting variants (18 checks per shell), using the exit code and diagnostics together. Git index bytes were identical before and after the probes. The normalizer still reports the extra-EOF file as encoding/line-ending compliant, accurately demonstrating its narrower scope. An ordinary diff check returned success without inspecting these task-owned new fixtures.

The instruction validator passed all 14 groups through both supported Windows shells. The four task files passed strict UTF-8/no-BOM/CRLF checks. Scoped tracked diff checks passed; the actual untracked summary passed the no-index check with exit 1 and no diagnostics. The Git index remained unchanged and empty of staged edits. Its three local links and two anchors resolve, its index entry occurs once, and the summary contains no private machine paths, private repository names, excluded external URLs or repeated author attribution.

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1
pwsh -NoProfile -File docs/agents/tools/Normalize-TextFiles.ps1 -Check -Path 'docs/agents/verification.md,docs/agents/instruction-pack.json,docs/agents/tasks/index.md,docs/agents/tasks/summary-2026-11-10-agent-verification-scope.md'
$textWhitespace = 'blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol'
git -c "core.whitespace=$textWhitespace" diff --check -- docs/agents/verification.md docs/agents/instruction-pack.json docs/agents/tasks/index.md
git -c "core.whitespace=$textWhitespace" diff --cached --check -- docs/agents/verification.md docs/agents/instruction-pack.json docs/agents/tasks/index.md
git -c "core.whitespace=$textWhitespace" diff --no-index --check -- NUL docs/agents/tasks/summary-2026-11-10-agent-verification-scope.md
```

Application builds, provider/browser tests and unchanged helper fixture suites are not required for this documentation-only change. The fresh independent `reviewer_high` review, selected for this ordinary shared-workflow change with the agent-workflow overlay, found one Medium configuration-sensitivity defect in the initial recipe. With automatic CRLF conversion disabled, it rejected a clean CRLF file; disabled whitespace checks could also hide invalid files. The recipe now supplies explicit whitespace rules for tracked and new files without changing Git settings. The reviewer found no additional blocking summary issue. The independent reviewer reran the 18 cases in each shell, checked the actual task files under all three settings, and re-audited the updated summary. Final adjudication: No blocking findings. Review loop can stop.

Manual walkthroughs of learning scenarios 1, 3, 6, 7 and 8 preserved ordinary work without mandatory reflection, retained adequate existing rules, routed the shared edit to independent review, recorded the complete pinned collection without treating summary counts as completed-task counts, and added no automatic trigger or publication. The fixture checks above cover clean, invalid and missing inputs; scoped checks preserved unrelated work. This is a manual maintenance cycle, not evidence of productivity improvement.

Retire or replace the recipe if an existing check gains equivalent coverage for explicitly selected new files, or if a changed Git/platform behavior defeats its exit/diagnostic contract. Preserve the positive cases, error detection and index isolation when doing so.

- Learning signal: A task-owned untracked text file passed encoding checks and escaped the ordinary Git diff check until staging; check new deliverable files explicitly with the read-only no-index recipe, including diagnostics; evidence: the six-case, three-setting, two-shell probe and results under the ignored artifact path above.
