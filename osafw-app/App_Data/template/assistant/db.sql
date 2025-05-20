DROP TABLE IF EXISTS users;
CREATE TABLE users (
  id                    INT NOT NULL auto_increment,

  email                 VARCHAR(128) NOT NULL DEFAULT '',
  access_level          TINYINT NOT NULL,                 -- 0 - visitor, 1 - usual user, 80 - moderator, 100 - admin

  iname                 AS CONCAT(fname,' ', lname) STORED, -- standard iname field, calculated from first+last name
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
  att_id                INT NULL,                -- avatar

  login_time            TIMESTAMP,

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY UX_users_email (email),
  FOREIGN KEY (att_id) REFERENCES att(id)
) DEFAULT CHARSET=utf8mb4;
