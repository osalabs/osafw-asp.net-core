# FPF refresh and selective adaptation - 2026-10-23

## Outcome

- `git pull --ff-only origin master` reported already up to date at `660d371fbea9fb694437692edfdf2313e09db435`.
- Refreshed and accepted FPF upstream `main` at `a5ac87ac15e78517d12bc66155b9da3ca254b643`; `git ls-remote https://github.com/ailev/FPF.git refs/heads/main` confirmed the same revision after review. The ignored cache now uses version-2 indexes and verified plain snapshots through the existing migration implementation.
- Instruction pack `1.13.2` clarifies citation versus method application, resolves complete references through the edition's document catalogue with an older-edition fallback, and adds one optional Core `C.40.JD` route for coupled procedure, capability and support questions. Adequate ordinary work still needs no lookup or development exercise.
- No application/runtime, helper implementation, root instruction, database or dependency changes. This routine documentation update needs no breaking-upgrade changelog entry. Changes remain uncommitted; unrelated working-tree files were preserved.

## Source and adoption scope

The actual prior local adoption was `e2c6ac1e97dae9b9f54f707477f7d86a12d91852`; the pre-edit tracked guidance attribution separately named `d9c3dd65da3cd9f7df898698e49c5464dd8a6c4b`. The source comparison used the former, not an assumed equality between attribution and adoption.

Reviewed the complete candidate `USING-FPF.md`, `Readme.md` and `LICENSING.md`, the usage/licensing diff from the prior accepted edition, and `FPF-LIBRARY-REFERENCE.md` section `Library.Reference:11.1` (lines 527-577). `LICENSE` was unchanged. CC BY 4.0 attribution remains; independently authored application code is not relicensed by using the methods.

The indexed publication set grew from 44 to 49. The complete 87-file change inventory was inspected; 1,206 pattern change records include moved-carrier additions/removals and do not mean 1,206 method bodies were reviewed. All 21 distinct profile references resolve uniquely to their intended publication and heading. Existing routes were checked for navigation, not comprehensively re-reviewed for semantics; supplying methods still require reading at use.

A bounded read-only `discovery_fast` packet assessed Core `C.43`, `C.40.OD`, `C.40.JD` and `C.40.NP`. Its consumed result was to add only `C.40.JD`, whose conditions and solution distinguish a delivered work result, demonstrated capability under retained support, and findings about the method/provision. The source is `FPF-Spec.md` at the accepted revision, especially `C.40.JD:4.3` and `C.40.JD:4.4`. Other new routes had no established need here. The primary owned integration; no broader development or training process was added.

## Verification and review

Passed:

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` and `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: all 14 validation groups.
- `git diff --check`; `Normalize-TextFiles.ps1 -Check` for the changed guide, profile, manifest, task index and this summary: strict UTF-8 without BOM and CRLF.
- Indexed exact-ID checks for all 21 profile references; a Windows PowerShell 5.1 bounded read of the full document catalogue completed without truncation. The prior accepted edition has no matching catalogue heading, confirming the need for the fallback.
- `Sync-Fpf.ps1 -Action Ensure -Force` migrated/refreshed; `-Action Accept -Revision a5ac87ac15e78517d12bc66155b9da3ca254b643 -ReviewNote <scoped-review>` recorded adoption after semantic and independent policy review. Subsequent Status in both supported shells and `Ensure -Offline` in PowerShell 7 reported `ready`, selected/accepted the new revision, index version 2, and no pending policy review, migration or cleanup.

Manual guidance walkthroughs preserved ordinary direct work without FPF; allowed sufficient cited material without an automatic extra lookup; required an explicit method request to return a useful contribution under its conditions; retained complete reference suffixes/list members/range endpoints and exact editions; and kept revised instruction text distinct from demonstrated capability. These are guidance checks, not measured agent trials.

Initial sandbox Git inventory/checker attempts failed on filesystem access; permitted runs outside the sandbox passed without repository configuration changes.

Independent `reviewer_high` review with the agent-workflow overlay found no blocking issues in the scoped policy diff. It independently read the complete `C.40.JD` pattern and verified the usage, catalogue, licensing and older-edition navigation boundaries. Active-summary audit passed with no supplemental findings. Review loop can stop.

Not run: cache lifecycle suites, builds, runtime/database checks or productivity experiments. Helper/runtime implementations are unchanged; the real cache migration/adoption and affected reads were checked. The full upstream corpus was not semantically reviewed, and no speed, token or quality improvement is claimed as measured.
