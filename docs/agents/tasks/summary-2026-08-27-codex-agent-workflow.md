# Adaptive Codex Agent Workflow Candidate

## Objective / acceptance

Improve the project-scoped Codex workflow for both framework maintenance and independently evolved applications copied from this framework. Keep small/local work direct, automatically route qualifying work through bounded orchestration, isolate replaceable role settings from model-neutral durable policy, preserve local fallback, prevent overlapping writers, and provide a deliberate versioned upgrade boundary for copied instruction packs. Freeze only the coherent durable candidate; comparative benchmark evidence remains pending.

## What changed

- Added explicit direct, orchestrated, and conditional escalation paths with bounded delegation packets, file leases, verification, escalation conditions, and sequential fallback.
- Added stable project custom-agent roles for read-heavy discovery, bounded routine implementation, independent review when warranted, and conditional architecture/escalation work. Durable routing names those role IDs while model and reasoning settings remain confined to replaceable profile files.
- Limited independent review to the risks routed by `review-routing.md` and retained one integrating verdict plus the documented local second pass.
- Added versioned instruction-pack metadata and a reviewed three-way upgrade workflow that preserves application-owned instructions and divergence.
- Kept the permanent instruction validator focused on durable routes, instruction-pack metadata, custom profiles, text format, and local links; it has no dependency on an experiment-specific benchmark package.

## Scope reviewed

Reviewed root/routed agent policy, workflow, verification, review integration and agent-workflow overlay, optional tooling references, upgrade/orchestrator prompts, custom-agent profiles, instruction-pack metadata, and the durable validator. Current official Codex documentation was used for project-scoped custom-agent schema and delegation behavior.

## Requirements / decisions

- Stable role IDs may appear in durable routing because they are semantic capabilities; model and effort identities may appear only in `.codex/agents/*.toml`.
- The primary task remains user-selected and unpinned by repository configuration.
- A matching bounded stage uses its named role only when the profile/capability is available; otherwise the primary task performs the same packet and checks locally.
- `discovery_fast` is configured and instructed for read-only discovery; `implementation_fast` owns only a small disjoint writable set; `reviewer_high` is conditional independent review; `architect_max` is conditional escalation, not a routine reviewer. Live parent permission overrides still govern spawned sessions.
- Existing copied applications upgrade through reviewed path-by-path merge/replace modes and update pack metadata only after their adapted pack validates.

## Changed contracts

Shared contributor/agent workflow and the copied instruction-pack upgrade boundary changed. Runtime APIs, application routes, templates, schemas/providers, production configuration, and deployment behavior did not change, so no runtime changelog entry is required.

## Commands used / verification

- `docs/agents/tools/Test-AgentInstructions.ps1`: passed durable route, role-profile, instruction-pack, text-format, local-link, and task-summary validation.
- `docs/agents/tools/Normalize-TextFiles.ps1 -Check`: passed for every changed durable candidate file.
- PowerShell AST parsing and manifest-shape validation passed for the private harness: four policy bundles, three tasks, and twelve one-repetition screening cells.
- The private harness smoke passed its synthetic fixture, policy-argument, grader, checkpoint, routing-expectation, and report-format checks. Smoke mode did not invoke a benchmark worker or blind-review model.
- `git diff --check`: passed.

No comparative benchmark cell ran while preparing this candidate. The private comparison harness is not tracked. Its adaptive arm remains blocked pending recoverable headless parent-to-child delegation evidence, so the smoke result is harness validation rather than empirical workflow evidence. The larger live host/model matrix was not repeated because no runtime-isolation contract changed and the bounded freeze explicitly limited verification to the cheap smoke and relevant static checks.

## Risks / follow-ups

- Empirical comparison of the frozen candidate is pending. A private, untracked screening harness may be run from a separate task, but its smoke checks are harness validation rather than evidence that this workflow improves application or framework outcomes.
- Custom-agent and delegation capabilities may be unavailable on a future host. Durable local fallback is therefore part of the contract rather than an error path.
- Copied applications can intentionally diverge from the framework instruction pack; upgrades require review and must not bulk-overwrite app-owned guidance.

## Reflection

Stable semantic role names make automatic routing actionable without coupling durable policy to a current model generation. Experiment-specific validation belongs with the experiment, while permanent validation should cover only the public instruction pack it protects.
