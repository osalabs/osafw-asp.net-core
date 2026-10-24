-- Add Settings row metadata to an existing SQLite database.
ALTER TABLE settings ADD COLUMN access_level INTEGER NOT NULL DEFAULT 100;
ALTER TABLE settings ADD COLUMN mask INTEGER NOT NULL DEFAULT 0;
ALTER TABLE settings ADD COLUMN basis INTEGER NOT NULL DEFAULT 0;

-- Every pre-existing row already had an explicit stored value.
UPDATE settings SET basis=1;
UPDATE settings SET access_level=90 WHERE icode='test';
UPDATE settings SET mask=10, access_level=100 WHERE input=90;

INSERT OR IGNORE INTO settings (is_user_edit, input, access_level, mask, basis, icat, icode, ivalue, iname, idesc, allowed_values) VALUES
(1, 0, 90, 0, 0, 'Site', 'SITE_NAME', '', 'Site Name', 'Name shown in page titles, email and authentication labels.', ''),
(1, 0, 90, 0, 0, 'Site', 'UNLOGGED_DEFAULT_URL', '', 'Public Default URL', 'App-local URL used when an unauthenticated user needs a default destination.', ''),
(1, 0, 90, 0, 0, 'Site', 'LOGGED_DEFAULT_URL', '', 'Signed-in Default URL', 'App-local URL used as the default destination after sign-in.', ''),
(1, 70, 100, 0, 0, 'Security', 'is_mfa_enforced', '', 'Require MFA', 'Require configured users to complete multi-factor authentication.', ''),
(1, 70, 90, 0, 0, 'Display', 'is_list_btn_left', '', 'List Actions on Left', 'Show standard list action buttons on the left side.', ''),
(1, 20, 90, 0, 0, 'Display', 'ui_theme', '', 'Default Interface Theme', 'Default interface theme for users without a saved preference.', '0|Default 10|Pink 20|Shadows 30|Blue'),
(1, 20, 90, 0, 0, 'Display', 'ui_mode', '', 'Default Display Mode', 'Default color mode for users without a saved preference.', '0|Auto 10|Light 20|Dark'),
(1, 0, 100, 0, 0, 'Email', 'feedback_email', '', 'Feedback Email', 'Recipient for user feedback messages.', ''),
(1, 0, 100, 0, 0, 'Email', 'support_email', '', 'Support Email', 'Recipient for support and contact messages.', ''),
(1, 0, 100, 0, 0, 'Email', 'mail_from', '', 'Mail From', 'Default sender address for application email.', ''),
(1, 0, 100, 0, 0, 'Email', 'admin_email', '', 'Administrator Email', 'Recipient for framework and application error notifications.', ''),
(1, 0, 100, 0, 1, 'Email', 'test_email', '', 'Test Email Recipient', 'Test-mode delivery address. Leave blank or enter current_user to use the logged-in user''s email.', ''),
(1, 0, 100, 0, 0, 'Email', 'mail.host', '', 'SMTP Host', 'SMTP server hostname.', ''),
(1, 60, 100, 0, 0, 'Email', 'mail.port', '', 'SMTP Port', 'SMTP server port.', 'min|1 max|65535 step|1'),
(1, 70, 100, 0, 0, 'Email', 'mail.is_ssl', '', 'SMTP TLS', 'Use TLS when connecting to the SMTP server.', ''),
(1, 0, 100, 0, 0, 'Email', 'mail.username', '', 'SMTP Username', 'Username used to authenticate to the SMTP server.', ''),
(1, 90, 100, 10, 0, 'Email', 'mail.password', '', 'SMTP Password', 'Password used to authenticate to the SMTP server.', ''),
(1, 20, 100, 0, 0, 'AWS', 'AWS_CREDENTIAL_SOURCE', '', 'AWS Credential Source', 'Use the AWS SDK credential chain or the encrypted static key pair.', 'sdk|SDK static|Static'),
(1, 90, 100, 10, 0, 'AWS', 'AWSAccessKey', '', 'AWS Access Key', 'Static AWS access key used only when Static credential source is selected.', ''),
(1, 90, 100, 10, 0, 'AWS', 'AWSSecretKey', '', 'AWS Secret Key', 'Static AWS secret key used only when Static credential source is selected.', ''),
(1, 90, 100, 10, 1, 'AI', 'OPENAI_API_KEY', '', 'OpenAI API Key', 'API key used by Assistant and LLM features.', ''),
(1, 90, 100, 10, 0, 'Security', 'API_KEY', '', 'Application API Key', 'Shared key accepted by framework API authentication when configured.', ''),
(1, 70, 100, 0, 1, 'AI', 'ASSISTANT_ENABLED', '0', 'Assistant Enabled', 'Set to 1 to enable the assistant UI and queued runs.', ''),
(1, 20, 100, 0, 1, 'AI', 'ASSISTANT_VECTOR_MODE', 'auto', 'Assistant Vector Mode', 'Use auto, json, or native. Auto uses SQL Server native vectors when available.', 'auto|Auto json|JSON native|Native'),
(1, 0, 100, 0, 1, 'AI', 'ASSISTANT_MODEL', 'gpt-5-mini', 'Assistant Model', 'Chat model used for assistant responses.', ''),
(1, 70, 100, 0, 1, 'AI', 'ASSISTANT_MEMORY_ENABLED', '0', 'Assistant Memory Enabled', 'Set to 1 to save optional per-user assistant memory summaries.', ''),
(1, 60, 100, 0, 1, 'AI', 'ASSISTANT_RUN_TIMEOUT_SECONDS', '120', 'Assistant Run Timeout Seconds', 'Maximum queued or processing time for UI-facing assistant responses before they fail and can be retried.', 'min|30 step|1'),
(1, 60, 100, 0, 1, 'AI', 'ASSISTANT_MAX_FILES_PER_MESSAGE', '5', 'Assistant Max Files Per Message', 'Maximum number of files accepted with one assistant message.', 'min|1 step|1'),
(1, 60, 100, 0, 1, 'AI', 'ASSISTANT_MAX_INDEXED_FILE_BYTES', '5242880', 'Assistant Max Indexed File Bytes', 'Maximum supported attachment size for queued indexing. Larger files remain attached but are not indexed.', 'min|1 step|1'),
(1, 60, 100, 0, 1, 'AI', 'ASSISTANT_MAX_INDEX_CHARS', '200000', 'Assistant Max Index Characters', 'Maximum parsed characters indexed per document.', 'min|1 step|1'),
(1, 60, 100, 0, 1, 'AI', 'ASSISTANT_MAX_INDEX_CHUNKS', '80', 'Assistant Max Index Chunks', 'Maximum embedding chunks indexed per document.', 'min|1 step|1');
