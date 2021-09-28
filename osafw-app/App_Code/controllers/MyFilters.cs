// User Filters Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw
{
    public class MyFiltersController : FwAdminController
    {
        public static new int access_level = Users.ACL_MEMBER;

        protected UserFilters model;

        public override void init(FW fw)
        {
            base.init(fw);
            model = fw.model<UserFilters>();
            model0 = model;

            // initialization
            base_url = "/My/Filters";
            required_fields = "iname";
            save_fields = "icode iname status";
            save_fields_checkboxes = "is_system";

            search_fields = "iname";
            list_sortdef = "iname asc";   // default sorting: name, asc|desc direction
            list_sortmap = Utils.qh("id|id iname|iname add_time|add_time");

            related_id = reqs("related_id");
        }

        public override Hashtable initFilter(string session_key = null)
        {
            var result = base.initFilter(session_key);
            if (!this.list_filter.ContainsKey("icode"))
                this.list_filter["icode"] = related_id;
            return this.list_filter;
        }

        public override void setListSearch()
        {
            // only logged user lists
            list_where = " status<>127 and add_users_id=@add_users_id";
            list_where_params["@add_users_id"] = fw.userId;

            base.setListSearch();

            if (!string.IsNullOrEmpty((string)list_filter["icode"]))
            {
                this.list_where += " and icode=@icode";
                this.list_where_params["@icode"] = list_filter["icode"];
            }                
        }

        public override Hashtable ShowFormAction(string form_id = "")
        {
            this.form_new_defaults = new();
            this.form_new_defaults["icode"] = related_id;
            var ps = base.ShowFormAction(form_id);
            ps["is_admin"] = Utils.f2int(fw.Session("access_level")) == Users.ACL_ADMIN;
            return ps;
        }

        public override Hashtable SaveAction(string form_id = "")
        {
            if (this.save_fields == null)
                throw new Exception("No fields to save defined, define in Controller.save_fields");

            var item = reqh("item");
            var id = Utils.f2int(form_id);
            var success = true;
            var is_new = (id == 0);
            var is_overwrite = reqi("is_overwrite") == 1;

            try
            {
                if (is_new)
                    required_fields += " icode";
                Validate(id, item);
                // load old record if necessary
                DBRow item_old = model0.one(id);

                // also check that this filter is user's filter (cannot override system filter)
                if (item_old.Count > 0 && Utils.f2int(item_old["is_system"]) == 1)
                    throw new ApplicationException("Cannot overwrite system filter");

                Hashtable itemdb = FormUtils.filter(item, this.save_fields);
                FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes);

                if (is_new || is_overwrite)
                    // read new filter data from session
                    itemdb["idesc"] = Utils.jsonEncode(fw.Session("_filter_" + item["icode"]));

                id = this.modelAddOrUpdate(id, new DBRow(itemdb));
            }
            catch (ApplicationException ex)
            {
                success = false;
                this.setFormError(ex);
            }

            if (!string.IsNullOrEmpty(return_url))
                fw.redirect(return_url);

            return this.afterSave(success, id, is_new);
        }
    }
}