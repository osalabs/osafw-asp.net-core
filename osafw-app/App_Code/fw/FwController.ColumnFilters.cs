// Fw Controller column filter helpers
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2026 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public abstract partial class FwController
{
    private const int LIST_COLUMN_FILTER_MULTI_LIMIT = 200;

    /// <summary>
    /// Loads the optional list column filter config without enabling behavior for base/simple controllers.
    /// </summary>
    /// <param name="controllerConfig">Controller config loaded from `config.json` or an equivalent source.</param>
    protected virtual void configureListColumnFilters(FwDict controllerConfig)
    {
        list_column_filters = controllerConfig["list_column_filters"] as FwDict ?? [];
        list_column_filter_defs = [];
        is_list_column_filters = false;
    }

    /// <summary>
    /// Returns the active per-column filter configuration for this controller.
    /// </summary>
    /// <returns>Config dictionary containing `enabled` and optional `fields` entries.</returns>
    protected virtual FwDict getListColumnFilterConfig()
    {
        return list_column_filters;
    }

    /// <summary>
    /// Returns dynamic form field definitions keyed by field name so list filters can reuse lookup/type metadata.
    /// </summary>
    /// <returns>Field-name keyed definitions, or an empty dictionary for non-dynamic controllers.</returns>
    protected virtual FwDict getListColumnFilterFormFieldDefs()
    {
        return [];
    }

    /// <summary>
    /// Allows derived controllers to fully handle a custom typed column filter.
    /// </summary>
    /// <param name="def">Whitelisted filter definition for the requested field.</param>
    /// <param name="rawValue">Parsed JSON filter payload from `search[field]`.</param>
    /// <returns><c>true</c> when the derived controller appended its own predicate and no default handling is needed.</returns>
    protected virtual bool applyListColumnFilter(FwDict def, object rawValue)
    {
        return false;
    }

    /// <summary>
    /// Builds filter definitions for the visible/configured list fields and caches them for the request.
    /// </summary>
    /// <returns>Field-name keyed filter definitions.</returns>
    protected virtual FwDict getListColumnFilterDefs()
    {
        if (!is_list_column_filters)
            return [];

        if (list_column_filter_defs.Count > 0)
            return list_column_filter_defs;

        var result = new FwDict();
        var fields = Utils.qw(getViewListUserFields());
        var overrides = getListColumnFilterFieldOverrides();

        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field) || !view_list_map.ContainsKey(field))
                continue;

            var header = new FwDict
            {
                ["field_name"] = field,
                ["field_name_visible"] = view_list_map[field],
            };
            result[field] = inferListColumnFilterDef(header);
        }

        foreach (var entry in overrides)
        {
            var field = entry.Key;
            if (string.IsNullOrWhiteSpace(field) || result.ContainsKey(field))
                continue;

            var header = new FwDict
            {
                ["field_name"] = field,
                ["field_name_visible"] = view_list_map[field] ?? field,
            };
            result[field] = inferListColumnFilterDef(header);
        }

        list_column_filter_defs = result;
        return result;
    }

    /// <summary>
    /// Infers one filter definition from a visible list header, dynamic field config, explicit overrides, and schema metadata.
    /// </summary>
    /// <param name="header">List header dictionary containing at least `field_name`.</param>
    /// <returns>A normalized filter definition for the field.</returns>
    protected virtual FwDict inferListColumnFilterDef(FwDict header)
    {
        var field = header["field_name"].toStr();
        var def = new FwDict
        {
            ["field_name"] = field,
            ["field_name_visible"] = header["field_name_visible"],
            ["filter_field"] = field,
            ["filter_type"] = "text",
            ["is_filterable"] = true,
        };

        var formDefs = getListColumnFilterFormFieldDefs();
        FwDict formDef = formDefs[field] as FwDict ?? [];
        if (formDef.Count > 0)
        {
            foreach (var entry in formDef)
                if (!def.ContainsKey(entry.Key))
                    def[entry.Key] = entry.Value;
            def["source_field_type"] = formDef["type"];
        }

        var overrides = getListColumnFilterFieldOverrides();
        FwDict overrideDef = overrides[field] as FwDict ?? [];
        var explicitType = overrideDef["type"].toStr();
        foreach (var entry in overrideDef)
            def[entry.Key] = entry.Value;

        if (def["filter_field"].toStr().Length == 0)
            def["filter_field"] = field;

        var schema = getListColumnFilterSchema(field, def["filter_field"].toStr());
        if (schema.Count > 0)
        {
            if (def["field_storage_type"].toStr().Length == 0)
                def["field_storage_type"] = schema["fw_type"];
            if (def["field_db_type"].toStr().Length == 0)
                def["field_db_type"] = schema["type"];
        }

        def["filter_type"] = normalizeListColumnFilterType(
            explicitType.Length > 0 ? explicitType : inferListColumnFilterType(field, formDef, def));
        if (def["filter_type"].toStr() == "none")
            def["is_filterable"] = false;

        if (def["template"].toStr().Length > 0)
            def["filter_template"] = def["template"];
        if (def["component"].toStr().Length > 0)
            def["filter_component"] = def["component"];

        return def;
    }

    /// <summary>
    /// Adds normalized column filter metadata to `list_headers` for templates and Vue payloads.
    /// </summary>
    protected virtual void enrichListColumnFilterHeaders()
    {
        if (!is_list_column_filters || list_headers.Count == 0)
            return;

        var defs = getListColumnFilterDefs();
        foreach (FwDict header in list_headers)
        {
            var field = header["field_name"].toStr();
            var def = defs[field] as FwDict;
            if (def == null)
            {
                header["filter_type"] = "none";
                header["is_filterable"] = false;
                continue;
            }

            var headerDef = Utils.cloneHashDeep(def) ?? new FwDict(def);
            prepareListColumnFilterHeader(header, headerDef);
        }
    }

    /// <summary>
    /// Dispatches one `search[field]` value through the typed filter pipeline when it is JSON.
    /// </summary>
    /// <param name="fieldname">Request field name inside `search[...]`.</param>
    /// <param name="value">Raw submitted value.</param>
    /// <returns><c>true</c> when the typed pipeline consumed the value; <c>false</c> to fall back to legacy text syntax.</returns>
    protected virtual bool applyListColumnFilterSearch(string fieldname, string value)
    {
        var defs = getListColumnFilterDefs();
        if (defs[fieldname] is not FwDict def)
            return false;

        if (!def["is_filterable"].toBool() || def["filter_type"].toStr() == "none")
            return true;

        if (!isListColumnFilterJson(value))
            return false;

        var raw = parseListColumnFilterJson(value);
        if (raw == null)
            return false;

        if (applyListColumnFilter(def, raw))
            return true;

        _ = applyTypedListColumnFilter(def, raw);
        return true;
    }

    private FwDict getListColumnFilterFieldOverrides()
    {
        var config = getListColumnFilterConfig();
        return config["fields"] as FwDict ?? [];
    }

    private string inferListColumnFilterType(string field, FwDict formDef, FwDict def)
    {
        var formType = formDef["type"].toStr();
        var storageType = def["field_storage_type"].toStr();
        var dbType = def["field_db_type"].toStr();

        if (formType is "cb" or "switch" or "yesno")
            return "boolean";

        if (formType is "date" or "date_popup" or "date_combo")
        {
            def["is_date_only"] = true;
            return "date_range";
        }

        if (formType is "datetime_popup" or "datetime_local")
            return "date_range";

        if (formType is "number" or "range" or "currency")
            return "number_conditions";

        if (formType is "select" or "radio" or "multicb" or "multi" or "multicb_prio" || def.ContainsKey("options") || def.ContainsKey("lookup_tpl"))
            return "multi_select";

        if (def.ContainsKey("lookup_model") && field.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && formType != "autocomplete")
            return "multi_select";

        if (storageType is "date")
        {
            def["is_date_only"] = true;
            return "date_range";
        }

        if (storageType is "datetime" or "datetimeoffset")
            return "date_range";

        if (dbType.Equals("bit", StringComparison.OrdinalIgnoreCase))
            return "boolean";

        if (storageType is "int" or "float" or "decimal")
            return "number_conditions";

        return "text";
    }

    private static string normalizeListColumnFilterType(string type)
    {
        type = type.Trim().ToLowerInvariant();
        return type switch
        {
            "date" or "datetime" or "date-range" => "date_range",
            "select" or "lookup" or "status" or "multi" => "multi_select",
            "number" or "numeric" => "number_conditions",
            "bool" or "yesno" => "boolean",
            "autocomplete" => "autocomplete",
            "none" => "none",
            "text" or "" => "text",
            _ => type,
        };
    }

    private FwDict getListColumnFilterSchema(string field, string filterField)
    {
        var table = list_view;
        if (string.IsNullOrEmpty(table))
            table = model0?.table_name ?? "";

        if (string.IsNullOrEmpty(table) || table.StartsWith('(') || Regex.IsMatch(table, @"\s"))
            return [];

        var schemaField = filterField;
        if (schemaField.Contains('.'))
            schemaField = schemaField.Split('.').Last();
        if (schemaField.Contains('(') || schemaField.Contains(' '))
            schemaField = field;

        try
        {
            var schema = db.tableSchemaFull(table);
            return schema[schemaField.ToLowerInvariant()] as FwDict ?? [];
        }
        catch (Exception ex)
        {
            logger(LogLevel.TRACE, "Column filter schema inference skipped for ", table, ".", schemaField, ": ", ex.Message);
            return [];
        }
    }

    private void prepareListColumnFilterHeader(FwDict header, FwDict def)
    {
        var searchValue = header["search_value"].toStr();
        var parsed = parseListColumnFilterJson(searchValue);
        var filterType = def["filter_type"].toStr();

        header["filter_type"] = filterType;
        header["filter_field"] = def["filter_field"];
        header["is_filterable"] = def["is_filterable"];
        header["filter_component"] = def["filter_component"];
        header["filter_template"] = def["filter_template"];

        applyListColumnFilterDisplayValues(def, parsed, searchValue);

        if (filterType == "multi_select")
            def["filter_options"] = getListColumnFilterOptions(def, def["filter_values_csv"].toStr());

        applyListColumnFilterDisplayText(def);

        if (def["filter_template"].toStr().Length > 0)
            def["filter_template_html"] = renderListColumnFilterTemplate(def);

        foreach (var entry in def)
            if (!header.ContainsKey(entry.Key))
                header[entry.Key] = entry.Value;

        header["filter_def"] = def;
    }

    private void applyListColumnFilterDisplayValues(FwDict def, FwDict? parsed, string rawValue)
    {
        if (parsed == null)
        {
            applyLegacyListColumnFilterDisplayValues(def, rawValue);
            return;
        }

        var requestType = parsed["type"].toStr(def["filter_type"].toStr());
        if (requestType is "blank" or "not_blank")
        {
            def["filter_blank_op"] = requestType;
            return;
        }

        switch (def["filter_type"].toStr())
        {
            case "text":
                def["filter_op"] = parsed["op"].toStr("contains");
                def["filter_value"] = parsed["value"];
                break;
            case "date_range":
                def["filter_from"] = parsed["from"];
                def["filter_to"] = parsed["to"];
                break;
            case "multi_select":
            case "autocomplete":
                def["filter_values_csv"] = string.Join(",", listColumnFilterValues(parsed["values"]));
                break;
            case "number_conditions":
                def["filter_equal"] = parsed["equal"].toStr(parsed["value"].toStr());
                def["filter_gte"] = parsed["gte"];
                def["filter_lte"] = parsed["lte"];
                def["filter_from"] = parsed["from"];
                def["filter_to"] = parsed["to"];
                def["filter_not_between_from"] = parsed["not_between_from"];
                def["filter_not_between_to"] = parsed["not_between_to"];
                break;
            case "boolean":
                def["filter_value"] = parsed["value"];
                break;
        }
    }

    private void applyLegacyListColumnFilterDisplayValues(FwDict def, string rawValue)
    {
        if (rawValue.Length == 0)
            return;

        if (def["filter_type"].toStr() != "text")
            return;

        var op = "contains";
        var value = rawValue;
        if (rawValue.StartsWith("!="))
        {
            op = "not_equals";
            value = rawValue[2..];
        }
        else if (rawValue.StartsWith('='))
        {
            op = "equals";
            value = rawValue[1..];
        }
        else if (rawValue.StartsWith('!'))
        {
            op = "not_contains";
            value = rawValue[1..];
        }
        else if (rawValue.StartsWith('^'))
        {
            op = "starts_with";
            value = rawValue[1..];
        }
        else if (rawValue.StartsWith('$'))
        {
            op = "ends_with";
            value = rawValue[1..];
        }

        def["filter_op"] = op;
        def["filter_value"] = value;
    }

    private void applyListColumnFilterDisplayText(FwDict def)
    {
        var display = listColumnFilterDisplayText(def);
        def["filter_display"] = display.Length > 0 ? display : "Any";
        def["filter_active_class"] = display.Length > 0 ? " is-active" : "";
    }

    private string listColumnFilterDisplayText(FwDict def)
    {
        var blankOp = def["filter_blank_op"].toStr();
        if (blankOp == "blank")
            return "blank";
        if (blankOp == "not_blank")
            return "not blank";

        switch (def["filter_type"].toStr())
        {
            case "date_range":
                var from = def["filter_from"].toStr();
                var to = def["filter_to"].toStr();
                if (from.Length > 0 && to.Length > 0)
                    return from == to ? from : $"{from} - {to}";
                if (from.Length > 0)
                    return $">= {from}";
                if (to.Length > 0)
                    return $"<= {to}";
                break;
            case "multi_select":
                var values = listColumnFilterValues(def["filter_values_csv"]);
                if (values.Count == 1)
                    return listColumnFilterOptionLabel(def, values[0]);
                if (values.Count > 1)
                    return $"{values.Count} selected";
                break;
            case "autocomplete":
                var acValues = listColumnFilterValues(def["filter_values_csv"]);
                if (acValues.Count == 1)
                    return autocompleteListColumnFilterLabel(acValues[0]);
                if (acValues.Count > 1)
                    return $"{acValues.Count} selected";
                break;
            case "number_conditions":
                return listColumnFilterNumberDisplayText(def);
            case "boolean":
                return def["filter_value"].toStr() switch
                {
                    "1" or "true" or "yes" or "y" => "Yes",
                    "0" or "false" or "no" or "n" => "No",
                    _ => "",
                };
        }

        return "";
    }

    private string listColumnFilterNumberDisplayText(FwDict def)
    {
        var equal = def["filter_equal"].toStr();
        if (equal.Length > 0)
            return $"= {equal}";

        StrList parts = [];
        var gte = def["filter_gte"].toStr();
        var lte = def["filter_lte"].toStr();
        if (gte.Length > 0)
            parts.Add($">= {gte}");
        if (lte.Length > 0)
            parts.Add($"<= {lte}");

        var from = def["filter_from"].toStr();
        var to = def["filter_to"].toStr();
        if (from.Length > 0 || to.Length > 0)
            parts.Add(from.Length > 0 && to.Length > 0 ? $"{from} - {to}" : $"{from}{to}");

        var notFrom = def["filter_not_between_from"].toStr();
        var notTo = def["filter_not_between_to"].toStr();
        if (notFrom.Length > 0 || notTo.Length > 0)
            parts.Add($"not {notFrom} - {notTo}");

        return string.Join(", ", parts);
    }

    private string listColumnFilterOptionLabel(FwDict def, string value)
    {
        if (def["filter_options"] is not IList optionRows)
            return value;

        foreach (FwDict option in optionRows)
            if (option["id"].toStr() == value)
                return option["iname"].toStr(value);

        return value;
    }

    private FwList getListColumnFilterOptions(FwDict def, string selectedValues)
    {
        if (def["options"] is FwDict options)
            return new FwList(options.Select(entry => new FwDict { ["id"] = entry.Key, ["iname"] = entry.Value }));

        if (def["options"] is IList optionRows)
            return new FwList(optionRows);

        var lookupTpl = def["lookup_tpl"].toStr();
        if (lookupTpl.Length > 0)
            return FormUtils.selectTplOptions(lookupTpl, fw.route.controller_path.ToLower());

        var lookupModel = def["lookup_model"].toStr();
        if (lookupModel.Length > 0)
        {
            var selected = selectedValues.Length > 0 ? selectedValues : null;
            return fw.model(lookupModel).listSelectOptions(def, selected);
        }

        return [];
    }

    private string renderListColumnFilterTemplate(FwDict def)
    {
        var template = def["filter_template"].toStr();
        if (template.Length == 0)
            return "";

        var tpl = template.TrimStart('/');
        var lastSlash = tpl.LastIndexOf('/');
        var basedir = lastSlash >= 0 ? "/" + tpl[..lastSlash] : fw.route.controller_path.ToLower();
        var layout = lastSlash >= 0 ? tpl[(lastSlash + 1)..] : tpl;

        try
        {
            return fw.parsePage(basedir, layout, def);
        }
        catch (Exception ex)
        {
            logger(LogLevel.WARN, "Column filter template failed: ", template, " ", ex.Message);
            return "";
        }
    }

    private static bool isListColumnFilterJson(string value)
    {
        return value.TrimStart().StartsWith('{');
    }

    private static FwDict? parseListColumnFilterJson(string value)
    {
        if (!isListColumnFilterJson(value))
            return null;

        try
        {
            return Utils.jsonDecode(value) as FwDict;
        }
        catch
        {
            return null;
        }
    }

    private bool applyTypedListColumnFilter(FwDict def, FwDict raw)
    {
        var requestType = raw["type"].toStr(def["filter_type"].toStr());
        var op = raw["op"].toStr();
        if (requestType is "blank" or "not_blank")
            return appendListColumnBlankPredicate(def, requestType == "not_blank");
        if (op is "blank" or "not_blank")
            return appendListColumnBlankPredicate(def, op == "not_blank");

        return def["filter_type"].toStr() switch
        {
            "text" => applyTextListColumnFilter(def, raw),
            "date_range" => applyDateRangeListColumnFilter(def, raw),
            "multi_select" => applyMultiListColumnFilter(def, raw),
            "autocomplete" => applyAutocompleteListColumnFilter(def, raw),
            "number_conditions" => applyNumberListColumnFilter(def, raw),
            "boolean" => applyBooleanListColumnFilter(def, raw),
            _ => false,
        };
    }

    private bool applyTextListColumnFilter(FwDict def, FwDict raw)
    {
        var value = raw["value"].toStr();
        if (value.Length == 0)
            return false;

        var sqlField = listColumnFilterSqlField(def);
        var expr = db.sqlTextExpr(sqlField);
        var op = raw["op"].toStr("contains");
        var param = nextListColumnFilterParamName(def, "text");

        var sqlOp = op switch
        {
            "equals" or "equal" => "=",
            "not_equals" or "not_equal" => "<>",
            "not_contains" => "NOT LIKE",
            "starts_with" => "LIKE",
            "ends_with" => "LIKE",
            _ => "LIKE",
        };

        var paramValue = op switch
        {
            "equals" or "equal" or "not_equals" or "not_equal" => value,
            "starts_with" => value + "%",
            "ends_with" => "%" + value,
            _ => "%" + value + "%",
        };

        list_where += $" AND {expr} {sqlOp} @{param}";
        list_where_params[param] = paramValue;
        return true;
    }

    private bool applyDateRangeListColumnFilter(FwDict def, FwDict raw)
    {
        var quick = raw["quick"].toStr();
        DateTime? from = null;
        DateTime? to = null;

        if (quick.Length > 0)
        {
            var today = DateUtils.convertTimezone(DateTime.UtcNow, DateUtils.TZ_UTC, fw.userTimezone).Date;
            if (quick == "today")
            {
                from = today;
                to = today;
            }
            else if (quick == "week")
            {
                from = today.AddDays(-6);
                to = today;
            }
            else if (quick == "30")
            {
                from = today.AddDays(-29);
                to = today;
            }
        }

        from ??= parseListColumnFilterDate(raw["from"].toStr());
        to ??= parseListColumnFilterDate(raw["to"].toStr());
        if (from == null && to == null)
            return false;

        var sqlField = listColumnFilterSqlField(def);
        var isDateOnly = def["is_date_only"].toBool() || def["field_storage_type"].toStr() == "date";

        if (from != null)
        {
            var param = nextListColumnFilterParamName(def, "from");
            list_where += $" AND {sqlField} >= @{param}";
            list_where_params[param] = isDateOnly
                ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Unspecified)
                : listColumnFilterDateBoundary(def, from.Value.Date);
        }

        if (to != null)
        {
            var param = nextListColumnFilterParamName(def, "to");
            if (isDateOnly)
            {
                list_where += $" AND {sqlField} <= @{param}";
                list_where_params[param] = DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Unspecified);
            }
            else
            {
                list_where += $" AND {sqlField} < @{param}";
                list_where_params[param] = listColumnFilterDateBoundary(def, to.Value.Date.AddDays(1));
            }
        }

        return true;
    }

    private bool applyMultiListColumnFilter(FwDict def, FwDict raw)
    {
        return appendListColumnInPredicate(def, listColumnFilterValues(raw["values"]));
    }

    private bool applyAutocompleteListColumnFilter(FwDict def, FwDict raw)
    {
        if (def["lookup_by_value"].toBool() && raw["value"].toStr().Length > 0)
            return applyTextListColumnFilter(def, raw);

        return appendListColumnInPredicate(def, listColumnFilterValues(raw["values"]));
    }

    private bool applyNumberListColumnFilter(FwDict def, FwDict raw)
    {
        var sqlField = listColumnFilterSqlField(def);
        var applied = false;

        if (tryGetListColumnFilterNumber(raw["equal"], out var equal) || tryGetListColumnFilterNumber(raw["value"], out equal))
        {
            var param = nextListColumnFilterParamName(def, "eq");
            list_where += $" AND {sqlField} = @{param}";
            list_where_params[param] = equal;
            applied = true;
        }

        if (tryGetListColumnFilterNumber(raw["gte"], out var gte))
        {
            var param = nextListColumnFilterParamName(def, "gte");
            list_where += $" AND {sqlField} >= @{param}";
            list_where_params[param] = gte;
            applied = true;
        }

        if (tryGetListColumnFilterNumber(raw["lte"], out var lte))
        {
            var param = nextListColumnFilterParamName(def, "lte");
            list_where += $" AND {sqlField} <= @{param}";
            list_where_params[param] = lte;
            applied = true;
        }

        if (tryGetListColumnFilterNumber(raw["from"], out var from) && tryGetListColumnFilterNumber(raw["to"], out var to))
        {
            var pFrom = nextListColumnFilterParamName(def, "from");
            list_where_params[pFrom] = from;
            var pTo = nextListColumnFilterParamName(def, "to");
            list_where += $" AND ({sqlField} >= @{pFrom} AND {sqlField} <= @{pTo})";
            list_where_params[pTo] = to;
            applied = true;
        }

        if (tryGetListColumnFilterNumber(raw["not_between_from"], out var nbFrom) && tryGetListColumnFilterNumber(raw["not_between_to"], out var nbTo))
        {
            var pFrom = nextListColumnFilterParamName(def, "notfrom");
            list_where_params[pFrom] = nbFrom;
            var pTo = nextListColumnFilterParamName(def, "notto");
            list_where += $" AND ({sqlField} < @{pFrom} OR {sqlField} > @{pTo})";
            list_where_params[pTo] = nbTo;
            applied = true;
        }

        return applied;
    }

    private bool applyBooleanListColumnFilter(FwDict def, FwDict raw)
    {
        var value = raw["value"].toStr().Trim().ToLowerInvariant();
        if (value.Length == 0 || value == "all")
            return false;

        if (value == "blank")
            return appendListColumnBlankPredicate(def, false);
        if (value == "not_blank")
            return appendListColumnBlankPredicate(def, true);

        var boolValue = value is "1" or "true" or "yes" or "y";
        var param = nextListColumnFilterParamName(def, "bool");
        list_where += $" AND {listColumnFilterSqlField(def)} = @{param}";
        list_where_params[param] = boolValue ? 1 : 0;
        return true;
    }

    private bool appendListColumnBlankPredicate(FwDict def, bool isNotBlank)
    {
        var sqlField = listColumnFilterSqlField(def);
        var textExpr = db.sqlTextExpr(sqlField);
        list_where += isNotBlank
            ? $" AND ({sqlField} IS NOT NULL AND {textExpr} <> '')"
            : $" AND ({sqlField} IS NULL OR {textExpr} = '')";
        return true;
    }

    private bool appendListColumnInPredicate(FwDict def, StrList values)
    {
        if (values.Count == 0)
            return false;

        var sqlParams = new StrList();
        foreach (var value in values.Take(LIST_COLUMN_FILTER_MULTI_LIMIT))
        {
            var param = nextListColumnFilterParamName(def, "in");
            sqlParams.Add("@" + param);
            list_where_params[param] = normalizeListColumnFilterParamValue(def, value);
        }

        if (sqlParams.Count == 0)
            return false;

        list_where += $" AND {listColumnFilterSqlField(def)} IN ({string.Join(",", sqlParams)})";
        return true;
    }

    private string listColumnFilterSqlField(FwDict def)
    {
        return db.qid(def["filter_field"].toStr(def["field_name"].toStr()));
    }

    private string nextListColumnFilterParamName(FwDict def, string suffix)
    {
        var field = def["field_name"].toStr(def["filter_field"].toStr());
        var safeField = Regex.Replace(field, @"\W+", "_").Trim('_');
        if (safeField.Length == 0)
            safeField = "field";
        return $"cf_{safeField}_{suffix}_{list_where_params.Count}";
    }

    private object normalizeListColumnFilterParamValue(FwDict def, string value)
    {
        if (def["filter_type"].toStr() == "autocomplete")
            value = autocompleteListColumnFilterValue(value);

        return def["field_storage_type"].toStr() switch
        {
            "int" => value.toLong(),
            "float" => value.toFloat(),
            "decimal" => value.toDecimal(),
            _ => value,
        };
    }

    private StrList listColumnFilterValues(object? raw)
    {
        var values = new StrList();
        if (raw is IList list && raw is not string)
        {
            foreach (var item in list)
                addListColumnFilterValue(values, item);
        }
        else
        {
            var str = raw.toStr();
            if (str.Contains(','))
            {
                foreach (var item in str.Split(','))
                    addListColumnFilterValue(values, item);
            }
            else
            {
                addListColumnFilterValue(values, str);
            }
        }

        return new StrList(values.Take(LIST_COLUMN_FILTER_MULTI_LIMIT));
    }

    private static void addListColumnFilterValue(StrList values, object? value)
    {
        var str = value.toStr().Trim();
        if (str.Length > 0)
            values.Add(str);
    }

    private static string autocompleteListColumnFilterValue(string value)
    {
        const string separator = FormUtils.AUTOCOMPLETE_SEPARATOR;
        var separatorIndex = value.IndexOf(separator, StringComparison.Ordinal);
        return separatorIndex < 0 ? value : value[(separatorIndex + separator.Length)..].Trim();
    }

    private static string autocompleteListColumnFilterLabel(string value)
    {
        const string separator = FormUtils.AUTOCOMPLETE_SEPARATOR;
        var separatorIndex = value.IndexOf(separator, StringComparison.Ordinal);
        return separatorIndex < 0 ? value : value[..separatorIndex].Trim();
    }

    private DateTime? parseListColumnFilterDate(string value)
    {
        if (value.Length == 0)
            return null;

        var sql = DateUtils.Str2SQL(value, fw.userDateFormat);
        if (sql.Length == 0)
            return null;

        return DateUtils.SQL2Date(sql);
    }

    private object listColumnFilterDateBoundary(FwDict def, DateTime userLocalDate)
    {
        var local = DateTime.SpecifyKind(userLocalDate, DateTimeKind.Unspecified);
        var utc = DateUtils.convertTimezone(local, fw.userTimezone, DateUtils.TZ_UTC);
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        var storageType = def["field_storage_type"].toStr();
        var filterField = def["filter_field"].toStr(def["field_name"].toStr());
        if (storageType == "datetimeoffset")
            return new DateTimeOffset(utc);
        if (filterField.EndsWith("_utc", StringComparison.OrdinalIgnoreCase))
            return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);

        var dbLocal = DateUtils.convertTimezone(utc, DateUtils.TZ_UTC, db.getTimezoneId());
        return DateTime.SpecifyKind(dbLocal, DateTimeKind.Unspecified);
    }

    private static bool tryGetListColumnFilterNumber(object? raw, out decimal value)
    {
        value = 0;
        var str = raw.toStr().Trim();
        return str.Length > 0
            && decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
