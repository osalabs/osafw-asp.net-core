// LookupManager Tables Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;
using System.Collections.Generic;

namespace osafw;

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
        save_fields = "tname iname idesc igroup header_text footer_text column_id columns column_names column_types column_groups groups url access_level status";
        save_fields_checkboxes = "is_one_form is_custom_form";

        search_fields = "tname iname";
        list_sortdef = "iname asc";
        list_sortmap = Utils.qh("id|id iname|iname tname|tname gname|igroup,tname acl|access_level");
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        Hashtable ps = new();
        Hashtable item;

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

    public override int modelAddOrUpdate(int id, Hashtable fields)
    {
        // convert from newline to comma str
        fields["list_columns"] = Utils.nlstr2commastr((string)fields["list_columns"]);
        fields["columns"] = Utils.nlstr2commastr((string)fields["columns"]);
        fields["column_names"] = Utils.nlstr2commastr((string)fields["column_names"]);
        fields["column_types"] = Utils.nlstr2commastr((string)fields["column_types"]);
        fields["column_groups"] = Utils.nlstr2commastr((string)fields["column_groups"]);

        return base.modelAddOrUpdate(id, fields);
    }

    public Hashtable ACGroupsAction()
    {
        List<string> items = model.getAutocompleteGroupsList(reqs("q"));

        return new Hashtable() { { "_json", items } };
    }

}