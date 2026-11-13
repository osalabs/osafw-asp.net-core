# FPF adoption and implementation simplicity - 2026-11-13

## Outcome

Adopted FPF Library `1e57ccda08ebbb9d49b7b2190bc2740b72075a13` in this checkout after source compatibility review. Writable `Sync-Fpf.ps1 -Action Ensure` migrated its legacy cache to version 2; a failed network attempt preserved the previous adoption, and an explicit `Ensure -Force` retry fetched the candidate. `Accept -Revision <candidate> -ReviewNote <scoped-review>` recorded adoption separately. Status in both supported Windows shells and offline inspection now report `ready`, with no pending policy review, migration or cleanup. Source and adoption data remain ignored.

Instruction pack `1.13.12` makes state scope, lifetime, existing owners and actual consumers explicit before introducing another mechanism. The existing review now checks the cumulative task change for obsolete machinery after a design change, preserves useful single-caller boundaries and required guarantees, and asks for concrete simplification findings. Optional improvement advice includes developer attention, adaptation and ongoing work in its cost comparison. Five routing scenarios exercise these distinctions. This adds no mandatory artifact, reviewer or evaluation stage. Runtime, helpers, model settings, permissions and existing review gates are unchanged.

## Source review

The previous tracked provenance was `b223469ecafb9735c1d9bac220671e3336618339`; this checkout's actual accepted edition was older: `0c6ade275e9360f0c5ab9715d8f05c0dbaa13cf8`. These are different comparison baselines.

- The [upstream change from the tracked edition](https://github.com/ailev/FPF/compare/b223469ecafb9735c1d9bac220671e3336618339...1e57ccda08ebbb9d49b7b2190bc2740b72075a13) is one commit affecting 12 files. Core and Library Reference text changed; companion references and the README clarify how a system's contribution follows from the larger whole and its interacting parts. The catalogue remains 51 indexed source files, 403 Core patterns and 36 domain frameworks. Usage and licensing files are unchanged against that tracked edition.
- The complete local change report against the actual accepted edition was consumed across eight pages: 89 file records and 1,430 pattern records. Many additions/removals reflect relocation into `DPFs/`. These are structural records, not a claim that every changed method was semantically reviewed.
- Compared complete `USING-FPF.md` and `LICENSING.md` at the actual accepted and candidate pins. The newer usage covers the Library layout, complete portable references, source citation versus method application, and returning results to their intended use. Existing guidance supports those distinctions. `LICENSE` has identical bytes; the notice extends its description to the current Library publications without changing CC BY 4.0.
- Read the candidate README, Core `C.11.DUA` problem/conditions and complete solution, and complete `SYSE.46`. Compared relevant accepted/candidate `C.11.DUA` passages and both complete `SYSE.46` bodies. Optional advice already included recipient costs in the old edition; this task makes that useful distinction more explicit locally. Updated evaluation guidance distinguishes an absolute property check from a comparative improvement claim, and lasting improvement from response to changed conditions. Existing local evaluation guidance already accommodates it; no extra evaluation procedure was added. All 29 profile IDs resolve at the new pin; this is navigation evidence, not full-body review of those 29 methods.

Source carriers are `FPF-Spec.md` for `C.11.DUA:4.2.1` and `DPFs/Engineering DPF Suite/SYSTEMS-ENGINEERING-PRINCIPLES-FRAMEWORK.md` for `SYSE.46`. The older Systems Engineering carrier lacked the `DPFs/` prefix. Adapted from [FPF Library at the reviewed edition](https://github.com/ailev/FPF/tree/1e57ccda08ebbb9d49b7b2190bc2740b72075a13), under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/); see [author attribution](../fpf.md#attribution-and-verification). Changes: selected software-workflow implications and repository-specific wording. External research cited by FPF was not independently verified.

## Verification and review

Passed:

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` and `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: all 14 groups.
- `Normalize-TextFiles.ps1 -Check` on all eight task files, including the new summary: strict UTF-8 without BOM and CRLF. `git diff --check` and `git diff --no-index --check -- NUL <new-summary>` used `git -c core.whitespace=blank-at-eol,blank-at-eof,space-before-tab,cr-at-eol`; the new-file comparison returned 1 with no diagnostics. Local links, the single index entry and transient private-identifier scans also passed in both shells.
- `Read-Fpf.ps1 -Action Read -Revision <candidate> -Path USING-FPF.md -MaxLines 60 -MaxChars 8000`, following each returned line/column continuation: three pages and identical complete content in PowerShell 7.6.9 and Windows PowerShell 5.1.
- `Sync-Fpf.ps1 -Action Status` in both shells and PowerShell 7 `-Action Ensure -Offline`: selected and accepted pins match the candidate; `PolicyChanged`, `MigrationRequired` and `CleanupPending` are false. Source/adoption file contents and last-write timestamps did not change across these inspections.

Manual baseline/candidate routing walkthroughs used the same six cases and protected invariants:

| Case | Result |
| --- | --- |
| Routine local correction | Remains direct with proportional checks; no FPF lookup or extra review stage is forced. |
| State spanning layers | Adds an explicit ownership/lifetime/normal-and-recovery trace before choosing a mechanism. |
| Ownership changes during a task | Makes the original baseline and cumulative affected flow explicit while keeping repair review focused. |
| Cohesive helper with one caller | Preserves the justified boundary; no automatic flattening or test-only interface. |
| Shorter implementation loses a guarantee | Retains authorization, recovery, provider and copied-app contracts or requires an authorized contract change. |
| Optional advice with unclear benefit | Compares continuing the work and recipient costs; required checks remain binding. |

These are routing walkthroughs, not agent execution trials or measured productivity gains. A fresh independent `reviewer_high`, using the agent-workflow overlay and pre-edit baseline `f11fa7df8adfd047590ae2fbec61e10d45a328c8`, found no blocking issues in the six policy files. The supplemental summary/privacy audit found no blocking issues; its optional clarification of the new-file whitespace command is recorded above. Final adjudication: no blocking findings; review loop can stop.

Application builds, database/browser checks and helper/cache lifecycle fixtures are Not required for this documentation-only change and use of unchanged helpers. General agent effectiveness and downstream application execution were not tested. Git publication was authorized after review.
