IF OBJECT_ID(N'dbo.fwreports', N'U') IS NULL
BEGIN
  CREATE TABLE fwreports (
    id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
    icode                 NVARCHAR(50) NOT NULL,
    iname                 NVARCHAR(255) NOT NULL DEFAULT '',
    idesc                 NVARCHAR(MAX),
    icon                  NVARCHAR(64) NOT NULL DEFAULT '',
    access_level          TINYINT NOT NULL DEFAULT 80,
    sql_template          NVARCHAR(MAX) NOT NULL DEFAULT '',
    params_json           NVARCHAR(MAX),
    render_options_json   NVARCHAR(MAX),

    status                TINYINT NOT NULL DEFAULT 0,
    add_time              DATETIME2 NOT NULL DEFAULT getdate(),
    add_users_id          INT DEFAULT 0,
    upd_time              DATETIME2,
    upd_users_id          INT DEFAULT 0
  );

  CREATE UNIQUE INDEX UX_fwreports_icode ON fwreports(icode);
END
GO

IF OBJECT_ID(N'dbo.fwreports', N'U') IS NOT NULL
  AND COL_LENGTH(N'dbo.fwreports', N'icon') IS NULL
BEGIN
  ALTER TABLE fwreports ADD icon NVARCHAR(64) NOT NULL CONSTRAINT DF_fwreports_icon DEFAULT '';
END
GO
