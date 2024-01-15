-- fill initial data for lookups here

-- lookup manager table definitions
insert into lookup_manager_tables (tname, iname, access_level) VALUES
('events','Events', 100)
, ('log_types','Log Types', 100)
, ('att_categories','Upload Categories', NULL)
;

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
-- for user login audit
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'login', 'User Login');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'logoff', 'User Logoff');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'login_fail', 'Login Failed');
INSERT INTO log_types (itype, icode, iname) VALUES (0, 'chpwd', 'User changed login/pwd');
-- user selectable types
INSERT INTO log_types (itype, icode, iname) VALUES (10, 'comment', 'Comment');

update log_types set prio=id; -- set default priority