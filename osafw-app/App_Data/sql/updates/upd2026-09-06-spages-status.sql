-- Keep standard list metadata aligned with the working draft before workflow is folded into status.
-- Missing, non-scalar, or non-convertible draft fields retain their existing column values.
UPDATE spages SET
  iname = COALESCE(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.iname'), iname),
  url = COALESCE(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.url'), url),
  parent_id = COALESCE(TRY_CONVERT(INT, JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.parent_id')), parent_id),
  template = COALESCE(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.template'), template),
  prio = COALESCE(TRY_CONVERT(INT, JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.prio')), prio),
  is_snippet = COALESCE(TRY_CONVERT(TINYINT, JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.is_snippet')), is_snippet),
  access_level = COALESCE(TRY_CONVERT(INT, JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.access_level')), access_level),
  is_nav_visible = COALESCE(TRY_CONVERT(TINYINT, JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.is_nav_visible')), is_nav_visible),
  nav_title = COALESCE(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.nav_title'), nav_title),
  meta_title = COALESCE(JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.meta_title'), meta_title),
  is_noindex = COALESCE(TRY_CONVERT(TINYINT, JSON_VALUE(CASE WHEN ISJSON(draft_json)=1 THEN draft_json END, '$.is_noindex')), is_noindex);
GO

-- 0 Published, 10 Draft, 20 In review, 30 Changes requested, 40 Scheduled, 127 Deleted.
-- Unconverted legacy rows retain their original status so content migration can reconstruct publication history.
-- Unexpected legacy workflow values are retained in status for manual review.
UPDATE spages SET status = CASE
  WHEN NULLIF(LTRIM(RTRIM(draft_json)), N'') IS NULL THEN status
  WHEN status=127 THEN 127
  WHEN workflow=0 THEN 10
  WHEN workflow=10 THEN 20
  WHEN workflow=20 THEN 30
  WHEN workflow=30 THEN 0
  WHEN workflow=40 THEN 40
  ELSE workflow
END;
GO

DECLARE @workflowDefaultConstraint sysname;
DECLARE @dropWorkflowDefaultSql nvarchar(max);
SELECT @workflowDefaultConstraint = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON c.default_object_id=dc.object_id
WHERE dc.parent_object_id=OBJECT_ID(N'dbo.spages')
  AND c.name=N'workflow';

IF @workflowDefaultConstraint IS NOT NULL
BEGIN
  SET @dropWorkflowDefaultSql=N'ALTER TABLE dbo.spages DROP CONSTRAINT ' + QUOTENAME(@workflowDefaultConstraint);
  EXEC sp_executesql @dropWorkflowDefaultSql;
END

ALTER TABLE spages DROP COLUMN workflow;
GO
