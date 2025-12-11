using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
// Code generation for Developers
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024  Oleg Savchuk www.osalabs.com

namespace osafw;

class DevCodeGen
{
    public const string DB_SQL_PATH = "/App_Data/sql/database.sql"; // relative to site_root
    public const string DB_JSON_PATH = "/dev/db.json";
    public const string ENTITIES_PATH = "/dev/entities.txt";
    public const string SYS_FIELDS = "id status add_time add_users_id upd_time upd_users_id"; // system fields

    private readonly FW fw;
    private readonly DB db;

    //constructor - accept fw
    public DevCodeGen(FW fw, DB? db = null)
    {
        this.fw = fw;
        if (db == null)
            // if db not passed - use default db
            this.db = fw.db;
        else
            this.db = db;
    }

    public static DevCodeGen init(FW fw, DB? db = null)
    {
        return new DevCodeGen(fw, db);
    }

    public static void replaceInFile(string filepath, FwDict strings)
    {
        var content = Utils.getFileContent(filepath);
        if (content.Length == 0)
            return;

        foreach (string str in strings.Keys)
            content = content.Replace(str, strings[str].toStr());

        Utils.setFileContent(filepath, ref content);
    }

    // replaces strings in all files under defined dir
    // RECURSIVE!
    private static void replaceInFiles(string dir, FwDict strings)
    {
        foreach (string filename in Directory.GetFiles(dir))
            replaceInFile(filename, strings);

        // dive into dirs
        foreach (string foldername in Directory.GetDirectories(dir))
            replaceInFiles(foldername, strings);
    }

    private static string entityFieldToSQLType(FwDict entity)
    {
        string result;

        var fw_subtype = entity["fw_subtype"].toStr();
        switch (entity["fw_type"])
        {
            case "int":
                {
                    if (fw_subtype == "boolean" || fw_subtype == "bit")
                        result = "BIT";
                    else if (entity["numeric_precision"].toInt() == 3 || fw_subtype == "tinyint")
                        result = "TINYINT";
                    else if (entity["numeric_precision"].toInt() == 5 || fw_subtype == "smallint")
                        result = "SMALLINT";
                    else
                        result = "INT";
                    break;
                }

            case "float":
                {
                    if (fw_subtype == "currency")
                        result = "DECIMAL(18,2)";
                    else if (fw_subtype == "decimal")
                        result = "DECIMAL(" + entity["numeric_precision"].toInt() + "," + entity["numeric_scale"].toInt() + ")";
                    else
                        result = "FLOAT";
                    break;
                }

            case "date":
                {
                    result = "DATE";
                    break;
                }

            case "datetime":
                {
                    result = "DATETIME2";
                    break;
                }

            default:
                {
                    result = "NVARCHAR";
                    var maxlen = entity["maxlen"].toInt();
                    if (maxlen > 0 & maxlen < 256)
                        result += "(" + entity["maxlen"] + ")";
                    else
                        result += "(MAX)";
                    break;
                }
        }

        return result;
    }

    private string entityFieldToSQLDefault(FwDict entity)
    {
        var result = "";
        if (entity["default"] == null)
            return result;

        string def = entity["default"].toStr();
        result += " DEFAULT ";
        // remove outer parentheses if any
        def = Regex.Replace(def, @"^\((.+)\)$", "$1");
        def = Regex.Replace(def, @"^\((.+)\)$", "$1"); // and again because of ((0)) but don't touch (getdate())

        if (Regex.IsMatch(def, @"^\d+$"))
            // only digits
            result += def;
        else if (def.ToLower().StartsWith("getdate") || Regex.IsMatch(def, @"^\=?now\(?\)?$", RegexOptions.IgnoreCase))
            // access now() => getdate()
            result += "GETDATE()";
        else
        {
            // any other text - quote
            def = Regex.Replace(def, "^'(.*)'$", "$1"); // remove outer quotes if any

            if (entity["fw_type"].toStr() == "int")
                // if field type int - convert to int
                result += "(" + db.qi(def) + ")";
            else
                result += "(" + db.q(def) + ")";
        }

        return result;
    }

    // if field is referece to other table - add named foreign key
    // CONSTRAINT FK_entity["table_name")]remotetable FOREIGN KEY REFERENCES remotetable(id)
    private string entityFieldToSQLForeignKey(FwDict field, FwDict entity)
    {
        var result = "";

        if (!entity.ContainsKey("foreign_keys"))
            return result;

        foreach (FwDict fk in entity["foreign_keys"] as FwList ?? new FwList())
        {
            fw.logger("CHECK FK:", fk["column"].toStr(), "=", field["name"].toStr());
            if (fk["column"].toStr() == field["name"].toStr())
            {
                //build FK name as FK_TABLE_FIELDWITHOUTID
                var fk_name = fk["column"].toStr();
                fk_name = Regex.Replace(fk_name, "_id$", "", RegexOptions.IgnoreCase);
                result = " CONSTRAINT FK_" + entity["fw_name"].toStr() + "_" + Utils.name2fw(fk_name) + " FOREIGN KEY REFERENCES " + db.qid(fk["pk_table"].toStr(), false) + "(" + db.qid(fk["pk_column"].toStr(), false) + ")";
                break;
            }
        }
        fw.logger("FK result: ", result);

        return result;
    }

    // convert db.json entity to SQL CREATE TABLE
    private string entity2SQL(FwDict entity)
    {
        var table_name = entity["table"].toStr();
        var result = "CREATE TABLE " + db.qid(table_name, false) + " (" + Environment.NewLine;

        var indexes = entity["indexes"] as FwDict;
        var i = 1;
        var fields = entity["fields"] as FwList ?? new FwList();
        foreach (FwDict field in fields)
        {
            var fsql = "";
            var field_name = field["name"].toStr();
            if (field_name == "status")
                fsql += Environment.NewLine; // add empty line before system fields starting with "status"

            fsql += "  " + db.qid(field_name, false).PadRight(21, ' ') + " " + entityFieldToSQLType(field);
            if (field["is_identity"].toBool())
            {
                fsql += " IDENTITY(1, 1)";
                if (indexes == null || !indexes.ContainsKey("PK"))
                    fsql += " PRIMARY KEY CLUSTERED";
            }

            fsql += field["is_nullable"].toBool() ? "" : " NOT NULL";
            fsql += entityFieldToSQLDefault(field);
            fsql += entityFieldToSQLForeignKey(field, entity);
            fsql += (i < fields.Count ? "," : "");
            if (field.ContainsKey("comments"))
                fsql = fsql.PadRight(64, ' ') + "-- " + field["comments"].toStr();

            result += fsql + Environment.NewLine;
            i += 1;
        }

        if (indexes != null)
        {
            //sort indexes keys this way: PK (always first), then by number in suffix - UX1, IX2, UX3, IX4, IX5, IX6, ...
            var keys = new List<string>(indexes.Keys.Cast<string>());
            keys.Sort((a, b) =>
            {
                var a2 = a.Substring(2);
                var b2 = b.Substring(2);
                if (a2 == "PK")
                    return -1;
                if (b2 == "PK")
                    return 1;
                return a2.CompareTo(b2);
            });

            foreach (string index_prefix in keys)
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

    public void createDatabaseFromDBJson()
    {
        var config_file = fw.config("template") + DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<FwList>(config_file);

        // drop all FKs we created before, so we'll be able to drop tables later
        DBList fks = db.arrayp(@"SELECT fk.name, o.name as table_name 
                        FROM sys.foreign_keys fk, sys.objects o 
                        where fk.is_system_named=0 
                          and o.object_id=fk.parent_object_id", DB.h());
        foreach (var fk in fks)
            db.exec("ALTER TABLE " + db.qid(fk["table_name"], false) + " DROP CONSTRAINT " + db.qid(fk["name"], false));

        foreach (FwDict entity in entities)
        {
            var sql = entity2SQL(entity);
            // create db tables directly in db

            try
            {
                db.exec("DROP TABLE IF EXISTS " + db.qid(entity["table"].toStr(), false));
            }
            catch (Exception ex)
            {
                fw.logger(ex.Message);
            }

            db.exec(sql);
        }
    }

    public void createDBSQLFromDBJson()
    {
        var config_file = fw.config("template") + DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<FwList>(config_file);

        var database_sql = "";
        foreach (FwDict entity in entities)
        {
            var sql = entity2SQL(entity);
            // only create App_Data/database.sql
            // add drop
            if (entity.ContainsKey("comments"))
            {
                //comments can contain new lines - comment each line separately
                var commentsRaw = entity["comments"].toStr();
                var comments = Regex.Split(commentsRaw, "[\r\n]+");
                foreach (string comment in comments)
                    database_sql += "-- " + comment + Environment.NewLine;
            }

            database_sql += "DROP TABLE IF EXISTS " + db.qid(entity["table"].toStr(), false) + ";" + Environment.NewLine;
            database_sql += sql + ";" + Environment.NewLine + Environment.NewLine;
        }

        var sql_file = fw.config("site_root") + DevCodeGen.DB_SQL_PATH;
        Utils.setFileContent(sql_file, ref database_sql);
    }

    public void createModelsAndControllersFromDBJson()
    {
        var config_file = fw.config("template") + DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<FwList>(config_file);

        foreach (FwDict entity in entities)
        {
            this.createModel(entity);
            this.createController(entity, entities);
        }
    }

    public void createModel(FwDict entity)
    {
        string table_name = entity["table"].toStr();
        string model_name = entity["model_name"].toStr();
        bool is_junction = entity["is_junction"].toBool();

        if (model_name == "")
            model_name = Utils.nameCamelCase(table_name);
        if (table_name == "" || model_name == "")
            throw new UserException("No table name or no model name");
        // If _models().Contains(model_name) Then Throw New ApplicationException("Such model already exists")

        var path = fw.config("site_root") + @"\App_Code\models";
        string mdemo;
        if (is_junction)
        {
            //for junction tables - use DemosDemoDicts.cs as a template
            mdemo = Utils.getFileContent(path + @"\DemosDemoDicts.cs");
            if (mdemo == "")
                throw new ApplicationException("Can't open DemosDemoDicts.cs");

            // replace: DemosDemoDicts => ModelName, demos_demo_dicts => table_name
            mdemo = mdemo.Replace("DemosDemoDicts", model_name);
            mdemo = mdemo.Replace("demos_demo_dicts", table_name);
            mdemo = mdemo.Replace("db_config = \"\"", "db_config = \"" + entity["db_config"].toStr() + "\"");

            // setup junction_* fields from foreign_keys (first is main, second is linked)
            var model_main = "";
            var field_main_id = "";
            var model_linked = "";
            var field_linked_id = "";
            foreach (FwDict fk in (entity["foreign_keys"] as FwList ?? new FwList()))
            {
                if (field_main_id == "")
                {
                    model_main = DevEntityBuilder.tablenameToModel(Utils.name2fw(fk["pk_table"].toStr()));
                    field_main_id = fk["column"].toStr();
                }
                else
                {
                    model_linked = DevEntityBuilder.tablenameToModel(Utils.name2fw(fk["pk_table"].toStr()));
                    field_linked_id = fk["column"].toStr();
                }

            }

            mdemo = mdemo.Replace("<Demos>", $"<{model_main}>");
            mdemo = mdemo.Replace("\"demos_id\"", $"\"{field_main_id}\"");

            mdemo = mdemo.Replace("<DemoDicts>", $"<{model_linked}>");
            mdemo = mdemo.Replace("\"demo_dicts_id\"", $"\"{field_linked_id}\"");
        }
        else
        {
            //for regular tables - use DemoDicts.cs as a template

            // copy DemoDicts.cs to model_name.cs
            mdemo = Utils.getFileContent(path + @"\DemoDicts.cs");
            if (mdemo == "")
                throw new ApplicationException("Can't open DemoDicts.cs");

            // replace: DemoDicts => ModelName, demo_dicts => table_name
            mdemo = mdemo.Replace("DemoDicts", model_name);
            mdemo = mdemo.Replace("demo_dicts", table_name);
            mdemo = mdemo.Replace("db_config = \"\"", "db_config = \"" + entity["db_config"] + "\"");

            // generate code for the model's constructor:
            // set field_*
            var codegen = "";
            if (entity.ContainsKey("fields"))
            {
                var entity_fields = entity["fields"] as FwList ?? [];
                var fields = Utils.array2hashtable(entity_fields, "name");

                // detect id and iname fields
                var i = 1;
                FwDict? fld_int = null;
                FwDict? fld_identity = null;
                FwDict? fld_iname = null;
                var is_normalize_names = false;
                foreach (FwDict fld in entity_fields)
                {
                    // find identity
                    if (fld_identity == null && fld["is_identity"].toBool())
                        fld_identity = fld;

                    // first int field
                    if (fld_int == null && fld["fw_type"].toStr() == "int")
                        fld_int = fld;

                    // for iname - just use 2nd to 4th field which not end with ID, varchar type and has some maxlen
                    if (fld_iname == null && i >= 2 && i <= 4 && fld["fw_type"].toStr() == "varchar" && fld["maxlen"].toInt() > 0 && Utils.Right(fld["name"].toStr(), 2).ToLower() != "id")
                        fld_iname = fld;

                    if (Regex.IsMatch(fld["name"].toStr(), @"[^\w_]", RegexOptions.IgnoreCase))
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

                if (fld_identity != null && fld_identity["name"].toStr() != "id")
                    codegen += "        field_id = \"" + fld_identity["name"] + "\";" + Environment.NewLine;
                if (fld_iname != null && fld_iname["name"].toStr() != "iname")
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

                if (is_normalize_names || !entity["is_fw"].toBool())
                    codegen += "        is_normalize_names = true;" + Environment.NewLine;
            }

            mdemo = mdemo.Replace("//###CODEGEN", codegen);
        }

        mdemo = replaceRowClass(mdemo, buildRowClass(entity));

        // copy demo model to model_name.cs
        Utils.setFileContent(path + @"\" + model_name + ".cs", ref mdemo);
    }

    private void createLookup(FwDict entity)
    {
        string model_name = entity["model_name"].toStr();
        var controller_options = entity["controller"] as FwDict ?? new FwDict();
        string controller_url = controller_options["url"].toStr();
        string controller_title = controller_options["title"].toStr();

        var icode = controller_url.Replace("/", ""); // /Admin/LogTypes => AdminLogTypes

        var item = new FwDict
        {
            { "igroup", "User" },
            { "icode", icode },
            { "url", controller_url },
            { "iname", controller_title },
            { "model", model_name },
            { "access_level", Users.ACL_MANAGER },
            { "is_lookup", 1 }
        };

        //make/append to sql update file  in App_Date/sql/updates/updYYYY-MM-DD.sql with insert
        var upd_file = fw.config("site_root") + "/App_Data/sql/updates/upd" + DateTime.Now.ToString("yyyy-MM-dd") + ".sql";
        var upd_sql = "";

        var lookup = fw.model<FwControllers>().oneByIcode(icode);
        if (lookup.Count > 0)
        {
            fw.model<FwControllers>().update(lookup["id"].toInt(), item);
            upd_sql = Environment.NewLine + $@"UPDATE fwcontrollers SET 
                            igroup={db.q(item["igroup"])}, 
                            url={db.q(item["url"])}, 
                            iname={db.q(item["iname"])}, 
                            model={db.q(item["model"])}, 
                            access_level={db.qi(item["access_level"])},
                            is_lookup=1
                        WHERE icode={db.q(item["icode"])}" + Environment.NewLine;
        }
        else
        {
            fw.model<FwControllers>().add(item);
            upd_sql = Environment.NewLine + $@"INSERT INTO fwcontrollers (igroup, icode, url, iname, model, access_level, is_lookup) VALUES (
                            {db.q(item["igroup"])},
                            {db.q(item["icode"])},
                            {db.q(item["url"])},
                            {db.q(item["iname"])},
                            {db.q(item["model"])},
                            {db.qi(item["access_level"])},
                            1
                        )" + Environment.NewLine;
        }
        Utils.setFileContent(upd_file, ref upd_sql, true);
    }

    public bool createController(FwDict entity, FwList entities)
    {
        string model_name = entity["model_name"].toStr();
        var controller_options = entity["controller"] as FwDict ?? new FwDict();
        string controller_url = controller_options["url"].toStr();
        string controller_title = controller_options["title"].toStr();
        string controller_type = controller_options["type"].toStr(); // ""(dynamic), "vue", "lookup", "api"

        if (controller_url == "")
        {
            //if controller url explicitly empty - do not create controller
            return false;
        }
        //if controller url is not defined - default to admin model
        controller_url ??= "/Admin/" + model_name;

        var controller_name = controller_url.toStr().Replace("/", "");
        if (controller_title == "")
            controller_title = Utils.name2human(model_name);

        if (model_name == "")
            throw new ApplicationException("No model or no controller name or no title");
        // If _controllers().Contains(controller_name & "Controller") Then Throw New ApplicationException("Such controller already exists")

        // save back to entity as it can be used by caller
        controller_options["url"] = controller_url;
        controller_options["title"] = controller_title;
        entity["controller"] = controller_options;

        if (controller_options["is_lookup"].toBool() || controller_type == "lookup")
        {
            // if requested controller as a lookup table - just add/update lookup tables, no actual controller creation
            this.createLookup(entity);
            return false;
        }

        // determine controller type used as a template
        var controller_from_class = "AdminDemosDynamic";
        var controller_from_url = "/Admin/DemosDynamic";
        var controller_from_title = "Demo Dynamic";
        if (controller_options["type"].toStr() == "vue")
        {
            controller_from_class = "AdminDemosVue";
            controller_from_url = "/Admin/DemosVue";
            controller_from_title = "Demo Vue";
        }
        ;

        entity["controller"] = controller_options; //write back

        // copy templates from /admin/demosdynamic to /controller/url
        var tpl_from = fw.config("template") + controller_from_url.ToLower();
        var tpl_to = fw.config("template") + controller_url.ToLower();
        if (controller_options["rwtpl"].toBool() && Directory.Exists(tpl_to))
        {
            //remove directory if exists
            Directory.Delete(tpl_to, true);
        }
        Utils.CopyDirectory(tpl_from, tpl_to, true);

        // replace in templates: DemoDynamic to Title
        // replace in url.html /Admin/DemosDynamic to controller_url
        FwDict replacements = new()
        {
            { controller_from_url, controller_url },
            { controller_from_title, controller_title }
        };
        replaceInFiles(tpl_to, replacements);

        // update config.json:
        updateControllerConfigJson(entity, tpl_to, entities);

        // this should be last as under VS auto-rebuild can lock template files
        // copy DemosController .cs to controller .cs
        var path = fw.config("site_root") + @"\App_Code\controllers";
        var mdemo = Utils.getFileContent(path + @$"\{controller_from_class}.cs");
        if (mdemo == "")
            throw new ApplicationException($"Can't open {controller_from_class}.cs");

        // replace: DemoDicts => ModelName, demo_dicts => table_name
        mdemo = mdemo.Replace(controller_from_class, controller_name);
        mdemo = mdemo.Replace(controller_from_url, controller_url);
        mdemo = mdemo.Replace("DemoDicts", model_name);
        mdemo = mdemo.Replace("Demos", model_name);

        Utils.setFileContent(path + @"\" + controller_name + ".cs", ref mdemo);

        // add controller to sidebar menu
        updateMenuItem(controller_url, controller_title.toStr());

        return true;
    }

    public void updateControllerConfigJson(FwDict entity, string tpl_to, FwList entities)
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
        var config = DevEntityBuilder.loadJson<FwDict>(config_file);

        updateControllerConfig(entity, config, entities);

        // Utils.jsonEncode(config) - can't use as it produces unformatted json string
        DevEntityBuilder.saveJsonController(config, config_file);
    }

    public void updateControllerConfig(FwDict entity, FwDict config, FwList? entities = null)
    {
        string model_name = entity["model_name"].toStr();
        var model = fw.model(model_name);

        string table_name = entity["table"].toStr();
        if (string.IsNullOrEmpty(table_name))
            table_name = model.table_name;

        var controller_options = entity["controller"] as FwDict ?? [];
        string controller_title = controller_options["title"].toStr();
        if (controller_title.Length == 0)
            controller_title = Utils.name2human(model_name);

        string controller_type = controller_options["type"].toStr();
        fw.logger($"updating config for controller({controller_type})=", controller_options["url"].toStr());

        var sys_fields = Utils.qh(SYS_FIELDS);

        FwDict tables = []; // hindex by table name to entities
        FwList fields = entity["fields"] as FwList ?? new FwList();
        if (fields == null)
        {
            // TODO deprecate reading from db, always use entity info
            DB db;
            var dbConfig = entity["db_config"].toStr();
            db = dbConfig.Length > 0 ? fw.getDB(dbConfig) : fw.db;
            fields = db.loadTableSchemaFull(table_name);
            entity["foreign_keys"] = db.listForeignKeys(table_name).toArrayList();

            if (!entity.ContainsKey("is_fw"))
                entity["is_fw"] = true; // TODO actually detect if there any fields to be normalized
            var atables = db.tables();
            foreach (string tbl in atables)
                tables[tbl] = new FwDict();
        }
        else
        {
            entities ??= [];
            foreach (FwDict tentity in entities)
            {
                if (tentity == null)
                    continue;
                var tname = tentity?["table"].toStr();
                if (!string.IsNullOrEmpty(tname))
                    tables[tname] = tentity;
            }
        }

        var is_fw = entity["is_fw"].toBool();

        //build index by field name
        FwDict hfields = []; // name => fld index
        foreach (FwDict fld in fields)
        {
            var fname = fld["name"].toStr();
            if (fname.Length > 0)
                hfields[fname] = fld;
        }

        var foreign_keys = entity["foreign_keys"] as FwList ?? new FwList();
        //add system user fields to fake foreign keys, so it can generate list query with user names
        var hforeign_keys = Utils.array2hashtable(foreign_keys, "column"); // column -> fk info
        if (hfields.ContainsKey("add_users_id") && !hforeign_keys.ContainsKey("add_users_id"))
        {
            FwDict fk = new()
            {
                ["pk_column"] = "id",
                ["pk_table"] = "users",
                ["column"] = "add_users_id"
            };
            foreign_keys.Add(fk);
        }
        if (hfields.ContainsKey("upd_users_id") && !hforeign_keys.ContainsKey("add_users_id"))
        {
            FwDict fk = new()
            {
                ["pk_column"] = "id",
                ["pk_table"] = "users",
                ["column"] = "upd_users_id"
            };
            foreign_keys.Add(fk);
        }
        hforeign_keys = Utils.array2hashtable(foreign_keys, "column"); // refresh in case new foreign keys added above

        StrList saveFields = [];
        FwList saveFieldsNullable = [];
        FwDict hFieldsMap = [];   // name => iname - map for the view_list_map
        FwDict hFieldsMapEdit = []; // for Vue editable list
        FwDict hFieldsMapFW = []; // fw_name => name

        List<FwDict> formTabs = [
            //default
            new FwDict
            {
                ["tab"] = "",
                ["label"] = "Main"
            }
        ]; // show form tabs

        // tab => fields
        Dictionary<string, List<List<FwDict>>> showFieldsTabs = [];  // show fields tabs/columns
        Dictionary<string, List<List<FwDict>>> showFormFieldsTabs = []; // show form fields tabs/columns

        foreach (FwDict fld in fields)
        {
            var ui = fld["ui"] as FwDict ?? new FwDict(); // ui options for the field
            if (ui.ContainsKey("skip"))
                continue; //skip unnecessary fields

            string fld_name = fld["name"].toStr();
            //fw.logger("field name=", fld_name, fld);

            if (fld["fw_name"].toStr() == "")
                fld["fw_name"] = Utils.name2fw(fld_name); // system name using fw standards
            if (fld["iname"].toStr() == "")
                fld["iname"] = Utils.name2human(fld_name); // human name using fw standards

            var is_field_fk = hforeign_keys.ContainsKey(fld_name);
            hFieldsMapEdit[fld_name] = fld["iname"]; //use regular field for Vue editable list
            if (is_field_fk && hforeign_keys[fld_name] is FwDict fkInfo)
            {
                var fk_field_name = fkInfo["column"].toStr();
                if (fk_field_name.Length > 0)
                    hFieldsMap[fk_field_name + "_iname"] = fld["iname"]; //if FK field - add as column_id_iname
            }
            else
                hFieldsMap[fld_name] = fld["iname"]; //regular field

            if (!is_fw)
            {
                var fwName = fld["fw_name"].toStr();
                if (fwName.Length > 0)
                {
                    hFieldsMap[fwName] = fld["iname"];
                    hFieldsMapFW[fwName] = fld_name;
                }
            }

            FwDict sf = [];  // show fields
            FwDict sff = []; // showform fields

            sf["field"] = fld_name;
            sf["label"] = fld["iname"];
            sf["type"] = "plaintext";

            sff["field"] = fld_name;
            sff["label"] = fld["iname"];

            if (!fld["is_nullable"].toBool() && fld["default"] == null)
                sff["required"] = true;// if not nullable and no default - required

            var maxlen = fld["maxlen"].toInt();
            if (maxlen > 0)
                sff["maxlength"] = maxlen;
            if (fld["fw_type"].toStr() == "varchar")
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
            else if (fld["fw_type"].toStr() == "int")
            {
                // int fields could be: foreign keys, yes/no, just a number input

                // check foreign keys - and make type=select
                var is_fk = false;
                foreach (FwDict fkinfo in foreign_keys)
                {
                    if (fkinfo["column"].toStr() == fld_name)
                    {
                        is_fk = true;
                        var mname = DevEntityBuilder.tablenameToModel(Utils.name2fw(fkinfo["pk_table"].toStr()));

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
                    else if (fld["fw_subtype"].toStr() == "boolean" || fld["fw_subtype"].toStr() == "bit" || fld_name.StartsWith("is_") || Regex.IsMatch(fld_name, @"^Is[A-Z]"))
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
                        if (!(sys_fields.ContainsKey(fld_name) || fld_name.EndsWith("_id")))
                        {
                            //for number non-ids - add min/max
                            sff["min"] = 0;
                            sff["max"] = 999999;
                        }
                    }
                }
            }
            else if (fld["fw_type"].toStr() == "float")
            {
                sff["type"] = "number";
                sff["step"] = 0.1;
                sff["class_contents"] = "col-md-4";
            }
            else if (fld["fw_type"].toStr() == "date")
            {
                sf["type"] = "date";
                sff["type"] = "date_popup";
                sff["class_contents"] = "col-md-5";
            }
            else if (fld["fw_type"].toStr() == "datetime")
            {
                sf["type"] = "datetime";
                sff["type"] = "datetime_popup";
                sff["class_contents"] = "col-md-5";
            }
            else
                // everything else - just input
                sff["type"] = "input";

            if (fld["is_identity"].toBool())
            {
                sff["type"] = "id";
                sff.Remove("class_contents");
                sff.Remove("required");
            }


            if (overrideSpecialFields(fld, sf, sff))
                continue; //skip field

            overrideUIOptions(fld, sf, sff, model_name); // override ui options (if any)

            // layout
            addToFormColumns(fld, sf, sff, showFieldsTabs, showFormFieldsTabs, sys_fields, fields);

            var is_sys = false;
            if (fld["is_identity"].toBool() || sys_fields.Contains(fld_name))
                is_sys = true;
            if (!is_sys || fld_name == "status")
                // add to save fields only if not system (except status)
                saveFields.Add(fld_name);
        } // end of foreach field

        // special case - "Lookup via Link Table" - could be multiple tables
        var rx_table_link = "^" + Regex.Escape(table_name) + "_(.+?)$";
        foreach (string table in tables.Keys)
        {
            var m = Regex.Match(table, rx_table_link);
            if (m.Success)
            {
                //table could be a junction table name then
                var tentity = tables[table] as FwDict ?? new FwDict();
                var is_junction = tentity["is_junction"].toBool();
                var junction_model = tentity["model_name"].toStr();
                string table_name_linked = m.Groups[1].Value;
                if (!string.IsNullOrEmpty(table_name_linked) && tables.ContainsKey(table_name_linked))
                {
                    // if tables "TBL2" and "MODELTBL_TBL2" exists - add control for linked table
                    FwDict sflink = new()
                    {
                        { "field", table_name_linked + "_link" },
                        { "label", Utils.name2human(table_name_linked) },
                        { "type", "multi" },
                        //{ "lookup_model", DevEntityBuilder.tablenameToModel(table_name_linked) },
                        //{ "table_link", table_name_link },
                        //{ "table_link_id_name", table_name + "_id" },
                        //{ "table_link_linked_id_name", table_name_linked + "_id" }
                    };
                    if (is_junction)
                        sflink["model"] = junction_model;
                    else
                        sflink["lookup_model"] = DevEntityBuilder.tablenameToModel(table_name_linked);

                    FwDict sfflink = new()
                    {
                        { "field", table_name_linked + "_link" },
                        { "label", Utils.name2human(table_name_linked) },
                        { "type", "multicb" },
                        //{ "lookup_model", DevEntityBuilder.tablenameToModel(table_name_linked) },
                        //{ "table_link", table_name_link },
                        //{ "table_link_id_name", table_name + "_id" },
                        //{ "table_link_linked_id_name", table_name_linked + "_id" }
                    };
                    if (is_junction)
                        sfflink["model"] = junction_model;
                    else
                        sfflink["lookup_model"] = DevEntityBuilder.tablenameToModel(table_name_linked);

                    //add to default tab, left column
                    addToTabColumn(showFieldsTabs, "", 0, sflink);
                    addToTabColumn(showFormFieldsTabs, "", 0, sfflink);
                }
            }
        }
        // end special case for link table

        config["model"] = model_name;
        config["is_dynamic_index"] = true;
        config["save_fields"] = saveFields; // save all non-system
        config["save_fields_checkboxes"] = "";
        config["search_fields"] = "id" + (hfields.ContainsKey("iname") ? " iname" : ""); // id iname

        // either deault sort by iname or id
        config["list_sortdef"] = "id desc";
        if (hfields.ContainsKey("iname"))
            config["list_sortdef"] = "iname asc";
        else if (fields.Count > 0)
        {
            var firstField = fields[0] as FwDict;
            var fwname = firstField?["fw_name"].toStr();
            if (!string.IsNullOrEmpty(fwname))
                config["list_sortdef"] = fwname;
        }

        config.Remove("list_sortmap"); // N/A in dynamic controller
        config.Remove("required_fields"); // not necessary in dynamic controller as controlled by showform_fields required attribute
        config["related_field_name"] = ""; // TODO?

        // list_view
        if (foreign_keys.Count > 0)
        {
            //we have foreign keys, so for the list screen we need to read FK entites names - build subquery

            var fk_inames = new StrList();
            var fk_joins = new StrList();
            for (int i = 0; i < foreign_keys.Count; i++)
            {
                var fk = foreign_keys[i] as FwDict ?? [];
                var alias = $"fk{i}";
                var tcolumn = fk["column"].toStr();
                var pk_table = fk["pk_table"].toStr();
                var pk_column = fk["pk_column"].toStr();

                var field = hfields[tcolumn] as FwDict ?? [];
                string sql_join;
                if (field["is_nullable"].toBool())
                {
                    //if FK field can be NULL - use LEFT OUTER JOIN
                    sql_join = $"LEFT OUTER JOIN {db.qid(pk_table, false)} {alias} ON ({alias}.{pk_column}=t.{tcolumn})";
                }
                else
                {
                    sql_join = $"INNER JOIN {db.qid(pk_table, false)} {alias} ON ({alias}.{pk_column}=t.{tcolumn})";
                }
                fk_joins.Add(sql_join);
                fk_inames.Add($"{alias}.iname as " + db.qid(tcolumn + "_iname", false)); //TODO detect non-iname for non-fw tables?
            }
            var inames = string.Join(", ", fk_inames.Cast<string>().ToArray());
            var joins = string.Join(" ", fk_joins.Cast<string>().ToArray());
            config["list_view"] = $"(SELECT t.*, {inames} FROM {db.qid(table_name, false)} t {joins}) tt";
        }
        else
        {
            config["list_view"] = table_name; //if no foreign keys - just read from main table
        }

        // default fields for list view
        var list_defaults = getListDefaults(fields, hforeign_keys, sys_fields, is_fw);
        config["view_list_defaults"] = list_defaults.view;

        if (!is_fw)
        {
            // for non-fw - list_sortmap separately
            config["list_sortmap"] = hFieldsMapFW;
        }
        config["view_list_map"] = hFieldsMap; // fields to names
        config["view_list_custom"] = "status";

        if (controller_type == "vue")
        {
            config["is_dynamic_index_edit"] = controller_options["is_dynamic_index_edit"] ?? false; // by default disable list editing
            config["list_edit"] = table_name;
            config["edit_list_defaults"] = list_defaults.edit;
            config["edit_list_map"] = hFieldsMapEdit;
        }

        config["form_tabs"] = formTabs;

        //view form
        var is_dynamic_show = controller_options.ContainsKey("is_dynamic_show") ? controller_options["is_dynamic_show"].toBool() : true;
        config["is_dynamic_show"] = is_dynamic_show;
        if (is_dynamic_show)
            configAddTabs(config, "show_fields", showFieldsTabs);

        //edit form
        var is_dynamic_showform = controller_options.ContainsKey("is_dynamic_showform") ? controller_options["is_dynamic_showform"].toBool() : true;
        config["is_dynamic_showform"] = is_dynamic_showform;
        if (is_dynamic_showform)
            configAddTabs(config, "showform_fields", showFormFieldsTabs);

        //titles
        config["list_title"] = controller_title;
        config["view_title"] = $"View {controller_title} Record";
        config["edit_title"] = $"Edit {controller_title} Record";
        config["add_new_title"] = $"Add New {controller_title} Record";

        // remove all commented items - name start with "#"
        foreach (var key in config.Keys.Cast<string>().ToArray())
        {
            if (key.StartsWith('#'))
                config.Remove(key);
        }
    }

    public static void addToTabColumn(Dictionary<string, List<List<FwDict>>> showFieldsTabs, string tab, int col, FwDict sf)
    {
        if (!showFieldsTabs.ContainsKey(tab))
            showFieldsTabs[tab] = [
                [], //left col
                [], //mid col
                []  //right col
            ];
        var showFieldsCols = showFieldsTabs[tab];
        showFieldsCols[col].Add(sf);
    }

    //add tabs to config[key] and config[key_tab] based on showFieldsTabs and update config["form_tabs"]
    public static void configAddTabs(FwDict config, string key, Dictionary<string, List<List<FwDict>>> showFieldsTabs)
    {
        var formTabs = config["form_tabs"] as List<FwDict> ?? new List<FwDict>();
        config["form_tabs"] = formTabs;

        var tabs = new FwList();
        foreach (var tab in showFieldsTabs.Keys)
        {
            var showFieldsCols = showFieldsTabs[tab];
            if (showFieldsCols.Count == 0)
                continue;

            var tabFields = makeLayoutForFields(showFieldsCols);

            var config_key = key + (tab.Length > 0 ? "_" + tab : ""); // show_fields (default) or show_fields_tab
            config[config_key] = tabFields;

            //add tab to formTabs if not exists
            if (!formTabs.Any(x => x != null && x["tab"].toStr() == tab))
            {
                formTabs.Add(new FwDict
                {
                    ["tab"] = tab,
                    ["label"] = Utils.name2human(tab)
                });
            }
        }
    }

    public static FwList makeLayoutForFields(List<List<FwDict>> fieldsCols)
    {
        //remove/filter empty columns from showFieldsCols
        var fieldsColsFinal = fieldsCols.Where(x => x.Count > 0).ToList();
        //here we have 2 or 3 collumns
        var class_col = "col-lg-" + (fieldsColsFinal.Count == 2 ? 6 : 4);

        var configFields = new FwList
        {
            Utils.qh("type|row"),
        };
        for (var i = 0; i < fieldsColsFinal.Count; i++)
        {
            configFields.Add(Utils.qh($"type|col class|{class_col}"));
            configFields.AddRange(fieldsColsFinal[i]);
            configFields.Add(Utils.qh("type|col_end"));
        }
        configFields.Add(Utils.qh("type|row_end"));
        return configFields;
    }

    public static int addToFormColumns(FwDict fld, FwDict sf, FwDict sff,
        Dictionary<string, List<List<FwDict>>> showFieldsTabs,
        Dictionary<string, List<List<FwDict>>> showFormFieldsTabs,
        FwDict sys_fields, FwList fields)
    {
        var ui = fld["ui"] as FwDict ?? new FwDict(); // ui options for the field
        var col = 0; //default to left

        if (fld["is_identity"].toBool() || sys_fields.Contains(fld["name"] ?? ""))
        {
            // add to system fields - to the right
            col = 2;
        }
        else
        {
            //non-system fields
            if (sf["type"].toStr() == "att"
                || sf["type"].toStr() == "att_links"
                || sff["type"].toStr() == "textarea" && fields.Count >= 10)
            {
                //add to the right: attachments, textareas (only if many fields)
                col = 2;
            }
        }

        //check if specific column required
        var formcol = ui["formcol"].toStr();
        if (formcol == "left")
            col = 0;
        else if (formcol == "mid")
            col = 1;
        else if (formcol == "right")
            col = 2;

        //select/add proper tab
        var formtab = ui["formtab"].toStr();

        addToTabColumn(showFieldsTabs, formtab, col, sf);
        addToTabColumn(showFormFieldsTabs, formtab, col, sff);

        return col;
    }

    //return 2 separte values for view_list_defaults and edit_list_defaults
    // show first 6 fields (priority to required) +status,
    // except identity, large text and system fields
    //
    // alternatively - just show couple fields
    // If is_fw Then config("view_list_defaults") = "id" & If(hfields.ContainsKey("iname"), " iname", "") & If(hfields.ContainsKey("add_time"), " add_time", "") & If(hfields.ContainsKey("status"), " status", "")
    public static (string view, string edit) getListDefaults(FwList fields, FwDict hforeign_keys, FwDict sys_fields, bool is_fw)
    {
        string view_list_defaults = "";
        string edit_list_defaults = "";

        int defaults_ctr = 0;
        var rfields = fields.Cast<FwDict>()
            .Where(fld =>
            {
                var fname = fld["name"].toStr();
                if (fname.Length == 0)
                    return false;

                var isLongText = fld["fw_type"].toStr() == "varchar" && fld["maxlen"].toInt() <= 0;
                var isSystemNonStatus = sys_fields.Contains(fname) && fname != "status";

                return !fld["is_identity"].toBool() && !isLongText && !isSystemNonStatus;
            })
            .OrderByDescending(fld => !fld["is_nullable"].toBool() && fld["default"] == null);
        foreach (FwDict field in rfields)
        {
            var fname = field["name"].toStr();
            if (defaults_ctr > 5 && fname != "status")
                continue;

            edit_list_defaults += (defaults_ctr == 0 ? "" : " ") + fname; //for edit list we need real field names only

            if (hforeign_keys.ContainsKey(fname))
                fname = (hforeign_keys[fname] as FwDict)?["column"].toStr() + "_iname";

            if (!is_fw)
                fname = field["fw_name"].toStr();

            view_list_defaults += (defaults_ctr == 0 ? "" : " ") + fname;
            defaults_ctr++;
        }


        return (view_list_defaults, edit_list_defaults);
    }

    //return true if skip this field
    public static bool overrideSpecialFields(FwDict fld, FwDict sf, FwDict sff)
    {
        var is_skip = false;
        var field_name = fld["name"].toStr();
        // special fields
        switch (field_name)
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
                    sff["att_category"] = AttCategories.CAT_GENERAL;
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
                    var iname = fld["iname"].toStr();
                    if (Regex.IsMatch(iname, @"\bState$"))
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

        return is_skip;
    }

    public static void overrideUIOptions(FwDict fld, FwDict sf, FwDict sff, string model_name)
    {
        var ui = fld["ui"] as FwDict ?? new FwDict(); // ui options for the field

        // override ui options
        if (ui.ContainsKey("required"))
            sff["required"] = ui["required"].toBool();

        //input types
        if (ui.ContainsKey("plaintext"))
            sff["type"] = "plaintext";
        if (ui.ContainsKey("checkbox"))
            sff["type"] = "cb";
        if (ui.ContainsKey("number"))
            sff["type"] = "number";
        if (ui.ContainsKey("password"))
            sff["type"] = "password";
        if (ui.ContainsKey("select"))
            sff["type"] = "select";
        if (ui.ContainsKey("radio"))
            sff["type"] = "radio";
        if (ui.ContainsKey("time"))
        {
            sf["type"] = "plaintext";
            sf["conv"] = "time_from_seconds";
            sff["type"] = "time";
            sff["conv"] = "time_from_seconds";
            sff.Remove("min");
            sff.Remove("max");
            sff.Remove("maxlength");
        }

        //direct attributes for show form
        foreach (string attr in "validate maxlength rows placeholder step min max".Split())
        {
            if (ui.ContainsKey(attr))
                sff[attr] = ui[attr];
        }

        //control attributes
        foreach (string attr in "pattern multiple".Split())
        {
            if (ui.ContainsKey(attr))
                sff["attrs_control"] = sff["attrs_control"] ?? "" + " " + attr + "=\"" + ui[attr] + "\"";
        }
        //data-* attributes
        foreach (string ui_key in ui.Keys)
        {
            if (ui_key.StartsWith("data-"))
                sff["attrs_control"] = sff["attrs_control"] ?? "" + " " + ui_key + "=\"" + ui[ui_key] + "\"";
        }

        //label and class overrides for both form and show form
        foreach (string attr in "label class class_label class_contents class_control".Split())
        {
            if (ui.ContainsKey(attr))
            {
                //if value is explicitly "false" string - set to false (used to disable row class)
                //otherwise - set to value
                var v = ui[attr].toStr() == "false" ? false : ui[attr];
                sf[attr] = v;
                sff[attr] = v;
            }
        }

        // options(value1|Display1 value2|Display2)
        // options(/common/sel/status.sel)
        // options(status.sel) -- relative to controller template folder
        if (ui.ContainsKey("options"))
        {
            var options = ui["options"].toStr();
            if (options.Contains('|'))
                // if contains | - it's a list of options
                sff["options"] = Utils.qh(options);
            else
                // otherwise - it's a template
                sff["lookup_tpl"] = options;
        }

        // help text under control
        if (ui.ContainsKey("help"))
        {
            sff["help_text"] = ui["help"];
        }

        // autocomplete
        if (ui.ContainsKey("autocomplete"))
        {
            sf["type"] = "plaintext_autocomplete";
            sff["type"] = "autocomplete";
            // url /controller/(Autocomplete)?model=ModelName&q=
            sff["autocomplete_url"] = $"/Admin/{model_name}/(Autocomplete)?model=" + sff["lookup_model"] + "&q=";
        }
    }

    public static void makeValueTags(FwList fields)
    {
        foreach (FwDict def in fields)
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

    public void createReport(string repcode)
    {
        repcode = FwReports.cleanupRepcode(repcode);
        if (string.IsNullOrEmpty(repcode))
            throw new UserException("No report code");

        var report_class = FwReports.repcodeToClass(repcode);
        var reports_path = fw.config("site_root") + @"\App_Code\models\Reports";
        var src_file = reports_path + @"\Sample.cs";
        var dest_file = reports_path + @"\" + report_class.Replace("Report", "") + ".cs";

        if (File.Exists(dest_file))
            throw new UserException("Such report already exists");

        var content = Utils.getFileContent(src_file);
        if (content == "")
            throw new ApplicationException("Can't open Sample.cs");

        content = content.Replace("SampleReport", report_class);
        content = content.Replace("Sample report", Utils.capitalize(repcode) + " Report");

        Utils.setFileContent(dest_file, ref content);

        // copy templates
        var tpl_from = fw.config("template") + "/admin/reports/sample";
        var tpl_to = fw.config("template") + "/admin/reports/" + repcode.ToLower();
        Utils.CopyDirectory(tpl_from, tpl_to, true);

        // Add link to /Admin/Reports screen (main.html)
        var reports_index_file = fw.config("template") + "/admin/reports/index/main.html";
        var html = Utils.getFileContent(reports_index_file);
        if (!string.IsNullOrEmpty(html))
        {
            // Find the hidden template div (use escaped quotes for normal string)
            // <~tplcodegen if="0" inline><a href="<~../url>/<~tpl-code>" class="list-group-item"><~tpl-title></a></~tplcodegen>
            var pattern = "(<~tplcodegen.+?>(.+?)</~tplcodegen>)";
            var match = Regex.Match(html, pattern, RegexOptions.Singleline);
            if (match.Success)
            {
                var wholeDiv = match.Groups[1].Value;
                var hiddenDiv = match.Groups[2].Value;
                // Replace placeholders
                var newDiv = hiddenDiv
                    .Replace("<~tpl-code>", repcode)
                    .Replace("<~tpl-title>", Utils.capitalize(repcode) + " Report");
                // Insert before the hidden div
                html = html.Replace(wholeDiv, newDiv + "\n" + wholeDiv);
                Utils.setFileContent(reports_index_file, ref html);
            }
        }
    }
    // update by url
    private void updateMenuItem(string controller_url, string controller_title)
    {
        var fields = new FwDict()
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

    private static string buildRowClass(IDictionary entity)
    {
        if (entity == null || !entity.Contains("fields"))
            return string.Empty;

        if (entity["fields"] is not IList fields || fields.Count == 0)
            return string.Empty;

        StringBuilder sb = new();
        sb.AppendLine("    public class Row");
        sb.AppendLine("    {");

        foreach (var field in fields)
        {
            if (field is not IDictionary dict)
                continue;

            var columnName = dict["name"].toStr();
            if (columnName.Length == 0)
                continue;

            var propertyName = dict["fw_name"].toStr();
            if (propertyName.Length == 0)
                propertyName = Utils.name2fw(columnName);
            if (propertyName.Length == 0)
                continue;

            var csType = buildRowPropertyType(dict);
            if (string.IsNullOrEmpty(csType))
                continue;

            if (!string.Equals(propertyName, columnName, StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($"        [DBName(\"{columnName}\")]");

            var initializer = buildRowPropertyInitializer(dict, csType);
            if (initializer.Length > 0)
                sb.AppendLine($"        public {csType} {propertyName} {{ get; set; }} = {initializer};");
            else
                sb.AppendLine($"        public {csType} {propertyName} {{ get; set; }};");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string replaceRowClass(string template, string rowClassBlock)
    {
        if (string.IsNullOrEmpty(rowClassBlock))
            return template;

        var regex = new Regex(@"^\s*public class Row\s*\{.*?^\s*\}\s*", RegexOptions.Singleline | RegexOptions.Multiline);
        return regex.Replace(template, rowClassBlock, 1);
    }

    private static string buildRowPropertyType(IDictionary field)
    {
        var fwType = field["fw_type"].toStr().ToLowerInvariant();
        var fwSubtype = field["fw_subtype"].toStr().ToLowerInvariant();
        bool isNullable = field["is_nullable"].toBool();

        string baseType = fwType switch
        {
            "int" => fwSubtype == "bit" || fwSubtype == "boolean" ? "bool" : "int",
            "float" => fwSubtype == "decimal" || fwSubtype == "currency" || fwSubtype == "numeric" ? "decimal" : "double",
            "date" => "DateTime",
            "datetime" => "DateTime",
            _ => "string",
        };

        bool isValueType = baseType != "string";
        if (isNullable && isValueType)
            return baseType + "?";

        return baseType;
    }

    private static string buildRowPropertyInitializer(IDictionary field, string csType)
    {
        bool isNullable = field["is_nullable"].toBool();

        if (csType == "string" && !isNullable)
            return "string.Empty";

        return string.Empty;
    }
}
