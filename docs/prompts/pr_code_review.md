# Pull Request Code Review Prompt

Perform an evidence-based code review for PR `<PR_URL>`, fix confirmed blocking issues in the local worktree, verify them, and repeat until ready for another human review.

This prompt authorizes scoped local fixes. Do not commit, push, post comments/reviews, resolve threads, merge, release, or deploy unless the invocation explicitly authorizes those external/Git actions.

## Review

1. Read repository instructions, the active summary rules, `docs/agents/review-routing.md`, and `docs/agents/code_reviewer.md`.
2. Inspect PR metadata, base/head/diff, changed files/commits, unresolved review context, and check status using available read-only capabilities. Do not assume CI, connectors, or subagents exist.
3. Run applicable deterministic checks first. Use the integrating reviewer for the broad diff and select at most the triggered specialist overlay(s); avoid a fixed panel.
4. Follow the capability-conditional execution in `docs/agents/review-routing.md` for one bounded independent-review pass. Do not launch one worker per file or suspected issue.
5. Adjudicate one final verdict. Deduplicate root defects and keep findings concrete: severity, tight file/line/control flow, evidence, impact, and smallest useful fix direction. Do not report style preferences or checklist questions as defects.

## Fix loop

For each adjudicated Blocker, High, or Medium issue:

1. Define the smallest fix that addresses the actual risk and preserves nearby/public contracts.
2. Implement tightly coupled, shared-contract, security, and state-integrity fixes in the main workspace. Delegate only bounded independent file scopes when it clearly reduces time/risk and the capability exists.
3. Inspect all delegated or generated changes before relying on them; the primary agent owns integration.
4. Run the nearest falsifying behavior-level check, then any required provider/compile/manual variant.
5. Re-review the integrated diff and continue only while a Blocker, High, or Medium finding remains. Low observations are non-blocking.

## Verification and closeout

- Use focused builds/tests/checks for affected surfaces; inspect PR checks when available. Separate pre-existing/unrelated failures only when evidence proves the distinction.
- Record material checks not run, provider/platform limits, external publication still needed, and residual risk.
- Summarize original adjudicated issues, fixes, verification, final verdict, and any comments/checks still needing human attention.

Placeholder: `<PR_URL>` is the GitHub pull request URL, for example `https://github.com/osalabs/osafw-asp.net-core/pull/XXX`.
