# Optional Model-Selection Advice

Snapshot: 2026-09-30. Use when choosing a developer starting model or deliberately recalibrating profiles. This is dated advice, not a required read or binding routing policy. The current developer choice, explicit constraints, runtime availability, and [workflow](workflow.md) take precedence. Do not change user settings or introduce a primary/project-wide model pin.

## Recommended starting points

- **GPT-6.1 Sol High:** recommended starting model for normal framework and copied-application development. The standard implementation profile `implementation_sol_high` and ordinary independent reviewer `reviewer_high` also select this model and effort.
- **GPT-6 Astra:** retain for difficult work and deeper reviews. `reviewer_astra_high` supplies the deeper review alternative; Extra High implementation, architecture, and review profiles follow their existing risk/recovery gates. A consequential task may start with the appropriate Astra profile without first failing with Sol.
- Keep existing inexpensive discovery and small-implementation helpers for bounded work. Max remains subject to the existing profile and review-router consequence gates. Older alternatives remain available when explicit constraints or availability justify them.
- Add effort only for the actual packet's demands. A primary model choice does not require every child to use the same model or generation.

These are operating recommendations, not claims about the product's built-in default or universal model superiority. Profile TOML files own executable model and effort settings. Select a role-appropriate available profile; settings in a pinned profile take precedence over spawn overrides. A Sol starting choice permits Astra delegation unless an explicit developer constraint rules it out. The workflow and [review router](review-routing.md) own selection, fallback, independence, and the short selection reason.

## External evidence

The current Artificial Analysis Intelligence Index v4.3.2 reports:

| Configuration | Intelligence Index | Average API cost per index task |
| --- | --- | --- |
| [GPT-6.1 Sol High](https://artificialanalysis.ai/models/gpt-6-1-sol-high) | 50 | $0.32 |
| [GPT-6 Astra Medium](https://artificialanalysis.ai/models/gpt-6-astra-medium) | 50 | $1.54 |
| [GPT-6 Astra High](https://artificialanalysis.ai/models/releases/gpt-6-astra) | 51 | $1.73 |

The [previous GPT-6 Sol High](https://artificialanalysis.ai/models/releases/gpt-6-sol) scores 43 on this index version. Aggregate equality and small score differences do not establish interchangeable performance on every task or statistically established superiority. Do not compare these numbers directly with an older index version.

[OpenAI's release report](https://openai.com/index/introducing-gpt-6-1-sol/) describes near-Astra coding performance and matching Astra on DeepSWE at roughly one-fifth the cost. The public DeepSWE table inspected for this refresh did not yet expose Sol 6.1 results, so an effort-specific coding duration was not independently verified. [DeepSWE's methodology](https://deepswe.datacurve.ai/blog/deepswe#evaluation-harness) uses mini-swe-agent and five languages excluding C#; it does not validate this repository's Windows, Codex, FW contracts, or independent-review quality.

Throughput, first-answer latency, reasoning-token use, tool time, and retries contribute differently to completed-task time. Artificial Analysis reported higher output throughput for Sol 6.1 High than Astra Medium, but a longer first-answer wait; these measurements vary. This recommendation does not claim that Sol finishes our tasks faster.

[Official Codex model guidance](https://learn.chatgpt.com/docs/models) recommends Sol 6.1 for complex coding when available and retains Astra for the most demanding work. High is our selected starting effort, not a universal product default or a cross-model quality equivalence. [Codex pricing](https://learn.chatgpt.com/docs/pricing) publishes Standard paid-credit input/output rates one-fifth Astra's for Sol 6.1; API prices and credit rates do not imply a fixed ratio of completed tasks within included subscription limits. Speed modes and actual token use also affect consumption. Check the active runtime for availability and effective settings.

## Lightweight calibration

For a model or effort change with unchanged workflow safeguards:

1. Run the instruction validator, strict UTF-8/CRLF checks, manifest/role checks, and fresh-runtime profile discovery. Distinguish file validation from an advertised and correctly configured runtime role.
2. Use three bounded checks: real repository contract tracing at the recommended starting effort, a small isolated implementation with prewritten behavioral checks, and independent review of a prepared diff containing known defects and preserved-valid behavior.
3. Record correctness, rework, elapsed time when available, and any tool/configuration fallback. An explicit-model fallback can check behavior but does not prove named-profile loading. A smoke check establishes bounded behavior, not a model ranking.
4. Compare another model on the same fixture only when a failure or consequential unresolved choice warrants it. Do not repeat a full model-by-effort-by-workflow matrix for profile availability alone.

Keep fixtures and raw results task-owned and ignored. Shared workflow still requires its ordinary independent review and active-summary audit. Revisit this dated recommendation when relevant model/runtime changes or observed regressions justify it; do not browse benchmarks before routine work.
