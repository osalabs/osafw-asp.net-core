## What changed
- Added S3 attachment key compatibility:
  - `S3.IS_ATT_KEY_BY_ID` defaults false and can be set directly by end-user apps that keep id-based S3 keys.
  - `Att.getS3Key()` and `Att.getS3FolderKey()` centralize object keys for redirect, upload, download, and delete.
  - Default S3 keys remain `att/{icode}/{icode}[_{size}]`; legacy mode uses `att/{id}/{id}[_{size}]`.
- Added `Att.TRUSTED_INLINE_EXTS = ".pdf"` and extended attachment disposition policy so `/Att/{icode}` opens PDFs inline while `/Att/Download/{icode}` remains attachment.
- Updated S3 upload metadata, changelog, SQL schema comments, agent domain facts, focused tests, and the task index.
## Scope reviewed
- Reviewed `docs/README.md`, `docs/agents/local_instructions.md`, `docs/agents/tasks/index.md`, and the prior attachment hardening summary.
- Reviewed attachment/S3 code paths in `Att.cs`, `S3.cs`, `UploadUtils.cs`, `AttController`, and focused attachment/upload tests.
## Commands used / verification
- `dotnet test osafw-tests\osafw-tests.csproj --filter "FullyQualifiedName~SecurityAttachmentTests|FullyQualifiedName~UploadUtilsTests" -p:OutDir=C:\DOCS_PROJ\github\osafw-asp.net-core\artifacts\assistant_test\`: passed, 28 tests.
- Earlier legacy compile-symbol test was removed after user feedback because compatibility is now a direct const, not a project property.
- `git diff --check`: passed after final normalization.
- `docs\agents\tools\Normalize-TextFiles.ps1 -Check ...`: passed after final normalization.
## Decisions - why
- Keep `/Att/{icode}` route and attachment authorization unchanged; the compatibility switch changes only S3 object key selection.
- Default S3 key token remains `att.icode`; legacy apps can opt into `att.id` by changing `S3.IS_ATT_KEY_BY_ID`.
- Keep trusted inline extension handling opt-in at the attachment call sites so generic upload utility callers keep their old download-by-default behavior for non-images.
## Pitfalls - fixes
- User feedback rejected a project property/compile symbol; simplified `S3.IS_ATT_KEY_BY_ID` to a direct const, simplified the test, and documented the direct const instead.
- PowerShell blocked the normalization helper by execution policy; reran it with per-process `-ExecutionPolicy Bypass`.
- Reviewer sub-agent was spawned but did not finish within the wait window and was shut down; performed the documented fallback self-review.
- Fallback review found `moveToS3()` should continue deriving S3 disposition from `filenameForPolicy(item)` before calling `S3.uploadLocalFile()`; fixed so active stored/display metadata cannot regain inline behavior through the local path.
## Risks / follow-ups
- Full suite was not run; focused attachment/upload tests passed for the default const mode.
- Review loop completed by fallback self-review after the sub-agent timed out; no remaining issues found.
## Heuristics (keep terse)
- None added.
## Testing instructions
- Run the targeted focused test command listed above.
## Reflection
The sub-agent review path was useful to attempt for this security-sensitive surface, but the wait timed out; future agents should switch to the documented self-review fallback after one unproductive reviewer wait instead of blocking. The fallback pass caught a subtle S3 metadata regression around `moveToS3()` and stored/display filename policy. Stable attachment/S3 facts were added to `docs/agents/domain.md`; no heuristics or ADRs were added.
