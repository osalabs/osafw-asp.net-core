# Pull Request Code Review Prompt

Perform a proper code review for PR `<PR_URL>`.

Goal: find material issues, fix confirmed issues, verify the fixes, and repeat until the PR is ready for another human review.

## Review

1. Read the repo instructions and the active task summary rules.
2. Fetch PR metadata, changed files, commits, review comments, and check status.
3. Review the diff as a skeptical senior engineer. Prioritize correctness, contracts, security/privacy, data integrity, performance, project fit, simplicity, tests, and documentation sync.
4. Keep findings concrete: severity, file/line, problem, impact, and fix direction.
5. Do not report style preferences unless they hide real maintenance or correctness risk.

## Fix Loop

For each confirmed material issue:

1. Define the smallest fix that addresses the actual risk.
2. Launch a bounded sub-agent to implement the fix when sub-agents are available and the file scope is independent. Give it exact files/modules, constraints, and verification expectations.
3. Keep tightly coupled, risky, shared-contract, or security-sensitive fixes in the main workspace if delegation would add merge risk.
4. Inspect any sub-agent changes before relying on them. The main agent owns integration.
5. Run the smallest verification that can falsify the fix quickly. Add or update tests when the risk justifies it.
6. Re-review the changed diff. Continue until no material review findings remain.

## Verification

- Run focused builds/tests/checks for the affected area.
- Check PR CI status if available.
- If broad tests are impractical, record the targeted checks run and the highest-risk checks not run.
- Do not hide unrelated existing failures; identify them as unrelated only when evidence supports that.

## Closeout

Summarize the original issues, fixes applied, verification run, residual risks, and any PR comments or CI failures still needing attention.

Placeholder:
- `<PR_URL>`: GitHub pull request URL, for example `https://github.com/osalabs/osafw-asp.net-core/pull/XXX`