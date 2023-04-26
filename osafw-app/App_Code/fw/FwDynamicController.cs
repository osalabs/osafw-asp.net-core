// Fw Dynamic controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2018 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualBasic;

namespace osafw;

public class FwDynamicController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected FwModel model_related;

    public override void init(FW fw)
    {
        base.init(fw);
    }

    public virtual Hashtable IndexAction()
    {
        // get filters from the search form
        this.initFilter();

        this.setListSorting();

        this.setListSearch();
        this.setListSearchStatus(); // status field is not always in table, so keep it separate
                                    // set here non-standard search
                                    // If f("field") > "" Then
                                    // Me.list_where &= " and field=" & db.q(f("field"))
                                    // End If

        this.getListRows();
        // add/modify rows from db if necessary
        // For Each row As Hashtable In Me.list_rows
        // row["field") ] "value"
        // Next

        // set standard output parse strings
        var ps = this.setPS();

        // userlists support if necessary
        if (this.is_userlists)
            this.setUserLists(ps);

        ps["select_userfilters"] = fw.model<UserFilters>().listSelectByIcode((string)fw.G["controller.action"]);

        if (is_dynamic_index)
            // customizable headers
            setViewList(ps, list_filter_search);

        if (reqs("export").Length > 0)
        {
            exportList();
            return null;
        }
        else
            return ps;
    }

    //Prev/Next navigation
    public void NextAction(string form_id)
    {
        var id = Utils.f2int(form_id);
        if (id == 0)
            fw.redirect(base_url);

        var is_prev = (reqi("prev") == 1);
        var is_edit = (reqi("edit") == 1);

        this.initFilter("_filter_" + fw.G["controller"] + ".Index"); //read list filter for the IndexAction

        if (list_sortmap.Count == 0)
            list_sortmap = getViewListSortmap();
        this.setListSorting();

        this.setListSearch();
        this.setListSearchStatus();

        // get all ids
        var sql = "SELECT id FROM " + list_view + " WHERE " + this.list_where + " ORDER BY " + this.list_orderby;
        var ids = db.colp(sql, list_where_params);
        if (ids.Count == 0)
            fw.redirect(base_url);

        var go_id = 0;
        if (is_prev)
        {
            var index_prev = -1;
            for (var index = ids.Count - 1; index >= 0; index += -1)
            {
                if (ids[index] == id.ToString())
                {
                    index_prev = index - 1;
                    break;
                }
            }
            if (index_prev > -1 && index_prev <= ids.Count - 1)
                go_id = Utils.f2int(ids[index_prev]);
            else if (ids.Count > 0)
                go_id = Utils.f2int(ids[ids.Count - 1]);
            else
                fw.redirect(base_url);
        }
        else
        {
            var index_next = -1;
            for (var index = 0; index <= ids.Count - 1; index++)
            {
                if (ids[index] == id.ToString())
                {
                    index_next = index + 1;
                    break;
                }
            }
            if (index_next > -1 && index_next <= ids.Count - 1)
                go_id = Utils.f2int(ids[index_next]);
            else if (ids.Count > 0)
                go_id = Utils.f2int(ids[0]);
            else
                fw.redirect(base_url);
        }

        var url = base_url + "/" + go_id;
        if (is_edit)
            url += "/edit";
        if (related_id.Length > 0 || return_url.Length > 0)
            url += "/?";
        if (related_id.Length > 0)
            url += "related_id=" + Utils.urlescape(related_id);
        if (return_url.Length > 0)
            url += "&return_url=" + Utils.urlescape(return_url);

        fw.redirect(url);
    }

    public virtual Hashtable ShowAction(int id = 0)
    {
        Hashtable ps = new();
        Hashtable item = model0.one(id);
        if (item.Count == 0)
            throw new NotFoundException();

        // added/updated should be filled before dynamic fields
        setAddUpdUser(ps, item);

        // dynamic fields
        if (is_dynamic_show)
            ps["fields"] = prepareShowFields(item, ps);

        // userlists support if necessary
        if (this.is_userlists)
            this.setUserLists(ps, id);

        ps["id"] = id;
        ps["i"] = item;
        ps["return_url"] = return_url;
        ps["related_id"] = related_id;
        ps["base_url"] = base_url;
        ps["is_userlists"] = is_userlists;
        ps["is_readonly"] = is_readonly;

        return ps;
    }

    public virtual Hashtable ShowFormAction(int id = 0)
    {
        // define form_new_defaults via config.json
        // Me.form_new_defaults = New Hashtable From {{"field", "default value"}} 'OR set new form defaults here

        Hashtable ps = new();
        var item = reqh("item"); // set defaults from request params

        if (isGet())
        {
            if (id > 0)
            {
                item = model0.one(id);
            }
            else
            {
                // override any defaults here
                Utils.mergeHash(item, this.form_new_defaults);
            }
        }
        else
        {
            // read from db
            Hashtable itemdb = model0.one(id);
            // and merge new values from the form
            Utils.mergeHash(itemdb, item);
            item = itemdb;
        }

        setAddUpdUser(ps, item);

        if (is_dynamic_showform)
            ps["fields"] = prepareShowFormFields(item, ps);
        // TODO
        // ps["select_options_parent_id") ] model.listSelectOptionsParent()
        // FormUtils.comboForDate(item["fdate_combo"], ps, "fdate_combo")

        ps["id"] = id;
        ps["i"] = item;
        ps["return_url"] = return_url;
        ps["related_id"] = related_id;
        ps["is_readonly"] = is_readonly;
        if (fw.FormErrors.Count > 0)
            logger(fw.FormErrors);

        return ps;
    }

    public override int modelAddOrUpdate(int id, Hashtable fields)
    {
        if (is_dynamic_showform)
            processSaveShowFormFields(id, fields);

        id = base.modelAddOrUpdate(id, fields);

        if (is_dynamic_showform)
            processSaveShowFormFieldsAfter(id, fields);

        return id;
    }

    public virtual Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");

        fw.model<Users>().checkReadOnly();
        if (reqi("refresh") == 1)
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, new object[] { id });
            return null;
        }

        Hashtable item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // Dim item_old As Hashtable = model0.one(id)

        Hashtable itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes);
        FormUtils.filterNullable(itemdb, save_fields_nullable);

        id = this.modelAddOrUpdate(id, itemdb);

        return this.afterSave(success, id, is_new);
    }

    /// <summary>
    /// Performs submitted form validation for required field and simple validations: exits, isemail, isphone, isdate, isfloat.
    /// If more complex validation required - just override this and call just necessary validation
    /// </summary>
    /// <param name="id"></param>
    /// <param name="item"></param>
    public virtual void Validate(int id, Hashtable item)
    {
        bool result = validateRequiredDynamic(item);

        if (result && is_dynamic_showform)
            validateSimpleDynamic(id, item);

        // If result AndAlso Not SomeOtherValidation() Then
        // FW.FERR("other field name") = "HINT_ERR_CODE"
        // End If

        this.validateCheckResult();
    }

    protected virtual bool validateRequiredDynamic(Hashtable item)
    {
        var result = true;
        if (string.IsNullOrEmpty(this.required_fields) && is_dynamic_showform)
        {
            // if required_fields not defined - fill from showform_fields
            ArrayList fields = (ArrayList)this.config["showform_fields"];
            ArrayList req = new();
            foreach (Hashtable def in fields)
            {
                if (Utils.f2bool(def["required"]))
                    req.Add(def["field"]);
            }

            if (req.Count > 0)
                result = this.validateRequired(item, req.ToArray());
        }
        else
            result = this.validateRequired(item, this.required_fields);
        return result;
    }

    // simple validation via showform_fields
    protected virtual bool validateSimpleDynamic(int id, Hashtable item)
    {
        bool result = true;
        ArrayList fields = (ArrayList)this.config["showform_fields"];
        foreach (Hashtable def in fields)
        {
            string field = (string)def["field"];
            if (string.IsNullOrEmpty(field))
                continue;

            string field_value = (string)item[field];

            var val = Utils.qh((string)def["validate"]);
            if (val.ContainsKey("exists") && model0.isExistsByField(field_value, id, field))
            {
                fw.FormErrors[field] = "EXISTS";
                result = false;
            }
            if (val.ContainsKey("isemail") && !FormUtils.isEmail(field_value))
            {
                fw.FormErrors[field] = "WRONG";
                result = false;
            }
            if (val.ContainsKey("isphone") && !FormUtils.isPhone(field_value))
            {
                fw.FormErrors[field] = "WRONG";
                result = false;
            }
            if (val.ContainsKey("isdate") && !Utils.isDate(field_value))
            {
                fw.FormErrors[field] = "WRONG";
                result = false;
            }
            if (val.ContainsKey("isfloat") && !Utils.isFloat(field_value))
            {
                fw.FormErrors[field] = "WRONG";
                result = false;
            }
        }
        return result;
    }

    public virtual void ShowDeleteAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        var ps = new Hashtable()
        {
            {"i", model0.one(id)},
            {"related_id", this.related_id},
            {"return_url", this.return_url},
            {"base_url", this.base_url},
        };

        fw.parser("/common/form/showdelete", ps);
    }

    public virtual Hashtable DeleteAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        model0.deleteWithPermanentCheck(id);

        fw.flash("onedelete", 1);
        return this.afterSave(true);
    }

    public virtual Hashtable RestoreDeletedAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        model0.update(id, new Hashtable() { { model0.field_status, Utils.f2str(FwModel.STATUS_ACTIVE) } });

        fw.flash("record_updated", 1);
        return this.afterSave(true, id);
    }

    public virtual Hashtable SaveMultiAction()
    {
        route_onerror = FW.ACTION_INDEX;

        Hashtable cbses = reqh("cb");
        bool is_delete = fw.FORM.ContainsKey("delete");
        if (is_delete)
            fw.model<Users>().checkReadOnly();

        int user_lists_id = reqi("addtolist");
        var remove_user_lists_id = reqi("removefromlist");
        int ctr = 0;

        if (user_lists_id > 0)
        {
            var user_lists = fw.model<UserLists>().one(user_lists_id);
            if (user_lists.Count == 0 || Utils.f2int(user_lists["add_users_id"]) != fw.userId)
                throw new UserException("Wrong Request");
        }

        foreach (string id1 in cbses.Keys)
        {
            int id = Utils.f2int(id1);
            if (is_delete)
            {
                model0.deleteWithPermanentCheck(id);
                ctr += 1;
            }
            else if (user_lists_id > 0)
            {
                fw.model<UserLists>().addItemList(user_lists_id, id);
                ctr += 1;
            }
            else if (remove_user_lists_id > 0)
            {
                fw.model<UserLists>().delItemList(remove_user_lists_id, id);
                ctr += 1;
            }
        }

        if (is_delete)
            fw.flash("multidelete", ctr);
        if (user_lists_id > 0)
            fw.flash("success", ctr + " records added to the list");

        return this.afterSave(true, new Hashtable() { { "ctr", ctr } });
    }


    // ********************* support for autocomlete related items
    public virtual Hashtable AutocompleteAction()
    {
        if (model_related == null)
            throw new ApplicationException("No model_related defined");
        List<string> items = model_related.getAutocompleteList(reqs("q"));

        return new Hashtable() { { "_json", items } };
    }

    // ********************* support for customizable list screen
    public virtual void UserViewsAction(int id = 0)
    {
        Hashtable ps = new();

        var rows = getViewListArr(getViewListUserFields(), true); // list all fields
        ps["rows"] = rows;

        ps["select_userviews"] = fw.model<UserViews>().listSelectByIcode(base_url);
        fw.parser("/common/list/userviews", ps);
    }

    public virtual void SaveUserViewsAction()
    {
        var fld = reqh("fld");
        var load_id = reqi("load_id");
        var is_reset = reqi("is_reset");

        if (load_id > 0)
        {
            fw.model<UserViews>().setViewForIcode(base_url, load_id);
        }
        else if (is_reset == 1)
            fw.model<UserViews>().updateByIcode(base_url, view_list_defaults);
        else
        {
            var item = reqh("item");
            var iname = Utils.f2str(item["iname"]);

            // save fields
            // order by value
            var ordered = fld.Cast<DictionaryEntry>().OrderBy(entry => Utils.f2int(entry.Value)).ToList();
            // and then get ordered keys
            List<string> anames = new();
            foreach (var el in ordered)
                anames.Add((string)el.Key);
            var fields = Strings.Join(anames.ToArray(), " ");

            if (!string.IsNullOrEmpty(iname))
            {
                // create new view by name or update if this name exists
                fw.model<UserViews>().addOrUpdateByUK(base_url, fields, iname);
            }
            // update default view with fields
            fw.model<UserViews>().updateByIcode(base_url, fields);
        }

        fw.redirect(return_url);
    }

    //********************* support for sortable rows
    public Hashtable SaveSortAction()
    {
        var ps = new Hashtable() { { "success", true } };

        var sortdir = reqs("sortdir");
        var id = reqi("id");
        var under_id = reqi("under");
        var above_id = reqi("above");

        ps["success"] = model0.reorderPrio(sortdir, id, under_id, above_id);

        return new Hashtable() { { "_json", ps } };
    }

    // ''''' HELPERS for dynamic fields

    /// <summary>
    /// prepare data for fields repeat in ShowAction based on config.json show_fields parameter
    /// </summary>
    /// <param name="item"></param>
    /// <param name="ps"></param>
    /// <returns></returns>
    public virtual ArrayList prepareShowFields(Hashtable item, Hashtable ps)
    {
        var id = Utils.f2int(item["id"]);

        ArrayList fields = (ArrayList)this.config["show_fields"];
        foreach (Hashtable def in fields)
        {
            def["i"] = item; // ref to item
            string dtype = (string)def["type"];
            string field = Utils.f2str(def["field"]);

            if (dtype is "row" or "row_end" or "col" or "col_end")
                // structural tags
                def["is_structure"] = true;
            else if (dtype == "multi")
            {
                // complex field
                if (!string.IsNullOrEmpty((string)def["table_link"]))
                    // def["multi_datarow") ] fw.model(def["lookup_model")).]etMultiListAL(model0.getLinkedIds(def["table_link"), ]d, def["table_link_id_name"), ]ef["table_link_linked_id_name")), ]ef)
                    def["multi_datarow"] = fw.model((string)def["lookup_model"]).getMultiListAL(model0.getLinkedIdsByDef(id, def), def);
                else
                    def["multi_datarow"] = fw.model((string)def["lookup_model"]).getMultiList((string)item[field], def);
            }
            else if (dtype == "multi_prio")
                // complex field with prio
                def["multi_datarow"] = fw.model((string)def["lookup_model"]).getMultiListLinkedRows(id, def);
            else if (dtype == "att")
                def["att"] = fw.model<Att>().one(Utils.f2int((string)item[field]));
            else if (dtype == "att_links")
                def["att_links"] = fw.model<Att>().getAllLinked(model0.table_name, Utils.f2int(id));
            else if (dtype == "subtable")
            {
                // subtable functionality
                var model_name = (string)def["model"];
                var sub_model = fw.model(model_name);
                var list_rows = sub_model.listByRelatedId(id, def); //list related rows from db                        
                sub_model.prepareSubtable(list_rows, id, def);

                def["list_rows"] = list_rows;
            }
            else
            {
                // single values
                // lookups
                if (def.ContainsKey("lookup_table"))
                {
                    string lookup_key = Utils.f2str(def["lookup_key"]);
                    if (lookup_key == "")
                        lookup_key = "id";

                    string lookup_field = Utils.f2str(def["lookup_field"]);
                    if (lookup_field == "")
                        lookup_field = "iname";

                    var lookup_row = db.row((string)def["lookup_table"], DB.h(lookup_key, item[field]));
                    def["lookup_row"] = lookup_row;
                    def["value"] = lookup_row[lookup_field];
                }
                else if (def.ContainsKey("lookup_model"))
                {
                    var lookup_model = fw.model((string)def["lookup_model"]);
                    def["lookup_id"] = Utils.f2int(item[field]);
                    var lookup_row = lookup_model.one(Utils.f2int(def["lookup_id"]));
                    def["lookup_row"] = lookup_row;

                    string lookup_field = Utils.f2str(def["lookup_field"]);
                    if (lookup_field == "")
                        lookup_field = lookup_model.field_iname;

                    def["value"] = lookup_row[lookup_field];
                    if (!def.ContainsKey("admin_url"))
                        def["admin_url"] = "/Admin/" + def["lookup_model"]; // default admin url from model name
                }
                else if (def.ContainsKey("lookup_tpl"))
                    def["value"] = FormUtils.selectTplName((string)def["lookup_tpl"], (string)item[field]);
                else
                    def["value"] = item[field];

                // convertors
                if (def.ContainsKey("conv"))
                {
                    if ((string)def["conv"] == "time_from_seconds")
                        def["value"] = FormUtils.intToTimeStr(Utils.f2int(def["value"]));
                }
            }
        }
        return fields;
    }

    public virtual ArrayList prepareShowFormFields(Hashtable item, Hashtable ps)
    {
        var id = Utils.f2int(item["id"]);

        var subtable_add = reqh("subtable_add");
        //var subtable_del = reqh("subtable_del");

        var fields = (ArrayList)this.config["showform_fields"];
        if (fields == null)
            throw new ApplicationException("Controller config.json doesn't contain 'showform_fields'");
        foreach (Hashtable def in fields)
        {
            // logger(def)
            def["i"] = item; // ref to item
            def["ps"] = ps; // ref to whole ps
            string dtype = (string)def["type"]; // type is required
            string field = Utils.f2str(def["field"]);

            if (id == 0 && (dtype == "added" || dtype == "updated"))
                // special case - hide if new item screen
                def["class"] = "d-none";

            if (dtype == "row" || dtype == "row_end" || dtype == "col" || dtype == "col_end")
                // structural tags
                def["is_structure"] = true;
            else if (dtype == "multicb")
            {
                // complex field
                if (!string.IsNullOrEmpty((string)def["table_link"]))
                    // model0.getLinkedIds(def["table_link"), ]d, def["table_link_id_name"), ]ef["table_link_linked_id_name"))]
                    def["multi_datarow"] = fw.model((string)def["lookup_model"]).getMultiListAL(model0.getLinkedIdsByDef(id, def), def);
                else
                    def["multi_datarow"] = fw.model((string)def["lookup_model"]).getMultiList((string)item[field], def);

                foreach (Hashtable row in (ArrayList)def["multi_datarow"]) // contains id, iname, is_checked
                    row["field"] = def["field"];
            }
            else if (dtype == "multicb_prio")
            {
                def["multi_datarow"] = fw.model((string)def["lookup_model"]).getMultiListLinkedRows(id, def);

                foreach (Hashtable row in (ArrayList)def["multi_datarow"]) // contains id, iname, is_checked, _link[prio]
                    row["field"] = def["field"];
            }
            else if (dtype == "att_edit")
            {
                def["att"] = fw.model<Att>().one(Utils.f2int(item[field]));
                def["value"] = item[field];
            }
            else if (dtype == "att_links_edit")
                def["att_links"] = fw.model<Att>().getAllLinked(model0.table_name, Utils.f2int(id));

            else if (dtype == "subtable_edit")
            {
                // subtable functionality
                var model_name = (string)def["model"];
                var sub_model = fw.model(model_name);
                var list_rows = new ArrayList();

                if (isGet())
                {
                    if (id > 0)
                    {
                        list_rows = sub_model.listByRelatedId(id, def); //list related rows from db                        
                    }
                    else
                        sub_model.prepareSubtableAddNew(list_rows, id, def); //add at least one row
                }
                else
                {
                    //copy list related rows from the form
                    // row ids submitted as: item$<~model>[<~id>]
                    // input name format: item$<~model>#<~id>[field_name]
                    var hids = reqh("item$" + model_name);
                    // sort hids.Keys, so numerical keys - first and keys staring with "new-" will be last
                    var sorted_keys = hids.Keys.Cast<string>().OrderBy(x => x.StartsWith("new-") ? 1 : 0).ThenBy(x => x).ToList();
                    foreach (string row_id in sorted_keys)
                    {
                        var row_item = reqh("item$" + model_name + "#" + row_id);
                        row_item["id"] = row_id;

                        list_rows.Add(row_item);
                    }
                }

                //delete row clicked
                //if (subtable_del.ContainsKey(model_name))
                //{
                //    var del_id = (string)subtable_del[model_name];
                //    // delete with LINQ from the form list (actual delete from db will be on save)
                //    list_rows = new ArrayList((from Hashtable d in list_rows
                //                               where (string)d["id"] != del_id
                //                               select d).ToList());
                //}

                //add new clicked
                if (subtable_add.ContainsKey(model_name))
                    sub_model.prepareSubtableAddNew(list_rows, id, def);

                //prepare rows for display (add selects, etc..)
                sub_model.prepareSubtable(list_rows, id, def);

                def["list_rows"] = list_rows;
            }
            else
            {
                // single values
                // lookups
                if (def.ContainsKey("lookup_table"))
                {
                    string lookup_key = Utils.f2str(def["lookup_key"]);
                    if (lookup_key == "")
                        lookup_key = "id";

                    string lookup_field = Utils.f2str(def["lookup_field"]);
                    if (lookup_field == "")
                        lookup_field = "iname";

                    var lookup_row = db.row((string)def["lookup_table"], DB.h(lookup_key, item[field]));
                    def["lookup_row"] = lookup_row;
                    def["value"] = lookup_row[lookup_field];
                }
                else if (def.ContainsKey("lookup_model"))
                {
                    if (dtype == "select" || dtype == "radio")
                    {
                        // lookup select
                        def["select_options"] = fw.model((string)def["lookup_model"]).listSelectOptions(def);
                        def["value"] = item[field];
                    }
                    else
                    {
                        // single value from lookup
                        var lookup_model = fw.model((string)def["lookup_model"]);
                        def["lookup_id"] = Utils.f2int(item[field]);
                        var lookup_row = lookup_model.one(Utils.f2int(def["lookup_id"]));
                        def["lookup_row"] = lookup_row;

                        string lookup_field = Utils.f2str(def["lookup_field"]);
                        if (lookup_field == "")
                            lookup_field = lookup_model.field_iname;

                        def["value"] = lookup_row[lookup_field];
                        if (!def.ContainsKey("admin_url"))
                            def["admin_url"] = "/Admin/" + def["lookup_model"]; // default admin url from model name
                    }
                }
                else if (def.ContainsKey("lookup_tpl"))
                {
                    def["select_options"] = FormUtils.selectTplOptions((string)def["lookup_tpl"]);
                    def["value"] = item[field];
                    foreach (Hashtable row in (ArrayList)def["select_options"]) // contains id, iname
                    {
                        row["is_inline"] = def["is_inline"];
                        row["field"] = def["field"];
                        row["value"] = item[field];
                    }
                }
                else
                    def["value"] = item[field];

                // convertors
                if (def.ContainsKey("conv"))
                {
                    if ((string)def["conv"] == "time_from_seconds")
                        def["value"] = FormUtils.intToTimeStr(Utils.f2int(def["value"]));
                }
            }
        }
        return fields;
    }

    // auto-process fields BEFORE record saved to db
    protected virtual void processSaveShowFormFields(int id, Hashtable fields)
    {
        Hashtable item = reqh("item");

        var showform_fields = _fieldsToHash((ArrayList)this.config["showform_fields"]);

        var fnullable = Utils.qh(save_fields_nullable);

        // special auto-processing for fields of particular types
        foreach (string field in fields.Keys.Cast<string>().ToArray())
        {
            if (showform_fields.ContainsKey(field))
            {
                var def = (Hashtable)showform_fields[field];
                string type = (string)def["type"];
                if (type == "multicb")
                    // multiple checkboxes
                    fields[field] = FormUtils.multi2ids(reqh(field + "_multi"));
                else if (type == "autocomplete")
                    fields[field] = Utils.f2str(fw.model((string)def["lookup_model"]).findOrAddByIname((string)fields[field], out _));
                else if (type == "date_combo")
                    fields[field] = FormUtils.dateForCombo(item, field).ToString();
                else if (type == "time")
                    fields[field] = Utils.f2str(FormUtils.timeStrToInt((string)fields[field])); // ftime - convert from HH:MM to int (0-24h in seconds)
                else if (type == "number")
                {
                    if (fnullable.ContainsKey(field) && string.IsNullOrEmpty((string)fields[field]))
                        // if field nullable and empty - pass NULL
                        fields[field] = null;
                    else
                        fields[field] = Utils.f2str(Utils.f2float(fields[field]));// number - convert to number (if field empty or non-number - it will become 0)
                }
            }
        }
    }

    // auto-process fields AFTER record saved to db
    protected virtual void processSaveShowFormFieldsAfter(int id, Hashtable fields)
    {
        var subtable_del = reqh("subtable_del");

        // for now we just look if we have att_links_edit field and update att links
        foreach (Hashtable def in (ArrayList)this.config["showform_fields"])
        {
            string type = (string)def["type"];
            if (type == "att_links_edit")
                fw.model<Att>().updateAttLinks(model0.table_name, id, reqh("att")); // TODO make att configurable
            else if (type == "multicb")
            {
                if (def.ContainsKey("table_link") && !string.IsNullOrEmpty((string)def["table_link"]))
                    model0.updateLinked((string)def["table_link"], id, (string)def["table_link_id_name"], (string)def["table_link_linked_id_name"], reqh(def["field"] + "_multi"));
            }
            else if (type == "multicb_prio")
                fw.model((string)def["lookup_model"]).updateLinkedRows(id, reqh(def["field"] + "_multi"));

            else if (type == "subtable_edit")
            {
                //save subtable
                var model_name = (string)def["model"];
                var sub_model = fw.model(model_name);

                var save_fields = (string)def["save_fields"];
                var save_fields_checkboxes = (string)def["save_fields_checkboxes"];

                //check if we delete specific row
                var del_id = (string)subtable_del[model_name] ?? "";

                //mark all related records as under update (status=1)
                sub_model.setUnderUpdate(id);

                //update and add new rows

                // row ids submitted as: item$<~model>[<~id>]
                // input name format: item$<~model>#<~id>[field_name]
                var hids = reqh("item$" + model_name);
                // sort hids.Keys, so numerical keys - first and keys staring with "new-" will be last
                var sorted_keys = hids.Keys.Cast<string>().OrderBy(x => x.StartsWith("new-") ? 1 : 0).ThenBy(x => x).ToList();
                foreach (string row_id in sorted_keys)
                {
                    if (row_id == del_id) continue; //skip deleted row

                    var row_item = reqh("item$" + model_name + "#" + row_id);
                    Hashtable itemdb = FormUtils.filter(row_item, save_fields);
                    FormUtils.filterCheckboxes(itemdb, row_item, save_fields_checkboxes);

                    itemdb["status"] = FwModel.STATUS_ACTIVE;
                    if (row_id.StartsWith("new-"))
                    {
                        itemdb[sub_model.linked_field_main_id] = id;
                        sub_model.add(itemdb);
                    }
                    else
                        sub_model.update(Utils.f2int(row_id), itemdb);
                }

                //remove any not updated rows (i.e. those deleted by user)
                sub_model.deleteUnderUpdate(id);
            }
        }
    }

    // convert config's fields list into hashtable as field => {}
    // if there are more than one field - just first field added to the hash
    protected Hashtable _fieldsToHash(ArrayList fields)
    {
        Hashtable result = new();
        foreach (Hashtable fldinfo in fields)
        {
            if (fldinfo.ContainsKey("field") && !result.ContainsKey((string)fldinfo["field"]))
                result[(string)fldinfo["field"]] = fldinfo;
        }
        return result;
    }
}
