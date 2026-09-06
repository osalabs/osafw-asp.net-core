-- Keep standard list metadata aligned with the working draft before workflow is folded into status.
-- Missing or incorrectly typed draft fields retain their existing column values.
UPDATE spages SET
  iname = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.iname'))='STRING' THEN JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.iname')) ELSE iname END,
  url = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.url'))='STRING' THEN JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.url')) ELSE url END,
  parent_id = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.parent_id'))='INTEGER' THEN CAST(JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.parent_id')) AS SIGNED) ELSE parent_id END,
  template = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.template'))='STRING' THEN JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.template')) ELSE template END,
  prio = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.prio'))='INTEGER' THEN CAST(JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.prio')) AS SIGNED) ELSE prio END,
  is_snippet = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.is_snippet'))='INTEGER' THEN CAST(JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.is_snippet')) AS UNSIGNED) ELSE is_snippet END,
  access_level = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.access_level'))='INTEGER' THEN CAST(JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.access_level')) AS SIGNED) ELSE access_level END,
  is_nav_visible = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.is_nav_visible'))='INTEGER' THEN CAST(JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.is_nav_visible')) AS UNSIGNED) ELSE is_nav_visible END,
  nav_title = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.nav_title'))='STRING' THEN JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.nav_title')) ELSE nav_title END,
  meta_title = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.meta_title'))='STRING' THEN JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.meta_title')) ELSE meta_title END,
  is_noindex = CASE WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.is_noindex'))='INTEGER' THEN CAST(JSON_UNQUOTE(JSON_EXTRACT(draft_json, '$.is_noindex')) AS UNSIGNED) ELSE is_noindex END;

-- 0 Published, 10 Draft, 20 In review, 30 Changes requested, 40 Scheduled, 127 Deleted.
-- Unconverted legacy rows retain their original status so content migration can reconstruct publication history.
-- Unexpected legacy workflow values are retained in status for manual review.
UPDATE spages SET status = CASE
  WHEN draft_json IS NULL OR TRIM(draft_json)='' THEN status
  WHEN status=127 THEN 127
  WHEN workflow=0 THEN 10
  WHEN workflow=10 THEN 20
  WHEN workflow=20 THEN 30
  WHEN workflow=30 THEN 0
  WHEN workflow=40 THEN 40
  ELSE workflow
END;

ALTER TABLE spages DROP COLUMN workflow;
