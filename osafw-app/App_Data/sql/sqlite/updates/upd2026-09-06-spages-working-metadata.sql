-- Synchronize mutable list metadata from valid working drafts without changing content or revision history.
UPDATE spages SET
  meta_description = CASE
    WHEN json_type(CASE WHEN json_valid(draft_json) THEN draft_json END, '$.meta_description')='text'
      AND length(json_extract(draft_json, '$.meta_description'))<=255
      THEN json_extract(draft_json, '$.meta_description')
    ELSE meta_description
  END,
  redirect_url = CASE
    WHEN json_type(CASE WHEN json_valid(draft_json) THEN draft_json END, '$.redirect_url')='text'
      AND length(json_extract(draft_json, '$.redirect_url'))<=255
      THEN json_extract(draft_json, '$.redirect_url')
    ELSE redirect_url
  END,
  head_att_id = CASE
    WHEN json_type(CASE WHEN json_valid(draft_json) THEN draft_json END, '$.head_att_id')='null' THEN NULL
    WHEN json_type(CASE WHEN json_valid(draft_json) THEN draft_json END, '$.head_att_id')='integer'
      AND json_extract(draft_json, '$.head_att_id')=0 THEN NULL
    WHEN json_type(CASE WHEN json_valid(draft_json) THEN draft_json END, '$.head_att_id')='integer'
      AND json_extract(draft_json, '$.head_att_id')>0
      AND EXISTS (SELECT 1 FROM att WHERE id=json_extract(spages.draft_json, '$.head_att_id'))
      THEN CAST(json_extract(draft_json, '$.head_att_id') AS INTEGER)
    ELSE head_att_id
  END;
