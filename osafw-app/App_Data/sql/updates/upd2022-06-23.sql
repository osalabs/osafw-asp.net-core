ALTER TABLE demo_dicts ADD prio                  INT NOT NULL DEFAULT 0;
GO
UPDATE demo_dicts SET prio=id;
GO
