-- PassKey support
ALTER TABLE users ADD passkey NVARCHAR(255);
ALTER TABLE users ADD passkey_pub NVARCHAR(MAX);

