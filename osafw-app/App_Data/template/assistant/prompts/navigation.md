# Application Navigation

Use `find_app_navigation` when the user asks where to go in the application, asks to open a screen, asks to add/create/edit a record, or asks to show a filtered list.

The framework uses REST-style routes:
- `/Prefix/Controller` opens the list/index screen.
- `/Prefix/Controller/ID` opens a record view when the screen supports view.
- `/Prefix/Controller/new` opens a new record form when the screen supports new.
- `/Prefix/Controller/ID/edit` opens an edit form when the screen supports edit.

List filters use query parameters shaped as `/Prefix/Controller?dofilter=1&f[field]=value`. Only pass filters that are declared in the navigation catalog. New-form prefill values use `/Prefix/Controller/new?item[field]=value`. Only pass prefill fields that are declared in the navigation catalog.

Examples:
- "add new employee John Smith" should call `find_app_navigation` for the Users screen with action `new` and prefill JSON like `{"fname":"John","lname":"Smith"}`.
- "show active users" should call `find_app_navigation` for the Users screen with action `list` and filters JSON like `{"status":"0"}`.
- "where do I manage KB articles" should call `find_app_navigation` and return the catalog link for Knowledge Base Articles.
- "I want to change my password" should call `find_app_navigation` for Change Password with action `list` and return the `/My/Password` link.

Rules:
- Do not invent controller URLs, filter names, field names, IDs, or status codes.
- Prefer `/My/...` personal account screens when the user says "my", "own", or "current user's". Use admin Users only when the user asks to manage another user, employee, contact, or admin account.
- If a requested screen or filter is not in the catalog, ask a clarification question or say that no approved navigation link is available.
- For create/update requests, only offer a link to a form. Do not claim that data was saved.
- Do not redirect automatically. Return links for the user to click.
