// Manage  controller for Developers
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021  Oleg Savchuk www.osalabs.com

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic;
using System.Collections;
using System.Text.RegularExpressions;

namespace osafw
{
    public class DevManageController : FwController
    {
        public static new int access_level = Users.ACL_SITEADMIN;

        const string DB_SQL_PATH = "/App_Data/sql/database.sql"; // relative to site_root
        const string DB_JSON_PATH = "/dev/db.json";
        const string ENTITIES_PATH = "/dev/entities.txt";
        const string FW_TABLES = "att_categories att att_table_link users settings spages events event_log lookup_manager_tables user_views user_lists user_lists_items menu_items";

        public override void init(FW fw)
        {
            base.init(fw);
            base_url = "/Dev/Manage";
        }

        public Hashtable IndexAction()
        {
            Hashtable ps = new Hashtable();

            // table and views list
            var tables = db.tables();
            var views = db.views();
            tables.AddRange(views);
            tables.Sort();
            var select_tables = new ArrayList();
            ps["select_tables"] = select_tables;
            foreach (string table in tables)
                select_tables.Add(new Hashtable() { { "id", table }, { "iname", table } });

            // models list - all clasess inherited from FwModel
            var select_models = new ArrayList();
            ps["select_models"] = select_models;

            foreach (string model_name in _models())
                select_models.Add(new Hashtable() { { "id", model_name }, { "iname", model_name } });

            var select_controllers = new ArrayList();
            ps["select_controllers"] = select_controllers;
            foreach (string controller_name in _controllers())
                select_controllers.Add(new Hashtable() { { "id", controller_name }, { "iname", controller_name } });

            return ps;
        }

        public void DumpLogAction()
        {
            var seek = reqi("seek");
            string logpath = (string)fw.config("log");
            rw("Dump of last " + seek + " bytes of the site log");

            var fs = new FileStream(logpath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(-seek, SeekOrigin.End);
            var sr = new StreamReader(fs);
            rw("<pre>");
            fw.rw(sr.ReadToEnd());
            rw("</pre>");

            rw("end of dump");
            sr.Close();
        }

        public void ResetCacheAction()
        {
            fw.flash("success", "Application Caches cleared");

            FwCache.clear();
            db.clear_schema_cache();
            var pp = new ParsePage(fw);
            pp.clear_cache();

            fw.redirect(base_url);
        }

        public void DeleteMenuItemsAction()
        {
            fw.flash("success", "Menu Items cleared");

            db.del("menu_items", new Hashtable());
            FwCache.remove("menu_items");

            fw.redirect(base_url);
        }

        public void ReloadSessionAction()
        {
            fw.flash("success", "Session Reloaded");

            fw.model<Users>().reloadSession();

            fw.redirect(base_url);
        }

        public Hashtable ShowDBUpdatesAction()
        {
            Hashtable ps = new();

            // show list of available db updates
            var updates_root = fw.config("site_root") + @"\App_Data\sql\updates";
            if (System.IO.Directory.Exists(updates_root))
            {
                string[] files = System.IO.Directory.GetFiles(updates_root);

                ArrayList rows = new ArrayList();
                foreach (string file in files)
                    rows.Add(new Hashtable() { { "filename", System.IO.Path.GetFileName(file) } });
                ps["rows"] = rows;
            }
            else
            {
                ps["is_nodir"] = true;
                ps["updates_root"] = updates_root;
            }

            return ps;
        }

        public void SaveDBUpdatesAction()
        {
            checkXSS();

            var is_view_only = (reqi("ViewsOnly") == 1);
            var ctr = 0;

            try
            {
                if (!is_view_only)
                {
                    // apply selected updates
                    var updates_root = fw.config("site_root") + @"\App_Data\sql\updates";
                    var item = reqh("item");
                    foreach (string filename in item.Keys)
                    {
                        var filepath = updates_root + @"\" + filename;
                        rw("applying: " + filepath);
                        ctr += exec_multi_sql(FW.getFileContent(filepath));
                    }
                    rw("Done, " + ctr + " statements executed");
                }

                // refresh views
                ctr = 0;
                var views_file = fw.config("site_root") + @"\App_Data\sql\views.sql";
                rw("Applying views file: " + views_file);
                // for views - ignore errors
                ctr = exec_multi_sql(FW.getFileContent(views_file), true);
                rw("Done, " + ctr + " statements executed");

                rw("<b>All Done</b>");
            }
            catch (Exception ex)
            {
                rw("got an error");
                rw("<span style='color:red'>" + ex.Message + "</span>");
            }

            // and last - reset db schema cache
            FwCache.clear();
            db.clear_schema_cache();
        }
        // TODO move these functions to DB?
        private int exec_multi_sql(string sql, bool is_ignore_errors = false)
        {
            var result = 0;
            // launch the query
            string sql1 = strip_comments_sql(sql);
            String[] asql = split_multi_sql(sql);
            foreach (string sqlone1 in asql)
            {
                var sqlone = sqlone1.Trim();
                if (sqlone.Length > 0)
                {
                    if (is_ignore_errors)
                    {
                        try
                        {
                            db.exec(sqlone);
                            result += 1;
                        }
                        catch (Exception ex)
                        {
                            rw("<span style='color:red'>" + ex.Message + "</span>");
                        }
                    }
                    else
                    {
                        db.exec(sqlone);
                        result += 1;
                    }
                }
            }
            return result;
        }
        private string strip_comments_sql(string sql)
        {
            return Regex.Replace(sql, @"/\*.+?\*/", " ", RegexOptions.Singleline);
        }
        private string[] split_multi_sql(string sql)
        {
            return Regex.Split(sql, @";[\n\r](?:GO[\n\r]+)[\n\r]*|[\n\r]+GO[\n\r]+");
        }



        public void CreateModelAction()
        {
            var item = reqh("item");
            var table_name = item["table_name"].ToString().Trim();
            var model_name = item["model_name"].ToString().Trim();

            Hashtable entity = new()
            {
                { "table", table_name },
                { "model_name", model_name },
                { "db_config", "" }
            };
            createModel(entity);

            fw.flash("success", model_name + ".cs model created");
            fw.redirect(base_url);
        }

        public void CreateControllerAction()
        {
            var item = reqh("item");
            var model_name = item["model_name"].ToString().Trim();
            var controller_url = item["controller_url"].ToString().Trim();
            var controller_title = item["controller_title"].ToString().Trim();

            // emulate entity
            var entity = new Hashtable()
            {
                {
                    "model_name",model_name
                },
                {
                    "controller_url",controller_url
                },
                {
                    "controller_title",controller_title
                },
                {
                    "table",fw.model(model_name).table_name
                }
            };
            // table = Utils.name2fw(model_name) - this is not always ok

            createController(entity, null);
            var controller_name = entity["controller_url"].ToString().Replace("/", "");

            fw.flash("controller_created", controller_name);
            fw.flash("controller_url", entity["controller_url"]);
            fw.redirect(base_url);
        }

        public void ExtractControllerAction()
        {
            var item = reqh("item");
            var controller_name = item["controller_name"].ToString().Trim();

            if (!_controllers().Contains(controller_name))
                throw new ApplicationException("No controller found");

            FwDynamicController cInstance = (FwDynamicController)Activator.CreateInstance(Type.GetType(FW.FW_NAMESPACE_PREFIX + controller_name, true));
            cInstance.init(fw);

            var tpl_to = cInstance.base_url.ToLower();
            var tpl_path = fw.config("template") + tpl_to;
            var config_file = tpl_path + "/config.json";
            var config = loadJson<Hashtable>(config_file);

            // extract ShowAction
            config["is_dynamic_show"] = false;
            Hashtable fitem = new();
            var fields = cInstance.prepareShowFields(fitem, new Hashtable());
            _makeValueTags(fields);

            Hashtable ps = new();
            ps["fields"] = fields;
            ParsePage parser = new(fw);
            string content = parser.parse_page(tpl_to + "/show", "/common/form/show/extract/form.html", ps);
            content = Regex.Replace(content, @"^(?:[\t ]*(?:\r?\n|\r))+", "", RegexOptions.Multiline); // remove empty lines
            FW.setFileContent(tpl_path + "/show/form.html", ref content);

            // extract ShowAction
            config["is_dynamic_showform"] = false;
            fields = cInstance.prepareShowFormFields(fitem, new Hashtable());
            _makeValueTags(fields);
            ps = new();
            ps["fields"] = fields;
            parser = new ParsePage(fw);
            content = parser.parse_page(tpl_to + "/show", "/common/form/showform/extract/form.html", ps);
            content = Regex.Replace(content, @"^(?:[\t ]*(?:\r?\n|\r))+", "", RegexOptions.Multiline); // remove empty lines
            content = Regex.Replace(content, "&lt;~(.+?)&gt;", "<~$1>"); // unescape tags
            FW.setFileContent(tpl_path + "/showform/form.html", ref content);

            // 'TODO here - also modify controller code ShowFormAction to include listSelectOptions, multi_datarow, comboForDate, autocomplete name, etc...

            // now we could remove dynamic field definitions - uncomment if necessary
            // config.Remove("show_fields")
            // config.Remove("showform_fields")

            saveJson(config, config_file);

            fw.flash("success", "Controller " + controller_name + " extracted dynamic show/showfrom to static templates");
            fw.redirect(base_url);
        }

        // analyse database tables and create db.json describing entities, fields and relationships
        public Hashtable AnalyseDBAction()
        {
            Hashtable ps = new Hashtable();
            var item = reqh("item");
            string connstr = item["connstr"] + "";

            var dbtype = "SQL";
            if (connstr.Contains("OLE"))
                dbtype = "OLE";

            // Try
            var db = new DB(fw, new Hashtable() { { "connection_string", connstr }, { "type", dbtype } });

            var entities = dbschema2entities(db);

            // save db.json
            saveJson(entities, fw.config("template") + DB_JSON_PATH);

            db.disconnect();
            fw.flash("success", "template" + DB_JSON_PATH + " created");

            // Catch ex As Exception
            // fw.flash("error", ex.Message)
            // fw.redirect(base_url)
            // End Try

            fw.redirect(base_url);

            return ps;
        }


        // ************************* APP CREATION Actions
        // ************************* DB Analyzer
        public Hashtable DBAnalyzerAction()
        {
            Hashtable ps = new();
            ArrayList dbsources = new();

            foreach (string dbname in ((Hashtable)fw.config("db")).Keys)
                dbsources.Add(new Hashtable()
                {
                    {
                        "id",dbname
                    },
                    {
                        "iname",dbname
                    }
                });

            ps["dbsources"] = dbsources;
            return ps;
        }

        public void DBAnalyzerSaveAction()
        {
            var item = reqh("item");
            string dbname = item["db"] + "";
            var dbconfig = ((Hashtable)fw.config("db"))[dbname];
            if (dbconfig == null)
                throw new ApplicationException("Wrong DB selection");

            createDBJsonFromExistingDB(dbname);
            fw.flash("success", "template" + DB_JSON_PATH + " created");

            fw.redirect(base_url + "/(AppCreator)");
        }

        public Hashtable EntityBuilderAction()
        {
            Hashtable ps = new Hashtable();

            var entities_file = fw.config("template") + ENTITIES_PATH;
            Hashtable item = new Hashtable();
            item["entities"] = FW.getFileContent(entities_file);
            ps["i"] = item;

            return ps;
        }

        public void EntityBuilderSaveAction()
        {
            var item = reqh("item");
            var is_create_all = reqi("DoMagic") == 1;

            var entities_file = fw.config("template") + ENTITIES_PATH;
            string filedata = (string)item["entities"];
            FW.setFileContent(entities_file, ref filedata);

            try
            {
                if (is_create_all)
                {
                    // TODO create db.json, db, models/controllers
                    createDBJsonFromText((string)item["entities"]);
                    createDBFromDBJson();
                    createDBSQLFromDBJson();
                    createModelsAndControllersFromDBJson();

                    fw.flash("success", "Application created");
                }
                else
                {
                    // create db.json only
                    createDBJsonFromText((string)item["entities"]);
                    fw.flash("success", "template" + DB_JSON_PATH + " created");
                    fw.redirect(base_url + "/(DBInitializer)");
                }
            }
            catch (ApplicationException ex)
            {
                fw.flash("error", ex.Message);
            }

            fw.redirect(base_url + "/(EntityBuilder)");
        }

        public Hashtable DBInitializerAction()
        {
            Hashtable ps = new Hashtable();

            var config_file = fw.config("template") + DB_JSON_PATH;
            var entities = loadJson<ArrayList>(config_file);

            ps["tables"] = entities;

            return ps;
        }

        public void DBInitializerSaveAction()
        {
            var is_sql_only = reqi("DoSQL") == 1;

            if (is_sql_only)
            {
                createDBSQLFromDBJson();
                fw.flash("success", DB_SQL_PATH + " created");

                fw.redirect(base_url + "/(DBInitializer)");
            }
            else
            {
                createDBFromDBJson();
                fw.flash("success", "DB tables created");

                fw.redirect(base_url + "/(AppCreator)");
            }
        }

        public Hashtable AppCreatorAction()
        {
            // reload session, so sidebar menu will be updated
            if (reqs("reload").Length > 0)
                fw.model<Users>().reloadSession();

            Hashtable ps = new();

            // tables
            var config_file = fw.config("template") + DB_JSON_PATH;
            var entities = loadJson<ArrayList>(config_file);

            var models = _models();
            var controllers = _controllers();

            foreach (Hashtable entity in entities)
            {
                entity["is_model_exists"] = _models().Contains(entity["model_name"]);
                entity["controller_name"] = entity["controller_url"].ToString().Replace("/", "");
                entity["is_controller_exists"] = _controllers().Contains(entity["controller_name"] + "Controller");
            }

            ps["entities"] = entities;
            return ps;
        }

        public void AppCreatorSaveAction()
        {
            var item = reqh("item");

            var config_file = fw.config("template") + DB_JSON_PATH;
            var entities = loadJson<ArrayList>(config_file);

            // go thru entities and:
            // update checked rows for any user input (like model name changed)
            var is_updated = false;
            foreach (Hashtable entity in entities)
            {
                var key = entity["fw_name"] + "#";
                if (item.ContainsKey(key + "is_model"))
                {
                    // create model
                    if (item[key + "model_name"].ToString().Length > 0 && entity["model_name"] != item[key + "model_name"])
                    {
                        is_updated = true;
                        entity["model_name"] = item[key + "model_name"];
                    }
                    this.createModel(entity);
                }

                if (item.ContainsKey(key + "is_controller"))
                {
                    // create controller (model must exists)
                    if (item[key + "controller_name"].ToString().Length > 0 && entity["controller_name"] != item[key + "controller_name"])
                    {
                        is_updated = true;
                        entity["controller_name"] = item[key + "controller_name"];
                    }
                    if (item[key + "controller_title"].ToString().Length > 0 && entity["controller_title"] != item[key + "controller_title"])
                    {
                        is_updated = true;
                        entity["controller_title"] = item[key + "controller_title"];
                    }
                    if (!entity.ContainsKey("controller_is_dynamic_show") || Utils.f2bool(entity["controller_is_dynamic_show"]) != (item[key + "coview"].ToString().Length > 0))
                    {
                        is_updated = true;
                        entity["controller_is_dynamic_show"] = item[key + "coview"].ToString().Length > 0;
                    }
                    if (!entity.ContainsKey("controller_is_dynamic_showform") || Utils.f2bool(entity["controller_is_dynamic_showform"]) != (item[key + "coedit"].ToString().Length > 0))
                    {
                        is_updated = true;
                        entity["controller_is_dynamic_showform"] = item[key + "coedit"].ToString().Length > 0;
                    }
                    if (!entity.ContainsKey("controller_is_lookup") || Utils.f2bool(entity["controller_is_lookup"]) != (item[key + "colookup"].ToString().Length > 0))
                    {
                        is_updated = true;
                        entity["controller_is_lookup"] = item[key + "colookup"].ToString().Length > 0;
                    }
                    this.createController(entity, entities);
                }
            }

            // save db.json if there are any changes
            if (is_updated)
                saveJson(entities, config_file);

            fw.flash("success", "App build successfull");
            fw.redirect(base_url + "/(AppCreator)?reload=1");
        }


        // ****************************** PRIVATE HELPERS (move to Dev model?)

        // load json
        private T loadJson<T>(string filename) where T : new()
        {
            T result;
            result = (T)Utils.jsonDecode(FW.getFileContent(filename));
            if (result == null)
                result = new T();
            return result;
        }

        private void saveJson(object data, string filename)
        {
            var json_str = Utils.jsonEncode(data, true);
            FW.setFileContent(filename, ref json_str);
        }

        private ArrayList dbschema2entities(DB db)
        {
            ArrayList result = new();
            // Access System tables:
            // MSysAccessStorage
            // MSysAccessXML
            // MSysACEs
            // MSysComplexColumns
            // MSysNameMap
            // MSysNavPaneGroupCategories
            // MSysNavPaneGroups
            // MSysNavPaneGroupToObjects
            // MSysNavPaneObjectIDs
            // MSysObjects
            // MSysQueries
            // MSysRelationships
            // MSysResources
            var tables = db.tables();
            foreach (string tblname in tables)
            {
                if (Strings.InStr(tblname, "MSys", CompareMethod.Binary) == 1)
                    continue;

                // get table schema
                var tblschema = db.load_table_schema_full(tblname);
                // logger(tblschema)

                Hashtable table_entity = new Hashtable();
                table_entity["db_config"] = db.db_name;
                table_entity["table"] = tblname;
                table_entity["fw_name"] = Utils.name2fw(tblname); // new table name using fw standards
                table_entity["iname"] = Utils.name2human(tblname); // human table name
                table_entity["fields"] = tableschema2fields(tblschema);
                table_entity["foreign_keys"] = db.get_foreign_keys(tblname);

                table_entity["model_name"] = this._tablename2model((string)table_entity["fw_name"]); // potential Model Name
                table_entity["controller_url"] = "/Admin/" + table_entity["model_name"]; // potential Controller URL/Name/Title
                table_entity["controller_title"] = Utils.name2human((string)table_entity["model_name"]);

                // set is_fw flag - if it's fw compatible (contains id,iname,status,add_time,add_users_id)
                var fields = array2hashtable((ArrayList)table_entity["fields"], "name");
                // AndAlso fields.Contains("iname") 
                table_entity["is_fw"] = fields.Contains("id") && fields.Contains("status") && fields.Contains("add_time") && fields.Contains("add_users_id");
                result.Add(table_entity);
            }

            return result;
        }

        private ArrayList tableschema2fields(ArrayList schema)
        {
            ArrayList result = new ArrayList(schema);

            foreach (Hashtable fldschema in schema)
            {
                // prepare system/human field names: State/Province -> state_province
                // If fldschema("is_identity") = 1 Then
                // fldschema("fw_name") = "id" 'identity fields always id
                // fldschema("iname") = "ID"
                // Else
                fldschema["fw_name"] = Utils.name2fw((string)fldschema["name"]);
                fldschema["iname"] = Utils.name2human((string)fldschema["name"]);
            }
            // result("xxxx") = "yyyy"
            // attrs used to build UI
            // name => iname
            // default
            // maxlen
            // is_nullable
            // type
            // fw_type
            // is_identity

            return result;
        }


        // convert array of hashtables to hashtable of hashtables using key
        private Hashtable array2hashtable(ArrayList arr, string key)
        {
            Hashtable result = new Hashtable();
            foreach (Hashtable item in arr)
                result[item[key]] = item;
            return result;
        }

        private List<string> _models()
        {
            var baseType = typeof(FwModel);
            var assembly = baseType.Assembly;
            return (from t in assembly.GetTypes()
                    where t.IsSubclassOf(baseType)
                    orderby t.Name
                    select t.Name).ToList();
        }

        private List<string> _controllers()
        {
            var baseType = typeof(FwController);
            var assembly = baseType.Assembly;
            return (from t in assembly.GetTypes()
                    where t.IsSubclassOf(baseType)
                    orderby t.Name
                    select t.Name).ToList();
        }

        // replaces strings in all files under defined dir
        // RECURSIVE!
        private void replaceInFiles(string dir, Hashtable strings)
        {
            foreach (string filename in Directory.GetFiles(dir))
                replaceInFile(filename, strings);

            // dive into dirs
            foreach (string foldername in Directory.GetDirectories(dir))
                replaceInFiles(foldername, strings);
        }

        private void replaceInFile(string filepath, Hashtable strings)
        {
            var content = FW.getFileContent(filepath);
            if (content.Length == 0)
                return;

            foreach (string str in strings.Keys)
                content = content.Replace(str, (string)strings[str]);

            FW.setFileContent(filepath, ref content);
        }

        // demo_dicts => DemoDicts
        // TODO actually go thru models and find model with table_name
        private string _tablename2model(string table_name)
        {
            string result = "";
            string[] pieces = Strings.Split(table_name, "_");
            foreach (string piece in pieces)
                result += Utils.capitalize(piece);
            return result;
        }

        private void _makeValueTags(ArrayList fields)
        {
            foreach (Hashtable def in fields)
            {
                var tag = "<~i[" + def["field"] + "]";
                switch (def["type"])
                {
                    case "date":
                        {
                            def["value"] = tag + " date>";
                            break;
                        }

                    case "date_long":
                        {
                            def["value"] = tag + " date=\"long\">";
                            break;
                        }

                    case "float":
                        {
                            def["value"] = tag + " number_format=\"2\">";
                            break;
                        }

                    case "markdown":
                        {
                            def["value"] = tag + " markdown>";
                            break;
                        }

                    case "noescape":
                        {
                            def["value"] = tag + " noescape>";
                            break;
                        }

                    default:
                        {
                            def["value"] = tag + ">";
                            break;
                        }
                }
            }
        }

        private void createDBJsonFromText(string entities_text)
        {
            var entities = new ArrayList();

            var lines = Regex.Split(entities_text, @"[\n\r]+");
            Hashtable table_entity = null;
            foreach (string line1 in lines)
            {
                string line = Regex.Replace(line1, "#.+$", ""); // remove any human comments
                if (line.Trim() == "")
                    continue;
                fw.logger(line);

                if (line.Substring(0, 1) == "-")
                {
                    // if new entity - add system fields to previous entity
                    if (table_entity != null)
                        ((ArrayList)table_entity["fields"]).AddRange(defaultFieldsAfter());

                    // new entity
                    table_entity = new Hashtable();
                    entities.Add(table_entity);

                    line = Regex.Replace(line, @"^-\s*", ""); // remove prefix 'human table name
                    var parts = Regex.Split(line, @"\s+");
                    var table_name = parts[0]; // name is first 

                    table_entity["db_config"] = ""; // main
                    table_entity["iname"] = Utils.name2human(table_name);
                    table_entity["table"] = Utils.name2fw(table_name);
                    if (isFwTableName((string)table_entity["table"]))
                        throw new ApplicationException("Cannot have table name " + table_entity["table"]);

                    table_entity["fw_name"] = Utils.name2fw(table_name); // new table name using fw standards

                    table_entity["model_name"] = this._tablename2model((string)table_entity["fw_name"]); // potential Model Name
                    table_entity["controller_url"] = "/Admin/" + table_entity["model_name"]; // potential Controller URL/Name/Title
                    table_entity["controller_title"] = Utils.name2human((string)table_entity["model_name"]);
                    if (Regex.IsMatch(line, @"\blookup\b"))
                        table_entity["controller_is_lookup"] = true;
                    table_entity["is_fw"] = true;
                    // add default system fields
                    table_entity["fields"] = new ArrayList(defaultFields());
                    table_entity["foreign_keys"] = new ArrayList();
                }
                else
                {
                    // entity field
                    if (table_entity == null)
                        continue; // skip if table_entity is not initialized yet
                    if (line.Substring(0, 3) != "  -")
                        continue; // skip strange things

                    line = Regex.Replace(line, @"^  -\s*", ""); // remove prefix 
                    var parts = Regex.Split(line, @"\s+");
                    var field_name = parts[0]; // name is first 

                    // special - *Address -> set of address fields
                    if (Regex.IsMatch(field_name, "Address$", RegexOptions.IgnoreCase))
                    {
                        ((ArrayList)table_entity["fields"]).AddRange(addressFields(field_name));
                        continue;
                    }

                    Hashtable field = new Hashtable();
                    ((ArrayList)table_entity["fields"]).Add(field);

                    // check if field is foreign key
                    if (field_name.Substring(field_name.Length - 3) == ".id")
                    {
                        // this is foreign key field
                        Hashtable fk = new Hashtable();
                        ((ArrayList)table_entity["foreign_keys"]).Add(fk);

                        fk["pk_table"] = Utils.name2fw(Regex.Replace(field_name, @"\.id$", ""));  // Customers.id => customers
                        fk["pk_column"] = "id";
                        field_name = fk["pk_table"] + "_id";
                        fk["column"] = field_name;

                        field["fw_type"] = "int";
                        field["fw_type"] = "int";
                    }

                    field["name"] = field_name;
                    field["iname"] = Utils.name2human(field_name);
                    field["fw_name"] = Utils.name2fw(field_name);
                    field["is_identity"] = 0;

                    field["is_nullable"] = Regex.IsMatch(line, @"\bNULL\b") ? 1 : 0;
                    field["numeric_precision"] = null;
                    field["maxlen"] = null;
                    // detect type if not yet set by foreigh key
                    if (field["fw_type"].ToString() == "")
                    {
                        field["fw_type"] = "varchar";
                        field["fw_subtype"] = "nvarchar";
                        var m = Regex.Match(line, @"varchar\((.+?)\)"); // detect varchar(LEN|MAX)
                        if (m.Success)
                        {
                            if (m.Groups[1].Value == "MAX" || Utils.f2int(m.Groups[1].Value) > 255)
                                field["maxlen"] = -1;
                            else
                                field["maxlen"] = Utils.f2int(m.Groups[1].Value);
                        }
                        else if (Regex.IsMatch(line, @"\bint\b", RegexOptions.IgnoreCase))
                        {
                            field["numeric_precision"] = 10;
                            field["fw_type"] = "int";
                            field["fw_subtype"] = "int";
                        }
                        else if (Regex.IsMatch(line, @"\btinyint\b", RegexOptions.IgnoreCase))
                        {
                            field["numeric_precision"] = 3;
                            field["fw_type"] = "int";
                            field["fw_subtype"] = "tinyint";
                        }
                        else if (Regex.IsMatch(line, @"\bbit\b", RegexOptions.IgnoreCase))
                        {
                            field["numeric_precision"] = 1;
                            field["fw_type"] = "int";
                            field["fw_subtype"] = "bit";
                        }
                        else if (Regex.IsMatch(line, @"\bfloat\b", RegexOptions.IgnoreCase))
                        {
                            field["numeric_precision"] = 53;
                            field["fw_type"] = "float";
                            field["fw_subtype"] = "float";
                        }
                        else if (Regex.IsMatch(line, @"\bdate\b", RegexOptions.IgnoreCase))
                        {
                            field["fw_type"] = "datetime";
                            field["fw_subtype"] = "date";
                        }
                        else if (Regex.IsMatch(line, @"\bdatetime\b", RegexOptions.IgnoreCase))
                        {
                            field["fw_type"] = "datetime";
                            field["fw_subtype"] = "datetime2";
                        }
                        else
                                                // not type specified
                                                // additionally detect date field from name
                                                if (Regex.IsMatch((string)field["name"], "Date$", RegexOptions.IgnoreCase))
                        {
                            field["fw_type"] = "datetime";
                            field["fw_subtype"] = "date";
                        }
                        else
                            // just a default varchar(255)
                            field["maxlen"] = 255;

                        // default
                        field["default"] = null;
                        m = Regex.Match(line, @"\bdefault\s+\((.+)\)"); // default (VALUE_HERE)
                        if (m.Success)
                            field["default"] = m.Groups[1].Value;
                        else
                            // no default set - then for nvarchar set empty strin gdefault
                            if (field["fw_type"].ToString() == "varchar")
                            field["default"] = "";
                    }
                }
            }
            // add system fields to last entity
            if (table_entity != null)
                ((ArrayList)table_entity["fields"]).AddRange(defaultFieldsAfter());

            // save db.json
            saveJson(entities, fw.config("template") + DB_JSON_PATH);
        }

        private void createDBJsonFromExistingDB(string dbname)
        {
            var db = new DB(fw, (Hashtable)((Hashtable)fw.config("db"))[dbname], dbname);

            var entities = dbschema2entities(db);

            // save db.json
            saveJson(entities, fw.config("template") + DB_JSON_PATH);

            db.disconnect();
        }

        private void createDBFromDBJson()
        {
            var config_file = fw.config("template") + DB_JSON_PATH;
            var entities = loadJson<ArrayList>(config_file);

            // drop all FKs we created before, so we'll be able to drop tables later
            var fks = db.arrayp("SELECT fk.name, o.name as table_name FROM sys.foreign_keys fk, sys.objects o where fk.is_system_named=0 and o.object_id=fk.parent_object_id", DB.h());
            foreach (Hashtable fk in fks)
                db.exec("ALTER TABLE " + db.q_ident((string)fk["table_name"]) + " DROP CONSTRAINT " + db.q_ident((string)fk["name"]));

            foreach (Hashtable entity in entities)
            {
                var sql = entity2SQL(entity);
                // create db tables directly in db

                try
                {
                    db.exec("DROP TABLE " + db.q_ident((string)entity["table"]));
                }
                catch (Exception ex)
                {
                    logger(ex.Message);
                }

                db.exec(sql);
            }
        }

        private void createDBSQLFromDBJson()
        {
            var config_file = fw.config("template") + DB_JSON_PATH;
            var entities = loadJson<ArrayList>(config_file);

            var database_sql = "";
            foreach (Hashtable entity in entities)
            {
                var sql = entity2SQL(entity);
                // only create App_Data/database.sql
                // add drop
                database_sql += "DROP TABLE " + db.q_ident((string)entity["table"]) + ";" + Constants.vbCrLf;
                database_sql += sql + ";" + Constants.vbCrLf + Constants.vbCrLf;
            }

            var sql_file = fw.config("site_root") + DB_SQL_PATH;
            FW.setFileContent(sql_file, ref database_sql);
        }

        private void createModelsAndControllersFromDBJson()
        {
            var config_file = fw.config("template") + DB_JSON_PATH;
            var entities = loadJson<ArrayList>(config_file);

            foreach (Hashtable entity in entities)
            {
                this.createModel(entity);
                this.createController(entity, entities);
            }
        }


        private void createModel(Hashtable entity)
        {
            string table_name = (string)entity["table"];
            string model_name = (string)entity["model_name"];

            if (model_name == "")
                model_name = Utils.nameCamelCase(table_name);
            if (table_name == "" || model_name == "")
                throw new ApplicationException("No table name or no model name");
            // If _models().Contains(model_name) Then Throw New ApplicationException("Such model already exists")

            // copy DemoDicts.cs to model_name.cs
            var path = fw.config("site_root") + @"\App_Code\models";
            var mdemo = FW.getFileContent(path + @"\DemoDicts.cs");
            if (mdemo == "")
                throw new ApplicationException("Can't open DemoDicts.cs");

            // replace: DemoDicts => ModelName, demo_dicts => table_name
            mdemo = mdemo.Replace("DemoDicts", (string)model_name);
            mdemo = mdemo.Replace("demo_dicts", table_name);
            mdemo = mdemo.Replace("db_config = \"\"", "db_config = \"" + entity["db_config"] + "\"");

            // generate code for the model's constructor:
            // set field_*
            var codegen = "";
            if (entity.ContainsKey("fields"))
            {
                var fields = array2hashtable((ArrayList)entity["fields"], "name");

                // detect id and iname fields
                var i = 1;
                Hashtable fld_int = null;
                Hashtable fld_identity = null;
                Hashtable fld_iname = null;
                var is_normalize_names = false;
                foreach (Hashtable fld in (ArrayList)entity["fields"])
                {
                    // find identity
                    if (fld_identity == null && (string)fld["is_identity"] == "1")
                        fld_identity = fld;

                    // first int field
                    if (fld_int == null && (string)fld["fw_type"] == "int")
                        fld_int = fld;

                    // for iname - just use 2nd to 4th field which not end with ID, varchar type and has some maxlen
                    if (fld_iname == null && i >= 2 && i <= 4 && (string)fld["fw_type"] == "varchar" && Utils.f2int(fld["maxlen"]) > 0 && Utils.Right(fld["name"].ToString(), 2).ToLower() != "id")
                        fld_iname = fld;

                    if (Regex.IsMatch((string)fld["name"], @"^[\w_]", RegexOptions.IgnoreCase))
                        // normalize names only if at least one field contains non-alphanumeric chars
                        is_normalize_names = true;

                    i += 1;
                }

                if (fld_identity == null && fld_int != null && fields.Count == 2)
                    // this is looks like lookup table (id/name fields only) without identity - just set id field as first int field
                    fld_identity = fld_int;

                if (fld_iname == null && fld_identity != null)
                    // if no iname field found - just use ID field
                    fld_iname = fld_identity;

                if (fld_identity != null && (string)fld_identity["name"] != "id")
                    codegen += "        field_id = \"" + fld_identity["name"] + "\"" + Constants.vbCrLf;
                if (fld_iname != null && (string)fld_iname["name"] != "iname")
                    codegen += "        field_iname = \"" + fld_iname["name"] + "\"" + Constants.vbCrLf;

                // also reset fw fields if such not exists
                if (!fields.ContainsKey("status"))
                    codegen += "        field_status = \"\"" + Constants.vbCrLf;
                if (!fields.ContainsKey("add_users_id"))
                    codegen += "        field_add_users_id = \"\"" + Constants.vbCrLf;
                if (!fields.ContainsKey("upd_users_id"))
                    codegen += "        field_upd_users_id = \"\"" + Constants.vbCrLf;
                if (!fields.ContainsKey("upd_time"))
                    codegen += "        field_upd_time = \"\"" + Constants.vbCrLf;

                if (!Utils.f2bool(entity["is_fw"]))
                    codegen += "        is_normalize_names = True" + Constants.vbCrLf;

                if (is_normalize_names)
                    codegen += "        is_normalize_names = True" + Constants.vbCrLf;
            }


            mdemo = mdemo.Replace("'###CODEGEN", codegen);

            FW.setFileContent(path + @"\" + model_name + ".cs", ref mdemo);
        }

        private void createLookup(Hashtable entity)
        {
            var ltable = fw.model<LookupManagerTables>().oneByTname((string)entity["table"]);

            string columns = "";
            string column_names = "";
            var fields = this.array2hashtable((ArrayList)entity["fields"], "fw_name");
            if (fields.ContainsKey("icode"))
            {
                columns += (columns.ToString().Length > 0 ? "," : "") + "icode";
                column_names += (column_names.ToString().Length > 0 ? "," : "") + ((Hashtable)fields["icode"])["iname"];
            }
            if (fields.ContainsKey("iname"))
            {
                columns += (columns.ToString().Length > 0 ? "," : "") + "iname";
                column_names += (column_names.ToString().Length > 0 ? "," : "") + ((Hashtable)fields["iname"])["iname"];
            }
            if (fields.ContainsKey("idesc"))
            {
                columns += (columns.ToString().Length > 0 ? "," : "") + "idesc";
                column_names += (column_names.ToString().Length > 0 ? "," : "") + ((Hashtable)fields["idesc"])["iname"];
            }

            Hashtable item = new Hashtable()
            {
                {
                    "tname",entity["table"]
                },
                {
                    "iname",entity["iname"]
                },
                {
                    "columns",columns
                },
                {
                    "column_names",column_names
                }
            };
            if (ltable.Count > 0)
                // replace
                fw.model<LookupManagerTables>().update((int)ltable["id"], item);
            else
                fw.model<LookupManagerTables>().add(item);
        }

        private void createController(Hashtable entity, ArrayList entities)
        {
            string model_name = (string)entity["model_name"];
            string controller_url = (string)entity["controller_url"];
            string controller_title = (string)entity["controller_title"];

            if (controller_url == "")
                controller_url = "/Admin/" + model_name;
            var controller_name = controller_url.Replace("/", "");
            if (controller_title == "")
                controller_title = Utils.name2human(model_name);

            if (model_name == "")
                throw new ApplicationException("No model or no controller name or no title");
            // If _controllers().Contains(controller_name & "Controller") Then Throw New ApplicationException("Such controller already exists")

            // save back to entity as it can be used by caller
            entity["controller_url"] = controller_url;
            entity["controller_title"] = controller_title;

            if (Utils.f2bool(entity["controller_is_lookup"]))
            {
                // if requested controller as a lookup table - just add/update lookup tables, no actual controller creation
                this.createLookup(entity);
                return;
            }

            // copy DemoDicts.cs to model_name.cs
            var path = fw.config("site_root") + @"\App_Code\controllers";
            var mdemo = FW.getFileContent(path + @"\AdminDemosDynamic.cs");
            if (mdemo == "")
                throw new ApplicationException("Can't open AdminDemosDynamic.cs");

            // replace: DemoDicts => ModelName, demo_dicts => table_name
            mdemo = mdemo.Replace("AdminDemosDynamic", controller_name);
            mdemo = mdemo.Replace("/Admin/DemosDynamic", controller_url);
            mdemo = mdemo.Replace("DemoDicts", model_name);
            mdemo = mdemo.Replace("Demos", model_name);

            FW.setFileContent(path + @"\" + controller_name + ".cs", ref mdemo);

            // copy templates from /admin/demosdynamic to /controller/url
            var tpl_from = fw.config("template") + "/admin/demosdynamic";
            var tpl_to = fw.config("template") + controller_url.ToLower();
            Utils.CopyDirectory(tpl_from, tpl_to, true);

            // replace in templates: DemoDynamic to Title
            // replace in url.html /Admin/DemosDynamic to controller_url
            Hashtable replacements = new()
            {
                {
                    "/Admin/DemosDynamic",
                    controller_url
                },
                {
                    "DemoDynamic",
                    controller_title
                }
            };
            replaceInFiles(tpl_to, replacements);

            // update config.json:
            updateControllerConfigJson(entity, tpl_to, entities);

            // add controller to sidebar menu
            updateMenuItem(controller_url, controller_title);
        }

        public void updateControllerConfigJson(Hashtable entity, string tpl_to, ArrayList entities)
        {
            // save_fields - all fields from model table (except id and sytem add_time/user fields)
            // save_fields_checkboxes - empty (TODO based on bit field?)
            // list_view - model.table_name
            // view_list_defaults - iname add_time status
            // view_list_map
            // view_list_custom - just status
            // show_fields - all
            // show_form_fields - all, analyse if:
            // field NOT NULL and no default - required
            // field has foreign key - add that table as dropdown
            var config_file = tpl_to + "/config.json";
            var config = loadJson<Hashtable>(config_file);

            updateControllerConfig(entity, config, entities);

            // Utils.jsonEncode(config) - can't use as it produces unformatted json string
            saveJson(config, config_file);
        }

        public void updateControllerConfig(Hashtable entity, Hashtable config, ArrayList entities)
        {
            string model_name = (string)entity["model_name"];
            string table_name = (string)entity["table"];
            logger("updating config for controller=", entity["controller_url"]);

            Hashtable tables = new(); // hindex by table name to entities
            ArrayList fields = (ArrayList)entity["fields"];
            if (fields == null)
            {
                // TODO deprecate reading from db, always use entity info
                DB db;
                if (entity["db_config"].ToString().Length > 0)
                    db = new DB(fw, (Hashtable)((Hashtable)fw.config("db"))[entity["db_config"]], (string)entity["db_config"]);
                else
                    db = new DB(fw);
                fields = db.load_table_schema_full(table_name);
                if (!entity.ContainsKey("is_fw"))
                    entity["is_fw"] = true; // TODO actually detect if there any fields to be normalized
                var atables = db.tables();
                foreach (string tbl in atables)
                    tables[tbl] = new Hashtable();
            }
            else
                foreach (Hashtable tentity in entities)
                    tables[tentity["table"]] = tentity;

            var is_fw = Utils.f2bool(entity["is_fw"]);
            Hashtable hfields = new Hashtable();
            var sys_fields = Utils.qh("id status add_time add_users_id upd_time upd_users_id");

            ArrayList saveFields = new ArrayList();
            ArrayList saveFieldsNullable = new ArrayList();
            Hashtable hFieldsMap = new Hashtable();   // name => iname
            Hashtable hFieldsMapFW = new Hashtable(); // fw_name => name
            ArrayList showFieldsLeft = new ArrayList();
            ArrayList showFieldsRight = new ArrayList();
            ArrayList showFormFieldsLeft = new ArrayList();
            ArrayList showFormFieldsRight = new ArrayList(); // system fields - to the right

            foreach (Hashtable fld in fields)
            {
                logger("field name=", fld["name"], fld);

                if (fld["fw_name"].ToString() == "")
                    fld["fw_name"] = Utils.name2fw((string)fld["name"]); // system name using fw standards
                if (fld["iname"].ToString() == "")
                    fld["iname"] = Utils.name2human((string)fld["name"]); // human name using fw standards

                hfields[fld["name"]] = fld;
                hFieldsMap[fld["name"]] = fld["iname"];
                if (!is_fw)
                {
                    hFieldsMap[fld["fw_name"]] = fld["iname"];
                    hFieldsMapFW[fld["fw_name"]] = fld["name"];
                }

                Hashtable sf = new Hashtable();  // show fields
                Hashtable sff = new Hashtable(); // showform fields
                var is_skip = false;
                sf["field"] = fld["name"];
                sf["label"] = fld["iname"];
                sf["type"] = "plaintext";

                sff["field"] = fld["name"];
                sff["label"] = fld["iname"];

                if (fld["is_nullable"].ToString() == "0" && fld["default"] == null)
                    sff["required"] = true;// if not nullable and no default - required

                if (fld["is_nullable"].ToString() == "1")
                    saveFieldsNullable.Add(fld["name"]);

                var maxlen = Utils.f2int(fld["maxlen"]);
                if (maxlen > 0)
                    sff["maxlength"] = maxlen;
                if (fld["fw_type"].ToString() == "varchar")
                {
                    if (maxlen <= 0 || fld["name"].ToString() == "idesc")
                    {
                        sf["type"] = "markdown";
                        sff["type"] = "textarea";
                        sff["rows"] = 5;
                        sff["class_control"] = "markdown autoresize"; // or fw-html-editor or fw-html-editor-short
                    }
                    else
                    {
                        // normal text input
                        sff["type"] = "input";
                        if (maxlen < 255)
                        {
                            int col = (int)Math.Round(maxlen / (double)255 * 9 * 4);
                            if (col < 2)
                                col = 2; // minimum - 2
                            if (col > 9)
                                col = 9;
                            sff["class_contents"] = "col-md-" + col;
                        }
                    }
                }
                else if (fld["fw_type"].ToString() == "int")
                {
                    // int fields could be: foreign keys, yes/no, just a number input

                    // check foreign keys - and make type=select
                    var is_fk = false;
                    if (entity.ContainsKey("foreign_keys"))
                    {
                        foreach (Hashtable fkinfo in (ArrayList)entity["foreign_keys"])
                        {
                            if (fkinfo["column"] == fld["name"])
                            {
                                is_fk = true;
                                var mname = _tablename2model(Utils.name2fw((string)fkinfo["pk_table"]));

                                sf["lookup_model"] = mname;
                                // sf["lookup_field"] = "iname"    
                                sf["type"] = "plaintext_link";

                                sff["type"] = "select";
                                sff["lookup_model"] = mname;
                                if (Regex.Replace(fld["default"] + "", @"\D+", "") == "0")
                                    // if default is 0 - allow 0 option
                                    sff["is_option0"] = true;
                                else
                                    sff["is_option_empty"] = true;
                                sff["option0_title"] = "- select -";

                                sff["class_contents"] = "col-md-3";
                                break;
                            }
                        }
                    }

                    if (!is_fk)
                    {
                        if (fld["name"].ToString() == "parent_id")
                        {
                            // special case - parent_id
                            var mname = model_name;

                            sf["lookup_model"] = mname;
                            // sf["lookup_field"] = "iname"
                            sf["type"] = "plaintext_link";

                            sff["type"] = "select";
                            sff["lookup_model"] = mname;
                            sff["is_option0"] = true;
                            sff["class_contents"] = "col-md-3";
                        }
                        else if (fld["fw_subtype"].ToString() == "boolean")
                        {
                            // make it as yes/no radio
                            sff["type"] = "yesno";
                            sff["is_inline"] = true;
                            sff["class_contents"] = "d-flex align-items-center";
                        }
                        else
                        {
                            sff["type"] = "number";
                            sff["min"] = 0;
                            sff["max"] = 999999;
                            sff["class_contents"] = "col-md-3";
                        }
                    }
                }
                else if (fld["fw_type"].ToString() == "float")
                {
                    sff["type"] = "number";
                    sff["step"] = 0.1;
                    sff["class_contents"] = "col-md-3";
                }
                else if (fld["fw_type"].ToString() == "datetime")
                {
                    sf["type"] = "date";
                    sff["type"] = "date_popup";
                    sff["class_contents"] = "col-md-3";
                }
                else
                    // everything else - just input
                    sff["type"] = "input";

                if (fld["is_identity"].ToString() == "1")
                {
                    sff["type"] = "group_id";
                    sff.Remove("class_contents");
                    sff.Remove("required");
                }

                // special fields
                switch (fld["name"])
                {
                    case "iname":
                        {
                            sff["validate"] = "exists"; // unique field
                            break;
                        }

                    case "att_id": // Single attachment field - TODO better detect on foreign key to "att" table
                        {
                            sf["type"] = "att";
                            sf["label"] = "Attachment";
                            sf["class_contents"] = "col-md-3";
                            sff.Remove("lookup_model");

                            sff["type"] = "att_edit";
                            sff["label"] = "Attachment";
                            sff["class_contents"] = "col-md-3";
                            sff["att_category"] = "general";
                            sff.Remove("class_contents");
                            sff.Remove("lookup_model");
                            sff.Remove("is_option0");
                            break;
                        }

                    case "status":
                        {
                            sf["label"] = "Status";
                            sf["lookup_tpl"] = "/common/sel/status.sel";

                            sff["label"] = "Status";
                            sff["type"] = "select";
                            sff["lookup_tpl"] = "/common/sel/status.sel";
                            sff["class_contents"] = "col-md-3";
                            break;
                        }

                    case "add_time":
                        {
                            sf["label"] = "Added on";
                            sf["type"] = "added";

                            sff["label"] = "Added on";
                            sff["type"] = "added";
                            sff.Remove("class_contents");
                            break;
                        }

                    case "upd_time":
                        {
                            sf["label"] = "Updated on";
                            sf["type"] = "updated";

                            sff["label"] = "Updated on";
                            sff["type"] = "updated";
                            sff.Remove("class_contents");
                            break;
                        }

                    case "add_users_id":
                    case "upd_users_id":
                        {
                            is_skip = true;
                            break;
                        }

                    default:
                        {
                            if (Regex.IsMatch((string)fld["iname"], @"\bState$"))
                            {
                                // if human name ends with State - make it State select
                                sf["lookup_tpl"] = "/common/sel/state.sel";

                                sff["type"] = "select";
                                sff["lookup_tpl"] = "/common/sel/state.sel";
                                sff["is_option_empty"] = true;
                                sff["option0_title"] = "- select -";
                                sff["class_contents"] = "col-md-3";
                            }
                            else
                            {
                            }

                            break;
                        }
                }

                if (is_skip)
                    continue;

                var is_sys = false;
                if (fld["is_identity"].ToString() == "1" || sys_fields.Contains(fld["name"]) || sf["type"].ToString() == "att" || sf["type"].ToString() == "att_links")
                {
                    // add to system fields
                    showFieldsRight.Add(sf);
                    showFormFieldsRight.Add(sff);
                    is_sys = true;
                }
                else
                {
                    showFieldsLeft.Add(sf);
                    showFormFieldsLeft.Add(sff);
                }

                if (!is_sys || fld["name"].ToString() == "status")
                    // add to save fields only if not system (except status)
                    saveFields.Add(fld["name"]);
            }

            // special case - "Lookup via Link Table" - could be multiple tables
            var rx_table_link = "^" + Regex.Escape(table_name) + "_(.+?)_link$";
            var table_name_linked = "";
            var table_name_link = "";
            foreach (string table in tables.Keys)
            {
                var m = Regex.Match(table, rx_table_link);
                if (m.Success)
                {
                    table_name_linked = m.Groups[1].Value;
                    table_name_link = table;

                    if (!string.IsNullOrEmpty(table_name_linked))
                    {
                        // if table "MODELTBL_TBL2_link" exists - add control for linked table
                        Hashtable sflink = new Hashtable()
                        {
                            {
                                "field",table_name_linked + "_link"
                            },
                            {
                                "label","Linked " + table_name_linked
                            },
                            {
                                "type","multi"
                            },
                            {
                                "lookup_model",_tablename2model(table_name_linked)
                            },
                            {
                                "table_link",table_name_link
                            },
                            {
                                "table_link_id_name",table_name + "_id"
                            },
                            {
                                "table_link_linked_id_name",table_name_linked + "_id"
                            }
                        };
                        Hashtable sfflink = new Hashtable()
                        {
                            {
                                "field",table_name_linked + "_link"
                            },
                            {
                                "label","Linked " + table_name_linked
                            },
                            {
                                "type","multicb"
                            },
                            {
                                "lookup_model",_tablename2model(table_name_linked)
                            },
                            {
                                "table_link",table_name_link
                            },
                            {
                                "table_link_id_name",table_name + "_id"
                            },
                            {
                                "table_link_linked_id_name",table_name_linked + "_id"
                            }
                        };

                        showFieldsLeft.Add(sflink);
                        showFormFieldsLeft.Add(sfflink);
                    }
                }
            }
            // end special case for link table

            config["model"] = model_name;
            config["is_dynamic_index"] = true;
            config["save_fields_nullable"] = saveFieldsNullable;
            config["save_fields"] = saveFields; // save all non-system
            config["save_fields_checkboxes"] = "";
            config["search_fields"] = "id" + (hfields.ContainsKey("iname") ? " iname" : ""); // id iname

            // either deault sort by iname or id
            config["list_sortdef"] = "id desc";
            if (hfields.ContainsKey("iname"))
                config["list_sortdef"] = "iname asc";
            else
                // just get first field
                if (fields.Count > 0)
                config["list_sortdef"] = ((Hashtable)fields[0])["fw_name"];

            config.Remove("list_sortmap"); // N/A in dynamic controller
            config.Remove("required_fields"); // not necessary in dynamic controller as controlled by showform_fields required attribute
            config["related_field_name"] = ""; // TODO?
            config["list_view"] = table_name;


            // default fields for list view
            // alternatively - just show couple fields
            // If is_fw Then config("view_list_defaults") = "id" & If(hfields.ContainsKey("iname"), " iname", "") & If(hfields.ContainsKey("add_time"), " add_time", "") & If(hfields.ContainsKey("status"), " status", "")

            // just show all fields, except identity, large text and system fields
            config["view_list_defaults"] = "";
            for (var i = 0; i <= fields.Count - 1; i++)
            {
                Hashtable field = (Hashtable)fields[i];
                if (field["is_identity"].ToString() == "1")
                    continue;
                if (field["fw_type"].ToString() == "varchar" && Utils.f2int(field["maxlen"]) <= 0)
                    continue;
                if (is_fw)
                {
                    if (field["name"].ToString() == "add_time" || field["name"].ToString() == "add_users_id" || field["name"].ToString() == "upd_time" || field["name"].ToString() == "upd_users_id")
                        continue;
                    config["view_list_defaults"] += (i == 0 ? "" : " ") + field["name"];
                }
                else
                    config["view_list_defaults"] += (i == 0 ? "" : " ") + field["fw_name"];
            }

            if (!is_fw)
                // nor non-fw tables - just show first 3 fields
                // config["view_list_defaults"] = ""
                // For i = 0 To Math.Min(2, fields.Count - 1)
                // config["view_list_defaults"] &= IIf(i = 0, "", " ") & fields(i)("fw_name")
                // Next

                // for non-fw - list_sortmap separately
                config["list_sortmap"] = hFieldsMapFW;
            config["view_list_map"] = hFieldsMap; // fields to names
            config["view_list_custom"] = "status";

            config["is_dynamic_show"] = entity.ContainsKey("controller_is_dynamic_show") ? entity["controller_is_dynamic_show"] : true;
            if ((bool)config["is_dynamic_show"])
            {
                var showFields = new ArrayList();
                showFields.Add(Utils.qh("type|row"));
                showFields.Add(Utils.qh("type|col class|col-lg-6"));
                showFields.AddRange(showFieldsLeft);
                showFields.Add(Utils.qh("type|col_end"));
                showFields.Add(Utils.qh("type|col class|col-lg-6"));
                showFields.AddRange(showFieldsRight);
                showFields.Add(Utils.qh("type|col_end"));
                showFields.Add(Utils.qh("type|row_end"));
                config["show_fields"] = showFields;
            }
            config["is_dynamic_showform"] = entity.ContainsKey("controller_is_dynamic_showform") ? entity["controller_is_dynamic_showform"] : true;
            if ((bool)config["is_dynamic_showform"])
            {
                var showFormFields = new ArrayList();
                showFormFields.Add(Utils.qh("type|row"));
                showFormFields.Add(Utils.qh("type|col class|col-lg-6"));
                showFormFields.AddRange(showFormFieldsLeft);
                showFormFields.Add(Utils.qh("type|col_end"));
                showFormFields.Add(Utils.qh("type|col class|col-lg-6"));
                showFormFields.AddRange(showFormFieldsRight);
                showFormFields.Add(Utils.qh("type|col_end"));
                showFormFields.Add(Utils.qh("type|row_end"));
                config["showform_fields"] = showFormFields;
            }

            // remove all commented items - name start with "#"
            foreach (var key in config.Keys.Cast<string>().ToArray())
            {
                if (Strings.Left(key, 1) == "#")
                    config.Remove(key);
            }
        }

        // convert db.json entity to SQL CREATE TABLE
        private string entity2SQL(Hashtable entity)
        {
            var result = "CREATE TABLE " + db.q_ident((string)entity["table"]) + " (" + Constants.vbCrLf;

            var i = 1;
            var fields = (ArrayList)entity["fields"];
            foreach (Hashtable field in fields)
            {
                var fsql = "";
                if (field["name"].ToString() == "status")
                    fsql += Constants.vbCrLf; // add empty line before system fields starting with "status"

                fsql += "  " + db.q_ident((string)field["name"]).PadRight(21, ' ') + " " + entityfield2dbtype(field);
                if ((int)field["is_identity"] == 1)
                    fsql += " IDENTITY(1, 1) PRIMARY KEY CLUSTERED";
                fsql += (int)field["is_nullable"] == 0 ? " NOT NULL" : "";
                fsql += entityfield2dbdefault(field);
                fsql += entityfield2dbfk(field, entity);

                result += fsql + (i < fields.Count ? "," : "") + Constants.vbCrLf;
                i += 1;
            }

            result += ")";

            return result;
        }

        private string entityfield2dbtype(Hashtable entity)
        {
            string result;

            switch (entity["fw_type"])
            {
                case "int":
                    {
                        if (entity["fw_subtype"].ToString() == "boolean" || entity["fw_subtype"].ToString() == "bit")
                            result = "BIT";
                        else if ((int)entity["numeric_precision"] == 3)
                            result = "TINYINT";
                        else
                            result = "INT";
                        break;
                    }

                case "float":
                    {
                        result = "FLOAT";
                        break;
                    }

                case "datetime":
                    {
                        if (entity["fw_subtype"].ToString() == "date")
                            result = "DATE";
                        else
                            result = "DATETIME2";
                        break;
                    }

                default:
                    {
                        result = "NVARCHAR";
                        if ((int)entity["maxlen"] > 0 & (int)entity["maxlen"] < 256)
                            result += "(" + entity["maxlen"] + ")";
                        else
                            result += "(MAX)";
                        break;
                    }
            }

            return result;
        }

        private string entityfield2dbdefault(Hashtable entity)
        {
            var result = "";
            string def = (string)entity["default"];
            if (def != null)
            {
                result += " DEFAULT ";
                // remove outer parentheses if any
                def = Regex.Replace(def, @"^\((.+)\)$", "$1");
                def = Regex.Replace(def, @"^\((.+)\)$", "$1"); // and again because of ((0)) but don't touch (getdate())

                if (Regex.IsMatch(def, @"^\d+$"))
                    // only digits
                    result += "(" + def + ")";
                else if (def == "getdate()" || Regex.IsMatch(def, @"^\=?now\(\)$", RegexOptions.IgnoreCase))
                    // access now() => getdate()
                    result += "(getdate())";
                else
                {
                    // any other text - quote
                    def = Regex.Replace(def, "^'(.*)'$", "$1"); // remove outer quotes if any

                    if (entity["fw_type"].ToString() == "int")
                        // if field type int - convert to int
                        result += "(" + db.qi(def) + ")";
                    else
                        result += "(" + db.q(def) + ")";
                }
            }

            return result;
        }

        // if field is referece to other table - add named foreign key
        // CONSTRAINT FK_entity["table_name")]remotetable FOREIGN KEY REFERENCES remotetable(id)
        private string entityfield2dbfk(Hashtable field, Hashtable entity)
        {
            var result = "";

            if (!entity.ContainsKey("foreign_keys"))
                return result;

            foreach (Hashtable fk in (ArrayList)entity["foreign_keys"])
            {
                if (fk["column"] == field["name"])
                {
                    result = " CONSTRAINT FK_" + entity["fw_name"] + "_" + Utils.name2fw((string)fk["pk_table"]) + " FOREIGN KEY REFERENCES " + db.q_ident((string)fk["pk_table"]) + "(" + db.q_ident((string)fk["pk_column"]) + ")";
                    break;
                }
            }

            return result;
        }

        // return default fields for the entity
        // id[, icode], iname, idesc, status, add_time, add_users_id, upd_time, upd_users_id
        private ArrayList defaultFields()
        {
            // New Hashtable From {
            // {"name", "icode"},
            // {"fw_name", "icode"},
            // {"iname", "Code"},
            // {"is_identity", 0},
            // {"default", ""},
            // {"maxlen", 64},
            // {"numeric_precision", Nothing},
            // {"is_nullable", 1},
            // {"fw_type", "varchar"},
            // {"fw_subtype", "nvarchar"}
            // },

            return new ArrayList()
            {
                new Hashtable()
                {
                    {"name","id"},
                    {"fw_name","id"},
                    {"iname","ID"},
                    {"is_identity",1},
                    {"default",null},
                    {"maxlen",null},
                    {"numeric_precision",10},
                    {"is_nullable",0},
                    {"fw_type","int"},
                    {"fw_subtype","integer"}
                },
                new Hashtable()
                {
                    {"name","iname"},
                    {"fw_name","iname"},
                    {"iname","Name"},
                    {"is_identity",0},
                    {"default",null},
                    {"maxlen",255},
                    {"numeric_precision",null},
                    {"is_nullable",0},
                    {"fw_type","varchar"},
                    {"fw_subtype","nvarchar"}
                },
                new Hashtable()
                {
                    {"name","idesc"},
                    {"fw_name","idesc"},
                    {"iname","Notes"},
                    {"is_identity",0},
                    {"default",null},
                    {"maxlen",-1},
                    {"numeric_precision",null},
                    {"is_nullable",1},
                    {"fw_type","varchar"},
                    {"fw_subtype","nvarchar"}
                }
            };
        }

        private ArrayList defaultFieldsAfter()
        {
            return new ArrayList()
            {
                new Hashtable()
                {
                    {"name","status"},
                    {"fw_name","status"},
                    {"iname","Status"},
                    {"is_identity",0},
                    {"default",0},
                    {"maxlen",null},
                    {"numeric_precision",3},
                    {"is_nullable",0},
                    {"fw_type","int"},
                    {"fw_subtype","tinyint"}
                },
                new Hashtable()
                {
                    {"name","add_time"},
                    {"fw_name","add_time"},
                    {"iname","Added on"},
                    {"is_identity",0},
                    {"default","getdate()"},
                    {"maxlen",null},
                    {"numeric_precision",null},
                    {"is_nullable",0},
                    {"fw_type","datetime"},
                    {"fw_subtype","datetime2"}
                },
                new Hashtable()
                {
                    {"name","add_users_id"},
                    {"fw_name","add_users_id"},
                    {"iname","Added by"},
                    {"is_identity",0},
                    {"default",null},
                    {"maxlen",null},
                    {"numeric_precision",10},
                    {"is_nullable",1},
                    {"fw_type","int"},
                    {"fw_subtype","int"}
                },
                new Hashtable()
                {
                    {"name","upd_time"},
                    {"fw_name","upd_time"},
                    {"iname","Updated on"},
                    {"is_identity",0},
                    {"default",null},
                    {"maxlen",null},
                    {"numeric_precision",null},
                    {"is_nullable",1},
                    {"fw_type","datetime"},
                    {"fw_subtype","datetime2"}
                },
                new Hashtable()
                {
                    {"name","upd_users_id"},
                    {"fw_name","upd_users_id"},
                    {"iname","Updated by"},
                    {"is_identity",0},
                    {"default",null},
                    {"maxlen",null},
                    {"numeric_precision",10},
                    {"is_nullable",1},
                    {"fw_type","int"},
                    {"fw_subtype","int"}
                }
            };
        }

        private ArrayList addressFields(string field_name)
        {
            var m = Regex.Match(field_name, "(.*?)(Address)$", RegexOptions.IgnoreCase);
            string prefix = m.Groups[1].Value;
            var city_name = prefix + "city";
            var state_name = prefix + "state";
            var zip_name = prefix + "zip";
            var country_name = prefix + "country";
            if (m.Groups[2].Value == "Address")
            {
                city_name = prefix + "City";
                state_name = prefix + "State";
                zip_name = prefix + "Zip";
                country_name = prefix + "Country";
            }

            return new ArrayList()
            {
                new Hashtable()
                {
                    {"name",field_name},
                    {"fw_name",Utils.name2fw(field_name)},
                    {"iname",Utils.name2human(field_name)},
                    {"is_identity",0},
                    {"default",""},
                    {"maxlen",255},
                    {"numeric_precision",null},
                    {"is_nullable",0},
                    {"fw_type","varchar"},
                    {"fw_subtype","nvarchar"}
                },
                new Hashtable()
                {
                    {"name",field_name + "2"},
                    {"fw_name",Utils.name2fw(field_name + "2")},
                    {"iname",Utils.name2human(field_name + "2")},
                    {"is_identity",0},
                    {"default",""},
                    {"maxlen",255},
                    {"numeric_precision",null},
                    {"is_nullable",0},
                    {"fw_type","varchar"},
                    {"fw_subtype","nvarchar"}
                },
                new Hashtable()
                {
                    {"name",city_name},
                    {"fw_name",Utils.name2fw(city_name)},
                    {"iname",Utils.name2human(city_name)},
                    {"is_identity",0},
                    {"default",""},
                    {"maxlen",64},
                    {"numeric_precision",null},
                    {"is_nullable",0},
                    {"fw_type","varchar"},
                    {"fw_subtype","nvarchar"}
                },
                new Hashtable()
                {
                    {"name",state_name},
                    {"fw_name",Utils.name2fw(state_name)},
                    {"iname",Utils.name2human(state_name)},
                    {"is_identity",0},
                    {"default",""},
                    {"maxlen",2},
                    {"numeric_precision",null},
                    {"is_nullable",0},
                    {"fw_type","varchar"},
                    {"fw_subtype","nvarchar"}
                },
                new Hashtable()
                {
                    {"name",zip_name},
                    {"fw_name",Utils.name2fw(zip_name)},
                    {"iname",Utils.name2human(zip_name)},
                    {"is_identity",0},
                    {"default",""},
                    {"maxlen",11},
                    {"numeric_precision",null},
                    {"is_nullable",0},
                    {"fw_type","varchar"},
                    {"fw_subtype","nvarchar"}
                }
            };
        }

        // update by url
        private void updateMenuItem(string controller_url, string controller_title)
        {
            var fields = new Hashtable()
            {
                {"url",controller_url},
                {"iname",controller_title},
                {"controller",Strings.Replace(controller_url, "/", "")}
            };

            var mitem = db.row("menu_items", DB.h("url", controller_url));
            if (mitem.Count > 0)
                db.update("menu_items", fields, DB.h("id", mitem["id"]));
            else
                // add to menu_items
                db.insert("menu_items", fields);
        }

        private bool isFwTableName(string table_name)
        {
            var tables = Utils.qh(FW_TABLES);
            return tables.ContainsKey(table_name.ToLower());
        }
    }
}