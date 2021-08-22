// User Views Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw
{
    public class MyViewsController : FwAdminController
    {
        public static new int access_level = Users.ACL_MEMBER;

        protected UserViews model;

        public override void init(FW fw)
        {
            base.init(fw);
            model = fw.model<UserViews>();
            model0 = model;

            // initialization
            base_url = "/My/Views";
            required_fields = "iname";
            save_fields = "screen iname status";
            save_fields_checkboxes = "is_system";

            search_fields = "iname";
            list_sortdef = "iname asc";   // default sorting: name, asc|desc direction
            list_sortmap = Utils.qh("id|id iname|iname add_time|add_time entity|screen");

            related_id = reqs("related_id");
        }

        public override Hashtable initFilter(string session_key = null)
        {
            var result = base.initFilter(session_key);
            if (!this.list_filter.ContainsKey("screen"))
                this.list_filter["screen"] = related_id;
            return this.list_filter;
        }

        public override void setListSearch()
        {
            list_where = " status<>127 and add_users_id=@add_users_id";
            list_where_params["@add_users_id"] = Users.id;

            base.setListSearch();

            if (!string.IsNullOrEmpty((string)list_filter["screen"]))
            {
                this.list_where += " and screen=@screen";
                this.list_where_params["@screen"] = list_filter["screen"];
            }
        }

        public override Hashtable ShowFormAction(string form_id = "")
        {
            this.form_new_defaults = new();
            this.form_new_defaults["screen"] = related_id;
            var ps = base.ShowFormAction(form_id);
            ps["is_admin"] = Utils.f2int(fw.Session("access_level")) == Users.ACL_ADMIN;
            return ps;
        }

        public override Hashtable SaveAction(string form_id = "")
        {
            if (this.save_fields == null)
                throw new Exception("No fields to save defined, define in Controller.save_fields");

            Hashtable item = reqh("item");
            int id = Utils.f2int(form_id);
            var success = true;
            var is_new = (id == 0);

            try
            {
                Validate(id, item);
                // load old record if necessary
                // Dim item_old As Hashtable = model0.one(id)

                Hashtable itemdb = FormUtils.filter(item, this.save_fields);
                FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes);

                if (is_new)
                    // read new filter data from session
                    itemdb["idesc"] = Utils.jsonEncode(fw.Session("_filter_" + item["screen"]));

                id = this.modelAddOrUpdate(id, itemdb);
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