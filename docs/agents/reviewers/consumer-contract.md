# Consumer Contract Review Overlay

Use when a change adds, removes, or alters a consumer-visible surface listed below, or when a copied application imports framework changes across such surfaces. Return candidate findings to the integrator; do not issue a separate verdict.

## Contract inventory

Trace the changed behavior through every affected consumer surface:

- public/protected C# types, members, virtual overrides, return/null/exception shapes, and extension points;
- controller names/actions, routes, HTTP verbs/status/redirects, request fields, JSON/page-state shapes, and access assumptions;
- ParsePage templates/includes/blocks/variables, `url.html` literals, Vue selectors/events/classes, static assets/plugins, email output, and storage keys/paths;
- `config.json`, `appsettings`, environment variables, compile symbols, package/project references, and defaults;
- database tables/fields/types/defaults, fresh schemas, additive updates, provider discovery/order, and scaffolded/generated output;
- examples, CLI behavior, upgrade prompts, canonical docs, and dated changelog/migration guidance.

## Questions

- Did the implementation search real callers, overrides, templates, generated examples, and app customization points before changing the contract?
- Can existing copied apps continue working through additive behavior or a small compatibility shim? If not, is the break necessary, explicit, and paired with practical migration steps?
- Does a security fix deliberately prioritize safety while clearly documenting behavioral impact?
- Are defaults safe for existing apps, not merely convenient for a fresh framework clone?
- When code classifies provider metadata, config values, routes, generated shapes, or trusted/untrusted content, do tests include attacker or ordinary negative values plus every intended-safe/trusted positive form and preserved baseline consumer?
- Do tests reach the established public consumer/generation entry path, or does a fixture use only a new wrapper, inject stored configuration, substitute a helper, or otherwise bypass the branch whose contract is claimed?
- Does framework-mode work avoid depending on private infrastructure? Does application-mode work preserve intentional local divergence rather than overwriting it with upstream?
- For source-copy distribution, is clean-clone/build and upgrade-diff evidence used instead of claiming nonexistent NuGet/package compatibility?
- Are generated/scaffolded outputs and canonical examples updated together so new and existing modules do not diverge silently?
- Is each affected provider/platform claimed only to the extent actually verified?

A finding must name the concrete downstream caller/output that breaks or the compatibility evidence that is missing; do not flag every internal refactor as a public change.
