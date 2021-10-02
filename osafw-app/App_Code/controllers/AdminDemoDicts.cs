// Demo Dictionary Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2013 Oleg Savchuk www.osalabs.com

namespace osafw
{
    public class AdminDemoDictsController : FwAdminController
    {
        public static new int access_level = Users.ACL_MANAGER;

        protected DemoDicts model;

        public override void init(FW fw)
        {
            base.init(fw);
            model = fw.model<DemoDicts>();
            model0 = model;

            // initialization
            base_url = "/Admin/DemoDicts";
            required_fields = "iname";
            save_fields = "iname idesc status";

            search_fields = "iname idesc";
            list_sortdef = "iname asc";   // default sorting: name, asc|desc direction
            list_sortmap = Utils.qh("id|id iname|iname add_time|add_time status|status");
        }
    }
}