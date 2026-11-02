# Sign-in contracts

Login forms submit `POST /Login` with the current session `XSS` token and credentials in the request body. The default form includes the token. Explicit action URLs cannot use GET to log in, and query-string `item` fields are rejected. Custom login forms and scripts must first establish their own session/token. MFA completion is a tokened POST as well.

Persistent remember-me cookies restore only currently active accounts without enrolled or enforced MFA. MFA-protected accounts sign in interactively when their session expires; this framework does not treat a remember-me cookie as a trusted MFA device. Cookie lookup enforces server-side age limits. Password, status, MFA enrollment/recovery changes revoke the user's stored persistent tokens; unrelated profile updates preserve them. Current account status is checked again before login session creation.

Windows login uses the complete authenticated `DOMAIN\username` identity for `users.login`, preserving the domain boundary. Before importing this change into a copied application, migrate existing short Windows login names to their exact trusted full principals. There is no short-name fallback. Unknown identities retain minimum-level provisioning; duplicate email constraints may reject provisioning rather than reuse an account from another domain. Existing disabled-account checks remain. Enrolled users follow `/Login/(MFA)` and enforced users without enrollment follow `/My/MFA`. These checks apply in addition to host Windows authentication.

No database schema migration is needed. Full Windows identity migration is application-owned data maintenance and must be reviewed against the application's trusted identity source.

Anonymous MFA enrollment requires a recent setup-purpose login for a currently active account without an existing factor. A pending challenge never authorizes replacing the enrolled factor; the policy is rechecked before saving enrollment.
