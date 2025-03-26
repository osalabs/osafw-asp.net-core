-- Spages - custom head

ALTER TABLE spages ADD custom_head NVARCHAR(MAX);

ALTER TABLE users ALTER COLUMN email NVARCHAR(255) NOT NULL;