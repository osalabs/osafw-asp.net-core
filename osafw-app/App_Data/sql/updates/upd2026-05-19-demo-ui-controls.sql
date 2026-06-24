--- demo UI control fields

IF OBJECT_ID('dbo.demos', 'U') IS NOT NULL AND COL_LENGTH('dbo.demos', 'frange') IS NULL
    EXEC('ALTER TABLE dbo.demos ADD frange INT NOT NULL DEFAULT 50');

IF OBJECT_ID('dbo.demos', 'U') IS NOT NULL AND COL_LENGTH('dbo.demos', 'is_switch') IS NULL
    EXEC('ALTER TABLE dbo.demos ADD is_switch TINYINT NOT NULL DEFAULT 0');
