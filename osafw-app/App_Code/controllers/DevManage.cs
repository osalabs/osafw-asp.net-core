// Manage  controller for Developers
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024  Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public class DevManageController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Dev/Manage";
    }

    public Hashtable IndexAction()
    {
        Hashtable ps = [];

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

        foreach (string model_name in DevEntityBuilder.listModels())
            select_models.Add(new Hashtable() { { "id", model_name }, { "iname", model_name } });

        var select_controllers = new ArrayList();
        ps["select_controllers"] = select_controllers;
        foreach (string controller_name in DevEntityBuilder.listControllers())
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

        db.del("menu_items", []);
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
        Hashtable ps = [];

        // show list of available db updates
        var updates_root = fw.config("site_root") + @"\App_Data\sql\updates";
        if (System.IO.Directory.Exists(updates_root))
        {
            string[] files = System.IO.Directory.GetFiles(updates_root);

            ArrayList rows = [];
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
        var table_name = Utils.toStr(item["table_name"]).Trim();
        var model_name = Utils.toStr(item["model_name"]).Trim();

        Hashtable entity = new()
        {
            { "table", table_name },
            { "model_name", model_name },
            { "db_config", "" }
        };
        DevCodeGen.init(fw).createModel(entity);

        fw.flash("success", model_name + ".cs model created");
        fw.redirect(base_url);
    }

    public void CreateControllerAction()
    {
        var item = reqh("item");
        var model_name = Utils.toStr(item["model_name"]).Trim();
        var controller_url = Utils.toStr(item["controller_url"]).Trim();
        var controller_title = Utils.toStr(item["controller_title"]).Trim();
        var controller_type = Utils.toStr(item["controller_type"]).Trim(); // empty("dynamic") or "vue"

        var config_file = fw.config("template") + DevCodeGen.DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<ArrayList>(config_file);

        // emulate entity
        var entity = new Hashtable()
        {
            {"model_name",model_name},
            {"controller", new Hashtable {
                    {"url",controller_url},
                    {"title",controller_title},
                    {"type",controller_type},
                }
            },
            {"table",fw.model(model_name).table_name}
        };
        // table = Utils.name2fw(model_name) - this is not always ok

        DevCodeGen.init(fw).createController(entity, entities);
        controller_url = Utils.toStr(((Hashtable)entity["controller"])["url"]);
        var controller_name = controller_url.Replace("/", "");

        fw.flash("controller_created", controller_name);
        fw.flash("controller_url", controller_url);
        fw.redirect(base_url);
    }

    public void ExtractControllerAction()
    {
        var item = reqh("item");
        var controller_name = Utils.toStr(item["controller_name"]).Trim();

        if (!DevEntityBuilder.listControllers().Contains(controller_name))
            throw new NotFoundException("No controller found");

        FwDynamicController cInstance = (FwDynamicController)Activator.CreateInstance(Type.GetType(FW.FW_NAMESPACE_PREFIX + controller_name, true));
        cInstance.init(fw);

        var tpl_to = cInstance.base_url.ToLower();
        var tpl_path = fw.config("template") + tpl_to;
        var config_file = tpl_path + "/config.json";
        var config = DevEntityBuilder.loadJson<Hashtable>(config_file);

        // extract ShowAction
        config["is_dynamic_show"] = false;
        Hashtable fitem = [];
        var fields = cInstance.prepareShowFields(fitem, []);
        DevCodeGen.makeValueTags(fields);

        Hashtable ps = new()
        {
            ["fields"] = fields
        };
        ParsePage parser = new(fw);
        string content = parser.parse_page(tpl_to + "/show", "/common/form/show/extract/form.html", ps);
        content = Regex.Replace(content, @"^(?:[\t ]*(?:\r?\n|\r))+", "", RegexOptions.Multiline); // remove empty lines
        FW.setFileContent(tpl_path + "/show/form.html", ref content);

        // extract ShowAction
        config["is_dynamic_showform"] = false;
        fields = cInstance.prepareShowFormFields(fitem, []);
        DevCodeGen.makeValueTags(fields);
        ps = new()
        {
            ["fields"] = fields
        };
        parser = new ParsePage(fw);
        content = parser.parse_page(tpl_to + "/show", "/common/form/showform/extract/form.html", ps);
        content = Regex.Replace(content, @"^(?:[\t ]*(?:\r?\n|\r))+", "", RegexOptions.Multiline); // remove empty lines
        content = Regex.Replace(content, "&lt;~(.+?)&gt;", "<~$1>"); // unescape tags
        FW.setFileContent(tpl_path + "/showform/form.html", ref content);

        // 'TODO here - also modify controller code ShowFormAction to include listSelectOptions, multi_datarow, comboForDate, autocomplete name, etc...

        // now we could remove dynamic field definitions - uncomment if necessary
        // config.Remove("show_fields")
        // config.Remove("showform_fields")

        DevEntityBuilder.saveJsonController(config, config_file);

        fw.flash("success", "Controller " + controller_name + " extracted dynamic show/showfrom to static templates");
        fw.redirect(base_url);
    }

    // analyse database tables and create db.json describing entities, fields and relationships
    public Hashtable AnalyseDBAction()
    {
        Hashtable ps = [];
        var item = reqh("item");
        string connstr = item["connstr"] + "";

        var dbtype = "SQL";
        if (connstr.Contains("OLE"))
            dbtype = "OLE";

        // Try
        var db = new DB(fw, new Hashtable() { { "connection_string", connstr }, { "type", dbtype } });

        var entities = DevEntityBuilder.dbschema2entities(db);

        // save db.json
        DevEntityBuilder.saveJsonEntity(entities, fw.config("template") + DevCodeGen.DB_JSON_PATH);

        db.disconnect();
        fw.flash("success", "template" + DevCodeGen.DB_JSON_PATH + " created");

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
        Hashtable ps = [];
        ArrayList dbsources = [];

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

        DevEntityBuilder.createDBJsonFromExistingDB(dbname, fw);
        fw.flash("success", "template" + DevCodeGen.DB_JSON_PATH + " created");

        fw.redirect(base_url + "/(AppCreator)");
    }

    public Hashtable EntityBuilderAction()
    {
        Hashtable ps = [];

        var entities_file = fw.config("template") + DevCodeGen.ENTITIES_PATH;
        Hashtable item = new()
        {
            ["entities"] = FW.getFileContent(entities_file)
        };
        ps["i"] = item;

        return ps;
    }

    public void EntityBuilderSaveAction()
    {
        var item = reqh("item");
        var is_create_all = reqi("DoMagic") == 1;

        var entities_file = fw.config("template") + DevCodeGen.ENTITIES_PATH;
        string filedata = (string)item["entities"];
        FW.setFileContent(entities_file, ref filedata);

        try
        {
            if (is_create_all)
            {
                // create db.json, db, models/controllers
                DevEntityBuilder.createDBJsonFromText((string)item["entities"], fw);
                var CodeGen = DevCodeGen.init(fw);
                CodeGen.createDatabaseFromDBJson();
                CodeGen.createDBSQLFromDBJson();
                CodeGen.createModelsAndControllersFromDBJson();

                fw.flash("success", "Application created");
            }
            else
            {
                // create db.json only
                DevEntityBuilder.createDBJsonFromText((string)item["entities"], fw);
                fw.flash("success", "template" + DevCodeGen.DB_JSON_PATH + " created");
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
        Hashtable ps = [];

        var config_file = fw.config("template") + DevCodeGen.DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<ArrayList>(config_file);

        ps["tables"] = entities;

        return ps;
    }

    public void DBInitializerSaveAction()
    {
        var is_sql_only = reqi("DoSQL") == 1;

        if (is_sql_only)
        {
            DevCodeGen.init(fw).createDBSQLFromDBJson();
            fw.flash("success", DevCodeGen.DB_SQL_PATH + " created");

            fw.redirect(base_url + "/(DBInitializer)");
        }
        else
        {
            DevCodeGen.init(fw).createDatabaseFromDBJson();
            fw.flash("success", "DB tables created");

            fw.redirect(base_url + "/(AppCreator)");
        }
    }

    public Hashtable AppCreatorAction()
    {
        // reload session, so sidebar menu will be updated
        if (reqs("reload").Length > 0)
            fw.model<Users>().reloadSession();

        Hashtable ps = [];

        // tables
        var config_file = fw.config("template") + DevCodeGen.DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<ArrayList>(config_file);

        var models = DevEntityBuilder.listModels();
        var controllers = DevEntityBuilder.listControllers();

        foreach (Hashtable entity in entities)
        {
            var controller_options = (Hashtable)entity["controller"] ?? [];
            var controller_url = Utils.toStr(controller_options["url"]);
            entity["is_model_exists"] = models.Contains(entity["model_name"]);
            controller_options["name"] = Utils.toStr(controller_url).Replace("/", "");
            //create controller only if not exists already and url not empty
            entity["is_controller_create"] = !controllers.Contains(controller_options["name"] + "Controller") && !string.IsNullOrEmpty(controller_url);

            entity["controller"] = controller_options;
        }

        var f = reqh("f");
        var sortby = Utils.toStr(f["sort"]);
        if (sortby == "table" || sortby == "model_name")
        {
            //sort entities ArrayList of Hashtables with linq
            entities = new ArrayList(entities.Cast<Hashtable>().OrderBy(x => x[sortby]).ToList());
        }

        ps["entities"] = entities;
        return ps;
    }

    public void AppCreatorSaveAction()
    {
        var f = reqh("f");
        var search = Utils.toStr(f["s"]);
        var item = reqh("item");

        var config_file = fw.config("template") + DevCodeGen.DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<ArrayList>(config_file);

        // go thru entities and:
        // update checked rows for any user input (like model name changed)
        var models_ctr = 0;
        var controllers_ctr = 0;
        var is_updated = false;
        var CodeGen = DevCodeGen.init(fw);
        foreach (Hashtable entity in entities)
        {
            if (search.Length > 0
                && !Utils.toStr(entity["table"]).Contains(search)
                && !Utils.toStr(entity["model_name"]).Contains(search))
                continue; // skip not matching rows if search is set

            var key = entity["fw_name"] + "#";
            if (item.ContainsKey(key + "is_model"))
            {
                // create model
                if (Utils.toStr(item[key + "model_name"]).Length > 0 && entity["model_name"] != item[key + "model_name"])
                {
                    is_updated = true;
                    entity["model_name"] = item[key + "model_name"];
                }
                CodeGen.createModel(entity);
                models_ctr++;
            }

            if (item.ContainsKey(key + "is_controller"))
            {
                var controller_options = (Hashtable)entity["controller"] ?? [];

                // create controller (model must exists)
                if (Utils.toStr(item[key + "controller_name"]).Length > 0 && Utils.toStr(controller_options["name"]) != Utils.toStr(item[key + "controller_name"]))
                {
                    is_updated = true;
                    controller_options["name"] = item[key + "controller_name"];
                }
                if (Utils.toStr(item[key + "controller_title"]).Length > 0 && Utils.toStr(controller_options["title"]) != Utils.toStr(item[key + "controller_title"]))
                {
                    is_updated = true;
                    controller_options["title"] = item[key + "controller_title"];
                }
                if (!controller_options.ContainsKey("is_dynamic_show") || Utils.toBool(controller_options["is_dynamic_show"]) != (Utils.toStr(item[key + "coview"]).Length > 0))
                {
                    is_updated = true;
                    controller_options["is_dynamic_show"] = Utils.toStr(item[key + "coview"]).Length > 0;
                }
                if (!controller_options.ContainsKey("is_dynamic_showform") || Utils.toBool(controller_options["is_dynamic_showform"]) != (Utils.toStr(item[key + "coedit"]).Length > 0))
                {
                    is_updated = true;
                    controller_options["is_dynamic_showform"] = Utils.toStr(item[key + "coedit"]).Length > 0;
                }
                if (!controller_options.ContainsKey("is_lookup") || Utils.toBool(controller_options["is_lookup"]) != (Utils.toStr(item[key + "colookup"]).Length > 0))
                {
                    is_updated = true;
                    controller_options["is_lookup"] = Utils.toStr(item[key + "colookup"]).Length > 0;
                }
                if (!controller_options.ContainsKey("type") || Utils.toStr(controller_options["type"]) != (Utils.toStr(item[key + "cotype"])))
                {
                    is_updated = true;
                    controller_options["type"] = Utils.toStr(item[key + "cotype"]);
                }
                if (!controller_options.ContainsKey("rwtpl") || Utils.toStr(controller_options["rwtpl"]) != (Utils.toStr(item[key + "corwtpl"])))
                {
                    controller_options["rwtpl"] = Utils.toStr(item[key + "corwtpl"]).Length > 0;
                }

                entity["controller"] = controller_options;

                CodeGen.createController(entity, entities);
                controllers_ctr++;
            }
        }

        // save db.json if there are any changes
        if (is_updated)
            DevEntityBuilder.saveJsonEntity(entities, config_file);

        fw.flash("success", $"App build successfull. Models created: {models_ctr}, Controllers created: {controllers_ctr}");
        fw.redirect(base_url + "/(AppCreator)?reload=1");
    }
}
