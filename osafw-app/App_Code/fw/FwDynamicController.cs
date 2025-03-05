// Fw Dynamic controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2018 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public class FwDynamicController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected FwModel model_related;

    public override void init(FW fw)
    {
        base.init(fw);
    }

    /// <summary>
    /// contains logic to display list screen
    /// Note! if query contains "export" - early empty result returned and FW will call exportList() after this
    /// </summary>
    /// <returns>Hashtable - related template will be parsed, null - no templates parsed (if action did all the output)</returns>
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

        // if export - no need to parse templates and prep for them - just return empty hashtable asap
        if (export_format.Length > 0)
            return []; // return empty hashtable just in case action overriden to avoid check for null

        if (is_dynamic_index)
            // customizable headers
            setViewList();

        // set standard output parse strings
        var ps = this.setPS();

        // userlists support if necessary
        if (this.is_userlists)
            this.setUserLists(ps);

        ps["select_userfilters"] = fw.model<UserFilters>().listSelectByIcode((string)fw.G["controller.action"]);

        return ps;
    }

    //Prev/Next navigation
    public virtual Hashtable NextAction(string form_id)
    {
        var id = form_id.toInt();
        if (id == 0)
            return new Hashtable { { "_redirect", base_url } };

        var is_prev = (reqi("prev") == 1);
        var is_edit = (reqi("edit") == 1);

        this.initFilter("_filter_" + fw.G["controller"] + ".Index"); //read list filter for the IndexAction

        if (list_sortmap.Count == 0)
            list_sortmap = getViewListSortmap();
        this.setListSorting();

        this.setListSearch();
        this.setListSearchStatus();

        // get all ids
        var ids = getListIds(list_view);
        if (ids.Count == 0)
            return new Hashtable { { "_redirect", base_url } };

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
                go_id = ids[index_prev].toInt();
            else if (ids.Count > 0)
                go_id = ids[ids.Count - 1].toInt();
            else
                return new Hashtable { { "_redirect", base_url } };
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
                go_id = ids[index_next].toInt();
            else if (ids.Count > 0)
                go_id = ids[0].toInt();
            else
                return new Hashtable { { "_redirect", base_url } };
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

        return new Hashtable { { "_redirect", url }, { "id", go_id } };
    }

    public virtual Hashtable ShowAction(int id = 0)
    {
        Hashtable ps = [];
        var item = model0.one(id);
        if (item.Count == 0)
            throw new NotFoundException();

        // added/updated should be filled before dynamic fields
        setAddUpdUser(ps, item);

        // dynamic fields
        if (is_dynamic_show)
        {
            //add form_tabs only if we have more than one tab
            if (config["form_tabs"] is ArrayList form_tabs && form_tabs.Count > 1)
                ps["form_tabs"] = form_tabs;

            ps["fields"] = prepareShowFields(item, ps);
        }

        // userlists support if necessary
        if (this.is_userlists)
            this.setUserLists(ps, id);

        if (is_activity_logs)
        {
            initFilter();

            list_filter["tab_activity"] = list_filter["tab_activity"].toStr(FwActivityLogs.TAB_COMMENTS);
            ps["list_filter"] = list_filter;
            ps["activity_entity"] = model0.table_name;
            ps["activity_rows"] = fw.model<FwActivityLogs>().listByEntityForUI(model0.table_name, id, (string)list_filter["tab_activity"]);
        }

        ps["id"] = id;
        ps["i"] = item;
        ps["return_url"] = return_url;
        ps["related_id"] = related_id;
        ps["base_url"] = base_url;
        ps["is_userlists"] = is_userlists;
        ps["is_activity_logs"] = is_activity_logs;
        ps["is_readonly"] = is_readonly;
        ps["tab"] = form_tab;

        //for RBAC
        ps["rbac"] = rbac;

        return ps;
    }

    public virtual Hashtable ShowFormAction(int id = 0)
    {
        // define form_new_defaults via config.json
        // Me.form_new_defaults = New Hashtable From {{"field", "default value"}} 'OR set new form defaults here

        Hashtable ps = [];
        var item = reqh("item"); // set defaults from request params

        if (isGet())
        {
            if (id > 0)
            {
                // edit screen
                item = model0.one(id);
            }
            else
            {
                // add new screen
                Hashtable item_new = [];
                Utils.mergeHash(item_new, form_new_defaults); // use hardcoded defaults if any
                Utils.mergeHash(item_new, item); // override with passed defaults
                item = item_new;
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
        {
            //add form_tabs only if we have more than one tab
            if (config["form_tabs"] is ArrayList form_tabs && form_tabs.Count > 1)
                ps["form_tabs"] = form_tabs;

            ps["fields"] = prepareShowFormFields(item, ps);
        }

        // TODO
        // ps["select_options_parent_id") ] model.listSelectOptionsParent()
        // FormUtils.comboForDate(item["fdate_combo"], ps, "fdate_combo")

        // Example: how to modify field definition
        //var fields = (ArrayList)ps["fields"];
        //var defRadio = defByFieldname("fradio", fields); // find field definition by fieldname
        //defRadio["type"] = "select"; // let's change 'radio' to 'select' type

        ps["id"] = id;
        ps["i"] = item;
        ps["return_url"] = return_url;
        ps["related_id"] = related_id;
        ps["is_readonly"] = is_readonly;
        ps["tab"] = form_tab;
        ps["is_showform"] = true; // flag for template that we are in show form

        //for RBAC
        ps["rbac"] = rbac;

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
        if (reqi("refresh") == 1 && (id == 0)) //for new record - just refresh the form, for existing - also try to save
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
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());
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
        bool result = validateRequiredDynamic(id, item);

        if (result && is_dynamic_showform)
            validateSimpleDynamic(id, item);

        // If result AndAlso Not SomeOtherValidation() Then
        // FW.FERR("other field name") = "HINT_ERR_CODE"
        // End If

        this.validateCheckResult();
    }

    /// <summary>
    /// return config for show/showform fields by tab
    /// </summary>
    /// <param name="prefix">show_fields or showform_fields</param>
    /// <param name="tab">optional tab code, if ommited - form_tab used</param>
    /// <returns></returns>
    protected virtual ArrayList getConfigShowFormFieldsByTab(string prefix, string tab = null)
    {
        tab ??= form_tab;
        var key = prefix + (tab.Length > 0 ? "_" + tab : "");
        return (ArrayList)config[key];
    }


    protected virtual bool validateRequiredDynamic(int id, Hashtable item)
    {
        var result = true;
        if (string.IsNullOrEmpty(this.required_fields) && is_dynamic_showform)
        {
            // if required_fields not defined - fill from showform_fields
            ArrayList fields = getConfigShowFormFieldsByTab("showform_fields");
            ArrayList req = [];
            foreach (Hashtable def in fields)
            {
                if (def["required"].toBool())
                    req.Add(def["field"]);
            }

            if (req.Count > 0)
                result = this.validateRequired(id, item, req.ToArray());
        }
        else
            result = this.validateRequired(id, item, this.required_fields);
        return result;
    }

    // simple validation via showform_fields
    protected virtual bool validateSimpleDynamic(int id, Hashtable item)
    {
        bool result = true;

        var is_new = (id == 0);
        var subtable_del = reqh("subtable_del");

        ArrayList fields = getConfigShowFormFieldsByTab("showform_fields");
        foreach (Hashtable def in fields)
        {
            string field = (string)def["field"];
            if (string.IsNullOrEmpty(field))
                continue;

            string type = (string)def["type"];

            if (type == "subtable_edit")
            {
                //validate subtable rows
                var sub_model = fw.model((string)def["model"]);

                var save_fields = (string)def["required_fields"] ?? "";
                var save_fields_checkboxes = (string)def["save_fields_checkboxes"];

                //check if we delete specific row
                var del_id = (string)subtable_del[field] ?? "";

                // row ids submitted as: item-<~field>[<~id>]
                // input name format: item-<~field>#<~id>[field_name]
                var hids = reqh("item-" + field);
                // sort hids.Keys, so numerical keys - first and keys staring with "new-" will be last
                var sorted_keys = hids.Keys.Cast<string>().OrderBy(x => x.StartsWith("new-") ? 1 : 0).ThenBy(x => x).ToList();
                foreach (string row_id in sorted_keys)
                {
                    if (row_id == del_id) continue; //skip deleted row

                    var row_item = reqh("item-" + field + "#" + row_id);
                    Hashtable itemdb = FormUtils.filter(row_item, save_fields);
                    FormUtils.filterCheckboxes(itemdb, row_item, save_fields_checkboxes, isPatch());

                    if (row_id.StartsWith("new-"))
                        itemdb[sub_model.junction_field_main_id] = id;

                    //VAILIDATE itemdb
                    var is_valid = validateSubtableRowDynamic(row_id, itemdb, def);
                }
            }
            else
            {
                // other types - use "validate" field
                var val = Utils.qh((string)def["validate"]);
                if (val.Count > 0)
                {
                    //for existing records only validate submitted fields
                    if (!is_new && !item.ContainsKey(field))
                        continue;

                    string field_value = (string)item[field];

                    if (val.ContainsKey("exists") && model0.isExistsByField(field_value, id, field))
                    {
                        fw.FormErrors[field] = "EXISTS";
                        result = false;
                    }
                    if (val.ContainsKey("isemail") && !Utils.isEmpty(field_value) && !FormUtils.isEmail(field_value))
                    {
                        fw.FormErrors[field] = "EMAIL";
                        result = false;
                    }
                    if (val.ContainsKey("isphone") && !Utils.isEmpty(field_value) && !FormUtils.isPhone(field_value))
                    {
                        fw.FormErrors[field] = "WRONG";
                        result = false;
                    }
                    if (val.ContainsKey("isdate") && !Utils.isEmpty(field_value) && !Utils.isDate(field_value))
                    {
                        fw.FormErrors[field] = "WRONG";
                        result = false;
                    }
                    if (val.ContainsKey("isfloat") && !Utils.isEmpty(field_value) && !Utils.isFloat(field_value))
                    {
                        fw.FormErrors[field] = "WRONG";
                        result = false;
                    }
                }
            }

        }
        return result;
    }

    /// <summary>
    /// validate single subtable row using def[required_fields] and fill fw.FormErrors with row errors if any
    /// Override in controller and add custom validation if needed
    /// </summary>
    /// <param name="row_id">row_id can start with "new-" (for new rows) or be numerical id (existing rows)</param>
    /// <param name="item">submitted row data from the form</param>
    /// <param name="def">subable definition from config.json</param>
    /// <returns></returns>
    protected virtual bool validateSubtableRowDynamic(string row_id, Hashtable item, Hashtable def)
    {
        var result = true;
        var required_fields = Utils.qw((string)def["required_fields"] ?? "");
        if (required_fields.Length == 0)
            return result; //nothing to validate

        var row_errors = new Hashtable();
        var id = row_id.StartsWith("new-") ? 0 : row_id.toInt();
        result = this.validateRequired(id, item, required_fields, row_errors);
        if (!result)
        {
            //fill global fw.FormErrors with row errors
            var field = (string)def["field"];
            foreach (var field_name in row_errors.Keys)
            {
                // row input names format: item-<~field>#<~id>[field_name]
                fw.FormErrors[$"item-{field}#{row_id}[{field_name}]"] = true;
            }
            fw.FormErrors["REQUIRED"] = true; // also set global error
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

        try
        {
            model0.deleteWithPermanentCheck(id);
            fw.flash("onedelete", 1);
        }
        catch (Exception ex)
        {
            // check and parese FK errors
            // The DELETE statement conflicted with the REFERENCE constraint "FK__demos_dem__demos__253C7D7E". The conflict occurred in database "demo", table "dbo.demos_demo_dicts", column 'demos_id'. The statement has been terminated.
            var msg = ex.Message;
            var regex = new Regex(@"table\s+""[^.]+\.([^""]+)"",\s+column\s+'([^']+)'");
            var match = regex.Match(msg);
            if (match.Success)
            {
                string tableName = Utils.capitalize(match.Groups[1].Value, "all");
                //string columnName = match.Groups[2].Value;
                msg = $"This record cannot be deleted because it is linked to another {tableName} record. You will need to unlink these records before either can be deleted";
            }

            fw.flash("error", msg);
            fw.redirect($"{base_url}/{id}/delete");
        }

        return this.afterSave(true);
    }

    public virtual Hashtable RestoreDeletedAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        model0.update(id, new Hashtable() { { model0.field_status, FwModel.STATUS_ACTIVE } });

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

        if (user_lists_id > 0)
        {
            var user_lists = fw.model<UserLists>().one(user_lists_id);
            if (user_lists.Count == 0 || user_lists["add_users_id"].toInt() != fw.userId)
                throw new UserException("Wrong Request");
        }

        int ctr = saveMultiRows(cbses.Keys, is_delete, user_lists_id, remove_user_lists_id);

        saveMultiResult(ctr, is_delete, user_lists_id, remove_user_lists_id);

        return this.afterSave(true, new Hashtable() { { "ctr", ctr } });
    }

    // ********************* support for autocomlete related items
    public virtual Hashtable AutocompleteAction()
    {
        var q = reqs("q"); //required - query string

        //optional params
        var id = reqi("id"); //specific id, if just need iname for it (used to preload existing id/label for edit form)
        var model_name = reqs("model");
        FwModel ac_model = null;
        if (string.IsNullOrEmpty(model_name))
        {
            //if no model passed - use model_related
            ac_model = model_related;
        }
        else
        {
            //validation - only allow models from showform_fields type=autocomplete
            var form_tabs = config["form_tabs"] as ArrayList ?? [];
            foreach (Hashtable form_tab in form_tabs)
            {
                var fields = getConfigShowFormFieldsByTab("showform_fields", form_tab["tab"].toStr());
                foreach (Hashtable def in fields)
                {
                    if (def["type"].toStr() == "autocomplete" && def["lookup_model"].toStr() == model_name)
                    {
                        ac_model = fw.model(model_name);
                        break;
                    }
                }
            }
        }

        if (ac_model == null)
            throw new UserException("No model defined");

        //ArrayList items;
        List<string> items;
        if (id > 0)
        {
            //var item = ac_model.one(id);
            //items = [new Hashtable() { { "id", id }, { "iname", item["iname"] } }];
            items = [ac_model.iname(id)];
        }
        else
        {
            //items = ac_model.listSelectOptionsAutocomplete(q);
            items = ac_model.getAutocompleteList(q);
        }

        return new Hashtable() { { "_json", items } };
    }

    // ********************* support for customizable list screen
    public virtual void UserViewsAction(int id = 0)
    {
        Hashtable ps = [];

        var rows = getViewListArr(getViewListUserFields(), true); // list all fields
        ps["rows"] = rows;

        ps["select_userviews"] = fw.model<UserViews>().listSelectByIcode(base_url);
        fw.parser("/common/list/userviews", ps);
    }

    public virtual Hashtable SaveUserViewsAction()
    {
        var fld = reqh("fld");
        var load_id = reqi("load_id");
        var is_reset = reqb("is_reset");
        var density = reqs("density");
        var is_list_edit = reqb("is_list_edit");
        var icode = base_url + (is_list_edit ? "/edit" : "");

        if (load_id > 0)
            // set fields from specific view
            fw.model<UserViews>().setViewForIcode(icode, load_id);
        else if (is_reset)
            // reset fields to defaults
            fw.model<UserViews>().updateByIcodeFields(icode, view_list_defaults);
        else if (density.Length > 0)
        {
            // save density
            // validate density can be only table-sm, table-dense, table-normal, otherwise - set empty
            if (!"table-sm table-dense table-normal".Contains(density))
                density = "";
            fw.model<UserViews>().updateByIcode(icode, DB.h("density", density));
        }
        else
        {
            var item = reqh("item");
            var iname = item["iname"].toStr();

            // save fields
            // order by value
            var ordered = fld.Cast<DictionaryEntry>().OrderBy(entry => entry.Value.toInt()).ToList();
            // and then get ordered keys
            List<string> anames = [];
            foreach (var el in ordered)
                anames.Add((string)el.Key);
            var fields = string.Join(" ", anames);

            if (!string.IsNullOrEmpty(iname))
            {
                // create new view by name or update if this name exists
                fw.model<UserViews>().addOrUpdateByUK(icode, fields, iname);
            }
            // update default view with fields
            fw.model<UserViews>().updateByIcodeFields(icode, fields);
        }

        return afterSave(true, null, false, "no_action", return_url);
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
        var id = item["id"].toInt();

        ArrayList fields = getConfigShowFormFieldsByTab("show_fields");
        foreach (Hashtable def in fields)
        {
            def["i"] = item; // ref to item
            string dtype = (string)def["type"];
            string field = def["field"].toStr();

            if (dtype is "row" or "row_end" or "col" or "col_end")
                // structural tags
                def["is_structure"] = true;
            else if (dtype == "multi")
            {
                if (def.ContainsKey("lookup_model"))
                    def["multi_datarow"] = fw.model((string)def["lookup_model"]).listWithChecked((string)item[field], def);
                else
                {
                    if (def["is_by_linked"].toBool())
                        // list main items by linked id from junction model (i.e. list of Users(with checked) for Company from UsersCompanies model)
                        def["multi_datarow"] = fw.model((string)def["model"]).listMainByLinkedId(id, def); //junction model
                    else
                        // list linked items by main id from junction model (i.e. list of Companies(with checked) for User from UsersCompanies model)
                        def["multi_datarow"] = fw.model((string)def["model"]).listLinkedByMainId(id, def); //junction model
                }

            }
            else if (dtype == "multi_prio")
                // complex field with prio
                def["multi_datarow"] = fw.model((string)def["model"]).listLinkedByMainId(id, def); //junction model
            else if (dtype == "att")
                def["att"] = fw.model<Att>().one(item[field]);
            else if (dtype == "att_links")
                def["att_links"] = fw.model<Att>().listLinked(model0.table_name, id.toInt());
            else if (dtype == "att_files")
                def["att_files"] = fw.model<Att>().listByEntity(model0.table_name, id.toInt());

            else if (dtype == "subtable")
            {
                // subtable functionality
                var sub_model = fw.model((string)def["model"]);
                var list_rows = sub_model.listByMainId(id, def); //list related rows from db
                sub_model.prepareSubtable(list_rows, id, def);

                def["list_rows"] = list_rows;
            }
            else
            {
                // single values
                // lookups
                if (def.ContainsKey("lookup_table"))
                {
                    string lookup_key = def["lookup_key"].toStr();
                    if (lookup_key == "")
                        lookup_key = "id";

                    string lookup_field = def["lookup_field"].toStr();
                    if (lookup_field == "")
                        lookup_field = "iname";

                    var lookup_row = db.row((string)def["lookup_table"], DB.h(lookup_key, item[field]));
                    def["lookup_row"] = lookup_row;
                    def["value"] = lookup_row[lookup_field];
                }
                else if (def.ContainsKey("lookup_model"))
                {
                    var lookup_model = fw.model((string)def["lookup_model"]);
                    def["lookup_id"] = item[field].toInt();
                    var lookup_row = lookup_model.one(def["lookup_id"]);
                    def["lookup_row"] = lookup_row;

                    string lookup_field = def["lookup_field"].toStr();
                    if (lookup_field == "")
                        lookup_field = lookup_model.field_iname;

                    def["value"] = lookup_row[lookup_field];
                    if (!def.ContainsKey("admin_url"))
                        def["admin_url"] = "/Admin/" + def["lookup_model"]; // default admin url from model name
                }
                else if (def.ContainsKey("lookup_tpl"))
                    def["value"] = FormUtils.selectTplName((string)def["lookup_tpl"], (string)item[field], fw.route.controller_path.ToLower());
                else if (def.ContainsKey("options"))
                {
                    // select options
                    var options = def["options"] as Hashtable;
                    def["value"] = options[item[field]];
                }
                else
                    def["value"] = item[field];

                // convertors
                if (def.ContainsKey("conv"))
                {
                    if ((string)def["conv"] == "time_from_seconds")
                        def["value"] = FormUtils.intToTimeStr(def["value"].toInt());
                }
            }
        }
        return fields;
    }

    public virtual ArrayList prepareShowFormFields(Hashtable item, Hashtable ps)
    {
        var id = item["id"].toInt();

        var subtable_add = reqh("subtable_add");
        var subtable_del = reqh("subtable_del");

        var fields = getConfigShowFormFieldsByTab("showform_fields");
        if (fields == null)
            throw new ApplicationException("Controller config.json doesn't contain 'showform_fields'");

        foreach (Hashtable def in fields)
        {
            //logger(def);
            def["i"] = item; // ref to item
            def["ps"] = ps; // ref to whole ps
            string dtype = (string)def["type"]; // type is required
            string field = def["field"].toStr();

            if (id == 0 && (dtype == "added" || dtype == "updated"))
                // special case - hide if new item screen
                def["class"] = "d-none";

            if (dtype == "row" || dtype == "row_end" || dtype == "col" || dtype == "col_end")
                // structural tags
                def["is_structure"] = true;
            else if (dtype == "multicb")
            {
                if (def.ContainsKey("lookup_model"))
                    def["multi_datarow"] = fw.model((string)def["lookup_model"]).listWithChecked(item[field].toStr(), def);
                else
                {
                    if (def["is_by_linked"].toBool())
                        // list main items by linked id from junction model (i.e. list of Users(with checked) for Company from UsersCompanies model)
                        def["multi_datarow"] = fw.model((string)def["model"]).listMainByLinkedId(id, def); //junction model
                    else
                        // list linked items by main id from junction model (i.e. list of Companies(with checked) for User from UsersCompanies model)
                        def["multi_datarow"] = fw.model((string)def["model"]).listLinkedByMainId(id, def); //junction model
                }

                foreach (Hashtable row in (ArrayList)def["multi_datarow"]) // contains id, iname, is_checked
                    row["field"] = def["field"];
            }
            else if (dtype == "multicb_prio")
            {
                def["multi_datarow"] = fw.model((string)def["model"]).listLinkedByMainId(id, def); // junction model

                foreach (Hashtable row in (ArrayList)def["multi_datarow"]) // contains id, iname, is_checked, _link[prio]
                    row["field"] = def["field"];
            }
            else if (dtype == "att_edit")
            {
                def["att"] = fw.model<Att>().one(item[field]);
                def["value"] = item[field];
            }
            else if (dtype == "att_links_edit")
                def["att_links"] = fw.model<Att>().listLinked(model0.table_name, id.toInt());
            else if (dtype == "att_files_edit")
                def["att_files"] = fw.model<Att>().listByEntity(model0.table_name, id.toInt());

            else if (dtype == "subtable_edit")
            {
                // subtable functionality
                var sub_model = fw.model((string)def["model"]);
                var list_rows = new ArrayList();

                if (isGet())
                {
                    if (id > 0)
                    {
                        list_rows = sub_model.listByMainId(id, def); //list related rows from db
                    }
                    else
                        sub_model.prepareSubtableAddNew(list_rows, id, def); //add at least one row
                }
                else
                {
                    //check if we deleted specific row
                    var del_id = (string)subtable_del[field] ?? "";

                    //copy list related rows from the form
                    // row ids submitted as: item-<~field>[<~id>]
                    // input name format: item-<~field>#<~id>[field_name]
                    var hids = reqh("item-" + field);
                    // sort hids.Keys, so numerical keys - first and keys staring with "new-" will be last
                    var sorted_keys = hids.Keys.Cast<string>().OrderBy(x => x.StartsWith("new-") ? 1 : 0).ThenBy(x => x).ToList();
                    foreach (string row_id in sorted_keys)
                    {
                        if (row_id == del_id) continue; //skip deleted row

                        var row_item = reqh("item-" + field + "#" + row_id);
                        row_item["id"] = row_id;

                        list_rows.Add(row_item);
                    }
                }

                //delete row clicked
                //if (subtable_del.ContainsKey(field))
                //{
                //    var del_id = (string)subtable_del[field];
                //    // delete with LINQ from the form list (actual delete from db will be on save)
                //    list_rows = new ArrayList((from Hashtable d in list_rows
                //                               where (string)d["id"] != del_id
                //                               select d).ToList());
                //}

                //add new clicked
                if (subtable_add.ContainsKey(field))
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
                    string lookup_key = def["lookup_key"].toStr();
                    if (lookup_key == "")
                        lookup_key = "id";

                    string lookup_field = def["lookup_field"].toStr();
                    if (lookup_field == "")
                        lookup_field = "iname";

                    var lookup_row = db.row((string)def["lookup_table"], DB.h(lookup_key, item[field]));
                    def["lookup_row"] = lookup_row;
                    def["value"] = lookup_row[lookup_field];
                }
                else if (def.ContainsKey("lookup_model"))
                {
                    var lookup_model = fw.model((string)def["lookup_model"]);

                    if (dtype == "select" || dtype == "radio")
                    {
                        // lookup select
                        def["select_options"] = lookup_model.listSelectOptions(def);
                        def["value"] = item[field];
                    }
                    else
                    {
                        // single value from lookup
                        if (isGet())
                        {
                            def["lookup_id"] = item[field].toInt();
                            var lookup_row = lookup_model.one(def["lookup_id"]);
                            def["lookup_row"] = lookup_row;

                            string lookup_field = def["lookup_field"].toStr();
                            if (lookup_field == "")
                                lookup_field = lookup_model.field_iname;

                            def["value"] = lookup_row[lookup_field];
                        }
                        else
                        {
                            //when form refreshed - get value from the form
                            if (dtype == "autocomplete")
                                def["value"] = item[field + "_iname"]; //for autocomplete get from _iname
                            else
                                def["value"] = item[field];
                        }
                    }

                    if (!def.ContainsKey("admin_url"))
                        def["admin_url"] = "/Admin/" + def["lookup_model"]; // default admin url from model name
                }
                else if (def.ContainsKey("lookup_tpl"))
                {
                    def["select_options"] = FormUtils.selectTplOptions((string)def["lookup_tpl"], fw.route.controller_path.ToLower());
                    def["value"] = item[field];
                    foreach (Hashtable row in (ArrayList)def["select_options"]) // contains id, iname
                    {
                        row["is_inline"] = def["is_inline"];
                        row["field"] = def["field"];
                        row["value"] = item[field];
                    }
                }
                else if (def.ContainsKey("options"))
                {
                    //select options as array - convert to arraylist of id => iname
                    var options = def["options"] as Hashtable;
                    var select_options = new ArrayList();
                    foreach (DictionaryEntry entry in options)
                        select_options.Add(new Hashtable() {
                            { "id", entry.Key },
                            { "iname", entry.Value },
                            { "is_inline", def["is_inline"] },
                            { "field", def["field"] },
                            { "value", item[field] }
                        });
                }
                else
                    def["value"] = item[field];

                // convertors
                if (def.ContainsKey("conv"))
                {
                    if ((string)def["conv"] == "time_from_seconds")
                        def["value"] = FormUtils.intToTimeStr(def["value"].toInt());
                }
            }
        }
        return fields;
    }

    // auto-process fields BEFORE record saved to db
    protected virtual void processSaveShowFormFields(int id, Hashtable fields)
    {
        Hashtable item = reqh("item");

        var showform_fields = _fieldsToHash(getConfigShowFormFieldsByTab("showform_fields"));

        var fnullable = Utils.qh(save_fields_nullable);

        // special auto-processing for fields of particular types - use .Cast<string>().ToArray() to make a copy of keys as we modify fields
        foreach (string field in fields.Keys.Cast<string>().ToArray())
        {
            if (!showform_fields.ContainsKey(field))
                continue;

            var def = (Hashtable)showform_fields[field];
            string type = (string)def["type"];
            if (type == "autocomplete")
            {
                var lookup_model = fw.model((string)def["lookup_model"]);
                var field_value = item[field + "_iname"].toStr(); // autocomplete value is in "${field}_iname"
                fields[field] = lookup_model.findOrAddByIname(field_value, out _);
            }
            else if (type == "date_combo")
                fields[field] = FormUtils.dateForCombo(item, field).ToString();
            else if (type == "time")
                fields[field] = FormUtils.timeStrToInt((string)fields[field]); // ftime - convert from HH:MM to int (0-24h in seconds)
            else if (type == "number")
            {
                if (fnullable.ContainsKey(field) && string.IsNullOrEmpty((string)fields[field]))
                    // if field nullable and empty - pass NULL
                    fields[field] = null;
                else
                    fields[field] = fields[field].toFloat();// number - convert to number (if field empty or non-number - it will become 0)
            }
        }
    }

    // auto-process fields AFTER record saved to db
    protected virtual void processSaveShowFormFieldsAfter(int id, Hashtable fields)
    {
        var subtable_del = reqh("subtable_del");

        var fields_update = new Hashtable();

        // for now we just look if we have att_links_edit field and update att links
        foreach (Hashtable def in getConfigShowFormFieldsByTab("showform_fields"))
        {
            string field = (string)def["field"];
            string type = (string)def["type"];
            if (type == "att_links_edit")
            {
                var att_post_param = "att";
                if (def.ContainsKey("att_post_prefix"))
                    att_post_param = (string)def["att_post_prefix"];
                // if PATCH - only update is post param is present (otherwise it will delete all records)
                if (isPatch() && req(att_post_param) == null)
                    continue;

                fw.model<AttLinks>().updateJunction(model0.table_name, id, reqh(att_post_param));
            }
            else if (type == "att_files_edit")
            {
                //table_name, item_id
                var itemdb = new Hashtable {
                    { "fwentities_id", fw.model<FwEntities>().idByIcodeOrAdd(model0.table_name) },
                    { "item_id", id }
                };
                var addedAtt = fw.model<Att>().uploadMulti(itemdb);

            }
            else if (type == "multicb")
            {
                if (Utils.isEmpty(def["model"]))
                {
                    // multiple checkboxes -> non-junction model single comma-delimited field                    
                    // if PATCH - only update is post param is present (otherwise it will delete all records)
                    if (isPatch() && req(field + "_multi") == null)
                        continue;

                    fields_update[field] = FormUtils.multi2ids(reqh(field + "_multi"));
                }
                else
                {
                    //junction model based
                    // if PATCH - only update is post param is present (otherwise it will delete all records)
                    if (isPatch() && req(field + "_multi") == null)
                        continue;

                    if (def["is_by_linked"].toBool())
                        //by linked id
                        fw.model((string)def["model"]).updateJunctionByLinkedId(id, reqh(field + "_multi")); // junction model
                    else
                        //by main id
                        fw.model((string)def["model"]).updateJunctionByMainId(id, reqh(field + "_multi")); // junction model
                }
            }
            else if (type == "multicb_prio")
            {
                // if PATCH - only update is post param is present (otherwise it will delete all records)
                if (isPatch() && req(field + "_multi") == null)
                    continue;

                fw.model((string)def["model"]).updateJunctionByMainId(id, reqh(field + "_multi")); // junction model
            }
            else if (type == "subtable_edit")
            {
                //save subtable
                // if PATCH - only update is post param is present (otherwise it will delete all records)
                if (isPatch() && req("item-" + field) == null)
                    continue;

                var sub_model = fw.model((string)def["model"]);

                var save_fields = (string)def["save_fields"];
                var save_fields_checkboxes = (string)def["save_fields_checkboxes"];

                //check if we delete specific row
                var del_id = (string)subtable_del[field] ?? "";

                //mark all related records as under update (status=1)
                sub_model.setUnderUpdateByMainId(id);

                //update and add new rows

                // row ids submitted as: item-<~field>[<~id>]
                // input name format: item-<~field>#<~id>[field_name]
                var hids = reqh("item-" + field);
                // sort hids.Keys, so numerical keys - first and keys staring with "new-" will be last
                var sorted_keys = hids.Keys.Cast<string>().OrderBy(x => x.StartsWith("new-") ? 1 : 0).ThenBy(x => x).ToList();
                var junction_field_status = sub_model.getJunctionFieldStatus();
                foreach (string row_id in sorted_keys)
                {
                    if (row_id == del_id) continue; //skip deleted row

                    var row_item = reqh("item-" + field + "#" + row_id);
                    Hashtable itemdb = FormUtils.filter(row_item, save_fields);
                    FormUtils.filterCheckboxes(itemdb, row_item, save_fields_checkboxes, isPatch());

                    itemdb[junction_field_status] = FwModel.STATUS_ACTIVE; // mark new and updated existing rows as active

                    modelAddOrUpdateSubtableDynamic(id, row_id, itemdb, def, sub_model);
                }

                //remove any not updated rows (i.e. those deleted by user)
                sub_model.deleteUnderUpdateByMainId(id);
            }
        }

        if (fields_update.Count > 0)
        {
            model0.update(id, fields_update);
        }
    }


    /// <summary>
    /// modelAddOrUpdate for subtable with dynamic model
    /// </summary>
    /// <param name="main_id">main entity id</param>
    /// <param name="row_id">row_id can start with "new-" (for new rows) or be numerical id (existing rows)</param>
    /// <param name="fields">fields to save to db</param>
    /// <param name="def">subable definition from config.json</param>
    /// <param name="sub_model">optional subtable model, if not passed def[model] will be used</param>
    /// <returns></returns>
    protected virtual int modelAddOrUpdateSubtableDynamic(int main_id, string row_id, Hashtable fields, Hashtable def, FwModel sub_model = null)
    {
        int id;

        if (sub_model == null)
        {
            var model_name = (string)def["model"];
            sub_model = fw.model(model_name);
        }

        if (row_id.StartsWith("new-"))
        {
            fields[sub_model.junction_field_main_id] = main_id;
            id = sub_model.add(fields);
        }
        else
        {
            id = row_id.toInt();
            sub_model.update(id, fields);
        }

        return id;
    }


    /// <summary>
    /// return first field definition by field name
    /// </summary>
    /// <param name="fields"></param>
    /// <param name="field_name"></param>
    /// <returns></returns>
    protected Hashtable defByFieldname(string field_name, ArrayList fields)
    {
        foreach (Hashtable def in fields)
        {
            if (def["field"].toStr() == field_name)
                return def;
        }
        return null;
    }

    // convert config's fields list into hashtable as field => {}
    // if there are more than one field - just first field added to the hash
    protected Hashtable _fieldsToHash(ArrayList fields)
    {
        Hashtable result = [];
        foreach (Hashtable fldinfo in fields)
        {
            if (fldinfo.ContainsKey("field") && !result.ContainsKey((string)fldinfo["field"]))
                result[(string)fldinfo["field"]] = fldinfo;
        }
        return result;
    }
}
