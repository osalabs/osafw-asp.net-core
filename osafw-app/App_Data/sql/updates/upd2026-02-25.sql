--- migrate lookup manager
INSERT INTO resources (icode,iname) VALUES ('AdminLookups', 'Lookup Manager');
INSERT INTO roles_resources_permissions (roles_id, resources_id, permissions_id, add_time, add_users_id, upd_time, upd_users_id)
SELECT roles_id, (select id from resources where icode='AdminLookups'), permissions_id, add_time, add_users_id, upd_time, upd_users_id
FROM roles_resources_permissions
where resources_id IN (select id from resources where icode='AdminLookupManager');

DELETE from roles_resources_permissions where resources_id IN (select id from resources where icode IN ('AdminLookupManager', 'AdminLookupManagerTables'));
DELETE from resources where icode IN ('AdminLookupManager', 'AdminLookupManagerTables');
