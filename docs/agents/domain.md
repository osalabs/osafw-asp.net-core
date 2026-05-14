# Domain / Bounded Context

Updated: 2026-05-14

Purpose
- Provide a reusable admin/back-office web framework for CRUD-heavy business apps on ASP.NET Core.

Core Subdomains
- Users and Access
  - Users with access levels (0 visitor, 1 user, 80 moderator, 100 admin). Optional roles/resources/permissions.
  - Authentication: password, optional Windows auth; MFA support fields present.
- Content and Pages
  - Static pages (`spages`) with optional templates, metadata, and publish dates.
- Settings
  - Key-value site settings (`settings`) with categories and UI metadata; user-editable flags.
  - Runtime `FwConfig` settings are the flat contents of JSON `appSettings`; callers read `db`, `SITE_NAME`, etc. directly, not through an `appSettings` child key.
- Files / Attachments
  - `att`, `att_links`, `att_categories` for uploads; supports S3 and inline images. Linked to entities via `fwentities`.
- Activity Logging
  - `activity_logs` capture actions with types, entity, item_id, payload of changed fields.
- Dynamic CRUD
  - Controllers with JSON configs for fields, lists, lookups, subtables, attachments, and Vue inline editing.
  - Model-backed dynamic lookup controls default to active rows; edit forms can include the same-field saved inactive row as an ` (inactive)` exception.
- Scheduling
  - `fwcron` for cron-like background jobs; optional `FwCronService` hosted service.
- Virtual Controllers
  - `fwcontrollers` define controller metadata stored in DB to render screens without code.

Boundaries
- DB access encapsulated by `DB` helper and models.
- Database providers are a bounded runtime concern: SQL Server is the default, SQLite is optional for durable single-node deployments, and provider-specific scripts live under `App_Data/sql/<provider>/` when the provider needs divergent SQL.
- Datetime boundaries: SQL `date` is calendar-only, ordinary `datetime`/`datetime2` is DB-timezone-normalized to UTC, `_utc` fields are already UTC, and SQL Server `datetimeoffset` is offset-aware instant storage.
- Dynamic/Vue `datetime_local` fields submit browser-native `YYYY-MM-DDTHH:mm` values that the backend parses as user-local datetimes before UTC save conversion.
- Dictionary DB single-row reads return empty `DBRow`/`FwDict` for "not found"; typed single-row reads return `null`, with `*OrFail` variants for required records.
- UI rendered by `ParsePage` templates; no Razor.
- ParsePage allows recursive file-template includes for tree rendering, but stops deeper includes at a fixed crash-protection recursion-depth limit and logs `WARN`.
- Multi-tenancy per-host via `FwConfig` overrides and caching.
