--- per user date/time options

ALTER TABLE users ADD date_format TINYINT NOT NULL DEFAULT 0;
ALTER TABLE users ADD time_format TINYINT NOT NULL DEFAULT 0;
ALTER TABLE users ADD timezone NVARCHAR(64) NOT NULL DEFAULT 'UTC';