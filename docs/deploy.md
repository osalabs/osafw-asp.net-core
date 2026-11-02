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

## Runtime Configuration Ownership

Keep `appsettings.json` limited to values needed before the application database is available: deployment identity, host trust, named database definitions, storage location, worker switches, and logging/hosting configuration. Site-managed operational values such as the site name, redirects, email configuration, API credentials, MFA policy, and UI defaults belong in the `settings` table and `/Admin/Settings`.

Migrate existing operational values and plaintext credentials before starting the upgraded runtime. There is no special legacy MFA minimum: preserve an enforced MFA policy in the effective Settings value and verify it before reopening traffic. See the [application migration guide](agents/migrations/settings.md).

Application policy that must also work without a database belongs in `FwHooks.configure(FwDict)`. The shipped hook defines route and XSS policy, signup availability, API CORS origin, and the application's default timezone. Keep this hook pure and DB-independent because startup, requests, and offline commands all use it. Framework-owned fallback values remain in `FwConfig`.

ASP.NET Core selects `appsettings.Development.json`, `appsettings.Beta.json`, or another environment file through `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT`. Existing named and host-matched `appSettings.override` configuration remains supported; hostname matching does not select the ASP.NET Core deployment environment. Prefer environment files for development/test/production deployment differences.

Apply the active provider's `upd2026-09-27-settings-foundation.sql` before deploying runtime code that reads Site Settings. Fresh installations receive the same rows from the provider's `settings.sql` during database initialization. Do not rely on web routes to bridge an old Settings schema: finish the controlled database and value migration before starting the upgraded web host.

For a first local clone with an empty development database, run the guarded initializer:

```powershell
dotnet run --project osafw-app -- database-init
```

The command is owned by `DevCli` and dispatched through the common CLI entry path in `Program.cs`. It accepts no other arguments, requires the resolved configuration to have `IS_DEV=true`, refuses a database that already contains any tables, and has no force mode. It applies the existing provider-specific development initialization chain and prints the generated bootstrap administrator password to the console. Treat that output as a credential.

Initialize an empty production database through the application's normal controlled SQL deployment before starting the IIS application pool. Its provider-specific initialization must include the framework/session/key tables and `settings.sql`; `database-init` deliberately cannot initialize a production configuration. Local developers can still start with the configured database unavailable and use Home or `/Dev/Configure`; sessions become usable after initialization. Session-cookie keys tolerate bootstrap storage failures, while stored credentials always require durable keys.

For existing applications, use the [one-time Settings migration instructions](agents/migrations/settings.md) to prepare a reviewed app-specific migration. Select the target environment and connection explicitly, stop traffic during the final migration, and obtain authority before changing a configured database. Keep legacy configuration and rollback material outside tracked files. The runtime includes no legacy-JSON migration CLI or browser action.

### Named Database Connections

Secrets Manager is optional and disabled by default. Enable `isSecretsManager` in `osafw-app.csproj` to include the AWS implementation and `AWSSDK.SecretsManager` package. The ordinary build contains neither that package nor the AWS retrieval/cache implementation; it retains a clear configuration error if an AWS reference is used without the feature. Existing S3/Core references are unchanged.

Every connection is named under `appSettings.db`, including `main` and any application-specific connections. A definition uses either an inline `connection_string` or an external `connection_string_secret`, never both:

```json
{
  "appSettings": {
    "db": {
      "main": {
        "type": "SQL",
        "timezone": "",
        "connection_string": "",
        "connection_string_secret": {
          "provider": "aws-secrets-manager",
          "secret_id": "production/app/sql-main",
          "region": "us-east-1"
        }
      }
    }
  }
}
```

The empty `connection_string` explicitly overrides any inline value inherited from the base configuration. Non-empty inline and external sources together are rejected. The AWS secret string must be the complete, non-empty connection string; JSON fields are not assembled into a connection string. `region` is optional when the AWS SDK can resolve it from the host. Prefer an EC2 instance role; the SDK credential chain is independent of AWS credentials stored in Site Settings, which cannot bootstrap access to their own database.

`FwConfig.getDatabaseSettings(settings, name)` resolves a copy of the named definition for startup sessions, Data Protection and `FW.getDB(name)`. The source configuration and template globals never receive the resolved value. `DB` itself accepts ordinary connection information and knows nothing about external providers. Applications constructing `DB` directly from a secret-backed definition must first use this configuration helper.

Successful secret strings are cached in private process memory by region and secret ID until the IIS application pool restarts. Concurrent cold reads share one fetch; failures are not cached and a later read can retry. The cache is independent of general FwCache clearing and does not add a refresh timer, distributed cache or caching package. Restart after changing a secret or its identity/region configuration. If a required external secret cannot be resolved, startup or the requesting operation fails with a value-free error; it never silently substitutes another credential source.

### Other External Secrets

Existing OAuth/API/SMTP credentials stay in Site Settings. A copied application can explicitly opt into the same helper for another secret:

```csharp
string value = FwSecretsManager.read(new FwDict
{
    ["provider"] = "aws-secrets-manager",
    ["secret_id"] = "production/app/custom-integration",
    ["region"] = "us-east-1",
});
```

The helper returns the secret string unchanged, including JSON or an empty string; the consumer owns format validation. Binary secrets are unsupported. Do not log returned values or put them in template globals. The public reference shape is provider-neutral: adding Azure Key Vault later requires its optional implementation and reference options, but callers using this entry point or the database configuration helper need not change. Azure is not implemented by this PR. There is no automatic external fallback for Settings reads.

AWS documents [SDK retrieval and caching](https://docs.aws.amazon.com/secretsmanager/latest/userguide/retrieving-secrets-net-sdk.html) and the [credential chain](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-assign.html).

### Data Protection Identity

Set `appSettings.DATA_PROTECTION_APPLICATION_NAME` explicitly and keep it stable across deployments and server moves. Existing applications must set it to their previous effective `SITE_NAME` before upgrading; the framework sample uses `Site Name`. Changing this value isolates the application from its existing Data Protection keys and makes protected Settings credentials unreadable. Branding changes should update the Site Settings `SITE_NAME` row, not this identifier.

Before moving an installation to another Windows server, a Site Admin can export all Site Settings from `/Admin/Settings`. The JSON contains decrypted credentials, so store and transfer it as a secret. Import it on the new server after its schema and durable Data Protection keys are initialized; the import validates the target definitions and encrypts credentials with the new server's keys.

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

## Developer tool authorization and diagnostics

The public configuration page is diagnostic only. Fresh local development setup uses `database-init`; the retained web `InitDB` action requires a tokened POST and a Site Admin session. Both entry points reject any database that already contains tables. There is no destructive reset/force mode and no public bootstrap password response.

The SQL console requires an active Site Admin account, a tokened POST, a writable account, and that account's current password for every SQL execution. The former shared source password and session unlock flag no longer grant access. Table-result links only prefill the form; execution still requires a new password-confirmed POST. Windows-only administrators must establish an application password through an authorized account-management flow before using this console.

Assistant DEBUG diagnostics redact tool arguments, result summaries, and retrieval queries when `log_pii=false`. Explicitly enabling PII diagnostics permits sensitive content in logs; retain the application's existing access/retention controls. The developer log viewer escapes file content as text.
