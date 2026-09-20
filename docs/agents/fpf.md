# Optional FPF and DPF Reference

Use this guide only for development work where maintained conceptual or domain source material could change the framing, a decision, or the evidence required. FPF is a reference, not repository authority. Current code, project files, canonical docs, ADRs, tests, and explicit developer decisions govern this repository and copied applications.

## Lightweight use boundary

Every task should internally confirm four things: the intended result, material assumptions, conflicts with live contracts or evidence, and what evidence would establish completion. That check should stay proportional and usually produces no separate artifact. A routine fix, mechanical edit, ordinary review, or already well-bounded feature normally stops there without reading FPF.

Use source lookup when a live question materially concerns one or more of these areas:

- product/problem framing, systems or architecture boundaries, interfaces, configurations, or trade-offs;
- domain terminology whose distinctions affect data, security, public contracts, or acceptance;
- design or change of a reusable Method, verification approach, agent workflow, or decision process;
- computational reasoning, algorithm structure, resource limits, or meaning-preserving transformation;
- current-practice/source synthesis, competing approaches, or evidence and provenance limits.

FPF does not supply facts about the application, confer permission or authority, choose a product decision, or replace specialist verification. Do not turn pattern terminology, citations, or task notes into mandatory ceremony.

## Publications and repository profile

The source is the [First Principles Framework repository](https://github.com/ailev/FPF) by Anatoly Levenchuk. The portable cache covers the whole FPF Core publication, both suite directories, independently published DPFs, and their license/attribution files. It is a source corpus: do not execute upstream code, install upstream tooling, or load the full Core into task context.

Start with [fpf-profile.md](fpf-profile.md) for repository-oriented routing. A copied application may add `docs/agents/fpf-app.md` with its own domain routes, examples, and exclusions. That optional file supplements the shared profile and remains application-owned; instruction-pack upgrades preserve it.

Use the suite Reference when a question crosses several DPFs or the applicable DPF is unclear. Use a known sufficient DPF directly. Return to Core for cross-domain distinctions or when a DPF explicitly depends on a Core pattern. Publication order is not a required project sequence.

## Cache and revision model

`docs/agents/tools/Sync-Fpf.ps1` maintains an ignored `.codex-local/fpf/` directory for each checkout. Adoption state and other checkout-owned cache state stay per checkout. In a linked worktree, the helper may reuse the primary checkout's source-only bare Git cache when the Git common-directory layout makes that cache accessible; it must not share adoption state. Pass the `-LocalCache` switch when the shared root is unavailable, inaccessible, or unsuitable.

`Ensure` is the default action. It lazily checks for source updates at most once per 24 hours unless `-Force` is supplied. State records the last attempt, last success, candidate revision, and accepted revision separately so a failed check or an unreviewed candidate cannot masquerade as adoption. `-Offline` forbids network refresh and uses an eligible cached last-good revision. `-SourcePath <localGitRepo>` supplies an explicit local Git corpus for offline use and deterministic tests.

The sync interface is `-Action Ensure|Status|Accept|Select`, with `Ensure` as the default. All actions accept `-RepositoryRoot <path>` and the `-LocalCache` switch; use `-Force` and `-Offline` only with the actions that support them. `Accept` requires `-Revision <full-sha>` and `-ReviewNote <text>`. `Select` requires an already cached full `-Revision` and does not change adoption.

An accepted revision must be a full commit SHA. Compatible source updates may be reviewed and accepted autonomously when they do not change repository policy. A source update that would change tracked rules, permissions, security/data behavior, public compatibility, or workflow policy must be presented for review; never convert it into a silent tracked rewrite. Acceptance requires an actual semantic review and an auditable note:

```powershell
pwsh -NoProfile -File docs/agents/tools/Sync-Fpf.ps1 -Action Accept -Revision <full-sha> -ReviewNote '<what was reviewed and why it is compatible>'
```

`Select` chooses an already cached full revision for the current task without changing accepted adoption. Once selected, keep that full revision stable for the whole task and pass it to every read. Do not combine passages from revisions silently.

Read-only reviewers use `-Action Status` and `Read-Fpf.ps1` against an already cached revision. They do not call `Ensure`, `Accept`, or any path that refreshes or writes cache state.

## Review and recovery details

When `ReviewRequired` is true, review the candidate during the relevant task. The helper validates transport, Git identity, UTF-8 text, required publications, and usable locators; it cannot decide whether changed advice is appropriate.

1. Inspect `Changes`, giving priority to licensing, usage/navigation, removed or renamed publications/patterns, and files marked `ProfileAffected`. This flag covers the shared starting publications and explicit paths or PatternIDs in the shared/app profile.
2. On first use, read `USING-FPF.md`, the relevant suite entry/reference, and licensing notices. On refresh, compare the changed passages at the accepted and candidate SHAs using bounded reads. Read actual pattern conditions and bodies when a changed reference affects the current decision.
3. `Changes` reports at most 40 files and 40 patterns, with totals and `Truncated`. When truncated, compare paged `List` results at both revisions and inspect the relevant affected publications; do not infer that omitted changes were reviewed.
4. If source routes and conditions remain compatible, call `Accept` with the candidate's exact SHA and a concrete review note. Do this autonomously after review. If adoption needs repository-policy changes or an unresolved compatibility decision, retain the accepted edition and prepare the necessary instruction change through the normal review route. Do not ask for approval merely because an upstream revision changed.

`PolicyChanged` also requests review when the guide, helpers, shared profile, or optional app profile differs from the accepted policy fingerprint. This keeps branches and copied-application adaptations independent.

Source operations are serialized with locks and state files are replaced atomically. A failed or interrupted fetch does not change the accepted revision. If a caller loses a race with another candidate update, repeat the review against current `Status`; a stale `Accept` is rejected.

`Status`, `Select`, and `Ensure -Offline` do not create or rewrite cache state. `Select` returns a task pin; it does not persist a new default. `SourcePath` applies only to `Ensure` and binds that cache to the explicitly chosen local Git source. Do not combine it with `-Offline`.

When `-LocalCache` selects an empty worktree cache, `UnavailableAcceptedRevision` reports a previously reviewed revision whose objects are missing there; the adoption record and old shared cache remain preserved. `Ensure -LocalCache` stages an installation in that cache. It remembers the fallback only after successful validation and availability of any previously accepted SHA. Until then, `CacheTransitionPending` reports the staged root and default commands retain the prior root. Inspect a staged candidate with `-LocalCache` and accept it there after review if the old SHA cannot be recovered. A successful `Accept -LocalCache` atomically adopts both the reviewed candidate and the new root; failed installation or acceptance preserves the old adoption record. If source access is denied by the environment before the helper runs, choose the local fallback explicitly.

No tracked file is rewritten with a machine path. Source objects, generated indexes, locks, attempt/success state, accepted policy fingerprint, and review notes stay ignored. Snapshot refs retain downloaded editions for active task pins and offline replay; there is no automatic pruning. Inspect cache size and active references before any separate, explicitly scoped cleanup.

## Bounded lookup

First inspect status and select a stable full revision:

```powershell
pwsh -NoProfile -File docs/agents/tools/Sync-Fpf.ps1 -Action Ensure
pwsh -NoProfile -File docs/agents/tools/Sync-Fpf.ps1 -Action Status
pwsh -NoProfile -File docs/agents/tools/Sync-Fpf.ps1 -Action Select -Revision <full-sha>
```

Then list publications or search narrowly before reading:

```powershell
pwsh -NoProfile -File docs/agents/tools/Read-Fpf.ps1 -Action List -Revision <full-sha>
pwsh -NoProfile -File docs/agents/tools/Read-Fpf.ps1 -Action Search -Revision <full-sha> -Query 'architecture interface'
pwsh -NoProfile -File docs/agents/tools/Read-Fpf.ps1 -Action Read -Revision <full-sha> -Path 'Engineering DPF Suite/SYSTEMS-ENGINEERING-PRINCIPLES-FRAMEWORK.md' -PatternId 'SYSE.6'
```

`Read` also accepts `-Heading`, `-StartLine`, and `-StartColumn`, with `-MaxLines` and `-MaxChars` bounds. `List` and `Search` use `-Offset` for page continuation. Defaults are 120 source lines/items and 12000 characters of Content or compact Items; JSON formatting and provenance metadata are additional. Results are bounded JSON containing the revision, publication/file, source line, and continuation information. Heading indexing excludes fenced code. Pattern IDs must match exactly; ambiguous IDs or headings are reported explicitly rather than guessed. Follow continuation only while the same task question still needs it. Never request or emit the entire Core.

The read interface is `-Action List|Search|Read -Revision <full-sha>`. `Search` takes `-Query <literalText>`, and `List`/`Search` page with `-Offset`. `Read` takes a relative publication `-Path` and one exact selector or continuation position: `-PatternId`, `-Heading`, or `-StartLine` with optional `-StartColumn`. All read actions also accept `-RepositoryRoot <path>` and the `-LocalCache` switch.

The sync result is also JSON and distinguishes source/cache status, accepted/selected/candidate revisions, last attempt/success, and change metadata. Treat those fields as evidence about the cache operation, not evidence that an upstream claim is correct or applicable here.

## Applying and recording source use

Translate useful distinctions into the repository's established language and connect them to the canonical owner. For example, a Systems Engineering interface question still returns to current controllers, templates, providers, or deployment contracts; a Method Engineering question still returns to [workflow.md](workflow.md) and [verification.md](verification.md); a product framing question still returns to the actual feature owner and acceptance evidence.

Record provenance when it materially influenced a decision, changed the verification boundary, or supplied copied/adapted expression. Usually name the full revision, publication/path, PatternID or exact heading, and the repository evidence that accepted or rejected the contribution. Do not add provenance noise for a lookup that changed nothing.

## Attribution and licensing

FPF and the DPF publications authored by Anatoly Levenchuk are published under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/); the upstream [LICENSING.md](https://github.com/ailev/FPF/blob/main/LICENSING.md) defines exact coverage and exceptions. Third-party material and repository tooling may have different terms.

When copying or adapting protected expression, credit Anatoly Levenchuk, link the FPF repository and CC BY 4.0, identify the actual publication/revision where practical, and indicate the adaptation separately. Applying ideas, methods, PatternIDs, or publication forms does not relicense independently authored application or framework code. CC BY 4.0 has no ShareAlike requirement, and using the ideas does not require a product UI badge.

## Verification and recovery

Run both supported Windows checks after changing the integration:

```powershell
pwsh -NoProfile -File docs/agents/tools/Test-Fpf.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-Fpf.ps1
```

The test uses task-owned ignored fixtures and no configured database. Windows PowerShell 5.1 and PowerShell 7 are the first verified platforms; Unix behavior is not yet verified. If refresh fails, use an eligible last-good cached revision or continue without the optional reference and state the limitation. Never weaken source validation, accept an unreviewed policy change, or mutate tracked rules merely to make the optional lookup available.
