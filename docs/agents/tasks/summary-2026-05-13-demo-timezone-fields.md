## What changed

- Added `demos` timezone sample columns: `fdatetime_utc`, `fdatetime_offset`, and `fdatetime_local` in SQL Server demo schema, MySQL compatibility schema, and an additive SQL Server update script.
- Added framework support for browser-native `datetime-local` values in `DateUtils`, `FwModel.convertUserInput`, ParsePage `date="datetime-local"`, Dynamic form templates, and Vue form/editable-list controls.
- Added explicit ISO-offset string parsing in `FwModel.convertUserInput` so Vue-stored `datetime_popup` values that already include offsets survive unchanged saves.
- Wired the static, Dynamic, and Vue demo screens/configs to show and save normal datetime, `_utc` datetime, `datetimeoffset`, and browser `datetime-local` examples.
- Updated datetime, Dynamic, template, and agent domain docs.
- Added targeted DateUtils/FwModel/ParsePage/DB tests for datetime-local and demo timezone fields.

## Scope reviewed

- Reviewed local instructions, docs map, demo schema, demo model/controller, static templates, Dynamic config, Vue config, common form/Vue date controls, DB timezone tests, and relevant docs.

## Commands used / verification

- `Get-Content docs\agents\local_instructions.md`
- `Get-Content docs\README.md`
- `rg -n "CREATE TABLE.*demos|\bdemos\b|AdminDemos|AdminDemosDynamic|AdminDemosVue|datetime-local|date_popup|datetime_popup" ...`
- `sqlcmd -S "(local)" -d demo -E -C -i osafw-app\App_Data\sql\updates\upd2026-05-13-demo-timezone-fields.sql`
- `sqlcmd -S "(local)" -d demo -E -C -Q "SELECT ... FROM sys.columns ..."`
- `dotnet build osafw-app\osafw-app.csproj -p:OutDir=...\artifacts\assistant_build_demo_tz\`
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DateUtilsTests|FullyQualifiedName~FwModelDateTimeInputTests|FullyQualifiedName~ParsePageTests|FullyQualifiedName~DBTests" --artifacts-path artifacts\assistant_test_demo_tz_targeted2`
- `dotnet test --artifacts-path artifacts\assistant_test_demo_tz_full`
- `dotnet build osafw-app\osafw-app.csproj`
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DateUtilsTests|FullyQualifiedName~FwModelDateTimeInputTests|FullyQualifiedName~ParsePageTests|FullyQualifiedName~DBTests" --artifacts-path artifacts\assistant_test_demo_tz_final_targeted2`
- `dotnet test --artifacts-path artifacts\assistant_test_demo_tz_full2`
- `git diff --check`
- CRLF byte scan over every touched/created task file: `CRLF OK`
- Playwright MCP against `https://localhost:44315/`: logged in, edited the disposable demo row through static, Dynamic, and Vue forms, reloaded values, and verified Vue quick edit uses `input[type=datetime-local]`.
- Playwright MCP final smoke after rebuild: `/Admin/DemosVue` loads and shows the `Browser local` list column.
- SQL Server raw value check after UI saves: normal/local fields stored DB-local wall time, `_utc` stored UTC, `datetimeoffset` stored offset-aware UTC instant.

## Decisions - why

- Use SQL Server as authoritative `datetimeoffset` demo.
- Add MySQL-compatible `DATETIME` columns so shared demo configs do not break when MySQL demo schema is used.
- Add `datetime_local` instead of overloading `datetime_popup`; browser-native input has a different wire format and needs explicit parser support.
- Leave `_utc`/`datetimeoffset` Vue showform fields as text datetime controls; only `fdatetime_local` should use browser-native `datetime-local`.

## Pitfalls - fixes

- First SQL update script failed under `sqlcmd` because `ALTER TABLE` and `UPDATE` referencing new columns were compiled in one batch; fixed with dynamic SQL.
- Initial `FwModel.convertUserInput` ternary promoted `DateTime` to `DateTimeOffset`; fixed with explicit branch assignment.
- VS/IIS Express locked normal build output; stopped and relaunched IIS Express with the VS-generated config and `LAUNCHER_PATH` set to the rebuilt apphost.
- Kestrel helper served blank pages because the app is configured for IIS-style hosting; browser verification used IIS Express on `https://localhost:44315/`.
- Local demo `user_views` rows hid the new Vue list defaults; updated only the local demo rows for `/Admin/DemosVue` and `/Admin/DemosVue/edit` so the running demo shows the new columns.
- Vue form had config entries using `datetime_popup`, but no renderer for that type; added a small text-input renderer path so UTC/offset demo fields are editable.
- Vue can keep `datetime_popup` values as ISO strings with explicit offsets; added parser/test coverage so unchanged saves do not convert them to NULL.
- First review-loop finding flagged LF/mixed line endings; fixed by normalizing all task files to CRLF and verifying with byte scan plus `git diff --check`.
- Final review-loop pass found no issues and said the loop can stop.

## Risks / follow-ups

- Full `dotnet test` still fails in pre-existing unrelated tests: autocomplete leading-id parsing, Dynamic prev/next null route, and culture-sensitive ParsePage time formatting.
- Local user-specific list views can hide new demo defaults until reset or updated; the local `demo` DB user view rows were updated for browser verification.
- MySQL columns are compatibility-only `DATETIME`; SQL Server remains the authoritative `datetimeoffset` demo.

## Heuristics (keep terse)

- 2026-05-13: Vue config defaults can be masked by `user_views`; inspect/update local user views when browser verification does not match config defaults.

## Testing instructions

- Apply `osafw-app\App_Data\sql\updates\upd2026-05-13-demo-timezone-fields.sql` to existing SQL Server demo databases.
- Build: `dotnet build osafw-app\osafw-app.csproj`.
- Targeted tests: `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~DateUtilsTests|FullyQualifiedName~FwModelDateTimeInputTests|FullyQualifiedName~ParsePageTests|FullyQualifiedName~DBTests"`.
- Browser smoke: log in, edit a demo row in `/Admin/Demos`, `/Admin/DemosDynamic`, and `/Admin/DemosVue`; verify `fdatetime_local` is `type=datetime-local` and values survive reload.

## Reflection

- Stable framework fact added to `docs/agents/domain.md`.
- No ADR added; this is demo/UI support for the timezone behavior already decided in the timezone patch task.
- Review loop completed with no remaining findings.
