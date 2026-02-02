-- tables for ROLE BASED ACCESS CONTROL (optional)
/*Resources*/
DROP TABLE IF EXISTS resources;
CREATE TABLE resources (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  icode                 TEXT NOT NULL,

  iname                 TEXT NOT NULL default '',
  idesc                 TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_resources_icode ON resources (icode);

/*Permissions*/
DROP TABLE IF EXISTS permissions;
CREATE TABLE permissions (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,
  resources_id          INTEGER NULL,
  icode                 TEXT NOT NULL,

  iname                 TEXT NOT NULL default '',
  idesc                 TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  FOREIGN KEY (resources_id) REFERENCES resources(id)
);
CREATE UNIQUE INDEX UX_permissions_icode ON permissions (icode);
CREATE INDEX IX_permissions_resources_id ON permissions (resources_id);

/*Roles*/
DROP TABLE IF EXISTS roles;
CREATE TABLE roles (
  id                    INTEGER PRIMARY KEY AUTOINCREMENT,

  iname                 TEXT NOT NULL default '',
  idesc                 TEXT,
  prio                  INTEGER NOT NULL DEFAULT 0,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0
);
CREATE UNIQUE INDEX UX_roles_iname ON roles (iname);

/*Assigned permissions for all roles and resource/permissions*/
DROP TABLE IF EXISTS roles_resources_permissions;
CREATE TABLE roles_resources_permissions (
  roles_id              INTEGER NOT NULL,
  resources_id          INTEGER NOT NULL,
  permissions_id        INTEGER NOT NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  PRIMARY KEY (roles_id, resources_id, permissions_id),
  FOREIGN KEY (roles_id) REFERENCES roles(id),
  FOREIGN KEY (resources_id) REFERENCES resources(id),
  FOREIGN KEY (permissions_id) REFERENCES permissions(id)
);
CREATE INDEX IX_roles_resources_permissions_resources_id ON roles_resources_permissions (resources_id, roles_id, permissions_id);
CREATE INDEX IX_roles_resources_permissions_permissions_id ON roles_resources_permissions (permissions_id, roles_id, resources_id);
CREATE INDEX IX_roles_resources_permissions_rpr ON roles_resources_permissions (resources_id, permissions_id, roles_id);

/*Roles for all users*/
DROP TABLE IF EXISTS users_roles;
CREATE TABLE users_roles (
  users_id              INTEGER NOT NULL,
  roles_id              INTEGER NOT NULL,

  status                INTEGER NOT NULL DEFAULT 0,
  add_time              DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
  add_users_id          INTEGER DEFAULT 0,
  upd_time              DATETIME,
  upd_users_id          INTEGER DEFAULT 0,

  PRIMARY KEY (users_id, roles_id),
  FOREIGN KEY (users_id) REFERENCES users(id),
  FOREIGN KEY (roles_id) REFERENCES roles(id)
);
CREATE INDEX IX_users_roles_roles_id ON users_roles (roles_id, users_id);


--- fill tables

INSERT INTO fwcontrollers (igroup, icode, url, iname, model, access_level)
VALUES ('RBAC', 'AdminPermissions', '/Admin/Permissions', 'Permissions', 'Permissions', 100),
       ('RBAC', 'AdminResources', '/Admin/Resources', 'Resources', 'Resources', 100),
       ('RBAC', 'AdminRoles', '/Admin/Roles', 'Roles', 'Roles', 100);

-- default Permissions
INSERT INTO permissions (icode, iname) VALUES ('list', 'List');
INSERT INTO permissions (icode, iname) VALUES ('view', 'View');
INSERT INTO permissions (icode, iname) VALUES ('add', 'Add');
INSERT INTO permissions (icode, iname) VALUES ('edit', 'Edit');
INSERT INTO permissions (icode, iname) VALUES ('del', 'Delete');
UPDATE permissions SET prio=id;

-- default Roles
INSERT INTO roles (iname) VALUES ('Admin');
INSERT INTO roles (iname) VALUES ('Manager');
INSERT INTO roles (iname) VALUES ('Employee');
INSERT INTO roles (iname) VALUES ('Customer Service');
INSERT INTO roles (iname) VALUES ('Vendor');
INSERT INTO roles (iname) VALUES ('Customer');

-- default Resources
INSERT INTO resources (icode,iname) VALUES ('Main', 'Main Dashboard');
INSERT INTO resources (icode,iname) VALUES ('Att', 'Uploads');
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
