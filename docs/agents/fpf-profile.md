# OSAFW FPF/DPF Profile

This profile maps recurring OSAFW development questions to the smallest useful FPF publication family and then back to repository authority. Read [fpf.md](fpf.md) first for the optional-use, cache, revision, provenance, and licensing contract.

The profile applies to both the public framework and repositories created by copying it. In a copied application, preserve deliberate application behavior and consult optional app-owned `docs/agents/fpf-app.md` for domain publications, examples, exclusions, or preferred entry patterns. That extension may narrow or add routes; it cannot override current code, canonical repository docs, security/data rules, or explicit developer decisions.

## First-entry routes

| Current work question | First source route | Return to repository evidence | Concrete example |
| --- | --- | --- | --- |
| What user/business problem and acceptance boundary are current? Which options deserve comparison? | Problem Structuring and Decision Support (PSD), then a domain DPF if one owns the subject | Feature owner, issue/requirements, `docs/feature_modules.md`, affected canonical topic docs, and acceptance tests | Before adding a new admin module, separate the requested operational outcome from the suggested screen/table and compare only viable whole options. |
| Which System, intended use, boundary, interface, configuration, or architecture is changing? | Systems Engineering (`SYSE`) | [domain.md](domain.md), `docs/adr/`, source call paths, `docs/templates.md`, `docs/db.md`, `docs/deploy.md`, and public/source-copy contracts | For a new storage/provider boundary, identify the framework, copied app, database, deployment, and downstream configuration interfaces before selecting structure. |
| Is the reusable way of working coherent, situationally fit, verifiable, and maintainable? | Method Engineering (`ME`) | [workflow.md](workflow.md), [verification.md](verification.md), [review-routing.md](review-routing.md), helper behavior, and observed task evidence | When changing upgrade or review workflow, distinguish the Method from one task's performed work and test the real entry path plus recovery conditions. |
| How should a computation be decomposed or transformed while preserving requested answers and resource limits? | Computational Thinking (current pattern prefix `CMP`) | C# or PowerShell implementation, tests, performance evidence, data/SQL contracts, and caller expectations | For a search/index helper, bound inputs and output, preserve continuation, and test that pruning does not discard a requested match. |
| Which established engineering or application-domain practice applies? | The closest Engineering DPF or other independent DPF; use the Engineering Suite Reference when several may contribute | The nearest code/docs owner and relevant specialist evidence | Authorization work may need software/security/domain practice, but the repository's POST token, row predicate, attachment, and provider contracts decide the implementation. |
| Does the question combine mathematical, modeling, physical, computational, or notational contributions? | Foundational Thinking Suite Reference, then the smallest sufficient member DPF | Exact algorithm/model/data representation, assumptions, tests, and user-facing meaning | A scoring or forecasting feature may need modeling and computational routes while UI labels and stored values remain governed by app contracts. |
| Is a cross-domain distinction about Systems, Methods, Work, claims, evidence, decisions, architecture, publication, or precise language unresolved? | FPF Core | Current source, canonical docs, ADRs, tests, and developer decision | A report or dashboard is a publication surface; its presence does not establish source truth, authorization, freshness, or a business decision. |

Publication names and prefixes can evolve. Use `Read-Fpf.ps1 -Action List -Revision <full-sha>` and exact search results from the selected revision rather than guessing a filename. The current Computational Thinking prefix is `CMP`; preserve the source-reported ID and revision in material provenance.

## Repository-specific combinations

- Public API, route, template, configuration, generated output, schema/provider, or frontend/email/storage changes: use the relevant route above only to clarify the problem, then apply the compatibility inventory and controls in [workflow.md](workflow.md), [verification.md](verification.md), and the consumer-contract review overlay.
- Authentication, authorization, HTML/markdown, attachments, redirects, secrets, or dev/admin tooling: FPF may help separate claims, permissions, evidence, and work, but [AGENTS.md](../../AGENTS.md) and the security-boundary overlay own the required controls.
- Database or lifecycle changes: use domain/Systems Engineering framing when boundaries are unclear, then follow `docs/db.md`, provider authority, additive-update policy, transaction/concurrency evidence, and state-integrity review.
- Agent instruction or helper changes: Method Engineering can inform the workflow design; the tracked instruction pack, deterministic validators, representative walkthroughs, and one integrator verdict remain the acceptance path.
- Product or app-domain ambiguity: start with PSD and the app's DPF extension when present. Do not let a general framework publication invent user needs, data ownership, or authorization.

## App-owned extension

An application may create `docs/agents/fpf-app.md` for stable local routing such as:

- named business domains and the DPFs that help with them;
- app-specific examples that connect a pattern family to canonical app docs or code;
- exclusions where legal, clinical, financial, safety, or organization policy requires a specialist source;
- approved local terminology and provenance expectations.

Keep the extension value-free and contributor-safe. Do not store credentials, private paths, production data, or cache state in it. Instruction-pack upgrades preserve the file and merge shared profile changes around it rather than replacing it.

## Stop and return conditions

Stop FPF lookup when the repository evidence already answers the question, the source cannot change a live decision, or the next step requires unavailable application facts, product authority, specialist evidence, or permission. Return an explicit assumption or blocker instead of adding more patterns. A source-grounded suggestion becomes repository guidance only through the normal code/doc/ADR decision and review path.
