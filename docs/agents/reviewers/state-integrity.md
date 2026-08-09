# State Integrity Review Overlay

Use when a change writes durable/shared state or changes schema/provider behavior, transactions/concurrency/idempotency, session/cache/keys, jobs/queues/retries, runtime file state, or external object lifecycle. Return candidate findings to the integrator; do not issue a separate verdict.

## Checks

- Identify the authoritative state, ownership boundary, lifecycle, invariants, and every duplicate/cache/projection. Do not let a derived store silently become authority.
- Validate write predicates, null/default/type/date conversions, uniqueness, foreign/parent relationships, affected-row expectations, and partial-failure behavior.
- Inspect transaction boundaries and ordering across DB, file, queue, cache, email, and external calls. Define compensation/retry behavior where one atomic transaction cannot cover all effects.
- Check concurrent requests/workers, duplicate delivery, retries, timeout recovery, lock ownership/expiry, idempotency keys, lost updates, and read-modify-write races.
- SQL Server is the production-primary provider and schema/update authority. Use it for provider-specific semantics. SQLite is useful for disposable provider-neutral isolation but cannot prove SQL Server locking, T-SQL, types, or deployment behavior. MySQL parity is not assumed.
- Fresh schemas are destructive initialization inputs; additive updates are sequential upgrade inputs. For an affected provider, keep the fresh schema, update script, runtime discovery/order, tests, and docs aligned without replaying another provider's incompatible SQL.
- Verify datetime/date/`_utc`/`datetimeoffset`, collation/case, identity/last-insert-id, paging/order, DDL/batch, and transaction differences on each provider actually claimed.
- Session/data-protection/cache state preserves host/user scope, encryption/platform behavior, expiration, invalidation, and multi-node assumptions.
- Background services (`FwCron`, Assistant workers/queues, update runners) shut down safely, do not double-run work, and leave recoverable state after crashes.
- File/upload/generated output and external objects use task/business ownership, atomic replace when needed, bounded cleanup, and no deletion of shared/unverified paths.

Name the violated invariant and a concrete failure sequence. Do not request cross-provider parity or distributed guarantees outside the task's declared support surface.
