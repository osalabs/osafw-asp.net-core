-- Spages CMS storage; run AdminSpages.MigrateAction once after applying this schema.
-- Existing page content and customizations remain intact until that explicit conversion.

-- 1 for a reusable snippet; url is its stable key
ALTER TABLE spages ADD is_snippet TINYINT NOT NULL DEFAULT 0;

-- Converted block document; draft and revision snapshots are authoritative
ALTER TABLE spages ADD content_json LONGTEXT;

-- Editable snapshot of all content fields; content_json remains a JSON string
ALTER TABLE spages ADD draft_json LONGTEXT;

-- 0 Draft, 10 In review, 20 Changes requested, 30 Published, 40 Scheduled
ALTER TABLE spages ADD workflow TINYINT NOT NULL DEFAULT 0;

-- Current editorial review feedback
ALTER TABLE spages ADD review_note LONGTEXT;

-- Minimum framework access level required to view this page
ALTER TABLE spages ADD access_level INT NOT NULL DEFAULT 0;

-- 1 to include the published page in navigation
ALTER TABLE spages ADD is_nav_visible TINYINT NOT NULL DEFAULT 1;

-- Optional navigation label; empty uses the page title
ALTER TABLE spages ADD nav_title VARCHAR(128) NOT NULL DEFAULT '';

-- Optional browser and search title; empty uses the page title
ALTER TABLE spages ADD meta_title VARCHAR(255) NOT NULL DEFAULT '';

-- 1 to exclude the published page from search indexing
ALTER TABLE spages ADD is_noindex TINYINT NOT NULL DEFAULT 0;

-- Manual app-local URL aliases, one per line
ALTER TABLE spages ADD url_aliases LONGTEXT;

-- Custom page head; preserve applications that already added this column.
SET @spages_custom_head_sql = (SELECT IF(EXISTS (
  SELECT 1 FROM information_schema.columns
  WHERE table_schema=DATABASE() AND table_name='spages' AND column_name='custom_head'
), 'SELECT 1', 'ALTER TABLE spages ADD custom_head LONGTEXT'));
PREPARE spages_custom_head_stmt FROM @spages_custom_head_sql;
EXECUTE spages_custom_head_stmt;
DEALLOCATE PREPARE spages_custom_head_stmt;

/* Immutable page snapshots and publication history */
CREATE TABLE spages_revisions (
  id                    INT NOT NULL AUTO_INCREMENT,                                 -- Revision identity
  spages_id             INT NOT NULL,                                                -- Page or snippet owning this revision

  kind                  TINYINT NOT NULL DEFAULT 0,                                  -- 0 Saved, 10 Submitted, 20 Changes requested, 30 Published, 40 Withdrawn, 50 Original
  snapshot_json         LONGTEXT NOT NULL,                                           -- Immutable content-field snapshot; content_json remains a JSON string
  snippet_versions      LONGTEXT,                                                    -- Pinned snippet revision IDs keyed by the snippet url
  effective_time        DATETIME NOT NULL,                                           -- Effective instant in DB timezone; reads normalize to UTC
  note                  VARCHAR(1000),                                               -- Editorial note for this revision

  status                TINYINT NOT NULL DEFAULT 0,                                  -- 0 retained/eligible, 127 cancelled scheduled release
  add_time              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,                 -- Creation instant in DB timezone; reads normalize to UTC
  add_users_id          INT NOT NULL DEFAULT 0,                                      -- User who created this revision; 0 for system seeds

  PRIMARY KEY (id),
  FOREIGN KEY (spages_id) REFERENCES spages(id),
  INDEX IX_spages_revisions_release (spages_id, kind, status, effective_time, id)
) DEFAULT CHARSET=utf8mb4;
