// ResourcesPermissions model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

namespace osafw;

public class ResourcesPermissions : FwModel
{
    public ResourcesPermissions() : base()
    {
        db_config = "";
        table_name = "resources_permissions";
    }

    public override void init(FW fw)
    {
        base.init(fw);
        junction_model_main = fw.model<Resources>();
        junction_field_main_id = "resources_id";
        junction_model_linked = fw.model<Permissions>();
        junction_field_linked_id = "permissions_id";
    }
}