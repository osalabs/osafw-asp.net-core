// Manage  controller for Developers
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024  Oleg Savchuk www.osalabs.com

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace osafw;

public class DevManageController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Dev/Manage";
    }

    public FwDict IndexAction()
    {
        FwDict ps = [];

        // table and views list
        var tables = db.tables();
        var views = db.views();
        tables.AddRange(views);
        tables.Sort();
        var select_tables = new FwList();
        ps["select_tables"] = select_tables;
        foreach (string table in tables)
            select_tables.Add(new FwDict() { { "id", table }, { "iname", table } });

        // models list - all clasess inherited from FwModel
        var select_models = new FwList();
        ps["select_models"] = select_models;

        foreach (string model_name in DevEntityBuilder.listModels())
            select_models.Add(new FwDict() { { "id", model_name }, { "iname", model_name } });

        var select_controllers = new FwList();
        ps["select_controllers"] = select_controllers;
        foreach (string controller_name in DevEntityBuilder.listControllers())
            select_controllers.Add(new FwDict() { { "id", controller_name }, { "iname", controller_name } });

        return ps;
    }

    public void DumpLogAction()
    {
        var seek = reqi("seek");
        string logpath = fw.config("log").toStr();
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

    public void InitPlaywrightAction()
    {
        ConvUtils.ensurePlaywrightInstalled(fw);

        fw.flash("success", "Playwright initialized");
        fw.redirect(base_url);
    }

    public void ResetCacheAction()
    {
        fw.flash("success", "Application Caches cleared");

        FwCache.clear();
        db.clearSchemaCache();
        fw.parsePageInstance().clear_cache();

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

    // generate documentation PDF
    public FwDict? DocsAction()
    {
        var is_export = reqs("format");

        var ps = new FwDict();
        ps["is_rbac"] = fw.model<Users>().isRoles();
        ps["access_levels"] = FormUtils.selectTplOptions("/common/sel/access_level.sel");

        ps["is_S3"] = S3.IS_ENABLED;
        ps["db_type"] = fw.getDB().dbtype;

        if (!Utils.isEmpty(is_export))
        {
            logger("exporting");
            var layout = fw.G["PAGE_LAYOUT_PRINT"].toStr();
            var options = new FwDict();
            options["disposition"] = "inline";
            ConvUtils.parsePagePdf(fw, "/dev/manage/docs", layout, ps, "documentation", options);
            return null;
        }
        else
            return ps;
    }

    #region Fw Updates
    public void RefreshViewsAction()
    {
        checkXSS();
        fw.model<FwUpdates>().refreshViews();
        fw.flash("success", "Views refreshed");
        fw.redirect("/Admin/FwUpdates");
    }

    public void MarkFwUpdatesAppliedAction()
    {
        checkXSS();
        fw.model<FwUpdates>().markAllPendingApplied();
        fw.flash("success", "All pending updates marked as applied");
        fw.redirect("/Admin/FwUpdates");
    }

    public FwDict? ApplyFwUpdateAction(int id)
    {
        checkXSS();
        fw.model<FwUpdates>().applyOne(id);

        if (fw.isJsonExpected())
        {
            var ps = new FwDict();
            ps["message"] = "Update applied";
            return new FwDict { { "_json", ps } };
        }
        else
        {
            fw.flash("success", "Update applied");
            fw.redirect("/Admin/FwUpdates");
            return null;
        }
    }

    public FwDict? ApplyFwUpdatesAction()
    {
        checkXSS();
        var ids = reqh("cb").Keys.Cast<string>().Select(x => x.toInt()).ToList();
        fw.model<FwUpdates>().applyList(ids);

        if (fw.isJsonExpected())
        {
            var ps = new FwDict();
            ps["message"] = "Updates applied";
            return new FwDict { { "_json", ps } };
        }
        else
        {
            fw.flash("success", "Updates applied");
            fw.redirect("/Admin/FwUpdates");
            return null;
        }
    }

    public void ReloadFwUpdatesAction()
    {
        checkXSS();
        fw.model<FwUpdates>().loadUpdates();
        fw.flash("success", "New Updates reloaded from disk");
        fw.redirect("/Admin/FwUpdates");
    }
    #endregion

    public void CreateModelAction()
    {
        var item = reqh("item");
        var table_name = item["table_name"].toStr().Trim();
        var model_name = item["model_name"].toStr().Trim();

        FwDict entity = DevEntityBuilder.table2entity(fw.db, table_name) ?? [];
        entity["table"] = table_name;
        if (model_name.Length > 0)
            entity["model_name"] = model_name;
        if (!entity.ContainsKey("db_config"))
            entity["db_config"] = "";
        DevCodeGen.init(fw).createModel(entity);

        fw.flash("success", model_name + ".cs model created");
        fw.redirect(base_url);
    }

    public void CreateControllerAction()
    {
        var item = reqh("item");
        var model_name = item["model_name"].toStr().Trim();
        var controller_url = item["controller_url"].toStr().Trim();
        var controller_title = item["controller_title"].toStr().Trim();
        var controller_type = item["controller_type"].toStr().Trim(); // empty("dynamic") or "vue" or "lookup" or "api"

        var config_file = fw.config("template") + DevCodeGen.DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<FwList>(config_file);

        // emulate entity
        var controller = new FwDict {
                    {"url",controller_url},
                    {"title",controller_title},
                    {"type",controller_type},
                };
        var entity = new FwDict()
        {
            {"model_name",model_name},
            {"controller", controller},
            {"table",fw.model(model_name).table_name}
        };
        // table = Utils.name2fw(model_name) - this is not always ok

        DevCodeGen.init(fw).createController(entity, entities);
        controller_url = controller["url"].toStr();
        var controller_name = controller_url.Replace("/", "");

        fw.flash("controller_created", controller_name);
        fw.flash("controller_url", controller_url);
        fw.redirect(base_url);
    }

    public void CreateReportAction()
    {
        var item = reqh("item");
        var repcode = item["report_code"].toStr().Trim();

        DevCodeGen.init(fw).createReport(repcode);

        fw.flash("success", repcode + " report created");
        fw.redirect(base_url);
    }

    public void ExtractControllerAction()
    {
        var item = reqh("item");
        var controller_name = item["controller_name"].toStr().Trim();

        if (!DevEntityBuilder.listControllers().Contains(controller_name))
            throw new NotFoundException("No controller found");

        var controllerType = Type.GetType(FW.FW_NAMESPACE_PREFIX + controller_name, true);
        if (controllerType == null)
            throw new UserException("Controller type not found");

        var controllerInstance = Activator.CreateInstance(controllerType) as FwDynamicController;
        if (controllerInstance == null)
            throw new UserException("Controller initialization failed");

        FwDynamicController cInstance = controllerInstance;
        cInstance.init(fw);

        var tpl_to = cInstance.base_url.ToLower();
        var tpl_path = fw.config("template") + tpl_to;
        var config_file = tpl_path + "/config.json";
        var conf = DevEntityBuilder.loadJson<FwDict>(config_file);

        // extract ShowAction
        conf["is_dynamic_show"] = false;
        FwDict fitem = [];
        var fields = cInstance.prepareShowFields(fitem, []);
        DevCodeGen.makeValueTags(fields);

        FwDict ps = new()
        {
            ["fields"] = fields
        };
        string content = fw.parsePage(tpl_to + "/show", "/common/form/show/extract/form.html", ps);
        content = Regex.Replace(content, @"^(?:[\t ]*(?:\r?\n|\r))+", "", RegexOptions.Multiline); // remove empty lines
        Utils.setFileContent(tpl_path + "/show/form.html", ref content);

        // extract ShowAction
        conf["is_dynamic_showform"] = false;
        fields = cInstance.prepareShowFormFields(fitem, []);
        DevCodeGen.makeValueTags(fields);
        ps = new()
        {
            ["fields"] = fields
        };
        content = fw.parsePage(tpl_to + "/show", "/common/form/showform/extract/form.html", ps);
        content = Regex.Replace(content, @"^(?:[\t ]*(?:\r?\n|\r))+", "", RegexOptions.Multiline); // remove empty lines
        content = Regex.Replace(content, "&lt;~(.+?)&gt;", "<~$1>"); // unescape tags
        Utils.setFileContent(tpl_path + "/showform/form.html", ref content);

        // 'TODO here - also modify controller code ShowFormAction to include listSelectOptions, multi_datarow, comboForDate, autocomplete name, etc...

        // now we could remove dynamic field definitions - uncomment if necessary
        // config.Remove("show_fields")
        // config.Remove("showform_fields")

        DevEntityBuilder.saveJsonController(conf, config_file);

        fw.flash("success", "Controller " + controller_name + " extracted dynamic show/showfrom to static templates");
        fw.redirect(base_url);
    }

    // analyse database tables and create db.json describing entities, fields and relationships
    public FwDict AnalyseDBAction()
    {
        FwDict ps = [];
        var item = reqh("item");
        string connstr = item["connstr"] + "";

        var dbtype = "SQL";
        if (connstr.Contains("OLE"))
            dbtype = "OLE";

        // Try
        var db = new DB(connstr, dbtype, "main");
        db.setLogger(fw.logger);
        db.setContext(fw.context);

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
    public FwDict DBAnalyzerAction()
    {
        FwDict ps = [];
        FwList dbsources = [];

        var dbConfig = fw.config("db") as FwDict ?? [];
        foreach (string dbname in dbConfig.Keys)
            dbsources.Add(new FwDict()
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
        var dbConfigs = fw.config("db") as FwDict ?? throw new UserException("Wrong DB selection");
        var dbconfig = dbConfigs[dbname] as FwDict ?? throw new UserException("Wrong DB selection");
        DevEntityBuilder.createDBJsonFromExistingDB(dbname, fw);
        fw.flash("success", "template" + DevCodeGen.DB_JSON_PATH + " created");

        fw.redirect(base_url + "/(AppCreator)");
    }

    public FwDict EntityBuilderAction()
    {
        FwDict ps = [];

        var entities_file = fw.config("template") + DevCodeGen.ENTITIES_PATH;
        FwDict item = new()
        {
            ["entities"] = Utils.getFileContent(entities_file)
        };
        ps["i"] = item;

        return ps;
    }

    public void EntityBuilderSaveAction()
    {
        var item = reqh("item");
        var is_create_all = reqi("DoMagic") == 1;

        var entities_file = fw.config("template") + DevCodeGen.ENTITIES_PATH;
        string filedata = item["entities"].toStr();
        Utils.setFileContent(entities_file, ref filedata);

        try
        {
            if (is_create_all)
            {
                // create db.json, db, models/controllers
                DevEntityBuilder.createDBJsonFromText(item["entities"].toStr(), fw);
                var CodeGen = DevCodeGen.init(fw);
                CodeGen.createDatabaseFromDBJson();
                CodeGen.createDBSQLFromDBJson();
                CodeGen.createModelsAndControllersFromDBJson();

                fw.flash("success", "Application created");
            }
            else
            {
                // create db.json only
                DevEntityBuilder.createDBJsonFromText(item["entities"].toStr(), fw);
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

    public FwDict DBInitializerAction()
    {
        FwDict ps = [];

        var config_file = fw.config("template") + DevCodeGen.DB_JSON_PATH;
        try
        {
            var entities = DevEntityBuilder.loadJson<FwList>(config_file);
            ps["tables"] = entities;
        }
        catch (Exception ex)
        {
            fw.flash("error", $"Failed to load db.json from '{config_file}': {ex.Message}");
            ps["tables"] = new FwList();
        }

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

    public FwDict AppCreatorAction()
    {
        // reload session, so sidebar menu will be updated
        if (reqs("reload").Length > 0)
            fw.model<Users>().reloadSession();

        FwDict ps = [];

        // tables
        var config_file = fw.config("template") + DevCodeGen.DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<FwList>(config_file);

        var models = DevEntityBuilder.listModels();
        var controllers = DevEntityBuilder.listControllers();

        foreach (FwDict entity in entities)
        {
            var controller_options = entity["controller"] as FwDict ?? [];
            var controller_url = controller_options["url"].toStr();
            entity["is_model_exists"] = models.Contains(entity["model_name"]);
            controller_options["name"] = controller_url.toStr().Replace("/", "");
            //create controller only if not exists already and url not empty
            entity["is_controller_create"] = !controllers.Contains(controller_options["name"] + "Controller") && !string.IsNullOrEmpty(controller_url);

            entity["controller"] = controller_options;
        }

        var f = reqh("f");
        var sortby = f["sort"].toStr();
        if (sortby == "table" || sortby == "model_name")
        {
            // sort entities list by requested column
            entities = new FwList(entities.OrderBy(x => x[sortby]).ToList());
        }

        ps["entities"] = entities;
        return ps;
    }

    public void AppCreatorSaveAction()
    {
        var f = reqh("f");
        var search = f["s"].toStr();
        var item = reqh("item");

        var config_file = fw.config("template") + DevCodeGen.DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<FwList>(config_file);

        // go thru entities and:
        // update checked rows for any user input (like model name changed)
        var models_ctr = 0;
        var controllers_ctr = 0;
        var is_updated = false;
        var CodeGen = DevCodeGen.init(fw);
        foreach (FwDict entity in entities)
        {
            if (search.Length > 0
                && !entity["table"].toStr().Contains(search)
                && !entity["model_name"].toStr().Contains(search))
                continue; // skip not matching rows if search is set

            var key = entity["fw_name"] + "#";
            if (item.ContainsKey(key + "is_model"))
            {
                // create model
                if (item[key + "model_name"].toStr().Length > 0 && entity["model_name"] != item[key + "model_name"])
                {
                    is_updated = true;
                    entity["model_name"] = item[key + "model_name"];
                }
                CodeGen.createModel(entity);
                models_ctr++;
            }

            if (item.ContainsKey(key + "is_controller"))
            {
                var controller_options = entity["controller"] as FwDict ?? [];

                // create controller (model must exists)
                if (item[key + "controller_name"].toStr().Length > 0 && controller_options["name"].toStr() != item[key + "controller_name"].toStr())
                {
                    is_updated = true;
                    controller_options["name"] = item[key + "controller_name"];
                }
                if (item[key + "controller_title"].toStr().Length > 0 && controller_options["title"].toStr() != item[key + "controller_title"].toStr())
                {
                    is_updated = true;
                    controller_options["title"] = item[key + "controller_title"];
                }
                if (!controller_options.TryGetValue("is_dynamic_show", out object? value) || value.toBool() != (item[key + "coview"].toStr().Length > 0))
                {
                    is_updated = true;
                    value = item[key + "coview"].toStr().Length > 0;
                    controller_options["is_dynamic_show"] = value;
                }
                if (!controller_options.TryGetValue("is_dynamic_showform", out object? value1) || value1.toBool() != (item[key + "coedit"].toStr().Length > 0))
                {
                    is_updated = true;
                    value1 = item[key + "coedit"].toStr().Length > 0;
                    controller_options["is_dynamic_showform"] = value1;
                }
                if (!controller_options.TryGetValue("is_lookup", out object? value2) || value2.toBool() != (item[key + "colookup"].toStr().Length > 0))
                {
                    is_updated = true;
                    value2 = item[key + "colookup"].toStr().Length > 0;
                    controller_options["is_lookup"] = value2;
                }
                if (!controller_options.TryGetValue("type", out object? value3) || value3.toStr() != item[key + "cotype"].toStr())
                {
                    is_updated = true;
                    value3 = item[key + "cotype"].toStr();
                    controller_options["type"] = value3;
                }
                if (!controller_options.TryGetValue("rwtpl", out object? value4) || value4.toStr() != item[key + "corwtpl"].toStr())
                {
                    value4 = item[key + "corwtpl"].toStr().Length > 0;
                    controller_options["rwtpl"] = value4;
                }

                entity["controller"] = controller_options;

                if (CodeGen.createController(entity, entities))
                    controllers_ctr++;
            }
        }

        // save db.json if there are any changes
        if (is_updated)
            DevEntityBuilder.saveJsonEntity(entities, config_file);

        fw.flash("success", $"App build successfull. Models created: {models_ctr}, Controllers created: {controllers_ctr}");
        fw.redirect(base_url + "/(AppCreator)?reload=1");
    }

    public Hashtable ConfigEditorAction()
    {
        Hashtable ps = [];

        var controller_name = reqs("controller");
        ps["controller_name"] = controller_name;

        var select_controllers = new ArrayList();
        foreach (string cname in DevEntityBuilder.listControllers())
            select_controllers.Add(DB.h("id", cname, "iname", cname));
        ps["select_controllers"] = select_controllers;

        if (!string.IsNullOrEmpty(controller_name))
        {
            var type = Type.GetType(FW.FW_NAMESPACE_PREFIX + controller_name, false);
            if (type != null && typeof(FwDynamicController).IsAssignableFrom(type))
            {
                var cInstance = (FwDynamicController)Activator.CreateInstance(type);
                cInstance.init(fw);

                var tpl_path = fw.config("template") + cInstance.base_url.ToLower();
                var config_file = tpl_path + "/config.json";
                if (System.IO.File.Exists(config_file))
                {
                    var config = DevEntityBuilder.loadJson<Hashtable>(config_file);
                    ps["config_json"] = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                    ps["config_json"] = "{}";
            }
            else
                ps["config_json"] = "{}";
        }
        else
            ps["config_json"] = "{}";

        return ps;
    }

    public Hashtable ConfigEditorSaveAction()
    {
        checkXSS();

        var controller_name = reqs("controller");
        var config_json = reqs("config");

        if (string.IsNullOrEmpty(controller_name))
            throw new UserException("No controller specified");

        var type = Type.GetType(FW.FW_NAMESPACE_PREFIX + controller_name, false) ?? throw new UserException("No controller found");
        if (!typeof(FwDynamicController).IsAssignableFrom(type))
            throw new UserException("Controller not dynamic");

        var config = (Hashtable)Utils.jsonDecode(config_json);

        var cInstance = (FwDynamicController)Activator.CreateInstance(type);
        cInstance.init(fw);
        var tpl_path = fw.config("template") + cInstance.base_url.ToLower();
        var config_file = tpl_path + "/config.json";

        DevEntityBuilder.saveJsonController(config, config_file);

        return new Hashtable { { "_json", new Hashtable { { "success", true } } } };
    }
}
