// Att Categories Dictionary Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

namespace osafw
{
    public class AdminAttCategoriesController : FwAdminController
    {
        public static new int access_level = Users.ACL_MANAGER;

        protected AttCategories model;

        public override void init(FW fw)
        {
            base.init(fw);
            model = fw.model<AttCategories>();
            model0 = model;

            // initialization
            base_url = "/Admin/AttCategories";
            required_fields = "iname";
            save_fields = "icode iname idesc status";

            search_fields = "iname idesc";
            list_sortdef = "iname asc";   // default sorting: name, asc|desc direction
            list_sortmap = Utils.qh("id|id icode|icode iname|iname add_time|add_time status|status");
        }
    }
}