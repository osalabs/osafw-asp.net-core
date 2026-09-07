-- Synchronize mutable list metadata from valid working drafts without changing content or revision history.
UPDATE spages SET
  meta_description = CASE
    WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.meta_description'))='STRING'
      AND CHAR_LENGTH(JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.meta_description')))<=255
      THEN JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.meta_description'))
    ELSE meta_description
  END,
  redirect_url = CASE
    WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.redirect_url'))='STRING'
      AND CHAR_LENGTH(JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.redirect_url')))<=255
      THEN JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.redirect_url'))
    ELSE redirect_url
  END,
  head_att_id = CASE
    WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.head_att_id'))='NULL' THEN NULL
    WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.head_att_id'))='INTEGER'
      AND CAST(JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.head_att_id')) AS SIGNED)=0 THEN NULL
    WHEN JSON_TYPE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.head_att_id'))='INTEGER'
      AND CAST(JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.head_att_id')) AS SIGNED)>0
      AND EXISTS (SELECT 1 FROM att WHERE id=CAST(JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(spages.draft_json) THEN spages.draft_json ELSE NULL END, '$.head_att_id')) AS SIGNED))
      THEN CAST(JSON_UNQUOTE(JSON_EXTRACT(CASE WHEN JSON_VALID(draft_json) THEN draft_json ELSE NULL END, '$.head_att_id')) AS SIGNED)
    ELSE head_att_id
  END;
