-- fill initial data for lookups here

-- lookup manager table definitions
insert into lookup_manager_tables (tname, iname, access_level) VALUES
('events','Events', 100)
, ('att_categories','Upload Categories', NULL)
;

-- att_categories
INSERT INTO att_categories (icode, iname) VALUES
('general', 'General images')
,('users', 'Member photos')
,('files', 'Files')
,('spage_banner', 'Page banners')
;

-- events
INSERT INTO events (icode, iname) VALUES ('login',    'User login');
INSERT INTO events (icode, iname) VALUES ('logoff',   'User logoff');
INSERT INTO events (icode, iname) VALUES ('login_fail', 'Login failed');
INSERT INTO events (icode, iname) VALUES ('chpwd',    'User changed login/pwd');
INSERT INTO events (icode, iname) VALUES ('users_add',    'New user added');
INSERT INTO events (icode, iname) VALUES ('users_upd',    'User updated');
INSERT INTO events (icode, iname) VALUES ('users_del',    'User deleted');
