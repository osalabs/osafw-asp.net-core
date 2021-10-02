-- Custom menu items for sidebar

CREATE TABLE menu_items (
  id INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  iname                 NVARCHAR(64) NOT NULL default '',
  url                   NVARCHAR(255) NOT NULL default '',  -- menu url
  icon                  NVARCHAR(64) NOT NULL default '',   -- menu icon
  controller            NVARCHAR(255) NOT NULL default '',  -- controller name for highlighting
  access_level          TINYINT NOT NULL DEFAULT 0,         -- min access level

  status                TINYINT DEFAULT 0,        /*0-ok, 10-hidden, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
