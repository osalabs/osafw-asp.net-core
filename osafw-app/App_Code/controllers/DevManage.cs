﻿// Manage  controller for Developers
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021  Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace osafw;

public class DevManageController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    const string DB_SQL_PATH = "/App_Data/sql/database.sql"; // relative to site_root
    const string DB_JSON_PATH = "/dev/db.json";
    const string ENTITIES_PATH = "/dev/entities.txt";
    const string FW_TABLES = "fwsessions fwentities att_categories att att_links users settings spages log_types activity_logs lookup_manager_tables user_views user_lists user_lists_items menu_items";

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Dev/Manage";
    }

    public Hashtable IndexAction()
    {
        Hashtable ps = new();

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
        db.clearSchemaCache();
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

            ArrayList rows = new();
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

        rw("<html><body>");

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
        db.clearSchemaCache();
    }
    // TODO move these functions to DB?
    private int exec_multi_sql(string sql, bool is_ignore_errors = false)
    {
        var result = 0;
        // launch the query
        //sql = strip_comments_sql(sql);
        String[] asql = DB.splitMultiSQL(sql);
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
                        rw(sqlone);
                        rw("<span style='color:red'>" + ex.Message + "</span>");
                        rw("");
                    }
                }
                else
                {
                    rw($"<pre>{sqlone}</pre>");
                    db.exec(sqlone);
                    result += 1;
                }
            }
        }
        return result;
    }
    private static string strip_comments_sql(string sql)
    {
        return Regex.Replace(sql, @"/\*.+?\*/", " ", RegexOptions.Singleline);
    }


    public void CreateModelAction()
    {
        var item = reqh("item");
        var table_name = Utils.f2str(item["table_name"]).Trim();
        var model_name = Utils.f2str(item["model_name"]).Trim();

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
        var model_name = Utils.f2str(item["model_name"]).Trim();
        var controller_url = Utils.f2str(item["controller_url"]).Trim();
        var controller_title = Utils.f2str(item["controller_title"]).Trim();

        var config_file = fw.config("template") + DB_JSON_PATH;
        var entities = loadJson<ArrayList>(config_file);

        // emulate entity
        var entity = new Hashtable()
        {
            {"model_name",model_name},
            {"controller_url",controller_url},
            {"controller_title",controller_title},
            {"table",fw.model(model_name).table_name}
        };
        // table = Utils.name2fw(model_name) - this is not always ok

        createController(entity, entities);
        var controller_name = Utils.f2str(entity["controller_url"]).Replace("/", "");

        fw.flash("controller_created", controller_name);
        fw.flash("controller_url", entity["controller_url"]);
        fw.redirect(base_url);
    }

    public void ExtractControllerAction()
    {
        var item = reqh("item");
        var controller_name = Utils.f2str(item["controller_name"]).Trim();

        if (!_controllers().Contains(controller_name))
            throw new NotFoundException("No controller found");

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
        Hashtable ps = new();
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
                {"id",dbname},
                {"iname",dbname}
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
            throw new UserException("Wrong DB selection");

        createDBJsonFromExistingDB(dbname);
        fw.flash("success", "template" + DB_JSON_PATH + " created");

        fw.redirect(base_url + "/(AppCreator)");
    }

    public Hashtable EntityBuilderAction()
    {
        Hashtable ps = new();

        var entities_file = fw.config("template") + ENTITIES_PATH;
        Hashtable item = new();
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
                // create db.json, db, models/controllers
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
        Hashtable ps = new();

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
            var controller_url = Utils.f2str(entity["controller_url"]);
            entity["is_model_exists"] = models.Contains(entity["model_name"]);
            entity["controller_name"] = Utils.f2str(controller_url).Replace("/", "");
            //create controller only if not exists already and url not empty
            entity["is_controller_create"] = !controllers.Contains(entity["controller_name"] + "Controller") && !string.IsNullOrEmpty(controller_url);
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
                if (Utils.f2str(item[key + "model_name"]).Length > 0 && entity["model_name"] != item[key + "model_name"])
                {
                    is_updated = true;
                    entity["model_name"] = item[key + "model_name"];
                }
                this.createModel(entity);
            }

            if (item.ContainsKey(key + "is_controller"))
            {
                // create controller (model must exists)
                if (Utils.f2str(item[key + "controller_name"]).Length > 0 && entity["controller_name"] != item[key + "controller_name"])
                {
                    is_updated = true;
                    entity["controller_name"] = item[key + "controller_name"];
                }
                if (Utils.f2str(item[key + "controller_title"]).Length > 0 && entity["controller_title"] != item[key + "controller_title"])
                {
                    is_updated = true;
                    entity["controller_title"] = item[key + "controller_title"];
                }
                if (!entity.ContainsKey("controller_is_dynamic_show") || Utils.f2bool(entity["controller_is_dynamic_show"]) != (Utils.f2str(item[key + "coview"]).Length > 0))
                {
                    is_updated = true;
                    entity["controller_is_dynamic_show"] = Utils.f2str(item[key + "coview"]).Length > 0;
                }
                if (!entity.ContainsKey("controller_is_dynamic_showform") || Utils.f2bool(entity["controller_is_dynamic_showform"]) != (Utils.f2str(item[key + "coedit"]).Length > 0))
                {
                    is_updated = true;
                    entity["controller_is_dynamic_showform"] = Utils.f2str(item[key + "coedit"]).Length > 0;
                }
                if (!entity.ContainsKey("controller_is_lookup") || Utils.f2bool(entity["controller_is_lookup"]) != (Utils.f2str(item[key + "colookup"]).Length > 0))
                {
                    is_updated = true;
                    entity["controller_is_lookup"] = Utils.f2str(item[key + "colookup"]).Length > 0;
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
    private static T loadJson<T>(string filename) where T : new()
    {
        T result;
        result = (T)Utils.jsonDecode(FW.getFileContent(filename));
        if (result == null)
            result = new T();
        return result;
    }

    private static void saveJson(object data, string filename)
    {
        string json_str;
        //use custom converter to output keys in specific order
        JsonSerializerOptions options = new();
        options.WriteIndented = true;
        options.Converters.Add(new ConfigJsonConverter());
        json_str = JsonSerializer.Serialize(data, data.GetType(), options);

        FW.setFileContent(filename, ref json_str);
    }

    private static ArrayList dbschema2entities(DB db)
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
            if (tblname.IndexOf("MSys", StringComparison.Ordinal) == 0)
                continue;

            // get table schema
            var tblschema = db.loadTableSchemaFull(tblname);
            // logger(tblschema)

            Hashtable table_entity = new();
            table_entity["db_config"] = db.db_name;
            table_entity["table"] = tblname;
            table_entity["fw_name"] = Utils.name2fw(tblname); // new table name using fw standards
            table_entity["iname"] = Utils.name2human(tblname); // human table name
            table_entity["fields"] = tableschema2fields(tblschema);
            table_entity["foreign_keys"] = db.listForeignKeys(tblname);

            table_entity["model_name"] = _tablename2model((string)table_entity["fw_name"]); // potential Model Name
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

    private static ArrayList tableschema2fields(ArrayList schema)
    {
        ArrayList result = new(schema);

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
    private static Hashtable array2hashtable(ArrayList arr, string key)
    {
        Hashtable result = new();
        foreach (Hashtable item in arr)
            result[item[key]] = item;
        return result;
    }

    private static List<string> _models()
    {
        var baseType = typeof(FwModel);
        var assembly = baseType.Assembly;
        return (from t in assembly.GetTypes()
                where t.IsSubclassOf(baseType)
                orderby t.Name
                select t.Name).ToList();
    }

    private static List<string> _controllers()
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

    private static void replaceInFile(string filepath, Hashtable strings)
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
    private static string _tablename2model(string table_name)
    {
        string result = "";
        string[] pieces = table_name.Split('_');
        foreach (string piece in pieces)
            result += Utils.capitalize(piece);
        return result;
    }

    private static void _makeValueTags(ArrayList fields)
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

    private void _addIndexToEntity(Hashtable table_entity, string index_prefix, string key_fields)
    {
        var indexes = (Hashtable)table_entity["indexes"];
        var next_num = indexes.Count + 1;
        indexes[index_prefix + next_num] = key_fields;
    }

    private void createDBJsonFromText(string entities_text)
    {
        var entities = new ArrayList();

        var lines = Regex.Split(entities_text, @"[\n\r]+");
        Hashtable table_entity = null;
        foreach (string line1 in lines)
        {
            string comments = "";
            var m1 = Regex.Match(line1, "#(.+)$");
            if (m1.Success)
            {
                comments = m1.Groups[1].Value;
            }

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
                    throw new UserException("Cannot have table name " + table_entity["table"]);

                table_entity["fw_name"] = Utils.name2fw(table_name); // new table name using fw standards

                table_entity["model_name"] = _tablename2model((string)table_entity["fw_name"]); // potential Model Name


                if (Regex.IsMatch(line, @"\bnoui\b"))
                {
                    //check if noui - no UI requested - set explicit empty URL so app won't not create controller
                    table_entity["controller_url"] = "";
                    table_entity["controller_title"] = "";
                }
                else
                {
                    table_entity["controller_url"] = "/Admin/" + table_entity["model_name"]; // potential Controller URL/Name/Title
                    table_entity["controller_title"] = Utils.name2human((string)table_entity["model_name"]);
                }
                if (Regex.IsMatch(line, @"\blookup\b"))
                    table_entity["controller_is_lookup"] = true;
                table_entity["is_fw"] = true;
                // add default system fields
                table_entity["fields"] = new ArrayList(defaultFields());
                table_entity["foreign_keys"] = new ArrayList();
                table_entity["indexes"] = new Hashtable(); // UX1|IX2 => comma separated fields fields
                if (comments.Length > 0) table_entity["comments"] = comments;
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

                //lookup on parts
                var hparts = new Hashtable();
                for (int i = 0; i < parts.Length; i++)
                {
                    hparts[parts[i]] = i;
                }

                if (field_name == "UNIQUEKEY" || field_name == "KEY")
                {
                    //add index
                    var m = Regex.Match(line, @"\s+\((.+)\)"); // (field1, field2...)
                    if (m.Success)
                    {
                        _addIndexToEntity(table_entity, (field_name == "UNIQUEKEY" ? "UX" : "IX"), m.Groups[1].Value);
                        continue;
                    }
                    else
                    {
                        logger("Wrong KEY defition");
                    }
                }

                if (field_name == "remove")
                {
                    //remove previously added field from the field list (usually used for automatically added fields)
                    var field_name_to_remove = parts[1];
                    var fields = (ArrayList)table_entity["fields"];
                    for (int i = 0; i < fields.Count; i++)
                    {
                        var item = (Hashtable)fields[i];
                        if ((string)item["name"] == field_name_to_remove)
                        {
                            fields.RemoveAt(i);
                            break;
                        }
                    }
                    continue;
                }

                // special - *Address -> set of address fields
                if (Regex.IsMatch(field_name, "Address$", RegexOptions.IgnoreCase))
                {
                    ((ArrayList)table_entity["fields"]).AddRange(addressFields(field_name));
                    continue;
                }

                Hashtable field = new();

                // check if field is foreign key like lookuptablename.id or prefix^lookuptablename.id
                if (field_name.Substring(field_name.Length - 3) == ".id")
                {
                    // this is foreign key field
                    Hashtable fk = new();
                    ((ArrayList)table_entity["foreign_keys"]).Add(fk);

                    if (field_name.Contains('^'))
                    {
                        //prefix^lookuptablename.id -> prefix_lookuptablename_id and FK to lookuptablename.id
                        field_name = Regex.Replace(field_name, @"\.id$", ""); //remove .id
                        var pk_table = Regex.Replace(field_name, @"^(.+?)\^", ""); //remove prefix
                        fk["pk_table"] = Utils.name2fw(pk_table);  // Customers.id => customers

                        field_name = field_name.Replace("^", "_"); //prefix^lookuptablename -> prefix_lookuptablename
                        field_name = Utils.name2fw(field_name) + "_id"; //normalize name and add _id
                    }
                    else
                    {
                        //lookuptablename.id -> lookuptablename_id and FK to lookuptablename.id
                        fk["pk_table"] = Utils.name2fw(Regex.Replace(field_name, @"\.id$", ""));  // Customers.id => customers
                        field_name = fk["pk_table"] + "_id";
                    }

                    fk["pk_column"] = "id";
                    fk["column"] = field_name;

                    field["fw_type"] = "int";

                    //for each field with foreign key - add an index (as SQL Server won't do this automatically, only MySQL)
                    _addIndexToEntity(table_entity, "IX", (string)fk["column"]);
                }
                else
                {
                    //check if we have FK like
                    //- birth_countries_id FK countries.id ...
                    if (hparts.Contains("FK"))
                    {
                        // this is foreign key field
                        Hashtable fk = new();
                        ((ArrayList)table_entity["foreign_keys"]).Add(fk);

                        var fk_table_field = parts[(int)hparts["FK"] + 1]; //table.id next to FK
                        fk["pk_table"] = Utils.name2fw(Regex.Replace(fk_table_field, @"\.id$", ""));  // Customers.id => customers
                        fk["pk_column"] = "id";
                        fk["column"] = field_name;

                        field["fw_type"] = "int";

                        //for each field with foreign key - add an index (as SQL Server won't do this automatically, only MySQL)
                        _addIndexToEntity(table_entity, "IX", (string)fk["column"]);
                    }

                    if (hparts.Contains("multiple"))
                    {
                        //this is many to many link table
                        var linked_tblname = Utils.name2fw(field_name);
                        var link_tblname = table_entity["table"] + "_" + linked_tblname;
                        var link_entity = new Hashtable();
                        link_entity["db_config"] = table_entity["db_config"];
                        link_entity["table"] = link_tblname;
                        link_entity["fw_name"] = Utils.name2fw(link_tblname); // new table name using fw standards
                        link_entity["iname"] = Utils.name2human(link_tblname); // human table name
                        link_entity["is_fw"] = true;

                        //2 link fields - one to main table, another - to lookup table
                        var link_fields = new ArrayList();
                        var field_name1 = table_entity["table"] + "_id";
                        var field_name2 = linked_tblname + "_id";
                        link_fields.Add(new Hashtable()
                                    {
                                        {"name",field_name1},
                                        {"fw_name",field_name1},
                                        {"iname",Utils.name2human((string)table_entity["table"])},
                                        {"is_identity",0},
                                        {"default",null},
                                        {"maxlen",null},
                                        {"numeric_precision",null},
                                        {"is_nullable",0}, //NOT NULL for linking!
                                        {"fw_type","int"},
                                        {"fw_subtype","int"}
                                    });
                        link_fields.Add(new Hashtable()
                                    {
                                        {"name",field_name2},
                                        {"fw_name",field_name2},
                                        {"iname",Utils.name2human((string)linked_tblname)},
                                        {"is_identity",0},
                                        {"default",null},
                                        {"maxlen",null},
                                        {"numeric_precision",null},
                                        {"is_nullable",0}, //NOT NULL for linking!
                                        {"fw_type","int"},
                                        {"fw_subtype","int"}
                                    });
                        link_fields.Add(defaultFieldStatus());
                        link_fields.AddRange(defaultFieldsAdded());
                        link_entity["fields"] = link_fields;

                        //2 foreign keys - to main table and lookup table
                        var link_fk = new ArrayList();
                        link_fk.Add(new Hashtable()
                                {
                                    {"pk_table", table_entity["table"]},
                                    {"pk_column", "id"},
                                    {"column", field_name1}
                                });
                        link_fk.Add(new Hashtable()
                                {
                                    {"pk_table", linked_tblname},
                                    {"pk_column", "id"},
                                    {"column", field_name2}
                                });
                        link_entity["foreign_keys"] = link_fk;
                        //automatic PK on both link fields
                        link_entity["indexes"] = new Hashtable()
                        {
                            {"PK", field_name1+", "+field_name2},
                            {"UX", field_name2+", "+field_name1}, //have an index with reversed fields order
                        };

                        link_entity["model_name"] = _tablename2model((string)link_entity["fw_name"]); // potential Model Name
                        link_entity["controller_url"] = ""; // no ui for link tables
                        link_entity["controller_title"] = ""; // no ui for link tables
                        if (comments.Length > 0) link_entity["comments"] = comments;

                        entities.Add(link_entity);
                        continue; //do not add a field as we made a link table
                    }
                }

                ((ArrayList)table_entity["fields"]).Add(field);
                field["name"] = field_name;
                field["iname"] = Utils.name2human(field_name);
                field["fw_name"] = Utils.name2fw(field_name);
                field["is_identity"] = 0;
                if (comments.Length > 0) field["comments"] = comments;

                field["is_nullable"] = Regex.IsMatch(line, @"\bNULL\b") ? 1 : 0;
                field["numeric_precision"] = null;
                field["maxlen"] = null;
                // detect type if not yet set by foreigh key
                if (Utils.f2str(field["fw_type"]) == "")
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
                    else if (Regex.IsMatch(line, @"\bcurrency\b", RegexOptions.IgnoreCase))
                    {
                        field["numeric_precision"] = 2;
                        field["fw_type"] = "float";
                        field["fw_subtype"] = "decimal";
                    }
                    else if (Regex.IsMatch(line, @"\bdecimal(?:\(\d+\))?\b", RegexOptions.IgnoreCase))
                    {
                        var numeric_precision = 2; // default precision
                        m = Regex.Match(line, @"\bdecimal\((\d+)\)"); // decimal(PRECISION_HERE)
                        if (m.Success)
                            numeric_precision = Utils.f2int(m.Groups[1].Value);
                        field["numeric_precision"] = numeric_precision;
                        field["fw_type"] = "float";
                        field["fw_subtype"] = "decimal";
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
                    else if (Regex.IsMatch((string)field["name"], "Date$", RegexOptions.IgnoreCase))
                    {
                        // not type specified
                        // additionally detect date field from name
                        field["fw_type"] = "datetime";
                        field["fw_subtype"] = "date";
                        field["is_nullable"] = 1;
                    }
                    else if (Regex.IsMatch(field_name, @"^is_", RegexOptions.IgnoreCase) || Regex.IsMatch(field_name, @"^Is[A-Z]"))
                    {
                        field["numeric_precision"] = 3;
                        field["fw_type"] = "int";
                        field["fw_subtype"] = "tinyint";
                        field["default"] = 0;
                    }
                    else
                        // just a default varchar(255)
                        field["maxlen"] = 255;

                    // default
                    m = Regex.Match(line, @"\bdefault\s*\((.+)\)"); // default(VALUE_HERE)
                    if (m.Success)
                        field["default"] = m.Groups[1].Value;

                    if (!field.Contains("default"))
                    {
                        field["default"] = null;
                        // no default set and field is NOT NULLable - then for nvarchar set empty string default
                        if (Utils.f2int(field["is_nullable"]) == 0 && Utils.f2str(field["fw_type"]) == "varchar")
                            field["default"] = "";
                    }
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
        DBList fks = db.arrayp("SELECT fk.name, o.name as table_name FROM sys.foreign_keys fk, sys.objects o where fk.is_system_named=0 and o.object_id=fk.parent_object_id", DB.h());
        foreach (var fk in fks)
            db.exec("ALTER TABLE " + db.qid((string)fk["table_name"]) + " DROP CONSTRAINT " + db.qid((string)fk["name"]));

        foreach (Hashtable entity in entities)
        {
            var sql = entity2SQL(entity);
            // create db tables directly in db

            try
            {
                db.exec("DROP TABLE IF EXISTS " + db.qid((string)entity["table"]));
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
            if (entity.ContainsKey("comments"))
                database_sql += "-- " + entity["comments"] + Environment.NewLine;
            database_sql += "DROP TABLE IF EXISTS " + q_ident((string)entity["table"]) + ";" + Environment.NewLine;
            database_sql += sql + ";" + Environment.NewLine + Environment.NewLine;
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
            throw new UserException("No table name or no model name");
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
                if (fld_identity == null && Utils.f2str(fld["is_identity"]) == "1")
                    fld_identity = fld;

                // first int field
                if (fld_int == null && Utils.f2str(fld["fw_type"]) == "int")
                    fld_int = fld;

                // for iname - just use 2nd to 4th field which not end with ID, varchar type and has some maxlen
                if (fld_iname == null && i >= 2 && i <= 4 && Utils.f2str(fld["fw_type"]) == "varchar" && Utils.f2int(fld["maxlen"]) > 0 && Utils.Right(Utils.f2str(fld["name"]), 2).ToLower() != "id")
                    fld_iname = fld;

                if (Regex.IsMatch((string)fld["name"], @"[^\w_]", RegexOptions.IgnoreCase))
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
                codegen += "        field_id = \"" + fld_identity["name"] + "\";" + Environment.NewLine;
            if (fld_iname != null && (string)fld_iname["name"] != "iname")
                codegen += "        field_iname = \"" + fld_iname["name"] + "\";" + Environment.NewLine;

            // also reset fw fields if such not exists
            if (!fields.ContainsKey("status"))
                codegen += "        field_status = \"\";" + Environment.NewLine;
            if (!fields.ContainsKey("add_users_id"))
                codegen += "        field_add_users_id = \"\";" + Environment.NewLine;
            if (!fields.ContainsKey("upd_users_id"))
                codegen += "        field_upd_users_id = \"\";" + Environment.NewLine;
            if (!fields.ContainsKey("upd_time"))
                codegen += "        field_upd_time = \"\";" + Environment.NewLine;
            if (fields.ContainsKey("prio"))
                codegen += "        field_prio = \"prio\";" + Environment.NewLine;

            if (is_normalize_names || !Utils.f2bool(entity["is_fw"]))
                codegen += "        is_normalize_names = true;" + Environment.NewLine;
        }


        mdemo = mdemo.Replace("//###CODEGEN", codegen);

        FW.setFileContent(path + @"\" + model_name + ".cs", ref mdemo);
    }

    private void createLookup(Hashtable entity)
    {
        var ltable = fw.model<LookupManagerTables>().oneByTname((string)entity["table"]);

        string columns = "";
        string column_names = "";
        string column_types = "";
        bool is_first = true;

        var hfks = array2hashtable((ArrayList)entity["foreign_keys"], "column");

        var fields = (ArrayList)entity["fields"];
        foreach (Hashtable field in fields)
        {
            var fw_name = (string)field["fw_name"];
            if (fw_name == "icode" || fw_name == "iname" || fw_name == "idesc")
            {
                columns += (!is_first ? "," : "") + fw_name;
                column_names += (!is_first ? "," : "") + field["iname"];
                column_types += (!is_first ? "," : "") + "";
            }
            else
            {
                columns += (!is_first ? "," : "") + field["name"];
                column_names += (!is_first ? "," : "") + field["iname"];

                //check if lookup table
                var ctype = "";
                if (hfks.ContainsKey(field["name"]))
                {
                    var fk = (Hashtable)hfks[field["name"]];
                    ctype = fk["pk_table"] + "." + fk["pk_column"] + ":iname"; //iname as default, might not work for non-fw tables
                }

                column_types += (!is_first ? "," : "") + ctype;
            }
            is_first = false;
        }

        //var fields = array2hashtable((ArrayList)entity["fields"], "fw_name");
        //if (fields.ContainsKey("icode"))
        //{
        //    columns += (columns.Length > 0 ? "," : "") + "icode";
        //    column_names += (column_names.Length > 0 ? "," : "") + ((Hashtable)fields["icode"])["iname"];
        //}
        //if (fields.ContainsKey("iname"))
        //{
        //    columns += (columns.Length > 0 ? "," : "") + "iname";
        //    column_names += (column_names.Length > 0 ? "," : "") + ((Hashtable)fields["iname"])["iname"];
        //}
        //if (fields.ContainsKey("idesc"))
        //{
        //    columns += (columns.Length > 0 ? "," : "") + "idesc";
        //    column_names += (column_names.Length > 0 ? "," : "") + ((Hashtable)fields["idesc"])["iname"];
        //}

        Hashtable item = new()
        {
            { "tname", entity["table"] },
            { "iname", entity["iname"] },
            { "columns", columns },
            { "column_names", column_names },
            { "column_types", column_types }
        };
        if (ltable.Count > 0)// replace
            fw.model<LookupManagerTables>().update(Utils.f2int(ltable["id"]), item);
        else
            fw.model<LookupManagerTables>().add(item);
    }

    private void createController(Hashtable entity, ArrayList entities)
    {
        string model_name = (string)entity["model_name"];
        string controller_url = (string)entity["controller_url"];
        string controller_title = (string)entity["controller_title"];

        if (controller_url == "")
        {
            //if controller url explicitly empty - do not create controller
            return;
        }
        if (controller_url == null)
        {
            //if controller url is not defined - default to admin model
            controller_url = "/Admin/" + model_name;
        }

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
            { "/Admin/DemosDynamic", controller_url },
            { "Demo Dynamic", controller_title }
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
        // list_view - model.table_name OR (select t.*, fk.iname... from model.table_name LEFT OUTER JOIN fk...) t in case there are foreign keys and we need names
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

        var sys_fields = Utils.qh("id status add_time add_users_id upd_time upd_users_id");




        Hashtable tables = new(); // hindex by table name to entities
        ArrayList fields = (ArrayList)entity["fields"];
        if (fields == null)
        {
            // TODO deprecate reading from db, always use entity info
            DB db;
            if (Utils.f2str(entity["db_config"]).Length > 0)
                db = new DB(fw, (Hashtable)((Hashtable)fw.config("db"))[entity["db_config"]], (string)entity["db_config"]);
            else
                db = new DB(fw);
            fields = db.loadTableSchemaFull(table_name);
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

        //build index by field name
        Hashtable hfields = new(); // name => fld index
        foreach (Hashtable fld in fields)
            hfields[Utils.f2str(fld["name"])] = fld;

        var foreign_keys = (ArrayList)entity["foreign_keys"] ?? new ArrayList();
        //add system user fields to fake foreign keys, so it can generate list query with user names
        var hforeign_keys = array2hashtable(foreign_keys, "column"); // column -> fk info
        if (hfields.ContainsKey("add_users_id") && !hforeign_keys.ContainsKey("add_users_id"))
        {
            Hashtable fk = new();
            fk["pk_column"] = "id";
            fk["pk_table"] = "users";
            fk["column"] = "add_users_id";
            foreign_keys.Add(fk);
        }
        if (hfields.ContainsKey("upd_users_id") && !hforeign_keys.ContainsKey("add_users_id"))
        {
            Hashtable fk = new();
            fk["pk_column"] = "id";
            fk["pk_table"] = "users";
            fk["column"] = "upd_users_id";
            foreign_keys.Add(fk);
        }
        hforeign_keys = array2hashtable(foreign_keys, "column"); // refresh in case new foreign keys added above

        ArrayList saveFields = new();
        ArrayList saveFieldsNullable = new();
        Hashtable hFieldsMap = new();   // name => iname - map for the view_list_map
        Hashtable hFieldsMapFW = new(); // fw_name => name
        ArrayList showFieldsLeft = new();
        ArrayList showFieldsRight = new();
        ArrayList showFormFieldsLeft = new();
        ArrayList showFormFieldsRight = new(); // system fields - to the right

        foreach (Hashtable fld in fields)
        {
            string fld_name = Utils.f2str(fld["name"]);
            logger("field name=", fld_name, fld);

            if (Utils.f2str(fld["fw_name"]) == "")
                fld["fw_name"] = Utils.name2fw(fld_name); // system name using fw standards
            if (Utils.f2str(fld["iname"]) == "")
                fld["iname"] = Utils.name2human(fld_name); // human name using fw standards

            var is_field_fk = hforeign_keys.ContainsKey(fld_name);
            var fk_field_name = "";

            if (is_field_fk)
            {
                fk_field_name = (string)((Hashtable)hforeign_keys[fld_name])["column"] + "_iname";
                hFieldsMap[fk_field_name] = fld["iname"]; //if FK field - add as column_id_iname
            }
            else
                hFieldsMap[fld_name] = fld["iname"]; //regular field

            if (!is_fw)
            {
                hFieldsMap[fld["fw_name"]] = fld["iname"];
                hFieldsMapFW[fld["fw_name"]] = fld_name;
            }

            Hashtable sf = new();  // show fields
            Hashtable sff = new(); // showform fields
            var is_skip = false;
            sf["field"] = fld_name;
            sf["label"] = fld["iname"];
            sf["type"] = "plaintext";

            sff["field"] = fld_name;
            sff["label"] = fld["iname"];

            if (Utils.f2str(fld["is_nullable"]) == "0" && fld["default"] == null)
                sff["required"] = true;// if not nullable and no default - required

            if (Utils.f2str(fld["is_nullable"]) == "1")
                saveFieldsNullable.Add(fld_name);

            var maxlen = Utils.f2int(fld["maxlen"]);
            if (maxlen > 0)
                sff["maxlength"] = maxlen;
            if (Utils.f2str(fld["fw_type"]) == "varchar")
            {
                if (maxlen <= 0 || fld_name == "idesc")
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
                        if (col <= 9)
                        {
                            //if more than 9 - just use whole width
                            if (col < 2)
                                col = 2; // minimum - 2
                            sff["class_contents"] = "col-md-" + col;
                        }
                    }
                }
            }
            else if (Utils.f2str(fld["fw_type"]) == "int")
            {
                // int fields could be: foreign keys, yes/no, just a number input

                // check foreign keys - and make type=select
                var is_fk = false;
                foreach (Hashtable fkinfo in foreign_keys)
                {
                    if ((string)fkinfo["column"] == fld_name)
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

                        //sff["class_contents"] = "col-md-4";
                        break;
                    }
                }

                if (!is_fk)
                {
                    if (fld_name == "parent_id")
                    {
                        // special case - parent_id
                        var mname = model_name;

                        sf["lookup_model"] = mname;
                        // sf["lookup_field"] = "iname"
                        sf["type"] = "plaintext_link";

                        sff["type"] = "select";
                        sff["lookup_model"] = mname;
                        sff["is_option0"] = true;
                        //sff["class_contents"] = "col-md-4";
                    }
                    else if (Utils.f2str(fld["fw_subtype"]) == "boolean" || Utils.f2str(entity["fw_subtype"]) == "bit" || fld_name.StartsWith("is_") || Regex.IsMatch(fld_name, @"^Is[A-Z]"))
                    {
                        // make it as yes/no radio
                        sff["type"] = "yesno";
                        sff["is_inline"] = true;
                        sff["class_contents"] = "col d-flex align-items-center";
                    }
                    else
                    {
                        sff["type"] = "number";
                        sff["class_contents"] = "col-md-4";
                        if (!(fld_name == "id" || fld_name.EndsWith("_id")) || fld_name == "status")
                        {
                            //for number non-ids - add min/max
                            sff["min"] = 0;
                            sff["max"] = 999999;
                        }
                    }
                }
            }
            else if (Utils.f2str(fld["fw_type"]) == "float")
            {
                sff["type"] = "number";
                sff["step"] = 0.1;
                sff["class_contents"] = "col-md-4";
            }
            else if (Utils.f2str(fld["fw_type"]) == "datetime")
            {
                sf["type"] = "date";
                sff["type"] = "date_popup";
                sff["class_contents"] = "col-md-5";
            }
            else
                // everything else - just input
                sff["type"] = "input";

            if (Utils.f2str(fld["is_identity"]) == "1")
            {
                sff["type"] = "id";
                sff.Remove("class_contents");
                sff.Remove("required");
            }

            // special fields
            switch (fld_name)
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
                        //sf["class_contents"] = "col-md-4";
                        sff.Remove("lookup_model");

                        sff["type"] = "att_edit";
                        sff["label"] = "Attachment";
                        //sff["class_contents"] = "col-md-4";
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
                        sff["class_contents"] = "col-md-4";
                        sff.Remove("min");//remove min/max because status detected above as numeric field
                        sff.Remove("max");
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
                            sff["class_contents"] = "col-md-4";
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
            if (Utils.f2str(fld["is_identity"]) == "1" || sys_fields.Contains(fld_name))
            {
                // add to system fields
                showFieldsRight.Add(sf);
                showFormFieldsRight.Add(sff);
                is_sys = true;
            }
            else
            {
                //non-system fields
                if (Utils.f2str(sf["type"]) == "att"
                    || Utils.f2str(sf["type"]) == "att_links"
                    || Utils.f2str(sff["type"]) == "textarea" && fields.Count >= 10)
                {
                    //add to the right: attachments, textareas (only if many fields)
                    showFieldsRight.Add(sf);
                    showFormFieldsRight.Add(sff);
                }
                else
                {
                    showFieldsLeft.Add(sf);
                    showFormFieldsLeft.Add(sff);
                }
            }

            if (!is_sys || fld_name == "status")
                // add to save fields only if not system (except status)
                saveFields.Add(fld_name);
        }

        // special case - "Lookup via Link Table" - could be multiple tables
        var rx_table_link = "^" + Regex.Escape(table_name) + "_(.+?)$";
        foreach (string table in tables.Keys)
        {
            var m = Regex.Match(table, rx_table_link);
            if (m.Success)
            {
                string table_name_linked = m.Groups[1].Value;
                string table_name_link = table;

                if (!string.IsNullOrEmpty(table_name_linked) && tables.ContainsKey(table_name_linked))
                {
                    // if tables "TBL2" and "MODELTBL_TBL2" exists - add control for linked table
                    Hashtable sflink = new()
                    {
                        { "field", table_name_linked + "_link" },
                        { "label", Utils.name2human(table_name_linked) },
                        { "type", "multi" },
                        { "lookup_model", _tablename2model(table_name_linked) },
                        //{ "table_link", table_name_link },
                        //{ "table_link_id_name", table_name + "_id" },
                        //{ "table_link_linked_id_name", table_name_linked + "_id" }
                    };
                    Hashtable sfflink = new()
                    {
                        { "field", table_name_linked + "_link" },
                        { "label", Utils.name2human(table_name_linked) },
                        { "type", "multicb" },
                        { "lookup_model", _tablename2model(table_name_linked) },
                        //{ "table_link", table_name_link },
                        //{ "table_link_id_name", table_name + "_id" },
                        //{ "table_link_linked_id_name", table_name_linked + "_id" }
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

        // list_view
        if (foreign_keys.Count > 0)
        {
            //we have foreign keys, so for the list screen we need to read FK entites names - build subquery

            var fk_inames = new ArrayList();
            var fk_joins = new ArrayList();
            for (int i = 0; i < foreign_keys.Count; i++)
            {
                var fk = (Hashtable)foreign_keys[i];
                var alias = $"fk{i}";
                var tcolumn = (string)fk["column"];
                var pk_table = (string)fk["pk_table"];
                var pk_column = (string)fk["pk_column"];

                var field = (Hashtable)hfields[tcolumn];
                var sql_join = "";
                if (Utils.f2int(field["is_nullable"]) == 1)
                {
                    //if FK field can be NULL - use LEFT OUTER JOIN
                    sql_join = $"LEFT OUTER JOIN {db.qid(pk_table)} {alias} ON ({alias}.{pk_column}=t.{tcolumn})";
                }
                else
                {
                    sql_join = $"INNER JOIN {db.qid(pk_table)} {alias} ON ({alias}.{pk_column}=t.{tcolumn})";
                }
                fk_joins.Add(sql_join);
                fk_inames.Add($"{alias}.iname as " + db.qid(tcolumn + "_iname")); //TODO detect non-iname for non-fw tables?
            }
            var inames = string.Join(", ", fk_inames.Cast<string>().ToArray());
            var joins = string.Join(" ", fk_joins.Cast<string>().ToArray());
            config["list_view"] = $"(SELECT t.*, {inames} FROM {db.qid(table_name)} t {joins}) tt";
        }
        else
        {
            config["list_view"] = table_name; //if no foreign keys - just read from main table
        }

        // default fields for list view
        // alternatively - just show couple fields
        // If is_fw Then config("view_list_defaults") = "id" & If(hfields.ContainsKey("iname"), " iname", "") & If(hfields.ContainsKey("add_time"), " add_time", "") & If(hfields.ContainsKey("status"), " status", "")

        // show first 6 fields (priority to required) +status, except identity, large text and system fields
        config["view_list_defaults"] = "";
        int defaults_ctr = 0;
        var rfields = (from Hashtable fld in fields
                       where Utils.f2str(fld["is_identity"]) != "1"
                         && !(Utils.f2str(fld["fw_type"]) == "varchar" && Utils.f2int(fld["maxlen"]) <= 0)
                         && !(sys_fields.Contains(fld["name"]) && Utils.f2str(fld["name"]) != "status")
                       orderby (Utils.f2str(fld["is_nullable"]) == "0" && fld["default"] == null) descending
                       select fld);
        foreach (Hashtable field in rfields)
        {
            var fname = (string)field["name"];
            if (defaults_ctr > 5 && fname != "status")
                continue;

            if (hforeign_keys.ContainsKey(fname))
            {
                fname = (string)((Hashtable)hforeign_keys[fname])["column"] + "_iname";
            }

            if (!is_fw)
            {
                fname = (string)field["fw_name"];
            }

            config["view_list_defaults"] += (defaults_ctr == 0 ? "" : " ") + fname;
            defaults_ctr++;
        }

        //for (var i = 0; i <= fields.Count - 1; i++)
        //{
        //    Hashtable field = (Hashtable)fields[i];
        //    if (Utils.f2str(field["is_identity"]) == "1")
        //        continue;
        //    if (Utils.f2str(field["fw_type"]) == "varchar" && Utils.f2int(field["maxlen"]) <= 0)
        //        continue;
        //    var fname = (string)field["name"];
        //    if (sys_fields.Contains(field["name"]) && fname != "status")
        //        continue;
        //    if (defaults_ctr > 5 && fname != "status")
        //        continue;

        //    if (!is_fw)
        //    {
        //        fname = (string)field["fw_name"];
        //    }

        //    config["view_list_defaults"] += (i == 0 ? "" : " ") + fname;
        //    defaults_ctr++;
        //}

        if (!is_fw)
        {
            // nor non-fw tables - just show first 3 fields
            // config["view_list_defaults"] = ""
            // For i = 0 To Math.Min(2, fields.Count - 1)
            // config["view_list_defaults"] &= IIf(i = 0, "", " ") & fields(i)("fw_name")
            // Next

            // for non-fw - list_sortmap separately
            config["list_sortmap"] = hFieldsMapFW;
        }
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
            if (key.StartsWith("#"))
                config.Remove(key);
        }
    }

    //quote identifier, but only if necessary
    private string q_ident(string str)
    {
        if (Regex.IsMatch(str, @"[^\w_]"))
        {
            return db.qid(str);
        }
        else
        {
            return str;
        }
    }

    // convert db.json entity to SQL CREATE TABLE
    private string entity2SQL(Hashtable entity)
    {
        var table_name = (string)entity["table"];
        var result = "CREATE TABLE " + q_ident(table_name) + " (" + Environment.NewLine;

        var i = 1;
        var fields = (ArrayList)entity["fields"];
        foreach (Hashtable field in fields)
        {
            var fsql = "";
            var field_name = Utils.f2str(field["name"]);
            if (field_name == "status")
                fsql += Environment.NewLine; // add empty line before system fields starting with "status"

            fsql += "  " + q_ident(field_name).PadRight(21, ' ') + " " + entityfield2dbtype(field);
            if (Utils.f2int(field["is_identity"]) == 1)
                fsql += " IDENTITY(1, 1) PRIMARY KEY CLUSTERED";
            fsql += Utils.f2int(field["is_nullable"]) == 0 ? " NOT NULL" : "";
            fsql += entityfield2dbdefault(field);
            fsql += entityfield2dbfk(field, entity);
            fsql += (i < fields.Count ? "," : "");
            if (field.ContainsKey("comments"))
                fsql = fsql.PadRight(64, ' ') + "-- " + field["comments"];

            result += fsql + Environment.NewLine;
            i += 1;
        }

        var indexes = (Hashtable)entity["indexes"] ?? null;
        if (indexes != null)
        {
            foreach (string index_prefix in indexes.Keys)
            {
                var isql = ", ";
                var prefix2 = index_prefix.Substring(0, 2);
                if (prefix2 == "PK")
                {
                    //PRIMARY KEY CLUSTERED (field, field,...)
                    isql += "PRIMARY KEY CLUSTERED ";
                }
                else
                {
                    //INDEX [UI]X123_tablename [UNIQUE] (field, field,...)
                    isql += "INDEX " + index_prefix + "_" + table_name;
                    isql += (prefix2 == "UX" ? " UNIQUE " : " ");
                }

                isql += "(" + indexes[index_prefix] + ")";
                result += isql + Environment.NewLine;
            }
        }

        result += ")";

        return result;
    }

    private static string entityfield2dbtype(Hashtable entity)
    {
        string result;

        switch (entity["fw_type"])
        {
            case "int":
                {
                    if (Utils.f2str(entity["fw_subtype"]) == "boolean" || Utils.f2str(entity["fw_subtype"]) == "bit")
                        result = "BIT";
                    else if (Utils.f2int(entity["numeric_precision"]) == 3)
                        result = "TINYINT";
                    else
                        result = "INT";
                    break;
                }

            case "float":
                {
                    if (Utils.f2str(entity["fw_subtype"]) == "currency")
                        result = "DECIMAL(18,2)";
                    else if (Utils.f2str(entity["fw_subtype"]) == "decimal")
                        result = "DECIMAL(18," + Utils.f2int(entity["numeric_precision"]) + ")";
                    else
                        result = "FLOAT";
                    break;
                }

            case "datetime":
                {
                    if (Utils.f2str(entity["fw_subtype"]) == "date")
                        result = "DATE";
                    else
                        result = "DATETIME2";
                    break;
                }

            default:
                {
                    result = "NVARCHAR";
                    var maxlen = Utils.f2int(entity["maxlen"]);
                    if (maxlen > 0 & maxlen < 256)
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
        if (entity["default"] != null)
        {
            string def = Utils.f2str(entity["default"]);
            result += " DEFAULT ";
            // remove outer parentheses if any
            def = Regex.Replace(def, @"^\((.+)\)$", "$1");
            def = Regex.Replace(def, @"^\((.+)\)$", "$1"); // and again because of ((0)) but don't touch (getdate())

            if (Regex.IsMatch(def, @"^\d+$"))
                // only digits
                result += def;
            else if (def == "getdate()" || Regex.IsMatch(def, @"^\=?now\(\)$", RegexOptions.IgnoreCase))
                // access now() => getdate()
                result += "getdate()";
            else
            {
                // any other text - quote
                def = Regex.Replace(def, "^'(.*)'$", "$1"); // remove outer quotes if any

                if (Utils.f2str(entity["fw_type"]) == "int")
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
            logger("CHECK FK:", fk["column"], "=", field["name"]);
            if ((string)fk["column"] == (string)field["name"])
            {
                //build FK name as FK_TABLE_FIELDWITHOUTID
                var fk_name = (string)fk["column"];
                fk_name = Regex.Replace(fk_name, "_id$", "", RegexOptions.IgnoreCase);
                result = " CONSTRAINT FK_" + entity["fw_name"] + "_" + Utils.name2fw(fk_name) + " FOREIGN KEY REFERENCES " + q_ident((string)fk["pk_table"]) + "(" + q_ident((string)fk["pk_column"]) + ")";
                break;
            }
        }
        logger("FK result: ", result);

        return result;
    }

    private static Hashtable defaultFieldID()
    {
        return new Hashtable()
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
            };
    }

    private static Hashtable defaultFieldStatus()
    {
        return new Hashtable()
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
            };
    }

    private static ArrayList defaultFieldsAdded()
    {
        return new ArrayList()
        {
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
            }
        };
    }

    private static ArrayList defaultFieldsUpdated()
    {
        return new ArrayList()
        {
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


    // return default fields for the entity
    // id[, icode], iname, idesc, status, add_time, add_users_id, upd_time, upd_users_id
    private static ArrayList defaultFields()
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
            defaultFieldID(),
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

    private static ArrayList defaultFieldsAfter()
    {
        var result = new ArrayList();
        result.Add(defaultFieldStatus());
        result.AddRange(defaultFieldsAdded());
        result.AddRange(defaultFieldsUpdated());
        return result;
    }

    private static ArrayList addressFields(string field_name)
    {
        var m = Regex.Match(field_name, "(.*?)(Address)$", RegexOptions.IgnoreCase);
        string prefix = m.Groups[1].Value;
        var city_name = prefix + "city";
        var state_name = prefix + "state";
        var zip_name = prefix + "zip";
        //var country_name = prefix + "country";
        if (m.Groups[2].Value == "Address")
        {
            city_name = prefix + "City";
            state_name = prefix + "State";
            zip_name = prefix + "Zip";
            //country_name = prefix + "Country";
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
            {"controller",controller_url.Replace("/", "")}
        };

        var mitem = db.row("menu_items", DB.h("url", controller_url));
        if (mitem.Count > 0)
            db.update("menu_items", fields, DB.h("id", mitem["id"]));
        else // add to menu_items
            db.insert("menu_items", fields);
    }

    private static bool isFwTableName(string table_name)
    {
        var tables = Utils.qh(FW_TABLES);
        return tables.ContainsKey(table_name.ToLower());
    }
}

public class ConfigJsonConverter : System.Text.Json.Serialization.JsonConverter<Hashtable>
{
    readonly string ordered_keys = "model is_dynamic_index list_view list_sortdef search_fields related_field_name view_list_defaults view_list_map view_list_custom is_dynamic_show show_fields is_dynamic_showform showform_fields form_new_defaults required_fields save_fields save_fields_checkboxes save_fields_nullable field type lookup_model label class class_control class_label class_contents required validate maxlength min max lookup_tpl is_option_empty option0_title table fields foreign_keys is_fw name iname fw_name fw_type fw_subtype maxlen is_nullable is_identity numeric_precision default db_config model_name controller_title controller_url controller_is_lookup";
    public override Hashtable Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, Hashtable value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        Hashtable hwritten = new();
        //write specific keys first
        foreach (var key in Utils.qw(ordered_keys))
        {
            if (value.ContainsKey(key))
            {
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, value[key], options);
                hwritten[key] = true;
            }
        }

        //then write rest of keys
        foreach (string key in value.Keys)
        {
            if (hwritten.ContainsKey(key))
                continue;
            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, value[key], options);
        }

        writer.WriteEndObject();
    }
}