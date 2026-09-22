# Portable FPF integration

## Objective / acceptance

Provide the same optional FPF/DPF source capability to framework contributors and developers of copied applications. Keep upstream publications ignored, preserve application-owned policy, use explicit task revisions and bounded retrieval, and leave runtime behavior and public C# APIs unchanged.

## What changed

- Instruction pack 1.5.0 adds the root/router entry, shared guide and osafw usage profile, sync/read helpers, their shared implementation and integration tests.
- A shallow bare Git cache contains the complete upstream tree. Derived indexes and source refresh state may be shared across linked worktrees; adoption and policy fingerprints stay per checkout. No upstream code is executed or checked out.
- Refresh attempts are throttled to 24 hours, with force and offline paths. Downloaded candidates, accepted revisions and task selections remain distinct. Agents review compatible updates before recording acceptance; unresolved policy changes retain the prior edition.
- Bounded readers return exact revision/path/line provenance and continuation. Missing or ambiguous IDs fail explicitly; fenced examples do not become pattern locators.
- Copied-app upgrades merge the exact cache-ignore rule and preserve optional `docs/agents/fpf-app.md`, application rules and local state. Attribution explains upstream ownership and CC BY 4.0 separately from independently authored application code.

## Requirements / decisions

The integration is development-time reference infrastructure. Repository contracts, observed behavior and explicit developer decisions retain authority. Every task gets a proportional internal intent/assumption/conflict/evidence check, but routine work does not require FPF retrieval or an interview.

Source installation does not write machine paths into tracked instructions. `Status`, `Select` and `Ensure -Offline` are read-only. Selection requires a complete SHA already validated in the cache. A local fallback discloses unavailable previously accepted objects and preserves the recorded SHA; recovery of identical objects can restore that acceptance.

The shared profile starts with PSD, Systems Engineering, Method Engineering, Computational Thinking, suite-selected domain DPFs and Core. A formal osafw LPF, additional retrieval services and general effectiveness claims are outside this change.

## Source evidence

Live installation validated upstream revision `083cdb15580906da7b94c36f613bf57358550b53` from [ailev/FPF](https://github.com/ailev/FPF), with 35 indexed text publications/notices. The full Core was never emitted to task context. Bounded reads covered:

- `USING-FPF.md` and the two suite entry documents: question-driven selection, actual body/condition reading, same-edition references and stopping when the needed result is available.
- `LICENSING.md`: author attribution, CC BY coverage and exceptions, and the distinction between applying methods and distributing protected expression.
- PSD.3 applicability and problem framing: alternative formulations matter only if they change a live decision; the existing requirement interview should remain proportional.
- ME.11 applicability and solution: observed trials, planned exercises and claims of effectiveness are separate. The walkthroughs below are not field evidence of better development outcomes.
- Core G.11 applicability and refresh-kit sections: edition/policy pins and scoped refresh informed the distinction between available source, an accepted edition and the current task.
- SYSE.6 applicability: architecture selection and reopening conditions remain separate from release authority.
- Computational Thinking catalog: current bodies use `CMP` IDs even where practical-use cards use `CP-...` names.

These are interpretations applied to repository constraints, not claims of formal FPF conformance. No upstream corpus text is distributed in the instruction pack.

## Representative request replay

This was a manual routing and decision walkthrough in the implementation task, comparing the baseline `HEAD` workflow/root contracts with the proposed pack. It did not execute five production features or measure causal improvement in agent performance.

| Representative request | Baseline route | Proposed route and observed decision |
| --- | --- | --- |
| Correct a typo in template documentation | Read nearby text and make the small edit. | Same result; the internal check terminates without FPF lookup, a new artifact or a question. |
| Allow guest deletion by GET while preserving existing authorization and POST rules | Evidence-based push-back from the root security contracts. | Same necessary push-back. PSD routing is available for unresolved interpretations, but no source lookup is needed to rediscover an explicit contradiction. |
| Rename the public `fw.model<T>()` entrypoint and remove the old name in the framework | Inventory callers and copied-app compatibility before changing the API. | Same contract protection; Systems Engineering can clarify boundary/consumer implications when alternatives remain open. A pattern cannot waive the migration or shim decision. |
| Accelerate publication search by truncating input before indexing | Falsify the optimization with requested-output/negative controls. | Computational Thinking provides a source route for representation and information-loss questions. Keep complete source for matching and bound emitted pages; actual CLI regressions cover continuation and Unicode preservation. |
| Add expense-approval CRUD to a copied business application | Establish approval roles, ownership, states and schema authority before scaffolding. | Consult the app-owned profile and current suite for useful domain distinctions. Preserve app decisions; ask only unresolved business/data questions. A finance or administration DPF cannot invent approval authority. |

The walkthroughs show reachable source routes and preservation of existing controls. They do not establish a practical quality gain over the baseline. Subsequent real tasks can record whether a specific source changed a decision, what evidence supported it and whether the extra reading was worthwhile.

## Commands used / verification

- `pwsh -NoProfile -File docs/agents/tools/Test-Fpf.ps1` and `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-Fpf.ps1` passed all 53 assertions on PowerShell 7.6.5 and Windows PowerShell 5.1.26100.9444.
- `Test-AgentInstructions.ps1` passed under PowerShell 7 and Windows PowerShell 5.1: instruction routes, managed/preserved pack paths, local links, private-path exclusions, strict UTF-8 without BOM/CRLF and single-line ParsePage routes.
- `git diff --check` passed for tracked changes. New script/doc text is included in the instruction validator.
- Live `Sync-Fpf.ps1 -Action Ensure` installed the source; bounded `Read-Fpf.ps1` reads verified Core, DPF, heading and search paths. `Accept` recorded a reviewed source edition.
- Tests invoke the public helper processes against disposable local Git repositories. They cover copying helpers into a customized app, targeted upgrade preservation, ignored source exclusion, concurrent installation/worktrees, daily throttle/expiry/force, missing first-use source, offline/last-good operation, interrupted temporary writes, invalid publication/UTF-8, repository identity mismatch, acceptance/selection and stable old revisions, app-policy changes, failed/stale/successful cache-root transitions, ambiguity, missing headings and bounded Unicode continuation.

No runtime source, templates, schema, provider configuration or app API changed, so no C# build, database test, browser run or runtime changelog entry was needed. Pack metadata and the integration/upgrade guide record the development-instruction change. Before the publication follow-up, no commit, branch operation in the real repository, push, PR or external release was performed; Git fixture commits/worktrees are disposable test inputs.

## Review and migration

Fresh independent review with the agent-workflow and state-integrity overlays found one Medium defect: a failed local-cache transition could persist a root that lacked the accepted objects. The repair stages the target root and persists it only after validated recovery of any accepted SHA or successful explicit acceptance. New CLI controls verify failed installation, missing/stale acceptance, a staged candidate missing the old SHA, and successful reviewed local adoption. All 53 assertions passed in both Windows shells after repair. Independent recheck confirmed the original defect fixed and found no further code issue. It caught a stale closeout statement after the policy fingerprint changed; the repaired policy was then re-accepted at the same source SHA with a concrete review note. Fresh Status and Offline both returned ready, PolicyChanged=false and ReviewRequired=false. Final independent verification confirmed the live state and summary evidence. The adjudicated verdict is: No blocking findings. Review loop can stop. The ordinary `reviewer_high` profile is selected for the shared workflow and cache lifecycle risks; no maximum-effort consequence gate is claimed. A separate `implementation_sol_high` worker owned the disjoint documentation/validator packet, which the primary integrated with the helpers. The primary retains final verification and cleanup ownership. Successful test runs cleaned their own fixtures; four known diagnostic fixture directories were removed after verification, with live and legacy caches preserved.

Replaced only the ignored legacy FPF section with the portable guide route after validation. The unrelated prefix and suffix were verified unchanged; the old source cache was not modified. Live Status and Offline both returned the accepted revision with no pending policy review.

## Risks / follow-ups

Windows PowerShell 5.1 and PowerShell 7 on Windows are the supported verified targets. Unix and actual sandbox-denial fallback remain unverified; explicit `-LocalCache` fallback is exercised. Source availability requires Git and either reachable HTTPS upstream or an explicitly selected local Git seed. No background scheduler is installed.

Snapshot refs are retained for task pins and offline replay, so disk usage grows with downloaded revisions; no automatic prune is implemented. Source manifests are derived local state. Semantic compatibility and usefulness still need agent judgment, and a successful installation is not proof of improved engineering decisions.

## Reflection

The isolated fixtures exposed a Windows atomic-replace null-binding issue and an incorrectly escaped synthetic fence before adoption. Live status inspection exposed cross-version date conversion and led to a UTC regression. The documentation worker's independent scope overlapped useful helper implementation; no implementation leases collided. Timing and token comparisons between baseline and proposed instructions were not measured. Future instruction changes should be justified by recorded real-task decisions rather than expanding this profile preemptively.
