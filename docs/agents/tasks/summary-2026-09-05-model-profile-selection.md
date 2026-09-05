# Model Profile Selection and Lightweight Calibration

## Objective / acceptance

Expand the optional Codex profile catalog for developers starting with either model generation. Preserve existing profile settings, inexpensive helper defaults, explicit developer constraints, and workflow safeguards. Provide dated starting-model advice without changing user settings or pinning the primary task. Verify the catalog and representative behavior without repeating the earlier full evaluation matrix.

## Updated plan and decisions

1. Use current primary-source benchmark results to inform an optional starting recommendation, with harness, uncertainty, latency, and cost limitations.
2. Keep the six existing profiles and add role-appropriate Astra options plus standard Sol High implementation. Select the role first; permit cross-generation delegation in either direction when explicit constraints allow it.
3. Update canonical routing, instruction-pack metadata, and the validator. Keep fixtures and raw evidence task-owned and ignored.
4. Run deterministic checks and three bounded behavior checks, then fresh independent review and the active-summary audit. Treat runtime limitations as limitations, not successful profile-loading evidence.

The resulting advisory recommends Astra Medium for general development when its quality tradeoff is worthwhile, with Sol High retained for speed, cost, or preference. The [dated model-selection note](../model-selection.md) owns the external evidence and caveats. These are repository recommendations, not claims about product defaults or universal superiority.

## What changed

- Added nine Astra profiles: discovery Low; small implementation Medium; implementation escalation Extra High and Max; architecture Extra High and Max; independent review High, Extra High, and Max.
- Added standard implementation Sol High. All six existing profile files remain unchanged, including discovery Luna Low and small implementation Terra Medium.
- Canonical workflow now prioritizes explicit developer constraints, then task quality, latency, cost, and consequences. The primary model's generation is only a tie-breaker. Pinned profile settings remain authoritative; role selection cannot be bypassed by retargeting a pinned profile.
- New Astra Max routes require a recorded exceptional consequence. Existing escalation gates and review isolation, summary audit, ownership, permission, and fallback rules remain in force.
- Added the optional advisory route, updated the upgrade prompt, and advanced the instruction pack from 1.3.1 to 1.4.0. The validator uses one sixteen-profile registry for presence, canonical role routing, sandbox boundaries, reviewer routes, and manifest coverage. Every required profile value, including developer instructions, must be a nonempty TOML string; adapted basic/literal strings, multiline forms, comments, and valid escapes remain supported.

## Changed contracts

The shared agent catalog and selection guidance are additive developer-workflow changes. There is no primary/project-wide model pin, user setting change, runtime application source change, schema/provider change, or public application behavior change. Copied applications retain deliberate model choices and additional profiles; the instruction-pack upgrade remains a deliberate three-way review. No runtime changelog or migration entry was needed.

## Commands used / verification

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1`: passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: passed. The process-local execution-policy option was needed for unsigned scripts in Windows PowerShell; no persisted setting was changed.
- `artifacts/assistant_model_profiles_20260905/Verify-ProfileCatalog.ps1`: twenty-four fixture cases through the real validator in both shells, forty-eight checks passed. Controls covered the baseline, future model values including a literal string with a comment, an extra application profile, missing profile, duplicate name, reviewer write sandbox, empty model, missing canonical role, missing review route, missing manifest entry, duplicate manifest path, and twelve instruction-string controls. Those twelve paired empty, whitespace/escaped-whitespace, numeric, unterminated, invalid-escape, and continuation-only negatives with adapted basic/literal/multiline, closing-quote, and escaped-backslash positives. Python `tomllib` independently confirmed their expected classifications.
- Python standard-library `tomllib.load` parsed all sixteen shipped profiles and checked required nonempty values. The six original profile contents matched the captured pre-edit contents.
- `git diff --check`: passed before independent review and again after the review fix. `Normalize-TextFiles.ps1 -Check` verified strict UTF-8 without BOM and CRLF for all nineteen task files, including the summary; the instruction validator also passed after the summary/index update.
- `dotnet run --project artifacts/assistant_model_profiles_20260905/implementation/ProfileSmoke.csproj --configuration Release`: the prewritten checks first rejected the unimplemented stub, then Astra Medium's first implementation passed all nine behavior cases without rework. No NuGet package sources or repository application/database configuration were used.

### Behavioral calibration and runtime limits

The current desktop task advertises only the original named profiles. Two fresh CLI metadata inspections, including one with the installed `multi_agent_v2` switch enabled for that invocation, exposed no profile selector. Named-profile discovery/loading is therefore unverified in this runtime; TOML parsing is not a substitute for that integration check.

The initial CLI smoke attempt also could not execute a read-only shell command under its effective policy. Automatic approval review rejected a broader retry because its sandbox scope was not explicit. A safer metadata-only retry used an explicit read-only sandbox, no tool calls, and no child agents. No persisted settings or permission bypass was introduced.

Three fresh agents used explicit Astra model/effort overrides as a disclosed fallback. The primary consumed their outputs while validating the catalog:

- Astra Medium traced real `return_url` behavior and existing positive/negative controls. It correctly distinguished controller validation through `Utils.isAppUrl` from `FW.redirect`, which prefixes `ROOT_URL` but does not validate destination safety. Source anchors: `FwController.cs:104`, `Utils.cs:207`, `FW.cs:1120`; existing test anchors: `SecurityQuickFixTests.cs:92` and `:313`. This was inspection, not an application test run.
- Astra Medium implemented only the ignored selection-ID helper and passed the nine prewritten checks. Ownership was exclusive; generated output stayed under the fixture's `bin` and `obj`.
- Astra High independently reviewed a prepared before/after fixture. It found both seeded defects (lost owner authorization and lost POST/CSRF enforcement) and accepted the preserved-valid preview action. The fixture findings are intentional test data, not defects introduced into application source.

These checks establish bounded behavior with explicit settings, not named-profile loading, comparative quality, or subscription cost. Per-stage native-agent elapsed time and approval-wait telemetry were unavailable; no durations were estimated. The C# smoke implementation had zero rework. The profile-catalog implementation required one accepted review fix; its initial correctness is recorded separately from the prepared review fixture results. One fixture-harness adjustment supplied process-local Windows PowerShell execution policy after an unsigned-script rejection.

## Independent review

The shared agent-workflow trigger selected a fresh `reviewer_high` (Sol High) with no inherited implementation conversation and the agent-workflow overlay. This available ordinary-review profile was appropriate for the bounded risk. The initial handoff excluded worker reports, active summaries, implementation rationale, and known or suspected findings. Initial verdict: Changes required. The reviewer found that empty non-reviewer developer instructions could pass the validator; the integrator accepted this Medium finding. The old validator reproduced the false acceptance with exit 0. The fix rejects empty/whitespace/malformed instruction strings and passes all forty-eight fixture checks; the reviewer returned no supplemental summary-audit candidates before the fix was submitted. Final independent source re-review and audit of the updated evidence returned No blocking findings, with no supplemental summary candidates. The integrator accepted that verdict. Review loop can stop.

## Testing instructions / residual scope

Run the two instruction-validator commands above from the repository root. For named-profile integration, use a supported fresh Codex session that actually advertises the custom role selector and verify role availability before spawning. Do not infer availability from files or silently substitute a required model.

No full application build/test suite, database/provider tests, UI tests, or performance matrix was run because runtime application behavior did not change. Task-owned fixtures and raw logs are retained under ignored `artifacts/assistant_model_profiles_20260905/`; no shared resources were changed. Preexisting unrelated untracked work was preserved.

## Reflection

The bounded behavior checks supplied useful correctness evidence without ranking models. CLI capability and execution-policy differences accounted for the extra attempts. Keep future calibration focused on an advertised runtime capability first, and record a configuration fallback separately from profile-loading success.
