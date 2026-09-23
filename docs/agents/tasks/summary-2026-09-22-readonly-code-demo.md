# Read-only-on-edit Code demos

Both DemosDynamic and DemosVue now demonstrate `is_edit_readonly` using the existing Code (`icode`) field. Creation retains an editable input; existing records show a read-only value. Both states display "Set when creating the record; read-only when editing."

The demo exposed missing Dynamic presentation support, so prepared form definitions now select a read-only rendering branch for existing records. That branch escapes values, masks passwords, and retains help text. Vue also retains help text in its existing read-only branch. Standard-save filtering is unchanged: later submitted replacements are ignored, while direct model writes and custom actions remain outside this metadata's enforcement. The distinction and demo entry points remain documented in `docs/dynamic.md`.

No database column, migration, CSS change, or breaking framework contract was introduced; no upgrade changelog entry is needed. The preceding validation changes were committed and pushed as `6bd599db` before this work.

## Verification

- `dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute-repository-root>/artifacts/assistant_vue297/build/default/ --filter 'FullyQualifiedName~ReadonlyOnEdit|FullyQualifiedName~FwDynamicControllerTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=readonly-code.trx' --results-directory artifacts/assistant_vue297/test-results/readonly-code`: 8 passed, 0 failed. `PLAYWRIGHT_BROWSERS_PATH` pointed to the task's restored browser directory. Coverage includes controller save filtering, child-row creation/editing, actual Dynamic template rendering with escaping/password masking, ordinary editable controls, and Vue creation/editing with visible help text.
- `dotnet build osafw-app/osafw-app.csproj --no-restore`: passed with zero warnings and errors.
- `dotnet test osafw-tests/osafw-tests.csproj --no-restore -p:OutDir=<absolute-repository-root>/artifacts/assistant_vue297/build/default/ --filter 'FullyQualifiedName~VueInteractionBrowserTests' --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=readonly-code-browser.trx' --results-directory artifacts/assistant_vue297/test-results/readonly-code-browser`: 57 passed, 0 failed, using the same browser directory. This covers the shared fixture's switch from a help-component stub to the production component.
- Live Chrome: existing and Add New forms in both demos showed the expected Code control and help text; existing forms had no Code input, creation inputs were enabled. No records were created or changed.
- IIS Express exited during the local reload. A temporary task-owned server completed the remaining checks and was stopped afterward; no task-owned server or offline marker remains. The app is ready to run again from Visual Studio.
- `git diff --check`: passed. Changed text uses UTF-8 without BOM and CRLF.

Independent `reviewer_high` review with the consumer-contract overlay found no blocking findings; the summary audit found no supplemental issues. An incidental search returned one outcome-only summary line after the initial verdict had been formed; the reviewer disclosed this minor isolation limitation. Review loop can stop. Broader provider and application suites were not rerun for this presentation/configuration change.
