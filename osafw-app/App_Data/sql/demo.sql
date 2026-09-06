-- demo tables, use for reference/development, remove when not required

/*Demo Dictionary table*/
DROP TABLE IF EXISTS demo_dicts;
CREATE TABLE demo_dicts (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

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
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  parent_id             INT NOT NULL DEFAULT 0,           /*parent id - combo selection from SQL*/
  demo_dicts_id         INT NULL FOREIGN KEY REFERENCES demo_dicts(id),           /* demo dictionary link*/

  icode                 NVARCHAR(32) NOT NULL DEFAULT '', /*project-style code used by the computed display name*/
  iname                 NVARCHAR(64) NOT NULL DEFAULT '', /*string value for names*/
  display_name          AS CAST(CONCAT(icode, CASE WHEN icode <> '' AND iname <> '' THEN N' — ' ELSE N'' END, iname) AS NVARCHAR(128)) PERSISTED,
  idesc                 NVARCHAR(MAX),                    /*large text value*/

  email                 NVARCHAR(128) NOT NULL DEFAULT '',/*string value for unique field, such as email*/

  fint                  INT NOT NULL DEFAULT 0,           /*accept only INT*/
  ffloat                FLOAT NOT NULL DEFAULT 0,         /*accept float digital values*/
  frange                INT NOT NULL DEFAULT 50,          /*range slider value 0-100*/

  dict_link_auto_id     INT NOT NULL DEFAULT 0,           /*index of autocomplete field - linked to demo_dicts*/
  dict_link_multi       NVARCHAR(255) NOT NULL DEFAULT '',    /*multiple select values, link to demo_dicts*/

  fcombo                INT NOT NULL DEFAULT 0,           /*index of combo selection*/
  fradio                INT NOT NULL DEFAULT 0,           /*index of radio selection*/
  fyesno                BIT NOT NULL DEFAULT 0,           /*yes/no field 0 - NO, 1 - YES*/
  is_checkbox           TINYINT NOT NULL DEFAULT 0,       /*checkbox field 0 - not set, 1 - set*/
  is_switch             TINYINT NOT NULL DEFAULT 0,       /*switch field 0 - off, 1 - on*/

  fdate_combo           DATE,                             /*date field with 3 combos editing*/
  fdate_pop             DATE,                             /*date field with popup editing*/
  fdatetime             DATETIME2,                         /*date+time field*/
  fdatetime_utc         DATETIME2,                         /*UTC instant marked by _utc suffix*/
  fdatetime_offset      DATETIMEOFFSET,                    /*offset-aware instant*/
  fdatetime_local       DATETIME2,                         /*browser datetime-local input demo*/
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

/*junction table*/
DROP TABLE IF EXISTS demos_demo_dicts;
CREATE TABLE demos_demo_dicts (
  demos_id              INT NULL FOREIGN KEY REFERENCES demos(id),
  demo_dicts_id         INT NULL FOREIGN KEY REFERENCES demo_dicts(id),

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under change, deleted instantly*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX IX_demos_demo_dicts_demos_id (demos_id, demo_dicts_id),
  INDEX IX_demos_demo_dicts_demo_dicts_id (demo_dicts_id, demos_id)
);

/*subtable for demo items*/
DROP TABLE IF EXISTS demos_items;
CREATE TABLE demos_items (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  demos_id              INT NOT NULL FOREIGN KEY REFERENCES demos(id), -- main record link

  demo_dicts_id         INT NULL FOREIGN KEY REFERENCES demo_dicts(id), -- item lookup
  iname                 NVARCHAR(64) NOT NULL DEFAULT '', /*string value for names*/
  idesc                 NVARCHAR(MAX),                    /*large text value*/
  is_checkbox           TINYINT NOT NULL DEFAULT 0,       /*checkbox field 0 - not set, 1 - set*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under change, deleted instantly*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX IX_demos_items_demos_id (demos_id),
  INDEX IX_demos_items_demo_dicts_id (demo_dicts_id, demos_id)
);




/*
TEST DATA
INSERT statements for demos table
*/
INSERT INTO demos (parent_id, demo_dicts_id, icode, iname, idesc, email, fint, ffloat, frange, dict_link_auto_id, dict_link_multi, fcombo, fradio, fyesno, is_checkbox, is_switch, fdate_combo, fdate_pop, fdatetime, fdatetime_utc, fdatetime_offset, fdatetime_local, ftime, att_id, status, add_time, add_users_id)
SELECT TOP 100
  ABS(CHECKSUM(NEWID())) % 10,    -- random parent_id between 0 and 9
  ABS(CHECKSUM(NEWID())) % 3 + 1, -- random demo_dicts_id between 1 and 3
  CONCAT('DEMO-', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), -- sequential project-style code
  CONCAT('Name', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), -- sequential name
  CONCAT('Description', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), -- sequential description
  CONCAT('email', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), -- sequential email
  ABS(CHECKSUM(NEWID())) % 1000, -- random fint between 0 and 999
  ABS(CHECKSUM(NEWID())) % 1000 + RAND(), -- random ffloat between 0 and 1000
  ABS(CHECKSUM(NEWID())) % 101, -- random frange between 0 and 100
  ABS(CHECKSUM(NEWID())) % 3 + 1, -- random dict_link_auto_id between 1 and 3
  CONCAT('LinkMulti', ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), -- sequential dict_link_multi
  ABS(CHECKSUM(NEWID())) % 3 + 1, -- random fcombo between 1 and 3
  ABS(CHECKSUM(NEWID())) % 3 + 1, -- random fradio between 1 and 3
  ABS(CHECKSUM(NEWID())) % 2,    -- random fyesno either 0 or 1
  ABS(CHECKSUM(NEWID())) % 2,    -- random is_checkbox either 0 or 1
  ABS(CHECKSUM(NEWID())) % 2,    -- random is_switch either 0 or 1
  DATEFROMPARTS(2023, ABS(CHECKSUM(NEWID())) % 12 + 1, ABS(CHECKSUM(NEWID())) % 28 + 1), -- random fdate_combo between Jan 1, 2023 and Dec 31, 2023
  DATEFROMPARTS(2023, ABS(CHECKSUM(NEWID())) % 12 + 1, ABS(CHECKSUM(NEWID())) % 28 + 1), -- random fdate_pop between Jan 1, 2023 and Dec 31, 2023
  DATEADD(MINUTE, ABS(CHECKSUM(NEWID())) % 1440, CONVERT(DATETIME2, GETDATE())), -- random fdatetime within 24 hours of current datetime
  DATEADD(MINUTE, ABS(CHECKSUM(NEWID())) % 1440, CONVERT(DATETIME2, SYSUTCDATETIME())), -- random UTC datetime
  TODATETIMEOFFSET(DATEADD(MINUTE, ABS(CHECKSUM(NEWID())) % 1440, CONVERT(DATETIME2, SYSUTCDATETIME())), '+00:00'), -- random UTC datetimeoffset
  DATEADD(MINUTE, ABS(CHECKSUM(NEWID())) % 1440, CONVERT(DATETIME2, GETDATE())), -- random browser datetime-local sample
  ABS(CHECKSUM(NEWID())) % 86400, -- random ftime between 0 and 86400 (seconds in a day)
  NULL, -- NULL for att_id due to foreign key
  0, -- status = 0 (ok)
  GETDATE(), -- current datetime for add_time
  1 -- add_users_id = 1 (arbitrary user ID)
FROM sys.all_objects a
CROSS JOIN sys.all_objects b
OPTION (MAXDOP 1); -- single-threaded to avoid duplicate rows due to parallelism

/* CMS demonstration drafts */
-- Existing URLs and the homepage are preserved; these inserts can be repeated safely.
-- Publish the demo-help snippet before publishing either sample page that includes it.
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, N'demo-help', N'How can we help?', N'article', 10, 0, 1, 1, N'Help', N'{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"callout","data":{"title":"Let''s find the right next step","text":"Tell us what you need. Our team will connect you with the right person."}},{"type":"button","data":{"text":"Contact our team","url":"/Contact"}}]}},"slots":{}}', N'{"iname":"How can we help?","parent_id":0,"url":"demo-help","head_att_id":0,"template":"article","prio":10,"meta_keywords":"","meta_description":"","meta_title":"","custom_head":"","custom_css":"","custom_js":"","redirect_url":"","url_aliases":"","content_json":"{\"schemaVersion\":1,\"regions\":{\"main\":{\"blocks\":[{\"type\":\"callout\",\"data\":{\"title\":\"Let''s find the right next step\",\"text\":\"Tell us what you need. Our team will connect you with the right person.\"}},{\"type\":\"button\",\"data\":{\"text\":\"Contact our team\",\"url\":\"/Contact\"}}]}},\"slots\":{}}","access_level":0,"is_nav_visible":1,"nav_title":"Help","is_noindex":0,"status":10,"is_home":0,"is_snippet":1}', 10
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)=N'demo-help'
    OR LOWER(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json ELSE NULL END, '$.url'))=N'demo-help'
);

INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, N'demo-services', N'Good work starts with a clear plan', N'landing', 20, 0, 0, 1, N'Our services', N'{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"Practical expertise. Thoughtful service. We help ambitious teams turn complex challenges into useful, lasting improvements."}},{"type":"button","data":{"text":"Explore our services","url":"#services"}},{"type":"header","data":{"text":"Support at every stage","level":2,"anchor":"services"}},{"type":"cards","data":{"items":[{"title":"Understand","text":"Find clarity through focused discovery, research, and a shared view of success."},{"title":"Create","text":"Build practical solutions around the people who will use them every day."},{"title":"Improve","text":"Keep learning, measure what matters, and make the next iteration better."}]}},{"type":"quote","data":{"text":"A good partnership makes the next step feel possible.","caption":"Our approach"}},{"type":"snippet","data":{"key":"demo-help"}}]}},"slots":{}}', N'{"iname":"Good work starts with a clear plan","parent_id":0,"url":"demo-services","head_att_id":0,"template":"landing","prio":20,"meta_keywords":"","meta_description":"","meta_title":"","custom_head":"","custom_css":"","custom_js":"","redirect_url":"","url_aliases":"","content_json":"{\"schemaVersion\":1,\"regions\":{\"main\":{\"blocks\":[{\"type\":\"paragraph\",\"data\":{\"text\":\"Practical expertise. Thoughtful service. We help ambitious teams turn complex challenges into useful, lasting improvements.\"}},{\"type\":\"button\",\"data\":{\"text\":\"Explore our services\",\"url\":\"#services\"}},{\"type\":\"header\",\"data\":{\"text\":\"Support at every stage\",\"level\":2,\"anchor\":\"services\"}},{\"type\":\"cards\",\"data\":{\"items\":[{\"title\":\"Understand\",\"text\":\"Find clarity through focused discovery, research, and a shared view of success.\"},{\"title\":\"Create\",\"text\":\"Build practical solutions around the people who will use them every day.\"},{\"title\":\"Improve\",\"text\":\"Keep learning, measure what matters, and make the next iteration better.\"}]}},{\"type\":\"quote\",\"data\":{\"text\":\"A good partnership makes the next step feel possible.\",\"caption\":\"Our approach\"}},{\"type\":\"snippet\",\"data\":{\"key\":\"demo-help\"}}]}},\"slots\":{}}","access_level":0,"is_nav_visible":1,"nav_title":"Our services","is_noindex":0,"status":10,"is_home":0,"is_snippet":0}', 10
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)=N'demo-services'
    OR LOWER(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json ELSE NULL END, '$.url'))=N'demo-services'
);

INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, N'demo-department', N'People, resources, and a place to start', N'sidebar-right', 30, 0, 0, 1, N'Department resources', N'{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"Your starting point for the people, guidance, and everyday resources that help our department do its best work."}},{"type":"header","data":{"text":"Start here","level":2}},{"type":"cards","data":{"items":[{"title":"New to the team?","text":"Get oriented with a simple checklist, team introductions, and the tools you need."},{"title":"Planning a project","text":"Use our shared guidance to define the outcome, find support, and prepare your next step."}]}},{"type":"table","data":{"caption":"Where to find support","withHeadings":true,"content":[["Need","First step"],["Getting access","Contact the service desk"],["Project advice","Speak with your team lead"]]}},{"type":"snippet","data":{"key":"demo-help"}}]},"right":{"blocks":[{"type":"callout","data":{"title":"Keep this page useful","text":"Found a gap or an outdated resource? Let the department editor know."}}]}},"slots":{}}', N'{"iname":"People, resources, and a place to start","parent_id":0,"url":"demo-department","head_att_id":0,"template":"sidebar-right","prio":30,"meta_keywords":"","meta_description":"","meta_title":"","custom_head":"","custom_css":"","custom_js":"","redirect_url":"","url_aliases":"","content_json":"{\"schemaVersion\":1,\"regions\":{\"main\":{\"blocks\":[{\"type\":\"paragraph\",\"data\":{\"text\":\"Your starting point for the people, guidance, and everyday resources that help our department do its best work.\"}},{\"type\":\"header\",\"data\":{\"text\":\"Start here\",\"level\":2}},{\"type\":\"cards\",\"data\":{\"items\":[{\"title\":\"New to the team?\",\"text\":\"Get oriented with a simple checklist, team introductions, and the tools you need.\"},{\"title\":\"Planning a project\",\"text\":\"Use our shared guidance to define the outcome, find support, and prepare your next step.\"}]}},{\"type\":\"table\",\"data\":{\"caption\":\"Where to find support\",\"withHeadings\":true,\"content\":[[\"Need\",\"First step\"],[\"Getting access\",\"Contact the service desk\"],[\"Project advice\",\"Speak with your team lead\"]]}},{\"type\":\"snippet\",\"data\":{\"key\":\"demo-help\"}}]},\"right\":{\"blocks\":[{\"type\":\"callout\",\"data\":{\"title\":\"Keep this page useful\",\"text\":\"Found a gap or an outdated resource? Let the department editor know.\"}}]}},\"slots\":{}}","access_level":0,"is_nav_visible":1,"nav_title":"Department resources","is_noindex":0,"status":10,"is_home":0,"is_snippet":0}', 10
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)=N'demo-department'
    OR LOWER(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json ELSE NULL END, '$.url'))=N'demo-department'
);
