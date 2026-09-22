# Agent Learning Pilot

Implemented the first repository-learning pilot with optional summary signals, bounded committed-source collection, and manual improvement cycles. Instruction-pack version is 1.6.0.

## Scope and decisions

- Ordinary tasks keep their existing summary/review requirements. An already-required summary may include at most three explicit learning-signal bullets when a verified reusable surprise exists; there is no required reflection, placeholder, history scan, or extra ledger.
- Maintenance uses the optional reflection prompt, revalidates evidence against current owners, selects at most one to three scoped changes, and prefers a code/test/helper fix over duplicate instructions. Review and publication authority remain unchanged.
- The read-only collector requires a full ancestor checkpoint, pins the end revision, ignores working-tree/untracked content and fenced examples, and exposes file pagination, output/body limits, and incomplete evidence. Counts distinguish added/modified summaries and explicitly remain a proxy for completed substantive tasks.
- Routed manual scenarios cover ordinary work, repeated friction, ignored existing guidance, compatibility/safety, stale evidence, publication authority, incomplete collection, and premature automation. Scenario walkthroughs are design checks, not measured agent-effectiveness evidence.
- No runtime, schema, provider, FPF cache, or public application contract changed; no runtime changelog or application build was needed. Copied applications retain local instructions, history, and app-owned policy during deliberate pack upgrades.

## Verification and review

- `pwsh -NoProfile -File docs/agents/tools/Test-LearningSignals.ps1`: passed all 25 assertions.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-LearningSignals.ps1`: passed all 25 assertions.
- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1`: passed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: passed.
- The text normalizer checked all ten changed pack files: strict UTF-8 without BOM and CRLF passed. `git diff --check` passed.
- Public CLI fixtures cover exact revisions and source lines, Unicode, inert note text, fenced/indented exclusions, empty ranges/placeholders, dirty/untracked exclusions, additions versus modifications, excessive bodies/notes, output budgets, pagination, invalid checkpoints/cursors/encoding, later-commit replay, and an unchanged fixture index.
- Initial fixture execution exposed automatic UTF-16 BOM decoding despite a configured UTF-8 stream. Disabling encoding autodetection made the malformed-input check fail closed. Fixture commits disable machine hooks, signing, and line-ending defaults. Successful fixtures and the exact retained failed fixture were removed.
- Manual design walkthrough: ordinary work adds nothing; repeated evidence proposes a helper repair; skipped existing guidance calls for an implementation fix; compatibility/safety changes retain real-entry controls; stale claims are reverified; note text grants no publication authority; every page/gap retains its pinned source; one cycle never qualifies as two. These are eight scenario walkthroughs, not live task trials.
- Delegation: one implementation worker owned workflow/reflection/scenario docs while the primary owned collector/tests and pack integration. Its scoped docs were consumed directly. A fresh `reviewer_high` using the agent-workflow overlay returned an independent initial verdict of no blocking findings before receiving the summary. The primary then clarified read-only reporting, single-line signals, and the router's local fallback; the supplemental summary correction was rechecked; final independent verdict: No blocking findings. Review loop can stop.

## Pilot checkpoint and follow-up

- PR #295 was merged at `89de6443c5c7552eb2f6bdaa36c66754c4ccef9d`; implementation is on the separate user-authorized `codex/agent-learning` branch. That merge SHA is the starting checkpoint for the first future learning sample; no historical summaries were retrofitted.
- Completed real maintenance cycles: zero. This task builds the pilot. Roughly ten new substantive summaries, with some signals, is an advisory reason to consider a manual cycle; a serious reproducible safety issue may justify one earlier.
- For each future cycle, record the resolved range, completion/continuation state, and deferred source references in the cycle report: the response for read-only work, or a summary only under existing workflow rules or a saved-report request. Preserve those references before advancing the checkpoint. No candidates from a real learning batch were accepted or deferred in this implementation.
- Evaluate later applicable tasks for recurrence, unnecessary discovery/tool work, and review rework. Token/time telemetry is unavailable here; do not infer productivity gains from text size, scripted assertions, or these walkthroughs.
- After two real cycles, assess whether the benefits justify an automatic draft proposal trigger as a separately authorized change. Retire ineffective guidance, or revert the pilot workflow/routes/helpers and their pack metadata together if overhead outweighs observed benefit. There is no scheduler or automatic publication in this change.
