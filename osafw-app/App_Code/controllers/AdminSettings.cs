// Site Settings Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class AdminSettingsController : FwAdminController
{
    public static new int access_level = Users.ACL_ADMIN;

    protected Settings model;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Settings>();
        model0 = model;

        base_url = "/Admin/Settings";
        required_fields = "ivalue";
        save_fields = "ivalue";
        save_fields_checkboxes = "";

        search_fields = "icode iname ivalue";
        list_sortdef = "iname asc";
        list_sortmap = Utils.qh("id|id iname|iname upd_time|upd_time");
    }

    public override void setListSearch()
    {
        base.setListSearch();

        if (!Utils.isEmpty(list_filter["icat"]))
        {
            list_where += " and icat=@icat";
            list_where_params["icat"] = Utils.toStr(list_filter["icat"]);
        }
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        // set new form defaults here if any
        // Me.form_new_defaults = New Hashtable
        // item("field")="default value"
        Hashtable ps = base.ShowFormAction(id);

        Hashtable item = (Hashtable)ps["i"];
        // TODO - multi values for select, checkboxes, radio
        // ps("select_options_parent_id") = FormUtils.select_options_db(db.array("select id, iname from " & model.table_name & " where parent_id=0 and status=0 order by iname"), item("parent_id"))
        // ps("multi_datarow") = fw.model(Of DemoDicts).get_multi_list(item("dict_link_multi"))

        return ps;
    }

    public override Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;
        route_return = FW.ACTION_INDEX;

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in save_fields");

        Hashtable item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // Dim item_old As Hashtable = model.one(id)

        Hashtable itemdb = FormUtils.filter(item, this.save_fields);
        // TODO - checkboxes
        // FormUtils.form2dbhash_checkboxes(itemdb, item, save_fields_checkboxes)
        // itemdb("dict_link_multi") = FormUtils.multi2ids(reqh("dict_link_multi"))

        // only update, no add new settings
        model.update(id, itemdb);
        fw.flash("record_updated", 1);

        // custom code:
        // reset cache
        FwCache.remove("main_menu");

        return this.afterSave(success, id);
    }

    public override void Validate(int id, Hashtable item)
    {
        bool result = this.validateRequired(id, item, this.required_fields);

        if (id == 0)
            throw new UserException("Wrong Settings ID");

        this.validateCheckResult();
    }

    public override Hashtable DeleteAction(int id)
    {
        throw new UserException("Site Settings cannot be deleted");
    }
}