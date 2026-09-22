# Instruction review using FPF/DPFs

## Outcome and decisions

Reviewed and simplified the framework instruction stack on `codex/fpf-integration`. Pack `1.5.1` preserves security/data rules, source-copy compatibility, read-only audit boundaries, direct implementation by default, and risk-triggered independent review. Application code, schema, templates, model profiles, and Git publication are outside this change.

| Finding/disposition | Correction and surviving owner |
| --- | --- |
| Fixed: pack validation required exactly `1.5.0`, rejecting a valid subsequent release. | `Test-AgentInstructions.ps1` validates release format and the existing schema/managed-path contract; `instruction-pack.json` owns the version. |
| Fixed: Markdown link checks skipped the root and manifest-managed prompts. | The validator covers shared policy plus all managed Markdown, preserving existing import and route checks. |
| Fixed: the upgrade prompt imposed cache lifecycle tests on every instruction upgrade, including unrelated wording. | `verification.md` separates guide/profile checks from source-helper/cache behavior tests; the prompt and FPF guide route there. |
| Fixed: source refresh and semantic adoption were conflated in the upgrade prompt. | `fpf.md` separates writable refresh, read-only inspection, downloaded candidates, reviewed adoption, and task pins. |
| Adapted: redundant startup, review adjudication, FPF routing, summary headings, and closeout instructions. | Root startup stays in `AGENTS.md`; review criteria/verdict stay in `code_reviewer.md`; workflow, review routing, and FPF files retain their distinct detailed owners. |
| Clarified: an authorized improvement does not require a second audit approval; an unresolved decision blocks only dependent work. | `workflow.md` and the upgrade prompt preserve existing permission limits and compatible/migration decisions. |
| Retained: root security/provider/compatibility rules, available model profiles, independent review, and summary audit. | No demonstrated defect justified weakening or redesigning these controls. |
| Deferred: general speed/quality claims or model recalibration. | This task establishes local instruction/validator corrections, not comparative development effectiveness. |

A bounded `discovery_fast` packet inspected the remaining prompts, domain/glossary/heuristics, optional-tool guidance, and agent profiles; it found no additional change-worthy defect. The primary consumed that evidence and owned all edits and integration.

## FPF source basis

Used accepted revision `083cdb15580906da7b94c36f613bf57358550b53` of [Anatoly Levenchuk's FPF](https://github.com/ailev/FPF/tree/083cdb15580906da7b94c36f613bf57358550b53), including `USING-FPF.md` and bounded applicability/solution sections of the [Method Engineering DPF](https://github.com/ailev/FPF/blob/083cdb15580906da7b94c36f613bf57358550b53/Engineering%20DPF%20Suite/METHOD-ENGINEERING-PRINCIPLES-FRAMEWORK.md):

- ME.12: locate a contradicted claim and repair its owner; applied to validation, adoption wording, and duplicate policy.
- ME.14: compare practical benefit and burden while retaining protective constraints; supported scoped simplification rather than new ceremony or wholesale redesign.
- ME.22: separate content/representation changes and observed usefulness; informed before/after request walkthroughs and limits on efficiency claims.

These ideas are expressed in repository language; no upstream corpus is distributed or executed. Upstream publications carry [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Existing licensing and adaptation guidance remains in `fpf.md`.

## Verification and representative decisions

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` and `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: passed.
- `pwsh -NoProfile -File artifacts/assistant_instruction_review/Verify-InstructionReview.ps1`: all 20 CLI controls passed across both shells. Disposable copied packs exercised a next release, malformed/noncanonical versions, unsupported schema, broken root/prompt links, restored valid state, and contrasting baseline behavior. The retained ignored harness is reproducible in this checkout; its exact task-owned fixtures were cleaned.
- Bounded `Read-Fpf.ps1` List/Search/Read at the pinned SHA verified publication headings, path-scoped search, ME pattern bodies, and continuation. `Sync-Fpf.ps1 -Action Status` and Windows PowerShell `Ensure -Offline` retained the same accepted/candidate source SHA and exposed the changed local policy fingerprint.
- `git diff --check` and strict UTF-8 without BOM/CRLF validation passed. No references to removed FPF heading anchors or superseded test/version wording were found in active routed docs.

Manual before/after walkthroughs (routing evidence, not production feature trials):

| Request/condition | Preserved or improved decision |
| --- | --- |
| Documentation typo or read-only audit | Direct scoped work; no FPF lookup or extra summary without its existing trigger. An audit-only request creates no files. |
| Public API rename or unsafe GET mutation | Inventory callers/consumers, require compatibility/migration or push back on security conflict; FPF cannot waive controls. |
| Authorized instruction cleanup with no product ambiguity | Implement and verify within existing authority; no separate audit approval. |
| Read-only FPF review with cached source | Use Status and pinned reads; do not refresh or adopt. |
| FPF wording edit versus cache-helper behavior change | Text/routes/read walkthroughs for wording; both-shell lifecycle tests remain required for helper/cache changes. |
| Copied-app pack upgrade | Preserve app-owned profile, configuration, history, and adoption; merge managed guidance and validate the target before updating its version. |
| A reviewer requests a scoped fix | Recheck affected contracts/evidence and changed summaries, retaining the initial independent review and required summary audit. |

Whitespace-delimited counts for the six edited guidance files fell from 7479 to 6552 words; the two FPF files fell from 2605 to 1714 (about 34%). These are text-volume measurements. End-to-end timing, token use, causal quality gains, and cross-platform performance were not measured.

## Review, migration, and limits

Fresh `reviewer_high` review with the agent-workflow overlay found no blocking findings. The reviewer independently checked the diff, both-shell validators, read-only FPF status/reads, and the regression harness; after its initial verdict it audited this summary and index for accuracy/privacy. Its Low clarification about the manifest's command inventories was applied in `verification.md`. The adjudicated verdict is: No blocking findings. Review loop can stop.

The same source SHA was re-accepted with a concrete local-policy compatibility note. Final `Status` is ready with accepted/candidate unchanged, `PolicyChanged=false`, and `ReviewRequired=false`; the reviewer independently confirmed that state. No source update or app-owned profile change was introduced.

No C# build, database/browser test, or runtime changelog entry is needed because runtime contracts did not change. FPF source/cache implementation and fixtures are unchanged, so the full lifecycle suite was not rerun; actual bounded reads, status, offline replay, and policy-fingerprint behavior were checked. Unix remains unverified.

Copied applications retain the existing three-way upgrade policy. The optional app profile and ignore rule were not edited. Rollback means restoring this task's scoped instruction changes (using three-way adaptation in a copied app), then reviewing the restored FPF policy fingerprint against the retained source edition. Preserve unrelated work; do not reset the branch or delete source caches.
