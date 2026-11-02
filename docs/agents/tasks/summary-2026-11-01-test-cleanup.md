# Test cleanup and automatic verification

## Outcome and decisions

Implemented a repository test command with Fast (default), Browser, SqlServer and Full profiles, focused filters, separate build/test timing, explicit exclusions and nonzero exit for failed/unavailable/empty selected runs. Fast enables SQLite only for its build and preserves existing project/global symbols. Production framework runtime and application configuration are unchanged. Build-only EnableSqliteTests and test-server selection are documented in verification.md.

Reviewed all 988 original C# methods in 82 files, including 246 DataRow attributes and conditional variants. The final per-method ledger is ignored at artifacts/test-cleanup/audit-final.csv; source reviewer ledgers audit-fw.csv, audit-security.csv and audit-utilities.csv preserve detailed scenarios and reasons. Reconciliation has zero missing original methods. Final dispositions: 877 keep, 64 simplify (including fixture/setup changes), 27 move, 3 merge and 17 remove. There are 968 retained test methods. All 128 named PowerShell fixture/self-test assertion sites were reviewed and retained; scenario detail is in audit-security.csv. Optional future cleanup recommendations are separate from implemented dispositions.

Removed assignment/exception boilerplate and exact duplicate scenarios, preserving meaningful default/security/compatibility contracts. Controller wording checks were reduced. Independent review identified distinct retry-radio template wiring coverage, which was restored; page-state coverage alone was insufficient. Browser process reuse is confined to Vue tests, each case retaining a fresh context. PDF tests exercise the real production renderer lifetime and single-method browser classes offer no reuse savings.

Moved 25 retained connection-free DB methods away from SQL setup, plus two settings policy methods out of the SQLite compile gate. SQL integration owns a unique database, seeds current schema with optional application database.sql, and deletes only its owned database. The normal helper rejects unexpected DB access; explicit report configuration tests retain production wrapper selection, and synthetic security models supply explicit schema metadata. SQLite file ownership is shared by database and FW lifetime fixtures. Retained assembly serial execution because environment variables and caches remain shared.

Corrected two baseline fixture failures (decimal expectation and signup password), a foreign-key assertion that swallowed its own failure, weak dashboard scoping assertions, a current-clock race, and an excessive random-ID smoke loop. No security check was removed merely for being simple. No dependency upgrades or public runtime APIs were added; no changelog entry is required.

## Verification

- Baseline isolated SQLite build/run, excluding legacy DBTests and tagged browser groups: 993 passed, 2 failed, 995 total, 37-second reported test duration. The old selection also included an untagged password browser test, now classified. This is not a directly comparable final Fast benchmark.
- scripts/test.ps1 -Profile Full -SqlServer '(local)': passed; results retained under artifacts/test-runs/5cac9f47b21e4d5398a9e0e372ce3fe5. Default 966, Fast 1008, Browser 87 and SqlServer 35 cases passed, with zero failed/unavailable. The final radio-template restoration was included in SQLite/browser and SQL stages; a focused check passed its single restored-radio case (artifacts/test-cleanup/radio/radio.trx).
- Fast selection with PLAYWRIGHT_BROWSERS_PATH pointing to a nonexistent task path: 1008 passed, no unavailable. Evidence: artifacts/test-cleanup/no-browser/no-browser.trx.
- Fast profile with FullyQualifiedName=NoSuchTest selected zero cases and returned exit 1, as required. An earlier two-fixture failure also returned exit 1.
- SQLite plus externally supplied isRoles: AdminSettingsRbacTests passed all 4 cases (artifacts/test-cleanup/roles/results/roles.trx).
- Earlier Browser profile: 87 passed. Earlier disposable SQL Server profile: 35 passed. Both used the real existing providers/assets; results remain ignored.
- A failed disposable initialization (missing optional database.sql) removed its created database; a corrected successful run also left zero osafw_test databases. Verified with a read-only sys.databases query. The legacy demo/application database was not used.
- EnableSqliteTests=true with global DefineConstants=isRoles evaluates to isRoles;TRACE;isSQLite;DEBUG. No project symbols are replaced by the runner.
- Test-AgentInstructions.ps1 passed under PowerShell 7 and Windows PowerShell 5.1. git diff --check and strict UTF-8/no-BOM/CRLF checks passed on changed files. Tooling fixture bodies are unchanged, so their full execution is not required by this change.

## Timing and additional isolation evidence

Matched sequential warm runs of the same 78 VueBrowser cases passed before and after. Summed TRX case durations decreased from 71.320 seconds to 23.560 seconds (67.0%); runner-reported durations were approximately 71 and 23 seconds. No other build/test workload ran during this final pair. Commands used dotnet vstest on the retained baseline and final SQLite assemblies with /TestCaseFilter:TestCategory=VueBrowser; raw TRX and comparison.json are under artifacts/test-cleanup/benchmark/isolated-before, isolated-after and comparison.json. This is one observed matched workload, not an overall suite-speed guarantee. Preliminary overlapping measurements are retained separately and were not used for this claim.

Final Fast still took about 41 seconds excluding build/restore, with 1008 cases including newly available connection-free DB tests. The original baseline had a different selection and two failures, so no overall Fast speedup is claimed. Routine savings also come from excluding browser/SQL profiles until relevant and choosing focused filters. The final Full run reported build/restore times separately: default 3.14s, SQLite 4.17s, Browser 4.05s, SQL 2.76s; these are warm-machine observations.

Two simultaneous no-build SQL profile processes each passed all 35 cases using separate generated database names. Both completed and the read-only sys.databases check found no osafw_test databases afterward. Evidence is under artifacts/test-cleanup/concurrent-sql. This proves test-fixture isolation, not simultaneous shared-obj builds; normal worktree/build ownership rules still apply.

## Review and limitations

Read-only audit was divided into three non-overlapping groups; their complete inventories and evidence were consumed rather than re-read. A separate fresh reviewer_high performed final diff review with agent-workflow and state-integrity overlays. One Medium candidate on template binding was accepted and fixed. The initial Medium candidate is resolved. Fresh independent review and the supplemental summary audit returned No blocking findings; the narrow follow-up audit also verified identical benchmark cases, recomputed timing totals and checked overlapping SQL runs. Review loop can stop.

The installed default LocalDB instance fails to start. Explicit -SqlServer '(local)' exercised the same automatic database lifecycle on the available local SQL Server; no machine instance was repaired or reconfigured. The command intentionally fails when an explicitly requested provider/browser prerequisite is unavailable. Linux, remote SQL Server and live external services are not verified. Copied apps must retain their actual schema/provider contracts and app-specific tests rather than assume SQLite parity.

Original sources, detailed audits, TRX files and benchmark evidence stay in ignored artifacts. Unrelated pre-existing untracked files were preserved. No branch operation, commit, push or deployment occurred.
