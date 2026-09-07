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
-- Five layouts and all built-in blocks, plus one shared help snippet.
-- Existing URLs and drafts are preserved; this CMS section can be repeated safely.
-- Publish demo-help first, then the five pages. The homepage remains unchanged.
-- DevConfigure initialization copies the bundled App_Data/demo media into attachment storage.

INSERT INTO att (icode, fname, iname, ext, fsize, is_image, status)
SELECT N'demo-spages-workshop', N'demo-spages-workshop.png', N'Planning workshop', N'.png', 0, 1, 10
WHERE NOT EXISTS (SELECT 1 FROM att WHERE icode=N'demo-spages-workshop');

INSERT INTO att (icode, fname, iname, ext, fsize, is_image, status)
SELECT N'demo-spages-checklist', N'demo-spages-checklist.txt', N'First-week checklist', N'.txt', 0, 0, 10
WHERE NOT EXISTS (SELECT 1 FROM att WHERE icode=N'demo-spages-checklist');

-- article: How can we help?
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, N'demo-help', N'How can we help?', N'article', 10, 0, 1, 1, N'Help',
  demo.content_json, (SELECT
    N'How can we help?' AS [iname],
    0 AS [parent_id],
    N'demo-help' AS [url],
    0 AS [head_att_id],
    N'article' AS [template],
    10 AS [prio],
    N'' AS [meta_keywords],
    N'' AS [meta_description],
    N'' AS [meta_title],
    N'' AS [custom_head],
    N'' AS [custom_css],
    N'' AS [custom_js],
    N'' AS [redirect_url],
    N'' AS [url_aliases],
    demo.content_json AS [content_json],
    0 AS [access_level],
    1 AS [is_nav_visible],
    N'Help' AS [nav_title],
    0 AS [is_noindex],
    10 AS [status],
    0 AS [is_home],
    1 AS [is_snippet]
  FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 10
FROM (SELECT N'{
  "schemaVersion": 1,
  "regions": {
    "main": {
      "blocks": [
        {
          "type": "callout",
          "data": {
            "title": "Let''s find the right next step",
            "text": "Tell us what you need. Our team will connect you with the right person."
          }
        },
        {
          "type": "button",
          "data": {
            "text": "Contact our team",
            "url": "/Contact"
          }
        }
      ]
    }
  },
  "slots": {}
}' AS content_json) demo
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)=N'demo-help'
    OR LOWER(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json ELSE NULL END, '$.url'))=N'demo-help'
);

-- landing: Good work starts with a clear plan
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, N'demo-services', N'Good work starts with a clear plan', N'landing', 20, 0, 0, 1, N'Our services',
  demo.content_json, (SELECT
    N'Good work starts with a clear plan' AS [iname],
    0 AS [parent_id],
    N'demo-services' AS [url],
    0 AS [head_att_id],
    N'landing' AS [template],
    20 AS [prio],
    N'' AS [meta_keywords],
    N'' AS [meta_description],
    N'' AS [meta_title],
    N'' AS [custom_head],
    N'' AS [custom_css],
    N'' AS [custom_js],
    N'' AS [redirect_url],
    N'' AS [url_aliases],
    demo.content_json AS [content_json],
    0 AS [access_level],
    1 AS [is_nav_visible],
    N'Our services' AS [nav_title],
    0 AS [is_noindex],
    10 AS [status],
    0 AS [is_home],
    0 AS [is_snippet]
  FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 10
FROM (SELECT N'{
  "schemaVersion": 1,
  "regions": {
    "main": {
      "blocks": [
        {
          "type": "paragraph",
          "data": {
            "text": "Practical expertise. Thoughtful service. We help ambitious teams turn complex challenges into useful, lasting improvements."
          }
        },
        {
          "type": "button",
          "data": {
            "text": "Explore our services",
            "url": "#services"
          }
        },
        {
          "type": "header",
          "data": {
            "text": "Support at every stage",
            "level": 2,
            "anchor": "services"
          }
        },
        {
          "type": "cards",
          "data": {
            "items": [
              {
                "title": "Understand",
                "text": "Find clarity through focused discovery, research, and a shared view of success."
              },
              {
                "title": "Create",
                "text": "Build practical solutions around the people who will use them every day."
              },
              {
                "title": "Improve",
                "text": "Keep learning, measure what matters, and make the next iteration better."
              }
            ]
          }
        },
        {
          "type": "quote",
          "data": {
            "text": "A good partnership makes the next step feel possible.",
            "caption": "Our approach"
          }
        },
        {
          "type": "snippet",
          "data": {
            "key": "demo-help"
          }
        }
      ]
    }
  },
  "slots": {}
}' AS content_json) demo
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)=N'demo-services'
    OR LOWER(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json ELSE NULL END, '$.url'))=N'demo-services'
);

-- sidebar-right: People, resources, and a place to start
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, N'demo-department', N'People, resources, and a place to start', N'sidebar-right', 30, 0, 0, 1, N'Department resources',
  demo.content_json, (SELECT
    N'People, resources, and a place to start' AS [iname],
    0 AS [parent_id],
    N'demo-department' AS [url],
    0 AS [head_att_id],
    N'sidebar-right' AS [template],
    30 AS [prio],
    N'' AS [meta_keywords],
    N'' AS [meta_description],
    N'' AS [meta_title],
    N'' AS [custom_head],
    N'' AS [custom_css],
    N'' AS [custom_js],
    N'' AS [redirect_url],
    N'' AS [url_aliases],
    demo.content_json AS [content_json],
    0 AS [access_level],
    1 AS [is_nav_visible],
    N'Department resources' AS [nav_title],
    0 AS [is_noindex],
    10 AS [status],
    0 AS [is_home],
    0 AS [is_snippet]
  FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 10
FROM (SELECT N'{
  "schemaVersion": 1,
  "regions": {
    "main": {
      "blocks": [
        {
          "type": "paragraph",
          "data": {
            "text": "Your starting point for the people, guidance, and everyday resources that help our department do its best work."
          }
        },
        {
          "type": "header",
          "data": {
            "text": "Start here",
            "level": 2
          }
        },
        {
          "type": "cards",
          "data": {
            "items": [
              {
                "title": "New to the team?",
                "text": "Get oriented with a simple checklist, team introductions, and the tools you need."
              },
              {
                "title": "Planning a project",
                "text": "Use our shared guidance to define the outcome, find support, and prepare your next step."
              }
            ]
          }
        },
        {
          "type": "table",
          "data": {
            "caption": "Where to find support",
            "withHeadings": true,
            "content": [
              [
                "Need",
                "First step"
              ],
              [
                "Getting access",
                "Contact the service desk"
              ],
              [
                "Project advice",
                "Speak with your team lead"
              ]
            ]
          }
        },
        {
          "type": "snippet",
          "data": {
            "key": "demo-help"
          }
        }
      ]
    },
    "right": {
      "blocks": [
        {
          "type": "callout",
          "data": {
            "title": "Keep this page useful",
            "text": "Found a gap or an outdated resource? Let the department editor know."
          }
        }
      ]
    }
  },
  "slots": {}
}' AS content_json) demo
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)=N'demo-department'
    OR LOWER(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json ELSE NULL END, '$.url'))=N'demo-department'
);

-- sidebar-left: Your first week, made simpler
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, N'demo-start-here', N'Your first week, made simpler', N'sidebar-left', 40, 0, 0, 1, N'Getting started',
  demo.content_json, (SELECT
    N'Your first week, made simpler' AS [iname],
    0 AS [parent_id],
    N'demo-start-here' AS [url],
    0 AS [head_att_id],
    N'sidebar-left' AS [template],
    40 AS [prio],
    N'' AS [meta_keywords],
    N'A welcoming first-week guide with practical steps, a downloadable checklist, and clear places to ask for help.' AS [meta_description],
    N'' AS [meta_title],
    N'' AS [custom_head],
    N'' AS [custom_css],
    N'' AS [custom_js],
    N'' AS [redirect_url],
    N'' AS [url_aliases],
    demo.content_json AS [content_json],
    0 AS [access_level],
    1 AS [is_nav_visible],
    N'Getting started' AS [nav_title],
    0 AS [is_noindex],
    10 AS [status],
    0 AS [is_home],
    0 AS [is_snippet]
  FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 10
FROM (SELECT REPLACE(N'{
  "schemaVersion": 1,
  "regions": {
    "main": {
      "blocks": [
        {
          "type": "paragraph",
          "data": {
            "text": "Welcome to the team. You do not need to learn everything on day one. Start with the essentials, meet the people around you, and leave room for questions."
          }
        },
        {
          "type": "header",
          "data": {
            "text": "Before you arrive",
            "level": 2,
            "anchor": "before-you-arrive"
          }
        },
        {
          "type": "paragraph",
          "data": {
            "text": "Your team lead will send your start time and meeting point. Bring a photo ID for reception and the laptop you were issued, if you already have one."
          }
        },
        {
          "type": "callout",
          "data": {
            "title": "A comfortable first day",
            "text": "Tell your team lead about any access requirements or adjustments that would help you settle in. We will plan them together."
          }
        },
        {
          "type": "header",
          "data": {
            "text": "Three things for day one",
            "level": 2,
            "anchor": "day-one"
          }
        },
        {
          "type": "list",
          "data": {
            "style": "ordered",
            "items": [
              "Meet your buddy and take a short tour of the workplace.",
              "Sign in to your account and check that you can reach the shared tools.",
              "Book a short conversation with your team lead about your first week."
            ]
          }
        },
        {
          "type": "header",
          "data": {
            "text": "Build your first-week plan",
            "level": 2,
            "anchor": "first-week"
          }
        },
        {
          "type": "paragraph",
          "data": {
            "text": "Keep the plan small: one useful introduction, one task you can finish, and one question to bring to your next check-in. Your buddy can help you find the right people."
          }
        },
        {
          "type": "file",
          "data": {
            "att_id": "@demo-spages-checklist",
            "title": "Download the first-week checklist (plain text)"
          }
        },
        {
          "type": "header",
          "data": {
            "text": "By the end of the week",
            "level": 2,
            "anchor": "week-one"
          }
        },
        {
          "type": "list",
          "data": {
            "style": "unordered",
            "items": [
              "Know where to ask for help.",
              "Understand your first priority and what a good result looks like.",
              "Have your next check-in in the calendar."
            ]
          }
        }
      ]
    },
    "left": {
      "blocks": [
        {
          "type": "header",
          "data": {
            "text": "On this page",
            "level": 2
          }
        },
        {
          "type": "list",
          "data": {
            "style": "unordered",
            "items": [
              "<a href=\"#before-you-arrive\">Before you arrive</a>",
              "<a href=\"#day-one\">Day one</a>",
              "<a href=\"#first-week\">Your first-week plan</a>",
              "<a href=\"#week-one\">End-of-week check-in</a>"
            ]
          }
        },
        {
          "type": "callout",
          "data": {
            "title": "You have a buddy",
            "text": "Your buddy is a friendly first point of contact for everyday questions. No question is too small."
          }
        }
      ]
    }
  },
  "slots": {
    "after_content": "demo-help"
  }
}', N'"@demo-spages-checklist"', CAST((SELECT id FROM att WHERE icode=N'demo-spages-checklist') AS NVARCHAR(20))) AS content_json) demo
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)=N'demo-start-here'
    OR LOWER(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json ELSE NULL END, '$.url'))=N'demo-start-here'
);

-- three-column: Around the workplace
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, N'demo-bulletin', N'Around the workplace', N'three-column', 50, 0, 0, 1, N'Workplace bulletin',
  demo.content_json, (SELECT
    N'Around the workplace' AS [iname],
    0 AS [parent_id],
    N'demo-bulletin' AS [url],
    0 AS [head_att_id],
    N'three-column' AS [template],
    50 AS [prio],
    N'' AS [meta_keywords],
    N'A workplace bulletin with team news, regular events, and useful resources in three clearly defined columns.' AS [meta_description],
    N'' AS [meta_title],
    N'' AS [custom_head],
    N'' AS [custom_css],
    N'' AS [custom_js],
    N'' AS [redirect_url],
    N'' AS [url_aliases],
    demo.content_json AS [content_json],
    0 AS [access_level],
    1 AS [is_nav_visible],
    N'Workplace bulletin' AS [nav_title],
    0 AS [is_noindex],
    10 AS [status],
    0 AS [is_home],
    0 AS [is_snippet]
  FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 10
FROM (SELECT N'{
  "schemaVersion": 1,
  "regions": {
    "main": {
      "blocks": [
        {
          "type": "paragraph",
          "data": {
            "text": "Small updates, useful ideas, and a few things to look forward to. A shared place to stay connected with the people and work around us."
          }
        },
        {
          "type": "header",
          "data": {
            "text": "This week, make a little space",
            "level": 2
          }
        },
        {
          "type": "paragraph",
          "data": {
            "text": "We are keeping Wednesday afternoon free of recurring internal meetings. Use the time for a task that needs concentration, a thoughtful review, or catching up with a colleague."
          }
        },
        {
          "type": "quote",
          "data": {
            "text": "Protect time for focused work, and make it easy to ask for help.",
            "caption": "Our working agreement"
          }
        },
        {
          "type": "header",
          "data": {
            "text": "Good things worth sharing",
            "level": 2
          }
        },
        {
          "type": "cards",
          "data": {
            "items": [
              {
                "title": "A clearer handover",
                "text": "The operations team is trying a shorter handover note: what changed, what is next, and who can help. Bring an example to the next team huddle."
              },
              {
                "title": "Learning, over lunch",
                "text": "Our next lunch session is a practical exchange of time-saving habits. Bring one small tip you wish you had known sooner."
              }
            ]
          }
        },
        {
          "type": "delimiter",
          "data": {}
        },
        {
          "type": "header",
          "data": {
            "text": "One useful conversation",
            "level": 2
          }
        },
        {
          "type": "paragraph",
          "data": {
            "text": "Ask someone from another team what makes their week easier. Share the answer at your next check-in, then choose one change you can try together."
          }
        }
      ]
    },
    "left": {
      "blocks": [
        {
          "type": "header",
          "data": {
            "text": "Regular moments",
            "level": 2
          }
        },
        {
          "type": "list",
          "data": {
            "style": "unordered",
            "items": [
              "<strong>Monday, 9:15</strong><br>Team huddle: priorities and questions.",
              "<strong>Thursday, 12:30</strong><br>Bring-your-lunch learning session.",
              "<strong>Friday, 15:00</strong><br>A short end-of-week wrap-up."
            ]
          }
        },
        {
          "type": "paragraph",
          "data": {
            "text": "Check your team calendar for meeting links and any changes."
          }
        }
      ]
    },
    "right": {
      "blocks": [
        {
          "type": "header",
          "data": {
            "text": "Useful starting points",
            "level": 2
          }
        },
        {
          "type": "list",
          "data": {
            "style": "unordered",
            "items": [
              "<a href=\"/demo-start-here\">New colleague guide</a>",
              "<a href=\"/demo-department\">Department resources</a>",
              "<a href=\"/demo-field-notes\">Plan a calmer project kickoff</a>"
            ]
          }
        },
        {
          "type": "callout",
          "data": {
            "title": "Have an update?",
            "text": "Share a short note, one clear next step, and the person to contact. Useful beats lengthy."
          }
        },
        {
          "type": "snippet",
          "data": {
            "key": "demo-help"
          }
        }
      ]
    }
  },
  "slots": {}
}' AS content_json) demo
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)=N'demo-bulletin'
    OR LOWER(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json ELSE NULL END, '$.url'))=N'demo-bulletin'
);

-- article: A practical guide to a calmer project kickoff
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, N'demo-field-notes', N'A practical guide to a calmer project kickoff', N'article', 60, 0, 0, 1, N'Field notes',
  demo.content_json, (SELECT
    N'A practical guide to a calmer project kickoff' AS [iname],
    0 AS [parent_id],
    N'demo-field-notes' AS [url],
    0 AS [head_att_id],
    N'article' AS [template],
    60 AS [prio],
    N'' AS [meta_keywords],
    N'A practical project kickoff guide with a clear agenda, a copyable plain-text brief, and a short review checklist.' AS [meta_description],
    N'' AS [meta_title],
    N'' AS [custom_head],
    N'' AS [custom_css],
    N'' AS [custom_js],
    N'' AS [redirect_url],
    N'' AS [url_aliases],
    demo.content_json AS [content_json],
    0 AS [access_level],
    1 AS [is_nav_visible],
    N'Field notes' AS [nav_title],
    0 AS [is_noindex],
    10 AS [status],
    0 AS [is_home],
    0 AS [is_snippet]
  FOR JSON PATH, WITHOUT_ARRAY_WRAPPER), 10
FROM (SELECT REPLACE(N'{
  "schemaVersion": 1,
  "regions": {
    "main": {
      "blocks": [
        {
          "type": "paragraph",
          "data": {
            "text": "The most useful kickoff is not the one with the most slides. It is the one that leaves everyone with the same understanding of the next step."
          }
        },
        {
          "type": "image",
          "data": {
            "att_id": "@demo-spages-workshop",
            "alt": "Four colleagues discussing a project plan around a table in a bright studio.",
            "decorative": false,
            "caption": "Make room for questions before making a plan."
          }
        },
        {
          "type": "header",
          "data": {
            "text": "Start with the change you want to see",
            "level": 2,
            "anchor": "start-with-the-outcome"
          }
        },
        {
          "type": "paragraph",
          "data": {
            "text": "Describe the problem in the language of the people who experience it. Ask what would be easier if the project worked, and how you would notice the difference."
          }
        },
        {
          "type": "quote",
          "data": {
            "text": "A shared understanding of the problem is already progress.",
            "caption": "A principle for collaborative work"
          }
        },
        {
          "type": "header",
          "data": {
            "text": "Give the meeting a small, useful shape",
            "level": 2,
            "anchor": "meeting-plan"
          }
        },
        {
          "type": "list",
          "data": {
            "style": "ordered",
            "items": [
              "Spend the first ten minutes hearing how the work happens today.",
              "Agree on one outcome and the people it will help.",
              "Name the first experiment, its owner, and a time to review it."
            ]
          }
        },
        {
          "type": "header",
          "data": {
            "text": "Write a brief that fits on one screen",
            "level": 2,
            "anchor": "one-page-brief"
          }
        },
        {
          "type": "paragraph",
          "data": {
            "text": "A plain-text note is enough to start. Copy this outline into your project space and fill it in together:"
          }
        },
        {
          "type": "code",
          "data": {
            "code": "Outcome: What should become easier?\nPeople: Who will use or support the result?\nFirst step: What can we try this week?\nOwner: Who will move that step forward?\nReview: When will we look at what we learned?"
          }
        },
        {
          "type": "legacyMarkdown",
          "data": {
            "text": "### Before you share the brief\n\n- **Keep the language specific.** Replace vague goals with an observable change.\n- **Check the assumptions.**\n  - Which ones have you confirmed?\n  - Which ones need a small experiment?\n- **Leave an open question.** A brief should invite a useful reply."
          }
        },
        {
          "type": "delimiter",
          "data": {}
        },
        {
          "type": "header",
          "data": {
            "text": "Finish with a next step, not a longer document",
            "level": 2
          }
        },
        {
          "type": "paragraph",
          "data": {
            "text": "Send a short recap while the conversation is fresh. Include the decision, the first action, and the next check-in. A little clarity now prevents a lot of chasing later."
          }
        },
        {
          "type": "button",
          "data": {
            "text": "Talk with us about your project",
            "url": "/Contact"
          }
        }
      ]
    }
  },
  "slots": {
    "after_content": "demo-help"
  }
}', N'"@demo-spages-workshop"', CAST((SELECT id FROM att WHERE icode=N'demo-spages-workshop') AS NVARCHAR(20))) AS content_json) demo
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)=N'demo-field-notes'
    OR LOWER(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json ELSE NULL END, '$.url'))=N'demo-field-notes'
);
