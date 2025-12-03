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
    protected string global_keys = "ROOT_URL is_list_btn_left date_format time_format timezone";

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
        //extract autocomplete fields
        var ac_fields = new ArrayList();
        var fields = (ArrayList)this.config["showform_fields"];
        foreach (Hashtable def in fields)
        {
            //var field_name = def["field"].toStr();
            //var model_name = def["lookup_model"].toStr();
            var dtype = def["type"].toStr();
            if (dtype == "autocomplete" || dtype == "plaintext_autocomplete")
            {
                ac_fields.Add(def);
            }
        }

        foreach (Hashtable row in list_rows)
        {
            model0.filterForJson(row);

            //added/updated username - it's readonly so we can replace _id fields with names
            if (!string.IsNullOrEmpty(model0.field_add_users_id) && row.ContainsKey(model0.field_add_users_id))
                row["add_users_id"] = fw.model<Users>().iname(row[model0.field_add_users_id]);
            if (!string.IsNullOrEmpty(model0.field_upd_users_id) && row.ContainsKey(model0.field_upd_users_id))
                row["upd_users_id"] = fw.model<Users>().iname(row[model0.field_upd_users_id]);

            //autocomplete fields - add _iname fields
            foreach (Hashtable def in ac_fields)
            {
                var field_name = def["field"].toStr();
                var model_name = def["lookup_model"].toStr();
                var dtype = def["type"].toStr();
                if (dtype == "autocomplete" || dtype == "plaintext_autocomplete")
                {
                    if (def["lookup_by_value"].toBool())
                    {
                        // Use the value itself
                        row[field_name + "_iname"] = row[field_name];
                    }
                    else
                    {
                        var ac_model = fw.model(model_name);
                        if (ac_model != null)
                        {
                            var ac_item = ac_model.one(row[field_name]);
                            row[field_name + "_iname"] = ac_item["iname"];
                        }
                    }
                }
            }
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
        global["user_iname"] = fw.model<Users>().iname(fw.userId); 
        ps["global"] = global;

        setViewList(false); // initialize list_headers and related

        // userviews customization support
        ps["all_list_columns"] = getViewListArr(getViewListUserFields(), true); // list all fields
        ps["select_userviews"] = fw.model<UserViews>().listSelectByIcode(UserViews.icodeByUrl(base_url, is_list_edit));

        ps["field_id"] = model0.field_id;
        ps["view_list_custom"] = Utils.qh(this.view_list_custom, "1");

        //return view form definitions
        ps["show_fields"] = this.config["show_fields"];
        //return editable fields definitions
        ps["showform_fields"] = this.config["showform_fields"];

        ps["list_user_view"] = this.list_user_view;
        ps["list_headers"] = this.list_headers;

        // other static params
        ps["related_id"] = this.related_id;
        ps["base_url"] = this.base_url;
        ps["is_userlists"] = this.is_userlists;
        ps["is_readonly"] = is_readonly;
        ps["is_list_edit"] = is_list_edit;
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
        //Hashtable hfields = _fieldsToHash(showform_fields);

        // extract lookups from config and add to ps
        var lookups = new Hashtable();
        foreach (Hashtable def in showform_fields)
        {
            if (def == null)
                continue;

            var dtype = def["type"].toStr();
            var lookup_model = def["lookup_model"].toStr();
            if (lookup_model.Length > 0 && dtype != "autocomplete")
            {
                //all lookup_models, except autocomplete (for those it could be too large)
                lookups[lookup_model] = fw.model(lookup_model).listSelectOptions(def);
            }

            var lookup_tpl = def["lookup_tpl"].toStr();
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

            if (scopes.Count == 0 || scopes.ContainsKey("init"))
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
        else
        {
            // if it's export - just get list_rows scope and return
            if (export_format.Length > 0)
            {
                setScopeListRows(ps);

                return ps;
            }

            // else - this is initial non-json page load - return layout/js to the browser, then Vue will load data via API
            // if url is /ID or /ID/edit or /new - add screen, id to ps so Vue app will switch to related screen
            var route = fw.getRoute(fw.request.Path);
            if (route.action == FW.ACTION_SHOW_FORM)
            {
                ps["screen"] = "edit";
                ps["id"] = route.id;
            }
            else if (route.action == FW.ACTION_SHOW_FORM_NEW)
            {
                ps["screen"] = "edit";
            }
            else if (route.action == FW.ACTION_SHOW)
            {
                ps["screen"] = "view";
                ps["id"] = route.id;
            }

            //override store if necessary
            Hashtable store = this.config["store"] as Hashtable ?? [];
            //add flash messages if any
            store["flash"] = new Hashtable()
            {
                ["success"] = fw.flash("success"),
                ["error"] = fw.flash("error"),
            };

            ps["store"] = store;
            ps = setPS(ps);
        }

        ps["f"] = this.list_filter;

        return ps;
    }

    public override Hashtable ShowAction(int id = 0)
    {
        if (!fw.isJsonExpected())
        {
            //direct access to show page - redirect to index
            fw.routeRedirect("Index");
            return null;
        }

        var mode = reqs("mode"); // view or edit

        Hashtable ps = [];
        Hashtable item = modelOneOrFail(id);

        // addtionally, if we have autocomplete fields - preload their values
        var multi_rows = new Hashtable();
        var subtables = new Hashtable();
        var attachments = new Hashtable(); //att_id => att item
        var att_links = new ArrayList(); //linked att ids
        var att_files = new Hashtable(); // per-field: field => [ids]

        var fields = (ArrayList)this.config[mode == "edit" ? "showform_fields" : "show_fields"];
        foreach (Hashtable def in fields)
        {
            var field_name = def["field"].toStr();
            var model_name = def["lookup_model"].toStr();
            var dtype = def["type"].toStr();
            if (dtype == "autocomplete" || dtype == "plaintext_autocomplete")
            {
                if (def["lookup_by_value"].toBool())
                {
                    item[field_name + "_iname"] = item[field_name];
                }
                else
                {
                    var ac_model = fw.model(model_name);
                    if (ac_model != null)
                    {
                        var ac_item = ac_model.one(item[field_name]);
                        item[field_name + "_iname"] = ac_item["iname"];
                    }
                }
            }
            else if (dtype == "multi" || dtype == "multicb" || dtype == "multicb_prio")
            {
                //multiple values either from lookup model or junction model
                FwModel multi_model;
                ArrayList rows;
                if (def.ContainsKey("lookup_model"))
                {
                    //use comma-separated values in field from lookup_model
                    multi_model = fw.model(model_name);
                    rows = multi_model.listWithChecked(item[field_name].toStr(), def);
                }
                else
                {
                    //use junction model
                    multi_model = fw.model(def["model"].toStr());
                    if (def["is_by_linked"].toBool())
                        // list main items by linked id from junction model (i.e. list of Users(with checked) for Company from UsersCompanies model)
                        rows = multi_model.listMainByLinkedId(id, def); //junction model
                    else
                        // list linked items by main id from junction model (i.e. list of Companies(with checked) for User from UsersCompanies model)
                        rows = multi_model.listLinkedByMainId(id, def); //junction model
                }
                multi_rows[field_name] = multi_model.filterListOptionsForJson(rows);
            }
            else if (dtype == "subtable" || dtype == "subtable_edit")
            {
                var sub_model = fw.model(def["model"].toStr());
                var list_rows = sub_model.listByMainId(id, def); //list related rows from db
                sub_model.prepareSubtable(list_rows, id, def);

                subtables[field_name] = list_rows;
            }
            else if (dtype == "att" || dtype == "att_edit")
            {
                var att_id = item[field_name].toInt();
                if (att_id > 0)
                {
                    Hashtable att_item = fw.model<Att>().one(att_id);
                    if (att_item.Count > 0)
                    {
                        fw.model<Att>().filterForJson(att_item);
                        // add size for display
                        attachments[att_id] = att_item;
                    }
                }
            }
            else if (dtype == "att_links" || dtype == "att_links_edit")
            {
                var att_items = fw.model<Att>().listLinked(model0.table_name, id);
                foreach (Hashtable att_item in att_items)
                {
                    fw.model<Att>().filterForJson(att_item);
                    attachments[att_item["id"]] = att_item;
                    att_links.Add(att_item["id"]);
                }
            }
            else if (dtype == "att_files" || dtype == "att_files_edit")
            {
                var category = def["att_category"].toStr();
                var att_items = fw.model<Att>().listByEntityCategory(model0.table_name, id, category);
                var ids = new ArrayList();
                foreach (Hashtable att_item in att_items)
                {
                    fw.model<Att>().filterForJson(att_item);
                    attachments[att_item["id"]] = att_item;
                    ids.Add(att_item["id"]);
                }
                att_files[field_name] = ids;
            }
        }

        if (multi_rows.Count > 0)
            ps["multi_rows"] = multi_rows;
        if (subtables.Count > 0)
            ps["subtables"] = subtables;
        if (attachments.Count > 0)
            ps["attachments"] = attachments;
        if (att_links.Count > 0)
            ps["att_links"] = att_links;
        if (att_files.Count > 0)
            ps["att_files"] = att_files;

        // fill added/updated too
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
        if (reqb("refresh"))
            throw new Exception("Wrong use refresh=1 on Vue Controller");

        fw.model<Users>().checkReadOnly();

        Hashtable item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // Dim item_old As Hashtable = modelOne(id)

        Hashtable itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());

        id = this.modelAddOrUpdate(id, itemdb);

        return this.afterSave(success, id, is_new);
    }

    public override Hashtable NextAction(string form_id)
    {
        var ps = base.NextAction(form_id);
        ps["_json"] = true;
        return ps;
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        if (!fw.isJsonExpected())
        {
            //direct access to show page - redirect to index
            fw.routeRedirect("Index");
            return null;
        }

        throw new NotImplementedException(); // N/A for Vue controllers
    }

    public override void ShowDeleteAction(int id)
    {
        throw new NotImplementedException(); // N/A for Vue controllers
    }

}
