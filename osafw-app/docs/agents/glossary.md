# Glossary

Updated: 2025-10-08

- FW: Core runtime orchestrator handling routing, sessions, parsing, and controller dispatch.
- ParsePage: Lightweight template engine using `<~tag>` syntax and file-based includes.
- Controller Action: Public method ending with `Action`, e.g., `IndexAction`.
- Dynamic Controller: Controller driven by config.json to render list/form screens with templates.
- Vue Controller: Dynamic controller variant leveraging Vue.js for inline list editing.
- Model: Class derived from `FwModel`, encapsulates DB reads/writes for a table or aggregate.
- ps (Parse Strings): Hashtable returned by controllers for ParsePage; contains data and hints like `"_json"`.
- XSS token: Anti-CSRF token validated on mutating requests stored in session as `XSS`.
- Entity: Business table represented in `fwentities` and models; used for uploads/links.
- Attachment: File in `att`, optionally linked via `att_links`; can be S3-hosted.
- Activity Log: Record in `activity_logs` describing user/system action.
- ADR: Architecture Decision Record stored under `docs/adr`.
