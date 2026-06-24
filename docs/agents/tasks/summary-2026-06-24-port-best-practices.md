## What changed

- Added lower-noise defaults to `docs/agents/tools/Search-Repo.ps1` for `docs/agents/tmp/**`, `test-results/**`, and `osafw-app/App_Data/logs/**`.
- Added generalized `docs/prompts/orchestrator.md` and linked it from `docs/prompts/README.md` and `docs/README.md`.
- Added reusable code-reviewer guidance for using `Search-Repo.ps1` and keeping prompt/instruction changes bounded and optional where appropriate.
- Added `plaintext_json` read-only display support for classic Dynamic and Vue controllers:
  - Dynamic Show/ShowForm fields pretty-print valid JSON server-side and leave invalid text unchanged.
  - Classic templates include `/common/form/show/plaintext_json.html` and selector wiring.
  - Vue form controls include a matching `plaintext_json` renderer.
  - Dev controller codegen uses `plaintext_json` for long text fields ending in `_json` and generates monospace textarea editing.
- Added focused regression/template tests for Dynamic formatting, codegen, classic escaping/selector wiring, and Vue template contract.

## Scope reviewed

- `docs/agents/local_instructions.md`
- `docs/README.md`
- `docs/agents/tasks/index.md`
- `docs/agents/tools/Search-Repo.ps1`
- `docs/agents/code_reviewer.md`
- `docs/prompts/README.md`
- `docs/dynamic.md`
- `osafw-app/App_Code/fw/FwDynamicController.cs`
- `osafw-app/App_Code/fw/FwVueController.cs`
- `osafw-app/App_Code/models/Dev/CodeGen.cs`
- `osafw-app/App_Data/template/common/form/show/*`
- `osafw-app/App_Data/template/common/vue/form-one-control.html`
- `osafw-tests/App_Code/fw/FwDynamicControllerTests.cs`
- `osafw-tests/App_Code/fw/DevCodeGenTests.cs`
- `osafw-tests/App_Code/security/SecurityStoredRenderingTests.cs`
- `docs/prompts/README.md`
- `docs/prompts/orchestrator.md`

## Commands used / verification

- `dotnet test osafw-tests\osafw-tests.csproj --filter "FwDynamicControllerTests|DevCodeGenTests|SecurityStoredRenderingTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_port_best_practices_tests\` - passed, 28/28.
- Follow-up cleanup: `dotnet test osafw-tests\osafw-tests.csproj --filter FwDynamicControllerTests -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_port_best_practices_tests\` - passed, 3/3.
- `git -c core.quotepath=false diff --check` - passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check <touched files>` - passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Search-Repo.ps1 -Pattern plaintext_json -Path docs osafw-app\App_Code osafw-app\App_Data\template\common osafw-tests\App_Code` - passed and returned expected source/docs/tests.
- Checked touched shared files and source paths for external app identifiers - no matches.
- Self-review using `docs/agents/code_reviewer.md` - no issues found worth another loop.

## Decisions - why

- Kept the orchestrator prompt optional instead of changing `AGENTS.md`, because the default workflow should stay autonomous for small tasks.
- Implemented `plaintext_json` formatting in `FwDynamicController` so classic Dynamic Show and ShowForm screens get the same behavior as Vue.
- Left invalid JSON unchanged so existing stored diagnostic text or partially-written payloads still render instead of disappearing behind an error state.
- Escaped classic `plaintext_json` output through normal ParsePage rendering and used Vue mustache rendering, avoiding raw HTML exposure.
- Updated Dev codegen because `_json` long text fields are a stable naming convention where read-only markdown rendering is the wrong default.
- No `docs/CHANGELOG.md` entry was added because the field type and codegen behavior are additive and do not break public upgrade contracts.
- No `AGENTS.md`/Copilot sync was needed because `AGENTS.md` did not change.

## Pitfalls - fixes

- Initial broad source search included generated build outputs; reran targeted searches and verified `Search-Repo.ps1` directly.
- `apply_patch` wrote LF line endings; normalized all touched files with `Normalize-TextFiles.ps1` and verified CRLF/UTF-8 without BOM.
- Existing unrelated untracked files and a concurrent reviewer-doc simplicity edit were left intact.
- Follow-up cleanup inlined the `plaintext_json` serializer options at the single call site.

## Risks / follow-ups

- Browser-level Vue rendering was not smoke-tested; coverage is via template contract assertions and focused .NET build/tests.
- `plaintext_json` pretty printing handles valid JSON values and invalid text, but does not add editor validation for JSON textareas.
- The search helper exclusion behavior was syntax-checked by running the helper, not by constructing ignored fixture files.

## Heuristics (keep terse)

No stable heuristics, domain facts, glossary entries, or ADRs added.

## Testing instructions

Focused tests:

`dotnet test osafw-tests\osafw-tests.csproj --filter "FwDynamicControllerTests|DevCodeGenTests|SecurityStoredRenderingTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_port_best_practices_tests\`

## Reflection

Reading the downstream implementation first was useful, but broad `rg` searches were noisy until generated outputs were excluded. Future agents should use `Search-Repo.ps1` sooner for cross-repo-style comparison once the scope is known, and should treat optional orchestration as a prompt-level workflow rather than a default `AGENTS.md` rule. No sub-agent was needed; the diff was small enough for direct integration plus self-review.
