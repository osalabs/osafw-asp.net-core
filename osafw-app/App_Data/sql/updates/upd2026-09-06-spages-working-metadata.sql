-- Synchronize mutable list metadata from valid working drafts without changing content or revision history.
UPDATE s SET
  meta_description = CASE WHEN draft.meta_description_type=1 AND DATALENGTH(draft.meta_description_value)<=510 THEN draft.meta_description_value ELSE s.meta_description END,
  redirect_url = CASE WHEN draft.redirect_url_type=1 AND DATALENGTH(draft.redirect_url_value)<=510 THEN draft.redirect_url_value ELSE s.redirect_url END,
  head_att_id = CASE
    WHEN draft.head_att_id_type=0 THEN NULL
    WHEN draft.head_att_id_type=2 AND TRY_CONVERT(DECIMAL(38,10), draft.head_att_id_value)=0 THEN NULL
    WHEN draft.head_att_id_type=2
      AND TRY_CONVERT(INT, draft.head_att_id_value)>0
      AND TRY_CONVERT(DECIMAL(38,10), draft.head_att_id_value)=TRY_CONVERT(INT, draft.head_att_id_value)
      AND EXISTS (SELECT 1 FROM att WHERE id=TRY_CONVERT(INT, draft.head_att_id_value))
      THEN TRY_CONVERT(INT, draft.head_att_id_value)
    ELSE s.head_att_id
  END
FROM spages s
OUTER APPLY (
  SELECT
    MAX(CASE WHEN [key]=N'meta_description' THEN [type] END) AS meta_description_type,
    MAX(CASE WHEN [key]=N'meta_description' THEN [value] END) AS meta_description_value,
    MAX(CASE WHEN [key]=N'redirect_url' THEN [type] END) AS redirect_url_type,
    MAX(CASE WHEN [key]=N'redirect_url' THEN [value] END) AS redirect_url_value,
    MAX(CASE WHEN [key]=N'head_att_id' THEN [type] END) AS head_att_id_type,
    MAX(CASE WHEN [key]=N'head_att_id' THEN [value] END) AS head_att_id_value
  FROM OPENJSON(CASE WHEN ISJSON(s.draft_json)=1 THEN s.draft_json END)
) draft;
GO
