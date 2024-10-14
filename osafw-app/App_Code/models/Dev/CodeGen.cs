using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public DevCodeGen(FW fw, DB db = null)
    {
        this.fw = fw;
        if (db == null)
            // if db not passed - use default db
            this.db = fw.db;
        else
            this.db = db;
    }

    public static DevCodeGen init(FW fw, DB db = null)
    {
        return new DevCodeGen(fw, db);
    }

    public static void replaceInFile(string filepath, Hashtable strings)
    {
        var content = FW.getFileContent(filepath);
        if (content.Length == 0)
            return;

        foreach (string str in strings.Keys)
            content = content.Replace(str, (string)strings[str]);

        FW.setFileContent(filepath, ref content);
    }

    // replaces strings in all files under defined dir
    // RECURSIVE!
    private static void replaceInFiles(string dir, Hashtable strings)
    {
        foreach (string filename in Directory.GetFiles(dir))
            replaceInFile(filename, strings);

        // dive into dirs
        foreach (string foldername in Directory.GetDirectories(dir))
            replaceInFiles(foldername, strings);
    }

    private static string entityFieldToSQLType(Hashtable entity)
    {
        string result;

        switch (entity["fw_type"])
        {
            case "int":
                {
                    if (Utils.toStr(entity["fw_subtype"]) == "boolean" || Utils.toStr(entity["fw_subtype"]) == "bit")
                        result = "BIT";
                    else if (Utils.toInt(entity["numeric_precision"]) == 3 || Utils.toStr(entity["fw_subtype"]) == "tinyint")
                        result = "TINYINT";
                    else if (Utils.toInt(entity["numeric_precision"]) == 5 || Utils.toStr(entity["fw_subtype"]) == "smallint")
                        result = "SMALLINT";
                    else
                        result = "INT";
                    break;
                }

            case "float":
                {
                    if (Utils.toStr(entity["fw_subtype"]) == "currency")
                        result = "DECIMAL(18,2)";
                    else if (Utils.toStr(entity["fw_subtype"]) == "decimal")
                        result = "DECIMAL(" + Utils.toInt(entity["numeric_precision"]) + "," + Utils.toInt(entity["numeric_scale"]) + ")";
                    else
                        result = "FLOAT";
                    break;
                }

            case "datetime":
                {
                    if (Utils.toStr(entity["fw_subtype"]) == "date")
                        result = "DATE";
                    else
                        result = "DATETIME2";
                    break;
                }

            default:
                {
                    result = "NVARCHAR";
                    var maxlen = Utils.toInt(entity["maxlen"]);
                    if (maxlen > 0 & maxlen < 256)
                        result += "(" + entity["maxlen"] + ")";
                    else
                        result += "(MAX)";
                    break;
                }
        }

        return result;
    }

    private string entityFieldToSQLDefault(Hashtable entity)
    {
        var result = "";
        if (entity["default"] == null)
            return result;

        string def = Utils.toStr(entity["default"]);
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

            if (Utils.toStr(entity["fw_type"]) == "int")
                // if field type int - convert to int
                result += "(" + db.qi(def) + ")";
            else
                result += "(" + db.q(def) + ")";
        }

        return result;
    }

    // if field is referece to other table - add named foreign key
    // CONSTRAINT FK_entity["table_name")]remotetable FOREIGN KEY REFERENCES remotetable(id)
    private string entityFieldToSQLForeignKey(Hashtable field, Hashtable entity)
    {
        var result = "";

        if (!entity.ContainsKey("foreign_keys"))
            return result;

        foreach (Hashtable fk in (ArrayList)entity["foreign_keys"])
        {
            fw.logger("CHECK FK:", fk["column"], "=", field["name"]);
            if ((string)fk["column"] == (string)field["name"])
            {
                //build FK name as FK_TABLE_FIELDWITHOUTID
                var fk_name = (string)fk["column"];
                fk_name = Regex.Replace(fk_name, "_id$", "", RegexOptions.IgnoreCase);
                result = " CONSTRAINT FK_" + entity["fw_name"] + "_" + Utils.name2fw(fk_name) + " FOREIGN KEY REFERENCES " + db.qid((string)fk["pk_table"], false) + "(" + db.qid((string)fk["pk_column"], false) + ")";
                break;
            }
        }
        fw.logger("FK result: ", result);

        return result;
    }

    // convert db.json entity to SQL CREATE TABLE
    private string entity2SQL(Hashtable entity)
    {
        var table_name = (string)entity["table"];
        var result = "CREATE TABLE " + db.qid(table_name, false) + " (" + Environment.NewLine;

        var i = 1;
        var fields = (ArrayList)entity["fields"];
        foreach (Hashtable field in fields)
        {
            var fsql = "";
            var field_name = Utils.toStr(field["name"]);
            if (field_name == "status")
                fsql += Environment.NewLine; // add empty line before system fields starting with "status"

            fsql += "  " + db.qid(field_name, false).PadRight(21, ' ') + " " + entityFieldToSQLType(field);
            if (Utils.toInt(field["is_identity"]) == 1)
                fsql += " IDENTITY(1, 1) PRIMARY KEY CLUSTERED";
            fsql += Utils.toInt(field["is_nullable"]) == 0 ? " NOT NULL" : "";
            fsql += entityFieldToSQLDefault(field);
            fsql += entityFieldToSQLForeignKey(field, entity);
            fsql += (i < fields.Count ? "," : "");
            if (field.ContainsKey("comments"))
                fsql = fsql.PadRight(64, ' ') + "-- " + field["comments"];

            result += fsql + Environment.NewLine;
            i += 1;
        }

        Hashtable indexes = (Hashtable)entity["indexes"] ?? null;
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
        var entities = DevEntityBuilder.loadJson<ArrayList>(config_file);

        // drop all FKs we created before, so we'll be able to drop tables later
        DBList fks = db.arrayp(@"SELECT fk.name, o.name as table_name 
                        FROM sys.foreign_keys fk, sys.objects o 
                        where fk.is_system_named=0 
                          and o.object_id=fk.parent_object_id", DB.h());
        foreach (var fk in fks)
            db.exec("ALTER TABLE " + db.qid(fk["table_name"], false) + " DROP CONSTRAINT " + db.qid(fk["name"], false));

        foreach (Hashtable entity in entities)
        {
            var sql = entity2SQL(entity);
            // create db tables directly in db

            try
            {
                db.exec("DROP TABLE IF EXISTS " + db.qid((string)entity["table"], false));
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
        var entities = DevEntityBuilder.loadJson<ArrayList>(config_file);

        var database_sql = "";
        foreach (Hashtable entity in entities)
        {
            var sql = entity2SQL(entity);
            // only create App_Data/database.sql
            // add drop
            if (entity.ContainsKey("comments"))
                database_sql += "-- " + entity["comments"] + Environment.NewLine;
            database_sql += "DROP TABLE IF EXISTS " + db.qid((string)entity["table"], false) + ";" + Environment.NewLine;
            database_sql += sql + ";" + Environment.NewLine + Environment.NewLine;
        }

        var sql_file = fw.config("site_root") + DevCodeGen.DB_SQL_PATH;
        FW.setFileContent(sql_file, ref database_sql);
    }

    public void createModelsAndControllersFromDBJson()
    {
        var config_file = fw.config("template") + DB_JSON_PATH;
        var entities = DevEntityBuilder.loadJson<ArrayList>(config_file);

        foreach (Hashtable entity in entities)
        {
            this.createModel(entity);
            this.createController(entity, entities);
        }
    }

    public void createModel(Hashtable entity)
    {
        string table_name = (string)entity["table"];
        string model_name = (string)entity["model_name"];
        bool is_junction = Utils.toBool(entity["is_junction"]);

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
            mdemo = FW.getFileContent(path + @"\DemosDemoDicts.cs");
            if (mdemo == "")
                throw new ApplicationException("Can't open DemosDemoDicts.cs");

            // replace: DemosDemoDicts => ModelName, demos_demo_dicts => table_name
            mdemo = mdemo.Replace("DemosDemoDicts", model_name);
            mdemo = mdemo.Replace("demos_demo_dicts", table_name);
            mdemo = mdemo.Replace("db_config = \"\"", "db_config = \"" + entity["db_config"] + "\"");

            // setup junction_* fields from foreign_keys (first is main, second is linked)
            var model_main = "";
            var field_main_id = "";
            var model_linked = "";
            var field_linked_id = "";
            foreach (Hashtable fk in (ArrayList)entity["foreign_keys"])
            {
                if (field_main_id == "")
                {
                    model_main = DevEntityBuilder.tablenameToModel(Utils.name2fw((string)fk["pk_table"]));
                    field_main_id = (string)fk["column"];
                }
                else
                {
                    model_linked = DevEntityBuilder.tablenameToModel(Utils.name2fw((string)fk["pk_table"]));
                    field_linked_id = (string)fk["column"];
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
            mdemo = FW.getFileContent(path + @"\DemoDicts.cs");
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
                var fields = Utils.array2hashtable((ArrayList)entity["fields"], "name");

                // detect id and iname fields
                var i = 1;
                Hashtable fld_int = null;
                Hashtable fld_identity = null;
                Hashtable fld_iname = null;
                var is_normalize_names = false;
                foreach (Hashtable fld in (ArrayList)entity["fields"])
                {
                    // find identity
                    if (fld_identity == null && Utils.toStr(fld["is_identity"]) == "1")
                        fld_identity = fld;

                    // first int field
                    if (fld_int == null && Utils.toStr(fld["fw_type"]) == "int")
                        fld_int = fld;

                    // for iname - just use 2nd to 4th field which not end with ID, varchar type and has some maxlen
                    if (fld_iname == null && i >= 2 && i <= 4 && Utils.toStr(fld["fw_type"]) == "varchar" && Utils.toInt(fld["maxlen"]) > 0 && Utils.Right(Utils.toStr(fld["name"]), 2).ToLower() != "id")
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

                if (is_normalize_names || !Utils.toBool(entity["is_fw"]))
                    codegen += "        is_normalize_names = true;" + Environment.NewLine;
            }

            mdemo = mdemo.Replace("//###CODEGEN", codegen);
        }

        // copy demo model to model_name.cs
        FW.setFileContent(path + @"\" + model_name + ".cs", ref mdemo);
    }

    private void createLookup(Hashtable entity)
    {
        var ltable = fw.model<LookupManagerTables>().oneByTname((string)entity["table"]);

        string columns = "";
        string column_names = "";
        string column_types = "";
        bool is_first = true;

        var hfks = Utils.array2hashtable((ArrayList)entity["foreign_keys"], "column");

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

        //var fields = Utils.array2hashtable((ArrayList)entity["fields"], "fw_name");
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
            fw.model<LookupManagerTables>().update(Utils.toInt(ltable["id"]), item);
        else
            fw.model<LookupManagerTables>().add(item);
    }

    public void createController(Hashtable entity, ArrayList entities)
    {
        string model_name = (string)entity["model_name"];
        string controller_url = (string)entity["controller_url"];
        string controller_title = (string)entity["controller_title"];
        string controller_type = (string)entity["controller_type"];

        if (controller_url == "")
        {
            //if controller url explicitly empty - do not create controller
            return;
        }
        //if controller url is not defined - default to admin model
        controller_url ??= "/Admin/" + model_name;

        var controller_name = controller_url.Replace("/", "");
        if (controller_title == "")
            controller_title = Utils.name2human(model_name);

        if (model_name == "")
            throw new ApplicationException("No model or no controller name or no title");
        // If _controllers().Contains(controller_name & "Controller") Then Throw New ApplicationException("Such controller already exists")

        // save back to entity as it can be used by caller
        entity["controller_url"] = controller_url;
        entity["controller_title"] = controller_title;

        if (Utils.toBool(entity["controller_is_lookup"]))
        {
            // if requested controller as a lookup table - just add/update lookup tables, no actual controller creation
            this.createLookup(entity);
            return;
        }

        // determine controller type used as a template
        var controller_from_class = "AdminDemosDynamic";
        var controller_from_url = "/Admin/DemosDynamic";
        var controller_from_title = "Demo Dynamic";
        if (controller_type == "vue")
        {
            controller_from_class = "AdminDemosVue";
            controller_from_url = "/Admin/DemosVue";
            controller_from_title = "Demo Vue";
        };

        // copy DemoDicts.cs to model_name.cs
        var path = fw.config("site_root") + @"\App_Code\controllers";
        var mdemo = FW.getFileContent(path + @$"\{controller_from_class}.cs");
        if (mdemo == "")
            throw new ApplicationException($"Can't open {controller_from_class}.cs");

        // replace: DemoDicts => ModelName, demo_dicts => table_name
        mdemo = mdemo.Replace(controller_from_class, controller_name);
        mdemo = mdemo.Replace(controller_from_url, controller_url);
        mdemo = mdemo.Replace("DemoDicts", model_name);
        mdemo = mdemo.Replace("Demos", model_name);

        FW.setFileContent(path + @"\" + controller_name + ".cs", ref mdemo);

        // copy templates from /admin/demosdynamic to /controller/url
        var tpl_from = fw.config("template") + controller_from_url.ToLower();
        var tpl_to = fw.config("template") + controller_url.ToLower();
        Utils.CopyDirectory(tpl_from, tpl_to, true);

        // replace in templates: DemoDynamic to Title
        // replace in url.html /Admin/DemosDynamic to controller_url
        Hashtable replacements = new()
        {
            { controller_from_url, controller_url },
            { controller_from_title, controller_title }
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
        var config = DevEntityBuilder.loadJson<Hashtable>(config_file);

        updateControllerConfig(entity, config, entities);

        // Utils.jsonEncode(config) - can't use as it produces unformatted json string
        DevEntityBuilder.saveJsonController(config, config_file);
    }

    public void updateControllerConfig(Hashtable entity, Hashtable config, ArrayList entities)
    {
        string model_name = (string)entity["model_name"];
        string table_name = (string)entity["table"];
        string controller_type = (string)entity["controller_type"];
        fw.logger($"updating config for controller({controller_type})=", entity["controller_url"]);

        var sys_fields = Utils.qh(SYS_FIELDS);

        Hashtable tables = []; // hindex by table name to entities
        ArrayList fields = (ArrayList)entity["fields"];
        if (fields == null)
        {
            // TODO deprecate reading from db, always use entity info
            DB db;
            if (Utils.toStr(entity["db_config"]).Length > 0)
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

        var is_fw = Utils.toBool(entity["is_fw"]);

        //build index by field name
        Hashtable hfields = []; // name => fld index
        foreach (Hashtable fld in fields)
            hfields[Utils.toStr(fld["name"])] = fld;

        var foreign_keys = (ArrayList)entity["foreign_keys"] ?? [];
        //add system user fields to fake foreign keys, so it can generate list query with user names
        var hforeign_keys = Utils.array2hashtable(foreign_keys, "column"); // column -> fk info
        if (hfields.ContainsKey("add_users_id") && !hforeign_keys.ContainsKey("add_users_id"))
        {
            Hashtable fk = new()
            {
                ["pk_column"] = "id",
                ["pk_table"] = "users",
                ["column"] = "add_users_id"
            };
            foreign_keys.Add(fk);
        }
        if (hfields.ContainsKey("upd_users_id") && !hforeign_keys.ContainsKey("add_users_id"))
        {
            Hashtable fk = new()
            {
                ["pk_column"] = "id",
                ["pk_table"] = "users",
                ["column"] = "upd_users_id"
            };
            foreign_keys.Add(fk);
        }
        hforeign_keys = Utils.array2hashtable(foreign_keys, "column"); // refresh in case new foreign keys added above

        ArrayList saveFields = [];
        ArrayList saveFieldsNullable = [];
        Hashtable hFieldsMap = [];   // name => iname - map for the view_list_map
        Hashtable hFieldsMapEdit = []; // for Vue editable list
        Hashtable hFieldsMapFW = []; // fw_name => name
        ArrayList showFieldsLeft = [];
        ArrayList showFieldsRight = [];
        ArrayList showFormFieldsLeft = [];
        ArrayList showFormFieldsRight = []; // system fields - to the right

        foreach (Hashtable fld in fields)
        {
            string fld_name = Utils.toStr(fld["name"]);
            fw.logger("field name=", fld_name, fld);

            if (Utils.toStr(fld["fw_name"]) == "")
                fld["fw_name"] = Utils.name2fw(fld_name); // system name using fw standards
            if (Utils.toStr(fld["iname"]) == "")
                fld["iname"] = Utils.name2human(fld_name); // human name using fw standards

            var is_field_fk = hforeign_keys.ContainsKey(fld_name);
            var fk_field_name = "";

            hFieldsMapEdit[fld_name] = fld["iname"]; //use regular field for Vue editable list
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

            Hashtable sf = [];  // show fields
            Hashtable sff = []; // showform fields
            var is_skip = false;
            sf["field"] = fld_name;
            sf["label"] = fld["iname"];
            sf["type"] = "plaintext";

            sff["field"] = fld_name;
            sff["label"] = fld["iname"];

            if (Utils.toStr(fld["is_nullable"]) == "0" && fld["default"] == null)
                sff["required"] = true;// if not nullable and no default - required

            if (Utils.toStr(fld["is_nullable"]) == "1")
                saveFieldsNullable.Add(fld_name);

            var maxlen = Utils.toInt(fld["maxlen"]);
            if (maxlen > 0)
                sff["maxlength"] = maxlen;
            if (Utils.toStr(fld["fw_type"]) == "varchar")
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
            else if (Utils.toStr(fld["fw_type"]) == "int")
            {
                // int fields could be: foreign keys, yes/no, just a number input

                // check foreign keys - and make type=select
                var is_fk = false;
                foreach (Hashtable fkinfo in foreign_keys)
                {
                    if ((string)fkinfo["column"] == fld_name)
                    {
                        is_fk = true;
                        var mname = DevEntityBuilder.tablenameToModel(Utils.name2fw((string)fkinfo["pk_table"]));

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
                    else if (Utils.toStr(fld["fw_subtype"]) == "boolean" || Utils.toStr(fld["fw_subtype"]) == "bit" || fld_name.StartsWith("is_") || Regex.IsMatch(fld_name, @"^Is[A-Z]"))
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
            else if (Utils.toStr(fld["fw_type"]) == "float")
            {
                sff["type"] = "number";
                sff["step"] = 0.1;
                sff["class_contents"] = "col-md-4";
            }
            else if (Utils.toStr(fld["fw_type"]) == "datetime")
            {
                sf["type"] = "date";
                sff["type"] = "date_popup";
                sff["class_contents"] = "col-md-5";
            }
            else
                // everything else - just input
                sff["type"] = "input";

            if (Utils.toStr(fld["is_identity"]) == "1")
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
            if (Utils.toStr(fld["is_identity"]) == "1" || sys_fields.Contains(fld_name))
            {
                // add to system fields
                showFieldsRight.Add(sf);
                showFormFieldsRight.Add(sff);
                is_sys = true;
            }
            else
            {
                //non-system fields
                if (Utils.toStr(sf["type"]) == "att"
                    || Utils.toStr(sf["type"]) == "att_links"
                    || Utils.toStr(sff["type"]) == "textarea" && fields.Count >= 10)
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
                //table could be a junction table name then
                var tentity = (Hashtable)tables[table];
                var is_junction = Utils.toBool(tentity["is_junction"]);
                string junction_model = (string)tentity["model_name"];
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
                        //{ "lookup_model", DevEntityBuilder.tablenameToModel(table_name_linked) },
                        //{ "table_link", table_name_link },
                        //{ "table_link_id_name", table_name + "_id" },
                        //{ "table_link_linked_id_name", table_name_linked + "_id" }
                    };
                    if (is_junction)
                        sflink["model"] = junction_model;
                    else
                        sflink["lookup_model"] = DevEntityBuilder.tablenameToModel(table_name_linked);

                    Hashtable sfflink = new()
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
                if (Utils.toInt(field["is_nullable"]) == 1)
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
        // alternatively - just show couple fields
        // If is_fw Then config("view_list_defaults") = "id" & If(hfields.ContainsKey("iname"), " iname", "") & If(hfields.ContainsKey("add_time"), " add_time", "") & If(hfields.ContainsKey("status"), " status", "")

        // show first 6 fields (priority to required) +status, except identity, large text and system fields
        config["view_list_defaults"] = "";
        var edit_list_defaults = "";
        int defaults_ctr = 0;
        var rfields = (from Hashtable fld in fields
                       where Utils.toStr(fld["is_identity"]) != "1"
                         && !(Utils.toStr(fld["fw_type"]) == "varchar" && Utils.toInt(fld["maxlen"]) <= 0)
                         && !(sys_fields.Contains(fld["name"]) && Utils.toStr(fld["name"]) != "status")
                       orderby (Utils.toStr(fld["is_nullable"]) == "0" && fld["default"] == null) descending
                       select fld);
        foreach (Hashtable field in rfields)
        {
            var fname = (string)field["name"];
            if (defaults_ctr > 5 && fname != "status")
                continue;

            edit_list_defaults += (defaults_ctr == 0 ? "" : " ") + fname; //for edit list we need real field names only

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

        if (controller_type == "vue")
        {
            config["is_dynamic_index_edit"] = false; // by default disable list editing        
            config["list_edit"] = table_name;
            config["edit_list_defaults"] = edit_list_defaults;
            config["edit_list_map"] = hFieldsMapEdit;
        }

        config["is_dynamic_show"] = entity.ContainsKey("controller_is_dynamic_show") ? entity["controller_is_dynamic_show"] : true;
        if ((bool)config["is_dynamic_show"])
        {
            var showFields = new ArrayList
            {
                Utils.qh("type|row"),
                Utils.qh("type|col class|col-lg-6")
            };
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
            var showFormFields = new ArrayList
            {
                Utils.qh("type|row"),
                Utils.qh("type|col class|col-lg-6")
            };
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
            if (key.StartsWith('#'))
                config.Remove(key);
        }
    }

    public static void makeValueTags(ArrayList fields)
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


}