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

    // list of keys from fw.G to pass to Vue
    protected string global_keys = "ROOT_URL is_list_btn_left";

    public override void init(FW fw)
    {
        base.init(fw);
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_VUE"); // layout for Vue pages
    }

    /// <summary>
    /// set list fields for db select, based on user-selected headers in list_headers
    /// so we fetch from db only fields that are visible in the list + id field
    /// </summary>
    protected override void setListFields()
    {
        var quoted_fields = new ArrayList();
        var is_id_in_fields = false;
        foreach (Hashtable header in list_headers)
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
    /// filter list rows for json output using model's filterForJson
    /// </summary>
    protected virtual void filterListForJson()
    {
        foreach (Hashtable row in list_rows)
        {
            model0.filterForJson(row);
        }
    }

    /// <summary>
    /// set data for initial scope for Vue controller
    /// </summary>
    /// <param name="ps"></param>
    protected virtual void setScopeInitial(Hashtable ps)
    {
        ps["XSS"] = fw.Session("XSS");
        ps["access_level"] = fw.userAccessLevel;
        ps["me_id"] = fw.userId;
        //some specific from global fw.G;            
        var global = new Hashtable();
        foreach (var key in Utils.qw(global_keys))
        {
            global[key] = fw.G[key];
        }
        ps["global"] = global;

        setViewList(false); // initialize list_headers and related

        // userviews customization support
        ps["all_list_columns"] = getViewListArr(getViewListUserFields(), true); // list all fields
        ps["select_userviews"] = fw.model<UserViews>().listSelectByIcode(base_url);

        //return editable fields definitions
        ArrayList showform_fields = (ArrayList)this.config["showform_fields"];
        ps["showform_fields"] = showform_fields;

        ps["list_user_view"] = this.list_user_view;
        ps["list_headers"] = this.list_headers;

        // other static params
        ps["related_id"] = this.related_id;
        ps["base_url"] = this.base_url;
        ps["is_userlists"] = this.is_userlists;
        ps["is_readonly"] = is_readonly;
        ps["is_list_edit"] = is_list_edit;
        ps["return_url"] = this.return_url;
    }

    /// <summary>
    /// set data for list_rows scope for Vue controller
    /// </summary>
    /// <param name="ps"></param>
    protected virtual void setScopeListRows(Hashtable ps)
    {
        setListSorting();

        setListSearch();
        setListSearchStatus();

        if (list_headers == null)
            setViewList(false); // initialize list_headers and related (can already be initialized in setScopeInitial)

        //only select from db visible fields + id, save as comma-separated string into list_fields
        setListFields();

        getListRows();
        filterListForJson();

        // if export - no need further processing - just return asap
        if (export_format.Length > 0)
            return;

        ps["list_rows"] = this.list_rows;
        ps["count"] = this.list_count;
        ps["pager"] = this.list_pager;
    }

    /// <summary>
    /// set data for lookups scope for Vue controller
    /// </summary>
    /// <param name="ps"></param>
    protected virtual void setScopeLookups(Hashtable ps)
    {
        // userlists support if necessary
        if (this.is_userlists)
            this.setUserLists(ps);

        if (list_headers == null)
            setViewList(false); // initialize list_headers and related (can already be initialized in setScopeInitial)

        ArrayList showform_fields = (ArrayList)this.config["showform_fields"];
        Hashtable hfields = _fieldsToHash(showform_fields);

        // extract lookups from config and add to ps
        var lookups = new Hashtable();
        foreach (Hashtable header in list_headers)
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

            if (scopes.Count == 0)
            {
                setScopeInitial(ps);
            }

            // prepare data for list_rows scope
            if (scopes.Count == 0 || scopes.ContainsKey("list_rows"))
            {
                setScopeListRows(ps);

                if (export_format.Length > 0)
                    return []; // return empty hashtable just in case action overriden to avoid check for null
            }

            // prepare data for lookups scope
            if (scopes.Count == 0 || scopes.ContainsKey("lookups"))
            {
                setScopeLookups(ps);
            }
        }
        // else - this is initial non-json page load - return layout/js to the browser, then Vue will load data via API

        ps["f"] = this.list_filter;

        return ps;
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

    // override/disable actions not used in Vue
    public override void NextAction(string form_id)
    {
        throw new NotImplementedException(); // N/A for Vue controllers
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        throw new NotImplementedException(); // N/A for Vue controllers
    }

    public override void ShowDeleteAction(int id)
    {
        throw new NotImplementedException(); // N/A for Vue controllers
    }

}
