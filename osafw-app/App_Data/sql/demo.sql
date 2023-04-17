-- demo tables, use for reference/development, remove when not required

/*Demo Dictionary table*/
DROP TABLE IF EXISTS demo_dicts;
CREATE TABLE demo_dicts (
  id INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  iname                 NVARCHAR(64) NOT NULL default '',
  idesc                 NVARCHAR(MAX),
  prio                  INT NOT NULL DEFAULT 0,     /*0-on insert, then =id, default order by prio asc,iname*/

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




/*
TEST DATA
INSERT statements for demos table
*/
INSERT INTO demos (parent_id, demo_dicts_id, iname, idesc, email, fint, ffloat, dict_link_auto_id, dict_link_multi, fcombo, fradio, fyesno, is_checkbox, fdate_combo, fdate_pop, fdatetime, ftime, att_id, status, add_time, add_users_id)
SELECT TOP 100
  ABS(CHECKSUM(NEWID())) % 10,    -- random parent_id between 0 and 9
  ABS(CHECKSUM(NEWID())) % 3 + 1, -- random demo_dicts_id between 1 and 3
  CONCAT('Name', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), -- sequential name
  CONCAT('Description', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), -- sequential description
  CONCAT('email', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), -- sequential email
  ABS(CHECKSUM(NEWID())) % 1000, -- random fint between 0 and 999
  ABS(CHECKSUM(NEWID())) % 1000 + RAND(), -- random ffloat between 0 and 1000
  ABS(CHECKSUM(NEWID())) % 3 + 1, -- random dict_link_auto_id between 1 and 3
  CONCAT('LinkMulti', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), -- sequential dict_link_multi
  ABS(CHECKSUM(NEWID())) % 3 + 1, -- random fcombo between 1 and 3
  ABS(CHECKSUM(NEWID())) % 3 + 1, -- random fradio between 1 and 3
  ABS(CHECKSUM(NEWID())) % 2,    -- random fyesno either 0 or 1
  ABS(CHECKSUM(NEWID())) % 2,    -- random is_checkbox either 0 or 1
  DATEFROMPARTS(2023, ABS(CHECKSUM(NEWID())) % 12 + 1, ABS(CHECKSUM(NEWID())) % 28 + 1), -- random fdate_combo between Jan 1, 2023 and Dec 31, 2023
  DATEFROMPARTS(2023, ABS(CHECKSUM(NEWID())) % 12 + 1, ABS(CHECKSUM(NEWID())) % 28 + 1), -- random fdate_pop between Jan 1, 2023 and Dec 31, 2023
  DATEADD(MINUTE, ABS(CHECKSUM(NEWID())) % 1440, CONVERT(DATETIME2, GETDATE())), -- random fdatetime within 24 hours of current datetime
  ABS(CHECKSUM(NEWID())) % 86400, -- random ftime between 0 and 86400 (seconds in a day)
  ABS(CHECKSUM(NEWID())) % 10 + 1, -- random att_id between 1 and 10
  0, -- status = 0 (ok)
  GETDATE(), -- current datetime for add_time
  1 -- add_users_id = 1 (arbitrary user ID)
FROM sys.all_objects a
CROSS JOIN sys.all_objects b
OPTION (MAXDOP 1); -- single-threaded to avoid duplicate rows due to parallelism
