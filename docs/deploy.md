# Deployment

This guide is for Windows/IIS deployments of real apps based on this framework.
Deploy scripts live in the repo under `scripts\`; do not copy them to `C:\inetpub`.

## Prerequisites

- Server has Git, PowerShell 5.1+, Robocopy, and the .NET 10 SDK in `PATH`.
- The app repo is checked out on the server, for example `C:\inetpub\site.domain.com`.
- IIS points to the publish folder: `osafw-app\bin\Release\net10.0\publish`.
- The deploy account has Git credentials and Modify rights to the repo, temp folder, publish target, and `osafw-app\App_Data\logs`.
- Use a dedicated deploy account when possible. Use `SYSTEM` only if Git credentials are configured for LocalSystem.

## IIS Header Hardening

The framework publishes `osafw-app\web.config` with an IIS baseline that suppresses the IIS `Server` header through `requestFiltering removeServerHeader="true"` and removes the default `X-Powered-By` response header. If IIS configuration sections are locked, or the server is older than IIS 10 on Windows Server/Windows 10 version 1709, apply equivalent hardening at the IIS server or site level during instance setup.

## Configure Profiles

Edit and commit the profile scripts for the real app:

- `scripts\deploy_production.ps1` - manual production deploy; defaults to branch `master`.
- `scripts\deploy_scheduled_staging.ps1` - scheduled staging deploy; defaults to branch `staging`.
- `scripts\deploy_scheduled_develop.ps1` - scheduled develop deploy; defaults to branch `develop`.

Usually only these values need app-specific changes:

```powershell
$EnvironmentName = "Production"
$GitBranch = "master"
$DeployName = "deploy-production"
$NotifyWebhook = ""
```

`scripts\deploy_core.ps1` auto-detects repo root, project file, publish target, log folder, status file, state file, and lock folder.
Set `$NotifyWebhook` to the app-specific deploy notification endpoint when notifications are wanted. Webhook payloads use the repo folder name and repo-relative paths, not absolute server paths.

## Validate

Run `-Check` before enabling a task or deploying manually:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_production.ps1 -Check
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_scheduled_staging.ps1 -Check
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_scheduled_develop.ps1 -Check
```

`-Check` validates tools, paths, branch resolution, and write access. It does not publish, reset, copy, or deploy.

## Manual Production Deploy

Run production manually:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_production.ps1 -Pause
```

Use `-Force` only when you need to redeploy the same commit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\deploy_production.ps1 -Force -Pause
```

## Scheduled Staging/Develop

Preview the inferred task before registering it:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup_deploy_scheduled_task.ps1 -Check
```

Register or update the develop task from an elevated PowerShell prompt:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup_deploy_scheduled_task.ps1 -RunAsUser "DOMAIN\deploy-user"
```

Register or update staging explicitly:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup_deploy_scheduled_task.ps1 -DeployScript scripts\deploy_scheduled_staging.ps1 -TaskName "OSAFW Deploy - Staging" -RunAsUser "DOMAIN\deploy-user"
```

Defaults:

- Runs every 10 minutes; change with `-EveryMinutes`.
- Uses `MultipleInstances IgnoreNew`.
- Refuses non-`deploy_scheduled_*.ps1` scripts unless `-AllowManualScript` is passed.
- Updates an existing task with the same name.

## Logs and Status

Deploy runtime files are written under `osafw-app\App_Data\logs`:

- `deploy-<env>.log`
- `deploy-<env>.1.log`
- `deploy-<env>.status.json`
- `deploy-<env>.last_successful_commit`
- `deploy-<env>.lock`

The status JSON is the quickest place to check the last result, branch, target commit, and error message.

## Behavior and Safety

- Fetches the fixed branch from `origin`.
- Skips deploy when the resolved commit matches the last successful commit, unless `-Force` is used.
- Builds in a temporary git worktree before touching the live repo.
- Publishes with `dotnet publish --configuration Release /p:EnvironmentName=<env>`.
- Uses `app_offline.htm` during the final reset/copy window.
- Resets the live repo with `git reset --hard <targetCommit>`.
- Copies published files with Robocopy and treats exit codes `0` through `7` as success.
- Writes last-successful commit only after copy succeeds and `app_offline.htm` is removed.
- Does not run `git clean`; untracked runtime files in `App_Data\db`, `App_Data\logs`, and `upload` are preserved.

Do not make server-local edits to tracked files. `git reset --hard` discards tracked local edits by design.

## Troubleshooting

- **Task does not run:** run setup with `-Check`, then check Task Scheduler history and deploy account credentials.
- **Git auth fails:** configure Git credentials for the scheduled task account. If using `-UseSystem`, configure credentials for LocalSystem.
- **No deploy happens:** check `deploy-<env>.status.json`, `deploy-<env>.last_successful_commit`, and the configured `$GitBranch`.
- **App stays offline:** inspect `deploy-<env>.log`; a failure after copy starts intentionally leaves `app_offline.htm` in place so the next scheduled run can retry safely.
- **Deploy script changed:** the server uses the updated script on the next run after the repo is reset to the commit containing that change.
