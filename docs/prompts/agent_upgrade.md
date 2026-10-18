# Development-Agent Instruction Upgrade

Use for an instruction-only import into a copied application or an evidence-based cleanup of existing agent guidance. A framework runtime upgrade is a separate task.

```text
Perform the evidence-driven upgrade in docs/prompts/agent_upgrade.md. Audit first; pause before edits only if I request an audit-only phase.
```

Follow [workflow.md](../agents/workflow.md) for authority, evidence, and closeout. An audit-only request remains read-only. Otherwise implement scoped supported improvements without another audit approval; ask only for material unresolved choices or actions outside existing authority.

## Audit the target

1. Establish status, repository role, branch policy, and actual runtime/language from current project files or the site's equivalent. Locate source/template/schema/test roots and the relevant build tools. Do not transplant framework paths, APIs, providers, compile symbols, or .NET commands into an older or differently laid-out app.
2. Trace root/nested instruction discovery and tool-specific imports to the docs actually loaded. Inspect local ignored constraints, workflow/review/verification, profiles, prompts, optional FPF, helpers/validators, pack metadata, and app-owned domain/knowledge files. A referenced path is not proof the file exists.
3. Use bounded representative code/history/tasks to identify consequential friction. Summaries and other repositories are evidence, not authority. Check current owners before treating a repeated edit as a process defect.
4. Classify material rules as `Keep`, `Adapt`, `Move`, `Remove duplicate`, `Remove stale/incorrect`, or `Defer`; name the surviving owner. Preserve security, state integrity, compatibility, permissions, independent-review gates, and task evidence while shortening their wording. Use the pre-change gates for this maintenance task; proposed policy cannot waive its own validation.

Read current official product guidance only for tool discovery/configuration behavior that needs verification. Repository files cannot grant capabilities, models, modes, or permissions absent from the runtime. Keep executable model settings in role profiles, not root/workflow prose; do not change the primary model or add project config/services without explicit authority.

## Import an instruction pack

Use this section only for an import; local cleanup needs no source/target merge exercise.

1. Pin the source revision (and a file snapshot/hash for any included uncommitted source changes), target starting revision/status, source pack version, and the previously imported source revision/version when recoverable. Compare **base / current target / new source** for every managed path. A version label alone is not a file baseline. If no trustworthy base exists, record that limit and use an explicit two-way classification; never invent ancestry or treat the app copy as unmodified upstream.
2. Map source paths to the target's actual instruction/doc layout before edits. Treat the pack's `files` list as the import scope, not permission to copy runtime source, templates, SQL, packages, deployment, or whole documentation trees. Use current target facts when adapting shared instructions.
3. Apply each `upgradeMode` through that comparison: merge shared rules; merge only needed ignore rules; deliberately reconcile profile/import/metadata replacements with target customizations and explicit model/cost constraints. Preserve app-owned helpers, stronger validator checks, and additional profiles unless their replacement is justified within task scope.
4. Preserve `preserveInCopiedApplications`: local instructions, project configuration, app domain/glossary/heuristics, task history, ADRs, optional `docs/agents/fpf-app.md`, shared-profile adaptations, and `.codex-local/fpf/` adoption/cache state. Add the exact `/.codex-local/` ignore rule once if absent; preserve all other ignore rules.
   Instruction import does not itself migrate or prune that cache. Review the new tooling contract before writable Ensure: migration retains adoption, but subsequent three-edition retention can expire older task/worktree pins. Retain needed long-lived source archives separately before opting into that cleanup.
5. Keep target tool entrypoints/nested routing until replacement discovery is verified. Repair missing routes and adapt commands/validators to the target layout without suppressing safeguards to obtain a pass. A first adoption may add needed routing; it must not fabricate task history or an upstream runtime identity.
6. Validate the merged pack before advancing its version. Record source-to-target path mapping, preserved customizations, omitted/deferred paths, and applicable checks in the existing task summary. If a required managed component remains unresolved, report a partial import and do not mark the source pack version fully adopted.

For an interrupted or partial import, keep the pinned source and completed components/checks in that same summary. For each unfinished component, record its remaining action, blocker or deferral reason, and the prerequisite or trigger for retrying. On resumption, compare those notes with actual files and effects; preserve completed work and repeat only missing steps or checks whose conditions changed. Instruction adoption, optional source/cache readiness, and the application's runtime verification are separate outcomes; none establishes the others.

## Improve and validate

- Keep `AGENTS.md` for always-needed constraints and routing. Detailed procedures belong in one task-routed owner; use [delegation.md](../agents/delegation.md) only when delegation/profile selection is needed. Retain adequate guidance; do not rewrite every file to make an audit look complete.
- Select FPF only for a useful question under [fpf.md](../agents/fpf.md). An edition refresh and tracked-policy edits have separate review; read-only work does not refresh/adopt. Preserve target FPF extensions and inspect conflicting local overrides.
- Use [verification.md](../agents/verification.md) for text, link/route, helper, and provider-specific check selection. The manifest lists available checks, not a demand to run unrelated suites. Docs-only work needs no app build/database exercise; changed helpers need their affected checks.
- Compare baseline/candidate decisions on representative requests: an ordinary fix, a security or compatibility boundary, and a target with different runtime/layout or local instruction customizations. Verify actions, evidence, and stop conditions, including safe and rejected cases. [instruction-upgrade-scenarios.md](../agents/instruction-upgrade-scenarios.md) owns optional probes and the execution-trial procedure when observed agent behavior is needed.
- For broad workflow redesign or effectiveness claims, use safely reversible held-out work before adoption. Distinguish source-size reduction, routing consistency, and measured development outcomes; validators/walkthroughs prove neither speed nor fewer bugs. Keep observational limits and a rollback or retirement condition.
- Follow [review-routing.md](../agents/review-routing.md) with the agent-workflow overlay. Supply the pre-edit instruction baseline as well as the candidate so edited policy does not judge its own safeguards; withhold the active summary until the initial verdict.

## Closeout

Record scope, dispositions/owners, exact checks, scenario results/limits, version and migration/rollback decisions in the required summary. Report preserved app contracts and unresolved import steps. Historical summaries remain unchanged. Commit, push, publish, or change app/runtime/data behavior only with the corresponding explicit authority.
