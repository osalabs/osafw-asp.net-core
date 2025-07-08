USE master
GO
CREATE LOGIN yyyyyy WITH PASSWORD = 'XXXX', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
GO
USE zzzzzz
GO
CREATE USER yyyyyy FOR LOGIN yyyyyy
EXEC sp_addrolemember N'db_owner', N'yyyyyy'
GO