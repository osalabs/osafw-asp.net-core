// Fw Vue controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public class FwVueController : FwDynamicController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected FwList validation_issues = [];

    // list of keys from fw.G to pass to Vue
    protected string global_keys = "ROOT_URL is_list_btn_left date_format time_format timezone";

    public override void init(FW fw)
    {
        base.init(fw);
        validation_issues = [];
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_VUE"); // layout for Vue pages
    }

    /// <summary>
    /// Sets the Vue list DB select fields from active headers plus the record id field.
    /// </summary>
    protected override void setListFields()
    {
        list_fields = buildListFields(list_headers.Select(header => ((FwDict)header)["field_name"].toStr()));
    }

    /// <summary>
    /// Returns the current request's create, edit, and delete capabilities from read-only and RBAC checks.
    /// </summary>
    protected virtual FwDict getCapabilities()
    {
        return new FwDict
        {
            ["create"] = !is_readonly && rbac[Permissions.PERMISSION_ADD].toBool(),
            ["edit"] = !is_readonly && rbac[Permissions.PERMISSION_EDIT].toBool(),
            ["delete"] = !is_readonly && rbac[Permissions.PERMISSION_DELETE].toBool(),
        };
    }

    /// <summary>
    /// Returns optional server-produced restrictions for one row. Overrides may return false values to narrow
    /// the page capabilities; true values never grant an action denied at page level.
    /// </summary>
    protected virtual FwDict getListRowCapabilities(FwDict row)
    {
        return row["_capabilities"] as FwDict ?? [];
    }

    /// <summary>
    /// Sanitizes and intersects one row's capability metadata with the page capability boundary.
    /// </summary>
    protected virtual void applyListRowCapabilities(FwDict row)
    {
        var restrictions = getListRowCapabilities(row);
        var hasRestrictions = restrictions.Count > 0 || row.ContainsKey("_capabilities");
        var capabilities = getCapabilities();

        foreach (var action in new[] { "create", "edit", "delete" })
        {
            if (restrictions.ContainsKey(action) && !restrictions[action].toBool())
                capabilities[action] = false;
        }

        if (row["_meta"] is FwDict meta && meta["is_ro"].toBool())
        {
            capabilities["edit"] = false;
            capabilities["delete"] = false;
            hasRestrictions = true;
        }

        if (hasRestrictions)
            row["_capabilities"] = capabilities;
        else
            row.Remove("_capabilities");
    }

    /// <summary>
    /// filter list rows for json output using model's filterForJson
    /// </summary>
    protected virtual void filterListForJson()
    {
        //extract autocomplete fields
        FwList ac_fields = [];
        FwList fields = collectFormFields("showform_fields");
        foreach (FwDict def in fields)
        {
            //var field_name = def["field"].toStr();
            //var model_name = def["lookup_model"].toStr();
            var dtype = def["type"].toStr();
            if (dtype == "autocomplete" || dtype == "plaintext_autocomplete")
            {
                ac_fields.Add(def);
            }
        }

        foreach (FwDict row in list_rows)
        {
            model0.filterForJson(row);

            //added/updated username - it's readonly so we can replace _id fields with names
            if (!string.IsNullOrEmpty(model0.field_add_users_id) && row.TryGetValue(model0.field_add_users_id, out object? add_id))
                row["add_users_id"] = fw.model<Users>().iname(add_id.toInt());
            if (!string.IsNullOrEmpty(model0.field_upd_users_id) && row.TryGetValue(model0.field_upd_users_id, out object? upd_id))
                row["upd_users_id"] = fw.model<Users>().iname(upd_id.toInt());

            //autocomplete fields - add _iname fields
            foreach (FwDict def in ac_fields)
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
    /// Populates initial Vue page state, including globals, headers, form config, and feature flags.
    /// </summary>
    protected virtual void setScopeInitial(FwDict ps)
    {
        ps["XSS"] = fw.Session("XSS");
        ps["access_level"] = fw.userAccessLevel;
        ps["me_id"] = fw.userId;
        //some specific from global fw.G;
        var global = new FwDict();
        foreach (var key in Utils.qw(global_keys))
        {
            global[key] = fw.G[key];
        }
        global["user_iname"] = fw.model<Users>().iname(fw.userId);
        ps["global"] = global;

        setViewList(false); // initialize list_headers and related
        list_user_view ??= [];
        list_user_view["widths"] = normalizeUserViewWidths(list_user_view["widths"]);

        // userviews customization support
        ps["all_list_columns"] = getViewListArr(getViewListUserFields(), true); // list all fields
        ps["select_userviews"] = fw.model<UserViews>().listSelectByIcode(UserViews.icodeByUrl(base_url, is_list_edit));

        ps["field_id"] = model0.field_id;
        ps["view_list_custom"] = Utils.qh(this.view_list_custom, "1");
        ps["view_list_custom_trusted"] = Utils.qh(this.view_list_custom_trusted, "1");
        if (config.ContainsKey("list_calculated_fields") || (config["store"] is FwDict legacyStore && legacyStore.ContainsKey("list_calculated_fields")))
            ps["list_calculated_fields"] = getListCalculatedFieldNames();

        // add form tabs with tab-specific field definitions if configured
        if (config["form_tabs"] is IList form_tabs && form_tabs.Count > 1)
        {
            var formTabs = new FwList(form_tabs);
            ps["form_tabs"] = formTabs;
            ps["show_fields_tabs"] = buildFormTabFields("show_fields", formTabs);
            ps["showform_fields_tabs"] = buildFormTabFields("showform_fields", formTabs);
        }

        //return view form definitions
        ps["show_fields"] = this.config["show_fields"];
        //return editable fields definitions
        ps["showform_fields"] = this.config["showform_fields"];

        ps["list_user_view"] = this.list_user_view;
        ps["list_headers"] = this.list_headers;
        ps["capabilities"] = getCapabilities();

        // other static params
        ps["related_id"] = this.related_id;
        ps["base_url"] = this.base_url;
        setPSReturnContext(ps);
        ps["is_userlists"] = this.is_userlists;
        ps["is_activity_logs"] = this.is_activity_logs;
        ps["is_readonly"] = is_readonly;
        ps["is_list_edit"] = is_list_edit;
    }

    /// <summary>
    /// Populates Vue list rows with sorting, filtering, paging, export, and JSON shaping applied.
    /// </summary>
    protected virtual void setScopeListRows(FwDict ps)
    {
        setListSorting();

        setListSearch();
        setListSearchStatus();

        if (list_headers.Count == 0)
            setViewList(false, false); // initialize list headers only; row JSON does not need filter UI options

        //only select from db visible fields + id, save as comma-separated string into list_fields
        setListFields();

        getListRows();
        filterListForJson();
        pruneListCalculatedDependenciesForJson();

        // if export - no need further processing - just return asap
        if (export_format.Length > 0)
            return;

        foreach (FwDict row in list_rows)
            applyListRowCapabilities(row);

        ps["list_rows"] = this.list_rows;
        ps["count"] = this.list_count;
        ps["pager"] = this.list_pager;
    }

    /// <summary>
    /// Removes source fields fetched only to calculate selected list columns after all row-shaping overrides have run.
    /// </summary>
    protected virtual void pruneListCalculatedDependenciesForJson()
    {
        if (list_calculated_dependency_fields_added.Count == 0)
            return;

        foreach (FwDict row in list_rows)
            foreach (var field in list_calculated_dependency_fields_added.Keys)
                row.Remove(field);
    }

    /// <summary>
    /// Populates Vue lookup data from configured form fields and optional user-list state.
    /// </summary>
    protected virtual void setScopeLookups(FwDict ps)
    {
        // userlists support if necessary
        if (this.is_userlists)
            this.setUserLists(ps);

        if (list_headers.Count == 0)
            setViewList(false, false); // lookup-only JSON does not need filter UI options

        FwList showform_fields = collectFormFields("showform_fields");
        var selectedValuesByLookupModel = listLookupSelectedValuesByLookupModel(showform_fields);
        //FwRow hfields = _fieldsToHash(showform_fields);

        // extract lookups from config and add to ps
        var lookups = new FwDict();
        foreach (FwDict def in showform_fields)
        {
            if (def == null)
                continue;

            var dtype = def["type"].toStr();
            var lookup_model = def["lookup_model"].toStr();
            if (lookup_model.Length > 0 && dtype != "autocomplete" && !lookups.ContainsKey(lookup_model))
            {
                //all lookup_models, except autocomplete (for those it could be too large)
                var selectedValues = selectedValuesByLookupModel[lookup_model] as StrList;
                lookups[lookup_model] = fw.model(lookup_model).listSelectOptions(def, selectedValues != null && selectedValues.Count > 0 ? selectedValues : null);
            }

            var lookup_tpl = def["lookup_tpl"].toStr();
            if (lookup_tpl.Length > 0 && !lookups.ContainsKey(lookup_tpl))
            {
                lookups[lookup_tpl] = FormUtils.selectTplOptions(lookup_tpl);
            }
        }

        ps["lookups"] = lookups;
    }

    private FwDict listLookupSelectedValuesByLookupModel(FwList showformFields)
    {
        FwDict result = [];
        if (list_rows.Count == 0)
            return result;

        foreach (FwDict def in showformFields)
        {
            var fieldName = def["field"].toStr();
            var lookupModel = def["lookup_model"].toStr();
            if (fieldName.Length == 0 || def["type"].toStr() == "autocomplete" || lookupModel.Length == 0)
                continue;

            foreach (FwDict row in list_rows)
            {
                var value = row[fieldName].toStr().Trim();
                if (value.Length == 0)
                    continue;

                if (result[lookupModel] is not StrList values)
                {
                    values = [];
                    result[lookupModel] = values;
                }

                if (!values.Contains(value))
                    values.Add(value);
            }
        }

        return result;
    }

    /// <summary>
    /// basically return layout/js to the browser, then Vue will load data via API
    /// </summary>
    /// <returns>FwRow - related template will be parsed, null - no templates parsed (if action did all the output)</returns>
    public override FwDict IndexAction()
    {
        var scope = reqs("scope");
        var scopes = scope.Length > 0 ? Utils.commastr2hash(scope, "1") : [];
        if (export_format.Length > 0)
            scopes["list_rows"] = "1";

        // get filters from the search form
        initFilter();

        // set standard output - load html with Vue app
        FwDict ps = [];

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
            var route = fw.getRoute(fw.request?.Path ?? string.Empty);
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
            FwDict store = this.config["store"] as FwDict ?? [];
            //add flash messages if any
            store["flash"] = new FwDict()
            {
                ["success"] = fw.flash("success"),
                ["error"] = fw.flash("error"),
            };

            ps["store"] = store;
#pragma warning disable CS0618 // Preserve dispatch to existing project overrides of setPS().
            ps = setPS(ps);
#pragma warning restore CS0618
        }

        ps["f"] = this.list_filter;

        return ps;
    }

    /// <summary>
    /// Return view/edit payload for Vue screens, including dynamic activity log and user list
    /// data so the Vue UI can render the same blocks as dynamic templates.
    /// </summary>
    /// <param name="id">Primary key for the item to load.</param>
    /// <returns>FwDict payload for Vue view/edit screen or null if redirect performed.</returns>
    public override FwDict? ShowAction(int id = 0)
    {
        if (!fw.isJsonExpected())
        {
            //direct access to show page - redirect to index
            fw.routeRedirect("Index");
            return null;
        }

        var mode = reqs("mode"); // view or edit

        FwDict ps = [];
        FwDict item = modelOneOrFail(id);

        // addtionally, if we have autocomplete fields - preload their values
        var multi_rows = new FwDict();
        var subtables = new FwDict();
        var attachments = new FwDict(); //att_id => att item
        var att_links = new IntList(); //linked att ids
        var att_files = new FwDict(); // per-field: field => [ids]
        var lookups_by_field = new FwDict();

        var fields = collectFormFields(mode == "edit" ? "showform_fields" : "show_fields");
        var processed_fields = new HashSet<string>();
        foreach (FwDict def in fields)
        {
            def["i"] = item;
            def["record_id"] = id;
            var field_name = def["field"].toStr();
            var model_name = def["lookup_model"].toStr();
            var dtype = def["type"].toStr();
            var processed_key = $"{dtype}:{field_name}";
            if (processed_fields.Contains(processed_key))
                continue;
            processed_fields.Add(processed_key);
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
                FwList rows;
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
            else if (mode == "edit" && (dtype == "select" || dtype == "radio") && model_name.Length > 0)
            {
                lookups_by_field[field_name] = fw.model(model_name).listSelectOptions(def);
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
                    FwDict att_item = fw.model<Att>().one(att_id);
                    if (att_item.Count > 0)
                    {
                        fw.model<Att>().filterForJson(att_item);
                        // add size for display
                        attachments[att_id.toStr()] = att_item;
                    }
                }
            }
            else if (dtype == "att_links" || dtype == "att_links_edit")
            {
                var att_items = fw.model<Att>().listLinked(model0.table_name, id);
                foreach (FwDict att_item in att_items)
                {
                    fw.model<Att>().filterForJson(att_item);
                    var attId = att_item["id"].toInt();
                    attachments[attId.toStr()] = att_item;
                    att_links.Add(attId);
                }
            }
            else if (dtype == "att_files" || dtype == "att_files_edit")
            {
                var category = def["att_category"].toStr();
                var att_items = fw.model<Att>().listByEntityCategory(model0.table_name, id, category);
                var ids = new IntList();
                foreach (FwDict att_item in att_items)
                {
                    fw.model<Att>().filterForJson(att_item);
                    var attId = att_item["id"].toInt();
                    attachments[attId.toStr()] = att_item;
                    ids.Add(attId);
                }
                att_files[field_name] = ids;
            }
        }

        if (multi_rows.Count > 0)
            ps["multi_rows"] = multi_rows;
        if (lookups_by_field.Count > 0)
            ps["lookups_by_field"] = lookups_by_field;
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

        model0.filterForJson(item);
        applyListRowCapabilities(item);

        ps["id"] = id;
        ps["i"] = item;
        ps["_json"] = true;
        return ps;
    }

    /// <summary>
    /// Build a mapping of form tab codes to their configured fields to support Vue tab rendering.
    /// </summary>
    /// <param name="prefix">The config prefix (show_fields or showform_fields).</param>
    /// <param name="formTabs">Tab definitions from config.</param>
    /// <returns>A dictionary keyed by tab code with field definitions for each tab.</returns>
    private FwDict buildFormTabFields(string prefix, FwList formTabs)
    {
        var tabFields = new FwDict();
        foreach (FwDict tab in formTabs)
        {
            var tabCode = tab["tab"].toStr();
            var fields = getConfigShowFormFieldsByTab(prefix, tabCode);
            if (fields.Count > 0)
                tabFields[tabCode] = fields;
        }

        return tabFields;
    }

    /// <summary>
    /// Saves a Vue dynamic controller row after checking update access to the target parent row.
    /// </summary>
    /// <param name="id">Existing parent row id to update, or zero for a new row.</param>
    /// <returns>JSON-oriented save response with subtable reconciliation metadata when needed.</returns>
    public override FwDict? SaveAction(int id = 0)
    {
        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");
        if (reqb("refresh"))
            throw new Exception("Wrong use refresh=1 on Vue Controller");

        checkReadOnly();
        if (id != 0)
            modelOneOrFail(id);

        FwDict item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        removeImmutableOnEditFields(id, item);
        Validate(id, item);
        if (validation_issues.Any(issue => issue["severity"].toStr() == "error"))
        {
            foreach (FwDict issue in validation_issues.Where(issue => issue["severity"].toStr() == "error"))
            {
                var field = issue["field"].toStr();
                if (!fw.FormErrors.ContainsKey(field))
                    fw.FormErrors[field] = issue["message"];
            }
            validateCheckResult(false);
        }
        // load old record if necessary
        // var itemOld = modelOne(id);

        FwDict itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());
        removeImmutableOnEditFields(id, itemdb);

        id = this.modelAddOrUpdate(id, itemdb);

        var moreJson = buildSubtableSavePayload(id);
        if (validation_issues.Count > 0)
            moreJson["validation_issues"] = new FwList(validation_issues);

        return this.afterSave(success, id, is_new, FW.ACTION_SHOW_FORM, "", moreJson.Count > 0 ? moreJson : null);
    }

    /// <summary>
    /// Adds a neutral validation issue. Attempted values are included only when the matching field definition
    /// explicitly sets <c>validation_show_value</c> to JSON boolean <c>true</c>. Password, hidden,
    /// credential, secret, token, and API-key control types never expose attempted values.
    /// </summary>
    protected virtual void addValidationIssue(
        string severity,
        string field,
        string message,
        string? tab = null,
        string? row_id = null,
        object? attempted_value = null)
    {
        if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(message))
            return;

        var resolved = resolveValidationField(field);
        row_id ??= resolved.rowId;
        tab ??= resolved.tab;
        if (validation_issues.Any(existing =>
            existing["severity"].toStr() == (severity == "warning" ? "warning" : "error")
            && existing["field"].toStr() == field
            && existing["row_id"].toStr() == row_id.toStr()))
            return;

        var issue = new FwDict
        {
            ["severity"] = severity == "warning" ? "warning" : "error",
            ["field"] = field,
            ["message"] = message,
        };
        if (!string.IsNullOrEmpty(tab))
            issue["tab"] = tab;
        if (!string.IsNullOrEmpty(row_id))
            issue["row_id"] = row_id;
        if (attempted_value != null && isValidationValueAllowed(resolved.definition))
            issue["value"] = attempted_value;

        validation_issues.Add(issue);
    }

    /// <summary>
    /// Converts legacy field-error details into neutral Vue validation issues without echoing submitted values by default.
    /// </summary>
    protected virtual void addFormErrorValidationIssues()
    {
        foreach (var entry in fw.FormErrors)
        {
            if (entry.Key is "REQUIRED" or "INVALID")
                continue;

            var resolved = resolveValidationField(entry.Key);
            object? attemptedValue = null;
            if (isValidationValueAllowed(resolved.definition))
            {
                attemptedValue = resolved.subtable.Length > 0
                    ? reqh($"item-{resolved.subtable}#{resolved.rowId}")[resolved.logicalField]
                    : reqh("item")[resolved.logicalField];
            }

            addValidationIssue(
                "error",
                entry.Key,
                validationIssueMessage(entry.Value),
                resolved.tab,
                resolved.rowId,
                attemptedValue);
        }
    }

    private static string validationIssueMessage(object? code)
    {
        if (code is true)
            return "Required field";

        return code.toStr() switch
        {
            "REQUIRED" => "Required field",
            "EXISTS" => "This name already exists in our database",
            "EMAIL" => "Invalid Email",
            "WRONG" => "Invalid",
            var message when message.Length > 0 => message,
            _ => "Invalid value",
        };
    }

    private static bool isValidationValueAllowed(FwDict? definition)
    {
        if (definition?["validation_show_value"] is not true)
            return false;

        return definition["type"].toStr().ToLowerInvariant() switch
        {
            "password" or "hidden" or "credential" or "secret" or "token" or "api_key" => false,
            _ => true,
        };
    }

    private (FwDict? definition, string logicalField, string subtable, string? rowId, string? tab) resolveValidationField(string issueField)
    {
        var match = Regex.Match(issueField, @"^item-(?<subtable>.+?)#(?<row>[^\[]+)\[(?<field>[^\]]+)\]$");
        var logicalField = match.Success ? match.Groups["field"].Value : issueField;
        var subtable = match.Success ? match.Groups["subtable"].Value : string.Empty;
        var rowId = match.Success ? match.Groups["row"].Value : null;

        foreach (var candidate in validationFieldDefinitions())
        {
            var def = candidate.definition;
            if (subtable.Length == 0)
            {
                if (def["field"].toStr() == logicalField || def["issue_field"].toStr() == issueField)
                    return (def, logicalField, subtable, rowId, candidate.tab);
                continue;
            }

            if (def["type"].toStr() != "subtable_edit" || def["field"].toStr() != subtable)
                continue;

            foreach (FwDict childDef in getSubtableFormFields(def))
                if (childDef["field"].toStr() == logicalField)
                    return (childDef, logicalField, subtable, rowId, candidate.tab);

            return (null, logicalField, subtable, rowId, candidate.tab);
        }

        return (null, logicalField, subtable, rowId, null);
    }

    private IEnumerable<(FwDict definition, string? tab)> validationFieldDefinitions()
    {
        if (config["showform_fields"] is IList baseFields)
            foreach (FwDict def in new FwList(baseFields))
                yield return (def, null);

        if (config["form_tabs"] is not IList tabs)
            yield break;

        foreach (FwDict tabDef in new FwList(tabs))
        {
            var tab = tabDef["tab"].toStr();
            if (tab.Length == 0)
                continue;
            foreach (FwDict def in getConfigShowFormFieldsByTab("showform_fields", tab))
                yield return (def, tab);
        }
    }

    /// <summary>
    /// Returns a structured 400 response for Vue validation failures while preserving the legacy error details map.
    /// </summary>
    public override FwDict? actionError(Exception? ex, object[] args)
    {
        if (fw.isJsonExpected() && ex is ValidationException validationException)
        {
            addFormErrorValidationIssues();
            fw.G["err_msg"] = validationException.Message;
            if (!fw.response.HasStarted)
                fw.response.StatusCode = 400;

            var moreJson = new FwDict { ["validation_issues"] = new FwList(validation_issues) };
            return afterSave(false, args.Length > 0 ? args[0] : null, more_json: moreJson);
        }

        return base.actionError(ex, args);
    }

    /// <summary>
    /// Build subtable save metadata for Vue clients so they can reconcile new row ids
    /// and refresh subtable data without losing focus during autosave.
    /// </summary>
    /// <param name="id">Main record id that subtable rows are linked to.</param>
    /// <returns>Payload with subtable row id mapping and refreshed rows for updated subtables.</returns>
    protected virtual FwDict buildSubtableSavePayload(int id)
    {
        var payload = new FwDict();
        if (!fw.isJsonExpected())
            return payload;

        if (subtable_save_row_ids.Count > 0)
            payload["subtable_row_ids"] = subtable_save_row_ids;

        var subtables = new FwDict();
        var fields = collectFormFields("showform_fields");
        foreach (FwDict def in fields)
        {
            var field = def["field"].toStr();
            var dtype = def["type"].toStr();
            if (dtype != "subtable_edit")
                continue;

            if (req("item-" + field) == null && !subtable_save_row_ids.ContainsKey(field))
                continue;

            var sub_model = fw.model(def["model"].toStr());
            var list_rows = sub_model.listByMainId(id, def); //list related rows from db
            sub_model.prepareSubtable(list_rows, id, def);
            subtables[field] = list_rows;
        }

        if (subtables.Count > 0)
            payload["subtables"] = subtables;

        return payload;
    }

    public override FwDict NextAction(string form_id)
    {
        var ps = base.NextAction(form_id);
        ps["_json"] = true;
        return ps;
    }

    public override FwDict? ShowFormAction(int id = 0)
    {
        if (!fw.isJsonExpected())
        {
            //direct access to show page - redirect to index
            fw.routeRedirect("Index");
            return null;
        }

        throw new NotImplementedException(); // N/A for Vue controllers
    }

    /// <summary>
    /// Renders the standard delete confirmation for direct Vue delete-route access.
    /// </summary>
    public override void ShowDeleteAction(int id)
    {
        base.ShowDeleteAction(id);
    }

}
