# Domain / Bounded Context

Updated: 2025-10-08

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
- Files / Attachments
  - `att`, `att_links`, `att_categories` for uploads; supports S3 and inline images. Linked to entities via `fwentities`.
- Activity Logging
  - `activity_logs` capture actions with types, entity, item_id, payload of changed fields.
- Dynamic CRUD
  - Controllers with JSON configs for fields, lists, lookups, subtables, attachments, and Vue inline editing.
- Scheduling
  - `fwcron` for cron-like background jobs; optional `FwCronService` hosted service.
- Virtual Controllers
  - `fwcontrollers` define controller metadata stored in DB to render screens without code.

Boundaries
- DB access encapsulated by `DB` helper and models.
- UI rendered by `ParsePage` templates; no Razor.
- Multi-tenancy per-host via `FwConfig` overrides and caching.
