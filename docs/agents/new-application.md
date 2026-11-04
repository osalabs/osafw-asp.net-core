# Prepare a new application

Use when the user explicitly asks to "prepare new application", "init app", "bootstrap", or clearly requests equivalent application setup. An unrelated first prompt does not authorize conversion, database initialization, or cleanup. For an existing app's Settings upgrade, use the [migration guide](migrations/settings.md).

## Establish the application

Inspect `git status`, the nearest project and configuration, current repository role and remotes, application customizations, and local instructions. A framework remote, unchanged project name, sample database, branch name, or default config is an indicator, never conclusive proof of a fresh app. Preserve unrelated/untracked work and establish the intended application scope from the user's request and current files.

Collect missing decisions in one compact intake, reusing answers already available:

- Site/project name and purpose; intended users and access roles.
- Domains and development/staging/production environments.
- Database provider, target database, and named connections; whether an empty task-owned database may be created/initialized.
- Authentication, MFA/security requirements, locale/timezone, and required integrations such as email or storage.

Resolve choices that materially affect security, data, compatibility, or authority before dependent changes. Continue independent authorized preparation. Do not invent production credentials or overwrite existing application configuration.

## Configure and bootstrap

Keep deployment/bootstrap values in `appsettings*.json`, framework fallbacks in `FwConfig`, and application defaults/policy in `FwHooks.configure`. Use database Site Settings for site-managed operational values; define credentials with `input=90` before writing them through the model. Give the app a stable Data Protection identity. See [Settings](../settings.md) and [deployment](../deploy.md#runtime-configuration-ownership).

For an explicitly authorized empty database, use the established `DevCli` entry path from the repository root:

```powershell
dotnet run --project osafw-app -- database-init
```

Confirm the effective target and host environment before running it. For production, select the environment explicitly and pass `--no-launch-profile` so a development launch profile cannot override it. The command accepts no extra arguments, supports development and production configurations, refuses any existing tables, and has no force mode. Database initialization has no web action; scaffolding remains development-only. It applies provider-specific initialization including Settings and prints the generated administrator password; keep that credential out of tracked files and reports. Enable the matching project/provider symbol when needed. Never substitute destructive fresh-schema scripts for an existing database or run initialization against a configured shared resource without explicit authority.

Once the database is ready, start through the application's established launch path and check `/Dev/Configure`, login, the intended roles, and representative application flows. Verify preserved defaults and a rejected unauthorized access path. Record actual provider/environment coverage and unresolved acceptance checks; do not claim a production deployment from a local bootstrap.

## Optional app-scoped cleanup

Propose the exact unused demo, update, or legacy paths and their consumers before removal. Delete only the explicitly approved application-scoped set after verifying it is unused. Do not automatically delete `App_Data/sql/updates` on clone, strip framework source, remove migrations on an unrelated prompt, or discard copied-app customizations. Keep required schema/update history, templates, role/menu links, and provider variants until their consumers and upgrade needs are resolved. No repository-name detection engine or automatic cleanup hook is needed.