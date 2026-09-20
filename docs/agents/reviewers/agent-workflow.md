# Agent Workflow Review Overlay

Use only when shared agent instructions, prompts, routing, memory, local configuration, verification/review workflow, or optional tooling behavior changes.

Return candidate findings to the integrator; do not issue a separate final verdict.

## Checks

- **Instruction discovery:** Can a fresh supported agent discover the root instructions without a private setup step? Are task-routed files reachable when needed, and do nested instructions apply only to their intended scope?
- **Canonical ownership:** Is each rule stated once, with detailed guidance in its routed topic owner rather than repeated across instruction files?
- **Dual repository role:** Does guidance work for both public framework maintenance and a copied real-application repository? Does it discover branch protection, remote, app customization, and release policy rather than hard-code framework assumptions into all copies?
- **Request validation and permissions:** Can a short prompt rely on discoverable repository/issue context? Are desired outcome and inferred implementation separated? Are diagnose/review boundaries and commit/push/PR/release/deploy/database/external-write permissions explicit?
- **Progressive disclosure:** Is the root concise enough for always-loaded context while routing architecture, verification, review overlays, task history, and optional tools only when triggered? Do all routed paths exist and avoid circular loading?
- **Local/private configuration:** Is the tracked contract value-free and portable? Are machine paths, credentials, IDE state, optional private references, and caches confined to ignored local instructions/environment mechanisms? Do worktrees have a safe route to shared ignored local instructions?
- **Task memory:** Does the index support deterministic recall without loading all history? Are summaries treated as evidence logs, not authority or duplicated stable docs? Are active and historical status labels preserved rather than semantically rewritten by a validator?
- **Adaptive routing:** Is direct execution the default unless every child has a reusable non-overlapping output and credible coordination payoff? Does the route normally spend at most one pre-implementation delegation stage, stacking stages only when each distinct output changes the next decision? Does the primary consume delegated discovery/implementation instead of repeating it, while retaining final integration?
- **Adaptive review:** Is there one integrator verdict, risk-triggered fresh review, an initial handoff free of worker narrative/self-verdict anchoring, triggered overlays, deduplication/adjudication, and a no-subagent fallback? Does guidance avoid assuming a model, mode, connector, or delegation capability?
- **Verification and cleanup:** Are deterministic checks distinguishable from quality claims? Do classifier, security-default, and compatibility checks cover established public entry paths plus both ordinary/attacker negatives and intended/preserved positives? Are Windows/IIS and database/worktree resource ownership explicit? Are ignored result/artifact paths used?
- **Self-improvement:** Are instruction changes evidence-based, reviewable, infrequent, isolated when substantial, and never automatically self-merged/deployed? Is prior agent output non-authoritative?

## FPF/reference checks

When the optional FPF integration is in scope, verify that repository evidence remains authoritative; lookup is material rather than ceremonial; upstream code is never executed; reads are bounded to one full revision with source location and continuation metadata; refresh is lazy, bounded, concurrency-safe, and last-good; accepted and merely available revisions remain distinct; policy-changing updates require review; read-only reviewers can use status/read without cache writes; copied applications preserve their profile extension and adoption state; and copied or adapted expression receives the required attribution without implying that FPF ideas relicense application code. The integration must not grant permissions, pin models, or add a runtime dependency.
