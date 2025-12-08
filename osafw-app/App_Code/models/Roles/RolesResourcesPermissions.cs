// RolesResourcesPermissions model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class RolesResourcesPermissions : FwModel<RolesResourcesPermissions.Row>
{
    public class Row
    {
        public int roles_id { get; set; }
        public int resources_id { get; set; }
        public int permissions_id { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public const string CACHE_KEY_UPDATED = "roles_resources_permissions_updated";

    const string KEY_DELIM = "#";

    public FwModel junction_model_permissions;
    public string junction_field_permissions_id;

    public RolesResourcesPermissions() : base()
    {
        db_config = "";
        table_name = "roles_resources_permissions";
    }

    public override void init(FW fw)
    {
        base.init(fw);

        junction_model_main = fw.model<Roles>();
        junction_field_main_id = "roles_id";
        junction_model_linked = fw.model<Resources>();
        junction_field_linked_id = "resources_id";

        junction_model_permissions = fw.model<Permissions>();
        junction_field_permissions_id = "permissions_id";
    }

    public string matrixKey(object resources_id, object permissions_id)
    {
        return resources_id + KEY_DELIM + permissions_id;
    }

    // extract resources_id and permissions_id from key
    public void extractKey(string key, out int resources_id, out int permissions_id)
    {
        string str_resources_id = "";
        string str_permissions_id = "";
        Utils.split2(KEY_DELIM, key, ref str_resources_id, ref str_permissions_id);

        resources_id = str_resources_id.toInt();
        permissions_id = str_permissions_id.toInt();
    }

    /// <summary>
    /// check if at least one record exists for resource/permission and multiple roles - i.e. user has a role with resource's permission
    /// </summary>
    /// <param name="resources_id"></param>
    /// <param name="permissions_id"></param>
    /// <param name="roles_ids"></param>
    /// <returns></returns>
    public bool isExistsByResourcePermissionRoles(int resources_id, int permissions_id, IList roles_ids)
    {
        var where = new Hashtable
        {
            { "resources_id", resources_id },
            { "permissions_id", permissions_id },
            { "roles_id", db.opIN(roles_ids) }
        };
        var value = db.value(table_name, where, "1");

        return value.toBool();
    }

    /// <summary>
    /// list of records for given role and resource
    /// </summary>
    /// <param name="roles_id"></param>
    /// <param name="resources_id"></param>
    /// <returns></returns>
    public DBList listByRoleResource(int roles_id, int resources_id)
    {
        return db.array(table_name, DB.h("roles_id", roles_id, "resources_id", resources_id));
    }

    /// <summary>
    /// list of records for given MULTIPLE roles and resources
    /// </summary>
    /// <param name="roles_ids"></param>
    /// <param name="resources_ids"></param>
    /// <returns></returns>
    public DBList listByRolesResources(IList roles_ids, IList resources_ids)
    {
        return db.array(table_name, DB.h("roles_id", db.opIN(roles_ids), "resources_id", db.opIN(resources_ids)));
    }

    /// <summary>
    /// list of records for given MULTIPLE roles and permissions
    /// </summary>
    /// <param name="roles_ids"></param>
    /// <param name="permissions_ids"></param>
    /// <returns></returns>
    public DBList listByRolesPermissions(IList roles_ids, IList permissions_ids)
    {
        return db.array(table_name, DB.h("roles_id", db.opIN(roles_ids), "permissions_id", db.opIN(permissions_ids)));
    }

    /// <summary>
    /// return hashtable of [resources_id#permissions_id => row] for given role and resource
    /// </summary>
    /// <param name="roles_id"></param>
    /// <param name="resources_id"></param>
    /// <returns></returns>
    public Hashtable matrixRowByRoleResource(int roles_id, int resources_id)
    {
        var result = new Hashtable();

        var rows = listByRoleResource(roles_id, resources_id);
        foreach (Hashtable row in rows)
        {
            result[matrixKey(row["resources_id"], row["permissions_id"])] = row;
        }
        return result;
    }

    public ArrayList resourcesMatrixByRole(int roles_id, DBList permissions)
    {
        var resources = fw.model<Resources>().list().toArrayList();

        // for each resource - get permissions for this role
        foreach (Hashtable resource in resources)
        {
            var permissions_cols = new ArrayList();
            resource["permissions_cols"] = permissions_cols;

            // load permissions for this resource
            var hpermissions = fw.model<RolesResourcesPermissions>().matrixRowByRoleResource(roles_id, resource["id"].toInt());

            foreach (Hashtable permission in permissions)
            {
                var permission_col = new Hashtable();
                //permission_col["resources_id"] = resource["id"];
                //permission_col["permissions_id"] = permission["id"];
                var key = fw.model<RolesResourcesPermissions>().matrixKey(resource["id"], permission["id"]);
                permission_col["key"] = key;
                permission_col["is_checked"] = hpermissions.ContainsKey(key);
                permissions_cols.Add(permission_col);
            }
        }

        return resources;
    }

    internal void updateMatrixByRole(int roles_id, Hashtable hresources_permissions)
    {
        var permissions = fw.model<Permissions>().list();

        Hashtable fields = [];
        Hashtable where = [];

        // set all fields as under update
        fields[field_status] = STATUS_UNDER_UPDATE;
        where[junction_field_main_id] = roles_id;
        db.update(table_name, fields, where);

        foreach (string key in hresources_permissions.Keys)
        {
            if (!hresources_permissions[key].toBool())
                continue; // skip unchecked

            extractKey(key, out int resources_id, out int permissions_id);

            fields = [];
            fields[junction_field_main_id] = roles_id;
            fields[junction_field_linked_id] = resources_id;
            fields[junction_field_permissions_id] = permissions_id;
            fields[field_status] = STATUS_ACTIVE;
            fields[field_upd_users_id] = fw.userId;
            fields[field_upd_time] = DB.NOW;

            where = [];
            where[junction_field_main_id] = roles_id;
            where[junction_field_linked_id] = resources_id;
            where[junction_field_permissions_id] = permissions_id;
            db.updateOrInsert(table_name, fields, where);
        }

        // remove those who still not updated (so removed)
        where = [];
        where[junction_field_main_id] = roles_id;
        where[field_status] = STATUS_UNDER_UPDATE;
        db.del(table_name, where);

        // set in cache time when roles updated
        FwCache.setValue(CACHE_KEY_UPDATED, DateTime.Now);
    }
}