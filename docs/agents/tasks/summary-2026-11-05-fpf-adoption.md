# Latest FPF adoption - 2026-11-05

## Outcome and scope

Prepared the FPF update on `master` at `29d9ec32b99a8b1e3aeb645723cc2f6e3073c1c9`, which matched freshly fetched `origin/master`. A separate worktree preserves unrelated work in the original checkout. The task worktree is retained for review; no commit or push was requested for this update.

The downloaded candidate is `70518df35a8b876c2700e60e847406df4903c4a7`; a subsequent `git ls-remote https://github.com/ailev/FPF.git refs/heads/main` confirmed the same revision. The comparison baseline and original checkout's prior accepted edition are `a5ac87ac15e78517d12bc66155b9da3ca254b643`. This new worktree has separate adoption state; refreshing its shared source cache does not accept an edition for another checkout.

Instruction pack `1.13.7` updates source attribution and refines the existing optional Core C.39 entry with C.39.QR and C.39.WA. It distinguishes what partial evidence establishes from completion of the original request, and routes whole-subject explanations to their organizing grounds and relations. Adequate ordinary work still needs no FPF lookup. Runtime, source helpers, permission boundaries, copied-app extensions and local-only read behavior are unchanged; no breaking-upgrade changelog entry is needed.

Adoption completed with `Sync-Fpf.ps1 -Action Accept -Revision 70518df35a8b876c2700e60e847406df4903c4a7 -ReviewNote <scoped-review>`. Subsequent Status in both supported shells and PowerShell 7 `Ensure -Offline` returned `ready`, selected/accepted the new revision, and reported no pending policy review, migration or cleanup. The existing index format remains version 2.

## Source review

- Inspected all 51 indexed source files by name (previously 49), the complete 48-file change inventory and the usage-guide diff. The 806 pattern change records were counted, not all semantically reviewed. New domain publications cover goal construction/regulation and human resource management; they remain available on demand without new default profile routes.
- Read the complete candidate `USING-FPF.md`, `Readme.md`, `LICENSING.md`, and `FPF-LIBRARY-REFERENCE.md` section `Library.Reference:11.1` (lines 1583-1635). Verified `LICENSE` and `LICENSING.md` are unchanged from the baseline. Source attribution and CC BY 4.0 notices remain.
- A bounded read-only `discovery_fast` packet read Core `C.39.QR` (lines 83018-83234) and `C.39.WA` (83235-83495), their conditions and method bodies. Its consumed result was to refine the existing entry rather than add a mandatory process. Both are Stable method patterns. QR sections 4.1-4.6 separate the original question, the relation and grounds of a partial result, and the remaining contribution. WA sections 4.1-4.6 apply when an account of a whole is missing, rather than to every local fix.
- The primary read candidate `SYSE.47` in full (lines 16455-16598), confirming the existing procedure route still calls for a concrete missing connection and useful bounded evaluation, while preserving adequate simpler work. Other existing profile routes were checked for unique IDs, titles and carriers, not comprehensive semantic equivalence; their bodies must still be read at use.

## Verification and review

Passed:

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` and `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: all 14 groups.
- `Normalize-TextFiles.ps1 -Check` for all five task files (guide, profile, manifest, index and this summary), plus `git diff --check`: strict UTF-8 without BOM and CRLF.
- Exact-ID verification of all 23 profile references in the intended carriers; the previous 21 remain resolvable at their existing paths. New QR/WA IDs are absent at the exact old Core selectors, without claiming no earlier equivalent existed.
- Full bounded candidate usage-guide reads in PowerShell 7 and Windows PowerShell 5.1, with no remaining truncation. Source refresh used `Sync-Fpf.ps1 -Action Ensure -Force`; no helper or migration implementation changed.

Manual guidance walkthroughs: an adequate local fix skips lookup; a passing component test supports its local claim without proving the requested end-to-end behavior; an incomplete framework explanation may need routing/model/provider/template relations rather than more isolated component descriptions. These are routing checks, not measured agent trials.

Review: fresh independent `reviewer_high` with the agent-workflow overlay found no blocking issues in the scoped policy diff. The active-summary/index audit passed with no supplemental findings. Review loop can stop.

Not required: cache lifecycle suites, application builds or database/browser tests because their implementations are unchanged. The full library's method bodies and downstream application behavior were not reviewed. No measured productivity, token or quality improvement is claimed.
