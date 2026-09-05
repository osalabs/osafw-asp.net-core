# Optional Model-Selection Advice

Snapshot: 2026-09-05. Use when choosing a developer starting model or deliberately recalibrating profiles. This is dated advice, not a required read or binding routing policy. The current developer choice, explicit constraints, runtime availability, and [workflow](workflow.md) take precedence. Do not change user settings or introduce a primary/project-wide model pin.

## Recommended starting points

- **GPT-6 Astra Medium:** recommended for general development when quality is worth moderately higher latency and cost.
- **GPT-5.6 Sol High:** a supported alternative when speed, cost, or developer preference favors it.
- Keep existing inexpensive helpers for bounded work. Add effort only for the actual packet's demands; a primary model choice does not require every child to use the same generation.

These are operating recommendations, not claims about the product's built-in default or universal model superiority. Profile TOML files own executable model and effort settings. Select a role-appropriate available profile; settings in a pinned profile take precedence over spawn overrides. Starting with either generation permits delegation to the other unless the developer says otherwise. The workflow owns selection, fallback, and the short selection reason.

## External evidence

The [Artificial Analysis comparison](https://artificialanalysis.ai/models/comparisons/gpt-6-astra-medium-vs-gpt-5-6-sol-high) reports an Intelligence Index of 52 versus 48, output speed of 56.3 versus 71.9 tokens/s, and time to first token of 11.42 versus 13.81 seconds for Astra Medium versus Sol High. First token and output speed do not by themselves measure completed coding work.

[DeepSWE v1.1](https://deepswe.datacurve.ai/) and its [duration data](https://deepswe.datacurve.ai/data/v1.1?hm_stat=avg_duration_seconds&pivot=true&hm_trials=non_errored) report:

| Configuration | Reported pass rate | Average trial duration | Average API cost |
| --- | --- | --- | --- |
| Sol High | 69% +/-1% | 11m37s | $2.66 |
| Sol Extra High | 71% +/-1% | 15m01s | $3.60 |
| Astra Low | 67% +/-1% | 10m13s | $2.19 |
| Astra Medium | 73% +/-3% | 14m44s | $4.38 |
| Astra High | 73% +/-3% | 17m19s | $5.72 |
| Astra Extra High | 74% +/-3% | 18m52s | $6.52 |
| Astra Max | 73% +/-1% | 33m03s | $12.37 |

Durations and [costs](https://deepswe.datacurve.ai/data/v1.1?hm_stat=avg_cost_usd&pivot=true&hm_trials=non_errored) exclude errored trials and include passed and failed solutions. The displayed score uncertainty is reproduced without claiming a statistically established ordering. Astra Medium takes about 27% longer than Sol High here; Max shows no clear aggregate gain over Medium despite much greater time and cost.

These API costs do not determine Codex subscription usage. Timings depend on harness and environment. [DeepSWE's methodology](https://deepswe.datacurve.ai/blog/deepswe#evaluation-harness) uses mini-swe-agent and five languages excluding C#; it does not validate this repository's Windows, Codex, and FW contracts. The [official Astra model page](https://developers.openai.com/api/docs/models/gpt-6-astra) documents supported effort levels. Check the active runtime for actual availability.

## Lightweight calibration

For an additive profile change with unchanged workflow safeguards:

1. Run the instruction validator, strict UTF-8/CRLF checks, manifest/role checks, and fresh-runtime profile discovery.
2. Use three bounded checks: real repository contract tracing at the recommended starting effort, a small isolated implementation with prewritten behavioral checks, and independent review of a prepared diff containing known defects and preserved-valid behavior.
3. Record correctness, rework, elapsed time when available, and any tool/configuration fallback. A smoke check establishes integration, not a model ranking.
4. Compare another model on the same fixture only when a failure or consequential unresolved choice warrants it. Do not repeat a full model-by-effort-by-workflow matrix for profile availability alone.

Keep fixtures and raw results task-owned and ignored. Shared workflow still requires its ordinary independent review and active-summary audit. Revisit this dated recommendation when relevant model/runtime changes or observed regressions justify it; do not browse benchmarks before routine work.
