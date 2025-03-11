-- support for windows authentication

ALTER TABLE users ADD login NVARCHAR(128) NULL;
GO

CREATE UNIQUE NONCLUSTERED INDEX UX_users_login ON users(login) WHERE login IS NOT NULL;
GO
