DECLARE @ConstraintName nvarchar(256);
SELECT @ConstraintName = name 
FROM sys.default_constraints 
WHERE parent_object_id = object_id('att')
AND col_name(parent_object_id, parent_column_id) = 'fsize';

-- Drop the default constraint
IF @ConstraintName IS NOT NULL
    EXEC('ALTER TABLE att DROP CONSTRAINT ' + @ConstraintName);
GO

-- Alter the column data type
ALTER TABLE att ALTER COLUMN fsize BIGINT;
GO

ALTER TABLE att ADD DEFAULT ((0)) FOR fsize;
GO
