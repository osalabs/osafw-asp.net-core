## What changed
- Replaced the standalone scheduled deploy samples with `scripts/deploy_core.ps1` and tiny repo-contained profile scripts for production, develop, and staging.
- Kept `scripts/deploy_sample.bat` unchanged for legacy compatibility.
- Updated `scripts/setup_deploy_scheduled_task.ps1` to infer the develop scheduled profile, preview defaults with `-Check`, and refuse non-scheduled profiles unless explicitly overridden.
- Added `osafw-app/wwwroot/offline.htm` for `app_offline.htm` deployments.
- Updated the Dev Manage installation doc to reference .NET 10, repo-contained PowerShell deploy profiles, and the Task Scheduler setup helper.
- Added `docs/deploy.md` as a concise admin runbook for production, staging, and develop deployments.
- Linked `docs/deploy.md` from `docs/README.md` and the Dev Manage installation doc.
- Updated deploy notifications to default to no webhook, use app-specific webhook variables in scheduled profiles, and avoid absolute paths in posted payloads.
- Added a domain note that `FwConfig` derives `site_root` from the parent before `bin` when hosted from a publish path.
- Added narrow ignore rules for deploy status/state/lock files under `osafw-app\App_Data\logs`.

## Scope reviewed
- Existing `scripts/deploy_sample.bat`.
- Earlier standalone scheduled deploy sample implementation.
- `osafw-app/osafw-app.csproj` target framework and publish shape.
- `FwConfig` `site_root` derivation.
- Dev Manage installation docs.
- Documentation map and new deploy runbook.
- Working app deploy scripts for reusable behavior only; no app-specific path or site name was recorded in repo docs.
- Git tracking for `osafw-app/App_Data/db`, `osafw-app/App_Data/logs`, and `osafw-app/upload`.

## Commands used / verification
- `[scriptblock]::Create((Get-Content -Raw -LiteralPath ...))` parse checks passed for `deploy_core.ps1`, all deploy profiles, and `setup_deploy_scheduled_task.ps1`.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_production.ps1 -Help` - passed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_scheduled_develop.ps1 -Help` - passed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_scheduled_staging.ps1 -Help` - passed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\setup_deploy_scheduled_task.ps1 -Help` - passed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\setup_deploy_scheduled_task.ps1 -Check` - passed and inferred `OSAFW Deploy - Develop`.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\setup_deploy_scheduled_task.ps1 -DeployScript scripts\deploy_production.ps1 -Check` - failed as expected because manual profiles require `-AllowManualScript`.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\setup_deploy_scheduled_task.ps1 -DeployScript scripts\deploy_production.ps1 -AllowManualScript -Check` - passed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_production.ps1 -Check` - passed with write access; resolved `origin/master`, verified write probes, and did not deploy.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check -Path ...` - passed for touched files; all UTF-8 without BOM and CRLF.
- `git diff --check -- osafw-app\.gitignore osafw-app\App_Data\template\dev\manage\docs\tab_installation.md docs\agents\tasks\index.md` - passed.
- `rg -n "[ \t]+$" ...` - no trailing whitespace in touched tracked/untracked text files.
- `git status --short --ignored ...` - confirmed `deploy-production.log`, `deploy-production.status.json`, and `main.log` are ignored.
- `git ls-files -- osafw-app/App_Data/db osafw-app/App_Data/logs osafw-app/upload` - confirmed only `osafw-app/App_Data/logs/.gitkeep` and `osafw-app/upload/.gitkeep` are tracked in those runtime folders.
- Self-review using `docs/agents/code_reviewer.md` completed; fixed profile/core exit-code propagation and normal-deploy write-access preflight during review.
- `Get-Content docs\deploy.md` review - confirmed the runbook is concise and admin-facing.
- `rg -n "deploy\.md|deploy_production|deploy_scheduled_develop|deploy_scheduled_staging|setup_deploy" docs\README.md docs\deploy.md osafw-app\App_Data\template\dev\manage\docs\tab_installation.md` - confirmed runbook links and script references.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check -Path docs\deploy.md,docs\README.md,osafw-app\App_Data\template\dev\manage\docs\tab_installation.md,docs\agents\tasks\summary-2026-06-09-deploy-script.md` - passed; all UTF-8 without BOM and CRLF.
- `git diff --check -- docs\deploy.md docs\README.md osafw-app\App_Data\template\dev\manage\docs\tab_installation.md` - passed.
- `rg -n "[ \t]+$" docs\deploy.md docs\README.md osafw-app\App_Data\template\dev\manage\docs\tab_installation.md docs\agents\tasks\summary-2026-06-09-deploy-script.md` - no trailing whitespace.
- `[scriptblock]::Create((Get-Content -Raw -LiteralPath ...))` parse checks passed for updated deploy core and scheduled profiles.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_scheduled_develop.ps1 -Help` - passed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_scheduled_staging.ps1 -Help` - passed.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_production.ps1 -Check` - passed with write access after notification updates.
- Targeted app-specific string search across `scripts`, `docs`, and `osafw-app` - no matches; no app-specific path/site strings or old hardcoded webhook were added.
- `Select-String -Path scripts\deploy_core.ps1 -Pattern 'repoRoot =|appRoot =|targetFolder =|logFile =|statusFile =|NotifyWebhook'` - confirmed webhook default is blank and posted notification paths are sanitized.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs\agents\tools\Normalize-TextFiles.ps1 -Check -Path scripts\deploy_core.ps1,scripts\deploy_scheduled_develop.ps1,scripts\deploy_scheduled_staging.ps1,docs\deploy.md,docs\agents\tasks\summary-2026-06-09-deploy-script.md` - passed; all UTF-8 without BOM and CRLF.
- `git diff --check -- scripts\deploy_core.ps1 scripts\deploy_scheduled_develop.ps1 scripts\deploy_scheduled_staging.ps1 docs\deploy.md` - passed.
- `rg -n "[ \t]+$" scripts\deploy_core.ps1 scripts\deploy_scheduled_develop.ps1 scripts\deploy_scheduled_staging.ps1 docs\deploy.md docs\agents\tasks\summary-2026-06-09-deploy-script.md` - no trailing whitespace.

## Decisions - why
- Moved to repo-contained PowerShell profiles so deploy scripts are maintained through normal commits and server-local edits are avoided.
- Kept legacy `deploy_sample.bat` unchanged rather than turning it into a wrapper.
- Added staging in addition to production/develop because original deployment usage includes live/staging/develop instances.
- Kept `docs/deploy.md` concise and admin-facing instead of duplicating implementation details from `deploy_core.ps1`.
- Notification webhook defaults to blank in core so framework deployments do not post to a hardcoded service; scheduled profiles show where an app-specific endpoint should be set.
- Notification payloads keep `repoRoot` to the repo folder name and send other paths relative to that repo root to avoid leaking absolute server paths.
- Recommended PowerShell as the default mechanism because structured errors, JSON/webhook/status handling, path/date operations, and Scheduled Task registration are less brittle than batch control flow.
- The Task Scheduler helper must be run elevated, but the scheduled deploy account should usually be a dedicated deploy account rather than local Administrator; it only needs Git credentials and filesystem rights when using `app_offline.htm`.
- Used a temporary git worktree so publish can happen before the live repo hard reset, minimizing downtime on small Windows instances.
- Kept `git clean` out of the script because runtime DB, logs, upload files, deploy status, deploy state, and deploy locks are untracked or ignored and must survive hard reset.
- Used `app_offline.htm` for the final reset/copy window instead of stopping the app pool by default.
- State is recorded only after successful copy and `app_offline.htm` removal so failed deployments retry.
- No `docs/CHANGELOG.md` entry was added because this changes deployment tooling/docs, not a breaking framework API, route, schema, config, storage, security default, or frontend contract.

## Pitfalls - fixes
- `git reset --hard` preserves untracked files, but it discards tracked local edits; environment-specific settings should live in committed profile scripts, not server-only edits.
- `robocopy /MIR` can delete the wrong tree if paths are mistyped; the deploy core validates blank/equal dangerous target paths first.
- `robocopy` uses nonstandard success codes; the deploy core treats 0-7 as success and 8+ as failure.
- A bare `robocopy /XD` is invalid when exclude directories are intentionally cleared; the deploy core adds `/XD` only when excludes are configured.
- Dot-sourced core scripts should not be trusted to set process exit codes by themselves; profile scripts now default to failure, catch initialization errors, and explicitly exit with the core result.
- Normal deploys now run the same write-access probes as `-Check` before fetch/build/reset so missing permissions fail before touching the live repo.
- Native command output capture temporarily uses `ErrorActionPreference = Continue` so stderr can be logged and exit codes handled consistently by the deploy core.

## Risks / follow-ups
- Full live deploy was not run locally because it would mutate git worktrees and IIS target folders.
- Server acceptance should run the repo-contained PowerShell profile once with `-Check`, then manually with `-Pause` for production or as a scheduled task for develop/staging, then confirm a no-change scheduled run exits 0.
- Task Scheduler registration was not run locally because it mutates machine scheduler state and prompts for deploy credentials.

## Heuristics (keep terse)
- No reusable heuristic added yet; this is deployment-tooling specific.
- Stable framework fact added to `docs/agents/domain.md`; no ADR or glossary entry needed.

## Testing instructions
- Edit committed profile scripts under `scripts\` rather than editing deploy scripts directly on the server.
- Run `scripts\deploy_scheduled_develop.ps1 -Check` before registering the scheduled task.
- Use `scripts\setup_deploy_scheduled_task.ps1` from an elevated PowerShell prompt to register/update the default 10-minute develop task.
- Run `scripts\deploy_production.ps1 -Pause` manually for production.
- Confirm the first successful run writes `osafw-app\App_Data\logs\deploy-<env>.last_successful_commit`; the next no-change run should exit 0 without publishing.
- Confirm server-local runtime files under `App_Data\db`, `App_Data\logs`, and `upload` remain present after deployment.

## Reflection
The key design constraint was that `FwConfig` treats the app root before `bin` as live runtime state. Future deploy-script work should check runtime path derivation before assuming source-tree changes are invisible to a running IIS app. No sub-agent was needed for implementation; local review against `docs/agents/code_reviewer.md` is enough for this focused script/doc change.
