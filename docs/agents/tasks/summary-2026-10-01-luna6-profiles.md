# Replace Remaining GPT-5.6 Agent Profiles

## Outcome

Applied the user-approved replacements: `discovery_fast` now uses GPT-6 Luna Medium; `implementation_fast` uses GPT-6 Luna High; `implementation_max`, `architect_max`, and `reviewer_max` use GPT-6.1 Sol Max. Profile names, descriptions, sandbox modes, and developer instructions are unchanged. Sol High remains the starting recommendation, standard implementation, and ordinary review; Astra remains preferred for qualifying difficult work and deeper reviews.

Updated the dated model-selection advice and instruction pack to 1.10.0 without changing its managed-file inventory. The advice distinguishes the new helper settings from measured cross-model equivalence and preserves the 2026-09-30 date on earlier benchmark data. Source links in that note support the approved routing. Active shared instructions and profiles contain no GPT-5.6 model selections; historical summaries retain the settings actually used.

No application/runtime, provider/schema, generated-output, public API, user-settings, or primary-model pin changed. No application migration or runtime changelog entry is needed. The user authorized implementation and push; unrelated untracked work is preserved.

## Verification and calibration

- `pwsh -NoProfile -File docs/agents/tools/Test-AgentInstructions.ps1` and `powershell -NoProfile -ExecutionPolicy Bypass -File docs/agents/tools/Test-AgentInstructions.ps1`: all 14 checks passed in each shell. Execution-policy scope is process-local.
- `git diff --check`: passed. The independent reviewer also verified strict UTF-8 without BOM and CRLF on the seven policy files.
- Scoped `rg` over `.codex/agents` and `Search-Repo.ps1` over the active instruction routes found no GPT-5.6 matches; historical records were excluded deliberately.
- The current session still advertises the old model/effort settings for the changed named profiles. Fresh default-role agents used explicit GPT-6 Luna Medium and High as disclosed behavior-check fallbacks. This does not prove refreshed named-profile loading. The new Sol Max files were validated statically; no Max behavior run or model-by-effort matrix was performed.

Luna Medium completed a read-only real-code trace of CRUD `return_url`: request validation in `FwController.init` at lines 100-109, URL policy in `Utils.isAppUrl` at 207-220, navigation in `afterSaveLocation`/`afterSave` at 1221-1352, and `FW.redirect` at 1205-1215. It correctly separated controller validation from the redirect helper and explicit-location caller responsibility. It distinguished shared helper tests and login `gourl` tests in `SecurityQuickFixTests.cs` from CRUD flow coverage, noting that its search found no dedicated CRUD request-init/return_url test. The primary consumed this evidence without repeating the trace; no application tests ran.

Luna High returned a complete implementation for an isolated paging helper without editing files or running tools that mutate. The primary saved that source unchanged and ran the prewritten 15-case harness under ignored `artifacts/assistant_luna6_20261001/implementation/`, copied from the earlier calibration harness with a new unimplemented stub. Baseline command `dotnet run --project artifacts/assistant_luna6_20261001/implementation/ModelProfileSmoke.csproj --configuration Release` failed 0/15 as expected. After source integration, the same command with `--no-restore` passed 15/15 on the first attempt with no compiler warnings or rework. Cases cover defaults, caps, nonpositive requests, empty/exact division, invalid configuration, and int.MaxValue arithmetic. The fixture clears NuGet feeds, has no external package dependencies, and uses no application/database configuration.

This implementation check establishes bounded code-generation behavior with primary-owned execution, not autonomous editor/build-tool behavior or named-profile loading. The ordinary Sol High reviewer configuration is unchanged; its earlier seeded-defect calibration was not repeated and is not evidence for Sol Max. These observations do not establish comparative quality, task speed, or subscription savings. Per-agent elapsed time and separated platform waits were unavailable, so no timings were estimated. No full application/provider/UI suite ran because application behavior is unchanged. The small task-owned fixture and build output are retained ignored.

## Independent review

Shared agent-workflow changes triggered a fresh `reviewer_high` using Sol 6.1 High and the agent-workflow overlay. Its initial packet excluded active summaries, worker reports, artifacts, implementation narrative, and known/suspected findings. Initial verdict: No blocking findings. The subsequent active-summary audit returned no supplemental findings. Final independent verdict: No blocking findings; the primary accepted the verdict. Review loop can stop.
