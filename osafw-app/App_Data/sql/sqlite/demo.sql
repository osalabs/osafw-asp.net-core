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
-- Five layouts and all built-in blocks, plus one shared help snippet.
-- Existing URLs and drafts are preserved; this CMS section can be repeated safely.
-- Publish demo-help first, then the five pages. The homepage remains unchanged.
-- DevConfigure initialization copies the bundled App_Data/demo media into attachment storage.

INSERT INTO att (icode, fname, iname, ext, fsize, is_image, status)
SELECT 'demo-spages-workshop', 'demo-spages-workshop.png', 'Planning workshop', '.png', 0, 1, 10
WHERE NOT EXISTS (SELECT 1 FROM att WHERE icode='demo-spages-workshop');

INSERT INTO att (icode, fname, iname, ext, fsize, is_image, status)
SELECT 'demo-spages-checklist', 'demo-spages-checklist.txt', 'First-week checklist', '.txt', 0, 0, 10
WHERE NOT EXISTS (SELECT 1 FROM att WHERE icode='demo-spages-checklist');

-- article: How can we help?
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, 'demo-help', 'How can we help?', 'article', 10, 0, 1, 1, 'Help',
  demo.content_json, JSON_OBJECT(
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
    'content_json', demo.content_json,
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', 'Help',
    'is_noindex', 0,
    'status', 10,
    'is_home', 0,
    'is_snippet', 1
  ), 10
FROM (SELECT '{
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
  WHERE LOWER(url)='demo-help'
    OR LOWER(json_extract(CASE WHEN json_valid(draft_json) THEN draft_json ELSE NULL END, '$.url'))='demo-help'
);

-- landing: Good work starts with a clear plan
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, 'demo-services', 'Good work starts with a clear plan', 'landing', 20, 0, 0, 1, 'Our services',
  demo.content_json, JSON_OBJECT(
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
    'content_json', demo.content_json,
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', 'Our services',
    'is_noindex', 0,
    'status', 10,
    'is_home', 0,
    'is_snippet', 0
  ), 10
FROM (SELECT '{
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
  WHERE LOWER(url)='demo-services'
    OR LOWER(json_extract(CASE WHEN json_valid(draft_json) THEN draft_json ELSE NULL END, '$.url'))='demo-services'
);

-- sidebar-right: People, resources, and a place to start
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, 'demo-department', 'People, resources, and a place to start', 'sidebar-right', 30, 0, 0, 1, 'Department resources',
  demo.content_json, JSON_OBJECT(
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
    'content_json', demo.content_json,
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', 'Department resources',
    'is_noindex', 0,
    'status', 10,
    'is_home', 0,
    'is_snippet', 0
  ), 10
FROM (SELECT '{
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
  WHERE LOWER(url)='demo-department'
    OR LOWER(json_extract(CASE WHEN json_valid(draft_json) THEN draft_json ELSE NULL END, '$.url'))='demo-department'
);

-- sidebar-left: Your first week, made simpler
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, 'demo-start-here', 'Your first week, made simpler', 'sidebar-left', 40, 0, 0, 1, 'Getting started',
  demo.content_json, JSON_OBJECT(
    'iname', 'Your first week, made simpler',
    'parent_id', 0,
    'url', 'demo-start-here',
    'head_att_id', 0,
    'template', 'sidebar-left',
    'prio', 40,
    'meta_keywords', '',
    'meta_description', 'A welcoming first-week guide with practical steps, a downloadable checklist, and clear places to ask for help.',
    'meta_title', '',
    'custom_head', '',
    'custom_css', '',
    'custom_js', '',
    'redirect_url', '',
    'url_aliases', '',
    'content_json', demo.content_json,
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', 'Getting started',
    'is_noindex', 0,
    'status', 10,
    'is_home', 0,
    'is_snippet', 0
  ), 10
FROM (SELECT REPLACE('{
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
}', '"@demo-spages-checklist"', CAST((SELECT id FROM att WHERE icode='demo-spages-checklist') AS TEXT)) AS content_json) demo
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)='demo-start-here'
    OR LOWER(json_extract(CASE WHEN json_valid(draft_json) THEN draft_json ELSE NULL END, '$.url'))='demo-start-here'
);

-- three-column: Around the workplace
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, 'demo-bulletin', 'Around the workplace', 'three-column', 50, 0, 0, 1, 'Workplace bulletin',
  demo.content_json, JSON_OBJECT(
    'iname', 'Around the workplace',
    'parent_id', 0,
    'url', 'demo-bulletin',
    'head_att_id', 0,
    'template', 'three-column',
    'prio', 50,
    'meta_keywords', '',
    'meta_description', 'A workplace bulletin with team news, regular events, and useful resources in three clearly defined columns.',
    'meta_title', '',
    'custom_head', '',
    'custom_css', '',
    'custom_js', '',
    'redirect_url', '',
    'url_aliases', '',
    'content_json', demo.content_json,
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', 'Workplace bulletin',
    'is_noindex', 0,
    'status', 10,
    'is_home', 0,
    'is_snippet', 0
  ), 10
FROM (SELECT '{
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
  WHERE LOWER(url)='demo-bulletin'
    OR LOWER(json_extract(CASE WHEN json_valid(draft_json) THEN draft_json ELSE NULL END, '$.url'))='demo-bulletin'
);

-- article: A practical guide to a calmer project kickoff
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
SELECT 0, 'demo-field-notes', 'A practical guide to a calmer project kickoff', 'article', 60, 0, 0, 1, 'Field notes',
  demo.content_json, JSON_OBJECT(
    'iname', 'A practical guide to a calmer project kickoff',
    'parent_id', 0,
    'url', 'demo-field-notes',
    'head_att_id', 0,
    'template', 'article',
    'prio', 60,
    'meta_keywords', '',
    'meta_description', 'A practical project kickoff guide with a clear agenda, a copyable plain-text brief, and a short review checklist.',
    'meta_title', '',
    'custom_head', '',
    'custom_css', '',
    'custom_js', '',
    'redirect_url', '',
    'url_aliases', '',
    'content_json', demo.content_json,
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', 'Field notes',
    'is_noindex', 0,
    'status', 10,
    'is_home', 0,
    'is_snippet', 0
  ), 10
FROM (SELECT REPLACE('{
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
}', '"@demo-spages-workshop"', CAST((SELECT id FROM att WHERE icode='demo-spages-workshop') AS TEXT)) AS content_json) demo
WHERE NOT EXISTS (
  SELECT 1 FROM spages
  WHERE LOWER(url)='demo-field-notes'
    OR LOWER(json_extract(CASE WHEN json_valid(draft_json) THEN draft_json ELSE NULL END, '$.url'))='demo-field-notes'
);
