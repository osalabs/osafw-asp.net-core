// Entity builder for Developers
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024  Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace osafw;

class DevEntityBuilder
{
    const string FW_TABLES = "fwsessions fwentities att_categories att att_links users settings spages log_types activity_logs lookup_manager_tables user_views user_lists user_lists_items menu_items";

    public static void createDBJsonFromExistingDB(string dbname, FW fw)
    {
        var db = fw.getDB(dbname);

        var entities = DevEntityBuilder.dbschema2entities(db);

        // save db.json
        DevEntityBuilder.saveJsonEntity(entities, fw.config("template") + DevCodeGen.DB_JSON_PATH);

        db.disconnect();
    }

    public static void createDBJsonFromText(string entities_text, FW fw)
    {
        var entities = ParseEntities(entities_text, fw);
        saveJsonEntity(entities, fw.config("template") + DevCodeGen.DB_JSON_PATH);
    }

    private static ArrayList ParseEntities(string inputText, FW fw)
    {
        var entities = new List<Dictionary<string, object>>();
        //split inputText by \r\n but leave empty lines (as empty lines delimit entities)
        var lines = Regex.Split(inputText, "\r\n|\n\r|\n|\r");
        int index = 0;
        //fw.logger("lines", lines);

        while (index < lines.Length)
        {
            //fw.logger("line: " + index, lines[index]);
            // Skip empty lines and comments
            while (index < lines.Length && (string.IsNullOrWhiteSpace(lines[index]) || lines[index].StartsWith("--")))
            {
                //fw.logger("index++");
                index++;
            }

            if (index >= lines.Length)
                break;

            // Entity-level comments
            string entityComments = "";
            int commentIndex = index - 1;
            while (commentIndex >= 0 && lines[commentIndex].StartsWith("--"))
            {
                entityComments = lines[commentIndex][2..].Trim() + "\n" + entityComments;
                commentIndex--;
            }

            // Entity name and parameters
            string entityLine = lines[index++].Trim();
            string[] entityParts = entityLine.Split(' ', 2);
            string entityName = entityParts[0];
            string entityParams = entityParts.Length > 1 ? entityParts[1] : "";

            //fw.logger($"index={index} Parsing entity: " + entityName + " with params: " + entityParams);
            string tableName = Utils.name2fw(entityName);

            var fields = new List<Dictionary<string, object>>();
            var foreignKeys = new List<Dictionary<string, string>>();
            var indexes = new Dictionary<string, string>();
            var entity = new Dictionary<string, object>
            {
                ["iname"] = Utils.name2human(entityName),
                ["table"] = tableName,
                ["fw_name"] = tableName,
                ["model_name"] = tablenameToModel(tableName),
                ["is_fw"] = true,
                ["db_config"] = "",
                ["fields"] = fields,
                ["foreign_keys"] = foreignKeys,
                ["indexes"] = indexes,
                ["comments"] = entityComments.Trim(),
            };
            entities.Add(entity);

            //add controller only if "noui" not specified
            if (!entityParams.Contains("noui"))
            {
                entity["controller"] = new Dictionary<string, object>
                {
                    ["title"] = Utils.name2human(entityName),
                    ["url"] = $"/Admin/" + entity["model_name"],
                    ["is_dynamic_show"] = true,
                    ["is_dynamic_showform"] = true
                };
            }
            if (entityParams.Contains("lookup"))
            {
                if (entity["controller"] == null)
                    entity["controller"] = new Dictionary<string, object>();
                ((Dictionary<string, object>)entity["controller"])["is_lookup"] = true;
            }

            bool includeStandardFields = !entityParams.Contains("nostd");
            if (includeStandardFields)
                AddStandardFieldsInitial(fields);

            // Parse entity fields
            while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]))
            {
                string line = lines[index++].Trim();
                //fw.logger($"entity line{index}: ", line);

                // Skip comments
                if (line.StartsWith("--"))
                    continue;

                // Remove inline comments
                string comment = "";
                int commentPos = line.IndexOf("--");
                if (commentPos >= 0)
                {
                    comment = line[(commentPos + 2)..].Trim();
                    line = line[..commentPos].Trim();
                }

                // Handle field removal
                if (line.EndsWith("remove"))
                {
                    string fieldName = line[..^"remove".Length].Trim();
                    RemoveField(fields, fieldName);
                    continue;
                }

                // check if this line is index definition
                var match = Regex.Match(line, @"(UNIQUE INDEX|INDEX)\s+\((.*?)\)");
                if (match.Success)
                {
                    string indexName = match.Groups[1].Value == "UNIQUE INDEX" ? "UX" : "IX";
                    string indexFields = match.Groups[2].Value;
                    indexes[indexName + (indexes.Count + 1)] = indexFields;
                    continue;
                }

                // check junction table from line
                // entityname junction
                if (line.EndsWith("junction"))
                {
                    //fw.logger("info", "Parsing junction: " + line);
                    var junction = ParseJunction(line, entity, comment);
                    entities.Add(junction);
                    continue;
                }

                // parse field line
                var field = ParseField(line, comment);
                if (field != null)
                {
                    fields.Add(field);

                    // Handle indexes
                    if (field.TryGetValue("unique", out object uvalue))
                    {
                        if ((bool)uvalue)
                            indexes["UX" + (indexes.Count + 1)] = field["name"].ToString();
                        field.Remove("unique");
                    }

                    // Handle foreign keys
                    if (field.TryGetValue("foreign_key", out object fkvalue))
                    {
                        foreignKeys.Add((Dictionary<string, string>)fkvalue);
                        field.Remove("foreign_key");
                        //also for each foreign key add index (unless it's already added as unique)
                        if (!indexes.ContainsValue(field["name"].ToString()))
                            indexes["IX" + (indexes.Count + 1)] = field["name"].ToString();
                    }

                }
            } //entity lines loop

            if (includeStandardFields)
                AddStandardFieldsAfter(fields);
        }// entities loop

        return new ArrayList(entities);
    }

    // id, iname, idesc
    private static void AddStandardFieldsInitial(List<Dictionary<string, object>> fields)
    {
        var standardFields = new List<Dictionary<string, object>>
            {
                CreateField("id", "ID", "int", "int", null, false, isIdentity: true),
                CreateField("iname", "Name", "varchar", "nvarchar", 255, false, defaultValue: ""),
                CreateField("idesc", "Notes", "varchar", "nvarchar", -1, false, defaultValue: ""),
            };

        fields.AddRange(standardFields);
    }

    // add fields status
    private static void AddStandardFieldsStatus(List<Dictionary<string, object>> fields)
    {
        fields.AddRange(
            [
                CreateField("status", "Status", "int", "tinyint", null, false, defaultValue: 0)
            ]);
    }

    //create 2 fields: add_time, add_users_id
    private static void AddStandardFieldsAdded(List<Dictionary<string, object>> fields)
    {
        fields.AddRange(
            [
                CreateField("add_time", "Added on", "datetime", "datetime2", null, false, defaultValue: "GETDATE()"),
                CreateField("add_users_id", "Added by", "int", "int", null, true)
            ]);
    }

    //create 2 fields: upd_time, upd_users_id
    private static void AddStandardFieldsUpdated(List<Dictionary<string, object>> fields)
    {
        fields.AddRange(
            [
                CreateField("upd_time", "Updated on", "datetime", "datetime2", null, true),
                CreateField("upd_users_id", "Updated by", "int", "int", null, true)
            ]);
    }

    private static void AddStandardFieldsAfter(List<Dictionary<string, object>> fields)
    {
        // add status, added and updated fields
        AddStandardFieldsStatus(fields);
        AddStandardFieldsAdded(fields);
        AddStandardFieldsUpdated(fields);
    }

    private static void RemoveField(List<Dictionary<string, object>> fields, string fieldName)
    {
        fields.RemoveAll(f => f["fw_name"].ToString().Equals(fieldName, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object> ParseJunction(string line, Dictionary<string, object> entity, string comment)
    {
        var linked_tblname = Utils.name2fw(line[..^"junction".Length].Trim());
        var junction_tblname = entity["table"] + "_" + linked_tblname;
        var junction = new Dictionary<string, object>
        {
            ["db_config"] = entity["db_config"],
            ["table"] = junction_tblname,
            ["iname"] = Utils.name2human(junction_tblname),
            ["fw_name"] = Utils.name2fw(junction_tblname),
            ["model_name"] = tablenameToModel(junction_tblname),
            ["is_fw"] = true,
            ["is_junction"] = true,
            ["comments"] = comment,
        };

        // link fields - one to main table, another - to lookup table
        var field_name1 = entity["table"] + "_id";
        var field_name2 = linked_tblname + "_id";
        var junction_fields = new List<Dictionary<string, object>>
                    {
                        CreateField(field_name1,Utils.name2human(entity["table"].ToString()), "int", "int", null, false),
                        CreateField(field_name2, Utils.name2human(linked_tblname), "int", "int", null, false)
                    };
        AddStandardFieldsStatus(junction_fields);
        AddStandardFieldsAdded(junction_fields);
        junction["fields"] = junction_fields;

        // foreign keys - to main table and lookup table
        var junction_foreign_keys = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object>
                        {
                            ["column"] = field_name1,
                            ["pk_table"] = entity["table"].ToString(),
                            ["pk_column"] = "id"
                        },
                        new Dictionary<string, object>
                        {
                            ["column"] = field_name2,
                            ["pk_table"] = linked_tblname,
                            ["pk_column"] = "id"
                        }
                    };
        junction["foreign_keys"] = junction_foreign_keys;

        // indexes
        junction["indexes"] = new Dictionary<string, string>
        {
            ["PK"] = $"{field_name1}, {field_name2}",
            ["UX1"] = $"{field_name2}, {field_name1}" //have an index with reversed fields order
        };

        return junction;
    }

    // parse single field line
    // syntax:
    // FieldName [Type(Length)] [NULL|NOT NULL] [DEFAULT(Value)] [UNIQUE] [UI:option,option(some other value),option,...]
    // FieldName.id [NULL] [UI:option,option(some other value),option,...] -- foreign key
    // FieldName FK(TableName.FieldName) [NULL] [UI:option,option(some other value),option,...] -- foreign key
    private static Dictionary<string, object> ParseField(string line, string comment)
    {
        var field = new Dictionary<string, object>();
        if (comment.Length > 0) field["comments"] = comment;

        // Split line into parts
        var tokens = Regex.Split(line, @"\s+");
        if (tokens.Length == 0)
            return null;

        int index = 0;

        // field is NOT NULL by default (except dates), but if NULL present - set to true
        bool is_nullable = false;
        bool is_notnull = false;
        bool is_null = false;
        // but if NOT NULL present - enforce it
        if (Regex.IsMatch(line, @"\bNOT NULL\b", RegexOptions.IgnoreCase))
        {
            is_nullable = false;
            is_notnull = true;
        }
        else if (Regex.IsMatch(line, @"\bNULL\b", RegexOptions.IgnoreCase))
        {
            //check if NULL present - force to true
            is_nullable = true;
            is_null = true;
        }

        field["unique"] = Regex.IsMatch(line, @"\bUNIQUE\b", RegexOptions.IgnoreCase);

        // extract UI options
        string ui = Regex.Match(line, @"\bUI:(.*)", RegexOptions.IgnoreCase).Groups[1].Value;
        if (!string.IsNullOrEmpty(ui))
        {
            var uiOptions = ParseUiOptions(ui);
            field["ui"] = uiOptions;

            // Handle 'required' UI option affecting nullability
            if (uiOptions.TryGetValue("required", out object value) && (bool)value)
            {
                is_nullable = is_null; // if NULL was present - keep it, otherwise set to false
                field["default"] = null; // enforce no default if field is required
            }
        }
        field["is_nullable"] = is_nullable ? 1 : 0;

        // Field name is always first
        string fieldName = tokens[index++];
        // can be in form "table_name.id" => make "table_name_id"
        if (fieldName[^3..] == ".id")
        {
            // this is foreign key field
            var pk_table = Utils.name2fw(Regex.Replace(fieldName, @"\.id$", ""));  // Customers.id => customers
            fieldName = pk_table + "_id";
            field["name"] = pk_table + "_id";
            field["iname"] = Utils.name2human(fieldName);
            field["fw_name"] = Utils.name2fw(fieldName);
            field["fw_type"] = "int";
            field["fw_subtype"] = "int";
            field["foreign_key"] = new Dictionary<string, string>
            {
                ["column"] = field["name"].ToString(),
                ["pk_table"] = pk_table,
                ["pk_column"] = "id"
            };

            return field;
        }

        //check if this if FK(TableName.FieldName) syntax
        if (tokens.Length > 1 && tokens[index].StartsWith("FK(", StringComparison.OrdinalIgnoreCase))
        {
            var fkParts = tokens[index][3..^1].Split('.');
            if (fkParts.Length == 2)
            {
                field["name"] = fieldName;
                field["iname"] = Utils.name2human(fieldName);
                field["fw_name"] = Utils.name2fw(fieldName);
                field["fw_type"] = "int";
                field["fw_subtype"] = "int";
                field["foreign_key"] = new Dictionary<string, string>
                {
                    ["column"] = fieldName,
                    ["pk_table"] = Utils.name2fw(fkParts[0]),
                    ["pk_column"] = fkParts[1]
                };

                return field;
            }
            else
                throw new Exception("Invalid foreign key syntax at line: " + line);
        }

        // normal field
        field["name"] = fieldName;
        field["iname"] = Utils.name2human(fieldName);
        field["fw_name"] = Utils.name2fw(fieldName);

        // check and extract DEFAULT value
        var defaultMatch = Regex.Match(line, @"DEFAULT\((.*?)\)", RegexOptions.IgnoreCase);
        if (defaultMatch.Success)
        {
            //remove anything from open bracket, if any (can happen if user type "getdate()" or "now()")
            var defaultVal = defaultMatch.Groups[1].Value;
            defaultVal = Regex.Replace(defaultVal, @"\(.+", "");

            field["default"] = defaultVal.Length > 0 ? defaultVal : null;
        }

        // basiscally only left to check is data type
        // if not data type - then it's UI options
        string fieldType = (tokens.Length > index) ? tokens[index++] : "";
        if (IsDataType(fieldType))
            ParseDataType(fieldType, field, is_notnull);
        else
            //default type is varchar
            ParseDataType("varchar", field, is_notnull);

        // already processed above

        // Set default value if not specified
        if (!field.ContainsKey("default") && field["is_nullable"].Equals(0))
            field["default"] = GetDefaultValueForType(field["fw_type"].ToString());

        return field;
    }

    private static bool IsDataType(string token)
    {
        //can be in form type or type(length) or type(length1,length2)
        if (string.IsNullOrEmpty(token))
            return false;

        // extract type
        if (token.Contains('('))
            token = token[..token.IndexOf('(')];

        return Utils.qh("varchar int smallint tinyint decimal date datetime datetime2 bit text currency").ContainsKey(token.ToLower());
    }

    // parse data type and length:
    // text => varchar(MAX)
    // varchar(255) => fw_type=varchar, maxlen=255
    // int => fw_type=int
    // currency => decimal(18,2)
    // decimal => decimal(18,2)
    // decimal(10,2) => fw_type=float, fw_subtype=decimal, numeric_precision=2
    private static void ParseDataType(string token, Dictionary<string, object> field, bool is_notnull)
    {
        // Handle data type and length
        var match = Regex.Match(token, @"(\w+)(?:\((.*?)\))?");
        if (!match.Success)
            throw new Exception("Invalid data type syntax: " + token);

        string dataType = match.Groups[1].Value.ToLower();
        string length = match.Groups[2].Success ? match.Groups[2].Value : null;

        switch (dataType)
        {
            case "text":
                field["fw_type"] = "varchar";
                field["fw_subtype"] = "nvarchar";
                field["maxlen"] = -1;
                break;
            case "varchar":
                field["fw_type"] = "varchar";
                field["fw_subtype"] = "nvarchar";
                field["maxlen"] = string.IsNullOrEmpty(length) ? 255 : (length.ToUpper() == "MAX" ? -1 : int.Parse(length));
                break;

            case "int":
                field["fw_type"] = "int";
                field["fw_subtype"] = "int";
                field["maxlen"] = 10;
                break;
            case "smallint":
                field["fw_type"] = "int";
                field["fw_subtype"] = "smallint";
                field["maxlen"] = 5;
                break;
            case "tinyint":
                field["fw_type"] = "int";
                field["fw_subtype"] = "tinyint";
                field["maxlen"] = 3;
                break;

            case "bit":
                field["fw_type"] = "int";
                field["fw_subtype"] = "bit";
                field["maxlen"] = 1;
                break;

            case "float":
                field["fw_type"] = "float";
                field["fw_subtype"] = "float";
                field["numeric_precision"] = 53;
                break;
            case "currency":
                field["fw_type"] = "float";
                field["fw_subtype"] = "decimal";
                field["numeric_precision"] = 18;
                field["numeric_scale"] = 2;
                break;
            case "decimal":
                field["fw_type"] = "float";
                field["fw_subtype"] = "decimal";

                // If length is not specified, use default precision and scale
                var parts = length?.Split(',');
                if (parts?.Length != 2)
                {
                    field["numeric_precision"] = 18;
                    field["numeric_scale"] = 2;
                }
                else
                {
                    field["numeric_precision"] = int.Parse(parts[0]);
                    field["numeric_scale"] = int.Parse(parts[1] ?? "0");
                }
                break;

            case "date":
                field["fw_type"] = "datetime";
                field["fw_subtype"] = "date";
                // Dates are nullable by default if not specified
                if (!is_notnull)
                    field["is_nullable"] = 1;
                break;
            case "datetime":
            case "datetime2":
                field["fw_type"] = "datetime";
                field["fw_subtype"] = "datetime2";
                // Dates are nullable by default if not specified
                if (!is_notnull)
                    field["is_nullable"] = 1;
                break;
            default:
                // Handle other types as needed
                break;
        }

    }

    // parse ui options
    // option,option(some other value),option,...
    private static Dictionary<string, object> ParseUiOptions(string uiOptions)
    {
        var result = new Dictionary<string, object>();
        var options = uiOptions.Split(',');

        foreach (var option in options)
        {
            var parts = option.Split(['(', ')'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                result[parts[0].Trim()] = true;
            }
            else if (parts.Length == 2)
            {
                result[parts[0].Trim()] = parts[1].Trim();
            }
            else
            {
                // case when option does not have value in brackets
                var key = option.Trim();
                if (string.IsNullOrEmpty(key))
                    continue; //skip empty options

                result[key] = true;
            }
        }

        return result;
    }

    private static object GetDefaultValueForType(string fwType)
    {
        return fwType switch
        {
            "varchar" => "",
            "int" => 0,
            "float" => 0,
            "datetime" => null,
            _ => null,
        };
    }

    private static Dictionary<string, object> CreateField(
        string name,
        string iname,
        string fwType,
        string fwSubtype,
        int? maxlen,
        bool isNullable,
        bool isIdentity = false,
        object defaultValue = null)
    {
        return new Dictionary<string, object>
        {
            ["name"] = name,
            ["iname"] = iname,
            ["fw_name"] = name,
            ["fw_type"] = fwType,
            ["fw_subtype"] = fwSubtype,
            ["maxlen"] = maxlen,
            ["is_nullable"] = isNullable ? 1 : 0,
            ["is_identity"] = isIdentity ? 1 : 0,
            ["numeric_precision"] = null,
            ["default"] = defaultValue
        };
    }

    public static bool isFwTableName(string table_name)
    {
        var tables = Utils.qh(FW_TABLES);
        return tables.ContainsKey(table_name.ToLower());
    }

    // demo_dicts => DemoDicts
    // TODO actually go thru models and find model with table_name
    public static string tablenameToModel(string table_name)
    {
        string result = "";
        string[] pieces = table_name.Split('_');
        foreach (string piece in pieces)
            result += Utils.capitalize(piece);
        return result;
    }

    // load json with expected type
    public static T loadJson<T>(string filename) where T : new()
    {
        T result;
        result = (T)Utils.jsonDecode(FW.getFileContent(filename));
        result ??= new T();
        return result;
    }

    // important - pass data as ArrayList or Hashtable to trigger custom converter
    public static void saveJsonEntity(object data, string filename)
    {
        string json_str;
        //use custom converter to output keys in specific order
        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };
        ConfigJsonConverter converter = new();
        converter.setOrderedKeys(converter.ordered_keys_entity);
        options.Converters.Add(converter);

        // Cast the data to object to ensure the custom converter handles it
        json_str = JsonSerializer.Serialize(data, data.GetType(), options);
        FW.setFileContent(filename, ref json_str);
    }

    // important - pass data as ArrayList or Hashtable to trigger custom converter
    public static void saveJsonController(object data, string filename)
    {
        string json_str;
        //use custom converter to output keys in specific order
        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };
        ConfigJsonConverter converter = new();
        converter.setOrderedKeys(converter.ordered_keys_controller);
        options.Converters.Add(converter);

        json_str = JsonSerializer.Serialize(data, data.GetType(), options);
        FW.setFileContent(filename, ref json_str);
    }


    // ****************************** PRIVATE HELPERS (move to Dev model?)

    public static ArrayList dbschema2entities(DB db)
    {
        ArrayList result = [];
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
            if (tblname.StartsWith("MSys", StringComparison.Ordinal))
                continue;

            // get table schema
            var tblschema = db.loadTableSchemaFull(tblname);
            // logger(tblschema)

            Hashtable controller_options = [];
            Hashtable table_entity = new()
            {
                ["db_config"] = db.db_name,
                ["table"] = tblname,
                ["fw_name"] = Utils.name2fw(tblname), // new table name using fw standards
                ["iname"] = Utils.name2human(tblname), // human table name
                ["fields"] = tableschema2fields(tblschema),
                ["foreign_keys"] = db.listForeignKeys(tblname),
                ["controller_options"] = controller_options,
            };

            table_entity["model_name"] = tablenameToModel((string)table_entity["fw_name"]); // potential Model Name
            controller_options["url"] = "/Admin/" + table_entity["model_name"]; // potential Controller URL/Name/Title
            controller_options["title"] = Utils.name2human((string)table_entity["model_name"]);

            // set is_fw flag - if it's fw compatible (contains id,iname,status,add_time,add_users_id)
            var fields = Utils.array2hashtable((ArrayList)table_entity["fields"], "name");
            // AndAlso fields.Contains("iname")
            table_entity["is_fw"] = fields.Contains("id") && fields.Contains("status") && fields.Contains("add_time") && fields.Contains("add_users_id");
            result.Add(table_entity);
        }

        return result;
    }

    public static ArrayList tableschema2fields(ArrayList schema)
    {
        ArrayList result = new(schema);

        foreach (Hashtable fldschema in schema)
        {
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

    public static List<string> listModels()
    {
        var baseType = typeof(FwModel);
        var assembly = baseType.Assembly;
        return (from t in assembly.GetTypes()
                where t.IsSubclassOf(baseType)
                orderby t.Name
                select t.Name).ToList();
    }

    public static List<string> listControllers()
    {
        var baseType = typeof(FwController);
        var assembly = baseType.Assembly;
        return (from t in assembly.GetTypes()
                where t.IsSubclassOf(baseType)
                orderby t.Name
                select t.Name).ToList();
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

        return
        [
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
        ];
    }
}
