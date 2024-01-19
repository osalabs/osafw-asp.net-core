-- Att upgrades

ALTER TABLE att ADD fwentities_id         INT NULL CONSTRAINT FK_att_fwentities FOREIGN KEY REFERENCES fwentities(id);
GO
ALTER TABLE att ALTER COLUMN item_id INT NULL;
GO

UPDATE att set item_id=NULL where item_id=0;
GO

-- remove default 0
DECLARE @ConstraintName nvarchar(256);
SET @ConstraintName = (
    SELECT name 
    FROM sys.default_constraints 
    WHERE parent_object_id = object_id('att')
    AND type = 'D' 
    AND parent_column_id = (
        SELECT column_id 
        FROM sys.columns 
        WHERE object_id = object_id('att') 
        AND name = 'item_id'
    )
);

IF @ConstraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE att DROP CONSTRAINT ' + @ConstraintName);
END
GO

-- migrate att.table_name to att.fwentities
INSERT INTO fwentities (icode, iname)
SELECT DISTINCT
    table_name,
    UPPER(LEFT(table_name, 1)) + LOWER(SUBSTRING(table_name, 2, LEN(table_name)))
FROM
    att a
WHERE
    table_name>''
    and a.table_name NOT IN (SELECT icode FROM fwentities);

UPDATE a
SET a.fwentities_id = f.id
FROM
    att a
JOIN
    fwentities f ON a.table_name = f.icode;

-- and now drop table_name and related index/default
DROP INDEX IX_att_table_name_item_id ON att;

DECLARE @ConstraintName2 nvarchar(256);
SET @ConstraintName2 = (
    SELECT name 
    FROM sys.default_constraints 
    WHERE parent_object_id = object_id('att')
    AND type = 'D' 
    AND parent_column_id = (
        SELECT column_id 
        FROM sys.columns 
        WHERE object_id = object_id('att') 
        AND name = 'table_name'
    )
);

IF @ConstraintName2 IS NOT NULL
BEGIN
    EXEC('ALTER TABLE att DROP CONSTRAINT ' + @ConstraintName2);
END
ALTER TABLE att DROP COLUMN table_name;
GO

-- new table att_links
CREATE TABLE att_links (
  att_id                INT NOT NULL CONSTRAINT FK_att_links_att FOREIGN KEY REFERENCES att(id),
  fwentities_id         INT NOT NULL CONSTRAINT FK_att_links_fwentities FOREIGN KEY REFERENCES fwentities(id), -- related to entity
  item_id               INT NOT NULL,

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under change, deleted instantly*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,

  INDEX UX_att_links UNIQUE (fwentities_id, item_id, att_id),
  INDEX IX_att_links_att (att_id, fwentities_id, item_id)
);
GO

-- migrate data 
-- Step 1: Insert new records into fwentities from att_table_link
INSERT INTO fwentities (icode, iname)
SELECT DISTINCT
    atl.table_name
    , UPPER(LEFT(atl.table_name, 1)) + LOWER(SUBSTRING(atl.table_name, 2, LEN(atl.table_name)))
FROM
    att_table_link atl
WHERE
    table_name>''
    and atl.table_name NOT IN (SELECT icode FROM fwentities);

-- Step 2: Migrate data to att_links
INSERT INTO att_links (att_id, fwentities_id, item_id, status, add_time, add_users_id)
SELECT
    atl.att_id,
    fe.id AS fwentities_id,
    atl.item_id,
    atl.status,
    atl.add_time,
    atl.add_users_id
FROM
    att_table_link atl
JOIN
    fwentities fe ON atl.table_name = fe.icode;

-- drop deprecated table
DROP TABLE att_table_link;
GO
