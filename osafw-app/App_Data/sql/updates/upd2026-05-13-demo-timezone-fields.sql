--- demo timezone fields

IF COL_LENGTH('dbo.demos', 'fdatetime_utc') IS NULL
    EXEC('ALTER TABLE dbo.demos ADD fdatetime_utc DATETIME2 NULL');

IF COL_LENGTH('dbo.demos', 'fdatetime_offset') IS NULL
    EXEC('ALTER TABLE dbo.demos ADD fdatetime_offset DATETIMEOFFSET NULL');

IF COL_LENGTH('dbo.demos', 'fdatetime_local') IS NULL
    EXEC('ALTER TABLE dbo.demos ADD fdatetime_local DATETIME2 NULL');

EXEC('UPDATE dbo.demos
SET
    fdatetime_utc = COALESCE(fdatetime_utc, SYSUTCDATETIME()),
    fdatetime_offset = COALESCE(fdatetime_offset, TODATETIMEOFFSET(SYSUTCDATETIME(), ''+00:00'')),
    fdatetime_local = COALESCE(fdatetime_local, GETDATE())
WHERE fdatetime_utc IS NULL
   OR fdatetime_offset IS NULL
   OR fdatetime_local IS NULL');
