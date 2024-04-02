// Fw Vue controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class FwVueController : FwDynamicController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    public override void init(FW fw)
    {
        base.init(fw);
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_VUE"); // layout for Vue pages
    }

    /// <summary>
    /// set list fields for db select, based on user-selected headers from config
    /// so we fetch from db only fields that are visible in the list + id field
    /// </summary>
    /// <param name="ps"></param>
    protected virtual void setListFields(Hashtable ps)
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

    /// <summary>
    /// basically return layout/js to the browser, then Vue will load data via API
    /// </summary>
    /// <returns>Hashtable - related template will be parsed, null - no templates parsed (if action did all the output)</returns>
    public override Hashtable IndexAction()
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


                //filter rows for json output - TODO make filterListForJson controller method
                foreach (Hashtable row in list_rows)
                {
                    model0.filterForJson(row);
                }


                ps["XSS"] = fw.Session("XSS");
                ps["access_level"] = fw.userAccessLevel;
                //some specific from global fw.G;            
                var global = new Hashtable();
                foreach (var key in Utils.qw("is_list_btn_left"))
                {
                    global[key] = fw.G[key];
                }
                ps["global"] = global;

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

                    //add to headers, if exists in def: maxlength, min, max, step, placeholder, pattern, required, readonly, disabled
                    foreach (string attr in Utils.qw("is_option0 is_option_empty maxlength min max step placeholder pattern required readonly disabled"))
                    {
                        if (def.ContainsKey(attr))
                            header[attr] = def[attr];
                    }
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

                //TODO TBD :
                // - return as is or filter out something?
                // - return for lookups scope or separate scope?
                // - Also if we return showform_fields to frontend, enrich headers on frontend side?
                ps["showform_fields"] = (ArrayList)this.config["showform_fields"];
            }
        }

        ps = setPS(ps);

        return ps;
    }

    public override void NextAction(string form_id)
    {
        throw new NotImplementedException(); // N/A for Vue controllers
    }

    public override Hashtable ShowAction(int id = 0)
    {
        Hashtable ps = [];
        Hashtable item = model0.one(id);
        if (item.Count == 0)
            throw new NotFoundException();

        // added/updated should be filled before dynamic fields
        setAddUpdUser(ps, item);

        model0.filterForJson(item);

        ps["id"] = id;
        ps["i"] = item;
        ps["_json"] = true;
        return ps;
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        throw new NotImplementedException(); // N/A for Vue controllers
    }

    public override Hashtable SaveAction(int id = 0)
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

    public override void ShowDeleteAction(int id)
    {
        throw new NotImplementedException(); // N/A for Vue controllers
    }

}
