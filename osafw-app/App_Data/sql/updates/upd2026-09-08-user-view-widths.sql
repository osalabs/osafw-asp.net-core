-- persisted list column widths

IF OBJECT_ID('dbo.user_views', 'U') IS NOT NULL AND COL_LENGTH('dbo.user_views', 'widths') IS NULL
    ALTER TABLE dbo.user_views ADD widths NVARCHAR(MAX) NOT NULL CONSTRAINT DF_user_views_widths DEFAULT ('{}') WITH VALUES;
