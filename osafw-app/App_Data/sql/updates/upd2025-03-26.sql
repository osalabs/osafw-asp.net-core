-- Spages - custom head
ALTER TABLE spages ADD custom_head NVARCHAR(MAX);

-- increase email length
ALTER TABLE users ALTER COLUMN email NVARCHAR(255) NOT NULL;


-- virtual controllers
CREATE TABLE fwcontrollers
(
    id                INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

    icode             NVARCHAR(128) NOT NULL DEFAULT '', -- controller class without Controller suffix: AdminDemos
    url               NVARCHAR(128) NOT NULL DEFAULT '', -- controller url: /Admin/Demos
    iname             NVARCHAR(128) NOT NULL DEFAULT '', -- human readable name
    idesc             NVARCHAR(MAX) NULL,

    model             NVARCHAR(255) NOT NULL DEFAULT '', -- model class name controller is based on
    is_lookup         TINYINT NOT NULL DEFAULT 0,       -- 1 if this is lookup controller (show in Lookup Manager)
    igroup            NVARCHAR(64) NOT NULL DEFAULT '', -- group name, if set - tables grouped under same group name
    access_level      TINYINT NOT NULL DEFAULT 0,       -- min view access level
    access_level_edit TINYINT NOT NULL DEFAULT 0,       -- min edit access level

    config            NVARCHAR(MAX) NULL,               -- config.json - use/create if file not exists /template/admin/demos/config.json

    status            TINYINT NOT NULL DEFAULT 0,       -- 0-ok, 10-inactive, 127-deleted
    add_time          DATETIME NOT NULL DEFAULT GETDATE(),
    add_users_id      INT DEFAULT 0,
    upd_time          DATETIME NULL,
    upd_users_id      INT DEFAULT 0,

    INDEX UX_fwcontrollers_icode UNIQUE (icode),
    INDEX UX_fwcontrollers_url UNIQUE (url)
);

-- drop deprecated table
DROP TABLE lookup_manager_tables;

-- track framework database updates
CREATE TABLE fwupdates
(
    id           INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

    iname        NVARCHAR(255) NOT NULL DEFAULT '', -- filename from db/updates folder
    idesc        NVARCHAR(MAX) NULL,                -- file content

    applied_time DATETIME NULL,                     -- applied date-time
    last_error   NVARCHAR(MAX) NULL,                -- last error message

    status       TINYINT NOT NULL DEFAULT 0,        -- 0(new), 10(inactive/skip), 20-failed, 30-applied, 127-deleted
    add_time     DATETIME NOT NULL DEFAULT GETDATE(),
    add_users_id INT DEFAULT 0,
    upd_time     DATETIME NULL,
    upd_users_id INT DEFAULT 0,

    INDEX UX_fwupdates_iname UNIQUE (iname)
);

INSERT INTO fwcontrollers (igroup, icode, url, iname, model, access_level)
VALUES ('System', 'AdminLogTypes', '/Admin/LogTypes', 'Log Types', 'FwLogTypes', 100),
       ('System', 'AdminAttCategories', '/Admin/AttCategories', 'Upload Categories', 'AttCategories', 50),
       ('System', 'AdminFwUpdates', '/Admin/FwUpdates', 'FW Updates', 'FwUpdates', 100)
;
UPDATE fwcontrollers
SET is_lookup=1;
