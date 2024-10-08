using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace osafw;

class DevEntityBuilder
{
    const string FW_TABLES = "fwsessions fwentities att_categories att att_links users settings spages log_types activity_logs lookup_manager_tables user_views user_lists user_lists_items menu_items";

    public static void test(FW fw)
    {
        string inputText = @"
-- from TimeCard
-- another entity level comment
Timesheets
iname remove
users.id UI:required,data-refresh(1) --EmployeeID
idate date ui:required,label(Work Date),data-calendar --WorkDate
eow date UI:required,placeholder(Enter End Of Week) --end of week
begin_time datetime NULL --idate+time start
end_time datetime NULL --idate+time end
break_min int default(0) --minutes 0, 15, 30, 45 60
salary currency
overtime currency
rate currency UI:placeholder(Pay Rate)
is_draft bit default(0) ui:checkbox --draft or final
secondary_users_id FK(users.id) NULL --secondary employee (optional)
projects junction -- junction subtable with projects
UNIQUE INDEX (idate, users_id)
INDEX (begin_time)

-- Customer entity with standard fields
Customers
email UNIQUE UI:required -- Email is unique and required
phone varchar(20) -- Defining specific field length
address text -- Makes SQL field ""address nvarchar(MAX) NOT NULL DEFAULT ''""
is_active bit DEFAULT(1) UI:checkbox -- Makes ""is_active bit DEFAULT(1) NULL""

-- Notes for customers, excludes standard fields
customers_notes nostd
customers.id UI:required -- Foreign key to customers, NOT NULL because of required
idesc text -- Using idesc field for the note content
add_time datetime2 DEFAULT(getdate())
add_users_id int DEFAULT 0

-- Vendor entity with standard fields
vendors
iname UI:label(Company name) -- Override label, all other params standard
contact_name
phone varchar(20)
email UNIQUE
address text

-- Lookup table for product categories
categories lookup

-- Product entity with vendor link and category junction
products
vendors.id UI:required -- Foreign key to vendors
categories junction -- Creates junction table `products_categories`
price decimal(10,2) DEFAULT(0.0) UI:number,required,step(0.1)
stock_quantity int DEFAULT(0)
UNIQUE INDEX (iname) -- 'iname' (product name) is unique

-- Orders entity with items junction
orders
customers.id UI:required -- Foreign key to customers
order_date date DEFAULT(getdate())
total_amount decimal(10,2) DEFAULT(0.0)

-- Order items entity with additional fields, no controller
orders_items noui
iname remove -- Remove some standard fields
idesc remove
orders.id UI:required -- Foreign key to orders
products.id UI:required -- Foreign key to products
quantity int DEFAULT(1)
price decimal(10,2) DEFAULT(0.0)
UNIQUE INDEX (orders_id, products_id)";

        var entities = ParseEntities(inputText, fw);
        //convert to ArrayList
        saveJsonEntity(entities, fw.config("template") + "/dev/test.json");
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
                    ["url"] = $"/Admin/{entityName}",
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
                    indexes[indexName + "_" + (indexes.Count + 1)] = indexFields;
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
                            indexes["UX_" + (indexes.Count + 1)] = field["name"].ToString();
                        field.Remove("unique");
                    }

                    // Handle foreign keys
                    if (field.TryGetValue("foreign_key", out object fkvalue))
                    {
                        foreignKeys.Add((Dictionary<string, string>)fkvalue);
                        field.Remove("foreign_key");
                        //also for each foreign key add index (unless it's already added as unique)
                        if (!indexes.ContainsValue(field["name"].ToString()))
                            indexes["IX_" + (indexes.Count + 1)] = field["name"].ToString();
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
                CreateField("add_time", "Added on", "datetime", "datetime2", null, false, defaultValue: "getdate()"),
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
            ["UX_1"] = $"{field_name2}, {field_name1}" //have an index with reversed fields order
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
            field["default"] = defaultMatch.Groups[1].Value.Length > 0 ? defaultMatch.Groups[1].Value : null;
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

        return Utils.qh("varchar int smallint decimal datetime date bit text currency").ContainsKey(token.ToLower());
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
        var uiDict = new Dictionary<string, object>();
        var options = uiOptions.Split(',');

        foreach (var option in options)
        {
            if (option.Equals("required", StringComparison.OrdinalIgnoreCase))
            {
                uiDict["required"] = true;
            }
            else if (option.StartsWith("label(", StringComparison.OrdinalIgnoreCase))
            {
                var label = option.Substring(6, option.Length - 7);
                uiDict["label"] = label;
            }
            else if (option.StartsWith("placeholder(", StringComparison.OrdinalIgnoreCase))
            {
                var placeholder = option.Substring(12, option.Length - 13);
                uiDict["placeholder"] = placeholder;
            }
            else if (option.StartsWith("data-", StringComparison.OrdinalIgnoreCase))
            {
                var dataAttr = option.Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                if (dataAttr.Length == 2)
                {
                    uiDict[dataAttr[0]] = dataAttr[1];
                }
            }
            else
            {
                // Handle other UI options as needed
            }
        }

        return uiDict;
    }

    private static string GetDefaultValueForType(string fwType)
    {
        switch (fwType)
        {
            case "varchar":
                return "";
            case "int":
                return "0";
            case "float":
                return "0.0";
            case "datetime":
                return null;
            default:
                return null;
        }
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
        if (result == null)
            result = new T();
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

}
