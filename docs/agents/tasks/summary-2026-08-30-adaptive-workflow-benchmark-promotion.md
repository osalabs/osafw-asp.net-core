## Objective / acceptance

- Promote only low-regret workflow changes supported by the current controlled framework benchmark while stating its evidence limits accurately.
- Reduce routine wall-clock time without weakening security, data integrity, copied-application compatibility, or review independence.
- Keep direct execution as the normal route, retain bounded fast/maximum-capability options, and prevent model labels or task size from triggering ceremony by themselves.
- Keep raw benchmark artifacts, private references, machine paths, and temporary permission configuration out of the tracked repository.

## What changed

- Kept direct primary-task execution as the default for tightly coupled work and added a stage budget: normally no more than one pre-implementation delegation stage, with every stacked stage requiring its own distinct consumed output and payoff.
- Narrowed `implementation_fast` to small, low-risk, well-specified packets with deterministic acceptance checks, and calibrated that profile to Terra Medium, the setting actually tested.
- Retained `implementation_max` and `reviewer_max` as available project-scoped options. Maximum-capability implementation requires a concrete bounded specialist payoff or substantive failed attempt; maximum-depth review requires a predeclared catastrophic or exceptionally costly miss condition.
- Required real established public entry paths, baseline consumer inventories, attacker or ordinary negative controls, and intended-safe/trusted plus preserved-compatibility positive controls for classifier, default-deny, security-boundary, and public-contract changes.
- Kept fresh independent review initially blinded from worker narrative, self-verdicts, policy identity, known findings, and changed active summaries; after the initial verdict, every changed active summary now receives a mandatory supplemental factual, privacy, and recorded-evidence audit before final adjudication.
- Bumped the copied instruction pack from `1.1.0` to `1.3.0` for the two new roles and the evidence-backed routing/verification changes.
- Added the routed `Search-Repo.ps1` helper to the copied pack, derived portability coverage from the pack manifest, and published a PowerShell 7 primary validation path plus a process-scoped Windows PowerShell 5.1 fallback.
- Allowed explicitly justified, model-neutral `.codex/config.toml` controls such as agent concurrency while retaining deterministic rejection of primary model/reasoning pins and project-wide subagent model defaults. Copied applications now preserve that project-owned configuration file.

## Benchmark evidence / decisions

- **Routine active-only lookup task:** direct Sol Max took 36m23s and scored 79/100; direct/reviewed Sol High took 25m52s and scored 49/100; candidate direct Sol High took 24m02s and scored 49/100; the full discovery/architecture/implementation/review stack took 58m22s and scored 49/100. All four failed the blind acceptance gate. This supports direct-by-default and the stage budget, not a claim that any routine policy is generally sufficient.
- **Narrow deterministic URL-escaping task:** Terra Medium and Sol High both scored 100/100. Gross worker time was about 1m35s versus 6m58s, with no reported user-approval wait. This supports a bounded fast route, but one small task does not establish a general model ranking or justify Terra for broad, security, schema, provider, or public-contract work.
- **Stored-rendering security task:** direct Sol Max took 34m58s and scored 39/100; direct Sol High plus fresh High review and one fix/re-review loop took 30m51s and scored 74/100; adaptive direct Sol High plus fresh Max review and one fix/re-review loop took 50m05s and scored 89/100, passing the blind gate. The adaptive route correctly kept the tightly coupled implementation in the primary task and used higher capability for independent review.
- The passing security arm still missed one family of intended trusted help/docs/entity-builder consumers. A real Chromium harness passed every tested client security branch and the full suite introduced no new failures, but the compatibility miss confirms that maximum-depth review is not proof and that negative security tests need baseline positive controls.
- Worker intervals in this controlled run did not include reported user-approval waits. Restore/build preflights and one shared-cache retry occurred outside model timings. Token/credit use was unavailable, so the benchmark compares observed wall-clock and blind quality rather than cost.
- The matrix supports conservative routing safeguards: start direct, use Terra Medium only for bounded low-risk deterministic implementation, add fresh review by consequence, and escalate implementation only for a concrete payoff. It does not establish a universal optimum; repeated samples and additional medium/copied-application tasks are still needed.

## Changed contracts

- Shared agent workflow now uses the smallest sufficient route and normally at most one pre-implementation delegation stage.
- `implementation_fast` is Terra Medium and explicitly rejects scope expansion into public, schema/provider, security, architecture, or broad compatibility work.
- `implementation_max` and `reviewer_max` are available conditional capabilities, not automatic consequences of a high-risk label and not correctness guarantees.
- Review and verification now require established-entry-path negative and intended/preserved positive controls for the failure classes exposed by the benchmark.
- Review requires a changed-active-summary supplemental audit after the initially blinded verdict; reviewer profiles encode the two-stage contract and deterministic validation guards its required policy surfaces.
- Copied-pack validation manages every routed helper, scans every manifest-managed file for private identifiers, uses `pwsh` as the primary shell, and provides explicit `powershell.exe -ExecutionPolicy Bypass` commands for the Windows PowerShell 5.1 fallback.
- Project `.codex/config.toml` is permitted when justified and model-neutral. Validation rejects top-level `model`/`model_reasoning_effort` and `[agents]` default model/effort pins while allowing documented controls such as `max_concurrent_threads_per_session`.
- Copied applications receive these workflow changes through instruction-pack version `1.3.0`; application-specific instructions and preserved files remain authoritative through the existing three-way-review upgrade policy.
- No runtime, public API, schema/provider, generated application output, security, or deployment contract changed; no product changelog entry is needed.

## Commands used / verification

- The exact manifest primary command, `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1`, and its Windows PowerShell 5.1 fallback with process-scoped `-ExecutionPolicy Bypass` both exited 0. Their positive checks cover routes/files, role boundaries, primary-model neutrality, the mandatory summary-audit policy, instruction-pack metadata and commands, every manifest-managed portability surface, links, task indexing, and ParsePage literals.
- `Get-RepoPath` and `invalid-encoding` private-identifier negative controls under both shells exited 1 against `Test-AgentInstructions.ps1` and `Normalize-TextFiles.ps1`, respectively, emitted the expected portability failure, and omitted the contradictory portability pass. An absent-identifier positive control exited 0 under both shells.
- The exact primary and Windows PowerShell 5.1 fallback `Normalize-TextFiles.ps1 -Check` commands passed for every changed instruction file: strict UTF-8 without BOM and CRLF.
- A scoped private-reference scan returned zero matches, and `git -c core.autocrlf=true diff --check` passed.
- The benchmark promotion's original fresh, initially blinded `reviewer_max` policy review used the agent-workflow overlay. After one bounded fix/re-review sequence, the final verdict was `No blocking findings.` and `Review loop can stop.`
- For the PR-review remediation, implementation stayed in the primary task because the manifest, validator, and review-policy contracts were tightly coupled. One independent-review attempt was discarded before adjudication after it self-reported loss of summary blinding. A replacement fresh `reviewer_high` completed a blinded initial pass across all 21 non-summary changed files with the agent-workflow and consumer-contract overlays and returned `No blocking findings.`; the same reviewer then performed the required supplemental audit of both changed summaries before closeout. The runtime exposed no exact child timing, and no implementation-child output was used.
- For the project-config compatibility remediation, a separate fresh `reviewer_high` remained blinded from both active summaries while reviewing the 21 non-summary PR files. Its bounded fix/re-review loops exposed escaped-key bypasses and an escaped multiline-delimiter false rejection; after semantic key decoding, case-sensitive matching, structure-aware inline-table scanning, and escape-aware multiline handling, its final initial verdict was `No blocking findings.` and `Review loop can stop.` The same reviewer then performed the required supplemental audit of both changed summaries before closeout.
- Project-config policy cases passed under PowerShell 7 and Windows PowerShell 5.1 for model-neutral table/dotted controls, comments and multiline values (including escaped multiline delimiters), top-level primary pins, `[agents]` table/dotted defaults, case-sensitive and Unicode-escaped quoted keys, and structure-aware inline tables. Temporary real `.codex/config.toml` positive controls for documented concurrency/interruption settings and escaped-delimiter `developer_instructions` passed the full validator under both shells. Top-level and Unicode-escaped primary or project-wide agent pins made both entrypoints exit 1 with the expected focused failures. Every task-owned fixture was then removed.
- Benchmark implementation and grading commands/results remain in ignored local evidence rather than this public summary.

## Risks / follow-ups

- The current matrix has one counted run per task/policy. Repeat the fast calibration and add several medium tasks before treating the observed speed ratio as stable.
- Add copied-application feature and migration tasks in future benchmarks using synthetic/public fixtures; private client repositories remain references only and never execution fixtures or public evidence.
- `implementation_max` remains useful as an available recovery/specialist option, but this run produced no evidence that automatically delegating high-risk implementation to Max improves accepted quality.
- Luna discovery was observed only inside a failed stacked routine route, so its independent value remains unproven.
- Capture token/credit use when the platform exposes reliable per-task telemetry; wall-clock alone cannot optimize credit efficiency.

## Reflection

- Task size and risk labels were poor routing signals by themselves. Tight coupling, exclusive ownership, a reusable consumed output, and a credible stage-specific payoff were more useful.
- Independent review produced more value than automatic implementation orchestration in the security calibration, but reviewer depth did not replace baseline consumer discovery or independent behavior probes.
- Candidate-authored tests repeatedly passed while blind probes found missed public APIs, entry paths, query behavior, or compatibility. Evidence must be designed from the external contract inward.
