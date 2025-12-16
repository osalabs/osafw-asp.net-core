// Fw Dynamic controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2018 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public class FwDynamicController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected FwModel? model_related;

    protected static readonly string[] DEF_TYPES_STRUCTURE = ["row", "row_end", "col", "col_end", "header"];

    public override void init(FW fw)
    {
        base.init(fw);
    }

    #region Standard REST Actions - Index/Show/ShowForm/Save

    /// <summary>
    /// contains logic to display list screen
    /// Note! if query contains "export" - early empty result returned and FW will call exportList() after this
    /// </summary>
    /// <returns>FwRow - related template will be parsed, null - no templates parsed (if action did all the output)</returns>
    public virtual FwDict IndexAction()
    {
        // get filters from the search form
        this.initFilter();

        this.setListSorting();

        this.setListSearch();
        this.setListSearchStatus(); // status field is not always in table, so keep it separate

        // make list select lean: only fetch visible columns (+ id)
        setListFields();

        // set here non-standard search
        // If f("field") > "" Then
        // Me.list_where &= " and field=" & db.q(f("field"))
        // End If

        this.getListRows();
        // add/modify rows from db if necessary
        // For Each row As FwRow In Me.list_rows
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

        ps["select_userfilters"] = fw.model<UserFilters>().listSelectByIcode(fw.G["controller.action"].toStr());

        return ps;
    }

    // NOTE, only use if list screen columns does not depend on non-visible fields
    /// <summary>
    /// Select only visible list fields (+ id) when dynamic index is enabled.
    /// </summary>
    //protected override void setListFields()
    //{
    //    if (!(is_dynamic_index || (is_dynamic_index_edit && is_list_edit)))
    //        return; // keep default behaviour - select all fields

    //    var fields = getViewListUserFields();
    //    var afields = Utils.qw(fields).Where(f => !string.IsNullOrEmpty(f)).ToList();

    //    // ensure id is present
    //    if (!afields.Contains(model0.field_id))
    //        afields.Insert(0, model0.field_id);

    //    // quote identifiers only when needed to keep compatibility with subquery aliases
    //    var quoted = afields.Select(f => db.qid(f, false));
    //    list_fields = string.Join(", ", quoted);
    //}

    //Prev/Next navigation
    public virtual FwDict NextAction(string form_id)
    {
        var id = form_id.toInt();
        if (id == 0)
            return new FwDict { { "_redirect", base_url } };

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
            return new FwDict { { "_redirect", base_url } };

        int go_id;
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
                return new FwDict { { "_redirect", base_url } };
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
                return new FwDict { { "_redirect", base_url } };
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

        return new FwDict { { "_redirect", url }, { "id", go_id } };
    }

    public virtual FwDict? ShowAction(int id = 0)
    {
        FwDict ps = [];
        var item = modelOneOrFail(id);

        // added/updated should be filled before dynamic fields
        setAddUpdUser(ps, item);

        // dynamic fields
        if (is_dynamic_show)
        {
            //add form_tabs only if we have more than one tab
            if (config["form_tabs"] is IList form_tabs && form_tabs.Count > 1)
                ps["form_tabs"] = new FwList(form_tabs);

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
            ps["activity_rows"] = fw.model<FwActivityLogs>().listByEntityForUI(model0.table_name, id, list_filter["tab_activity"].toStr());
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

    public virtual FwDict? ShowFormAction(int id = 0)
    {
        // define form_new_defaults via config.json
        // Me.form_new_defaults = New FwRow From {{"field", "default value"}} 'OR set new form defaults here

        FwDict ps = [];
        var item = reqh("item"); // set defaults from request params

        if (isGet())
        {
            if (id > 0)
            {
                // edit screen
                item = modelOneOrFail(id);
            }
            else
            {
                // add new screen
                FwDict item_new = [];
                Utils.mergeHash(item_new, form_new_defaults); // use hardcoded defaults if any
                Utils.mergeHash(item_new, item); // override with passed defaults
                item = item_new;
            }
        }
        else
        {
            // read from db
            FwDict itemdb = modelOne(id);
            // and merge new values from the form
            Utils.mergeHash(itemdb, item);
            item = itemdb;
        }

        setAddUpdUser(ps, item);

        if (is_dynamic_showform)
        {
            //add form_tabs only if we have more than one tab
            if (config["form_tabs"] is IList form_tabs && form_tabs.Count > 1)
                ps["form_tabs"] = new FwList(form_tabs);

            ps["fields"] = prepareShowFormFields(item, ps);
        }

        // TODO
        // ps["select_options_parent_id") ] model.listSelectOptionsParent()

        // Example: how to modify field definition
        //var fields = (FwList)ps["fields"]!;
        //var defRadio = defByFieldname("fradio", fields); // find field definition by fieldname
        //defRadio?["type"] = "select"; // let's change 'radio' to 'select' type

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

    public override int modelAddOrUpdate(int id, FwDict fields)
    {
        if (is_dynamic_showform)
            processSaveShowFormFields(id, fields);

        id = base.modelAddOrUpdate(id, fields);

        if (is_dynamic_showform)
            processSaveShowFormFieldsAfter(id, fields);

        return id;
    }

    public virtual FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");

        fw.model<Users>().checkReadOnly();
        if (reqb("refresh"))
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, [id]);
            return null;
        }

        FwDict item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // var itemOld = modelOne(id);

        FwDict itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());

        id = this.modelAddOrUpdate(id, itemdb);

        return this.afterSave(success, id, is_new);
    }
    #endregion

    #region Validation
    /// <summary>
    /// Performs submitted form validation for required field and simple validations: exits, isemail, isphone, isdate, isfloat.
    /// If more complex validation required - just override this and call just necessary validation
    /// </summary>
    /// <param name="id"></param>
    /// <param name="item"></param>
    public virtual void Validate(int id, FwDict item)
    {
        bool result = validateRequiredDynamic(id, item);

        if (result && is_dynamic_showform)
            validateSimpleDynamic(id, item);

        // If result AndAlso Not SomeOtherValidation() Then
        // FW.FERR("other field name") = "HINT_ERR_CODE"
        // End If

        this.validateCheckResult();
    }

    public virtual void Validate<TRow>(int id, TRow dto) where TRow : class, new()
    {
        ArgumentNullException.ThrowIfNull(dto);
        Validate(id, dto.toFwDict());
    }

    protected virtual bool validateRequiredDynamic(int id, FwDict item)
    {
        var result = true;
        if (string.IsNullOrEmpty(this.required_fields) && is_dynamic_showform)
        {
            // if required_fields not defined - fill from showform_fields
            FwList fields = getConfigShowFormFieldsByTab("showform_fields");
            StrList req = [];
            foreach (FwDict def in fields)
            {
                if (def["required"].toBool())
                    req.Add(def["field"].toStr());
            }

            if (req.Count > 0)
                result = this.validateRequired(id, item, req.ToArray());
        }
        else
            result = this.validateRequired(id, item, this.required_fields);
        return result;
    }

    // simple validation via showform_fields
    protected virtual bool validateSimpleDynamic(int id, FwDict item)
    {
        bool result = true;

        var is_new = (id == 0);

        FwList fields = getConfigShowFormFieldsByTab("showform_fields");
        foreach (FwDict def in fields)
        {
            string field = def["field"].toStr();
            if (string.IsNullOrEmpty(field))
                continue;

            string type = def["type"].toStr();

            if (type == "subtable_edit")
            {
                validateSubtableDynamic(id, item, def);
            }
            else
            {
                // other types - use "validate" field
                var val = Utils.qh(def["validate"].toStr());
                if (val.Count > 0)
                {
                    //for existing records only validate submitted fields
                    if (!is_new && !item.ContainsKey(field))
                        continue;

                    string field_value = item[field].toStr();

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
    /// validate all posted subtable rows for the given subtable definition
    /// </summary>
    /// <param name="id">related item id</param>
    /// <param name="item">posted item (use in overrides as needed)</param>
    /// <param name="def">field definition</param>
    protected virtual void validateSubtableDynamic(int id, FwDict item, FwDict def)
    {
        var subtable_del = reqh("subtable_del");
        var field = def["field"].toStr();

        //validate subtable rows
        var sub_model = fw.model(def["model"].toStr());

        var save_fields = def["required_fields"].toStr();
        var save_fields_checkboxes = def["save_fields_checkboxes"].toStr();

        //check if we delete specific row
        var del_id = subtable_del[field].toStr();

        // row ids submitted as: item-<~field>[<~id>]
        // input name format: item-<~field>#<~id>[field_name]
        var hids = reqh("item-" + field);
        // sort hids.Keys, so numerical keys - first and keys staring with "new-" will be last
        var sorted_keys = hids.Keys.Cast<string>().OrderBy(x => x.StartsWith("new-") ? 1 : 0).ThenBy(x => x).ToList();
        foreach (string row_id in sorted_keys)
        {
            if (row_id == del_id) continue; //skip deleted row

            var row_item = reqh("item-" + field + "#" + row_id);
            FwDict itemdb = FormUtils.filter(row_item, save_fields);
            FormUtils.filterCheckboxes(itemdb, row_item, save_fields_checkboxes, isPatch());

            if (row_id.StartsWith("new-"))
                itemdb[sub_model.junction_field_main_id] = id;

            //VAILIDATE itemdb
            var is_valid = validateSubtableRowDynamic(row_id, itemdb, def);
        }
    }

    /// <summary>
    /// validate single subtable row using def[required_fields] and fill fw.FormErrors with row errors if any
    /// Override in controller and add custom validation if needed
    /// </summary>
    /// <param name="row_id">row_id can start with "new-" (for new rows) or be numerical id (existing rows)</param>
    /// <param name="item">submitted row data from the form</param>
    /// <param name="def">subable definition from config.json</param>
    /// <returns></returns>
    protected virtual bool validateSubtableRowDynamic(string row_id, FwDict item, FwDict def)
    {
        var result = true;
        var req_fields = Utils.qw(def["required_fields"].toStr());
        if (req_fields.Length == 0)
            return result; //nothing to validate

        var row_errors = new FwDict();
        var id = row_id.StartsWith("new-") ? 0 : row_id.toInt();
        result = this.validateRequired(id, item, req_fields, row_errors);
        if (!result)
        {
            //fill global fw.FormErrors with row errors
            var field = def["field"].toStr();
            foreach (var field_name in row_errors.Keys)
            {
                // row input names format: item-<~field>#<~id>[field_name]
                fw.FormErrors[$"item-{field}#{row_id}[{field_name}]"] = true;
            }
            fw.FormErrors["REQUIRED"] = true; // also set global error
        }

        return result;
    }
    #endregion

    #region Delete/Restore Actions
    public virtual void ShowDeleteAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        var ps = new FwDict()
        {
            {"i", modelOneOrFail(id)},
            {"related_id", this.related_id},
            {"return_url", this.return_url},
            {"base_url", this.base_url},
        };

        fw.parser("/common/form/showdelete", ps);
    }

    public virtual FwDict? DeleteAction(int id)
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

    public virtual FwDict? RestoreDeletedAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        model0.update(id, new FwDict() { { model0.field_status, FwModel.STATUS_ACTIVE } });

        fw.flash("record_updated", 1);
        return this.afterSave(true, id);
    }
    #endregion Actions

    #region Bulk Actions
    public virtual FwDict? SaveMultiAction()
    {
        route_onerror = FW.ACTION_INDEX;

        FwDict cbses = reqh("cb");
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

        return this.afterSave(true, new FwDict() { { "ctr", ctr } });
    }
    #endregion

    #region support for autocomlete related items
    public virtual FwDict AutocompleteAction()
    {
        var q = reqs("q"); //required - query string

        //optional params
        var id = reqi("id"); //specific id, if just need iname for it (used to preload existing id/label for edit form)
        var model_name = reqs("model");
        FwModel? ac_model = null;
        if (string.IsNullOrEmpty(model_name))
        {
            //if no model passed - use model_related
            ac_model = model_related;
        }
        else
        {
            //validation - only allow models from showform_fields type=autocomplete
            FwList ftabs = config["form_tabs"] is IList tabs ? new(tabs) : [];
            foreach (FwDict ftab in ftabs)
            {
                var fields = getConfigShowFormFieldsByTab("showform_fields", ftab["tab"].toStr());
                foreach (FwDict def in fields)
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

        var acModel = ac_model;

        //FwList items;
        StrList items;
        if (id > 0)
        {
            //var item = ac_model.one(id);
            //items = [new FwRow() { { "id", id }, { "iname", item["iname"] } }];
            items = [acModel.iname(id)];
        }
        else
        {
            //items = ac_model.listSelectOptionsAutocomplete(q);
            items = acModel.listAutocomplete(q);
        }

        return new FwDict() { { "_json", items } };
    }
    #endregion

    #region support for customizable list screen
    public virtual void UserViewsAction(int id = 0)
    {
        FwDict ps = [];

        var rows = getViewListArr(getViewListUserFields(), true); // list all fields
        ps["rows"] = rows;

        ps["select_userviews"] = fw.model<UserViews>().listSelectByIcode(base_url);
        fw.parser("/common/list/userviews", ps);
    }

    public virtual FwDict? SaveUserViewsAction()
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
            var ordered = fld.OrderBy(kvp => kvp.Value.toInt()).ToList();
            // and then get ordered keys
            StrList anames = [];
            foreach (var el in ordered)
                anames.Add(el.Key.toStr());
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
    #endregion

    #region support for sortable rows
    public FwDict SaveSortAction()
    {
        var ps = new FwDict() { { "success", true } };

        var sortdir = reqs("sortdir");
        var id = reqi("id");
        var under_id = reqi("under");
        var above_id = reqi("above");

        ps["success"] = model0.reorderPrio(sortdir, id, under_id, above_id);

        return new FwDict() { { "_json", ps } };
    }
    #endregion

    #region Att Files support

    // upload one or many files to the Att storage and link to the current entity and id
    // json only response
    public virtual FwDict SaveAttFilesAction(int id)
    {
        var item = reqh("item");

        // validation
        if (id == 0)
            throw new UserException("Invalid ID");

        var files = fw.request?.Form?.Files;
        if (files == null || files.Count == 0 || files[0] == null || files[0].Length == 0)
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


    #endregion

    #region HELPERS for dynamic fields

    /// <summary>
    /// return config for show/showform fields by tab
    /// </summary>
    /// <param name="prefix">show_fields or showform_fields</param>
    /// <param name="tab">optional tab code, if ommited - form_tab used</param>
    /// <returns></returns>
    protected virtual FwList getConfigShowFormFieldsByTab(string prefix, string? tab = null)
    {
        tab ??= form_tab;
        var key = prefix + (tab.Length > 0 ? "_" + tab : "");

        if (config[key] is IList arr)
            return new FwList(arr);

        return [];
    }

    /// <summary>
    /// prepare data for fields repeat in ShowAction based on config.json show_fields parameter
    /// </summary>
    /// <param name="item"></param>
    /// <param name="ps"></param>
    /// <returns></returns>
    public virtual FwList prepareShowFields(FwDict item, FwDict ps)
    {
        var id = item[model0.field_id].toInt();

        FwList fields = getConfigShowFormFieldsByTab("show_fields");
        foreach (FwDict def in fields)
        {
            def["i"] = item; // ref to item
            string dtype = def["type"].toStr();
            string field = def["field"].toStr();

            if (DEF_TYPES_STRUCTURE.Contains(dtype))
                // structural tags
                def["is_structure"] = true;
            else if (dtype == "multi")
            {
                if (def.TryGetValue("lookup_model", out object? value))
                    def["multi_datarow"] = fw.model(value.toStr()).listWithChecked(item[field].toStr(), def);
                else
                {
                    if (def["is_by_linked"].toBool())
                        // list main items by linked id from junction model (i.e. list of Users(with checked) for Company from UsersCompanies model)
                        def["multi_datarow"] = fw.model(def["model"].toStr()).listMainByLinkedId(id, def); //junction model
                    else
                        // list linked items by main id from junction model (i.e. list of Companies(with checked) for User from UsersCompanies model)
                        def["multi_datarow"] = fw.model(def["model"].toStr()).listLinkedByMainId(id, def); //junction model
                }

            }
            else if (dtype == "multi_prio")
                // complex field with prio
                def["multi_datarow"] = fw.model(def["model"].toStr()).listLinkedByMainId(id, def); //junction model
            else if (dtype == "att")
                def["att"] = fw.model<Att>().one(item[field]);
            else if (dtype == "att_links")
                def["att_links"] = fw.model<Att>().listLinked(model0.table_name, id);
            else if (dtype == "att_files")
                def["att_files"] = fw.model<Att>().listByEntityCategory(model0.table_name, id, def["att_category"].toStr());

            else if (dtype == "subtable")
            {
                // subtable functionality
                var sub_model = fw.model(def["model"].toStr());
                var sub_list_rows = sub_model.listByMainId(id, def); //list related rows from db
                sub_model.prepareSubtable(sub_list_rows, id, def);

                def["list_rows"] = sub_list_rows;
            }
            else
            {
                // single values
                // lookups
                if (def.TryGetValue("lookup_table", out object? value))
                {
                    string lookup_key = def["lookup_key"].toStr();
                    if (lookup_key == "")
                        lookup_key = "id";

                    string lookup_field = def["lookup_field"].toStr();
                    if (lookup_field == "")
                        lookup_field = "iname";

                    var lookup_row = db.row(value.toStr(), DB.h(lookup_key, item[field]));
                    def["lookup_row"] = lookup_row;
                    def["value"] = lookup_row[lookup_field];
                }
                else if (def.TryGetValue("lookup_model", out object? lm_value))
                {
                    var lookup_model = fw.model(value.toStr());
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
                else if (def.TryGetValue("lookup_tpl", out object? lt_value))
                    def["value"] = FormUtils.selectTplName(lt_value.toStr(), item[field].toStr(), fw.route.controller_path.ToLower());
                else if (def.ContainsKey("options"))
                {
                    // select options
                    var itemValue = item[field].toStr();
                    if (def["options"] is FwDict options && options.TryGetValue(itemValue, out object? o_value))
                        def["value"] = o_value;
                    else
                        def["value"] = "";
                }
                else
                    def["value"] = item[field];

                // convertors
                if (def.ContainsKey("conv"))
                {
                    if (def["conv"].toStr() == "time_from_seconds")
                        def["value"] = FormUtils.intToTimeStr(def["value"].toInt());
                }

                // special handling for date combo: prepare select defaults
                if (dtype == "date_combo")
                {
                    // fill day/mon/year into item and def selects
                    if (FormUtils.comboForDate(def["value"].toStr(), item, field))
                    {
                        def["select_day"] = item[$"{field}_day"]; // selected day
                        def["select_mon"] = item[$"{field}_mon"]; // selected month
                        def["select_year"] = item[$"{field}_year"]; // selected year
                    }
                }
            }
        }
        return fields;
    }

    public virtual FwList prepareShowFormFields(FwDict item, FwDict ps)
    {
        var id = item[model0.field_id].toInt();

        var fields = getConfigShowFormFieldsByTab("showform_fields") ?? throw new ApplicationException("Controller config.json doesn't contain 'showform_fields'");

        // build index by field if necessary
        FwDict hfields = [];
        var is_get_existing = false;
        if (isGet() && !Utils.isEmpty(id))
        {
            is_get_existing = true;
            hfields = Utils.array2hashtable(fields, "field");
        }

        foreach (FwDict def in fields)
        {
            //logger(def);
            def["i"] = item; // ref to item
            def["ps"] = ps; // ref to whole ps
            string dtype = def["type"].toStr(); // type is required
            string field = def["field"].toStr();

            // for just loaded forms for existing items - pre-load filter's values into "item"
            if (is_get_existing && def.TryGetValue("filter_for", out object? value))
            {
                var filter_for_field = value.toStr();
                var filter_field = def["filter_field"].toStr();

                if (!string.IsNullOrEmpty(filter_for_field) && hfields[filter_for_field] is FwDict def_for)
                {
                    var defFieldName = def_for["field"].toStr();
                    var filtered_item = fw.model(def_for["lookup_model"].toStr()).one(item[defFieldName]);
                    item[field] = filtered_item?[filter_field];
                }
            }

            if (def.ContainsKey("append") && def["append"] is ICollection coll1 && coll1.Count > 0
                || def.ContainsKey("prepend") && def["prepend"] is ICollection coll2 && coll2.Count > 0)
                def["is_input_group"] = true;

            if (id == 0 && (dtype == "added" || dtype == "updated"))
                // special case - hide if new item screen
                def["class"] = "d-none";

            if (DEF_TYPES_STRUCTURE.Contains(dtype))
                // structural tags
                def["is_structure"] = true;
            else if (dtype == "multicb")
            {
                FwList multi_datarow;
                if (def.TryGetValue("lookup_model", out object? lm_value))
                    multi_datarow = fw.model(lm_value.toStr()).listWithChecked(item[field].toStr(), def);
                else
                {
                    if (def["is_by_linked"].toBool())
                        // list main items by linked id from junction model (i.e. list of Users(with checked) for Company from UsersCompanies model)
                        multi_datarow = fw.model(def["model"].toStr()).listMainByLinkedId(id, def); //junction model
                    else
                        // list linked items by main id from junction model (i.e. list of Companies(with checked) for User from UsersCompanies model)
                        multi_datarow = fw.model(def["model"].toStr()).listLinkedByMainId(id, def); //junction model
                }

                foreach (FwDict row in multi_datarow) // contains id, iname, is_checked
                    row["field"] = def["field"];

                def["multi_datarow"] = multi_datarow;
            }
            else if (dtype == "multicb_prio")
            {
                var multi_datarow = fw.model(def["model"].toStr()).listLinkedByMainId(id, def); // junction model

                foreach (FwDict row in multi_datarow) // contains id, iname, is_checked, _link[prio]
                    row["field"] = def["field"];

                def["multi_datarow"] = multi_datarow;
            }
            else if (dtype == "att_edit")
            {
                def["att"] = fw.model<Att>().one(item[field]);
                def["value"] = item[field];
            }
            else if (dtype == "att_links_edit")
                def["att_links"] = fw.model<Att>().listLinked(model0.table_name, id);

            else if (dtype == "att_files_edit")
            {
                if (!def.ContainsKey("att_upload_url"))
                    def["att_upload_url"] = this.base_url + "/(SaveAttFiles)/" + id;
                if (!def.ContainsKey("att_post_prefix"))
                    def["att_post_prefix"] = field;

                def["att_files"] = fw.model<Att>().listByEntityCategory(model0.table_name, id, def["att_category"].toStr());
            }

            else if (dtype == "subtable_edit")
            {
                prepareShowFormSubtable(id, item, def);
            }
            else
            {
                // single values
                // lookups
                if (def.TryGetValue("lookup_table", out object? lt_value))
                {
                    string lookup_key = def["lookup_key"].toStr();
                    if (lookup_key == "")
                        lookup_key = "id";

                    string lookup_field = def["lookup_field"].toStr();
                    if (lookup_field == "")
                        lookup_field = "iname";

                    var lookup_row = db.row(lt_value.toStr(), DB.h(lookup_key, item[field]));
                    def["lookup_row"] = lookup_row;
                    def["value"] = lookup_row[lookup_field];
                }
                else if (def.TryGetValue("lookup_model", out object? lm_value))
                {
                    var lookup_model = fw.model(lm_value.toStr());

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
                            if (def["lookup_by_value"].toBool())
                            {
                                //if lookup by value - use value itself, not as id
                                def["value"] = item[field];
                            }
                            else
                            {
                                def["lookup_id"] = item[field].toInt();
                                var lookup_row = lookup_model.one(def["lookup_id"]);
                                def["lookup_row"] = lookup_row;

                                string lookup_field = def["lookup_field"].toStr();
                                if (lookup_field == "")
                                    lookup_field = lookup_model.field_iname;

                                def["value"] = lookup_row[lookup_field];
                            }
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
                else if (def.TryGetValue("lookup_tpl", out object? lt_value2))
                {
                    var select_options = FormUtils.selectTplOptions(lt_value.toStr(), fw.route.controller_path.ToLower());
                    def["select_options"] = select_options;
                    def["value"] = item[field];
                    foreach (FwDict row in select_options) // contains id, iname
                    {
                        row["is_inline"] = def["is_inline"];
                        row["field"] = def["field"];
                        row["value"] = item[field];
                    }
                }
                else if (def.TryGetValue("options", out object? o_value))
                {
                    //select options as array - convert to arraylist of id => iname
                    var options = o_value as FwDict ?? [];
                    var select_options = new FwList();
                    foreach (var entry in options)
                        select_options.Add(new FwDict() {
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
                    if (def["conv"].toStr() == "time_from_seconds")
                        def["value"] = FormUtils.intToTimeStr(def["value"].toInt());
                }
            }

        }
        return fields;
    }

    /// <summary>
    /// Prepares the data and structure for displaying and editing a subtable in a form.
    /// </summary>
    /// <remarks>This method handles both GET and POST requests. For GET requests, it retrieves the existing
    /// subtable rows from the database or initializes new rows if none exist. For POST requests, it processes
    /// user-submitted data, including adding new rows, deleting rows, and updating existing rows. The prepared rows are
    /// stored in the <c>def["list_rows"]</c> key for rendering in the form.</remarks>
    /// <param name="id">The identifier of the main record associated with the subtable. A value greater than 0 indicates an existing
    /// record; otherwise, a new record is assumed.</param>
    /// <param name="item">current form data, including any user-submitted values.</param>
    /// <param name="def">field definition</param>
    protected virtual void prepareShowFormSubtable(int id, FwDict item, FwDict def)
    {
        var subtable_add = reqh("subtable_add");
        var subtable_del = reqh("subtable_del");

        string field = def["field"].toStr();

        var sub_model = fw.model(def["model"].toStr());
        var sub_list_rows = new FwList();

        if (isGet())
        {
            if (id > 0)
                sub_list_rows = sub_model.listByMainId(id, def); //list related rows from db
            else
                sub_model.prepareSubtableAddNew(sub_list_rows, id, def); //add at least one row
        }
        else
        {
            //check if we deleted specific row
            var del_id = subtable_del[field].toStr();

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

                sub_list_rows.Add(row_item);
            }
        }

        //delete row clicked
        //if (subtable_del.ContainsKey(field))
        //{
        //    var del_id = subtable_del[field].toInt();
        //    // delete with LINQ from the form list (actual delete from db will be on save)
        //    list_rows = new FwList((from FwRow d in list_rows
        //                               where d["id"].toInt() != del_id
        //                               select d).ToList());
        //}

        //add new clicked
        if (subtable_add.ContainsKey(field))
            sub_model.prepareSubtableAddNew(sub_list_rows, id, def);

        //prepare rows for display (add selects, etc..)
        sub_model.prepareSubtable(sub_list_rows, id, def);

        def["list_rows"] = sub_list_rows;
    }

    // auto-process fields BEFORE record saved to db
    protected virtual void processSaveShowFormFields(int id, FwDict fields)
    {
        FwDict item = reqh("item");

        var showform_fields = _fieldsToHash(getConfigShowFormFieldsByTab("showform_fields"));

        // special auto-processing for fields of particular types - use .Cast<string>().ToArray() to make a copy of keys as we modify fields
        foreach (string field in fields.Keys.Cast<string>().ToArray())
        {
            if (!showform_fields.ContainsKey(field))
                continue;

            var def = (FwDict)showform_fields[field]!;
            string type = def["type"].toStr();
            if (type == "autocomplete")
            {
                var lookup_model = fw.model(def["lookup_model"].toStr());
                var field_value = item[field + "_iname"].toStr(); // autocomplete value is in "${field}_iname"
                if (def["lookup_by_value"].toBool())
                    fields[field] = field_value; // just by value, not by id
                else
                    fields[field] = lookup_model.findOrAddByIname(field_value, out _);
            }
            else if (type == "time")
                fields[field] = FormUtils.timeStrToInt(fields[field].toStr()); // ftime - convert from HH:MM to int (0-24h in seconds)
            else if (type == "number")
            {
                // no need to do this as DB knows if field nullable and convert empty string to NULL
                //if (Utils.qh(save_fields_nullable).ContainsKey(field) && string.IsNullOrEmpty(fields[field].toStr()))
                //    // if field nullable and empty - pass NULL
                //    fields[field] = null;
                //else
                //    fields[field] = fields[field].toFloat();// number - convert to number (if field empty or non-number - it will become 0)
            }
        }
    }

    // auto-process fields AFTER record saved to db
    protected virtual void processSaveShowFormFieldsAfter(int id, FwDict fields)
    {
        var fields_update = new FwDict();

        // for now we just look if we have att_links_edit field and update att links
        foreach (FwDict def in getConfigShowFormFieldsByTab("showform_fields"))
        {
            string field = def["field"].toStr();
            string type = def["type"].toStr();
            if (type == "att_links_edit")
            {
                var att_post_prefix = "att";
                if (def.TryGetValue("att_post_prefix", out object? value))
                    att_post_prefix = value.toStr();
                // if PATCH - only update is post param is present (otherwise it will delete all records)
                if (isPatch() && req(att_post_prefix) == null)
                    continue;

                fw.model<AttLinks>().updateJunction(model0.table_name, id, reqh(att_post_prefix));
            }
            else if (type == "att_files_edit")
            {
                processSaveAttFiles(id, fields, def);
            }
            else if (type == "multicb")
            {
                processSaveMultiCb(id, fields, def, ref fields_update);
            }
            else if (type == "multicb_prio")
            {
                processSaveMultiCb(id, fields, def, ref fields_update);
            }
            else if (type == "subtable_edit")
            {
                processSaveSubtable(id, fields, def);
            }
            else if (type == "date_combo")
            {
                fields_update[field] = FormUtils.dateForCombo(reqh("item"), field);
            }
        }

        if (fields_update.Count > 0)
        {
            model0.update(id, fields_update);
        }
    }

    /// <summary>
    /// Processes and updates attachment files associated with a specific entity and category.
    /// </summary>
    /// <remarks>Attachments not included in the provided list are deleted from the specified
    /// category. If the operation is a PATCH request and the relevant attachment field is not present in the
    /// request, no changes are made. </remarks>
    /// <param name="id">The unique identifier of the entity to which the attachments belong.</param>
    /// <param name="fields">posted and saved main item data (use in overrides as needed)</param>
    /// <param name="def">field definition</param>
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
            var rowId = row["id"].toStr();
            if (rowId.Length > 0 && !att_ids.ContainsKey(rowId))
                att_model.delete(rowId.toInt(), true);
        }
    }

    protected virtual void processSaveMultiCb(int id, FwDict fields, FwDict def, ref FwDict fields_update)
    {
        var field = def["field"].toStr();

        // if PATCH - only update is post param is present (otherwise it will delete all records)
        if (isPatch() && req(field + "_multi") == null)
            return;

        if (Utils.isEmpty(def["model"]))
        {
            // multiple checkboxes -> non-junction model single comma-delimited field                    
            fields_update[field] = FormUtils.multi2ids(reqh(field + "_multi"));
        }
        else
        {
            //junction model based
            if (def["is_by_linked"].toBool())
                //by linked id
                fw.model(def["model"].toStr()).updateJunctionByLinkedId(id, reqh(field + "_multi")); // junction model
            else
                //by main id
                fw.model(def["model"].toStr()).updateJunctionByMainId(id, reqh(field + "_multi")); // junction model
        }
    }

    /// <summary>
    /// Processes and saves data for a subtable associated with a main record.
    /// </summary>
    /// <remarks>This method handles the creation, update, and deletion of subtable rows based on the provided
    /// input.  It ensures that rows are marked as active or deleted as appropriate and processes fields according to
    /// the specified subtable definition.  If the HTTP request is a PATCH operation and no subtable data is provided
    /// for the specified field, the method exits without making changes.</remarks>
    /// <param name="id">The identifier of the main record to which the subtable is related.</param>
    /// <param name="fields">posted and saved main item data (use in overrides as needed)</param>
    /// <param name="def">field definition</param>
    protected virtual void processSaveSubtable(int id, FwDict fields, FwDict def)
    {
        var subtable_del = reqh("subtable_del");

        var field = def["field"].toStr();

        // if PATCH - only update is post param is present (otherwise it will delete all records)
        if (isPatch() && req("item-" + field) == null)
            return;

        var sub_model = fw.model(def["model"].toStr());

        var save_fields = def["save_fields"].toStr();
        var save_fields_checkboxes = def["save_fields_checkboxes"].toStr();

        //check if we delete specific row
        var del_id = subtable_del[field].toStr();

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
            FwDict itemdb = FormUtils.filter(row_item, save_fields);
            FormUtils.filterCheckboxes(itemdb, row_item, save_fields_checkboxes, isPatch());

            itemdb[junction_field_status] = FwModel.STATUS_ACTIVE; // mark new and updated existing rows as active

            modelAddOrUpdateSubtableDynamic(id, row_id, itemdb, def, sub_model);
        }

        //remove any not updated rows (i.e. those deleted by user)
        sub_model.deleteUnderUpdateByMainId(id);
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
    protected virtual int modelAddOrUpdateSubtableDynamic(int main_id, string row_id, FwDict fields, FwDict def, FwModel? sub_model = null)
    {
        int id;

        if (sub_model == null)
        {
            var model_name = def["model"].toStr();
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
    protected FwDict? defByFieldname(string field_name, FwList fields)
    {
        foreach (FwDict def in fields)
        {
            if (def["field"].toStr() == field_name)
                return def;
        }
        return null;
    }

    // convert config's fields list into hashtable as field => {}
    // if there are more than one field - just first field added to the hash
    protected FwDict _fieldsToHash(FwList fields)
    {
        FwDict result = [];
        foreach (FwDict fldinfo in fields)
        {
            if (fldinfo.TryGetValue("field", out object? value) && !result.ContainsKey(value.toStr()))
                result[value.toStr()] = fldinfo;
        }
        return result;
    }

    #endregion
}
