-- Spages CMS storage; run AdminSpages.MigrateAction once after applying this schema.
-- Existing page content and customizations remain intact until that explicit conversion.

-- 1 for a reusable snippet; url is its stable key
ALTER TABLE spages ADD is_snippet TINYINT NOT NULL DEFAULT 0;

-- Converted block document; draft and revision snapshots are authoritative
ALTER TABLE spages ADD content_json NVARCHAR(MAX);

-- Editable snapshot of all content fields; content_json remains a JSON string
ALTER TABLE spages ADD draft_json NVARCHAR(MAX);

-- 0 Draft, 10 In review, 20 Changes requested, 30 Published, 40 Scheduled
ALTER TABLE spages ADD workflow TINYINT NOT NULL DEFAULT 0;

-- Current editorial review feedback
ALTER TABLE spages ADD review_note NVARCHAR(MAX);

-- Minimum framework access level required to view this page
ALTER TABLE spages ADD access_level INT NOT NULL DEFAULT 0;

-- 1 to include the published page in navigation
ALTER TABLE spages ADD is_nav_visible TINYINT NOT NULL DEFAULT 1;

-- Optional navigation label; empty uses the page title
ALTER TABLE spages ADD nav_title NVARCHAR(128) NOT NULL DEFAULT '';

-- Optional browser and search title; empty uses the page title
ALTER TABLE spages ADD meta_title NVARCHAR(255) NOT NULL DEFAULT '';

-- 1 to exclude the published page from search indexing
ALTER TABLE spages ADD is_noindex TINYINT NOT NULL DEFAULT 0;

-- Manual app-local URL aliases, one per line
ALTER TABLE spages ADD url_aliases NVARCHAR(MAX);

/* Immutable page snapshots and publication history */
CREATE TABLE spages_revisions (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,                     -- Revision identity
  spages_id             INT NOT NULL REFERENCES spages(id),                          -- Page or snippet owning this revision

  kind                  TINYINT NOT NULL DEFAULT 0,                                  -- 0 Saved, 10 Submitted, 20 Changes requested, 30 Published, 40 Withdrawn, 50 Original
  snapshot_json         NVARCHAR(MAX) NOT NULL,                                      -- Immutable content-field snapshot; content_json remains a JSON string
  snippet_versions      NVARCHAR(MAX),                                               -- Pinned snippet revision IDs keyed by the snippet url
  effective_time        DATETIME2 NOT NULL,                                          -- Effective instant in DB timezone; reads normalize to UTC
  note                  NVARCHAR(1000),                                              -- Editorial note for this revision

  status                TINYINT NOT NULL DEFAULT 0,                                  -- 0 retained/eligible, 127 cancelled scheduled release
  add_time              DATETIME2 NOT NULL DEFAULT GETDATE(),                        -- Creation instant in DB timezone; reads normalize to UTC
  add_users_id          INT NOT NULL DEFAULT 0,                                      -- User who created this revision; 0 for system seeds

  INDEX IX_spages_revisions_release (spages_id, kind, status, effective_time, id)
);
