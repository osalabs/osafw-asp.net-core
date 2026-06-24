# App Test Bootstrap Cleanup Prompt

Use this when starting or upgrading a downstream app that was created from the framework repository and still contains framework-level tests.

Goal: keep framework tests in the framework repo, remove inherited framework implementation coverage from the app repo, and leave the app with a small smoke suite plus app-specific tests.

## Inputs

- `<FRAMEWORK_REPO_PATH>`: path to the current framework repository.
- `<APP_REPO_PATH>`: path to the downstream app repository.
- `<APP_SPECIFIC_MARKER>`: marker for retained app customizations, for example `APP SPECIFIC`, `CLIENT SPECIFIC`, or a project code.

## Work

1. Follow the app repo instructions, line-ending policy, task-summary rules, and review workflow.
2. Inventory the app test project and classify tests as app-specific behavior, local framework customization, smoke coverage for wiring/config/upgrades, or inherited upstream framework implementation coverage.
3. Remove inherited upstream framework implementation tests from the app repo unless the app has intentionally forked that framework behavior.
4. Keep or add a small smoke suite for app startup/config loading, database connection or migration readiness, login/access-control basics, one representative CRUD screen, and one app-specific high-risk workflow.
5. Preserve tests for app-specific overrides, custom controllers, custom models, custom templates, schema customizations, integrations, and business rules.
6. If an inherited framework test is kept because the app changed the framework behavior, rename it around the app contract and add a short comment naming the customization.
7. Do not weaken failing app tests by deleting them as "framework tests" unless the same behavior is verified in the framework repo and the app has no local customization.

## Verification

- Run the app test project after removals.
- Run the smallest app smoke checks that cover local config, database, login, and one core app flow.
- If a framework upgrade is also in progress, run the upgrade verification from `fw_upgrade.md` after this cleanup.

## Closeout

Summarize removed framework-only test areas, retained app-specific tests, smoke coverage left behind, and any framework behavior intentionally forked by the app.
