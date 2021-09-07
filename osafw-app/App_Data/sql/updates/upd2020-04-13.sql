ALTER TABLE demos DROP COLUMN fyesno;
GO

-- then chagne contraint name
ALTER TABLE demos DROP CONSTRAINT DF__demos__fyesno__090A5324;
ALTER TABLE demos DROP COLUMN fyesno;
GO

ALTER TABLE demos ADD fyesno                BIT NOT NULL DEFAULT 0
GO