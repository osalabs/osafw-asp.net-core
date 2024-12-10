-- ability to group Lookup Manager Tables

ALTER TABLE lookup_manager_tables ADD igroup                NVARCHAR(64) NOT NULL DEFAULT '';