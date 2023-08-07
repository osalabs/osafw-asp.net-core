-- tables for ROLE BASED ACCESS CONTROL (optional)
/*Resources*/
DROP TABLE IF EXISTS resources;
CREATE TABLE resources (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  icode                 NVARCHAR(64) NOT NULL, -- controller, ex: AdminUsers

  iname                 NVARCHAR(255) NOT NULL default '', -- ex: Manage Members
  idesc                 NVARCHAR(MAX),
  prio                  INT NOT NULL DEFAULT 0,     /*0-on insert, then =id, default order by prio asc,iname*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
GO

/*Permissions*/
DROP TABLE IF EXISTS permissions;
CREATE TABLE permissions (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  resources_id          INT NULL FOREIGN KEY REFERENCES resources(id), -- optional link to a specific resource if permission specific to that resource
  icode                 NVARCHAR(64) NOT NULL, -- list, view, add, edit, del, del_perm or custom codes

  iname                 NVARCHAR(255) NOT NULL default '', -- View, Add, Edit, Delete, ...
  idesc                 NVARCHAR(MAX),
  prio                  INT NOT NULL DEFAULT 0,     /*0-on insert, then =id, default order by prio asc,iname*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  INDEX IX_permissions_resources_id (resources_id)
);
GO

/*Roles*/
DROP TABLE IF EXISTS roles;
CREATE TABLE roles (
  id                    INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  iname                 NVARCHAR(255) NOT NULL default '', -- Admin, Manager, Employee, External, Guest, ...
  idesc                 NVARCHAR(MAX),
  prio                  INT NOT NULL DEFAULT 0,     /*0-on insert, then =id, default order by prio asc,iname*/

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
GO

/*Assigned permissions for all roles and resource/permissions*/
DROP TABLE IF EXISTS roles_resources_permissions;
CREATE TABLE roles_resources_permissions (
  roles_id              INT NOT NULL FOREIGN KEY REFERENCES roles(id),
  resources_id          INT NOT NULL FOREIGN KEY REFERENCES resources(id),
  permissions_id        INT NOT NULL FOREIGN KEY REFERENCES permissions(id),

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under change, deleted instantly*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (roles_id, resources_id, permissions_id),
  INDEX IX_roles_resources_permissions_resources_id (resources_id, roles_id, permissions_id),
  INDEX IX_roles_resources_permissions_permissions_id (permissions_id, roles_id, resources_id),
  INDEX IX_roles_resources_permissions_rpr (resources_id, permissions_id, roles_id) -- for quick check resrouce/permissions access by set of roles
);
GO

/*Roles for all users*/
DROP TABLE IF EXISTS users_roles;
CREATE TABLE users_roles (
  users_id              INT NOT NULL FOREIGN KEY REFERENCES users(id),
  roles_id              INT NOT NULL FOREIGN KEY REFERENCES roles(id),

  status                TINYINT NOT NULL DEFAULT 0,        /*0-ok, 1-under change, deleted instantly*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0,

  PRIMARY KEY (users_id, roles_id),
  INDEX IX_users_roles_roles_id (roles_id, users_id)
);
GO


--- fill tables

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
-- INSERT INTO permissions (icode, iname) VALUES ('del_perm', 'Permanently Delete'); -- DeleteAction with permanent TODO do we need this?
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