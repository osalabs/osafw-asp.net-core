# Local FPF retrieval and instruction maintenance

## Outcome and acceptance

Instruction pack 1.13.0 implements PowerShell-only local FPF retrieval, explicit source adoption, three-edition cache retention, current publication routing, and focused instruction/validator improvements. Framework/application contracts, user configuration, model profiles, historical records, and unrelated work are preserved. Baseline: `00826fa038f9c9a67971c307b77cf11fe2592fe3`.

The approved retention policy deliberately allows older task/worktree pins to expire. Read-only operations must invoke no Git process and modify no cache state. Writable refresh owns migration, recovery and cleanup; acceptance remains explicit. No application, database, CI, Git publication, or notification changes are in scope.

## Changes and scope

- Kept existing instruction owners and added concise evidence/authority, read-only setup, explicit maintenance scope, generic independent-review fallback, and verification-outcome rules. Extended optional upgrade-routing probes without treating them as productivity measurements.
- Added recursively discovered publication snapshots and version-2 checksummed indexes. Read-only commands verify local metadata and accessed blobs without Git, repair, locks or writes. The reader retains its selectors, section boundaries and continuation fields; change listing adds both comparison pins and complete pagination.
- Writable Ensure owns migration, recovery, throttled refresh and bounded retention. Snapshot refs precede published receipts and are protected before Git-object collection. Pending cleanup retries within the throttle. Candidate observation order determines the most recent other edition; interrupted adoption temporarily protects the calling checkout's prior pin within the three-edition cap.
- The validator inventories tracked instruction/helper/test paths and explicit new managed files. It rejects omitted ownership, redirected paths and filesystem aliases while excluding protected application knowledge, private context, history and unrelated untracked content. Added isolated positive/negative fixtures.
- Updated public FPF routes and attribution, pack metadata, upgrade guidance and the dated tooling compatibility note. Actual checkout adoption/cache state was not migrated as part of this implementation; migration and source smoke checks used isolated copies.

## Verification and review

- **Passed:** baseline and final candidate instruction validation (14 groups) on both supported shells; UTF-8 without BOM/CRLF across all 20 changed/new files, local links, `git diff --check`, and a full changed-file privacy scan with identifiers supplied transiently.
- **Passed:** `Test-AgentInstructionFixtures.ps1`, 15 assertions on PowerShell 7.6.5 and Windows PowerShell 5.1.26100.9711, including omitted tracked helpers, protected/untracked content, aliases, junctions and transient privacy identifiers.
- **Passed:** final `Test-Fpf.ps1` run, 97 assertions on both shells, including a real failed adoption replace where the calling checkout and shared accepted revision differ.
- FPF coverage includes both publication layouts; v1 migration and interrupted receipts; failed refresh and stale acceptance; independent worktrees; six ordinary CLI paths with Git absent and the object store moved away; unchanged cache hashes/mtimes; Unicode/ambiguity/section continuation; complete change pagination; damaged indexes/publications; retention beyond three editions; expired pins and actual object reclamation; interrupted adoption/cleanup; redirects and unsafe Git configuration.
- **Passed:** an isolated copy of a real legacy cache migrated under the refresh throttle while preserving adoption. Nine source pages at `0c6ade275e9360f0c5ab9715d8f05c0dbaa13cf8` matched every field returned by the baseline reader, including text and continuation, in the final comparison on both shells.
- **Passed:** public-source refresh discovered 48 publications at `d9c3dd65da3cd9f7df898698e49c5464dd8a6c4b`. Read `USING-FPF.md` completely and resolved all 20 profile PatternIDs to unique current carriers. This establishes navigation and source availability, not blanket method applicability or automatic adoption.
- **Not required:** application builds, databases and unchanged learning-helper suites; this change touches instruction/tooling contracts only. Unix behavior and real-task productivity remain unverified and are not claimed.
- Fresh `reviewer_high` review used the governing baseline and candidate with agent-workflow/state-integrity overlays, without implementation history or this summary. Three Medium findings concerned ref publication ordering, protected-path aliases and previous-edition selection. The reviewer confirmed all three repairs and regression controls, with no new blocking candidates, then audited this summary and index for factual consistency and privacy. Final outcome: no blocking findings.

## Risks and follow-ups

The deliberate compatibility change is loss of indefinite pin retention. Old worktree pins may become unavailable, and existing caches need writable Ensure before local reads. Acceptance never prunes or repairs. Public guidance retains selective method use and source conditions.

Local Git/junction fixtures required execution outside the Windows filesystem sandbox; runs stayed in task-owned ignored directories. Successful suites remove their fixtures. Retained source-comparison evidence is ignored under `docs/agents/artifacts/fpf-port-20261018/`; failed development fixtures are cleaned only by exact task-owned path. No credentials, private identifiers or source snapshots belong in public output.
