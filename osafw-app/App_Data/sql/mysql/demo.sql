-- MySQL style
-- demo tables, use for reference/development, remove when not required

/*Demo Dictionary table*/
DROP TABLE IF EXISTS demo_dicts;
CREATE TABLE demo_dicts (
  id                    INT NOT NULL auto_increment,

  iname                 VARCHAR(64) NOT NULL default '',
  idesc                 TEXT,
  prio                  INT NOT NULL DEFAULT 0,     /*0-on insert, then =id, default order by prio asc,iname*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

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
  id                    INT NOT NULL auto_increment,
  parent_id             INT NOT NULL DEFAULT 0,           /*parent id - combo selection from SQL*/
  demo_dicts_id         INT NULL,                         /* demo dictionary link*/

  icode                 VARCHAR(32) NOT NULL DEFAULT '',  /*project-style code used by the computed display name*/
  iname                 VARCHAR(64) NOT NULL DEFAULT '',  /*string value for names*/
  display_name          VARCHAR(128) GENERATED ALWAYS AS (CONCAT(icode, CASE WHEN icode <> '' AND iname <> '' THEN ' — ' ELSE '' END, iname)) STORED,
  idesc                 TEXT,                             /*large text value*/

  email                 VARCHAR(128) NOT NULL DEFAULT '', /*string value for unique field, such as email*/

  fint                  INT NOT NULL DEFAULT 0,           /*accept only INT*/
  ffloat                FLOAT NOT NULL DEFAULT 0,         /*accept float digital values*/
  frange                INT NOT NULL DEFAULT 50,          /*range slider value 0-100*/

  dict_link_auto_id     INT NOT NULL DEFAULT 0,  /*index of autocomplete field - linked to demo_dicts*/
  dict_link_multi       VARCHAR(255) NOT NULL DEFAULT '', /*multiple select values, link to demo_dicts*/

  fcombo                INT NOT NULL DEFAULT 0,  /*index of combo selection*/
  fradio                INT NOT NULL DEFAULT 0,  /*index of radio selection*/
  fyesno                TINYINT NOT NULL DEFAULT 0, /*yes/no field 0 - NO, 1 - YES*/
  is_checkbox           TINYINT NOT NULL DEFAULT 0, /*checkbox field 0 - not set, 1 - set*/
  is_switch             TINYINT NOT NULL DEFAULT 0, /*switch field 0 - off, 1 - on*/

  fdate_combo           DATE,                             /*date field with 3 combos editing*/
  fdate_pop             DATE,                             /*date field with popup editing*/
  fdatetime             DATETIME,                         /*date+time field*/
  fdatetime_utc         DATETIME,                         /*UTC instant marked by _utc suffix*/
  fdatetime_offset      DATETIME,                         /*SQL Server datetimeoffset compatibility field*/
  fdatetime_local       DATETIME,                         /*browser datetime-local input demo*/
  ftime                 INT NOT NULL DEFAULT 0,  /*time field - we always store time as seconds from start of the day [0-86400]*/

  att_id                INT NULL,                /*optional attached image*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,  /*date record added*/
  add_users_id          INT DEFAULT 0,                        /*user added record*/
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (id),
  UNIQUE KEY (email),
  FOREIGN KEY (demo_dicts_id) REFERENCES demo_dicts(id),
  FOREIGN KEY (att_id) REFERENCES att(id)
) DEFAULT CHARSET=utf8mb4;

/*multi link table*/
DROP TABLE IF EXISTS demos_demo_dicts;
CREATE TABLE demos_demo_dicts (
  demos_id              INT NULL,
  demo_dicts_id         INT NULL,

  iname                 VARCHAR(64) NOT NULL DEFAULT '',  /*string value for names*/
  idesc                 TEXT,                             /*large text value*/
  is_checkbox           TINYINT NOT NULL DEFAULT 0, /*checkbox field 0 - not set, 1 - set*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under change, deleted instantly*/
  add_time              TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  add_users_id          INT DEFAULT 0,
  upd_time              TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  upd_users_id          INT DEFAULT 0,

  FOREIGN KEY (demos_id) REFERENCES demos(id),
  FOREIGN KEY (demo_dicts_id) REFERENCES demo_dicts(id)
) DEFAULT CHARSET=utf8mb4;

/* CMS demonstration drafts */
-- Existing URLs and the homepage are preserved; these inserts can be repeated safely.
-- Publish the demo-help snippet before publishing either sample page that includes it.
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, 'demo-help', 'How can we help?', 'article', 10, 0, 1, 1, 'Help', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"callout","data":{"title":"Let''s find the right next step","text":"Tell us what you need. Our team will connect you with the right person."}},{"type":"button","data":{"text":"Contact our team","url":"/Contact"}}]}},"slots":{}}', JSON_OBJECT(
    'iname', 'How can we help?',
    'parent_id', 0,
    'url', 'demo-help',
    'head_att_id', 0,
    'template', 'article',
    'prio', 10,
    'meta_keywords', '',
    'meta_description', '',
    'meta_title', '',
    'custom_head', '',
    'custom_css', '',
    'custom_js', '',
    'redirect_url', '',
    'url_aliases', '',
    'content_json', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"callout","data":{"title":"Let''s find the right next step","text":"Tell us what you need. Our team will connect you with the right person."}},{"type":"button","data":{"text":"Contact our team","url":"/Contact"}}]}},"slots":{}}',
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', 'Help',
    'is_noindex', 0,
    'status', 10,
    'is_home', 0,
    'is_snippet', 1
  ), 10
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)='demo-help'
    OR LOWER(JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.url')))='demo-help'
);

INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, 'demo-services', 'Good work starts with a clear plan', 'landing', 20, 0, 0, 1, 'Our services', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"Practical expertise. Thoughtful service. We help ambitious teams turn complex challenges into useful, lasting improvements."}},{"type":"button","data":{"text":"Explore our services","url":"#services"}},{"type":"header","data":{"text":"Support at every stage","level":2,"anchor":"services"}},{"type":"cards","data":{"items":[{"title":"Understand","text":"Find clarity through focused discovery, research, and a shared view of success."},{"title":"Create","text":"Build practical solutions around the people who will use them every day."},{"title":"Improve","text":"Keep learning, measure what matters, and make the next iteration better."}]}},{"type":"quote","data":{"text":"A good partnership makes the next step feel possible.","caption":"Our approach"}},{"type":"snippet","data":{"key":"demo-help"}}]}},"slots":{}}', JSON_OBJECT(
    'iname', 'Good work starts with a clear plan',
    'parent_id', 0,
    'url', 'demo-services',
    'head_att_id', 0,
    'template', 'landing',
    'prio', 20,
    'meta_keywords', '',
    'meta_description', '',
    'meta_title', '',
    'custom_head', '',
    'custom_css', '',
    'custom_js', '',
    'redirect_url', '',
    'url_aliases', '',
    'content_json', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"Practical expertise. Thoughtful service. We help ambitious teams turn complex challenges into useful, lasting improvements."}},{"type":"button","data":{"text":"Explore our services","url":"#services"}},{"type":"header","data":{"text":"Support at every stage","level":2,"anchor":"services"}},{"type":"cards","data":{"items":[{"title":"Understand","text":"Find clarity through focused discovery, research, and a shared view of success."},{"title":"Create","text":"Build practical solutions around the people who will use them every day."},{"title":"Improve","text":"Keep learning, measure what matters, and make the next iteration better."}]}},{"type":"quote","data":{"text":"A good partnership makes the next step feel possible.","caption":"Our approach"}},{"type":"snippet","data":{"key":"demo-help"}}]}},"slots":{}}',
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', 'Our services',
    'is_noindex', 0,
    'status', 10,
    'is_home', 0,
    'is_snippet', 0
  ), 10
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)='demo-services'
    OR LOWER(JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.url')))='demo-services'
);

INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, 'demo-department', 'People, resources, and a place to start', 'sidebar-right', 30, 0, 0, 1, 'Department resources', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"Your starting point for the people, guidance, and everyday resources that help our department do its best work."}},{"type":"header","data":{"text":"Start here","level":2}},{"type":"cards","data":{"items":[{"title":"New to the team?","text":"Get oriented with a simple checklist, team introductions, and the tools you need."},{"title":"Planning a project","text":"Use our shared guidance to define the outcome, find support, and prepare your next step."}]}},{"type":"table","data":{"caption":"Where to find support","withHeadings":true,"content":[["Need","First step"],["Getting access","Contact the service desk"],["Project advice","Speak with your team lead"]]}},{"type":"snippet","data":{"key":"demo-help"}}]},"right":{"blocks":[{"type":"callout","data":{"title":"Keep this page useful","text":"Found a gap or an outdated resource? Let the department editor know."}}]}},"slots":{}}', JSON_OBJECT(
    'iname', 'People, resources, and a place to start',
    'parent_id', 0,
    'url', 'demo-department',
    'head_att_id', 0,
    'template', 'sidebar-right',
    'prio', 30,
    'meta_keywords', '',
    'meta_description', '',
    'meta_title', '',
    'custom_head', '',
    'custom_css', '',
    'custom_js', '',
    'redirect_url', '',
    'url_aliases', '',
    'content_json', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"Your starting point for the people, guidance, and everyday resources that help our department do its best work."}},{"type":"header","data":{"text":"Start here","level":2}},{"type":"cards","data":{"items":[{"title":"New to the team?","text":"Get oriented with a simple checklist, team introductions, and the tools you need."},{"title":"Planning a project","text":"Use our shared guidance to define the outcome, find support, and prepare your next step."}]}},{"type":"table","data":{"caption":"Where to find support","withHeadings":true,"content":[["Need","First step"],["Getting access","Contact the service desk"],["Project advice","Speak with your team lead"]]}},{"type":"snippet","data":{"key":"demo-help"}}]},"right":{"blocks":[{"type":"callout","data":{"title":"Keep this page useful","text":"Found a gap or an outdated resource? Let the department editor know."}}]}},"slots":{}}',
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', 'Department resources',
    'is_noindex', 0,
    'status', 10,
    'is_home', 0,
    'is_snippet', 0
  ), 10
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)='demo-department'
    OR LOWER(JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.url')))='demo-department'
);
