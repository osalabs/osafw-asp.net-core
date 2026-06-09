## What changed
- Created `docs/drafts/custom_reports_review.md` with architecture, code, docs, security, template/UI, simplification, test-gap, and open-question findings for the custom reports branch.
- Updated the review with user feedback: prefer table-name purity, keep `fwreports`, record the runtime/base class rename as a breaking change in `CHANGELOG.md`, treat Site Admins as trusted DB readers/report authors, avoid table/limit allowlists, skip schema/version storage, and keep manual RBAC grants intentional.
- Updated the review with follow-up feedback: use `FwReportsBase` as the report runtime/base class name and keep custom report SQL read-only.
- No implementation fixes were made.
## Scope reviewed
- Compared `custom-reports` to `master` with `git diff --name-status master...HEAD`, `git diff --stat master...HEAD`, and targeted file reads.
- Reviewed changed report runtime/controller/model files: `AdminReports.cs`, `FwReports.cs`, `FwCustomReport.cs`, `FwReportsModel.cs`, and the changed `DB.cs` limit/materialization support.
- Reviewed changed report templates under `osafw-app/App_Data/template/admin/reports/**`.
- Reviewed SQL scripts for SQL Server, MySQL, SQLite, and the additive update script.
- Reviewed `docs/README.md`, `docs/reports.md`, `docs/fw_upgrade_prompt.md`, `docs/naming.md`, `docs/templates.md`, and `docs/agents/code_reviewer.md`.
- Large draft note: `docs/drafts/FPF-Spec.md` is over 1 MB, so only targeted `rg` searches for report/dashboard/view/evidence/access/run concepts were used; no whole-file read was performed.
## Commands used / verification
- `git status --short --branch`
- `git branch --show-current`
- `git diff --name-status master...HEAD`
- `git diff --stat master...HEAD`
- `git diff --numstat master...HEAD`
- `rg -n "reports|custom|FwReports|AdminReports|report_html|page_header" docs/reports.md docs/templates.md docs/db.md docs/naming.md`
- `rg -n "class |public |protected |private |internal |static |cleanup|clean|validate|execute|query|Access|access|Role|roles|sql|SQL|render|Run|Save|Delete|Show|Index|Preview|export|timeout|limit" ...`
- Targeted `Get-Content` reads for changed files and supporting docs/templates.
- No build or tests were run because this task created review documentation only.
- Follow-up: targeted reads of `docs/drafts/custom_reports_review.md` and `docs/agents/tasks/summary-2026-06-04-custom-reports-review.md`. A local-instructions read was blocked by approval policy because it can expose machine-local secrets; no local-only guidance was needed for this docs feedback.
- Follow-up: updated and re-read `docs/drafts/custom_reports_review.md` and the task summary.
## Decisions - why
- Kept the review as a findings/questions document because the user asked to pass it to a developer for fixes.
- Updated naming guidance to use table-name purity because the user explicitly chose that path, while still calling out the breaking framework rename and changelog requirement.
- Focused security findings on changed report execution paths and directly supporting validation/access code.
- Removed now-answered security questions from the open-question list and recorded the trust/RBAC decisions in the review doc.
- Recorded `FwReportsBase` and read-only SQL as settled decisions, leaving no open developer questions in the review doc.
## Pitfalls - fixes
- Avoided whole-file reads of the large draft specification; used targeted searches and recorded that scope here.
- The formal security scan workflow was too heavy for the requested deliverable, so the review stayed diff-scoped and evidence-based without producing separate scan artifacts.
## Risks / follow-ups
- Review findings were not fixed in this task.
- Build/test status of the branch was not re-run during this review.
- Existing untracked files in the working tree were ignored as unrelated.
## Heuristics (keep terse)
- No stable heuristics added.
## Testing instructions
- N/A - docs/review only.
## Reflection
The main slowdown was separating useful branch-review depth from a full security-scan workflow. Future review tasks that include a security section but ask for one developer-facing review document should stay anchored to the diff and only escalate to formal scan artifacts when explicitly requested. Targeted reads worked well; the large draft spec should continue to be searched by headings/keywords rather than read whole.
