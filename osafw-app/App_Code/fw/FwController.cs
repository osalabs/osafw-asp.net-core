// Fw Controller base class

// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;

namespace osafw;

public abstract class FwController
{
    public static int access_level = Users.ACL_VISITOR; // access level for the controller. fw.config("access_levels") overrides this. 0 (public access), 1(min logged level), 100(max admin level)

    public static string route_default_action = ""; // supported values - "" (use Default Parser for unknown actions), Index (use IndexAction for unknown actions), Show (assume action is id and use ShowAction)
    public string route_onerror = ""; //route redirect action name in case ApplicationException occurs in current route, if empty - 500 error page returned

    public string base_url = string.Empty;               // base url for the controller
    public string base_url_suffix = string.Empty;        // additional base url suffix
    public string template_basedir = "";  // templates base dir for the controller, if not set - [/route_prefix]/route_controller used

    public Hashtable form_new_defaults = [];   // optional, defaults for the fields in new form, overridden by item passed from request
    public string required_fields = string.Empty;        // optional, default required fields, space-separated
    public string save_fields = string.Empty;            // required, fields to save from the form to db, space-separated
    public string save_fields_checkboxes = string.Empty; // optional, checkboxes fields to save from the form to db, qw string: "field|def_value field2|def_value2"

    protected FW fw = null!;
    protected DB db = null!;
    protected FwModel model0 = null!;
    protected Hashtable config = [];                  // controller config, loaded from template dir/config.json
    protected Hashtable access_actions_to_permissions = []; // optional, controller-level custom actions to permissions mapping for role-based access checks, e.g. "UIMain" => Permissions.PERMISSION_VIEW . Can also be used to override default actions to permissions

    protected string list_view = string.Empty;                  // table/view to use in list sql, if empty model0.table_name used
    protected string list_fields = "*";          // comma-separated and quoted list of fields to select in list sql
    protected string list_orderby = string.Empty;               // orderby for the list screen
    protected Hashtable list_filter = [];             // filter values for the list screen
    protected Hashtable list_filter_search = [];      // filter for the search columns from reqh("search")
    protected Hashtable list_where_params = [];       // any sql params for the list_where
    protected string list_where = " 1=1 ";       // where to use in list sql, default is non-deleted records (see setListSearch() )
    protected long list_count;                    // count of list rows returned from db
    protected ArrayList list_rows = [];               // list rows returned from db (array of hashes)
    protected ArrayList list_headers = [];            // list headers with misc meta info per column
    protected ArrayList list_pager = [];              // pager for the list from FormUtils.getPager
    protected int list_pagesize_export = 10000;  // max pagesize for export (to avoid memory issues on large exports), override in controller if needed
    protected string list_sortdef = string.Empty;               // required for Index, default list sorting: name asc|desc
    protected Hashtable list_sortmap = [];            // required for Index, sortmap fields
    protected Hashtable list_user_view = [];          // optional, user view settings for the list screen from UserViews model
    protected string search_fields = string.Empty;              // optional, search fields, space-separated
                                                 // fields to search via $s=list_filter["s"), ] - means exact match, not "like"

    // editable list support
    protected bool is_dynamic_index_edit = false;
    protected bool is_list_edit = false;         // true if requested list edit mode and it's allowed by is_dynamic_index_edit

    public string export_format = "";            // empty or "csv" or "xls" (set from query string "export") - export format for IndexAction
    protected string export_filename = "export"; // default filename for export, without extension

    // support of customizable view list
    // map of fileld names to screen names
    protected bool is_dynamic_index = false;     // true if controller has dynamic IndexAction, then define below:
    protected string view_list_defaults = "";    // qw list of default columns
    protected Hashtable view_list_map = [];           // list of all available columns fieldname|visiblename
    protected string view_list_custom = "";      // qw list of custom-formatted fields for the list_table

    protected bool is_dynamic_show = false;      // true if controller has dynamic ShowAction, requires "show_fields" to be defined in config.json
    protected bool is_dynamic_showform = false;  // true if controller has dynamic ShowFormAction, requires "showform_fields" to be defined in config.json
    protected string form_tab = "";              // current tab in the form

    protected bool is_userlists = false;         // true if controller should support UserLists
    protected bool is_activity_logs = false;     // true if controller should support ActivityLogs

    protected bool is_readonly = false;          // true if user is readonly, no actions modifying data allowed

    protected string route_return = FW.ACTION_SHOW_FORM; // FW.ACTION_SHOW or _INDEX to return (usually after SaveAction, default ACTION_SHOW_FORM)
    protected string return_url = string.Empty;          // url to return after SaveAction successfully completed, passed via request
    protected string related_id = string.Empty;          // related id, passed via request. Controller should limit view to items related to this id
    protected string related_field_name = string.Empty;  // if set (in Controller) and $related_id passed - list will be filtered on this field

    protected Hashtable rbac = [];               // RBAC for the current user, read in init()

    private const string ControllerConfigCacheKeyPrefix = "fw:controller-config:";

    protected FwController(FW? fw = null)
    {
        if (fw != null)
        {
            this.fw = fw;
            this.db = fw.db;
        }
    }

    public virtual void init(FW fw)
    {
        this.fw = fw;
        this.db = fw.db;

        is_readonly = fw.model<Users>().isReadOnly();

        return_url = reqs("return_url");
        related_id = reqs("related_id");
        form_tab = reqs("tab");
        export_format = reqs("export");

        rbac = fw.model<Users>().getRBAC();
    }

    // load controller config (usually from config.json) with caching based on file last write time
    public virtual Hashtable getControllerConfigCached(string full_path)
    {
        var lastWriteTimeUtc = System.IO.File.GetLastWriteTimeUtc(full_path);

        var cacheKey = ControllerConfigCacheKeyPrefix + full_path.ToLowerInvariant();
        if (FwCache.getValue(cacheKey) is Hashtable cached &&
            cached["lastWriteTimeUtc"] is DateTime cachedWriteTime &&
            cachedWriteTime == lastWriteTimeUtc)
        {
            if (cached["template"] is Hashtable cachedTemplate)
                return Utils.cloneHashDeep(cachedTemplate) ?? []; // CACHE HIT - return a clone of the cached template
        }

        // CACHE MISS - load from file
        var parsedObj = Utils.jsonDecode(Utils.getFileContent(full_path));
        if (parsedObj is not Hashtable parsed)
            throw new ApplicationException("Controller Config is invalid, check json in templates: " + full_path);

        // store a pristine copy so subsequent loads can reuse the parsed structure without re-reading JSON
        var cacheEntry = new Hashtable
        {
            ["lastWriteTimeUtc"] = lastWriteTimeUtc,
            ["template"] = parsed,
        };
        FwCache.setValue(cacheKey, cacheEntry, 3600);

        return Utils.cloneHashDeep(parsed) ?? [];
    }

    // load controller config from json in template dir (based on base_url)
    public virtual void loadControllerConfig(string config_filename = "config.json")
    {
        if (string.IsNullOrEmpty(base_url))
        {
            // silent fail as it could happen for virtual controllers
            logger(LogLevel.TRACE, "no base_url set, cannot load config");
            return;
        }

        var conf_file0 = base_url.ToLower() + "/" + config_filename;
        logger(LogLevel.TRACE, "loading config from:", conf_file0);

        var conf_file = fw.config("template") + "/" + conf_file0;
        if (!System.IO.File.Exists(conf_file))
            throw new ApplicationException("Controller Config file not found in templates: " + conf_file0);

        var config = getControllerConfigCached(conf_file) ?? throw new ApplicationException("Controller Config is invalid, check json in templates: " + conf_file0);
        loadControllerConfig(config);
    }

    public virtual void loadControllerConfig(Hashtable config)
    {
        this.config = config;
        // logger("loaded config:")
        // logger(config)

        var model_name = this.config["model"].toStr();
        if (!string.IsNullOrEmpty(model_name))
            model0 = fw.model(model_name);

        // check/conv to str
        required_fields = this.config["required_fields"].toStr();
        is_userlists = this.config["is_userlists"].toBool();

        // save_fields could be defined as qw string - check and convert
        var save_fields_raw = this.config["save_fields"];
        if (save_fields_raw is IList list)
            save_fields = Utils.qwRevert(list); // not optimal, but simplest for now
        else
            save_fields = save_fields_raw.toStr();

        form_new_defaults = this.config["form_new_defaults"] as Hashtable ?? [];

        // save_fields_checkboxes could be defined as qw string - check and convert
        var save_fields_checkboxes_raw = this.config["save_fields_checkboxes"];
        if (save_fields_checkboxes_raw is IDictionary dictionary)
            save_fields_checkboxes = Utils.qhRevert(dictionary); // not optimal, but simplest for now
        else
            save_fields_checkboxes = save_fields_checkboxes_raw.toStr();

        search_fields = this.config["search_fields"].toStr();
        list_sortdef = this.config["list_sortdef"].toStr();

        var list_sortmap_raw = this.config["list_sortmap"];
        if (list_sortmap_raw is IDictionary)
            list_sortmap = (Hashtable)list_sortmap_raw;
        else
            list_sortmap = Utils.qh(this.config["list_sortmap"].toStr());

        related_field_name = this.config["related_field_name"].toStr();

        list_view = this.config["list_view"].toStr();

        is_dynamic_index = this.config["is_dynamic_index"].toBool();
        if (is_dynamic_index)
        {
            // Whoah! list view is dynamic
            view_list_defaults = this.config["view_list_defaults"].toStr();

            // since view_list_map could be defined as qw string or as hashtable - check and convert
            var raw_view_list_map = this.config["view_list_map"];
            if (raw_view_list_map is IDictionary)
                view_list_map = (Hashtable)raw_view_list_map;
            else
                view_list_map = Utils.qh(raw_view_list_map.toStr());

            view_list_custom = this.config["view_list_custom"].toStr();
        }

        is_dynamic_index_edit = this.config["is_dynamic_index_edit"].toBool();
        if (is_dynamic_index_edit)
        {
            //combine with request param
            if (Utils.isEmpty(req("is_list_edit")))
                is_list_edit = is_dynamic_index_edit;
            else
                is_list_edit = reqb("is_list_edit") && is_dynamic_index_edit;

            if (is_list_edit)
            {
                //list edit is on - override view used for list
                var list_edit = config["list_edit"].toStr();
                if (!Utils.isEmpty(list_edit))
                    list_view = list_edit;

                //override list defaults if set
                if (!Utils.isEmpty(config["edit_list_defaults"]))
                    view_list_defaults = config["edit_list_defaults"].toStr();

                //override list map if set
                // since edit_list_map could be defined as qw string or as hashtable - check and convert
                if (!Utils.isEmpty(config["edit_list_map"]))
                {
                    var raw_edit_list_map = config["edit_list_map"];
                    if (raw_edit_list_map is IDictionary)
                        view_list_map = (Hashtable)raw_edit_list_map;
                    else
                        view_list_map = Utils.qh(raw_edit_list_map.toStr());
                }
            }
        }

        //common for both dynamic index and index_edit
        if (is_dynamic_index || is_dynamic_index_edit && is_list_edit)
        {
            if (list_sortmap.Count == 0)
                list_sortmap = getViewListSortmap(); // just add all fields from view_list_map if no list_sortmap in config
            if (search_fields == "")
                search_fields = getViewListUserFields(); // just search in all visible fields if no specific fields defined
        }

        is_dynamic_show = this.config["is_dynamic_show"].toBool();
        is_dynamic_showform = this.config["is_dynamic_showform"].toBool();

        route_return = this.config["route_return"].toStr();
    }

    /// <summary>
    /// return true if current request is GET request
    /// </summary>
    /// <returns></returns>
    public bool isGet()
    {
        return (fw.route.method == "GET");
    }

    /// <summary>
    /// return true if current request is PATCH request
    /// </summary>
    /// <returns></returns>
    public bool isPatch()
    {
        return (fw.route.method == "PATCH");
    }

    // set of helper functions to return string, integer, date values from request (fw.FORM)

    /// <summary>
    /// return value from request (fw.FORM) by name
    /// </summary>
    /// <param name="iname"></param>
    /// <param name="def">optional default value to return if request value is not set</param>
    /// <returns></returns>
    public object? req(string iname, object? def = null)
    {
        return fw.FORM[iname] ?? def;
    }

    /// <summary>
    /// return Hashtable value from request (fw.FORM)
    /// </summary>
    /// <param name="iname"></param>
    /// <returns></returns>
    public Hashtable reqh(string iname)
    {
        if (fw.FORM[iname] is Hashtable hf)
            return hf; 
        else
            return [];
    }

    /// <summary>
    /// return string value from request (fw.FORM)
    /// </summary>
    /// <param name="iname"></param>
    /// <param name="def">optional default value</param>
    /// <returns></returns>
    public string reqs(string iname, string def = "")
    {
        return fw.FORM[iname].toStr(def);
    }

    /// <summary>
    /// return int value from request (fw.FORM)
    /// </summary>
    /// <param name="iname"></param>
    /// <param name="def">optional default value</param>
    /// <returns></returns>
    public int reqi(string iname, int def = 0)
    {
        return fw.FORM[iname].toInt(def);
    }

    /// <summary>
    /// return date value from request (fw.FORM)
    /// </summary>
    /// <param name="iname"></param>
    /// <param name="def">optional default value</param>
    /// <returns></returns>
    public DateTime? reqd(string iname, DateTime? def = null)
    {
        return fw.FORM[iname].toDateOrNull() ?? def;
    }

    /// <summary>
    /// return bool value from request (fw.FORM)
    /// </summary>
    /// <param name="iname"></param>
    /// <param name="def"></param>
    /// <returns></returns>
    public bool reqb(string iname, bool def = false)
    {
        return fw.FORM.ContainsKey(iname) ? fw.FORM[iname].toBool() : def;
    }

    public void rw(string str)
    {
        fw.rw(str);
    }

    // methods from fw - just for a covenience, so no need to use "fw.", as they are used quite frequently
    public void logger(params object?[] args)
    {
        fw.logger(args);
    }
    public void logger(LogLevel level, params object?[] args)
    {
        fw.logger(level, args);
    }

    public virtual void checkXSS()
    {
        if (fw.Session("XSS") != reqs("XSS"))
            throw new AuthException("XSS Error. Reload the page or try to re-login");
    }

    // return hashtable of filter values
    // NOTE: automatically set to defaults - pagenum=0 and pagesize=MAX_PAGE_ITEMS
    // NOTE: if request param 'dofilter' passed - session filters cleaned
    // sample in IndexAction: me.get_filter()
    public virtual Hashtable initFilter(string? session_key = null)
    {
        Hashtable f = reqh("f");

        session_key ??= "_filter_" + fw.G["controller.action"];

        Hashtable sfilter = fw.SessionHashtable(session_key) ?? [];

        // if not forced filter - merge form filters to session filters
        bool is_dofilter = fw.FORM.ContainsKey("dofilter");
        if (!is_dofilter)
        {
            Utils.mergeHash(sfilter, f);
            f = sfilter;
        }
        else
        {
            // check if we need to load user filer
            var userfilters_id = reqi("userfilters_id");
            if (userfilters_id > 0)
            {
                var uf = fw.model<UserFilters>().one(userfilters_id);
                if (Utils.jsonDecode(uf["idesc"]) is Hashtable f1)
                    f = f1;
                if (!uf["is_system"].toBool())
                {
                    f["userfilters_id"] = userfilters_id; // set filter id (for edit/delete) only if not system
                    f["userfilter"] = uf;
                }
            }
            else
            {
                // check if we have some filter loaded
                userfilters_id = f["userfilters_id"].toInt();
                if (userfilters_id > 0)
                {
                    // just ned info on this filter
                    var uf = fw.model<UserFilters>().one(userfilters_id);
                    f["userfilter"] = uf;
                }
            }
        }

        // paging
        f["pagenum"] = f["pagenum"].toInt(); //sets default to 0 if no value or non-numeric
        f["pagesize"] = f["pagesize"].toInt(FormUtils.MAX_PAGE_ITEMS);

        // save in session for later use
        fw.SessionHashtable(session_key, f);

        this.list_filter = f;

        // advanced search
        string session_key_search = "_filtersearch_" + fw.G["controller.action"];
        list_filter_search = reqh("search");
        if (list_filter_search.Count == 0 && !is_dofilter)
        {
            //read from session
            list_filter_search = fw.SessionHashtable(session_key_search) ?? [];
        }
        else
        {
            //remember in session
            fw.SessionHashtable(session_key_search, list_filter_search);
        }

        return f;
    }

    /// <summary>
    /// clears list_filter and related session key
    /// </summary>
    /// <param name="session_key"></param>
    public virtual void clearFilter(string? session_key = null)
    {
        Hashtable f = [];
        session_key ??= "_filter_" + fw.G["controller.action"];
        fw.SessionHashtable(session_key, f);
        this.list_filter = f;
    }

    /// <summary>
    /// Validate required fields are non-empty and set global fw.FormErrors[field] values in case of errors
    /// </summary>
    /// <param name="id">id of the record, 0 if new record to add (for existing records - do not require fields not present in item)</param>
    /// <param name="item">fields/values to validate</param>
    /// <param name="fields">field names required to be non-empty (trim used)</param>
    /// <param name="form_errors">optional - form errors to fill</param>
    /// <returns>true if all required field names non-empty</returns>
    /// <remarks>also set global fw.FormErrors[REQUIRED]=true in case of validation error if no form_errors defined</remarks>
    public virtual bool validateRequired(int id, Hashtable item, Array fields, Hashtable? form_errors = null)
    {
        bool result = true;

        var is_global_errors = false;
        if (form_errors == null)
        {
            //if no form_errors passed - use global fw.FormErrors
            form_errors = fw.FormErrors;
            is_global_errors = true;
        }

        if (fields.Length > 0)
        {
            item ??= []; // if item is null - make it empty hash
            var is_new = (id == 0);
            foreach (string fld in fields)
            {
                var is_fld_exists = item.ContainsKey(fld);
                if (!is_new && !is_fld_exists)
                    continue; // for existing records - do not require fields not present in item (so we can update only some fields)

                var fldValue = item[fld].toStr();
                if (!string.IsNullOrEmpty(fld) && (!is_fld_exists || string.IsNullOrEmpty(fldValue.Trim())))
                {
                    result = false;
                    form_errors[fld] = true;
                }
            }
        }
        //else - no required fields defined - valid

        if (!result && is_global_errors)
            form_errors["REQUIRED"] = true; // set global error

        return result;
    }
    // same as above but fields param passed as a qw string
    public virtual bool validateRequired(int id, Hashtable item, string fields)
    {
        return validateRequired(id, item, Utils.qw(fields));
    }

    /// <summary>
    /// Check validation result (validate_required)
    /// </summary>
    /// <param name="result">to use from external validation check</param>
    /// <remarks>throw ValidationException exception if global FormErrors non-empty.
    /// Also set global FormErrors[INVALID] if FormErrors non-empty, but FormErrors[REQUIRED] not true
    /// </remarks>
    public virtual void validateCheckResult(bool result = true)
    {
        var hasRequired = fw.FormErrors.ContainsKey("REQUIRED") && fw.FormErrors["REQUIRED"] is true;
        if (hasRequired)
            result = false;

        if (fw.FormErrors.Count > 0 && !hasRequired)
        {
            fw.FormErrors["INVALID"] = true;
            result = false;
        }

        if (!result)
        {
            logger(LogLevel.DEBUG, "Validation failed:", fw.FormErrors);
            throw new ValidationException();
        }

    }

    /// <summary>
    /// Set list sorting fields - Me.list_orderby according to Me.list_filter filter and Me.list_sortmap and Me.list_sortdef
    /// </summary>
    /// <remarks></remarks>
    public virtual void setListSorting()
    {
        if (this.list_sortdef.Length == 0)
            throw new Exception("No default sort order defined, define in list_sortdef");
        if (this.list_sortmap.Count == 0)
            throw new Exception("No sort order mapping defined, define in list_sortmap");

        string sortby = this.list_filter["sortby"].toStr();
        string sortdir = this.list_filter["sortdir"].toStr();

        string sortdef_field = string.Empty;
        string sortdef_dir = string.Empty;
        Utils.split2(" ", this.list_sortdef, ref sortdef_field, ref sortdef_dir);

        // validation/mapping
        if (string.IsNullOrEmpty(sortby))
            sortby = sortdef_field;
        if (sortdir != "desc" && sortdir != "asc")
            sortdir = sortdef_dir;

        string orderby = this.list_sortmap[sortby].toStr().Trim();
        if (string.IsNullOrEmpty(orderby))
            throw new Exception("No orderby defined for [" + sortby + "], define in list_sortmap");

        this.list_filter["sortby"] = sortby;
        this.list_filter["sortdir"] = sortdir;

        this.list_orderby = FormUtils.sqlOrderBy(db, sortby, sortdir, list_sortmap);
    }

    /// <summary>
    /// Add to Me.list_where search conditions from Me.list_filter["s") ]nd based on fields in Me.search_fields
    /// </summary>
    /// <remarks>Sample: Me.search_fields="field1 field2,!field3 field4" => field1 LIKE '%$s%' or (field2 LIKE '%$s%' and field3='$s') or field4 LIKE '%$s%'</remarks>
    public virtual void setListSearch()
    {
        string s = this.list_filter["s"].toStr().Trim();
        if (!string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(this.search_fields))
        {
            var is_subquery = false;
            string list_table_name = list_view;
            if (string.IsNullOrEmpty(list_table_name))
                list_table_name = model0.table_name;
            else
                // list_table_name could contain subquery as "(...) t" - detect it (contains whitespace)
                is_subquery = Regex.IsMatch(list_table_name, @"\s");

            string like_s = "%" + s + "%";

            string[] afields = Utils.qw(this.search_fields); // OR fields delimited by space
            for (int i = 0; i <= afields.Length - 1; i++)
            {
                string[] afieldsand = afields[i].Split(","); // AND fields delimited by comma

                for (int j = 0; j <= afieldsand.Length - 1; j++)
                {
                    string fand = afieldsand[j];
                    string param_name = "list_search_" + i + "_" + j;
                    if (fand.StartsWith('!'))
                    {
                        // exact match
                        fand = fand.Substring(1);
                        if (is_subquery)
                        {
                            // for subqueries - just use string quoting, but convert to number (so only numeric search supported in this case)
                            list_where_params[param_name] = s.toLong();
                        }
                        else
                        {
                            var ft = db.schemaFieldType(list_table_name, fand);
                            if (ft == "int")
                                list_where_params[param_name] = s.toLong();
                            else if (ft == "float")
                                list_where_params[param_name] = s.toFloat();
                            else if (ft == "decimal")
                                list_where_params[param_name] = s.toDecimal();
                            else
                                list_where_params[param_name] = s;
                        }
                        afieldsand[j] = db.qid(fand) + " = @" + param_name;
                    }
                    else
                    {
                        // like match
                        afieldsand[j] = db.qid(fand) + " LIKE @" + param_name;
                        list_where_params[param_name] = like_s;
                    }
                }
                afields[i] = string.Join(" and ", afieldsand);
            }
            list_where += " and (" + string.Join(" or ", afields) + ")";
        }

        setListSearchUserList();

        if (!string.IsNullOrEmpty(related_id) && !string.IsNullOrEmpty(related_field_name))
        {
            list_where += " and " + db.qid(related_field_name) + "=@related_field_name";
            list_where_params["related_field_name"] = related_id;
        }

        setListSearchAdvanced();
    }

    public virtual void setListSearchUserList()
    {
        if (!Utils.isEmpty(list_filter["userlist"]))
        {
            list_where += " and id IN (select ti.item_id from " + db.qid(fw.model<UserLists>().table_items) + " ti where ti.user_lists_id=@user_lists_id and ti.add_users_id=@userId) ";
            list_where_params["user_lists_id"] = db.qi(list_filter["userlist"]);
            list_where_params["userId"] = fw.userId;
        }
    }


    /// <summary>
    /// set list_where based on search[] filter
    ///      - exact: "=term" or just "=" - mean empty
    ///      - Not equals "!=term" or just "!" - means not empty
    ///      - Not contains: "!term"
    ///      - more/less: <=, <, >=, >"
    ///      - and support search by date if search value looks like date in format MM/DD/YYYY
    /// </summary>
    public virtual void setListSearchAdvanced()
    {
        // advanced search
        Hashtable hsearch = list_filter_search;
        foreach (string fieldname in hsearch.Keys)
        {
            string value = hsearch[fieldname].toStr();
            if (string.IsNullOrEmpty(value) || (is_dynamic_index && !view_list_map.ContainsKey(fieldname)))
                continue;


            var qfieldname = db.qid(fieldname);
            var fieldname_sql = $"ISNULL(CAST({qfieldname} as NVARCHAR(255)), '')"; //255 need as SQL Server by default makes only 30
            var fieldname_sql_num = $"TRY_CONVERT(DECIMAL(18,1),CAST({qfieldname} as NVARCHAR))"; // SQL Server 2012+ only
            var fieldname_sql_date = $"TRY_CONVERT(DATE, {qfieldname})"; //for date search

            string op = value[..1];
            string op2 = value.Length >= 2 ? value[..2] : string.Empty;

            string v = value[1..];
            if (op2 == "!=" || op2 == "<=" || op2 == ">=")
                v = value[2..];

            var qv = db.q(v); // quoted value
            if (DateUtils.isDateStr(v))
            {
                //if input looks like a date - compare as date
                fieldname_sql = fieldname_sql_date;
                qv = db.q(DateUtils.Str2SQL(v, fw.userDateFormat));
            }
            else
            {
                if (op2 == "<=" || op == "<" || op2 == ">=" || op == ">")
                {
                    //numerical comparison
                    fieldname_sql = fieldname_sql_num;
                    qv = db.qdec(v).toStr();
                }
            }

            string op_value = op2 switch
            {
                // first - check for 2-char operators
                "!=" => $" <> {qv}",
                "<=" => $" <= {qv}",
                ">=" => $" >= {qv}",
                _ => op switch
                {
                    "=" => $" = {qv}",
                    "<" => $" < {qv}",
                    ">" => $" > {qv}",
                    "!" => $" NOT LIKE {db.q($"%{v}%")}",
                    _ => $" LIKE {db.q($"%{value}%")}",//default is just LIKE/contains
                },// then check for 1-char operators
            };
            list_where += $" AND {fieldname_sql} {op_value}";
        }
    }

    /// <summary>
    /// set list_where filter based on status filter:
    /// - if status not set - filter our deleted (i.e. show all)
    /// - if status set - filter by status, but if status=127 (deleted) only allow to see deleted by admins
    /// </summary>
    public virtual void setListSearchStatus()
    {
        if (!string.IsNullOrEmpty(model0.field_status))
        {
            if (!Utils.isEmpty(this.list_filter["status"]))
            {
                var status = this.list_filter["status"].toInt();
                // if want to see trashed and not admin - just show active
                if (status == FwModel.STATUS_DELETED & !fw.model<Users>().isAccessLevel(Users.ACL_SITEADMIN))
                    status = 0;
                this.list_where += " and " + db.qid(model0.field_status) + "=@status";
                this.list_where_params["status"] = status;
            }
            else
            {
                this.list_where += " and " + db.qid(model0.field_status) + "<>@status";
                this.list_where_params["status"] = FwModel.STATUS_DELETED;// by default - show all non-deleted
            }
        }
    }

    // get count of rows from db
    public virtual void getListCount(string list_view = "")
    {
        string list_view_name = (!string.IsNullOrEmpty(list_view) ? list_view : this.list_view);
        var qlist_view_name = list_view_name.StartsWith('(') ? list_view_name : db.qid(list_view_name); // don't quote if list_view is a subquery (starting with parentheses)

        this.list_count = db.valuep($"select count(*) from {qlist_view_name} where {list_where}", list_where_params).toLong();
    }

    // get list of rows from db
    public virtual DBList getListRowsQuery(int offset = 0, int limit = -1)
    {
        var qlist_view_name = list_view.StartsWith('(') ? list_view : db.qid(list_view); // don't quote if list_view is a subquery (starting with parentheses)

        return db.selectRaw(list_fields, qlist_view_name, list_where, list_where_params, list_orderby, offset, limit);
    }

    public virtual List<string> getListIds(string list_view = "")
    {
        var sql = $"SELECT {model0.field_id} FROM {list_view} WHERE {list_where} ORDER BY {list_orderby}";
        return db.colp(sql, list_where_params);
    }

    /// <summary>
    /// set list fields for db select, based on user-selected headers from config
    /// so we fetch from db only fields that are visible in the list + id field
    /// </summary>
    protected virtual void setListFields()
    {
        // default is "*", override in controller
    }

    /// <summary>
    /// Perform 2 queries to get list of rows.
    /// Set variables:
    /// Me.list_count - count of rows obtained from db
    /// Me.list_rows list of rows
    /// Me.list_pager pager from FormUtils.get_pager
    /// </summary>
    /// <remarks></remarks>
    public virtual void getListRows()
    {
        var is_export = false;
        int pagenum = list_filter["pagenum"].toInt();
        int pagesize = list_filter["pagesize"].toInt();
        // if export requested - start with first page and have a high limit (still better to have a limit just for the case)
        if (export_format.Length > 0)
        {
            is_export = true;
            pagenum = 0;
            pagesize = list_pagesize_export;
        }

        if (string.IsNullOrEmpty(list_view))
            list_view = model0.table_name;

        this.getListCount();
        if (this.list_count > 0)
        {
            int offset = pagenum * pagesize;
            int limit = pagesize;

            this.list_rows = getListRowsQuery(offset, limit);

            model0.normalizeNames(this.list_rows);

            if (!is_export)
                this.list_pager = FormUtils.getPager(this.list_count, pagenum, pagesize);
        }
        else
        {
            this.list_rows = [];
            this.list_pager = [];
        }

        if (related_id.Length > 0)
            Utils.arrayInject(list_rows, new Hashtable() { { "related_id", related_id } });
    }

    public virtual void setFormError(Exception ex)
    {
        //if Validation exception - don't set general error message - specific validation message set in templates
        if (ex is not ValidationException)
            fw.setGlobalError(ex.Message);
    }

    /// <summary>
    /// Add or update records in db (Me.model0)
    /// </summary>
    /// <param name="id">id of the record, 0 if add</param>
    /// <param name="fields">hash of field/values</param>
    /// <returns>new autoincrement id (if added) or old id (if update)</returns>
    /// <remarks>Also set fw.FLASH</remarks>
    public virtual int modelAddOrUpdate(int id, Hashtable fields)
    {
        // make conversions
        // - for date/time fields - convert from user format to UTC/SQL format
        model0.convertUserInput(fields);

        if (id > 0)
        {
            model0.update(id, fields);
            fw.flash("record_updated", 1);
        }
        else
        {
            id = model0.add(fields);
            fw.flash("record_added", 1);
        }
        return id;
    }

    protected FwModel<TRow> ensureTypedModel<TRow>() where TRow : class, new()
    {
        if (model0 is FwModel<TRow> typedModel)
            return typedModel;

        var modelName = model0?.GetType().Name ?? "<null>";
        throw new InvalidOperationException($"Model {modelName} is not typed as {typeof(TRow).FullName}.");
    }

    public virtual int modelAddOrUpdate<TRow>(int id, TRow dto) where TRow : class, new()
    {
        ArgumentNullException.ThrowIfNull(dto);
        var typedModel = ensureTypedModel<TRow>();

        typedModel.convertUserInput(dto);

        if (id > 0)
        {
            typedModel.update(id, dto);
            fw.flash("record_updated", 1);
        }
        else
        {
            id = typedModel.add(dto);
            fw.flash("record_added", 1);
        }

        return id;
    }

    /// <summary>
    /// Return one item from controller's model. For simpler overriding in child controllers.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    public virtual Hashtable modelOneOrFail(int id)
    {
        var item = modelOne(id);
        if (item.Count == 0)
            throw new NotFoundException();

        return item;
    }

    public virtual TRow? modelOneT<TRow>(int id) where TRow : class, new()
    {
        return ensureTypedModel<TRow>().oneT(id);
    }

    public virtual TRow modelOneOrFailT<TRow>(int id) where TRow : class, new()
    {
        return ensureTypedModel<TRow>().oneTOrFail(id);
    }

    // for simpler overriding in child controllers
    public virtual Hashtable modelOne(int id)
    {
        return model0.one(id);
    }

    /// <summary>
    /// return url for afterSave based on:
    /// if return_url set (and no add new form requested) - go to return_url
    /// id:
    ///   - if empty - base_url
    ///   - if >0 - base_url + index/view/new/edit depending on return_to var/param
    ///  also appends:
    ///   - base_url_suffix
    ///   - related_id
    ///   - copy_id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public virtual string afterSaveLocation(string id = "")
    {
        string url;
        string url_q = (!string.IsNullOrEmpty(related_id) ? "&related_id=" + related_id : "");
        var is_add_new = false;

        if (!string.IsNullOrEmpty(id))
        {
            var request_route_return = reqs("route_return");
            var to = (string.IsNullOrEmpty(request_route_return) ? this.route_return : request_route_return);
            if (to == FW.ACTION_SHOW)
                // or return to view screen
                url = this.base_url + "/" + id;
            else if (to == FW.ACTION_INDEX)
                // or return to list screen
                url = this.base_url + "/";
            else if (to == FW.ACTION_SHOW_FORM_NEW)
            {
                // or return to add new form
                url = this.base_url + "/new";
                url_q += "&copy_id=" + id;
                is_add_new = true;
            }
            else
                // default is return to edit screen
                url = this.base_url + "/" + id + "/edit";
        }
        else
            url = this.base_url;

        //preserve return url if present
        if (!string.IsNullOrEmpty(return_url))
            url_q += "&return_url=" + Utils.urlescape(return_url);

        //add base_url_suffix if any
        if (!string.IsNullOrEmpty(base_url_suffix))
            url_q += "&" + base_url_suffix;

        //add tab if any, so after save we can return to the same tab
        if (!string.IsNullOrEmpty(form_tab))
            url_q += "&tab=" + form_tab;

        //add query
        var is_url_q = false;
        if (!string.IsNullOrEmpty(url_q))
        {
            is_url_q = true;
            url_q = Regex.Replace(url_q, @"^\&", ""); // make url clean
            url_q = "?" + url_q;
        }

        string result;
        if (is_add_new || string.IsNullOrEmpty(return_url))
        {
            //if has add new or no specific return_url - just
            result = url + url_q;
        }
        else
        {
            //if has return url - go to it
            if (fw.isJsonExpected())
                // if json - it's usually autosave - don't redirect back to return url yet
                result = url + url_q + (is_url_q ? "&" : "?") + "return_url=" + Utils.urlescape(return_url);
            else
                result = return_url;
        }

        return result;
    }

    /// <summary>
    /// Called from SaveAction/DeleteAction/DeleteMulti or similar.
    /// Return json or route redirect back to ShowForm
    /// or redirect to proper location
    /// </summary>
    /// <param name="success">operation successful or not</param>
    /// <param name="id">item id</param>
    /// <param name="is_new">true if it's newly added item</param>
    /// <param name="action">route redirect to this method if error</param>
    /// <param name="location">redirect to this location if success</param>
    /// <param name="more_json">added to json response</param>
    /// <returns></returns>
    public virtual Hashtable? afterSave(bool success, object? id = null, bool is_new = false, string action = "ShowForm", string location = "", Hashtable? more_json = null)
    {
        if (string.IsNullOrEmpty(location))
            location = this.afterSaveLocation(id.toStr());

        if (fw.isJsonExpected())
        {
            var ps = new Hashtable();
            var _json = new Hashtable()
            {
                {"id",id},
                {"is_new",is_new},
                {"location",location},
                {"success",success}, //legacy TODO DEPRECATE
            };
            if (!success)
            {
                // set error message if any
                _json["err_msg"] = fw.G["err_msg"]; // legacy TODO DEPRECATE
                _json["error"] = new Hashtable() { { "message", fw.G["err_msg"] } };
            }

            // add FormErrors field errors to response if any
            if (fw.FormErrors.Count > 0)
            {
                var error = _json["error"] as Hashtable ?? [];
                error["details"] = fw.FormErrors;
                _json["error"] = error;
            }

            if (more_json != null)
                Utils.mergeHash(_json, more_json);

            ps["_json"] = _json;
            return ps;
        }
        else
        {
            // If save Then success - Return redirect
            // If save Then failed - Return back To add/edit form
            if (success)
                fw.redirect(location);
            else
                fw.routeRedirect(action, [id.toStr()]);
        }
        return null;
    }

    public virtual Hashtable? afterSave(bool success, Hashtable more_json)
    {
        return afterSave(success, "", false, "no_action", "", more_json);
    }

    // called before each controller action (init() already called), check access to current fw.route
    // throw exception if no access
    public virtual void checkAccess()
    {
        //var id = fw.route.id;

        // if user is logged and not SiteAdmin(can access everything)
        // and user's access level is enough for the controller - check access by roles (if enabled)
        int current_user_level = fw.userAccessLevel;
        if (current_user_level >= Users.ACL_VISITOR && current_user_level < Users.ACL_SITEADMIN)
        {
            var action_more = fw.route.action_more;
            if ((fw.route.action == FW.ACTION_SAVE || fw.route.action == FW.ACTION_SHOW_FORM) && Utils.isEmpty(fw.route.id))
            {
                //if save/showform and no id - it's add new - check for Add permission
                action_more = FW.ACTION_MORE_NEW;
            }

            if (!fw.model<Users>().isAccessByRolesResourceAction(fw.userId, fw.route.controller, fw.route.action, action_more, access_actions_to_permissions))
                throw new AuthException("Bad access - Not authorized (3)");
        }
    }

    //called when unhandled error happens in action
    public virtual Hashtable? actionError(Exception? ex, object[] args)
    {
        var edi = ExceptionDispatchInfo.Capture(ex ?? new Exception("Unknown error"));

        Hashtable? ps = null;
        if (fw.isJsonExpected())
        {
            edi.Throw(); //exception will be handled in fw.dispatch() and fw.errMsg() called
        }
        else
        {
            //if not json - redirect to route route_onerror if it's defined
            setFormError(ex ?? new Exception("Unknown error"));

            if (string.IsNullOrEmpty(route_onerror))
                edi.Throw(); //re-throw exception
            else
                fw.routeRedirect(route_onerror, args);
        }
        return ps;
    }

    public virtual Hashtable setPS(Hashtable? ps = null)
    {
        ps ??= [];

        ps["list_user_view"] = this.list_user_view;
        ps["list_headers"] = this.list_headers;
        ps["list_rows"] = this.list_rows;
        ps["count"] = this.list_count;
        ps["pager"] = this.list_pager;
        ps["f"] = this.list_filter;
        ps["related_id"] = this.related_id;
        ps["base_url"] = this.base_url;
        ps["is_userlists"] = this.is_userlists;
        ps["is_readonly"] = is_readonly;
        ps["is_list_edit"] = is_list_edit;
        ps["controller_config"] = this.config;

        //for RBAC
        ps["rbac"] = rbac;

        //implement "Showing FROM to TO of TOTAL records"
        if (this.list_rows != null && this.list_rows.Count > 0)
        {
            int pagenum = list_filter["pagenum"].toInt();
            int pagesize = list_filter["pagesize"].toInt();
            ps["count_from"] = pagenum * pagesize + 1;
            ps["count_to"] = pagenum * pagesize + this.list_rows.Count;
        }

        if (!string.IsNullOrEmpty(this.return_url))
            ps["return_url"] = this.return_url; // if not passed - don't override return_url.html

        return ps;
    }

    public virtual bool setUserLists(Hashtable ps, int id = 0)
    {
        // userlists support
        if (id == 0)
            // select only for list screens
            ps["select_userlists"] = fw.model<UserLists>().listSelectByEntity(base_url);
        ps["my_userlists"] = fw.model<UserLists>().listForItem(base_url, id);
        return true;
    }

    // export to csv or html/xls
    public virtual void exportList()
    {
        list_rows ??= [];

        var fields = getViewListUserFields();
        // header names
        ArrayList headers = [];
        foreach (var fld in Utils.qw(fields))
            headers.Add(view_list_map[fld]);

        string csv_export_headers = string.Join(",", headers.ToArray());

        if (export_format == "xls")
            Utils.writeXLSExport(fw, export_filename + ".xls", csv_export_headers, fields, list_rows);
        else
            Utils.writeCSVExport(fw.response, export_filename + ".csv", csv_export_headers, fields, list_rows);
    }

    public virtual void setAddUpdUser(Hashtable ps, Hashtable item)
    {
        if (!string.IsNullOrEmpty(model0.field_add_users_id) && item.ContainsKey(model0.field_add_users_id))
        {
            ps["add_users_id_name"] = fw.model<Users>().iname(item[model0.field_add_users_id].toInt());
        }
        if (!string.IsNullOrEmpty(model0.field_upd_users_id) && item.ContainsKey(model0.field_upd_users_id))
        {
            ps["upd_users_id_name"] = fw.model<Users>().iname(item[model0.field_upd_users_id].toInt());
        }
    }

    // ********************************** dynamic controller support
    // as arraylist of hashtables {field_name=>, field_name_visible=> [, is_checked=>true]} in right order
    // if fields defined - show fields only
    // if is_all true - then show all fields (not only from fields param)
    public virtual ArrayList getViewListArr(string fields = "", bool is_all = false)
    {
        ArrayList result = [];

        // if fields defined - first show these fields, then the rest
        Hashtable fields_added = [];
        if (!string.IsNullOrEmpty(fields))
        {
            foreach (var fieldname in Utils.qw(fields))
            {
                result.Add(new Hashtable()
                {
                    {"field_name",fieldname},
                    {"field_name_visible",view_list_map[fieldname]},
                    {"is_checked",true},
                    {"is_sortable", list_sortmap.ContainsKey(fieldname)}
                });
                fields_added[fieldname] = true;
            }
        }

        if (is_all)
        {
            // rest/all fields
            // sorted by values (visible field name)
            var keys = view_list_map.Keys.Cast<string>().ToArray();
            var values = view_list_map.Values.Cast<string>().ToArray();
            Array.Sort(values, keys);

            foreach (string k in keys)
            {
                // var v = k.Replace("&nbsp;", " ");
                // var parts = v.Split('|', 2);
                // if (parts.Length < 2) throw new ApplicationException("Wrong Format for view_list_map");
                if (fields_added.ContainsKey(k))
                    continue;

                result.Add(new Hashtable()
                {
                    {"field_name",k},
                    {"field_name_visible",view_list_map[k]},
                    {"is_sortable",list_sortmap.ContainsKey(k)}
                });
            }
        }
        return result;
    }

    public virtual Hashtable getViewListSortmap()
    {
        Hashtable result = [];
        foreach (var fieldname in view_list_map.Keys)
            result[fieldname] = fieldname;
        return result;
    }

    public virtual string getViewListUserFields()
    {
        list_user_view ??= fw.model<UserViews>().oneByIcode(UserViews.icodeByUrl(base_url, is_list_edit)); // base_url is screen identifier
        var fields = list_user_view["fields"].toStr();
        return (fields.Length > 0 ? fields : view_list_defaults);
    }

    /// <summary>
    /// Called from setViewList to get conversions for fields.
    /// Currently supports only "date" conversion - i.e. date only fields will be formatted as date only (without time)
    /// Override to add more custom conversions
    /// </summary>
    /// <param name="afields"></param>
    /// <returns></returns>
    public virtual Hashtable getViewListConversions(string[] afields)
    {
        // load schema info to perform specific conversions
        var result = new Hashtable();
        //use table_name or list_view if it's not subquery
        var list_view_name = model0.table_name;
        if (!string.IsNullOrEmpty(list_view) && !list_view.StartsWith('('))
            list_view_name = list_view;

        var table_schema = db.tableSchemaFull(list_view_name);
        foreach (var fieldname in afields)
        {
            var fieldname_lc = fieldname.ToLower();
            if (!table_schema.ContainsKey(fieldname_lc)) continue;

            var field_schema = (Hashtable)table_schema[fieldname_lc]!;
            var fw_type = field_schema["fw_type"].toStr();

            if (fw_type == "date")
            {
                //if field is exactly DATE - show only date part without time and format per user settings
                result[fieldname] = "date";
            }
            else if (fw_type == "datetime")
            {
                // if fields is date and time - we'll format it as date and time per user settings
                result[fieldname] = "datetime";
            }
            // ADD OTHER CONVERSIONS HERE if necessary
        }

        return result;
    }

    /// <summary>
    /// Apply conversions to data for a single view list field
    /// Override to add custom conversions.
    /// </summary>
    /// <param name="fieldname">field name to apply conversion to</param>
    /// <param name="row">data row from db</param>
    /// <param name="hconversions">standard conversion rules from getViewListConversions</param>
    /// <returns></returns>
    public virtual string applyViewListConversions(string fieldname, Hashtable row, Hashtable hconversions)
    {
        var data = row[fieldname].toStr();
        var conversion = hconversions[fieldname].toStr();
        if (conversion == "date")
        {
            data = DateUtils.Str2DateOnly(data, fw.userDateFormat);
        }
        else if (conversion == "datetime")
        {
            data = fw.formatUserDateTime(row[fieldname]);
        }
        return data;
    }

    /// <summary>
    /// set list_headers (and add search_value from list_filter_search) 
    /// and 
    /// </summary>
    /// <param name="is_cols">if true - update list_rows with cols, use false for json responses</param>
    public virtual void setViewList(bool is_cols = true)
    {
        list_user_view = fw.model<UserViews>().oneByIcode(UserViews.icodeByUrl(base_url, is_list_edit));

        var fields = getViewListUserFields();

        list_headers = getViewListArr(fields);
        // add search from user's submit
        foreach (Hashtable header in list_headers)
        {
            var fieldName = header["field_name"].toStr();
            header["search_value"] = list_filter_search?[fieldName];
        }

        if (is_cols)
        {
            var hcustom = Utils.qh(view_list_custom);

            // dynamic cols
            var afields = Utils.qw(fields);

            var hconversions = getViewListConversions(afields);

            foreach (Hashtable row in list_rows)
            {
                ArrayList cols = [];
                foreach (var fieldname in afields)
                {
                    var data = applyViewListConversions(fieldname, row, hconversions);

                    cols.Add(new Hashtable()
                    {
                        {"row",row},
                        {"field_name",fieldname},
                        {"data",data},
                        {"is_custom",hcustom.ContainsKey(fieldname)}
                    });
                }
                row["cols"] = cols;
            }
        }
    }

    protected virtual int saveMultiRows(ICollection keys, bool is_delete, int user_lists_id, int remove_user_lists_id)
    {
        int ctr = 0;
        if (keys == null || keys.Count == 0 || !(is_delete || user_lists_id > 0 || remove_user_lists_id > 0))
            return ctr;

        foreach (string id1 in keys)
        {
            var id = id1.toInt();
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

        return ctr;
    }

    protected virtual void saveMultiResult(int ctr, bool is_delete, int user_lists_id, int remove_user_lists_id)
    {
        if (is_delete)
            fw.flash("multidelete", ctr);
        if (user_lists_id > 0)
            fw.flash("success", ctr + " records added to the list");
        if (remove_user_lists_id > 0)
            fw.flash("success", ctr + " records removed from the list");
    }

    // Default Actions
    //public virtual Hashtable IndexAction()
    //{
    //    logger("in Base controller IndexAction");
    //    return new Hashtable();
    //}

}
