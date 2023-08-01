// RolesResourcesPermissions model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

namespace osafw;

public class RolesResourcesPermissions : FwModel
{
    public RolesResourcesPermissions() : base()
    {
        db_config = "";
        table_name = "roles_resources_permissions";
    }

    public override void init(FW fw)
    {
        base.init(fw);
        //TODO
        junction_model_main = fw.model<Roles>();
        junction_field_main_id = "roles_id";
        junction_model_linked = fw.model<Permissions>();
        junction_field_linked_id = "permissions_id";
    }

}