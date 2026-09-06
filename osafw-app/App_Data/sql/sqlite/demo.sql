-- SQLite demo tables, use for reference/development, remove when not required

PRAGMA foreign_keys = OFF;

DROP TABLE IF EXISTS demos_items;
DROP TABLE IF EXISTS demos_demo_dicts;
DROP TABLE IF EXISTS demos;
DROP TABLE IF EXISTS demo_dicts;

/* Demo Dictionary table */
CREATE TABLE demo_dicts (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);

INSERT INTO demo_dicts (iname, idesc, add_time) VALUES
('test1', 'test1 description', CURRENT_TIMESTAMP),
('test2', 'test2 description', CURRENT_TIMESTAMP),
('test3', 'test3 description', CURRENT_TIMESTAMP);

/* Demo table */
CREATE TABLE demos (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  parent_id             INTEGER NOT NULL DEFAULT 0,
  demo_dicts_id         INTEGER NULL REFERENCES demo_dicts(id),

  icode                 TEXT NOT NULL DEFAULT '',
  iname                 TEXT NOT NULL DEFAULT '',
  display_name          TEXT GENERATED ALWAYS AS (icode || CASE WHEN icode <> '' AND iname <> '' THEN ' — ' ELSE '' END || iname) STORED,
  idesc                 TEXT,

  email                 TEXT NOT NULL DEFAULT '',

  fint                  INTEGER NOT NULL DEFAULT 0,
  ffloat                REAL NOT NULL DEFAULT 0,
  frange                INTEGER NOT NULL DEFAULT 50,

  dict_link_auto_id     INTEGER NOT NULL DEFAULT 0,
  dict_link_multi       TEXT NOT NULL DEFAULT '',

  fcombo                INTEGER NOT NULL DEFAULT 0,
  fradio                INTEGER NOT NULL DEFAULT 0,
  fyesno                INTEGER NOT NULL DEFAULT 0,
  is_checkbox           INTEGER NOT NULL DEFAULT 0,
  is_switch             INTEGER NOT NULL DEFAULT 0,

  fdate_combo           DATE,
  fdate_pop             DATE,
  fdatetime             DATETIME,
  fdatetime_utc         DATETIME,
  fdatetime_offset      DATETIMEOFFSET,
  fdatetime_local       DATETIME,
  ftime                 INTEGER NOT NULL DEFAULT 0,

  att_id                INTEGER NULL REFERENCES att(id),

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_demos_email ON demos (email);
CREATE INDEX IX_demos_demo_dicts_id ON demos (demo_dicts_id);
CREATE INDEX IX_demos_dict_link_auto_id ON demos (dict_link_auto_id);

/* junction table */
CREATE TABLE demos_demo_dicts (
  demos_id              INTEGER NULL REFERENCES demos(id),
  demo_dicts_id         INTEGER NULL REFERENCES demo_dicts(id),

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_demos_demo_dicts_demos_id ON demos_demo_dicts (demos_id, demo_dicts_id);
CREATE INDEX IX_demos_demo_dicts_demo_dicts_id ON demos_demo_dicts (demo_dicts_id, demos_id);

/* subtable for demo items */
CREATE TABLE demos_items (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  demos_id              INTEGER NOT NULL REFERENCES demos(id),

  demo_dicts_id         INTEGER NULL REFERENCES demo_dicts(id),
  iname                 TEXT NOT NULL DEFAULT '',
  idesc                 TEXT,
  is_checkbox           INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE INDEX IX_demos_items_demos_id ON demos_items (demos_id);
CREATE INDEX IX_demos_items_demo_dicts_id ON demos_items (demo_dicts_id, demos_id);

PRAGMA foreign_keys = ON;

/*
TEST DATA
INSERT statements for demos table
*/
WITH RECURSIVE nums(n) AS (
  SELECT 1
  UNION ALL
  SELECT n + 1 FROM nums WHERE n < 50
)
INSERT INTO demos (
  parent_id, demo_dicts_id, icode, iname, idesc, email, fint, ffloat,
  frange, dict_link_auto_id, dict_link_multi, fcombo, fradio, fyesno, is_checkbox, is_switch,
  fdate_combo, fdate_pop, fdatetime, fdatetime_utc, fdatetime_offset,
  fdatetime_local, ftime, att_id, status, add_time, add_users_id
)
SELECT
  abs(random()) % 10,
  abs(random()) % 3 + 1,
  'DEMO-' || n,
  'Name' || n,
  'Description' || n,
  'email' || n,
  abs(random()) % 1000,
  (abs(random()) % 1000) + (abs(random()) % 1000) / 1000.0,
  abs(random()) % 101,
  abs(random()) % 3 + 1,
  'LinkMulti' || n,
  abs(random()) % 3 + 1,
  abs(random()) % 3 + 1,
  abs(random()) % 2,
  abs(random()) % 2,
  abs(random()) % 2,
  date('2023-01-01', '+' || (abs(random()) % 365) || ' days'),
  date('2023-01-01', '+' || (abs(random()) % 365) || ' days'),
  datetime('now', '+' || (abs(random()) % 1440) || ' minutes'),
  datetime('now', '+' || (abs(random()) % 1440) || ' minutes'),
  strftime('%Y-%m-%dT%H:%M:%SZ', 'now', '+' || (abs(random()) % 1440) || ' minutes'),
  datetime('now', '+' || (abs(random()) % 1440) || ' minutes'),
  abs(random()) % 86400,
  NULL,
  0,
  CURRENT_TIMESTAMP,
  1
FROM nums;

/* CMS demonstration drafts */
-- Existing URLs and the homepage are preserved; these inserts can be repeated safely.
-- Publish the demo-help snippet before publishing either sample page that includes it.
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, workflow, status)
SELECT 0, 'demo-help', 'How can we help?', 'article', 10, 0, 1, 1, 'Help', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"callout","data":{"title":"Let''s find the right next step","text":"Tell us what you need. Our team will connect you with the right person."}},{"type":"button","data":{"text":"Contact our team","url":"/Contact"}}]}},"slots":{}}', '{"iname":"How can we help?","parent_id":0,"url":"demo-help","head_att_id":0,"template":"article","prio":10,"meta_keywords":"","meta_description":"","meta_title":"","custom_head":"","custom_css":"","custom_js":"","redirect_url":"","url_aliases":"","content_json":"{\"schemaVersion\":1,\"regions\":{\"main\":{\"blocks\":[{\"type\":\"callout\",\"data\":{\"title\":\"Let''s find the right next step\",\"text\":\"Tell us what you need. Our team will connect you with the right person.\"}},{\"type\":\"button\",\"data\":{\"text\":\"Contact our team\",\"url\":\"/Contact\"}}]}},\"slots\":{}}","access_level":0,"is_nav_visible":1,"nav_title":"Help","is_noindex":0,"status":10,"is_home":0,"is_snippet":1}', 0, 10
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)='demo-help'
    OR LOWER(json_extract(CASE WHEN json_valid(draft_json) THEN draft_json ELSE NULL END, '$.url'))='demo-help'
);

INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, workflow, status)
SELECT 0, 'demo-services', 'Good work starts with a clear plan', 'landing', 20, 0, 0, 1, 'Our services', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"Practical expertise. Thoughtful service. We help ambitious teams turn complex challenges into useful, lasting improvements."}},{"type":"button","data":{"text":"Explore our services","url":"#services"}},{"type":"header","data":{"text":"Support at every stage","level":2,"anchor":"services"}},{"type":"cards","data":{"items":[{"title":"Understand","text":"Find clarity through focused discovery, research, and a shared view of success."},{"title":"Create","text":"Build practical solutions around the people who will use them every day."},{"title":"Improve","text":"Keep learning, measure what matters, and make the next iteration better."}]}},{"type":"quote","data":{"text":"A good partnership makes the next step feel possible.","caption":"Our approach"}},{"type":"snippet","data":{"key":"demo-help"}}]}},"slots":{}}', '{"iname":"Good work starts with a clear plan","parent_id":0,"url":"demo-services","head_att_id":0,"template":"landing","prio":20,"meta_keywords":"","meta_description":"","meta_title":"","custom_head":"","custom_css":"","custom_js":"","redirect_url":"","url_aliases":"","content_json":"{\"schemaVersion\":1,\"regions\":{\"main\":{\"blocks\":[{\"type\":\"paragraph\",\"data\":{\"text\":\"Practical expertise. Thoughtful service. We help ambitious teams turn complex challenges into useful, lasting improvements.\"}},{\"type\":\"button\",\"data\":{\"text\":\"Explore our services\",\"url\":\"#services\"}},{\"type\":\"header\",\"data\":{\"text\":\"Support at every stage\",\"level\":2,\"anchor\":\"services\"}},{\"type\":\"cards\",\"data\":{\"items\":[{\"title\":\"Understand\",\"text\":\"Find clarity through focused discovery, research, and a shared view of success.\"},{\"title\":\"Create\",\"text\":\"Build practical solutions around the people who will use them every day.\"},{\"title\":\"Improve\",\"text\":\"Keep learning, measure what matters, and make the next iteration better.\"}]}},{\"type\":\"quote\",\"data\":{\"text\":\"A good partnership makes the next step feel possible.\",\"caption\":\"Our approach\"}},{\"type\":\"snippet\",\"data\":{\"key\":\"demo-help\"}}]}},\"slots\":{}}","access_level":0,"is_nav_visible":1,"nav_title":"Our services","is_noindex":0,"status":10,"is_home":0,"is_snippet":0}', 0, 10
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)='demo-services'
    OR LOWER(json_extract(CASE WHEN json_valid(draft_json) THEN draft_json ELSE NULL END, '$.url'))='demo-services'
);

INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, workflow, status)
SELECT 0, 'demo-department', 'People, resources, and a place to start', 'sidebar-right', 30, 0, 0, 1, 'Department resources', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"Your starting point for the people, guidance, and everyday resources that help our department do its best work."}},{"type":"header","data":{"text":"Start here","level":2}},{"type":"cards","data":{"items":[{"title":"New to the team?","text":"Get oriented with a simple checklist, team introductions, and the tools you need."},{"title":"Planning a project","text":"Use our shared guidance to define the outcome, find support, and prepare your next step."}]}},{"type":"table","data":{"caption":"Where to find support","withHeadings":true,"content":[["Need","First step"],["Getting access","Contact the service desk"],["Project advice","Speak with your team lead"]]}},{"type":"snippet","data":{"key":"demo-help"}}]},"right":{"blocks":[{"type":"callout","data":{"title":"Keep this page useful","text":"Found a gap or an outdated resource? Let the department editor know."}}]}},"slots":{}}', '{"iname":"People, resources, and a place to start","parent_id":0,"url":"demo-department","head_att_id":0,"template":"sidebar-right","prio":30,"meta_keywords":"","meta_description":"","meta_title":"","custom_head":"","custom_css":"","custom_js":"","redirect_url":"","url_aliases":"","content_json":"{\"schemaVersion\":1,\"regions\":{\"main\":{\"blocks\":[{\"type\":\"paragraph\",\"data\":{\"text\":\"Your starting point for the people, guidance, and everyday resources that help our department do its best work.\"}},{\"type\":\"header\",\"data\":{\"text\":\"Start here\",\"level\":2}},{\"type\":\"cards\",\"data\":{\"items\":[{\"title\":\"New to the team?\",\"text\":\"Get oriented with a simple checklist, team introductions, and the tools you need.\"},{\"title\":\"Planning a project\",\"text\":\"Use our shared guidance to define the outcome, find support, and prepare your next step.\"}]}},{\"type\":\"table\",\"data\":{\"caption\":\"Where to find support\",\"withHeadings\":true,\"content\":[[\"Need\",\"First step\"],[\"Getting access\",\"Contact the service desk\"],[\"Project advice\",\"Speak with your team lead\"]]}},{\"type\":\"snippet\",\"data\":{\"key\":\"demo-help\"}}]},\"right\":{\"blocks\":[{\"type\":\"callout\",\"data\":{\"title\":\"Keep this page useful\",\"text\":\"Found a gap or an outdated resource? Let the department editor know.\"}}]}},\"slots\":{}}","access_level":0,"is_nav_visible":1,"nav_title":"Department resources","is_noindex":0,"status":10,"is_home":0,"is_snippet":0}', 0, 10
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)='demo-department'
    OR LOWER(json_extract(CASE WHEN json_valid(draft_json) THEN draft_json ELSE NULL END, '$.url'))='demo-department'
);
