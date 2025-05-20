-- fill initial data for lookups here

-- lookups in virtual controllers
INSERT INTO fwcontrollers (igroup, icode, url, iname, model, access_level)
VALUES ('System', 'AdminLogTypes', '/Admin/LogTypes', 'Log Types', 'FwLogTypes', 100),
       ('System', 'AdminAttCategories', '/Admin/AttCategories', 'Upload Categories', 'AttCategories', 50),
       ('System', 'AdminFwUpdates', '/Admin/FwUpdates', 'FW Updates', 'FwUpdates', 100)
;
UPDATE fwcontrollers
SET is_lookup=1;

-- att_categories
INSERT INTO att_categories (icode, iname) VALUES
('general', 'General images')
,('users', 'Member photos')
,('files', 'Files')
,('spage_banner', 'Page banners')
;

-- log_types
-- for tracking changes
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'added', 'Record Added');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'updated', 'Record Updated');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'deleted', 'Record Deleted');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'executed', 'Code Executed');
-- for user login audit
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'login', 'User Login');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'logoff', 'User Logoff');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'login_fail', 'Login Failed');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'chpwd', 'User changed login/pwd');
-- user selectable types
INSERT INTO log_types (itype, icode, iname) VALUES (10, 'comment', 'Comment');

update log_types set prio=id; -- set default priority

-- fwcron tasks
-- insert into fwcron(icode, cron) values ('example_sleep', '* * * * *') - example task to run every minute