// Fw Controller base class

// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;

namespace osafw
{

    public abstract class FwController
    {
        public static int access_level = Users.ACL_VISITOR; // access level for the controller. fw.config("access_levels") overrides this. -1 (public access), 0(min logged level), 100(max admin level)

        public static string route_default_action = ""; // supported values - "" (use Default Parser for unknown actions), index (use IndexAction for unknown actions), show (assume action is id and use ShowAction)
        public string base_url; // base url for the controller
        public string base_url_suffix; // additional base url suffix

        public Hashtable form_new_defaults;   // optional, defaults for the fields in new form
        public string required_fields;        // optional, default required fields, space-separated
        public string save_fields;            // required, fields to save from the form to db, space-separated
        public string save_fields_checkboxes; // optional, checkboxes fields to save from the form to db, qw string: "field|def_value field2|def_value2"
        public string save_fields_nullable;   // optional, nullable fields that should be set to null in db if form submit as ''

        protected FW fw;
        protected DB db;
        protected FwModel model0;
        protected Hashtable config;                  // controller config, loaded from template dir/config.json

        protected string list_view;                  // table/view to use in list sql, if empty model0.table_name used
        protected string list_orderby;               // orderby for the list screen
        protected Hashtable list_filter;             // filter values for the list screen
        protected Hashtable list_where_params = new();       // any sql params for the list_where
        protected string list_where = " 1=1 ";       // where to use in list sql, default is non-deleted records (see setListSearch() )
        protected int list_count;                    // count of list rows returned from db
        protected ArrayList list_rows;               // list rows returned from db (array of hashes)
        protected ArrayList list_pager;              // pager for the list from FormUtils.getPager
        protected string list_sortdef;               // required for Index, default list sorting: name asc|desc
        protected Hashtable list_sortmap;            // required for Index, sortmap fields
        protected string search_fields;              // optional, search fields, space-separated 
                                                     // fields to search via $s=list_filter["s"), ] - means exact match, not "like"
                                                     // format: "field1 field2,!field3 field4" => field1 LIKE '%$s%' or (field2 LIKE '%$s%' and field3='$s') or field4 LIKE '%$s%'

        // support of customizable view list
        // map of fileld names to screen names
        protected bool is_dynamic_index = false;   // true if controller has dynamic IndexAction, then define below:
        protected string view_list_defaults = "";     // qw list of default columns
        protected Hashtable view_list_map;            // list of all available columns fieldname|visiblename
        protected string view_list_custom = "";       // qw list of custom-formatted fields for the list_table

        protected bool is_dynamic_show = false;    // true if controller has dynamic ShowAction, requires "show_fields" to be defined in config.json
        protected bool is_dynamic_showform = false; // true if controller has dynamic ShowFormAction, requires "showform_fields" to be defined in config.json

        protected bool is_userlists = false;       // true if controller should support UserLists

        protected string return_url;                 // url to return after SaveAction successfully completed, passed via request
        protected string related_id;                 // related id, passed via request. Controller should limit view to items related to this id
        protected string related_field_name;         // if set (in Controller) and $related_id passed - list will be filtered on this field


        protected FwController(FW fw = null)
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

            return_url = reqs("return_url");
            related_id = reqs("related_id");
        }

        // load controller config from json in template dir (based on base_url)
        public virtual void loadControllerConfig(string config_filename = "config.json")
        {
            var conf_file0 = base_url.ToLower() + "/" + config_filename;
            var conf_file = fw.config("template") + "/" + conf_file0;
            if (!System.IO.File.Exists(conf_file))
                throw new ApplicationException("Controller Config file not found in templates: " + conf_file0);

            this.config = (Hashtable)Utils.jsonDecode(FW.getFileContent(conf_file));
            if (this.config == null)
                throw new ApplicationException("Controller Config is invalid, check json in templates: " + conf_file0);
            // logger("loaded config:")
            // logger(Me.config)

            var model_name = Utils.f2str(this.config["model"]);
            if (!string.IsNullOrEmpty(model_name))
                model0 = fw.model(model_name);

            // check/conv to str
            required_fields = Utils.f2str(this.config["required_fields"]);
            is_userlists = Utils.f2bool(this.config["is_userlists"]);

            // save_fields could be defined as qw string - check and convert
            var save_fields_raw = this.config["save_fields"];
            if (save_fields_raw is IList list)
                save_fields = Utils.qwRevert(list); // not optimal, but simplest for now
            else
                save_fields = Utils.f2str(save_fields_raw);

            form_new_defaults = (Hashtable)this.config["form_new_defaults"];

            // save_fields_checkboxes could be defined as qw string - check and convert
            var save_fields_checkboxes_raw = this.config["save_fields_checkboxes"];
            if (save_fields_checkboxes_raw is IDictionary dictionary)
                save_fields_checkboxes = Utils.qhRevert(dictionary); // not optimal, but simplest for now
            else
                save_fields_checkboxes = Utils.f2str(save_fields_checkboxes_raw);

            // save_fields_nullable could be defined as qw string - check and convert
            var save_fields_nullable_raw = this.config["save_fields_nullable"];
            if (save_fields_nullable_raw is IList list1)
                save_fields_nullable = Utils.qwRevert(list1); // not optimal, but simplest for now
            else
                save_fields_nullable = Utils.f2str(save_fields_nullable_raw);

            search_fields = Utils.f2str(this.config["search_fields"]);
            list_sortdef = Utils.f2str(this.config["list_sortdef"]);

            var list_sortmap_raw = this.config["list_sortmap"];
            if (list_sortmap_raw is IDictionary)
                list_sortmap = (Hashtable)list_sortmap_raw;
            else
                list_sortmap = Utils.qh(Utils.f2str(this.config["list_sortmap"]));

            related_field_name = Utils.f2str(this.config["related_field_name"]);

            list_view = Utils.f2str(this.config["list_view"]);

            is_dynamic_index = Utils.f2bool(this.config["is_dynamic_index"]);
            if (is_dynamic_index)
            {
                // Whoah! list view is dynamic
                view_list_defaults = Utils.f2str(this.config["view_list_defaults"]);

                // since view_list_map could be defined as qw string or as hashtable - check and convert
                var raw_view_list_map = this.config["view_list_map"];
                if (raw_view_list_map is IDictionary)
                    view_list_map = (Hashtable)raw_view_list_map;
                else
                    view_list_map = Utils.qh((string)raw_view_list_map);

                view_list_custom = Utils.f2str(this.config["view_list_custom"]);

                if (list_sortmap.Count == 0)
                    list_sortmap = getViewListSortmap(); // just add all fields from view_list_map if no list_sortmap in config
                if (search_fields == "")
                    search_fields = getViewListUserFields(); // just search in all visible fields if no specific fields defined
            }

            is_dynamic_show = Utils.f2bool(this.config["is_dynamic_show"]);
            is_dynamic_showform = Utils.f2bool(this.config["is_dynamic_showform"]);
        }

        /// <summary>
        /// return true if current request is GET request
        /// </summary>
        /// <returns></returns>
        public bool isGet()
        {
            return (fw.route.method == "GET");
        }

        // set of helper functions to return string, integer, date values from request (fw.FORM)
        public object req(string iname)
        {
            return fw.FORM[iname];
        }
        public Hashtable reqh(string iname)
        {
            if (fw.FORM[iname] != null && fw.FORM[iname].GetType() == typeof(Hashtable))
                return (Hashtable)fw.FORM[iname];
            else
                return new Hashtable();
        }

        public string reqs(string iname)
        {
            string value = (string)fw.FORM[iname] ?? "";
            return value;
        }
        public int reqi(string iname)
        {
            return Utils.f2int(fw.FORM[iname]);
        }
        public object reqd(string iname)
        {
            return Utils.f2date(fw.FORM[iname]);
        }

        public void rw(string str)
        {
            fw.rw(str);
        }

        // methods from fw - just for a covenience, so no need to use "fw.", as they are used quite frequently
        public void logger(params object[] args)
        {
            if (args.Length == 0)
                return;
            fw._logger(LogLevel.DEBUG, ref args);
        }
        public void logger(LogLevel level, params object[] args)
        {
            if (args.Length == 0)
                return;
            fw._logger(level, ref args);
        }

        public virtual void checkXSS()
        {
            if (fw.Session("XSS") != (string)fw.FORM["XSS"])
                throw new AuthException("XSS Error. Reload the page or try to re-login");
        }

        // return hashtable of filter values
        // NOTE: automatically set to defaults - pagenum=0 and pagesize=MAX_PAGE_ITEMS
        // NOTE: if request param 'dofilter' passed - session filters cleaned
        // sample in IndexAction: me.get_filter()
        public virtual Hashtable initFilter(string session_key = null)
        {
            Hashtable f = (Hashtable)fw.FORM["f"] ?? new();

            if (session_key == null)
                session_key = "_filter_" + fw.G["controller.action"];

            Hashtable sfilter = fw.SessionHashtable(session_key);
            if (sfilter == null || !(sfilter is Hashtable))
                sfilter = new Hashtable();

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
                    Hashtable uf = fw.model<UserFilters>().one(userfilters_id).toHashtable();
                    Hashtable f1 = (Hashtable)Utils.jsonDecode(uf["idesc"]);
                    if (f1 != null)
                        f = f1;
                    if (Utils.f2int(uf["is_system"]) == 0)
                    {
                        f["userfilters_id"] = userfilters_id; // set filter id (for edit/delete) only if not system
                        f["userfilter"] = uf;
                    }
                }
                else
                {
                    // check if we have some filter loaded
                    userfilters_id = Utils.f2int(f["userfilters_id"]);
                    if (userfilters_id > 0)
                    {
                        // just ned info on this filter
                        var uf = fw.model<UserFilters>().one(userfilters_id);
                        f["userfilter"] = uf;
                    }
                }
            }

            // paging
            f["pagenum"] = Utils.f2int(f["pagenum"]); //sets default to 0 if no value or non-numeric
            f["pagesize"] = Utils.f2int(f["pagesize"] ?? fw.config("MAX_PAGE_ITEMS"));

            // save in session for later use
            fw.SessionHashtable(session_key, f);

            this.list_filter = f;
            return f;
        }

        /// <summary>
        /// clears list_filter and related session key
        /// </summary>
        /// <param name="session_key"></param>
        public virtual void clearFilter(string session_key = null)
        {
            Hashtable f = new();
            if (session_key == null)
                session_key = "_filter_" + fw.G["controller.action"];
            fw.SessionHashtable(session_key, f);
            this.list_filter = f;
        }

        /// <summary>
        /// Validate required fields are non-empty and set global fw.ERR[field] values in case of errors
        /// </summary>
        /// <param name="item">fields/values to validate</param>
        /// <param name="fields">field names required to be non-empty (trim used)</param>
        /// <returns>true if all required field names non-empty</returns>
        /// <remarks>also set global fw.ERR[REQUIRED]=true in case of validation error</remarks>
        public virtual bool validateRequired(Hashtable item, Array fields)
        {
            bool result = true;
            if (item != null && fields.Length > 0)
            {
                foreach (string fld in fields)
                {
                    if (!string.IsNullOrEmpty(fld) && (!item.ContainsKey(fld) || ((string)item[fld]).Trim() == ""))
                    {
                        result = false;
                        fw.FormErrors[fld] = true;
                    }
                }
            }
            else
                result = false;
            if (!result)
                fw.FormErrors["REQUIRED"] = true;
            return result;
        }
        // same as above but fields param passed as a qw string
        public virtual bool validateRequired(Hashtable item, string fields)
        {
            return validateRequired(item, Utils.qw(fields));
        }

        /// <summary>
        /// Check validation result (validate_required)
        /// </summary>
        /// <param name="result">to use from external validation check</param>
        /// <remarks>throw ValidationException exception if global ERR non-empty.
        /// Also set global ERR[INVALID] if ERR non-empty, but ERR[REQUIRED] not true
        /// </remarks>
        public virtual void validateCheckResult(bool result = true)
        {
            if (fw.FormErrors.ContainsKey("REQUIRED") && (bool)fw.FormErrors["REQUIRED"])
                result = false;

            if (fw.FormErrors.Count > 0 && (!fw.FormErrors.ContainsKey("REQUIRED") || !(bool)fw.FormErrors["REQUIRED"]))
            {
                fw.FormErrors["INVALID"] = true;
                result = false;
            }

            if (!result)
                throw new ValidationException();
        }

        /// <summary>
        /// Set list sorting fields - Me.list_orderby according to Me.list_filter filter and Me.list_sortmap and Me.list_sortdef
        /// </summary>
        /// <remarks></remarks>
        public virtual void setListSorting()
        {
            if (this.list_sortdef == null)
                throw new Exception("No default sort order defined, define in list_sortdef ");
            if (this.list_sortmap == null)
                throw new Exception("No sort order mapping defined, define in list_sortmap ");

            string sortby = (string)this.list_filter["sortby"] ?? "";
            string sortdir = (string)this.list_filter["sortdir"] ?? "";

            string sortdef_field = null;
            string sortdef_dir = null;
            Utils.split2(" ", this.list_sortdef, ref sortdef_field, ref sortdef_dir);

            // validation/mapping
            if (string.IsNullOrEmpty(sortby))
                sortby = sortdef_field;
            if (sortdir != "desc" && sortdir != "asc")
                sortdir = sortdef_dir;

            string orderby = ((string)this.list_sortmap[sortby] ?? "").Trim();
            if (string.IsNullOrEmpty(orderby))
                throw new Exception("No orderby defined for [" + sortby + "], define in list_sortmap");

            this.list_filter["sortby"] = sortby;
            this.list_filter["sortdir"] = sortdir;

            if (sortdir == "desc")
            {
                // if sortdir is desc, i.e. opposite to default - invert order for orderby fields
                // go thru each order field
                string[] aorderby = Strings.Split(orderby, ",");
                for (int i = 0; i <= aorderby.Length - 1; i++)
                {
                    string field = null;
                    string order = null;
                    Utils.split2(@"\s+", Strings.Trim(aorderby[i]), ref field, ref order);

                    if (order == "desc")
                        order = "asc";
                    else
                        order = "desc";
                    aorderby[i] = db.q_ident(field) + " " + order;
                }
                orderby = Strings.Join(aorderby, ", ");
            }
            else
            {
                // quote
                string[] aorderby = Strings.Split(orderby, ",");
                for (int i = 0; i <= aorderby.Length - 1; i++)
                {
                    string field = null;
                    string order = null;
                    Utils.split2(@"\s+", Strings.Trim(aorderby[i]), ref field, ref order);
                    aorderby[i] = db.q_ident(field) + " " + order;
                }
                orderby = Strings.Join(aorderby, ", ");
            }
            this.list_orderby = orderby;
        }

        /// <summary>
        /// Add to Me.list_where search conditions from Me.list_filter["s") ]nd based on fields in Me.search_fields
        /// </summary>
        /// <remarks>Sample: Me.search_fields="field1 field2,!field3 field4" => field1 LIKE '%$s%' or (field2 LIKE '%$s%' and field3='$s') or field4 LIKE '%$s%'</remarks>
        public virtual void setListSearch()
        {
            string s = ((string)this.list_filter["s"] ?? "").Trim();
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
                    string[] afieldsand = Strings.Split(afields[i], ","); // AND fields delimited by comma

                    for (int j = 0; j <= afieldsand.Length - 1; j++)
                    {
                        string fand = afieldsand[j];
                        string param_name = "list_search_" + i + "_" + j;
                        if (fand.Substring(0, 1) == "!")
                        {
                            // exact match
                            fand = fand.Substring(1);
                            if (is_subquery)
                            {
                                // for subqueries - just use string quoting, but convert to number (so only numeric search supported in this case)
                                list_where_params[param_name] = Utils.f2long(s);
                                afieldsand[j] = db.q_ident(fand) + " = @" + param_name;
                            }
                            else
                            {
                                var ft = db.schema_field_type(list_table_name, fand);
                                if (ft == "int")
                                    list_where_params[param_name] = Utils.f2long(s);
                                else if (ft == "float")
                                    list_where_params[param_name] = Utils.f2float(s);
                                else
                                    list_where_params[param_name] = s;
                            }
                            afieldsand[j] = db.q_ident(fand) + " = @" + param_name;
                        }
                        else
                        {
                            // like match
                            afieldsand[j] = db.q_ident(fand) + " LIKE @" + param_name;
                            list_where_params[param_name] = like_s;
                        }
                    }
                    afields[i] = Strings.Join(afieldsand, " and ");
                }
                list_where += " and (" + Strings.Join(afields, " or ") + ")";
            }

            if (!string.IsNullOrEmpty((string)list_filter["userlist"]))
                this.list_where += " and id IN (select ti.item_id from " + fw.model<UserLists>().table_items + " ti where ti.user_lists_id=" + db.qi(list_filter["userlist"]) + " and ti.add_users_id=" + db.qi(fw.userId) + " ) ";

            if (!string.IsNullOrEmpty(related_id) && !string.IsNullOrEmpty(related_field_name))
            {
                list_where += " and " + db.q_ident(related_field_name) + "=@related_field_name";
                list_where_params["related_field_name"] = related_id;
            }

            setListSearchAdvanced();
        }

        /// <summary>
        /// set list_where based on search[] filter
        ///      - exact: "=term"
        ///      - Not equals "!=term"
        ///      - Not contains: "!term"
        ///      - more/less: <=, <, >=, >"
        /// </summary>
        public virtual void setListSearchAdvanced()
        {
            // advanced search
            Hashtable hsearch = reqh("search");
            foreach (string fieldname in hsearch.Keys)
            {
                if (!string.IsNullOrEmpty((string)hsearch[fieldname]) && (!is_dynamic_index || view_list_map.ContainsKey(fieldname)))
                {
                    string value = (string)hsearch[fieldname];
                    string str;
                    var fieldname_sql = "ISNULL(CAST(" + db.q_ident(fieldname) + " as NVARCHAR), '')";
                    var fieldname_sql2 = "TRY_CONVERT(DECIMAL(18,1),CAST(" + db.q_ident(fieldname) + " as NVARCHAR))"; // SQL Server 2012+ only
                    if (value.Substring(0, 1) == "=")
                        str = " = " + db.q(value[1..]);
                    else if (value.Substring(0, 2) == "!=")
                        str = " <> " + db.q(value[2..]);
                    else if (value.Substring(0, 2) == "<=")
                    {
                        fieldname_sql = fieldname_sql2;
                        str = " <= " + db.q(value[2..]);
                    }
                    else if (value.Substring(0, 1) == "<")
                    {
                        fieldname_sql = fieldname_sql2;
                        str = " < " + db.q(value[1..]);
                    }
                    else if (value.Substring(0, 2) == ">=")
                    {
                        fieldname_sql = fieldname_sql2;
                        str = " >= " + db.q(value[2..]);
                    }
                    else if (value.Substring(0, 1) == ">")
                    {
                        fieldname_sql = fieldname_sql2;
                        str = " > " + db.q(value[1..]);
                    }
                    else if (value.Substring(0, 1) == "!")
                        str = " NOT LIKE " + db.q("%" + value[1..] + "%");
                    else
                        str = " LIKE " + db.q("%" + value + "%");

                    this.list_where += " and " + fieldname_sql + " " + str;
                }
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
                if (!string.IsNullOrEmpty((string)this.list_filter["status"]))
                {
                    var status = Utils.f2int(this.list_filter["status"]);
                    // if want to see trashed and not admin - just show active
                    if (status == 127 & !fw.model<Users>().checkAccess(Users.ACL_SITEADMIN, false))
                        status = 0;
                    this.list_where += " and " + db.q_ident(model0.field_status) + "=" + db.qi(status);
                }
                else
                    this.list_where += " and " + db.q_ident(model0.field_status) + "<>127 ";// by default - show all non-deleted
            }
        }

        public virtual void getListCount(string list_view = "")
        {
            string list_view_name = (!string.IsNullOrEmpty(list_view) ? list_view : this.list_view);
            this.list_count = (int)db.valuep("select count(*) from " + list_view_name + " where " + this.list_where, this.list_where_params);
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
            int pagenum = Utils.f2int(list_filter["pagenum"]);
            int pagesize = Utils.f2int(list_filter["pagesize"]);
            // if export requested - start with first page and have a high limit (still better to have a limit just for the case)
            if (reqs("export").Length > 0)
            {
                is_export = true;
                pagenum = 0;
                pagesize = 100000;
            }


            if (string.IsNullOrEmpty(list_view))
                list_view = model0.table_name;
            var list_view_name = (list_view.Substring(0, 1) == "(" ? list_view : db.q_ident(list_view)); // don't quote if list_view is a subquery (starting with parentheses)

            this.getListCount(list_view_name);
            if (this.list_count > 0)
            {
                int offset = pagenum * pagesize;
                int limit = pagesize;

                string sql;

                if (db.dbtype == "SQL")
                {
                    // for SQL Server 2012+
                    sql = "SELECT * FROM " + list_view_name + " WHERE " + this.list_where + " ORDER BY " + this.list_orderby + " OFFSET " + offset + " ROWS " + " FETCH NEXT " + limit + " ROWS ONLY";
                    this.list_rows = db.arrayp(sql, list_where_params);
                }
                else if (db.dbtype == "OLE")
                {
                    // OLE - for Access - emulate using TOP and return just a limit portion (bad perfomance, but no way)
                    sql = "SELECT TOP " + (offset + limit) + " * FROM " + list_view_name + " WHERE " + this.list_where + " ORDER BY " + this.list_orderby;
                    var rows = db.arrayp(sql, list_where_params);
                    if (offset >= rows.Count)
                        // offset too far
                        this.list_rows = new ArrayList();
                    else
                        this.list_rows = (DBList)rows.GetRange(offset, Math.Min(limit, rows.Count - offset));
                }
                else
                    throw new ApplicationException("Unsupported db type");
                model0.normalizeNames(this.list_rows);

                // for 2005<= SQL Server versions <2012
                // offset+1 because _RowNumber starts from 1
                // Dim sql As String = "SELECT * FROM (" &
                // "   SELECT *, ROW_NUMBER() OVER (ORDER BY " & Me.list_orderby & ") AS _RowNumber" &
                // "   FROM " & list_view &
                // "   WHERE " & Me.list_where &
                // ") tmp WHERE _RowNumber BETWEEN " & (offset + 1) & " AND " & (offset + 1 + limit - 1)

                // for MySQL this would be much simplier
                // sql = "SELECT * FROM model0.table_name WHERE Me.list_where ORDER BY Me.list_orderby LIMIT offset, limit";


                if (!is_export)
                    this.list_pager = FormUtils.getPager(this.list_count, pagenum, pagesize);
            }
            else
            {
                this.list_rows = new ArrayList();
                this.list_pager = new ArrayList();
            }

            if (related_id.Length > 0)
                Utils.arrayInject(list_rows, new Hashtable() { { "related_id", related_id } });
        }

        public virtual void setFormError(Exception ex)
        {
            // if Validation exception - don't set general error message - specific validation message set in templates
            if (!(ex is ValidationException))
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

        public virtual string getReturnLocation(string id = "")
        {
            string url;
            string url_q = (!string.IsNullOrEmpty(related_id) ? "&related_id=" + related_id : "");
            var is_add_new = reqi("is_add_more");

            if (!string.IsNullOrEmpty(id))
            {
                if (is_add_new > 0)
                {
                    // if Submit and Add New - redirect to new
                    url = this.base_url + "/new";
                    url_q = "&copy_id=" + id;
                }
                else
                    // or just return to edit screen
                    url = this.base_url + "/" + id + "/edit";
            }
            else
                url = this.base_url;

            if (!string.IsNullOrEmpty(base_url_suffix))
                url_q += "&" + base_url_suffix;

            if (!string.IsNullOrEmpty(url_q))
            {
                url_q = Regex.Replace(url_q, @"^\&", ""); // make url clean
                url_q = "?" + url_q;
            }

            string result;
            if (is_add_new != 1 && !string.IsNullOrEmpty(return_url))
            {
                if (fw.isJsonExpected())
                    // if json - it's usually autosave - don't redirect back to return url yet
                    result = url + url_q + (!string.IsNullOrEmpty(url_q) ? "&" : "?") + "return_url=" + Utils.urlescape(return_url);
                else
                    result = return_url;
            }
            else
                result = url + url_q;

            return result;
        }

        /// <summary>
        /// Called from SaveAction/DeleteAction/DeleteMulti or similar. Return json or route redirect back to ShowForm or redirect to proper location
        /// </summary>
        /// <param name="success">operation successful or not</param>
        /// <param name="id">item id</param>
        /// <param name="is_new">true if it's newly added item</param>
        /// <param name="action">route redirect to this method if error</param>
        /// <param name="location">redirect to this location if success</param>
        /// <param name="more_json">added to json response</param>
        /// <returns></returns>
        public virtual Hashtable afterSave(bool success, object id = null, bool is_new = false, string action = "ShowForm", string location = "", Hashtable more_json = null)
        {
            if (string.IsNullOrEmpty(location))
                location = this.getReturnLocation(Utils.f2str(id));

            if (fw.isJsonExpected())
            {
                var ps = new Hashtable();
                var _json = new Hashtable()
                {
                    {"success",success},
                    {"id",id},
                    {"is_new",is_new},
                    {"location",location},
                    {"err_msg",fw.G["err_msg"]}
                };
                // add ERR field errors to response if any
                if (fw.FormErrors.Count > 0)
                    _json["ERR"] = fw.FormErrors;

                if (more_json != null)
                    Utils.mergeHash(_json, more_json);

                ps["_json"] = _json;
                return ps;
            }
            else
                // If save Then success - Return redirect
                // If save Then failed - Return back To add/edit form
                if (success)
                fw.redirect(location);
            else
                fw.routeRedirect(action, new[] { id.ToString() });
            return null;
        }

        public virtual Hashtable afterSave(bool success, Hashtable more_json)
        {
            return afterSave(success, "", false, "no_action", "", more_json);
        }

        public virtual Hashtable setPS(Hashtable ps = null)
        {
            if (ps == null)
                ps = new Hashtable();

            ps["list_rows"] = this.list_rows;
            ps["count"] = this.list_count;
            ps["pager"] = this.list_pager;
            ps["f"] = this.list_filter;
            ps["related_id"] = this.related_id;
            ps["base_url"] = this.base_url;
            ps["is_userlists"] = this.is_userlists;

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
            if (list_rows == null)
                list_rows = new ArrayList();

            var fields = getViewListUserFields();
            // header names
            ArrayList headers = new();
            foreach (var fld in Utils.qw(fields))
                headers.Add(view_list_map[fld]);

            string csv_export_headers = string.Join(",", headers.ToArray());

            if (reqs("export") == "xls")
                Utils.writeXLSExport(fw, "export.xls", csv_export_headers, fields, list_rows);
            else
                Utils.writeCSVExport(fw.response, "export.csv", csv_export_headers, fields, list_rows);
        }

        public virtual void setAddUpdUser(Hashtable ps, Hashtable item)
        {
            if (!string.IsNullOrEmpty(model0.field_add_users_id))
                ps["add_users_id_name"] = fw.model<Users>().iname(item[model0.field_add_users_id]);
            if (!string.IsNullOrEmpty(model0.field_upd_users_id))
                ps["upd_users_id_name"] = fw.model<Users>().iname(item[model0.field_upd_users_id]);
        }

        // ********************************** dynamic controller support
        // as arraylist of hashtables {field_name=>, field_name_visible=> [, is_checked=>true]} in right order
        // if fields defined - show fields only
        // if is_all true - then show all fields (not only from fields param)
        public virtual ArrayList getViewListArr(string fields = "", bool is_all = false)
        {
            ArrayList result = new();

            // if fields defined - first show these fields, then the rest
            Hashtable fields_added = new();
            if (!string.IsNullOrEmpty(fields))
            {
                foreach (var fieldname in Utils.qw(fields))
                {
                    result.Add(new Hashtable()
                {
                    {
                        "field_name",fieldname
                    },
                    {
                        "field_name_visible",view_list_map[fieldname]
                    },
                    {
                        "is_checked",true
                    },
                    {
                        "is_sortable", !string.IsNullOrEmpty((string)list_sortmap[fieldname])
                    }
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
                    // Dim v = Replace(k, "&nbsp;", " ")
                    // Dim asub() As String = Split(v, "|", 2)
                    // If UBound(asub) < 1 Then Throw New ApplicationException("Wrong Format for view_list_map")
                    if (fields_added.ContainsKey(k))
                        continue;

                    result.Add(new Hashtable()
                {
                    {
                        "field_name",k
                    },
                    {
                        "field_name_visible",view_list_map[k]
                    },
                    {
                        "is_sortable",string.IsNullOrEmpty((string)list_sortmap[k])
                    }
                });
                }
            }
            return result;
        }

        public virtual Hashtable getViewListSortmap()
        {
            Hashtable result = new();
            foreach (var fieldname in view_list_map.Keys)
                result[fieldname] = fieldname;
            return result;
        }

        public virtual string getViewListUserFields()
        {
            var item = fw.model<UserViews>().oneByIcode(base_url); // base_url is screen identifier
            var fields = (string)item["fields"] ?? "";
            return (fields.Length > 0 ? fields : view_list_defaults);
        }

        // add to ps:
        // headers
        // headers_search
        // depends on ps("list_rows")
        // use is_cols=false when return ps as json
        // usage:
        // model.setViewList(ps, reqh("search"))
        public virtual void setViewList(Hashtable ps, Hashtable hsearch, bool is_cols = true)
        {
            var fields = getViewListUserFields();

            var headers = getViewListArr(fields);
            // add search from user's submit
            foreach (Hashtable header in headers)
                header["search_value"] = hsearch[header["field_name"]];

            ps["headers"] = headers;
            ps["headers_search"] = headers;

            var hcustom = Utils.qh(view_list_custom);

            if (is_cols)
            {
                // dynamic cols
                foreach (Hashtable row in (ArrayList)ps["list_rows"])
                {
                    ArrayList cols = new();
                    foreach (var fieldname in Utils.qw(fields))
                        cols.Add(new Hashtable()
                    {
                        {
                            "row",row
                        },
                        {
                            "field_name",fieldname
                        },
                        {
                            "data",row[fieldname]
                        },
                        {
                            "is_custom",hcustom.ContainsKey(fieldname)
                        }
                    });
                    row["cols"] = cols;
                }
            }
        }

        // Default Actions
        //public virtual Hashtable IndexAction()
        //{
        //    logger("in Base controller IndexAction");
        //    return new Hashtable();
        //}

    }
}
