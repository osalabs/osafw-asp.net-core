insert into lookup_manager_tables (tname, iname, url, access_level) VALUES
('permissions','Permissions', '', 100)
, ('resources','Resources', '', 100)
, ('roles','Roles', '/Admin/Roles', 100)
GO

-- default Permissions
INSERT INTO permissions (icode, iname) VALUES ('list', 'List');   -- IndexAction
INSERT INTO permissions (icode, iname) VALUES ('view', 'View');   -- ShowAction, ShowFormAction
INSERT INTO permissions (icode, iname) VALUES ('add', 'Add');     -- SaveAction(id=0)
INSERT INTO permissions (icode, iname) VALUES ('edit', 'Edit');   -- SaveAction
INSERT INTO permissions (icode, iname) VALUES ('del', 'Delete');  -- ShowDeleteAction, DeleteAction
INSERT INTO permissions (icode, iname) VALUES ('del_perm', 'Permanently Delete'); -- DeleteAction with permanent TODO do we need this?
UPDATE permissions SET prio=id;
GO

-- default Roles
INSERT INTO roles (iname) VALUES ('Admin');
INSERT INTO roles (iname) VALUES ('Manager');
INSERT INTO roles (iname) VALUES ('Employee');
INSERT INTO roles (iname) VALUES ('Customer Service');
INSERT INTO roles (iname) VALUES ('Vendor');
INSERT INTO roles (iname) VALUES ('Customer');
GO

-- default Resources
INSERT INTO resources (icode,iname) VALUES ('Main', 'Main Dashboard');
INSERT INTO resources (icode,iname) VALUES ('AdminReports', 'Reports');
--- optional demo
INSERT INTO resources (icode,iname) VALUES ('AdminDemos', 'Demo');
INSERT INTO resources (icode,iname) VALUES ('AdminDemosDynamic', 'Demo Dynamic');
INSERT INTO resources (icode,iname) VALUES ('AdminDemoDicts', 'Demo Dict');
--- optional demo end
INSERT INTO resources (icode,iname) VALUES ('AdminSpages', 'Pages');
INSERT INTO resources (icode,iname) VALUES ('AdminAtt', 'Manage Uploads');
INSERT INTO resources (icode,iname) VALUES ('AdminLookupManager', 'Lookup Manager');
INSERT INTO resources (icode,iname) VALUES ('AdminLookupManagerTables', 'Lookup Manager Table Definitions');
INSERT INTO resources (icode,iname) VALUES ('AdminRoles', 'Manage Roles');
INSERT INTO resources (icode,iname) VALUES ('AdminUsers', 'Manage Members');
INSERT INTO resources (icode,iname) VALUES ('AdminSettings', 'Site Settings');
INSERT INTO resources (icode,iname) VALUES ('MyViews', 'My Views');
INSERT INTO resources (icode,iname) VALUES ('MyLists', 'My Lists');
INSERT INTO resources (icode,iname) VALUES ('MySettings', 'My Profile');
INSERT INTO resources (icode,iname) VALUES ('MyPassword', 'Change Password');
UPDATE resources SET prio=id;
GO