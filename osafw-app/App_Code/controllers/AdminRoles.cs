// Demo Dynamic Admin controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class AdminRolesController : FwDynamicController
{
    public static new int access_level = Users.ACL_ADMIN;

    protected Roles model;

    public override void init(FW fw)
    {
        base.init(fw);
        // use if config doesn't contains model name
        // model0 = fw.model(Of Roles)()
        // model = model0

        base_url = "/Admin/Roles";
        this.loadControllerConfig();
        model = model0 as Roles;
        db = model.getDB(); // model-based controller works with model's db

        model_related = fw.model<Roles>();
        is_userlists = true;

        // override sortmap for date fields
        // list_sortmap["fdate_pop_str"] = "fdate_pop";
    }

    public override Hashtable ShowAction(int id = 0)
    {
        var ps = base.ShowAction(id);
        var item = ps["i"] as Hashtable;
        var fields = ps["fields"] as ArrayList;

        // roles_resources_permissions matrix
        var defMatrix = defByFieldname("roles_resources_permissions", fields);
        var permissions = fw.model<Permissions>().list();
        defMatrix["permissions_header"] = permissions;
        defMatrix["permissions_count"] = permissions.Count;

        defMatrix["resources"] = fw.model<RolesResourcesPermissions>().resourcesMatrixByRole(id, permissions);

        return ps;
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        var ps = base.ShowFormAction(id);
        var item = ps["i"] as Hashtable;
        var fields = ps["fields"] as ArrayList;

        // roles_resources_permissions matrix
        var defMatrix = defByFieldname("roles_resources_permissions", fields);
        var permissions = fw.model<Permissions>().list();
        defMatrix["permissions_header"] = permissions;
        defMatrix["permissions_count"] = permissions.Count;

        defMatrix["resources"] = fw.model<RolesResourcesPermissions>().resourcesMatrixByRole(id, permissions);

        return ps;
    }

    public override int modelAddOrUpdate(int id, Hashtable fields)
    {
        id = base.modelAddOrUpdate(id, fields);

        // update roles_resources_permissions matrix
        var hresources_permissions = reqh("rp"); // contains checked items only
        fw.model<RolesResourcesPermissions>().updateMatrixByRole(id, hresources_permissions);

        return id;
    }
}