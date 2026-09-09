-- persisted list column widths; FwUpdates applies each update file once

ALTER TABLE user_views ADD COLUMN widths TEXT NOT NULL DEFAULT '{}';
