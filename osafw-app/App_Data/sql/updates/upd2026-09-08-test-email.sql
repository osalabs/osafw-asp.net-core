INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc, allowed_values)
SELECT 1, 0, 'Email', 'test_email', '', 'Test Email Recipient', 'Test-mode delivery address. Leave blank or enter current_user to use the logged-in user''s email.', ''
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE icode='test_email');
