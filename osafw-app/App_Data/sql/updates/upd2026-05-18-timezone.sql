--- user timezone auto preference

IF COL_LENGTH('dbo.users', 'timezone') IS NOT NULL
BEGIN
    UPDATE dbo.users
    SET timezone = ''
    WHERE timezone = 'UTC';

    DECLARE @timezoneDefaultConstraint sysname;

    SELECT @timezoneDefaultConstraint = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.users')
      AND c.name = N'timezone';

    IF @timezoneDefaultConstraint IS NOT NULL
    BEGIN
        DECLARE @sql NVARCHAR(MAX);
        SET @sql = N'ALTER TABLE dbo.users DROP CONSTRAINT ' + QUOTENAME(@timezoneDefaultConstraint);
        EXEC sp_executesql @sql;
    END

    ALTER TABLE dbo.users ADD CONSTRAINT DF_users_timezone DEFAULT '' FOR timezone;
END