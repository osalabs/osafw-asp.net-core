-- Initial published pages for a fresh database. Run after fwdatabase.sql.
INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
VALUES (0, '', 'Home', 'article', 1, 1, 0, 1, '', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"Welcome to your new website."}}]}},"slots":{}}', JSON_OBJECT(
    'iname', 'Home',
    'parent_id', 0,
    'url', '',
    'head_att_id', 0,
    'template', 'article',
    'prio', 1,
    'meta_keywords', '',
    'meta_description', '',
    'meta_title', '',
    'custom_head', '',
    'custom_css', '',
    'custom_js', '',
    'redirect_url', '',
    'url_aliases', '',
    'content_json', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"Welcome to your new website."}}]}},"slots":{}}',
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', '',
    'is_noindex', 0,
    'status', 0,
    'is_home', 1,
    'is_snippet', 0
  ), 0);

INSERT INTO spages (parent_id, url, iname, template, prio, is_home, is_snippet, is_nav_visible, nav_title, content_json, draft_json, status)
VALUES (0, 'test-page', 'Test  page', 'article', 2, 0, 0, 1, '', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"This is a sample page. Edit its blocks in Pages."}}]}},"slots":{}}', JSON_OBJECT(
    'iname', 'Test  page',
    'parent_id', 0,
    'url', 'test-page',
    'head_att_id', 0,
    'template', 'article',
    'prio', 2,
    'meta_keywords', '',
    'meta_description', '',
    'meta_title', '',
    'custom_head', '',
    'custom_css', '',
    'custom_js', '',
    'redirect_url', '',
    'url_aliases', '',
    'content_json', '{"schemaVersion":1,"regions":{"main":{"blocks":[{"type":"paragraph","data":{"text":"This is a sample page. Edit its blocks in Pages."}}]}},"slots":{}}',
    'access_level', 0,
    'is_nav_visible', 1,
    'nav_title', '',
    'is_noindex', 0,
    'status', 0,
    'is_home', 0,
    'is_snippet', 0
  ), 0);

INSERT INTO spages_revisions (spages_id, kind, snapshot_json, snippet_versions, effective_time, note, status, add_users_id)
SELECT id, 30, draft_json, '{}', COALESCE(pub_time, CURRENT_TIMESTAMP), 'Initial published page', 0, 0
FROM spages WHERE id IN (1, 2);
