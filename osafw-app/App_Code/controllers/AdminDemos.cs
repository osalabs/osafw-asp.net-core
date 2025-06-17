﻿// Demo Admin controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;

namespace osafw;

public class AdminDemosController : FwAdminController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected Demos model;
    protected DemoDicts model_related;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Demos>();
        model0 = model;

        base_url = "/Admin/Demos";
        required_fields = "iname";
        save_fields = "parent_id demo_dicts_id iname idesc email fint ffloat fcombo fradio fyesno fdate_pop fdatetime att_id status";
        save_fields_checkboxes = "is_checkbox|0";

        search_fields = "iname idesc";
        list_sortdef = "iname asc";
        list_sortmap = Utils.qh("id|id iname|iname add_time|add_time demo_dicts_id|demo_dicts_id email|email status|status");

        related_field_name = "demo_dicts_id";
        model_related = fw.model<DemoDicts>();

        is_userlists = true; //enable work with user lists
        is_activity_logs = true;  //enable work with activity_logs (comments, history)
    }

    public override void getListRows()
    {
        base.getListRows();

        // add/modify rows from db if necessary
        foreach (Hashtable row in this.list_rows)
            row["demo_dicts"] = model_related.one(row["demo_dicts_id"].toInt()).toHashtable();
    }

    public override Hashtable ShowAction(int id)
    {
        Hashtable ps = base.ShowAction(id);
        Hashtable item = (DBRow)ps["i"];
        //var id = Utils.f2int(item["id"]);

        ps["parent"] = model.one(item["parent_id"].toInt());
        ps["demo_dicts"] = model_related.one(item["demo_dicts_id"].toInt());
        ps["dict_link_auto"] = model_related.one(item["dict_link_auto_id"].toInt());
        ps["multi_datarow"] = model_related.listWithChecked((string)item["dict_link_multi"]);
        ps["multi_datarow_link"] = fw.model<DemosDemoDicts>().listLinkedByMainId(id);
        ps["att"] = fw.model<Att>().one(item["att_id"].toInt());
        ps["att_links"] = fw.model<Att>().listLinked(model.table_name, id);

        if (is_activity_logs)
        {
            initFilter();

            list_filter["tab_activity"] = list_filter["tab_activity"].toStr(FwActivityLogs.TAB_COMMENTS);
            ps["list_filter"] = list_filter;
            ps["activity_entity"] = model0.table_name;
            ps["activity_rows"] = fw.model<FwActivityLogs>().listByEntityForUI(model.table_name, id, (string)list_filter["tab_activity"]);
        }

        return ps;
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        // form_new_defaults = new() { { "iname", "New Item" } }; //set new form defaults if any
        Hashtable ps = base.ShowFormAction(id);

        // read dropdowns lists from db
        var item = (Hashtable)ps["i"];
        ps["select_options_parent_id"] = model.listSelectOptionsParent();
        ps["select_options_demo_dicts_id"] = model_related.listSelectOptions();
        ps["dict_link_auto_id_iname"] = model_related.iname(item["dict_link_auto_id"]);
        ps["multi_datarow"] = model_related.listWithChecked(item["dict_link_multi"].toStr());
        ps["multi_datarow_link"] = fw.model<DemosDemoDicts>().listLinkedByMainId(id);
        FormUtils.comboForDate((string)item["fdate_combo"], ps, "fdate_combo");

        ps["att"] = fw.model<Att>().one(item["att_id"]).toHashtable();
        ps["att_links"] = fw.model<Att>().listLinked(model.table_name, id);

        return ps;
    }

    public override Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in save_fields");

        if (reqb("refresh"))
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, [id]);
            return null;
        }

        Hashtable item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // Dim item_old As Hashtable = model.one(id)

        Hashtable itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());
        itemdb["dict_link_auto_id"] = model_related.findOrAddByIname((string)item["dict_link_auto_id_iname"], out _);
        itemdb["dict_link_multi"] = FormUtils.multi2ids(reqh("dict_link_multi"));
        itemdb["fdate_combo"] = FormUtils.dateForCombo(item, "fdate_combo");
        itemdb["ftime"] = FormUtils.timeStrToInt((string)item["ftime_str"]); // ftime - convert from HH:MM to int (0-24h in seconds)
        itemdb["fint"] = itemdb["fint"].toInt(); // field accepts only int

        id = this.modelAddOrUpdate(id, itemdb);

        fw.model<DemosDemoDicts>().updateJunctionByMainId(id, reqh("demo_dicts_link"));
        fw.model<AttLinks>().updateJunction(model.table_name, id, reqh("att"));

        return this.afterSave(success, id, is_new);
    }

    public override void Validate(int id, Hashtable item)
    {
        bool result = this.validateRequired(id, item, this.required_fields);

        if (result && model.isExists(item["email"], id))
            fw.FormErrors["email"] = "EXISTS";
        if (result && !FormUtils.isEmail((string)item["email"]))
            fw.FormErrors["email"] = "EMAIL";

        //if (result && !SomeOtherValidation())
        //{
        //    fw.FERR["other field name"] = "HINT_ERR_CODE";
        //}

        this.validateCheckResult();
    }

    public Hashtable AutocompleteAction()
    {
        List<string> items = model_related.listAutocomplete(reqs("q"));

        return new Hashtable() { { "_json", items } };
    }
}