# Sol 6.1 High Model Profile Refresh

## Outcome and scope

Adopted GPT-6.1 Sol High as the recommended starting model, standard implementation profile, and ordinary independent reviewer. Retained Astra for difficult implementation/architecture work and deeper reviews. The user approved this selection after the dated model research.

- Updated `implementation_sol_high` and `reviewer_high` to `gpt-6.1-sol` with High effort; preserved role names, sandbox permissions, and developer instructions.
- Made the standard implementation and ordinary-review defaults explicit. Astra High supplies deeper review; Astra Extra High is preferred for qualifying implementation/architecture escalation. Existing Max consequence gates, inexpensive helper defaults, alternative profiles, independence, and availability fallback remain.
- Refreshed `docs/agents/model-selection.md` with the 2026-09-30 evidence and limitations, including API versus subscription costs, task duration versus token throughput, and benchmark transfer to this C# framework. The note owns the source links; no routine benchmark lookup is required.
- Advanced instruction-pack metadata from 1.8.0 to 1.9.0; its managed-file inventory is unchanged.

The starting-model recommendation remains advisory. No user settings or primary/project-wide model pin changed. There are no runtime application, schema/provider, generated-output, or public API changes, so no application migration or runtime changelog entry is needed. Copied applications retain the existing deliberate three-way instruction-pack upgrade policy.

## Verification

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1`: all 14 checks passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: all 14 checks passed. The execution-policy option applies only to that process.
- `git diff --check`: passed.
- `Normalize-TextFiles.ps1 -Check` for the nine task files: strict UTF-8 without BOM and CRLF passed; both instruction validators and the diff check also passed after adding the summary/index entry.
- The active collaboration tool advertised `implementation_sol_high` and `reviewer_high` with `gpt-6.1-sol` and High effort. Fresh agents were successfully spawned through those named roles without model/profile fallback. This is evidence of advertised runtime settings and role execution; it does not independently prove how the runtime loaded the edited TOML files.

Three bounded calibration packets supplied distinct evidence consumed by the primary:

1. A fresh Sol 6.1 High agent traced real `return_url` behavior. It distinguished `FwController.init` validation through `Utils.isAppUrl` from `FW.redirect`, which does not validate destination safety, and qualified explicit-location callers. Current anchors: `FwController.cs:107`, `:1270`, `:1301`; `Utils.cs:207`; `FW.cs:1207`. It inspected positive/negative controls in `SecurityQuickFixTests.cs:93`, `:115`, and `:368`, correctly separating login `gourl` checks from CRUD `return_url`. This was read-only inspection, not an application test run.
2. The named standard implementation role completed a disposable paging helper against 15 prewritten checks. The stub first failed all 15; the implementation passed defaults, normalization, caps, empty/exact division, invalid configuration, and `int.MaxValue` arithmetic. Final command: `dotnet run --project artifacts/assistant_sol61_20260930/implementation/ModelProfileSmoke.csproj --configuration Release --no-restore`; exit 0, 15/15 passed, no compiler warnings. One nullable-warning cleanup was required; both implementation runs passed all behavior checks. The primary inspected the returned source.
3. The named ordinary-review role independently reviewed a synthetic before/after diff. It found both seeded High defects (removed POST enforcement and lost owner authorization), accepted the preserved-valid authorized GET preview, and checked unchanged signatures. These intentional fixture defects are not application-source defects. The fixture had no separate active task summary.

Fixtures and build output are retained under ignored, task-owned `artifacts/assistant_sol61_20260930/`. The implementation fixture has no NuGet package dependencies, clears package feeds, and uses no application/database configuration. The initial delegated fixture write was rejected by automatic approval review as outside the instruction-update authorization; the user explicitly approved the isolated calibration, after which the same bounded operation succeeded. No approval bypass or broader write was used.

The checks establish bounded behavior, not comparative model quality, faster completion, or subscription savings. Native-agent elapsed time and separately measured approval/platform waits were unavailable; no durations were estimated. No second-model matrix or full application/provider/UI suite was run because neither a consequential unresolved comparison nor a runtime change called for it.

## Independent review

The shared agent-workflow trigger selected a fresh `reviewer_astra_high` with the agent-workflow overlay for deeper judgment on shared model/review routing and external-evidence transfer. Its initial handoff excluded implementation narrative, worker reports, active summaries, and known/suspected findings. Initial independent verdict: No blocking findings. The subsequent active-summary audit returned no supplemental findings. Final independent verdict: No blocking findings; the primary accepted the verdict. Review loop can stop.

Unrelated untracked work was preserved. The user subsequently authorized committing and pushing the reviewed changes.
