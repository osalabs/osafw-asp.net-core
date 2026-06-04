// Stored custom reports model
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public class FwReportsModel : FwModel<FwReportsModel.Row>
{
    public const int DEFAULT_ROW_LIMIT = 1000;
    public const int DEFAULT_PREVIEW_LIMIT = 50;
    public const int DEFAULT_LOOKUP_LIMIT = 500;
    public const int DEFAULT_TIMEOUT_SECONDS = 30;

    private static readonly Regex ParamRegex = new(@"(?<!@)@([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex ForbiddenSqlRegex = new(@"\b(insert|update|delete|merge|drop|alter|create|truncate|exec|execute|grant|revoke|backup|restore|into)\b|\b(?:sp_|xp_)\w*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public class Row
    {
        public int id { get; set; }
        public string icode { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public string icon { get; set; } = string.Empty;
        public int access_level { get; set; }
        public string sql_template { get; set; } = string.Empty;
        public string params_json { get; set; } = string.Empty;
        public string render_options_json { get; set; } = string.Empty;
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public FwReportsModel() : base()
    {
        db_config = "";
        table_name = "fwreports";
        field_iname = "iname";
        field_icode = "icode";
    }

    /// <summary>
    /// Cleans a custom report code for route/resource use while keeping it compatible with existing report codes.
    /// </summary>
    /// <param name="icode">Submitted report code.</param>
    /// <returns>Route-safe report code.</returns>
    public static string cleanupIcode(string icode)
    {
        return FwReports.cleanupRepcode((icode ?? string.Empty).Trim());
    }

    /// <summary>
    /// Cleans a Bootstrap Icons suffix before it is rendered into a CSS class.
    /// </summary>
    /// <param name="icon">Submitted icon name, optionally with a bi prefix.</param>
    /// <returns>Safe Bootstrap Icons suffix, such as currency-dollar.</returns>
    public static string cleanupIcon(string icon)
    {
        var result = Regex.Replace((icon ?? string.Empty).Trim().ToLowerInvariant().Replace('_', '-'), @"\s+", "-");
        while (result.StartsWith("bi-", StringComparison.OrdinalIgnoreCase))
            result = result[3..];

        result = Regex.Replace(result, @"[^a-z0-9-]+", "");
        return result.Length > 64 ? result[..64] : result;
    }

    /// <summary>
    /// Returns parameter placeholders from a stored SQL template so save, preview, and runtime binding agree.
    /// </summary>
    /// <param name="sql">Report SQL template.</param>
    /// <returns>Unique parameter names without the leading at sign, in first-use order.</returns>
    public static StrList extractParamNames(string sql)
    {
        var result = new StrList();
        var stripped = stripSqlLiteralsAndComments(sql ?? string.Empty);
        foreach (Match match in ParamRegex.Matches(stripped))
        {
            var name = match.Groups[1].Value;
            if (!result.Contains(name))
                result.Add(name);
        }
        return result;
    }

    /// <summary>
    /// Validates that admin-authored report SQL stays in the read-only custom-report subset.
    /// </summary>
    /// <param name="sql">Report SQL template submitted by a Site Admin.</param>
    /// <exception cref="UserException">Thrown when SQL is empty, not SELECT/CTE-shaped, or contains forbidden statements.</exception>
    public static void validateSqlTemplate(string sql)
    {
        var trimmed = (sql ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new UserException("Report SQL is required");

        var stripped = stripSqlLiteralsAndComments(trimmed);
        if (stripped.Contains(';'))
            throw new UserException("Report SQL must contain one statement without semicolons");

        if (!Regex.IsMatch(stripped, @"^\s*(select|with)\b", RegexOptions.IgnoreCase))
            throw new UserException("Report SQL must start with SELECT or WITH");

        if (ForbiddenSqlRegex.IsMatch(stripped))
            throw new UserException("Report SQL can only read data");
    }

    /// <summary>
    /// Parses and normalizes parameter metadata, adding default text params for placeholders with no metadata.
    /// </summary>
    /// <param name="sql">Report SQL template containing at-prefixed placeholders.</param>
    /// <param name="paramsJson">Metadata JSON submitted for those placeholders.</param>
    /// <returns>Normalized parameter rows with name, label, type, required, default, source, and options fields.</returns>
    /// <exception cref="UserException">Thrown when the JSON shape is invalid or references unknown placeholders.</exception>
    public static FwList parseParamDefinitions(string sql, string paramsJson)
    {
        var names = extractParamNames(sql);
        var result = new FwList();
        var byName = new FwDict(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(paramsJson))
        {
            object? decoded;
            try
            {
                decoded = Utils.jsonDecode(paramsJson);
            }
            catch (Exception ex)
            {
                throw new UserException("Parameter JSON is invalid: " + ex.Message);
            }

            if (decoded is FwDict dict)
            {
                foreach (var key in dict.Keys)
                {
                    var meta = dict[key] is FwDict row ? new FwDict(row) : [];
                    meta["name"] = key;
                    addParamDefinition(byName, meta);
                }
            }
            else if (decoded is IList list)
            {
                foreach (var item in list)
                {
                    if (item is not FwDict row)
                        throw new UserException("Parameter JSON array items must be objects");
                    addParamDefinition(byName, new FwDict(row));
                }
            }
            else
            {
                throw new UserException("Parameter JSON must be an object or array");
            }
        }

        foreach (string name in names)
        {
            FwDict meta;
            if (byName[name] is FwDict existing)
            {
                meta = existing;
            }
            else
            {
                meta = new FwDict
                {
                    ["name"] = name,
                    ["label"] = Utils.name2human(name),
                    ["type"] = inferParamType(name),
                    ["required"] = false,
                    ["default"] = ""
                };
            }

            normalizeParamDefinition(meta);
            result.Add(meta);
        }

        foreach (string key in byName.Keys)
            if (!names.Any(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase)))
                throw new UserException("Parameter JSON defines @" + key + " but SQL does not use it");

        return result;
    }

    /// <summary>
    /// Serializes normalized parameter metadata so saved reports have explicit, stable parameter definitions.
    /// </summary>
    /// <param name="sql">Report SQL template.</param>
    /// <param name="paramsJson">Submitted metadata JSON.</param>
    /// <returns>Pretty JSON array of normalized metadata rows, or an empty string when no params exist.</returns>
    public static string normalizeParamsJson(string sql, string paramsJson)
    {
        var defs = parseParamDefinitions(sql, paramsJson);
        return defs.Count == 0 ? string.Empty : Utils.jsonEncode(defs, true);
    }

    /// <summary>
    /// Parses report render options without letting malformed JSON break report listing.
    /// </summary>
    /// <param name="renderOptionsJson">Stored render options JSON.</param>
    /// <returns>Options dictionary; empty when no options are configured.</returns>
    /// <exception cref="UserException">Thrown when non-empty JSON is malformed or not an object.</exception>
    public static FwDict parseRenderOptions(string renderOptionsJson)
    {
        if (string.IsNullOrWhiteSpace(renderOptionsJson))
            return [];

        object? decoded;
        try
        {
            decoded = Utils.jsonDecode(renderOptionsJson);
        }
        catch (Exception ex)
        {
            throw new UserException("Render options JSON is invalid: " + ex.Message);
        }

        if (decoded is not FwDict options)
            throw new UserException("Render options JSON must be an object");

        return options;
    }

    /// <summary>
    /// Suggests the next `repN` code for the new custom report form.
    /// </summary>
    /// <returns>First unused code using the rep prefix and a positive integer suffix.</returns>
    public string suggestNextIcode()
    {
        var codes = db.col(table_name, [], "icode");
        var maxNumber = 0;
        foreach (var code in codes)
        {
            var match = Regex.Match(code, @"^rep(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
                maxNumber = Math.Max(maxNumber, match.Groups[1].Value.toInt());
        }

        return "rep" + (maxNumber + 1);
    }

    /// <summary>
    /// Lists active custom reports the current user can run so the reports index only shows useful links.
    /// </summary>
    /// <returns>Rows from fwreports filtered by status, access level, and report-specific RBAC when enabled.</returns>
    public DBList listAccessible()
    {
        var rows = db.array(table_name, new FwDict
        {
            ["status"] = STATUS_ACTIVE,
            ["access_level"] = db.opLE(fw.userAccessLevel)
        }, "iname");

        if (fw.model<Users>().isSiteAdmin())
            return withIndexDisplayState(rows);

        var result = new DBList();
        foreach (var row in rows)
            if (isAccessible(row.toFwDict(), FW.ACTION_SHOW))
                result.Add(row);

        return withIndexDisplayState(result);
    }

    /// <summary>
    /// Loads one active custom report by route code for runtime execution.
    /// </summary>
    /// <param name="icode">Route-safe report code.</param>
    /// <returns>Report row or an empty row when no active report exists.</returns>
    public DBRow oneActiveByIcode(string icode)
    {
        return db.row(table_name, new FwDict
        {
            ["icode"] = cleanupIcode(icode),
            ["status"] = STATUS_ACTIVE
        });
    }

    /// <summary>
    /// Loads one non-deleted custom report by route code for Site Admin edit/delete screens.
    /// </summary>
    /// <param name="icode">Route-safe report code.</param>
    /// <returns>Report row or an empty row when not found.</returns>
    public DBRow oneManageableByIcode(string icode)
    {
        return db.row(table_name, new FwDict
        {
            ["icode"] = cleanupIcode(icode),
            ["status"] = db.opNOT(STATUS_DELETED)
        });
    }

    /// <summary>
    /// Checks whether a loaded custom report can be used by the current user for the requested action.
    /// </summary>
    /// <param name="report">Loaded fwreports row.</param>
    /// <param name="resourceAction">Framework route action being checked.</param>
    /// <param name="resourceActionMore">Optional route action qualifier.</param>
    /// <returns>True when access_level and RBAC allow the action.</returns>
    public bool isAccessible(FwDict report, string resourceAction, string resourceActionMore = "")
    {
        if (report.Count == 0 || report["status"].toInt() != STATUS_ACTIVE)
            return false;

        if (fw.userAccessLevel < report["access_level"].toInt())
            return false;

        var currentUserLevel = fw.userAccessLevel;
        if (currentUserLevel > Users.ACL_VISITOR && currentUserLevel < Users.ACL_SITEADMIN)
        {
            var resourceCode = reportResourceIcode(report["icode"].toStr());
            if (!fw.model<Users>().isAccessByRolesResourceAction(fw.userId, resourceCode, resourceAction, resourceActionMore))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Throws the standard authorization exception when the current user cannot use a report.
    /// </summary>
    /// <param name="report">Loaded fwreports row.</param>
    /// <param name="resourceAction">Framework route action being checked.</param>
    /// <param name="resourceActionMore">Optional route action qualifier.</param>
    public void checkReportAccess(FwDict report, string resourceAction, string resourceActionMore = "")
    {
        if (!isAccessible(report, resourceAction, resourceActionMore))
            throw new AuthException("Bad access - Not authorized to view the Report");
    }

    /// <summary>
    /// Normalizes and validates submitted report fields before the controller saves them.
    /// </summary>
    /// <param name="item">Mutable submitted item dictionary.</param>
    /// <param name="id">Existing report id or 0 for a new report.</param>
    public void normalizeForSave(FwDict item, int id)
    {
        item["icode"] = cleanupIcode(item["icode"].toStr());
        item["iname"] = item["iname"].toStr().Trim();
        item["icon"] = cleanupIcon(item["icon"].toStr());
        item["sql_template"] = item["sql_template"].toStr().Trim();
        item["params_json"] = normalizeParamsJson(item["sql_template"].toStr(), item["params_json"].toStr());

        var options = parseRenderOptions(item["render_options_json"].toStr());
        item["render_options_json"] = options.Count == 0 ? string.Empty : Utils.jsonEncode(options, true);

        validateSqlTemplate(item["sql_template"].toStr());
    }

    /// <summary>
    /// Creates or updates the optional RBAC resource tied to a custom report.
    /// </summary>
    /// <param name="report">Saved fwreports row.</param>
    public void saveReportResource(FwDict report)
    {
        if (!fw.model<Users>().isRoles())
            return;

        var resourceCode = reportResourceIcode(report["icode"].toStr());
        var resources = fw.model<Resources>();
        var existing = resources.oneByIcode(resourceCode);
        var fields = new FwDict
        {
            ["icode"] = resourceCode,
            ["iname"] = report["iname"].toStr(),
            ["idesc"] = "Custom report",
            ["status"] = STATUS_ACTIVE
        };

        if (existing.Count > 0)
            resources.update(existing["id"].toInt(), fields);
        else
            resources.add(fields);
    }

    /// <summary>
    /// Marks the optional RBAC resource inactive when a report is deleted or its code changes.
    /// </summary>
    /// <param name="icode">Report code whose resource should no longer be active.</param>
    public void retireReportResource(string icode)
    {
        if (!fw.model<Users>().isRoles())
            return;

        var resourceCode = reportResourceIcode(icode);
        var resources = fw.model<Resources>();
        var existing = resources.oneByIcode(resourceCode);
        if (existing.Count > 0)
            resources.update(existing["id"].toInt(), new FwDict { ["status"] = STATUS_DELETED });
    }

    /// <summary>
    /// Builds the report-specific RBAC resource code used by the existing report access checks.
    /// </summary>
    /// <param name="icode">Report code.</param>
    /// <returns>Resource code stored in resources.icode.</returns>
    public static string reportResourceIcode(string icode)
    {
        return cleanupIcode(icode) + "Report";
    }

    /// <summary>
    /// Builds SQL parameters from filter input according to normalized parameter metadata.
    /// </summary>
    /// <param name="defs">Normalized parameter metadata rows.</param>
    /// <param name="f">Current report filter values.</param>
    /// <param name="userDateFormat">Date format used by the logged-in user.</param>
    /// <param name="userTimeFormat">Time format used by the logged-in user.</param>
    /// <returns>Dictionary suitable for DB.arrayp.</returns>
    /// <exception cref="UserException">Thrown when a required or typed value is invalid.</exception>
    public FwDict buildSqlParams(FwList defs, FwDict f, int userDateFormat, int userTimeFormat = DateUtils.TIME_FORMAT_24)
    {
        var result = new FwDict();
        foreach (var def in defs)
        {
            var name = def["name"].toStr();
            var type = def["type"].toStr();
            var value = f[name].toStr();
            if (string.IsNullOrEmpty(value))
                value = resolveDefaultValue(def, userDateFormat);

            if (string.IsNullOrEmpty(value))
            {
                if (def["required"].toBool())
                    throw new UserException("Report parameter is required: " + def["label"].toStr());
                result["@" + name] = DBNull.Value;
                continue;
            }

            result["@" + name] = convertParamValue(name, type, value, userDateFormat, userTimeFormat);
            f[name] = value;
        }
        return result;
    }

    /// <summary>
    /// Loads dynamic lookup options declared in parameter metadata for generic custom-report filters.
    /// </summary>
    /// <param name="def">One normalized parameter definition.</param>
    /// <returns>Select options with id and iname fields.</returns>
    public FwList listParamOptions(FwDict def)
    {
        if (def["options"] is IList options)
            return normalizeOptions(options);

        if (def["options"] is FwDict optionsDict)
            return normalizeOptionDict(optionsDict);

        var source = def["source"].toStr().Trim();
        if (string.IsNullOrEmpty(source))
            return [];

        if (source.Equals("users", StringComparison.OrdinalIgnoreCase))
            return fw.model<Users>().listSelectOptions();
        if (source.Equals("fwentities", StringComparison.OrdinalIgnoreCase))
            return fw.model<FwEntities>().listSelectOptions();
        if (source.Equals("log_types", StringComparison.OrdinalIgnoreCase))
            return fw.model<FwLogTypes>().listSelectOptions();

        if (source.StartsWith("model:", StringComparison.OrdinalIgnoreCase))
        {
            var modelName = source[6..].Trim();
            if (!Regex.IsMatch(modelName, @"^[A-Za-z][A-Za-z0-9_]*$"))
                throw new UserException("Unsupported lookup source: " + source);

            return fw.model(modelName).listSelectOptions();
        }

        if (source.StartsWith("sql:", StringComparison.OrdinalIgnoreCase))
        {
            var sql = source[4..].Trim();
            validateSqlTemplate(sql);
            if (Regex.IsMatch(sql, @"^\s*select\b", RegexOptions.IgnoreCase))
                sql = db.limit(sql, DEFAULT_LOOKUP_LIMIT);

            return normalizeOptions(db.arrayp(sql, null, DEFAULT_LOOKUP_LIMIT));
        }

        throw new UserException("Unsupported lookup source: " + source);
    }

    /// <summary>
    /// Adds list-screen-only flags used by report index templates without changing the persisted report fields.
    /// </summary>
    /// <param name="rows">Accessible custom report rows.</param>
    /// <returns>The same row list with derived display flags attached.</returns>
    private DBList withIndexDisplayState(DBList rows)
    {
        foreach (var row in rows)
        {
            try
            {
                var defs = parseParamDefinitions(row["sql_template"], row["params_json"]);
                row["is_autorun"] = defs.Count == 0 ? "1" : "";
            }
            catch
            {
                row["is_autorun"] = "";
            }
        }

        return rows;
    }

    /// <summary>
    /// Adds one metadata row to the case-insensitive definition map after normalizing its placeholder name.
    /// </summary>
    /// <param name="byName">Mutable parameter definition map keyed by placeholder name.</param>
    /// <param name="meta">Submitted metadata row.</param>
    private static void addParamDefinition(FwDict byName, FwDict meta)
    {
        var name = cleanupParamName(meta["name"].toStr());
        if (string.IsNullOrEmpty(name))
            throw new UserException("Parameter JSON item is missing name");

        meta["name"] = name;
        if (byName[name] != null)
            throw new UserException("Parameter JSON defines @" + name + " more than once");

        byName[name] = meta;
    }

    /// <summary>
    /// Normalizes parameter metadata so the renderer and SQL binder can use one stable shape.
    /// </summary>
    /// <param name="meta">Mutable parameter metadata row.</param>
    private static void normalizeParamDefinition(FwDict meta)
    {
        var name = cleanupParamName(meta["name"].toStr());
        meta["name"] = name;
        if (string.IsNullOrEmpty(meta["label"].toStr()))
            meta["label"] = Utils.name2human(name);

        var type = meta["type"].toStr().Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(type))
            type = inferParamType(name);
        if (!Utils.qw("text int number date datetime lookup").Contains(type))
            throw new UserException("Unsupported parameter type for @" + name);

        meta["type"] = type;
        meta["required"] = meta["required"].toBool();
        meta["default"] = meta["default"].toStr();
        meta["source"] = meta["source"].toStr();
    }

    /// <summary>
    /// Cleans a placeholder name without the leading at sign and strips route/SQL-unsafe characters.
    /// </summary>
    /// <param name="name">Submitted parameter name.</param>
    /// <returns>Safe placeholder name.</returns>
    private static string cleanupParamName(string name)
    {
        return Regex.Replace(name ?? string.Empty, @"^@|[^A-Za-z0-9_]+", "");
    }

    /// <summary>
    /// Infers a conservative filter type from common placeholder naming conventions.
    /// </summary>
    /// <param name="name">Clean placeholder name.</param>
    /// <returns>Default parameter type.</returns>
    private static string inferParamType(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.EndsWith("_date") || lower.Contains("date"))
            return "date";
        if (lower.EndsWith("_id") || lower == "id")
            return "int";
        return "text";
    }

    /// <summary>
    /// Removes SQL literals and comments before validation so blocked keywords inside data values do not fail safe queries.
    /// </summary>
    /// <param name="sql">Original SQL template.</param>
    /// <returns>SQL with string literals and comments replaced or removed.</returns>
    private static string stripSqlLiteralsAndComments(string sql)
    {
        var result = Regex.Replace(sql, @"'([^']|'')*'", "''");
        result = Regex.Replace(result, @"""([^""]|"""")*""", "\"\"");
        result = Regex.Replace(result, @"--.*?$", "", RegexOptions.Multiline);
        result = Regex.Replace(result, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return result;
    }

    /// <summary>
    /// Converts submitted filter text into typed values before parameter binding.
    /// </summary>
    /// <param name="name">Placeholder name used in error messages.</param>
    /// <param name="type">Normalized parameter type.</param>
    /// <param name="value">Submitted filter value.</param>
    /// <param name="userDateFormat">Current user's date format.</param>
    /// <param name="userTimeFormat">Current user's time format.</param>
    /// <returns>Typed value for a DB parameter.</returns>
    private static object convertParamValue(string name, string type, string value, int userDateFormat, int userTimeFormat)
    {
        switch (type)
        {
            case "int":
            case "lookup":
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    throw new UserException("Invalid number for @" + name);
                return intValue;
            case "number":
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue)
                    && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out decimalValue))
                    throw new UserException("Invalid decimal number for @" + name);
                return decimalValue;
            case "date":
            case "datetime":
                var sqlDate = DateUtils.Str2SQL(value, userDateFormat, userTimeFormat, type == "datetime");
                if (DateUtils.SQL2Date(sqlDate) is not DateTime dateValue)
                    throw new UserException("Invalid date for @" + name);
                return dateValue;
            default:
                return value;
        }
    }

    /// <summary>
    /// Resolves configured default values, including relative date defaults, before required checks run.
    /// </summary>
    /// <param name="def">Normalized parameter metadata row.</param>
    /// <param name="userDateFormat">Current user's date format.</param>
    /// <returns>Resolved default value or an empty string when none is configured.</returns>
    private static string resolveDefaultValue(FwDict def, int userDateFormat)
    {
        var defaultValue = def["default"].toStr();
        if (string.IsNullOrEmpty(defaultValue))
            return string.Empty;

        var type = def["type"].toStr();
        if (type != "date" && type != "datetime")
            return defaultValue;

        var lower = defaultValue.ToLowerInvariant();
        if (lower == "today")
            return DateUtils.Date2Str(DateTime.Now, userDateFormat);

        var match = Regex.Match(lower, @"^([+-]?\d+)d$");
        if (match.Success)
            return DateUtils.Date2Str(DateTime.Now.AddDays(match.Groups[1].Value.toInt()), userDateFormat);

        return defaultValue;
    }

    /// <summary>
    /// Converts lookup rows from SQL or JSON arrays into the select-option shape used by ParsePage templates.
    /// </summary>
    /// <param name="options">Lookup rows or option objects.</param>
    /// <returns>Options with id and iname fields.</returns>
    private static FwList normalizeOptions(IList options)
    {
        var result = new FwList();
        foreach (var option in options)
        {
            if (option is IDictionary dict)
            {
                var row = new FwDict(dict);
                result.Add(new FwDict
                {
                    ["id"] = firstValue(row, "id", "value", "icode"),
                    ["iname"] = firstValue(row, "iname", "label", "name")
                });
            }
        }
        return result;
    }

    /// <summary>
    /// Converts simple JSON object lookup definitions into the select-option shape used by ParsePage templates.
    /// </summary>
    /// <param name="options">Dictionary of value to display label.</param>
    /// <returns>Options with id and iname fields.</returns>
    private static FwList normalizeOptionDict(FwDict options)
    {
        var result = new FwList();
        foreach (var key in options.Keys)
            result.Add(new FwDict { ["id"] = key, ["iname"] = options[key].toStr() });
        return result;
    }

    /// <summary>
    /// Finds the first populated value across accepted lookup field aliases.
    /// </summary>
    /// <param name="row">Lookup row.</param>
    /// <param name="keys">Candidate field names in priority order.</param>
    /// <returns>First non-empty value or an empty string.</returns>
    private static string firstValue(FwDict row, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = row[key].toStr();
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        return string.Empty;
    }
}
