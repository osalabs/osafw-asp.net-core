-- Spages CMS storage; run the CMS Upgrade action after applying this schema.
ALTER TABLE spages ADD is_snippet INT NOT NULL DEFAULT 0;
ALTER TABLE spages ADD snippet_key NVARCHAR(64) NOT NULL DEFAULT '';
ALTER TABLE spages ADD content_json NVARCHAR(MAX);
ALTER TABLE spages ADD draft_json NVARCHAR(MAX);
ALTER TABLE spages ADD edit_version INT NOT NULL DEFAULT 0;
ALTER TABLE spages ADD workflow NVARCHAR(24) NOT NULL DEFAULT 'draft';
ALTER TABLE spages ADD review_note NVARCHAR(MAX);
ALTER TABLE spages ADD access_level INT NOT NULL DEFAULT 0;
ALTER TABLE spages ADD nav_visible INT NOT NULL DEFAULT 1;
ALTER TABLE spages ADD nav_title NVARCHAR(128) NOT NULL DEFAULT '';
ALTER TABLE spages ADD meta_title NVARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE spages ADD noindex INT NOT NULL DEFAULT 0;
ALTER TABLE spages ADD image_alt NVARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE spages ADD image_decorative INT NOT NULL DEFAULT 0;

CREATE TABLE spages_revisions (
  id INT IDENTITY(1,1) PRIMARY KEY,
  spages_id INT NOT NULL REFERENCES spages(id),
  kind NVARCHAR(24) NOT NULL,
  snapshot_json NVARCHAR(MAX) NOT NULL,
  snippet_versions NVARCHAR(MAX),
  effective_time DATETIME2 NOT NULL,
  cancelled INT NOT NULL DEFAULT 0,
  note NVARCHAR(1000),
  add_time DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  add_users_id INT NOT NULL DEFAULT 0
);
CREATE INDEX IX_spages_revisions_release ON spages_revisions (spages_id, kind, cancelled, effective_time, id);
CREATE TABLE spages_redirects (
  id INT IDENTITY(1,1) PRIMARY KEY,
  source_url NVARCHAR(450) NOT NULL,
  target_url NVARCHAR(450) NOT NULL DEFAULT '',
  spages_id INT NOT NULL DEFAULT 0,
  revision_id INT NOT NULL DEFAULT 0,
  effective_time DATETIME2 NOT NULL,
  status INT NOT NULL DEFAULT 0,
  add_time DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
  add_users_id INT NOT NULL DEFAULT 0
);
CREATE INDEX IX_spages_redirects_source ON spages_redirects (source_url, status, effective_time);
