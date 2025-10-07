# Glossary (≤60 terms)
- **FwController** — Base controller class providing routing, auth, and request helpers for OSAF controllers.
- **FwModel** — Data access base class encapsulating CRUD helpers and database wiring.
- **ParsePage** — Custom templating engine used to render views from `App_Data/template` folders.
- **FwUpdates** — Component that applies SQL migration scripts from `App_Data/sql/updates/` during startup.
- **FwSelfTest** — Diagnostic suite accessible via `/Dev/SelfTest` to verify configuration and dependencies.
- **FwCronService** — Optional background job runner toggled in `Program.cs` for scheduled tasks.
- **LibMan** — Library Manager manifest (`libman.json`) managing client-side libraries under `wwwroot/lib`.
