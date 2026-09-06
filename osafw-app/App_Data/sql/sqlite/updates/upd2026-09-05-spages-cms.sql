-- Spages CMS storage; run the CMS Upgrade action after applying this schema.
ALTER TABLE spages ADD is_snippet INT NOT NULL DEFAULT 0;
ALTER TABLE spages ADD snippet_key TEXT NOT NULL DEFAULT '';
ALTER TABLE spages ADD content_json TEXT;
ALTER TABLE spages ADD draft_json TEXT;
ALTER TABLE spages ADD edit_version INT NOT NULL DEFAULT 0;
ALTER TABLE spages ADD workflow TEXT NOT NULL DEFAULT 'draft';
ALTER TABLE spages ADD review_note TEXT;
ALTER TABLE spages ADD access_level INT NOT NULL DEFAULT 0;
ALTER TABLE spages ADD nav_visible INT NOT NULL DEFAULT 1;
ALTER TABLE spages ADD nav_title TEXT NOT NULL DEFAULT '';
ALTER TABLE spages ADD meta_title TEXT NOT NULL DEFAULT '';
ALTER TABLE spages ADD noindex INT NOT NULL DEFAULT 0;
ALTER TABLE spages ADD image_alt TEXT NOT NULL DEFAULT '';
ALTER TABLE spages ADD image_decorative INT NOT NULL DEFAULT 0;

CREATE TABLE spages_revisions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  spages_id INT NOT NULL REFERENCES spages(id),
  kind TEXT NOT NULL,
  snapshot_json TEXT NOT NULL,
  snippet_versions TEXT,
  effective_time DATETIME NOT NULL,
  cancelled INT NOT NULL DEFAULT 0,
  note TEXT,
  add_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id INT NOT NULL DEFAULT 0
);
CREATE INDEX IX_spages_revisions_release ON spages_revisions (spages_id, kind, cancelled, effective_time, id);
CREATE TABLE spages_redirects (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  source_url TEXT NOT NULL,
  target_url TEXT NOT NULL DEFAULT '',
  spages_id INT NOT NULL DEFAULT 0,
  revision_id INT NOT NULL DEFAULT 0,
  effective_time DATETIME NOT NULL,
  status INT NOT NULL DEFAULT 0,
  add_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  add_users_id INT NOT NULL DEFAULT 0
);
CREATE INDEX IX_spages_redirects_source ON spages_redirects (source_url, status, effective_time);
