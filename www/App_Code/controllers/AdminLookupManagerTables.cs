// LookupManager Tables Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw
{
    public class AdminLookupManagerTablesController : FwAdminController
    {
        public static new int access_level = Users.ACL_SITEADMIN;

        protected LookupManagerTables model;

        public override void init(FW fw)
        {
            base.init(fw);
            model = fw.model<LookupManagerTables>();
            model0 = model;

            base_url = "/Admin/LookupManagerTables";
            required_fields = "tname iname";
            save_fields = "tname iname idesc header_text footer_text column_id columns column_names column_types column_groups groups status";
            save_fields_checkboxes = "is_one_form is_custom_form";

            search_fields = "tname iname";
            list_sortdef = "iname asc";
            list_sortmap = Utils.qh("id|id iname|iname tname|tname");
        }

        public override Hashtable ShowFormAction(string form_id = "")
        {
            Hashtable ps = new Hashtable();
            Hashtable item;
            int id = Utils.f2int(form_id);

            if (isGet())
            {
                if (id > 0)
                {
                    item = model0.one(id);
                    // convert comma separated to newline separated
                    item["list_columns"] = Utils.commastr2nlstr((string)item["list_columns"]);
                    item["columns"] = Utils.commastr2nlstr((string)item["columns"]);
                    item["column_names"] = Utils.commastr2nlstr((string)item["column_names"]);
                    item["column_types"] = Utils.commastr2nlstr((string)item["column_types"]);
                    item["column_groups"] = Utils.commastr2nlstr((string)item["column_groups"]);
                }
                else
                {
                    // set defaults here
                    item = new Hashtable();
                    Utils.mergeHash(item, this.form_new_defaults);
                }
            }
            else
            {
                // read from db
                item = model0.one(id);
                // convert comma separated to newline separated
                item["list_columns"] = Utils.commastr2nlstr((string)item["list_columns"]);
                item["columns"] = Utils.commastr2nlstr((string)item["columns"]);
                item["column_names"] = Utils.commastr2nlstr((string)item["column_names"]);
                item["column_types"] = Utils.commastr2nlstr((string)item["column_types"]);
                item["column_groups"] = Utils.commastr2nlstr((string)item["column_groups"]);

                // and merge new values from the form
                Utils.mergeHash(item, reqh("item"));
            }

            ps["add_users_id_name"] = fw.model<Users>().iname(item["add_users_id"]);
            ps["upd_users_id_name"] = fw.model<Users>().iname(item["upd_users_id"]);

            ps["id"] = id;
            ps["i"] = item;
            ps["return_url"] = return_url;
            ps["related_id"] = related_id;

            return ps;
        }

        public override Hashtable SaveAction(string form_id = "")
        {
            if (this.save_fields == null)
                throw new Exception("No fields to save defined, define in save_fields ");

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

                // convert from newline to comma str
                itemdb["list_columns"] = Utils.nlstr2commastr((string)itemdb["list_columns"]);
                itemdb["columns"] = Utils.nlstr2commastr((string)itemdb["columns"]);
                itemdb["column_names"] = Utils.nlstr2commastr((string)itemdb["column_names"]);
                itemdb["column_types"] = Utils.nlstr2commastr((string)itemdb["column_types"]);
                itemdb["column_groups"] = Utils.nlstr2commastr((string)itemdb["column_groups"]);

                id = this.modelAddOrUpdate(id, itemdb);
            }
            catch (ApplicationException ex)
            {
                success = false;
                this.setFormError(ex);
            }

            return this.afterSave(success, id, is_new);
        }
    }
}