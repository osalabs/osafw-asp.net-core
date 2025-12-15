// Demo Admin controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class AdminDemosController : FwAdminController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected Demos model = null!;
    protected DemoDicts model_related = null!;

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
        foreach (FwDict row in this.list_rows)
            row["demo_dicts"] = model_related.one(row["demo_dicts_id"].toInt()).toHashtable();
    }

    public override FwDict? ShowAction(int id)
    {
        FwDict ps = base.ShowAction(id) ?? [];
        FwDict item = ps["i"] as FwDict ?? [];
        //var id = Utils.f2int(item["id"]);

        ps["parent"] = model.one(item["parent_id"].toInt());
        ps["demo_dicts"] = model_related.one(item["demo_dicts_id"].toInt());
        ps["dict_link_auto"] = model_related.one(item["dict_link_auto_id"].toInt());
        ps["multi_datarow"] = model_related.listWithChecked(item["dict_link_multi"].toStr());
        ps["multi_datarow_link"] = fw.model<DemosDemoDicts>().listLinkedByMainId(id);
        FormUtils.comboForDate(item["fdate_combo"].toStr(), ps, "fdate_combo");

        ps["att"] = fw.model<Att>().one(item["att_id"].toInt());
        ps["att_links"] = fw.model<Att>().listLinked(model.table_name, id);
        ps["att_files"] = fw.model<Att>().listByEntityCategory(model0.table_name, id, AttCategories.CAT_GENERAL);

        if (is_activity_logs)
        {
            initFilter();

            list_filter["tab_activity"] = list_filter["tab_activity"].toStr(FwActivityLogs.TAB_COMMENTS);
            ps["list_filter"] = list_filter;
            ps["activity_entity"] = model0.table_name;
            ps["activity_rows"] = fw.model<FwActivityLogs>().listByEntityForUI(model.table_name, id, list_filter["tab_activity"].toStr());
        }

        return ps;
    }

    public override FwDict? ShowFormAction(int id = 0)
    {
        // form_new_defaults = new() { { "iname", "New Item" } }; //set new form defaults if any
        FwDict ps = base.ShowFormAction(id) ?? [];

        // read dropdowns lists from db
        var item = ps["i"] as FwDict ?? [];
        ps["select_options_parent_id"] = model.listSelectOptionsParent();
        ps["select_options_demo_dicts_id"] = model_related.listSelectOptions();
        ps["dict_link_auto_id_iname"] = model_related.iname(item["dict_link_auto_id"]);
        ps["multi_datarow"] = model_related.listWithChecked(item["dict_link_multi"].toStr());
        ps["multi_datarow_link"] = fw.model<DemosDemoDicts>().listLinkedByMainId(id);
        FormUtils.comboForDate(item["fdate_combo"].toStr(), ps, "fdate_combo");

        ps["att"] = fw.model<Att>().one(item["att_id"]).toHashtable();
        ps["att_links"] = fw.model<Att>().listLinked(model.table_name, id);

        // Files upload sample like in DemosDynamic (att_files_edit)
        // provide variables required by /common/form/showform/att_files.html
        ps["att_upload_url"] = this.base_url + "/(SaveAttFiles)/" + id;
        ps["att_category"] = AttCategories.CAT_GENERAL;   // sample category
        ps["att_post_prefix"] = "att_files1"; // hidden fields prefix in the form
        ps["att_files"] = fw.model<Att>().listByEntityCategory(model.table_name, id, ps["att_category"].toStr());

        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in save_fields");

        if (reqb("refresh"))
        {
            logger("refresh element:", reqs("refresh")); // id or name of the element refreshed OR "1" if no element id/name
            fw.routeRedirect(FW.ACTION_SHOW_FORM, [id]);
            return null;
        }

        FwDict item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // var itemOld = model.one(id);

        FwDict itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());
        itemdb["dict_link_auto_id"] = model_related.findOrAddByIname(item["dict_link_auto_id_iname"].toStr(), out _);
        itemdb["dict_link_multi"] = FormUtils.multi2ids(reqh("dict_link_multi"));
        itemdb["fdate_combo"] = FormUtils.dateForCombo(item, "fdate_combo");
        itemdb["ftime"] = FormUtils.timeStrToInt(item["ftime_str"].toStr()); // ftime - convert from HH:MM to int (0-24h in seconds)
        itemdb["fint"] = itemdb["fint"].toInt(); // field accepts only int

        id = this.modelAddOrUpdate(id, itemdb);

        fw.model<DemosDemoDicts>().updateJunctionByMainId(id, reqh("demo_dicts_link"));
        fw.model<AttLinks>().updateJunction(model.table_name, id, reqh("att"));

        processSaveAttFiles(id, item, new FwDict()
        {
            { "field", "att_files1" },
            { "att_post_prefix", "att_files1" },
            { "att_category", AttCategories.CAT_GENERAL }
        });

        return this.afterSave(success, id, is_new);
    }

    public override void Validate(int id, FwDict item)
    {
        bool result = this.validateRequired(id, item, this.required_fields);

        if (result && model.isExists(item["email"].toStr(), id))
            fw.FormErrors["email"] = "EXISTS";
        if (result && !FormUtils.isEmail(item["email"].toStr()))
            fw.FormErrors["email"] = "EMAIL";

        //if (result && !SomeOtherValidation())
        //{
        //    fw.FERR["other field name"] = "HINT_ERR_CODE";
        //}

        this.validateCheckResult();
    }

    public FwDict AutocompleteAction()
    {
        List<string> items = model_related.listAutocomplete(reqs("q"));

        return new FwDict() { { "_json", items } };
    }

    // upload one or many files to the Att storage and link to the current entity and id
    // json only response
    public FwDict SaveAttFilesAction(int id)
    {
        var item = reqh("item");

        // validation
        if (id == 0)
            throw new UserException("Invalid ID");
        var files = fw.request!.Form?.Files;
        var firstFile = files == null || files.Count == 0 ? null : files[0];
        if (firstFile == null || firstFile.Length == 0)
            throw new UserException("No file(s) selected");

        var modelAtt = fw.model<Att>();
        var att_cat = fw.model<AttCategories>().oneByIcode(item["att_category"].toStr());
        var ent = fw.model<FwEntities>().oneByIcode(model0.table_name);
        var itemdb = new FwDict()
        {
            { "item_id", id },
            { "att_categories_id", att_cat.Count > 0 ? att_cat["id"].toInt() : null },
            { "fwentities_id", ent.Count > 0 ? ent["id"].toInt() : null },
            { "status", FwModel.STATUS_ACTIVE }
        };

        var att_id = 0;
        var addedAtt = modelAtt.uploadMulti(itemdb);
        if (addedAtt.Count > 0)
            att_id = (addedAtt[0] as FwDict)!["id"].toInt();

        // make same response as in AdminAtt.SaveAction
        // if select in popup - return json
        var ps = new FwDict();
        var _json = new FwDict();
        _json["id"] = att_id;
        if (att_id > 0)
        {
            var item_new = modelAtt.one(att_id);
            _json["icode"] = item_new["icode"];
            _json["url"] = modelAtt.getUrl(att_id);
            _json["url_preview"] = modelAtt.getUrlPreview(att_id);
            _json["iname"] = item_new["iname"];
            _json["is_image"] = item_new["is_image"];
            _json["fsize"] = item_new["fsize"];
            _json["ext"] = item_new["ext"];
        }
        else
            _json["error"] = new FwDict() { { "message", "File upload error" } };

        ps["_json"] = _json;
        return ps;
    }

    // Files upload sample handler (same as FwDynamicController for att_files_edit)
    protected virtual void processSaveAttFiles(int id, FwDict fields, FwDict def)
    {
        var field = def["field"].toStr();

        // per-field prefix support
        var att_post_prefix = def["att_post_prefix"].toStr(field);
        // if PATCH - only update is post param is present (otherwise it will delete all records)
        if (isPatch() && req(att_post_prefix) == null)
            return;

        var att_ids = reqh(att_post_prefix);
        var att_category = def["att_category"].toStr();
        var att_model = fw.model<Att>();

        // delete any files in this category not present in the posted list
        var existing = att_model.listByEntityCategory(model0.table_name, id, att_category);
        foreach (FwDict row in existing)
        {
            if (!att_ids.ContainsKey(row["id"].toStr()))
                att_model.delete(row["id"].toInt(), true);
        }
    }
}