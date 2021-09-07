CREATE TABLE user_filters (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  icode                 NVARCHAR(128) NOT NULL, -- related screen, ex: GLOBAL[controller.action]

  iname                 NVARCHAR(255) NOT NULL,
  idesc                 NVARCHAR(MAX), -- json with filter data
  is_system             TINYINT NOT NULL DEFAULT 0, -- 1 - system - visible for all
  is_shared             TINYINT NOT NULL DEFAULT 0, -- 1 if shared/published

  status                TINYINT DEFAULT 0,
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0, -- related owner user
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);

ALTER TABLE user_views ADD
  iname                 NVARCHAR(255) NOT NULL DEFAULT '',
  is_system             TINYINT NOT NULL DEFAULT 0, -- 1 - system - visible for all
  is_shared             TINYINT NOT NULL DEFAULT 0 -- 1 if shared/published
GO

DROP INDEX user_views.UX_user_views;
GO
CREATE UNIQUE INDEX UX_user_views ON user_views (add_users_id, icode, iname);
GO