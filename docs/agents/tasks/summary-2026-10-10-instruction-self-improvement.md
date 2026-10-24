# Lean instruction pack and FPF refresh

## Outcome and ownership

Instruction pack `1.11.0` -> `1.12.0`, based on framework revision `9c72954f`. Scope is agent guidance, one profile's documentation route, and the instruction validator; runtime, schemas, packages, model/effort settings, and historical summaries are unchanged. After implementation and review, the user explicitly authorized committing and pushing these scoped framework changes on the existing branch. No branch change, PR, deployment, or database action is part of this task. No runtime breaking-change changelog entry is needed.

| Disposition | Result / surviving owner |
| --- | --- |
| Adapt / remove repeated explanation | `AGENTS.md` retains startup, authority, framework, security, compatibility, and closeout constraints; `workflow.md` owns execution/evidence. |
| Move | Role/profile selection, packets, ownership and fallback move from workflow to on-demand `delegation.md`; eligibility stays in workflow, independent-review gates in review-routing. Profile settings are retained. |
| Adapt | `agent_upgrade.md` specifies pinned source/target/base comparison, explicit two-way fallback, target path/runtime mapping, preservation of local tools/profiles, and partial-import reporting before version adoption. |
| Adapt | Review handoff supplies baseline and candidate instructions; candidate policy cannot waive its controlling review gate. New optional upgrade probes exercise these decisions. |
| Keep | Five reviewer overlays, 16 role profiles, canonical domain/glossary/heuristics, optional MCP/model advice, learning pilot, and other task prompts retain their roles. One changed heading link in consumer-contract is repaired. |
| Defer | Broad validator/parser refactoring, automated learning, model changes, weaker summary/review gates, and runtime changes lack a need in this scope. |

The read-only audit covered all live instruction families and task prompts; helpers were inspected selectively around affected contracts, not comprehensively re-audited. Bounded discovery packets supplied coverage and were consumed by the primary; follow-ups corrected incomplete evidence. Implementation and integration stayed with the primary. This is instruction maintenance, not a counted learning-pilot cycle or a productivity benchmark.

## Source review

FPF source: [Anatoly Levenchuk's Library at e2c6ac1](https://github.com/ailev/FPF/tree/e2c6ac1e97dae9b9f54f707477f7d86a12d91852), full SHA `e2c6ac1e97dae9b9f54f707477f7d86a12d91852`, fetched on 2026-10-10. Actual prior accepted state was `181b9e798f6ebae07c0eb871c0a91e6c9b1fbce8`; current state, not a historical summary, establishes this comparison.

Reviewed complete candidate `USING-FPF.md`, licensing notices (unchanged), 43/44-publication inventories, selected suite navigation changes, Core `C.39:4.1` and `F.1:4.4`, and complete Method Engineering `ME.14` with relevant old/new differences. Verified 25 existing profile pattern roots plus `F.1` resolve uniquely. The change report lists 34 files and 383 patterns and truncates its pattern list; this was selective semantic review, not review of every changed body.

`C.39:4.1` keeps intermediate work tied to acceptance; `ME.14` supports bounded comparisons without treating shorter prose as effectiveness. The guide/profile now route uncertain terminology/source access to `F.1:4.4`, with no mandatory search record. Existing CC BY 4.0 attribution and adaptation notices remain. No upstream code was executed and no full Core was loaded into agent context.

Official [instruction discovery](https://learn.chatgpt.com/docs/agent-configuration/agents-md) and [progressive disclosure guidance](https://developers.openai.com/blog/rethinking-skills-and-prompts-for-gpt-6-astra) were consulted for tool assumptions; repository facts and safeguards determine this change.

## Measured text and routing checks

Whitespace-delimited word counts, not model tokens:

| Surface | Before | After |
| --- | ---: | ---: |
| AGENTS.md | 1,439 | 970 |
| Common workflow | 2,031 | 913 |
| Root + common workflow | 3,470 | 1,883 |
| Root + docs router + common workflow | 4,126 | 2,571 |

Root text is 32.6% shorter; root plus workflow is 45.7% shorter. Delegated work still loads its moved guidance; these figures do not measure whole-task tokens, latency, or defects.

Manual baseline/candidate walkthroughs preserve direct execution for an ordinary fix; required POST/row authorization, provider and actual-consumer controls for risky changes; optional FPF at any task size; and read-only/explicit-write boundaries. Import probes add explicit handling for older runtimes, root/nested layouts, new test locations, no trustworthy base, stronger local validators, partial imports, and policy review. Ten optional cases are in `instruction-upgrade-scenarios.md`. They test instruction decisions, not application acceptance or measured development outcomes.

## Verification and review

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1`: baseline and candidate passed all 14 check groups.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: candidate passed.
- Task-owned ignored `docs/agents/artifacts/instruction-pack-2026-10-10/Test-RoutingFixtures.ps1`, invoked under both Windows shells with `-Shell pwsh` / `-Shell powershell.exe`: five controls each passed. A valid copied instruction surface and its restoration pass; missing workflow delegation route, missing canonical role, and missing managed-path entry fail as intended. Initial fixture setup lacked linked topic docs; correcting that fixture resolved its positive-control failure without production changes. Task fixture directories were removed.
- `git diff --check`: passed. Both validators checked strict UTF-8/no BOM/CRLF, portable paths, profile rules, managed files, local link paths and summary indexing. Targeted heading-link inspection caught the consumer-contract fragment that path-only validation cannot check.
- FPF `Ensure -Force` fetched the named revision. After independent review, final `Accept` bound the current guide/profile policy to that revision. Both `Status` and `Ensure -Offline` returned `ready`, with accepted/candidate/selected SHA equal, `SelectedIsAccepted=true`, `PolicyChanged=false`, and `ReviewRequired=false`.
- No C# build, app/browser/database exercise, FPF/learning lifecycle suite, Unix check, or production/task-performance experiment: those implementations are unchanged and no such acceptance is claimed.

Fresh independent `reviewer_high` review used agent-workflow and consumer-contract overlays with no author history or active summary. The initial Low stale fragment was repaired. Supplemental summary/index audit reconciled evidence, privacy, measurements, limits, migration and rollback. Final adjudicated verdict: **No blocking findings.** Final full instruction validation passed under both Windows shells after summary/index and wording updates.

## Migration and rollback

Imports must include the two new managed docs and update workflow, review, prompt, profile, manifest and validator routes together. Preserve target runtime/layout, app-owned knowledge, profiles/tools, optional FPF extension and adoption state; adapt through the upgrade prompt instead of bulk copying. The manifest's validation-selection note routes checks by changed surface.

If the new route causes extra lookup or loses a guard, repair that owner or restore the pre-change guidance and pack metadata together, retaining app adaptations and evidence. FPF can select the retained older SHA for a task; source rollback and tracked-policy rollback are distinct. Runtime acceptance, productivity gains, and future application imports remain unverified.
