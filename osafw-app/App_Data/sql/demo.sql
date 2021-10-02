-- demo tables, use for reference/development, remove when not required

/*Demo Dictionary table*/
DROP TABLE IF EXISTS demo_dicts;
CREATE TABLE demo_dicts (
  id INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  iname                 NVARCHAR(64) NOT NULL default '',
  idesc                 NVARCHAR(MAX),

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
INSERT INTO demo_dicts (iname, idesc, add_time) VALUES ('test1', 'test1 description', GETDATE());
INSERT INTO demo_dicts (iname, idesc, add_time) VALUES ('test2', 'test2 description', GETDATE());
INSERT INTO demo_dicts (iname, idesc, add_time) VALUES ('test3', 'test3 description', GETDATE());

/*Demo table*/
DROP TABLE IF EXISTS demos;
CREATE TABLE demos (
  id INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  parent_id             INT NOT NULL DEFAULT 0,           /*parent id - combo selection from SQL*/
  demo_dicts_id         INT NULL FOREIGN KEY REFERENCES demo_dicts(id),           /* demo dictionary link*/

  iname                 NVARCHAR(64) NOT NULL DEFAULT '', /*string value for names*/
  idesc                 NVARCHAR(MAX),                    /*large text value*/

  email                 NVARCHAR(128) NOT NULL DEFAULT '',/*string value for unique field, such as email*/

  fint                  INT NOT NULL DEFAULT 0,           /*accept only INT*/
  ffloat                FLOAT NOT NULL DEFAULT 0,         /*accept float digital values*/

  dict_link_auto_id     INT NOT NULL DEFAULT 0,           /*index of autocomplete field - linked to demo_dicts*/
  dict_link_multi       NVARCHAR(255) NOT NULL DEFAULT '',    /*multiple select values, link to demo_dicts*/

  fcombo                INT NOT NULL DEFAULT 0,           /*index of combo selection*/
  fradio                INT NOT NULL DEFAULT 0,           /*index of radio selection*/
  fyesno                BIT NOT NULL DEFAULT 0,           /*yes/no field 0 - NO, 1 - YES*/
  is_checkbox           TINYINT NOT NULL DEFAULT 0,       /*checkbox field 0 - not set, 1 - set*/

  fdate_combo           DATE,                             /*date field with 3 combos editing*/
  fdate_pop             DATE,                             /*date field with popup editing*/
  fdatetime             DATETIME2,                         /*date+time field*/
  ftime                 INT NOT NULL DEFAULT 0,           /*time field - we always store time as seconds from start of the day [0-86400]*/

  att_id                int NULL FOREIGN KEY REFERENCES att(id), /*optional attached image*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),  /*date record added*/
  add_users_id          INT DEFAULT 0,                        /*user added record*/
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX UX_demos_email UNIQUE (email),
  INDEX IX_demos_demo_dicts_id (demo_dicts_id),
  INDEX IX_demos_dict_link_auto_id (dict_link_auto_id)
);

/*multi link table*/
DROP TABLE IF EXISTS demos_demo_dicts_link;
CREATE TABLE demos_demo_dicts_link (
  demos_id              INT NULL FOREIGN KEY REFERENCES demos(id),
  demo_dicts_id         INT NULL FOREIGN KEY REFERENCES demo_dicts(id),

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under change, deleted instantly*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX IX_demos_demos_id (demos_id),
  INDEX IX_demos_demo_dicts_id (demo_dicts_id)
);
