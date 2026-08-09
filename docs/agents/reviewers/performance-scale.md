# Performance and Scale Review Overlay

Use only when a change touches repeated/hot paths, query/data shape, caching, background throughput, blocking I/O, or meaningful allocation/resource behavior. Return candidate findings to the integrator; do not issue a separate verdict.

## Checks

- Follow the real request/job path and estimate multiplicity: per request, row, field, template include, tenant/host, attachment, queue item, retry, or cron iteration.
- Look for DB, remote, file, configuration, template parse, reflection/metadata, serialization, or cache work inside loops. Prefer batch/preload/project/filter/aggregate/page or an existing cache while preserving authorization, order, staleness, and empty-result semantics.
- Verify SQL selects only needed rows/columns, uses deterministic paging/order, parameterizes inputs, and does not create N+1 lookup/authorization patterns.
- Check unbounded materialization, large per-request strings/byte arrays, repeated JSON/template work, sync-over-async/blocking I/O, and expensive per-request clients/resources.
- For `FwCache`, session/cache keys, or provider-backed caches, inspect invalidation, tenant/host/user scope, staleness tolerance, stampede/concurrency behavior, and multi-node assumptions.
- For workers/queues/retries, inspect batch size, backpressure, polling cadence, lock duration, duplicate work, retry amplification, and shutdown/cancellation.
- Prefer measurement or a small data-shape fix before invasive optimization. A theoretical micro-optimization without a plausible workload and impact is not a finding.

State the expected scale/multiplicity and evidence behind the impact. Preserve security and correctness even when a faster shortcut exists.
