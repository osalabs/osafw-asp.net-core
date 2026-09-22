# OSAFW FPF/DPF Profile

Use this map for a live question that needs external conceptual guidance. [fpf.md](fpf.md) owns source use, revision pins, bounds, provenance, licensing, and stop conditions; it does not need to be reread for every lookup.

The routes apply to public framework work and copied applications. Preserve deliberate application behavior and consult optional app-owned `docs/agents/fpf-app.md` for local domains, examples, exclusions, and preferred patterns. That extension supplements these routes within repository contracts and explicit developer decisions.

## First-entry routes

| Current question | First source route | Return to repository evidence |
| --- | --- | --- |
| What problem, acceptance boundary, or viable alternatives should shape the request? | Problem Structuring and Decision Support (PSD), then a domain DPF if useful | Feature requirements, `docs/feature_modules.md`, affected topic docs, and acceptance checks. A proposed screen/table is an implementation option, not proof of the operational need. |
| Which system, intended use, boundary, interface, configuration, or architecture changes? | Systems Engineering (`SYSE`) | [domain.md](domain.md), `docs/adr/`, current call paths, and public/source-copy contracts. Identify framework, app, provider, and deployment interfaces actually affected. |
| Is a reusable workflow coherent, usable, and worth its burden? | Method Engineering (`ME`) | [workflow.md](workflow.md), [verification.md](verification.md), [review-routing.md](review-routing.md), helper behavior, and observed task evidence. Separate instruction consistency, routing walkthroughs, and demonstrated development outcomes. |
| How can a computation preserve requested answers within resource limits? | Computational Thinking (current prefix `CMP`) | Implementation, tests, data/SQL contracts, caller expectations, and measured performance. For search/index changes, verify preserved matches and continuation rather than truncating inputs. |
| Which established engineering or application-domain practice applies? | Closest Engineering DPF or independent DPF; Engineering Suite Reference if several contribute | Nearest canonical code/docs owner and specialist evidence. General guidance cannot replace row authorization, token, attachment, provider, or domain-specific controls. |
| Does the question combine mathematical, modeling, physical, computational, or notational work? | Foundational Thinking Suite Reference, then the smallest sufficient member DPF | Exact algorithm/model/representation, assumptions, tests, and app meaning. Domain estimates and displayed numbers require their own evidence. |
| Is a cross-domain distinction about systems, methods, work, claims, evidence, decisions, or publication unresolved? | FPF Core | Current source, canonical docs, ADRs, tests, and developer decisions. A report's existence does not prove source truth, authorization, freshness, or a business decision. |

Publication names and prefixes can evolve. Use `Read-Fpf.ps1 -Action List -Revision <full-sha>` and exact search results instead of guessing filenames or PatternIDs.

## App-owned extension

An application may maintain `docs/agents/fpf-app.md` with domain routes, local terminology, source-to-code examples, and exclusions requiring specialist legal, clinical, financial, safety, or organizational evidence. Keep it contributor-safe: no credentials, private paths, production data, or cache state. Instruction-pack upgrades preserve it and app-specific shared-profile adaptations.

Return every recommendation to its repository owner. FPF does not create a second acceptance path: compatibility, security, provider, review, and permission rules still apply. Missing application facts or authority cannot be filled by a general DPF.
