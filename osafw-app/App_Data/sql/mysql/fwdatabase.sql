-- core framework tables only, create app-specific tables in database.sql

/* net core sessions */
DROP TABLE IF EXISTS fwsessions;
CREATE TABLE fwsessions (
  Id                    VARCHAR(449) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  Value                 LONGBLOB NOT NULL,
  ExpiresAtTime         DATETIME NOT NULL,
  SlidingExpirationInSeconds BIGINT NULL,
  AbsoluteExpiration    DATETIME NULL,

  PRIMARY KEY (Id),
  KEY IX_ExpiresAtTime (ExpiresAtTime)
) DEFAULT CHARSET=utf8mb4;

/* upload categories */
DROP TABLE IF EXISTS att_categories;
CREATE TABLE att_categories (
  id                    INT UNSIGNED NOT NULL auto_increment,

  icode                 VARCHAR(64) NOT NULL DEFAULT '', /*to use from code*/
  iname                 VARCHAR(64) NOT NULL DEFAULT '',
  idesc                 TEXT,
  prio                  INT NOT NULL DEFAULT 0,     /*0-on insert, then =id, default order by prio asc,iname*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id)
) DEFAULT CHARSET=utf8mb4;

DROP TABLE IF EXISTS att;
CREATE TABLE att (
  id                    INT UNSIGNED NOT NULL auto_increment, /* files stored on disk under 0/0/0/id.dat */
  att_categories_id     INT UNSIGNED NULL,

  table_name            VARCHAR(128) NOT NULL DEFAULT '',
  item_id               INT UNSIGNED NOT NULL DEFAULT 0,

  is_s3                 TINYINT DEFAULT 0, /* 1 if file is in S3 - see config: $S3Bucket/$S3Root/att/att_id */
  is_inline             TINYINT DEFAULT 0, /* if uploaded with wysiwyg */
  is_image              TINYINT DEFAULT 0, /* 1 if this is supported image */

  fname                 VARCHAR(255) NOT NULL DEFAULT '',              /*original file name*/
  fsize                 INT UNSIGNED DEFAULT 0,                   /*file size*/
  ext                   VARCHAR(16) NOT NULL DEFAULT '',                 /*extension*/
  iname                 VARCHAR(255) NOT NULL DEFAULT '',   /*attachment name*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_att_table_name_item_id (table_name, item_id),
  FOREIGN KEY (att_categories_id) REFERENCES att_categories(id)
) DEFAULT CHARSET=utf8mb4;

/* link att files to table items*/
DROP TABLE IF EXISTS att_table_link;
CREATE TABLE att_table_link (
  id                    INT UNSIGNED NOT NULL auto_increment,
  att_id                INT UNSIGNED NOT NULL,

  table_name            VARCHAR(128) NOT NULL DEFAULT '',
  item_id               INT NOT NULL,

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under update*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_att_table_link (table_name, item_id, att_id),
  FOREIGN KEY (att_id) REFERENCES att(id)
) DEFAULT CHARSET=utf8mb4;


DROP TABLE IF EXISTS users;
CREATE TABLE users (
  id                    INT UNSIGNED NOT NULL auto_increment,

  email                 VARCHAR(128) NOT NULL DEFAULT '',
  pwd                   VARCHAR(255) NOT NULL DEFAULT '', -- hashed password
  access_level          TINYINT NOT NULL,                 -- 0 - visitor, 1 - usual user, 80 - moderator, 100 - admin

  fname                 VARCHAR(32) NOT NULL DEFAULT '',
  lname                 VARCHAR(32) NOT NULL DEFAULT '',
  title                 VARCHAR(128) NOT NULL DEFAULT '',

  address1              VARCHAR(128) NOT NULL DEFAULT '',
  address2              VARCHAR(64) NOT NULL DEFAULT '',
  city                  VARCHAR(64) NOT NULL DEFAULT '',
  state                 VARCHAR(4) NOT NULL DEFAULT '',
  zip                   VARCHAR(16) NOT NULL DEFAULT '',
  phone                 VARCHAR(16) NOT NULL DEFAULT '',
  lang                  VARCHAR(16) NOT NULL DEFAULT 'en', -- user interface language

  idesc                 TEXT,
  att_id                INT UNSIGNED NULL,                -- avatar

  login_time            TIMESTAMP,
  pwd_reset             VARCHAR(255) NULL,
  pwd_reset_time        TIMESTAMP NULL,

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_users_email (email),
  FOREIGN KEY (att_id) REFERENCES att(id)
) DEFAULT CHARSET=utf8mb4;
INSERT INTO users (fname, lname, email, pwd, access_level)
VALUES ('Website','Admin','admin@admin.com',UUID(),100);

/*user cookies (for permanent sessions)*/
DROP TABLE IF EXISTS users_cookies;
CREATE TABLE users_cookies (
  cookie_id           VARCHAR(32) NOT NULL,      /*cookie id: time(secs)+rand(16)*/
  users_id            INT UNSIGNED NOT NULL,

  add_time            TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

  PRIMARY KEY (cookie_id),
  FOREIGN KEY (users_id) REFERENCES users(id)
) DEFAULT CHARSET=utf8mb4;

/*Site Settings - special table for misc site settings*/
DROP TABLE IF EXISTS settings;
CREATE TABLE settings (
  id                    INT UNSIGNED NOT NULL auto_increment,
  icat                  VARCHAR(64) NOT NULL DEFAULT '', /*settings category: ''-system, 'other' -site specific*/
  icode                 VARCHAR(64) NOT NULL DEFAULT '', /*settings internal code*/
  ivalue                TEXT, /*value*/

  iname                 VARCHAR(64) NOT NULL DEFAULT '', /*settings visible name*/
  idesc                 TEXT,                    /*settings visible description*/
  input                 TINYINT NOT NULL default 0,       /*form input type: 0-input, 10-textarea, 20-select, 21-select multi, 30-checkbox, 40-radio, 50-date*/
  allowed_values        TEXT,                    /*space-separated values, use &nbsp; for space, used for: select, select multi, checkbox, radio*/

  is_user_edit          TINYINT DEFAULT 0,  /* if 1 - use can edit this value*/

  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_settings_icode (icode),
  KEY IX_settings_icat (icat)
) DEFAULT CHARSET=utf8mb4;
INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc) VALUES
(1, 10, '', 'test', 'novalue', 'test settings', 'description');

/*Static pages*/
DROP TABLE IF EXISTS spages;
CREATE TABLE spages (
  id                    INT UNSIGNED NOT NULL auto_increment,
  parent_id             INT NOT NULL DEFAULT 0,  /*parent page id*/

  url                   VARCHAR(255) NOT NULL DEFAULT '',      /*sub-url from parent page*/
  iname                 VARCHAR(64) NOT NULL DEFAULT '',       /*page name-title*/
  idesc                 TEXT,                          /*page contents, markdown*/
  head_att_id           INT UNSIGNED NULL, /*optional head banner image*/

  idesc_left            TEXT,                          /*left sidebar content, markdown*/
  idesc_right           TEXT,                          /*right sidebar content, markdown*/
  meta_keywords         VARCHAR(255) NOT NULL DEFAULT '',      /*meta keywords*/
  meta_description      VARCHAR(255) NOT NULL DEFAULT '',      /*meta description*/

  pub_time              DATETIME,                               /*publish date-time*/
  template              VARCHAR(64),                           /*template to use, if not defined - default site template used*/
  prio                  INT NOT NULL DEFAULT 0,                 /*0-on insert, then =id, default order by prio asc,iname*/
  is_home               INT DEFAULT 0,                          /* 1 is for home page (non-deletable page*/
  redirect_url          VARCHAR(255) NOT NULL DEFAULT '',      /*if set - redirect to this url instead displaying page*/

  custom_css            TEXT,                          /*custom page css*/
  custom_js             TEXT,                          /*custom page js*/

  status                TINYINT NOT NULL DEFAULT 0,    /*0-ok, 10-not published, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_spages_parent_id (parent_id, prio),
  KEY IX_spages_url (url),
  FOREIGN KEY (head_att_id) REFERENCES att(id)
) DEFAULT CHARSET=utf8mb4;
--TRUNCATE TABLE spages;
INSERT INTO spages (parent_id, url, iname) VALUES
(0,'','Home') --1
,(0,'test-page','Test  page') --2
;
update spages set is_home=1 where id=1;



/*event types for log*/
DROP TABLE IF EXISTS events;
CREATE TABLE events (
  id                    INT UNSIGNED NOT NULL auto_increment,
  icode                 VARCHAR(64) NOT NULL default '',

  iname                 VARCHAR(255) NOT NULL default '',
  idesc                 TEXT,

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_events_icode (icode)
) DEFAULT CHARSET=utf8mb4;

/* log of all user-initiated events */
DROP TABLE IF EXISTS event_log;
CREATE TABLE event_log (
  id                    BIGINT UNSIGNED NOT NULL auto_increment,
  events_id             INT UNSIGNED NOT NULL,           /* event id */

  item_id               INT NOT NULL DEFAULT 0,           /*related id*/
  item_id2              INT NOT NULL DEFAULT 0,           /*related id (if another)*/

  iname                 VARCHAR(255) NOT NULL DEFAULT '', /*short description of what's happened or additional data*/

  records_affected      INT NOT NULL DEFAULT 0,
  fields                TEXT,       /*serialized json with related fields data (for history) in form {fieldname: data, fieldname: data}*/

  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,  /*date record added*/
  add_users_id          INT UNSIGNED DEFAULT 0,                        /*user added record, 0 if sent by cron module*/

  PRIMARY KEY (id),
  KEY IX_event_log_events_id (events_id),
  KEY IX_event_log_item_id (item_id),
  KEY IX_event_log_item_id2 (item_id2),
  KEY IX_event_log_add_users_id (add_users_id),
  KEY IX_event_log_add_time (add_time),
  FOREIGN KEY (events_id) REFERENCES events(id)
) DEFAULT CHARSET=utf8mb4;

/*Lookup Manager Tables*/
DROP TABLE IF EXISTS lookup_manager_tables;
CREATE TABLE lookup_manager_tables (
  id                    INT UNSIGNED NOT NULL auto_increment,

  tname                 VARCHAR(255) NOT NULL DEFAULT '', /*table name*/
  iname                 VARCHAR(255) NOT NULL DEFAULT '', /*human table name*/
  idesc                 TEXT,                     /*table internal description*/

  is_one_form           TINYINT NOT NULL DEFAULT 0,        /*1 - lookup table cotains one row, use form view*/
  is_custom_form        TINYINT NOT NULL DEFAULT 0,        /*1 - use custom form template, named by lowercase(tname)*/
  header_text           TEXT,                     /*text to show in header when editing table*/
  footer_text           TEXT,                     /*text to show in footer when editing table*/
  column_id             VARCHAR(255),                     /*table id column, if empty - use id*/

  list_columns          TEXT,                     /*comma-separated field list to display on list view, if defined - bo table edit mode available*/
  columns               TEXT,                     /*comma-separated field list to display, if empty - all fields displayed*/
  column_names          TEXT,                     /*comma-separated column list of column names, if empty - use field name*/
  column_types          TEXT,                     /*comma-separated column list of column types/lookups (" "-string(default),readonly,textarea,checkbox,tname.IDfield:INAMEfield-lookup table), if empty - use standard input[text]*/
  column_groups         TEXT,                     /*comma-separated column list of groups column related to, if empty - don't include column in group*/
  url                   VARCHAR(255) NOT NULL DEFAULT '', /*if defined - redirected to this URL instead of LookupManager forms*/

  status                TINYINT NOT NULL DEFAULT 0,                /*0-ok, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,  /*date record added*/
  add_users_id          INT UNSIGNED DEFAULT 0,                        /*user added record*/
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_lookup_manager_tables_tname (tname)
) DEFAULT CHARSET=utf8mb4;

/*user custom views*/
DROP TABLE IF EXISTS user_views;
CREATE TABLE user_views (
  id                    INT UNSIGNED NOT NULL auto_increment,
  icode                 VARCHAR(128) NOT NULL, --related screen url, ex: "/Admin/Demos"
  fields                TEXT, -- comma-separated list of fields to display, order kept

  iname                 VARCHAR(255) NOT NULL DEFAULT '', -- if empty - it's a "default" view
  is_system             TINYINT NOT NULL DEFAULT 0, -- 1 - system - visible for all
  is_shared             TINYINT NOT NULL DEFAULT 0, -- 1 if shared/published

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0, -- related user
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  KEY UX_user_views (add_users_id, icode, iname)
) DEFAULT CHARSET=utf8mb4;

/*user lists*/
DROP TABLE IF EXISTS user_lists;
CREATE TABLE user_lists (
  id                    INT UNSIGNED NOT NULL auto_increment,
  entity                VARCHAR(128) NOT NULL, -- usually table name or base_url, ex: 'demos' or /Admin/Demos

  iname                 VARCHAR(255) NOT NULL,
  idesc                 TEXT, -- description

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0, -- related owner user
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  KEY IX_user_lists (add_users_id, entity)
) DEFAULT CHARSET=utf8mb4;

/*items linked to user lists */
DROP TABLE IF EXISTS user_lists_items;
CREATE TABLE user_lists_items (
  id                    INT UNSIGNED NOT NULL auto_increment,
  user_lists_id         INT UNSIGNED NOT NULL,
  item_id               INT UNSIGNED NOT NULL, -- related item id, example demos.id

  status                TINYINT NOT NULL DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0, -- related owner user
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  KEY UX_user_lists_items (user_lists_id, item_id),
  FOREIGN KEY (user_lists_id) REFERENCES user_lists(id)
) DEFAULT CHARSET=utf8mb4;

/*Custom menu items for sidebar*/
DROP TABLE IF EXISTS menu_items;
CREATE TABLE menu_items (
  id                    INT UNSIGNED NOT NULL auto_increment,

  iname                 VARCHAR(64) NOT NULL default '',
  url                   VARCHAR(255) NOT NULL default '',  -- menu url
  icon                  VARCHAR(64) NOT NULL default '',   -- menu icon
  controller            VARCHAR(255) NOT NULL default '',  -- controller class name for UI highlighting
  access_level          TINYINT NOT NULL DEFAULT 0,         -- min access level

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 10-hidden, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id)
) DEFAULT CHARSET=utf8mb4;
-- INSERT INTO menu_items (iname, url, icon, controller) VALUES ('Test Menu Item', '/Admin/Demos', 'list-ul', 'AdminDemos');

DROP TABLE IF EXISTS user_filters;
CREATE TABLE user_filters (
  id                    INT UNSIGNED NOT NULL auto_increment,
  icode                 VARCHAR(128) NOT NULL, -- related screen, ex: GLOBAL[controller.action]

  iname                 VARCHAR(255) NOT NULL,
  idesc                 TEXT, -- json with filter data
  is_system             TINYINT NOT NULL DEFAULT 0, -- 1 - system - visible for all
  is_shared             TINYINT NOT NULL DEFAULT 0, -- 1 if shared/published

  status                TINYINT DEFAULT 0,
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0, -- related owner user
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id)
) DEFAULT CHARSET=utf8mb4;

-- after this file - run lookups.sql
