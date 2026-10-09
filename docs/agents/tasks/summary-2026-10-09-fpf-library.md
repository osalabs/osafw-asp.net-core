# FPF Library adoption for everyday development

## Outcome and scope

Upgrade the .NET instruction pack from `1.10.0` to `1.11.0` on local `master`. The canonical guide already allowed ordinary work; a stale ignored override and absent local cache prevented consistent use. Restore the cache, repair that override, and make selective Library use practical for decisions that arise during development. Daily update checks occur when lookup is useful, with read-only/offline alternatives and separate candidate/accepted revisions.

No runtime APIs, application behavior, schema, source helpers, model settings, external integrations, commits, pushes, or worktrees change. Preserve unrelated untracked summaries and legacy draft files. Other application repositories are outside the implementation scope. No runtime upgrade changelog entry is needed.

## Source and adopted contributions

Source: [Anatoly Levenchuk's FPF Library, edition 0c6ade2](https://github.com/ailev/FPF/tree/0c6ade275e9360f0c5ab9715d8f05c0dbaa13cf8), full revision `0c6ade275e9360f0c5ab9715d8f05c0dbaa13cf8`. The restored source matches the edition inspected during planning; no later edition was fetched. Review covered `Readme.md`, `USING-FPF.md`, licensing notices, both suite references, and the complete 44-file publication inventory. Detailed reading was selective, not a review of every pattern body. Publications are under CC BY 4.0; the guide/profile credit the author, source, license, and adaptation.

- `FPF-Spec.md`: `C.39`, especially section 4.1, keeps intermediate contributions connected to the user's outcome; `C.11.DUA` tests the burden of additional work. `F.19` supports bounded wording repair while preserving meaning and attribution; selected `F.18` distinctions help ambiguous durable names without importing FPF naming forms.
- `Engineering DPF Suite/METHOD-ENGINEERING-PRINCIPLES-FRAMEWORK.md`: selected `ME.12`/`ME.14` guidance distinguishes sound instructions, useful workflow alternatives, and evidence of actual effectiveness.
- `Engineering DPF Suite/KNOWLEDGE-CORPUS-ACCESS-ENGINEERING-PRINCIPLES-FRAMEWORK.md`: overview and relevant `KCAE.SEARCH`, `KCAE.ASSESS`, `KCAE.CHANGE`, and `KCAE.DELIVER` sections support finding, evaluating, updating, and delivering sufficient source context. Outer tool truncation can still omit conditions after a reader reports an untruncated page.
- Existing Systems Engineering `SYSE.38`, Checklist `CHK.6`, Computational Thinking `CMP.3`, and Core `B.1.5.RS` routes remain. Their current problem frames and locators were checked for continued fit; all 25 distinct named pattern roots in the revised profile resolve uniquely at the selected edition. Other established routes remain available.

The main map stays in `fpf-profile.md`; source access/adoption stays in `fpf.md`. Root instructions, workflow, verification, naming, the documentation router, the workflow-review overlay, and the upgrade prompt carry narrow links or their own rules. The ignored local override now follows the canonical guide instead of restricting use to architecture or refreshing a monolith.

Rejected additions: compulsory F.19 processing for every request, whole-Library context loading, a permanent pattern checklist, new naming artifacts or bulk renames, mandatory process stages, and automatic acceptance of downloaded sources. Clearer wording supplies neither truth nor permission. A compatible accepted edition can serve ordinary work while an unrelated candidate waits for review; policy changes still require review when they affect that use.

A bounded read-only reference audit informed the planning comparison; implementation stayed with one owner. Shared workflow changes require a fresh independent `reviewer_high` with the agent-workflow overlay, selected for ordinary instruction/compatibility judgment risk.

## Routing walkthroughs

These are manual comparisons against the pre-change canonical routes and observed stale local override, not measured task runs or productivity benchmarks.

| Scenario | Baseline | Revised route and acceptance observation |
| --- | --- | --- |
| Mechanical documentation typo | Canonical guide already allowed direct work. | Direct edit and proportional text check; no Library fetch, method ritual, or new artifact. |
| Ordinary model/template bug with an unknown cause | `SYSE.38` existed, but the local architecture-only rule discouraged access. | Diagnose the actual failing boundary; use a bounded discriminating observation when needed, or apply a known sufficient fix directly. Verify the user's result and earlier side effects before retrying. |
| Unclear request or technical passage | General request validation/explanation routes lacked a direct F.19 entry. | Repair the unclear span, preserving conditions, quantities, attribution, and original quotations. Resolve factual or material authorization uncertainty at its real source; a paraphrase grants no permission. |
| Ambiguous public method name | Canonical naming/compatibility rules existed without a focused FPF naming route. | Start with actual result, effect, callers, and existing conventions; consult selected F.18 only for the unresolved distinction. No unrelated rename or automatic NameCard. |
| Cache optimization | `CMP.3` already covered reuse and effects. | Preserve outputs, invalidation/lifetime, completion, and side effects; use observed cost to choose reuse or cheap recomputation. New sufficiency/burden guidance also applies to decisions introduced mid-task. |
| Source-access failure or truncated material | Pinned/offline/continuation support existed, with less explicit outer-limit and candidate-review guidance. | Reuse a suitable accepted edition, reformulate a literal-search miss, or continue the same pinned section. Missing conditions remain unresolved; optional access failure does not expand permissions or force an unrelated adoption review. |

Additional handoff check: a successfully returned source extract or passing subtask that leaves the requested action unsupported still needs the missing connection (`C.39`). Required security, compatibility, verification, and review rules remain binding; discretionary investigation must have a useful attainable result (`C.11.DUA`).

## Verification

- `pwsh -NoProfile -File docs/agents/tools/Sync-Fpf.ps1 -Action Ensure -Force`: restored the ignored cache from upstream; candidate matches the full SHA above. Network access needed sandbox escalation; the initial default-sandbox DNS attempt failed without adopting a source.
- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` and `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: passed all 14 checks after the instruction edits.
- `pwsh -NoProfile -File docs/agents/artifacts/fpf-library-2026-10-09/verify-cache.ps1`: task-owned ignored probe passed under PowerShell 7.6.5. Explicit selection left the first-use candidate unaccepted; `Ensure -Offline`, pinned reads, and selection preserved the adoption-file hash. Complete publication discovery returned 44 files and 25 unique profile pattern roots. Seven 2,048-character / 30-line pages reconstructed the 13,482-character `KCAE.DELIVER` read exactly, retaining the selector and section boundary. Literal search continued using its returned offset. A normal `Ensure` returned `throttled`.
- An earlier ad hoc locator probe incorrectly treated the filename `USING-FPF.md` as a pattern ID; narrowing it to inline pattern references removed that probe-only false positive. No source-helper repair was needed.
- `git diff --check`: passed. Explicit byte/text checks passed strict UTF-8 without BOM and CRLF for all 13 changed repository/local text files, including the new summary and ignored local override. Both instruction validators also passed with the summary/index present. Cache state, the local override, and task probe are ignored.
- No C# build, database, browser, full cache-lifecycle suite, or Unix run: runtime/helper code is unchanged. Actual multi-edition refresh failures and worktree fallback were not re-exercised. Walkthroughs demonstrate routing consistency; token use, latency, and development effectiveness remain unmeasured.

## Review and adoption

Fresh independent `reviewer_high` review used `code_reviewer.md` and the agent-workflow overlay, with the active summary withheld until its initial verdict. Initial review and the separate summary/index audit returned no blocking findings and no supplemental candidates. The integrator retained that verdict; no tracked fixes were required by review.

After those reviews, `pwsh -NoProfile -File docs/agents/tools/Sync-Fpf.ps1 -Action Accept -Revision 0c6ade275e9360f0c5ab9715d8f05c0dbaa13cf8 -ReviewNote 'Reviewed pinned Readme, USING-FPF, licenses, 44-publication inventory, selected C.39/C.11.DUA/F.19/F.18/ME/KCAE guidance and retained diagnosis/check/computation/replacement routes. Instruction pack 1.11.0 passed two-shell validators, bounded reader/routing checks, independent agent-workflow review and summary audit. No runtime or source-helper changes; ordinary use stays selective with separate truth, permission and compatibility evidence.'` succeeded.

The same ignored probe with `-ExpectAccepted` passed under PowerShell 7.6.5, including offline state preservation and the daily throttle. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Sync-Fpf.ps1 -Action Ensure -Offline` returned `ready`, selected the accepted SHA above, and reported `PolicyChanged=false`, `ReviewRequired=false`, and no notice. Candidate-versus-accepted selection was exercised before and after adoption of this restored edition; this was not a two-different-edition coexistence experiment. Changes remain uncommitted on `master`.
