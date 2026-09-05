-- project-style code and computed display name for the demos kitchen-sink example

IF OBJECT_ID('dbo.demos', 'U') IS NOT NULL AND COL_LENGTH('dbo.demos', 'icode') IS NULL
    EXEC(N'ALTER TABLE dbo.demos ADD icode NVARCHAR(32) NOT NULL DEFAULT N''''');

IF OBJECT_ID('dbo.demos', 'U') IS NOT NULL AND COL_LENGTH('dbo.demos', 'icode') IS NOT NULL
    EXEC(N'UPDATE dbo.demos SET icode = CONCAT(N''DEMO-'', id) WHERE icode = N''''');

IF OBJECT_ID('dbo.demos', 'U') IS NOT NULL AND COL_LENGTH('dbo.demos', 'display_name') IS NULL
    EXEC(N'ALTER TABLE dbo.demos ADD display_name AS CAST(CONCAT(icode, CASE WHEN icode <> N'''' AND iname <> N'''' THEN N'' — '' ELSE N'''' END, iname) AS NVARCHAR(128)) PERSISTED');
