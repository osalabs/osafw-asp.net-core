-- core framework tables only, create app-specific tables in database.sql

/* net core sessions */
DROP TABLE IF EXISTS fwsessions;
CREATE TABLE fwsessions (
  Id                    NVARCHAR(449) NOT NULL PRIMARY KEY,
  Value                 VARBINARY(MAX) NOT NULL,
  ExpiresAtTime         DATETIMEOFFSET(7) NOT NULL,
  SlidingExpirationInSeconds BIGINT NULL,
  AbsoluteExpiration    DATETIMEOFFSET NULL,

  INDEX IX_ExpiresAtTime (ExpiresAtTime)
);

/*application entities lookup - autofilled on demand*/
DROP TABLE IF EXISTS fwentities;
CREATE TABLE fwentities (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  icode                 NVARCHAR(128) NOT NULL default '', -- basically table name
  iname                 NVARCHAR(128) NOT NULL default '', -- human readable name
  idesc                 NVARCHAR(MAX),

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX UX_fwentities_icode UNIQUE (icode)
);


/* upload categories */
DROP TABLE IF EXISTS att_categories;
CREATE TABLE att_categories (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  icode                 NVARCHAR(64) NOT NULL DEFAULT '', /*to use from code*/
  iname                 NVARCHAR(64) NOT NULL DEFAULT '',
  idesc                 NVARCHAR(MAX),
  prio                  INT NOT NULL DEFAULT 0,     /*0-on insert, then =id, default order by prio asc,iname*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);

/* attachments - file uploads */
DROP TABLE IF EXISTS att;
CREATE TABLE att (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED, /* files stored on disk under 0/0/0/id.dat */
  att_categories_id     INT NULL FOREIGN KEY REFERENCES att_categories(id),

  fwentities_id         INT NULL CONSTRAINT FK_att_fwentities FOREIGN KEY REFERENCES fwentities(id), -- related to entity (optional)
  item_id               INT NULL,

  is_s3                 TINYINT DEFAULT 0, /* 1 if file is in S3 - see config: $S3Bucket/$S3Root/att/att_id */
  is_inline             TINYINT DEFAULT 0, /* if uploaded with wysiwyg */
  is_image              TINYINT DEFAULT 0, /* 1 if this is supported image */

  fname                 NVARCHAR(255) NOT NULL DEFAULT '',              /*original file name*/
  fsize                 BIGINT DEFAULT 0,                   /*file size*/
  ext                   NVARCHAR(16) NOT NULL DEFAULT '',                 /*extension*/
  iname                 NVARCHAR(255) NOT NULL DEFAULT '',   /*attachment name*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX IX_att_categories (att_categories_id),
  INDEX IX_att_fwentities (fwentities_id, item_id)
);

/*junction to link multiple att files to multiple entity items*/
DROP TABLE IF EXISTS att_links;
CREATE TABLE att_links (
  att_id                INT NOT NULL CONSTRAINT FK_att_links_att FOREIGN KEY REFERENCES att(id),
  fwentities_id         INT NOT NULL CONSTRAINT FK_att_links_fwentities FOREIGN KEY REFERENCES fwentities(id), -- related to entity
  item_id               INT NOT NULL,

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under change, deleted instantly*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,

  INDEX UX_att_links UNIQUE (fwentities_id, item_id, att_id),
  INDEX IX_att_links_att (att_id, fwentities_id, item_id)
);


-- DROP TABLE IF EXISTS att_table_link;
-- CREATE TABLE att_table_link (
--   id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,
--   att_id                INT NOT NULL,

--   table_name            NVARCHAR(128) NOT NULL DEFAULT '',
--   item_id               INT NOT NULL,

--   status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under update*/
--   add_time              DATETIME2 NOT NULL DEFAULT getdate(),
--   add_users_id          INT DEFAULT 0,

--   INDEX UX_att_table_link UNIQUE (table_name, item_id, att_id)
-- );


DROP TABLE IF EXISTS users;
CREATE TABLE users (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  email                 NVARCHAR(128) NOT NULL DEFAULT '',
  pwd                   NVARCHAR(255) NOT NULL DEFAULT '', -- hashed password
  access_level          TINYINT NOT NULL,  /*0 - visitor, 1 - usual user, 80 - moderator, 100 - admin*/
  is_readonly           TINYINT NOT NULL DEFAULT 0,        -- 1 if user is readonly

  iname                 AS CONCAT(fname,' ', lname) PERSISTED, -- standard iname field, calculated from first+last name
  fname                 NVARCHAR(32) NOT NULL DEFAULT '',
  lname                 NVARCHAR(32) NOT NULL DEFAULT '',
  title                 NVARCHAR(128) NOT NULL DEFAULT '',

  address1              NVARCHAR(128) NOT NULL DEFAULT '',
  address2              NVARCHAR(64) NOT NULL DEFAULT '',
  city                  NVARCHAR(64) NOT NULL DEFAULT '',
  state                 NVARCHAR(4) NOT NULL DEFAULT '',
  zip                   NVARCHAR(16) NOT NULL DEFAULT '',
  phone                 NVARCHAR(16) NOT NULL DEFAULT '',

  lang                  NVARCHAR(16) NOT NULL DEFAULT 'en', -- user interface language
  ui_theme              TINYINT NOT NULL DEFAULT 0, -- 0--default theme
  ui_mode               TINYINT NOT NULL DEFAULT 0, -- 0--auto, 10-light, 20-dark

  idesc                 NVARCHAR(MAX),
  att_id                INT NULL FOREIGN KEY REFERENCES att(id),                -- avatar

  login_time            DATETIME2,
  pwd_reset             NVARCHAR(255) NULL,
  pwd_reset_time        datetime2 NULL,
  mfa_secret            NVARCHAR(64), -- mfa secret code, if empty - no mfa for the user configured
  mfa_recovery          NVARCHAR(1024), -- mfa recovery hashed codes, space-separated
  mfa_added             DATETIME2,    -- last datetime when mfa setup or resynced

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX UX_users_email UNIQUE (email)
);
INSERT INTO users (fname, lname, email, pwd, access_level)
VALUES ('Website','Admin','admin@admin.com','CHANGE_ME',100);

/*user cookies (for permanent sessions)*/
DROP TABLE IF EXISTS users_cookies;
CREATE TABLE users_cookies (
    cookie_id           NVARCHAR(32) PRIMARY KEY CLUSTERED NOT NULL,      /*cookie id: time(secs)+rand(16)*/
    users_id            INT NOT NULL CONSTRAINT FK_users_cookies_users FOREIGN KEY REFERENCES users(id),

    add_time            DATETIME2 NOT NULL DEFAULT getdate()
);

/*Site Settings - special table for misc site settings*/
DROP TABLE IF EXISTS settings;
CREATE TABLE settings (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  icat                  NVARCHAR(64) NOT NULL DEFAULT '', /*settings category: ''-system, 'other' -site specific*/
  icode                 NVARCHAR(64) NOT NULL DEFAULT '', /*settings internal code*/
  ivalue                NVARCHAR(MAX) NOT NULL DEFAULT '', /*value*/

  iname                 NVARCHAR(64) NOT NULL DEFAULT '', /*settings visible name*/
  idesc                 NVARCHAR(MAX),                    /*settings visible description*/
  input                 TINYINT NOT NULL default 0,       /*form input type: 0-input, 10-textarea, 20-select, 21-select multi, 30-checkbox, 40-radio, 50-date*/
  allowed_values        NVARCHAR(MAX),                    /*space-separated values, use &nbsp; for space, used for: select, select multi, checkbox, radio*/

  is_user_edit          TINYINT DEFAULT 0,  /* if 1 - use can edit this value*/

  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX UX_settings_icode UNIQUE (icode),
  INDEX IX_settings_icat (icat)
);
INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc) VALUES
(1, 10, '', 'test', 'novalue', 'test settings', 'description');

/*Static pages*/
DROP TABLE IF EXISTS spages;
CREATE TABLE spages (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  parent_id             INT NOT NULL DEFAULT 0,  /*parent page id*/

  url                   NVARCHAR(255) NOT NULL DEFAULT '',      /*sub-url from parent page*/
  iname                 NVARCHAR(64) NOT NULL DEFAULT '',       /*page name-title*/
  idesc                 NVARCHAR(MAX),                          /*page contents, markdown*/
  head_att_id           INT NULL FOREIGN KEY REFERENCES att(id), /*optional head banner image*/

  idesc_left            NVARCHAR(MAX),                          /*left sidebar content, markdown*/
  idesc_right           NVARCHAR(MAX),                          /*right sidebar content, markdown*/
  meta_keywords         NVARCHAR(255) NOT NULL DEFAULT '',      /*meta keywords*/
  meta_description      NVARCHAR(255) NOT NULL DEFAULT '',      /*meta description*/

  pub_time              DATETIME2,                               /*publish date-time*/
  template              NVARCHAR(64),                           /*template to use, if not defined - default site template used*/
  prio                  INT NOT NULL DEFAULT 0,                 /*0-on insert, then =id, default order by prio asc,iname*/
  is_home               INT DEFAULT 0,                          /* 1 is for home page (non-deletable page*/
  redirect_url          NVARCHAR(255) NOT NULL DEFAULT '',      /*if set - redirect to this url instead displaying page*/

  custom_css            NVARCHAR(MAX),                          /*custom page css*/
  custom_js             NVARCHAR(MAX),                          /*custom page js*/

  status                TINYINT NOT NULL DEFAULT 0,    /*0-ok, 10-not published, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX IX_spages_parent_id (parent_id, prio),
  INDEX IX_spages_url (url)
);
--TRUNCATE TABLE spages;
INSERT INTO spages (parent_id, url, iname) VALUES
(0,'','Home') --1
,(0,'test-page','Test  page') --2
;
update spages set is_home=1 where id=1;

/*Logs types*/
DROP TABLE IF EXISTS log_types;
CREATE TABLE log_types (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  itype                 TINYINT NOT NULL DEFAULT 0,       -- 0-system, 10-user selectable
  icode                 NVARCHAR(64) NOT NULL default '', -- added/updated/deleted /comment /simulate/login_fail/login/logoff/chpwd

  iname                 NVARCHAR(255) NOT NULL default '',
  idesc                 NVARCHAR(MAX),
  prio                  INT NOT NULL DEFAULT 0,     /*0-on insert, then =id, default order by prio asc,iname*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX IX_log_types_icode (icode)
);

/*Activity Logs*/
DROP TABLE IF EXISTS activity_logs;
CREATE TABLE activity_logs (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  reply_id              INT NULL,                         -- for hierarcy, if needed
  log_types_id          INT NOT NULL CONSTRAINT FK_activity_logs_log_types FOREIGN KEY REFERENCES log_types(id), -- log type
  fwentities_id         INT NOT NULL CONSTRAINT FK_activity_logs_fwentities FOREIGN KEY REFERENCES fwentities(id), -- related to entity
  item_id               INT NULL,                         -- related item id in the entity table

  idate                 DATETIME2 NOT NULL DEFAULT getdate(), -- default now, but can be different for user types if activity added at a different date/time
  users_id              INT NULL CONSTRAINT FK_activity_logs_users FOREIGN KEY REFERENCES users(id), -- default logged user, but can be different if adding "on behalf of"
  idesc                 NVARCHAR(MAX),
  payload               NVARCHAR(MAX), -- serialized/json - arbitrary payload, should be {fields:{fieldname1: data1, fieldname2: data2,..}} for added/updated/deleted

  status                TINYINT NOT NULL DEFAULT 0,       -- 0-active, 10-inactive/hidden, 20-draft(for user types, private to user added), 127-deleted
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX IX_activity_logs_reply_id (reply_id),
  INDEX IX_activity_logs_log_types_id (log_types_id),
  INDEX IX_activity_logs_fwentities_id (fwentities_id),
  INDEX IX_activity_logs_item_id (item_id),
  INDEX IX_activity_logs_idate (idate),
  INDEX IX_activity_logs_users_id (users_id)
);


/*Lookup Manager Tables*/
DROP TABLE IF EXISTS lookup_manager_tables;
CREATE TABLE lookup_manager_tables (
  id INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  tname                 NVARCHAR(255) NOT NULL DEFAULT '', /*table name*/
  iname                 NVARCHAR(255) NOT NULL DEFAULT '', /*human table name*/
  idesc                 NVARCHAR(MAX),                     /*table internal description*/

  is_one_form           TINYINT NOT NULL DEFAULT 0,        /*1 - lookup table cotains one row, use form view*/
  is_custom_form        TINYINT NOT NULL DEFAULT 0,        /*1 - use custom form template, named by lowercase(tname)*/
  header_text           NVARCHAR(MAX),                     /*text to show in header when editing table*/
  footer_text           NVARCHAR(MAX),                     /*text to show in footer when editing table*/
  column_id             NVARCHAR(255),                     /*table id column, if empty - use id*/

  list_columns          NVARCHAR(MAX),                     /*comma-separated field list to display on list view, if defined - bo table edit mode available*/
  columns               NVARCHAR(MAX),                     /*comma-separated field list to display, if empty - all fields displayed*/
  column_names          NVARCHAR(MAX),                     /*comma-separated column list of column names, if empty - use field name*/
  column_types          NVARCHAR(MAX),                     /*comma-separated column list of column types/lookups (" "-string(default),readonly,textarea,checkbox,tname.IDfield:INAMEfield-lookup table), if empty - use standard input[text]*/
  column_groups         NVARCHAR(MAX),                     /*comma-separated column list of groups column related to, if empty - don't include column in group*/
  url                   NVARCHAR(255) NOT NULL DEFAULT '', /*if defined - redirected to this URL instead of LookupManager forms*/

  access_level          TINYINT NULL,                       -- min access level, if NULL - use Lookup Manager's acl

  status                TINYINT NOT NULL DEFAULT 0,                /*0-ok, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),  /*date record added*/
  add_users_id          INT DEFAULT 0,                        /*user added record*/
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX UX_lookup_manager_tables_tname (tname)
);

/*user custom views*/
DROP TABLE IF EXISTS user_views;
CREATE TABLE user_views (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  icode                 NVARCHAR(128) NOT NULL, --related screen url, ex: "/Admin/Demos"
  fields                NVARCHAR(MAX), -- comma-separated list of fields to display, order kept

  iname                 NVARCHAR(255) NOT NULL DEFAULT '', -- if empty - it's a "default" view
  is_system             TINYINT NOT NULL DEFAULT 0, -- 1 - system - visible for all
  is_shared             TINYINT NOT NULL DEFAULT 0, -- 1 if shared/published
  density               NVARCHAR(16) NOT NULL DEFAULT '', -- list table density class: table-sm(or empty - default), table-dense, table-normal

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0, -- related user
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX UX_user_views (add_users_id, icode, iname)
);

/*user lists*/
DROP TABLE IF EXISTS user_lists;
CREATE TABLE user_lists (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  entity                NVARCHAR(128) NOT NULL, -- usually table name or base_url, ex: 'demos' or /Admin/Demos

  iname                 NVARCHAR(255) NOT NULL,
  idesc                 NVARCHAR(MAX), -- description

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0, -- related owner user
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX IX_user_lists (add_users_id, entity)
);

/*items linked to user lists */
DROP TABLE IF EXISTS user_lists_items;
CREATE TABLE user_lists_items (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  user_lists_id         INT NOT NULL FOREIGN KEY REFERENCES user_lists(id),
  item_id               INT NOT NULL, -- related item id, example demos.id

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0, -- related owner user
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX UX_user_lists_items (user_lists_id, item_id)
);

/*Custom menu items for sidebar*/
DROP TABLE IF EXISTS menu_items;
CREATE TABLE menu_items (
  id INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  iname                 NVARCHAR(64) NOT NULL default '',
  url                   NVARCHAR(255) NOT NULL default '',  -- menu url
  icon                  NVARCHAR(64) NOT NULL default '',   -- menu icon
  controller            NVARCHAR(255) NOT NULL default '',  -- controller class name for UI highlighting
  access_level          TINYINT NOT NULL DEFAULT 0,         -- min access level

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 10-hidden, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
-- INSERT INTO menu_items (iname, url, icon, controller) VALUES ('Test Menu Item', '/Admin/Demos', 'list-ul', 'AdminDemos');
DROP TABLE IF EXISTS user_filters;
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

-- run roles.sql if roles support required and also uncomment #define isRoles in Users model

-- after this file - run lookups.sql
