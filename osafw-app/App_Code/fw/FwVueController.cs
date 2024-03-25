// Fw Vue controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace osafw;

public class FwVueController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected FwModel model_related;

    public override void init(FW fw)
    {
        base.init(fw);
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_VUE"); // layout for Vue pages
    }

    /// <summary>
    /// basically return layout/js to the browser, then Vue will load data via API
    /// </summary>
    /// <returns>Hashtable - related template will be parsed, null - no templates parsed (if action did all the output)</returns>
    public virtual Hashtable IndexAction()
    {
        var scope = reqs("scope");
        var scopes = scope.Length > 0 ? Utils.commastr2hash(scope, "1") : [];
        if (export_format.Length > 0)
            scopes["list_rows"] = "1";

        // get filters from the search form
        initFilter();

        // set standard output - load html with Vue app
        Hashtable ps = [];

        if (fw.isJsonExpected())
        {
            // if json expected - return data only as json
            ps["_json"] = true;
            Hashtable hfields = _fieldsToHash((ArrayList)this.config["showform_fields"]);

            //do db work only if json or export expected
            if (scopes.Count == 0 || scopes.ContainsKey("list_rows"))
            {
                setListSorting();

                setListSearch();
                setListSearchStatus();

                setViewList(ps, list_filter_search, false);

                //only select from db visible fields + id, save as comma-separated string into list_fields
                setListFields(ps);
                ps.Remove("headers_search"); // TODO refactor to not include in ps at all even for ParsePage controllers
                var headers = (ArrayList)ps["headers"];

                getListRows();

                // if export - no need to parse templates and prep for them - just return empty hashtable asap
                if (export_format.Length > 0)
                    return []; // return empty hashtable just in case action overriden to avoid check for null


                //TODO filter rows for json output


                ps["XSS"] = fw.Session("XSS");
                ps["access_level"] = fw.userAccessLevel;
                //TODO only specific from global ps["global"] = fw.G;            

                //editable list support - read from config                
                //add to headers data for editable list: is_ro, input_type, lookup_model, lookup_tpl
                var editable_types = Utils.qh("input email number textarea date_popup datetime_popup autocomplete select cb radio yesno");
                foreach (Hashtable header in headers)
                {
                    var field_name = (string)header["field_name"];
                    var def = (Hashtable)hfields[field_name] ?? null;
                    if (def == null)
                        continue;

                    var def_type = Utils.f2str(def["type"]);
                    header["input_type"] = def_type;
                    if (!editable_types.ContainsKey(def_type))
                        header["is_ro"] = true; // TODO make ability to override in controller as some edit type fields might not be editable due to access level or other conditions

                    var lookup_model = Utils.f2str(def["lookup_model"]);
                    if (lookup_model.Length > 0)
                        header["lookup_model"] = lookup_model;

                    var lookup_tpl = Utils.f2str(def["lookup_tpl"]);
                    if (lookup_tpl.Length > 0)
                        header["lookup_tpl"] = lookup_tpl;
                }
            }

            if (scopes.Count == 0 || scopes.ContainsKey("lookups"))
            {
                // userlists support if necessary
                if (this.is_userlists)
                    this.setUserLists(ps);

                // extract lookups from config and add to ps
                var lookups = new Hashtable();
                var headers = (ArrayList)ps["headers"];
                foreach (Hashtable header in headers)
                {
                    var field_name = (string)header["field_name"];
                    var def = (Hashtable)hfields[field_name] ?? null;
                    if (def == null)
                        continue;

                    var lookup_model = Utils.f2str(def["lookup_model"]);
                    if (lookup_model.Length > 0)
                    {
                        lookups[lookup_model] = fw.model(lookup_model).listSelectOptions();
                    }

                    var lookup_tpl = Utils.f2str(def["lookup_tpl"]);
                    if (lookup_tpl.Length > 0)
                    {
                        lookups[lookup_tpl] = FormUtils.selectTplOptions(lookup_tpl);
                    }
                }

                ps["lookups"] = lookups;
            }
        }

        ps = setPS(ps);

        return ps;
    }

    public virtual Hashtable SaveAction(int id = 0)
    {
        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");
        if (reqi("refresh") == 1)
            throw new Exception("Wrong use refresh=1 on Vue Controller");

        fw.model<Users>().checkReadOnly();

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

    protected virtual bool validateRequiredDynamic(int id, Hashtable item)
    {
        var result = true;
        if (string.IsNullOrEmpty(this.required_fields) && is_dynamic_showform)
        {
            // if required_fields not defined - fill from showform_fields
            ArrayList fields = (ArrayList)this.config["showform_fields"];
            ArrayList req = [];
            foreach (Hashtable def in fields)
            {
                if (Utils.f2bool(def["required"]))
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

        ArrayList fields = (ArrayList)this.config["showform_fields"];
        foreach (Hashtable def in fields)
        {
            string field = (string)def["field"];
            if (string.IsNullOrEmpty(field))
                continue;

            string type = (string)def["type"];

            if (type == "subtable_edit")
            {
                //validate subtable rows
                var model_name = (string)def["model"];
                var sub_model = fw.model(model_name);

                var save_fields = (string)def["required_fields"] ?? "";
                var save_fields_checkboxes = (string)def["save_fields_checkboxes"];

                //check if we delete specific row
                var del_id = (string)subtable_del[model_name] ?? "";

                // row ids submitted as: item-<~model>[<~id>]
                // input name format: item-<~model>#<~id>[field_name]
                var hids = reqh("item-" + model_name);
                // sort hids.Keys, so numerical keys - first and keys staring with "new-" will be last
                var sorted_keys = hids.Keys.Cast<string>().OrderBy(x => x.StartsWith("new-") ? 1 : 0).ThenBy(x => x).ToList();
                foreach (string row_id in sorted_keys)
                {
                    if (row_id == del_id) continue; //skip deleted row

                    var row_item = reqh("item-" + model_name + "#" + row_id);
                    Hashtable itemdb = FormUtils.filter(row_item, save_fields);
                    FormUtils.filterCheckboxes(itemdb, row_item, save_fields_checkboxes, isPatch());

                    if (row_id.StartsWith("new-"))
                        itemdb[sub_model.junction_field_main_id] = id;

                    //VAILIDATE itemdb
                    //TODO VUE var is_valid = validateSubtableRowDynamic(row_id, itemdb, def);
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

                    string field_value = Utils.f2str(item[field]);

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
            }

        }
        return result;
    }

    //TODO refactor with FwDynamicController same method to deduplicate code
    public virtual Hashtable SaveUserViewsAction()
    {
        var fld = reqh("fld");
        var load_id = reqi("load_id");
        var is_reset = reqi("is_reset");
        var density = reqs("density");

        if (load_id > 0)
            // set fields from specific view
            fw.model<UserViews>().setViewForIcode(base_url, load_id);
        else if (is_reset == 1)
            // reset fields to defaults
            fw.model<UserViews>().updateByIcodeFields(base_url, view_list_defaults);
        else if (density.Length > 0)
        {
            // save density
            // validate density can be only table-sm, table-dense, table-normal, otherwise - set empty
            if (!"table-sm table-dense table-normal".Contains(density))
                density = "";
            fw.model<UserViews>().updateByIcode(base_url, DB.h("density", density));
        }
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
            var fields = string.Join(" ", anames);

            if (!string.IsNullOrEmpty(iname))
            {
                // create new view by name or update if this name exists
                fw.model<UserViews>().addOrUpdateByUK(base_url, fields, iname);
            }
            // update default view with fields
            fw.model<UserViews>().updateByIcodeFields(base_url, fields);
        }

        return afterSave(true, null, false, "no_action", return_url);
    }

    // TODO refactor with FwDynamicController 
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

    protected void setListFields(Hashtable ps)
    {
        //TODO have headers as controller class property
        var headers = (ArrayList)ps["headers"]; //arraylist of hashtables, we need header["field_name"]
        var quoted_fields = new ArrayList();
        var is_id_in_fields = false;
        foreach (Hashtable header in headers)
        {
            var field_name = (string)header["field_name"];
            quoted_fields.Add(db.qid(field_name));
            if (field_name == model0.field_id)
                is_id_in_fields = true;
        }
        //always include id field
        if (!is_id_in_fields && !Utils.isEmpty(model0.field_id))
            quoted_fields.Add(db.qid(model0.field_id));
        //join quoted_fields arraylist into comma-separated string
        list_fields = string.Join(",", quoted_fields.ToArray());
    }

}
