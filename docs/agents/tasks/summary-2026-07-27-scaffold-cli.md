## What changed

- Added a generic built-in `DevCli` entry point to the existing web executable, with the initial `scaffold` command supporting CRUD, model, controller, and report generation.
- Reused `FW.initOffline`, `DevEntityBuilder`, and `DevCodeGen`; the command exits before the web host and hosted services start.
- Added development-environment enforcement, bounded CLI input validation, non-overwrite defaults, explicit model/controller `--force`, deterministic output, and exit codes.
- Kept API as a recognized option reserved for future support while refusing it before generation, blocked lookup registrations that collide with compiled controller routes, and rejected generated model names that collide with compiled top-level types in the generated `osafw` namespace.
- Documented developer usage and taught repository agents to prefer the command after applying a new table to the intended development database.

## Scope reviewed

- `Program` startup and environment selection.
- Developer Tools model/controller/report actions and their lower-level generators.
- Developer Tools controller-type selection and shared generator fallback behavior.
- Offline framework lifecycle, schema metadata, generated source/template paths, menu-item side effects, and existing code-generator tests.
- Canonical feature-module documentation and mirrored agent workflow instructions.

## Commands used / verification

- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DevCodeGenTests|FullyQualifiedName~DevCliTests" -p:OutDir=...` passed 29 generator/CLI tests.
- `dotnet test osafw-tests\osafw-tests.csproj -p:OutDir=...` passed all 715 tests.
- Executed the built CLI help path and verified it exits successfully without application configuration.
- Executed non-writing CLI probes and observed exit code `1` for the reserved API type, `2` for an unsafe controller URL, and `3` for a configuration without `IS_DEV=true`.
- Ran the repository text-normalization check, `git diff --check`, and SHA-256 comparison of `AGENTS.md` with `.github/copilot-instructions.md`.
- Performed a deliberate local review using `docs/agents/code_reviewer.md`; no blocking findings remained.

## Decisions - why

- Kept the generic `DevCli` entry point inside `osafw-app` so current and future developer commands can call internal framework contracts without a new project, package, public API, or file-based-app wrapper.
- Kept invocation syntax in `docs/feature_modules.md`; agent instructions state when to prefer the CLI and route to that canonical document without duplicating commands.
- Kept HTTP POST/XSS enforcement unchanged; the CLI calls the shared generator layer as local development tooling.
- Kept API visible in both developer entry points, added an explicit future-generator TODO, and return a clear not-yet-available error before side effects until its template and behavior exist.
- Made `scaffold crud <table>` the shortest common path and retained focused commands for incremental workflows.
- Refused existing targets by default because scripted generation is easier to invoke accidentally than the browser forms.

## Risks / follow-ups

- CRUD generation is not transactional across source files, templates, and the development-database menu row; inspect partial output if an underlying generator fails.
- The CLI and `/Dev/Manage` expose dynamic, Vue, lookup, and the reserved API choice; API and unknown controller types fail before filesystem or database side effects.
- A live successful scaffold was not run because it would intentionally create repository files/templates and mutate the configured development database; existing DevCodeGen tests plus the CLI parsing, environment, build, and command-surface checks covered the changed boundaries without disposable schema setup.
- `docs/README.md` already routes feature-module work to `docs/feature_modules.md`, so its navigation did not need a change.
- No changelog entry was required because the changes are limited to development-only scaffolding; API remains a reserved choice and never had an API generation contract.
- No stable domain fact, heuristic, or ADR was added beyond the canonical feature-module and agent workflow guidance.

## Pitfalls - fixes

- The first focused build caught a local variable shadowing the controller-plan helper; renaming the local restored compilation before broader verification.
- Review found silent API-to-Dynamic fallback plus incomplete model and lookup collision checks; an explicit reserved-API guard and focused regression tests now cover those boundaries.
- Follow-up review found the model check crossed namespace boundaries; collision matching now scopes to `osafw`, with `osafw.Parsers.HtmlParser` as the regression case.

## Testing instructions

From the repository root:

```powershell
dotnet run --project osafw-app -- scaffold --help
dotnet test osafw-tests\osafw-tests.csproj
```

For an end-to-end generation smoke, use a disposable table in an `IS_DEV=true` database, run `scaffold crud <table>`, inspect the generated model/controller/templates and menu row, then remove the disposable outputs deliberately.

## Reflection

Reusing the existing offline lifecycle avoided an HTTP/session emulator and kept command startup isolated from the web host. Pure parsing tests provide inexpensive coverage while existing generator tests continue to own detailed generated-output behavior.
