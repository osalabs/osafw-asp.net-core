-- ActivityLogs update

-- create new tables

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

-- migrate data from events/event_log
-- events -> fwentities
INSERT INTO fwentities (icode, iname, add_users_id, upd_users_id)
SELECT
  extracted_icode,
  MAX(extracted_icode) AS iname, -- Using aggregation to avoid duplicates
  MAX(add_users_id) AS add_users_id,
  MAX(upd_users_id) AS upd_users_id
FROM (
    SELECT
      LEFT(icode, LEN(icode) - CHARINDEX('_', REVERSE(icode))) AS extracted_icode,
      add_users_id,
      upd_users_id
    FROM events
    WHERE icode LIKE '%_%'
      AND icode NOT IN ('login', 'logoff', 'login_fail', 'chpwd')
) AS subquery
WHERE NOT EXISTS (
    SELECT 1
    FROM fwentities
    WHERE icode = subquery.extracted_icode
)
GROUP BY extracted_icode;

-- event_log+events -> activity_logs
INSERT INTO activity_logs (log_types_id, fwentities_id, item_id, idate, users_id, idesc, payload, status, add_users_id, upd_time)
SELECT
  -- Determine log_types_id
  CASE
    WHEN RIGHT(ev.icode, 4) = '_add' THEN (SELECT id FROM log_types WHERE icode = 'added')
    WHEN RIGHT(ev.icode, 4) = '_upd' THEN (SELECT id FROM log_types WHERE icode = 'updated')
    WHEN RIGHT(ev.icode, 4) = '_del' THEN (SELECT id FROM log_types WHERE icode = 'deleted')
    ELSE (SELECT id FROM log_types WHERE icode = ev.icode)
  END AS log_types_id,

  -- Determine fwentities_id
  CASE
    WHEN ev.icode IN ('login', 'logoff', 'login_fail', 'chpwd') THEN (SELECT id FROM fwentities WHERE icode = 'users')
    ELSE (SELECT id FROM fwentities WHERE icode = LEFT(ev.icode, LEN(ev.icode) - CHARINDEX('_', REVERSE(ev.icode))))
  END AS fwentities_id,

  el.item_id,
  el.add_time AS idate,
  NULLIF(el.add_users_id,0) AS users_id,
  el.iname,
  el.fields AS payload,
  0 AS status, -- Assuming default status
  el.add_users_id,
  el.add_time AS upd_time -- Using add_time in place of upd_users_id
FROM event_log el
INNER JOIN events ev ON el.events_id = ev.id;

-- drop deprecated tables
DROP TABLE event_log;
DROP TABLE events;
GO
