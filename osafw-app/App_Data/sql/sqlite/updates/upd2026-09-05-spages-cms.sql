-- Spages CMS storage; run AdminSpages.MigrateAction once after applying this schema.
-- Existing page content and customizations remain intact until that explicit conversion.

-- 1 for a reusable snippet; url is its stable key
ALTER TABLE spages ADD is_snippet TINYINT NOT NULL DEFAULT 0;

-- Converted block document; draft and revision snapshots are authoritative
ALTER TABLE spages ADD content_json TEXT;

-- Editable snapshot of all content fields; content_json remains a JSON string
ALTER TABLE spages ADD draft_json TEXT;

-- 0 Draft, 10 In review, 20 Changes requested, 30 Published, 40 Scheduled
ALTER TABLE spages ADD workflow TINYINT NOT NULL DEFAULT 0;

-- Current editorial review feedback
ALTER TABLE spages ADD review_note TEXT;

-- Minimum framework access level required to view this page
ALTER TABLE spages ADD access_level INT NOT NULL DEFAULT 0;

-- 1 to include the published page in navigation
ALTER TABLE spages ADD is_nav_visible TINYINT NOT NULL DEFAULT 1;

-- Optional navigation label; empty uses the page title
ALTER TABLE spages ADD nav_title TEXT NOT NULL DEFAULT '';

-- Optional browser and search title; empty uses the page title
ALTER TABLE spages ADD meta_title TEXT NOT NULL DEFAULT '';

-- 1 to exclude the published page from search indexing
ALTER TABLE spages ADD is_noindex TINYINT NOT NULL DEFAULT 0;

-- Manual app-local URL aliases, one per line
ALTER TABLE spages ADD url_aliases TEXT;

/* Immutable page snapshots and publication history */
CREATE TABLE spages_revisions (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,                           -- Revision identity
  spages_id             INT NOT NULL REFERENCES spages(id),                          -- Page or snippet owning this revision

  kind                  TINYINT NOT NULL DEFAULT 0,                                  -- 0 Saved, 10 Submitted, 20 Changes requested, 30 Published, 40 Withdrawn, 50 Original
  snapshot_json         TEXT NOT NULL,                                               -- Immutable content-field snapshot; content_json remains a JSON string
  snippet_versions      TEXT,                                                        -- Pinned snippet revision IDs keyed by the snippet url
  effective_time        DATETIME NOT NULL,                                           -- Effective instant in DB timezone; reads normalize to UTC
  note                  TEXT,                                                        -- Editorial note for this revision

  status                TINYINT NOT NULL DEFAULT 0,                                  -- 0 retained/eligible, 127 cancelled scheduled release
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,                 -- Creation instant in DB timezone; reads normalize to UTC
  add_users_id          INT NOT NULL DEFAULT 0                                       -- User who created this revision; 0 for system seeds
);
CREATE INDEX IX_spages_revisions_release ON spages_revisions (spages_id, kind, status, effective_time, id);
