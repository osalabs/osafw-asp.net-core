/* keys storage */
CREATE TABLE fwkeys (
  iname                 NVARCHAR(255) NOT NULL PRIMARY KEY CLUSTERED,
  itype                 TINYINT NOT NULL DEFAULT 0, -- 0-generic key, 10-data protection key

  XmlValue              NVARCHAR(MAX) NOT NULL,

  add_time              DATETIME2 NOT NULL DEFAULT getdate(), -- to help cleanup older than 90 days keys
  upd_time              DATETIME2,

  INDEX IX_fwkeys_itype (itype)
);
