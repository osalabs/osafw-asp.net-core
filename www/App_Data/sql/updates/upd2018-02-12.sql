-- update - increase size of passwords

ALTER TABLE users DROP CONSTRAINT DF__users__pwd__02925FBF;
ALTER TABLE users ALTER COLUMN pwd varchar(255) NOT NULL;
ALTER TABLE users ADD CONSTRAINT DF__users__pwd DEFAULT ('') FOR pwd;

ALTER TABLE users ADD 
  pwd_reset             NVARCHAR(255) NULL,
  pwd_reset_time        datetime2 NULL;