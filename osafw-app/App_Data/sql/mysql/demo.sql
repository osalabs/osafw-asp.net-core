-- MySQL style
-- demo tables, use for reference/development, remove when not required

/*Demo Dictionary table*/
DROP TABLE IF EXISTS demo_dicts;
CREATE TABLE demo_dicts (
  id                    INT UNSIGNED NOT NULL auto_increment,

  iname                 VARCHAR(64) NOT NULL default '',
  idesc                 TEXT,
  prio                  INT NOT NULL DEFAULT 0,     /*0-on insert, then =id, default order by prio asc,iname*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id)
  -- UNIQUE KEY (iname)
) DEFAULT CHARSET=utf8mb4;
INSERT INTO demo_dicts (iname, idesc) VALUES
('test1', 'test1 description')
,('test2', 'test2 description')
,('test3', 'test3 description')
;

/*Demo table*/
DROP TABLE IF EXISTS demos;
CREATE TABLE demos (
  id                    INT UNSIGNED NOT NULL auto_increment,
  parent_id             INT UNSIGNED NOT NULL DEFAULT 0,           /*parent id - combo selection from SQL*/
  demo_dicts_id         INT UNSIGNED NULL,                         /* demo dictionary link*/

  iname                 VARCHAR(64) NOT NULL DEFAULT '',  /*string value for names*/
  idesc                 TEXT,                             /*large text value*/

  email                 VARCHAR(128) NOT NULL DEFAULT '', /*string value for unique field, such as email*/

  fint                  INT NOT NULL DEFAULT 0,           /*accept only INT*/
  ffloat                FLOAT NOT NULL DEFAULT 0,         /*accept float digital values*/

  dict_link_auto_id     INT UNSIGNED NOT NULL DEFAULT 0,  /*index of autocomplete field - linked to demo_dicts*/
  dict_link_multi       VARCHAR(255) NOT NULL DEFAULT '', /*multiple select values, link to demo_dicts*/

  fcombo                INT UNSIGNED NOT NULL DEFAULT 0,  /*index of combo selection*/
  fradio                INT UNSIGNED NOT NULL DEFAULT 0,  /*index of radio selection*/
  fyesno                TINYINT UNSIGNED NOT NULL DEFAULT 0, /*yes/no field 0 - NO, 1 - YES*/
  is_checkbox           TINYINT UNSIGNED NOT NULL DEFAULT 0, /*checkbox field 0 - not set, 1 - set*/

  fdate_combo           DATE,                             /*date field with 3 combos editing*/
  fdate_pop             DATE,                             /*date field with popup editing*/
  fdatetime             DATETIME,                         /*date+time field*/
  ftime                 INT UNSIGNED NOT NULL DEFAULT 0,  /*time field - we always store time as seconds from start of the day [0-86400]*/

  att_id                INT UNSIGNED NULL,                /*optional attached image*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,  /*date record added*/
  add_users_id          INT UNSIGNED DEFAULT 0,                        /*user added record*/
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY (email),
  FOREIGN KEY (demo_dicts_id) REFERENCES demo_dicts(id),
  FOREIGN KEY (att_id) REFERENCES att(id)
) DEFAULT CHARSET=utf8mb4;

/*multi link table*/
DROP TABLE IF EXISTS demos_demo_dicts_link;
CREATE TABLE demos_demo_dicts_link (
  demos_id              INT UNSIGNED NULL FOREIGN KEY REFERENCES demos(id),
  demo_dicts_id         INT UNSIGNED NULL FOREIGN KEY REFERENCES demo_dicts(id),

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under change, deleted instantly*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT UNSIGNED DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT UNSIGNED DEFAULT 0,

  FOREIGN KEY (demos_id) REFERENCES demos(id),
  FOREIGN KEY (demo_dicts_id) REFERENCES demo_dicts(id)
) DEFAULT CHARSET=utf8mb4;
