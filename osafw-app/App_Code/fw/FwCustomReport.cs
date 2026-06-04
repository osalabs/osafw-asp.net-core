// Stored custom report adapter
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public class FwCustomReport : FwReports
{
    private static readonly Regex NonTotalFieldRegex = new(@"(^id$|_id$|^status$|_status$|^access_level$|^icode$|^code$|^type$|^prio$|^att$|_user$|^is_|^has_|^ui_|_mode$|_theme$|_format$|^staff$|^phone$|^zip$|^color_codes$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly FwDict report;
    private FwReportsModel reportModel = null!;
    private FwList paramDefs = [];

    public FwCustomReport(FwDict report)
    {
        this.report = new FwDict(report);
    }

    /// <summary>
    /// Initializes a stored report while preserving the normal FwReports rendering contract.
    /// </summary>
    /// <param name="fw">Current framework request context.</param>
    /// <param name="report_code">Route-safe report code.</param>
    /// <param name="f">Submitted report filters.</param>
    public override void init(FW fw, string report_code, FwDict f)
    {
        base.init(fw, report_code, f);

        reportModel = fw.model<FwReportsModel>();
        access_level = report["access_level"].toInt();

        var options = FwReportsModel.parseRenderOptions(report["render_options_json"].toStr());
        foreach (var key in options.Keys)
            render_options[key] = options[key];
    }

    /// <summary>
    /// Checks stored-report access through the report row rather than hardcoded class fields.
    /// </summary>
    public override void checkAccess()
    {
        reportModel.checkReportAccess(report, fw.route.action, fw.route.action_more);
    }

    /// <summary>
    /// Uses one shared template set for every database-backed report.
    /// </summary>
    /// <returns>ParsePage base directory for generic custom report templates.</returns>
    protected override string templateBaseDir()
    {
        return TPL_BASE_DIR + "/custom";
    }

    /// <summary>
    /// Builds filter metadata from SQL placeholders and stored parameter JSON.
    /// </summary>
    public override void setFilters()
    {
        base.setFilters();

        paramDefs = FwReportsModel.parseParamDefinitions(report["sql_template"].toStr(), report["params_json"].toStr());
        foreach (var def in paramDefs)
        {
            var name = def["name"].toStr();
            if (string.IsNullOrEmpty(f[name].toStr()))
                f[name] = resolveFilterDefault(def);

            def["value"] = f[name].toStr();
            if (def["type"].toStr() == "lookup")
                def["options"] = reportModel.listParamOptions(def);
        }

        f_data["custom_params"] = paramDefs;
        ps["custom_report"] = report;
        ps["custom_params"] = paramDefs;
        ps["is_custom_report"] = true;
        ps["is_site_admin"] = fw.model<Users>().isSiteAdmin();
        ps["has_custom_params"] = paramDefs.Count > 0;
        ps["title"] = report["iname"].toStr();
    }

    /// <summary>
    /// Executes the stored SQL with bound filter parameters and prepares a generic table view model.
    /// </summary>
    public override void getData()
    {
        base.getData();

        if (paramDefs.Count == 0)
            setFilters();

        var sql = report["sql_template"].toStr();
        var rowLimit = optionInt("row_limit", FwReportsModel.DEFAULT_ROW_LIMIT);
        if (f["is_preview"].toBool())
            rowLimit = optionInt("preview_limit", FwReportsModel.DEFAULT_PREVIEW_LIMIT);

        if (rowLimit > 0 && Regex.IsMatch(sql, @"^\s*select\b", RegexOptions.IgnoreCase))
            sql = db.limit(sql, rowLimit);

        var sqlParams = reportModel.buildSqlParams(paramDefs, f, fw.userDateFormat, fw.userTimeFormat);
        var oldTimeout = db.sql_command_timeout;
        db.sql_command_timeout = optionInt("timeout_seconds", FwReportsModel.DEFAULT_TIMEOUT_SECONDS);
        try
        {
            list_rows = db.arrayp(sql, sqlParams, rowLimit > 0 ? rowLimit : -1);
        }
        finally
        {
            db.sql_command_timeout = oldTimeout;
        }

        list_count = list_rows.Count;
        buildResultTable();
    }

    /// <summary>
    /// Reads a positive integer render option while falling back to framework defaults for absent or invalid values.
    /// </summary>
    /// <param name="key">Render option key.</param>
    /// <param name="defaultValue">Fallback value.</param>
    /// <returns>Configured positive value or the fallback.</returns>
    private int optionInt(string key, int defaultValue)
    {
        var value = render_options[key].toInt();
        return value > 0 ? value : defaultValue;
    }

    /// <summary>
    /// Resolves display defaults for generated filters before the report is run.
    /// </summary>
    /// <param name="def">Normalized parameter metadata row.</param>
    /// <returns>Default filter value formatted for the current user.</returns>
    private string resolveFilterDefault(FwDict def)
    {
        var defaultValue = def["default"].toStr();
        if (string.IsNullOrEmpty(defaultValue))
            return string.Empty;

        var type = def["type"].toStr();
        if (type != "date" && type != "datetime")
            return defaultValue;

        var lower = defaultValue.ToLowerInvariant();
        if (lower == "today")
            return DateUtils.Date2Str(DateTime.Now, fw.userDateFormat);

        var match = Regex.Match(lower, @"^([+-]?\d+)d$");
        if (match.Success)
            return DateUtils.Date2Str(DateTime.Now.AddDays(match.Groups[1].Value.toInt()), fw.userDateFormat);

        return defaultValue;
    }

    /// <summary>
    /// Converts raw DB rows into header and cell collections for the generic custom-report table template.
    /// </summary>
    private void buildResultTable()
    {
        var headers = new FwList();
        var rows = new FwList();
        var totals = new FwList();
        var fields = list_rows.Count > 0 ? list_rows[0].Keys.ToArray() : Array.Empty<string>();
        var numericFields = fields.ToDictionary(field => field, field => isNumericColumn(field));
        var totalableFields = fields.ToDictionary(field => field, field => numericFields[field] && !NonTotalFieldRegex.IsMatch(field));
        var totalValues = fields.ToDictionary(field => field, _ => 0m);
        var hasTotals = false;

        foreach (var field in fields)
        {
            var isNumeric = numericFields[field];
            headers.Add(new FwDict
            {
                ["field"] = field,
                ["iname"] = Utils.name2human(field),
                ["is_numeric"] = isNumeric,
                ["align_class"] = isNumeric ? "text-end" : string.Empty
            });

            foreach (var row in list_rows)
                if (totalableFields[field] && tryDecimal(row[field], out var value))
                    totalValues[field] += value;

            if (totalableFields[field])
                hasTotals = true;
        }

        foreach (var row in list_rows)
        {
            var cells = new FwList();
            foreach (var field in fields)
            {
                var isNumeric = numericFields[field];
                cells.Add(new FwDict
                {
                    ["field"] = field,
                    ["value"] = row[field],
                    ["is_numeric"] = isNumeric,
                    ["align_class"] = isNumeric ? "text-end" : string.Empty
                });
            }

            rows.Add(new FwDict { ["cells"] = cells });
        }

        var isFirstCell = true;
        foreach (var field in fields)
        {
            var hasTotal = totalableFields[field];
            totals.Add(new FwDict
            {
                ["field"] = field,
                ["has_total"] = hasTotal,
                ["display_value"] = isFirstCell ? "Total" : hasTotal ? totalValues[field].ToString("0.##", CultureInfo.InvariantCulture) : string.Empty,
                ["align_class"] = isFirstCell ? string.Empty : numericFields[field] ? "text-end" : string.Empty
            });
            isFirstCell = false;
        }

        ps["result_headers"] = headers;
        ps["result_rows"] = rows;
        ps["result_totals"] = totals;
        ps["has_result_rows"] = rows.Count > 0;
        ps["has_result_totals"] = rows.Count > 0 && hasTotals;
    }

    /// <summary>
    /// Determines whether every populated value in a field is numeric so the generic table can align it consistently.
    /// </summary>
    /// <param name="field">Result field name.</param>
    /// <returns>True when the column has at least one numeric value and no populated non-numeric values.</returns>
    private bool isNumericColumn(string field)
    {
        var hasNumeric = false;
        foreach (var row in list_rows)
        {
            var value = row[field];
            if (value == null || value == DBNull.Value)
                continue;

            if (!tryDecimal(value, out _))
                return false;

            hasNumeric = true;
        }

        return hasNumeric;
    }

    /// <summary>
    /// Converts DB numeric values to decimal for generic totals while ignoring dates, booleans, and strings.
    /// </summary>
    /// <param name="value">Raw DB result value.</param>
    /// <param name="result">Converted decimal value.</param>
    /// <returns>True when the value is a numeric CLR type.</returns>
    private static bool tryDecimal(object? value, out decimal result)
    {
        result = 0m;
        if (value == null || value == DBNull.Value)
            return false;

        switch (value)
        {
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            case string s when Regex.IsMatch(s.Trim(), @"^-?\d+(?:\.\d+)?$"):
                return decimal.TryParse(s.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out result);
            default:
                return false;
        }
    }
}
