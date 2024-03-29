// UsersRoles model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

namespace osafw;

public class UsersRoles : FwModel
{
    public UsersRoles() : base()
    {
        db_config = "";
        table_name = "users_roles";
    }

    public override void init(FW fw)
    {
        base.init(fw);
        junction_model_main = fw.model<Users>();
        junction_field_main_id = "users_id";
        junction_model_linked = fw.model<Roles>();
        junction_field_linked_id = "roles_id";
    }

}