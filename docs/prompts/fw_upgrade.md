# Framework Upgrade Prompt

The latest framework source is in `<FRAMEWORK_REPO_PATH>`. This application repository was created from an older copy of that framework and needs a compatibility-aware upgrade.

Do a three-way framework upgrade, not a blind overwrite. Preserve the target application's behavior and intentional customizations unless the task explicitly changes them.

## Inputs

- `<FRAMEWORK_REPO_PATH>`: clean/current framework checkout, for example `C:\projects\osafw-asp.net-core`.
- `<APP_SPECIFIC_MARKER>`: existing marker for retained customizations, for example `APP SPECIFIC`, `CLIENT SPECIFIC`, or `<PROJECT_CODE> SPECIFIC`.
- `<APP_LOG_PATH>`: optional application log path, for example `osafw-app\App_Data\logs\main.log`.
- `<DATABASE_PROVIDER>`: optional known provider. Discover it from value-free configuration/project context when omitted; never print connection secrets.

## Permissions and preparation

1. Follow the target application's `AGENTS.md`, ignored local instructions, branch policy, line-ending rules, summary requirements, and review routing. Do not assume the upstream framework's `master` protection or release workflow applies to this app.
2. Inspect status in both repositories and preserve unrelated/untracked work. Do not modify the framework source checkout.
3. This prompt authorizes scoped application-file changes and verification. It does not authorize branch/commit/push/PR/release/deploy actions or database/external writes. Obtain explicit approval before applying updates to a configured/shared database.

## Merge work

1. Identify the best old upstream baseline for this application using commit/version evidence plus file/hash/content comparison. Record uncertainty rather than forcing a false exact baseline.
2. Review upstream `docs/CHANGELOG.md` entries after that baseline before merging. Inventory public API, route/template/include, schema/update, config/compile-symbol/package, storage, security/default, generated output, and frontend changes requiring app adaptation.
3. Inventory target-app divergence, including business code, templates, schema/update history, config shape, deployment scripts, tests, docs, and retained framework customizations/markers.
4. Use the upstream baseline-to-latest delta as the primary merge guide, plus a targeted latest-framework-vs-app comparison to catch added or previously omitted files.
5. Direct-copy only a file that still matches the baseline and has no relevant application change. Hand-merge diverged files and preserve still-required app behavior. Use `<APP_SPECIFIC_MARKER>` only where a marker genuinely helps future upgrades; do not add comment noise everywhere.
6. Add required new framework files, templates, scripts, project/package references, configuration keys/default contracts, and generated/scaffolding changes. Merge application agent instructions rather than overwriting repository-specific policy, and never copy ignored local instructions or private values from either checkout.

## Database upgrades

- Determine the provider actually used by the application. SQL Server is the production-primary/common real-application path. SQLite and MySQL are optional and should be upgraded only when the target app uses them; do not infer MySQL schema parity.
- Bring forward only additive update scripts newer than the detected baseline plus any required fresh-schema changes for future new databases. Fresh schema scripts are destructive and must never be run against an existing database.
- Preserve the framework's provider-specific discovery/override rules from `docs/db.md`. Existing databases apply new update scripts sequentially through the application's normal `FwUpdates` path; do not replay already-recorded successful updates or manually substitute another provider's SQL.
- When database execution is explicitly approved, record the exact server/provider/database, preconditions, ordered scripts/result, and cleanup/rollback limits without exposing credentials. Prefer a disposable clone/database for upgrade rehearsal.

## Verification

- Build with the target application's actual symbols/packages and run focused tests for merged flows. Expand to its solution/full suite only when risk or failures justify it.
- Check public routes/actions, important templates/page-state, startup/config, authentication/security defaults, schema update discovery, generated modules, and preserved app-specific behavior affected by the upgrade.
- If safe runtime startup is available, use a task-owned port/process, smoke-test affected flows, and inspect `<APP_LOG_PATH>` for new errors/warnings. Do not stop user-owned IIS/Visual Studio processes.
- Route review through the target repo's adaptive review guidance, emphasizing lost customization, consumer compatibility, schema/provider order, security/default changes, project/package drift, and text hygiene. Fix adjudicated Blocker, High, and Medium findings.

## Closeout

Summarize the baseline and confidence, upstream range, relevant changelog items handled, important retained/reimplemented customizations, database scripts brought forward or applied, verification results, material smoke/provider checks not completed, residual risks, and any human deployment/migration steps.
