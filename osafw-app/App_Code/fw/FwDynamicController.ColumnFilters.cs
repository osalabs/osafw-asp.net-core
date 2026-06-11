// Dynamic controller list column filters
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2026 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public partial class FwDynamicController
{
    private const int LIST_COLUMN_FILTER_MULTI_VALUE_LIMIT = 200;

    protected bool is_list_column_filters = false;
    protected FwDict list_column_filters = [];
    protected FwDict list_column_filter_defs = [];
    private readonly FwDict list_column_filter_options_cache = [];

    /// <summary>
    /// Loads Dynamic-only typed list column filter configuration after normal controller config is applied.
    /// </summary>
    public override void loadControllerConfig(FwDict config)
    {
        base.loadControllerConfig(config);

        list_column_filters = config["list_column_filters"] as FwDict ?? [];
        list_column_filter_defs = [];
        list_column_filter_options_cache.Clear();
        is_list_column_filters = list_column_filters["enabled"].toBool();
    }

    /// <summary>
    /// Adds typed Dynamic list column filters before falling back to the legacy text-search syntax.
    /// </summary>
    public override void setListSearchAdvanced()
    {
        foreach (string fieldname in list_filter_search.Keys)
        {
            string value = list_filter_search[fieldname].toStr();
            if (string.IsNullOrEmpty(value) || (is_dynamic_index && !view_list_map.ContainsKey(fieldname)))
                continue;

            if (is_list_column_filters && applyListColumnFilterSearch(fieldname, value))
                continue;

            appendListSearchAdvancedField(fieldname, value);
        }
    }

    /// <summary>
    /// Builds standard list headers, then adds typed filter metadata for opted-in Dynamic/Vue lists.
    /// </summary>
    public override void setViewList(bool is_cols = true, bool is_column_filters = true)
    {
        base.setViewList(is_cols, is_column_filters);

        if (is_column_filters)
            enrichListColumnFilterHeaders();
    }

    /// <summary>
    /// Allows Dynamic subclasses to handle custom typed filter predicates.
    /// </summary>
    protected virtual bool applyListColumnFilter(FwDict def, FwDict rawValue)
    {
        return false;
    }

    private FwDict listColumnFilterFormFieldDefs()
    {
        var result = new FwDict();
        foreach (var prefix in new[] { "showform_fields", "show_fields" })
        {
            foreach (FwDict def in collectFormFields(prefix))
            {
                var field = def["field"].toStr();
                if (field.Length > 0 && !result.ContainsKey(field))
                    result[field] = def;
            }
        }

        return result;
    }

    private FwDict listColumnFilterDefs()
    {
        if (!is_list_column_filters)
            return [];

        if (list_column_filter_defs.Count == 0)
            list_column_filter_defs = buildListColumnFilterDefs();

        return list_column_filter_defs;
    }

    private FwDict buildListColumnFilterDefs()
    {
        FwDict result = [];
        var formDefs = listColumnFilterFormFieldDefs();
        var overrides = list_column_filters["fields"] as FwDict ?? [];

        foreach (var field in Utils.qw(getViewListUserFields()))
        {
            if (string.IsNullOrWhiteSpace(field) || !view_list_map.ContainsKey(field))
                continue;

            var def = buildListColumnFilterDef(field, view_list_map[field], formDefs, overrides, overrides.ContainsKey(field));
            if (def.Count > 0)
                result[field] = def;
        }

        foreach (var entry in overrides)
        {
            var field = entry.Key;
            if (string.IsNullOrWhiteSpace(field) || result.ContainsKey(field))
                continue;

            var label = view_list_map[field] ?? field;
            var def = buildListColumnFilterDef(field, label, formDefs, overrides, true);
            if (def.Count > 0)
                result[field] = def;
        }

        return result;
    }

    private FwDict buildListColumnFilterDef(string field, object? label, FwDict formDefs, FwDict overrides, bool isExplicit)
    {
        var def = new FwDict
        {
            ["field"] = field,
            ["field_name"] = field,
            ["label"] = label,
            ["field_name_visible"] = label,
            ["filter_field"] = field,
            ["type"] = "text",
            ["filterable"] = true,
        };

        var formDef = formDefs[field] as FwDict ?? [];
        foreach (var entry in formDef)
        {
            if (entry.Key == "type")
                def["source_field_type"] = entry.Value;
            else if (!def.ContainsKey(entry.Key))
                def[entry.Key] = entry.Value;
        }

        var overrideDef = overrides[field] as FwDict ?? [];
        var explicitType = overrideDef["type"].toStr();
        foreach (var entry in overrideDef)
            if (entry.Key != "type")
                def[entry.Key] = entry.Value;

        if (def["filter_field"].toStr().Length == 0)
            def["filter_field"] = field;

        var schema = schemaForListColumnFilter(def);
        if (schema.Count > 0)
        {
            if (def["field_storage_type"].toStr().Length == 0)
                def["field_storage_type"] = schema["fw_type"];
            if (def["field_db_type"].toStr().Length == 0)
                def["field_db_type"] = schema["type"];
        }

        if (!isExplicit && !hasListColumnFilterInferenceSource(formDef, def, schema))
            return [];

        def["type"] = normalizeListColumnFilterType(explicitType.Length > 0 ? explicitType : inferListColumnFilterType(field, formDef, def));
        if (def["type"].toStr() == "none")
            def["filterable"] = false;

        normalizeListColumnFilterTemplate(def);
        return def;
    }

    private bool hasListColumnFilterInferenceSource(FwDict formDef, FwDict def, FwDict schema)
    {
        return formDef.Count > 0
            || schema.Count > 0
            || def.ContainsKey("options")
            || def["lookup_tpl"].toStr().Length > 0
            || def["lookup_model"].toStr().Length > 0;
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
        if (formType is "select" or "radio" or "multicb" or "multi" or "multicb_prio" || def.ContainsKey("options") || def["lookup_tpl"].toStr().Length > 0)
            return "multi_select";
        if (def["lookup_model"].toStr().Length > 0 && field.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && formType != "autocomplete")
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
        return type.Trim().ToLowerInvariant() switch
        {
            "date" or "datetime" or "date-range" => "date_range",
            "select" or "lookup" or "status" or "multi" => "multi_select",
            "number" or "numeric" => "number_conditions",
            "bool" or "yesno" => "boolean",
            "autocomplete" => "autocomplete",
            "none" => "none",
            "text" or "" => "text",
            var value => value,
        };
    }

    private void normalizeListColumnFilterTemplate(FwDict def)
    {
        var template = def["template"].toStr();
        if (template.Length == 0)
            return;

        if (template == "custom")
            return;

        logger(LogLevel.WARN, "Unsupported list column filter template '", template, "' for ", def["field"], "; use template=\"custom\".");
        def.Remove("template");
    }

    private FwDict schemaForListColumnFilter(FwDict def)
    {
        var table = string.IsNullOrEmpty(list_view) ? model0?.table_name ?? "" : list_view;
        var field = def["filter_field"].toStr();
        if (!isSimpleListColumnFilterTable(table) || !isSimpleListColumnFilterField(field))
            return [];

        return db.schemaField(table, field);
    }

    private static bool isSimpleListColumnFilterTable(string value)
    {
        return Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$");
    }

    private static bool isSimpleListColumnFilterField(string value)
    {
        return Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*$");
    }

    private void enrichListColumnFilterHeaders()
    {
        if (!is_list_column_filters || list_headers.Count == 0)
            return;

        var defs = listColumnFilterDefs();
        foreach (FwDict header in list_headers)
        {
            var field = header["field_name"].toStr();
            if (defs[field] is FwDict def)
            {
                var filter = Utils.cloneHashDeep(def) ?? new FwDict(def);
                prepareListColumnFilterHeader(header, filter);
                continue;
            }

            header["filter"] = new FwDict
            {
                ["type"] = "none",
                ["field"] = field,
                ["label"] = header["field_name_visible"],
                ["filterable"] = false,
            };
            header["filter_type"] = "none";
            header["is_filterable"] = false;
        }
    }

    private void prepareListColumnFilterHeader(FwDict header, FwDict filter)
    {
        filter["search_value"] = header["search_value"];
        applyListColumnFilterDisplayValues(filter, decodeListColumnFilterJson(filter["search_value"].toStr()), filter["search_value"].toStr());
        if (filter["type"].toStr() == "multi_select")
            filter["options"] = loadListColumnFilterOptions(filter, filter["values_csv"].toStr());

        filter["display"] = listColumnFilterDisplayText(filter);
        filter["active_class"] = filter["display"].toStr().Length > 0 ? " is-active" : "";

        header["filter"] = filter;
        header["filter_type"] = filter["type"];
        header["is_filterable"] = filter["filterable"];
        header["filter_component"] = filter["component"];
        header["filter_options"] = filter["options"] is IList ? filter["options"] : new FwList();
        header["autocomplete_url"] = filter["autocomplete_url"];
        header["filter_def"] = filter;
    }

    private void applyListColumnFilterDisplayValues(FwDict filter, FwDict? parsed, string rawValue)
    {
        if (parsed == null)
        {
            applyLegacyTextDisplayValues(filter, rawValue);
            return;
        }

        var requestType = parsed["type"].toStr(filter["type"].toStr());
        if (requestType is "blank" or "not_blank")
        {
            filter["blank_op"] = requestType;
            return;
        }

        switch (filter["type"].toStr())
        {
            case "text":
                filter["op"] = parsed["op"].toStr("contains");
                filter["value"] = parsed["value"];
                break;
            case "date_range":
                filter["from"] = parsed["from"];
                filter["to"] = parsed["to"];
                break;
            case "multi_select":
            case "autocomplete":
                filter["values_csv"] = string.Join(",", listColumnFilterValuesFrom(parsed["values"]));
                break;
            case "number_conditions":
                filter["equal"] = parsed["equal"].toStr(parsed["value"].toStr());
                filter["not_equal"] = parsed["not_equal"].toStr(parsed["not_equals"].toStr());
                filter["gte"] = parsed["gte"];
                filter["lte"] = parsed["lte"];
                filter["from"] = parsed["from"];
                filter["to"] = parsed["to"];
                filter["not_between_from"] = parsed["not_between_from"];
                filter["not_between_to"] = parsed["not_between_to"];
                break;
            case "boolean":
                filter["value"] = parsed["value"];
                break;
        }
    }

    private static void applyLegacyTextDisplayValues(FwDict filter, string rawValue)
    {
        if (rawValue.Length == 0 || filter["type"].toStr() != "text")
            return;

        foreach (var (prefix, op) in new[] {
            ("!=", "not_equals"), ("!^", "not_starts_with"), ("!$", "not_ends_with"),
            ("=", "equals"), ("!", "not_contains"), ("^", "starts_with"), ("$", "ends_with"),
        })
        {
            if (!rawValue.StartsWith(prefix))
                continue;
            filter["op"] = op;
            filter["value"] = rawValue[prefix.Length..];
            return;
        }

        filter["op"] = "contains";
        filter["value"] = rawValue;
    }

    private string listColumnFilterDisplayText(FwDict filter)
    {
        return filter["blank_op"].toStr() switch
        {
            "blank" => "blank",
            "not_blank" => "not blank",
            _ => filter["type"].toStr() switch
            {
                "date_range" => rangeDisplay(filter["from"].toStr(), filter["to"].toStr()),
                "multi_select" => valuesDisplay(listColumnFilterValuesFrom(filter["values_csv"]), v => optionLabel(filter, v)),
                "autocomplete" => valuesDisplay(listColumnFilterValuesFrom(filter["values_csv"]), autocompleteLabel),
                "number_conditions" => numberDisplay(filter),
                "boolean" => filter["value"].toStr() switch
                {
                    "1" or "true" or "yes" or "y" => "Yes",
                    "0" or "false" or "no" or "n" => "No",
                    _ => "",
                },
                _ => "",
            },
        };
    }

    private static string rangeDisplay(string from, string to)
    {
        if (from.Length > 0 && to.Length > 0)
            return from == to ? from : $"{from} - {to}";
        if (from.Length > 0)
            return $">= {from}";
        return to.Length > 0 ? $"<= {to}" : "";
    }

    private static string valuesDisplay(StrList values, Func<string, string> labelFor)
    {
        if (values.Count == 1)
            return labelFor(values[0]);
        return values.Count > 1 ? $"{values.Count} selected" : "";
    }

    private string optionLabel(FwDict filter, string value)
    {
        if (filter["options"] is IList optionRows)
            foreach (FwDict option in optionRows)
                if (option["id"].toStr() == value)
                    return option["iname"].toStr(value);
        return value;
    }

    private static string numberDisplay(FwDict filter)
    {
        var equal = filter["equal"].toStr();
        if (equal.Length > 0)
            return $"= {equal}";

        StrList parts = [];
        addDisplayPart(parts, filter["not_equal"].toStr(), "!= ");
        addDisplayPart(parts, filter["gte"].toStr(), ">= ");
        addDisplayPart(parts, filter["lte"].toStr(), "<= ");
        addRangePart(parts, filter["from"].toStr(), filter["to"].toStr(), "");
        addRangePart(parts, filter["not_between_from"].toStr(), filter["not_between_to"].toStr(), "not ");
        return string.Join(", ", parts);
    }

    private static void addDisplayPart(StrList parts, string value, string prefix)
    {
        if (value.Length > 0)
            parts.Add(prefix + value);
    }

    private static void addRangePart(StrList parts, string from, string to, string prefix)
    {
        if (from.Length > 0 || to.Length > 0)
            parts.Add(prefix + (from.Length > 0 && to.Length > 0 ? $"{from} - {to}" : $"{from}{to}"));
    }

    private FwList loadListColumnFilterOptions(FwDict filter, string selectedValues)
    {
        var cacheKey = listColumnFilterOptionsCacheKey(filter, selectedValues);
        if (cacheKey.Length > 0 && list_column_filter_options_cache[cacheKey] is FwList cached)
            return cached;

        FwList result;
        if (filter["options"] is FwDict or IList)
            result = FormUtils.normalizeSelectOptions(filter["options"]);
        else if (filter["lookup_tpl"].toStr().Length > 0)
            result = FormUtils.selectTplOptions(filter["lookup_tpl"].toStr(), fw.route.controller_path.ToLower());
        else if (filter["lookup_model"].toStr().Length > 0)
            result = fw.model(filter["lookup_model"].toStr()).listSelectOptions(filter, selectedValues.Length > 0 ? selectedValues : null);
        else
            result = [];

        if (cacheKey.Length > 0)
            list_column_filter_options_cache[cacheKey] = result;
        return result;
    }

    private string listColumnFilterOptionsCacheKey(FwDict filter, string selectedValues)
    {
        var lookupTpl = filter["lookup_tpl"].toStr();
        if (lookupTpl.Length > 0)
            return $"tpl|{fw.route.controller_path.ToLowerInvariant()}|{lookupTpl}";

        var lookupModel = filter["lookup_model"].toStr();
        if (lookupModel.Length == 0)
            return "";

        var filterBy = filter["filter_by"].toStr();
        var filterValue = filterBy.Length > 0 && filter["i"] is FwDict item ? item[filterBy].toStr() : "";
        return string.Join("|", [
            "model", lookupModel, selectedValues, filter["lookup_params"].toStr(),
            filter["lookup_field"].toStr(), filter["lookup_key"].toStr(),
            filterBy, filterBy.Length > 0 ? filter["filter_field"].toStr() : "", filterValue,
        ]);
    }

    private bool applyListColumnFilterSearch(string fieldName, string value)
    {
        var defs = listColumnFilterDefs();
        if (defs[fieldName] is not FwDict def)
            return false;
        if (!def["filterable"].toBool() || def["type"].toStr() == "none")
            return true;

        var raw = decodeListColumnFilterJson(value);
        if (raw == null)
            return false;
        if (applyListColumnFilter(def, raw))
            return true;

        _ = applyTypedListColumnFilter(def, raw);
        return true;
    }

    private static FwDict? decodeListColumnFilterJson(string value)
    {
        return value.TrimStart().StartsWith('{') ? Utils.jsonDecode(value) as FwDict : null;
    }

    private bool applyTypedListColumnFilter(FwDict filter, FwDict raw)
    {
        var requestType = raw["type"].toStr(filter["type"].toStr());
        var op = raw["op"].toStr();
        if (requestType is "blank" or "not_blank")
            return appendListColumnFilterBlank(filter, requestType == "not_blank");
        if (op is "blank" or "not_blank")
            return appendListColumnFilterBlank(filter, op == "not_blank");

        return filter["type"].toStr() switch
        {
            "text" => applyListColumnFilterText(filter, raw),
            "date_range" => applyListColumnFilterDateRange(filter, raw),
            "multi_select" => appendListColumnFilterIn(filter, listColumnFilterValuesFrom(raw["values"])),
            "autocomplete" => filter["lookup_by_value"].toBool() && raw["value"].toStr().Length > 0
                ? applyListColumnFilterText(filter, raw)
                : appendListColumnFilterIn(filter, listColumnFilterValuesFrom(raw["values"])),
            "number_conditions" => applyListColumnFilterNumber(filter, raw),
            "boolean" => applyListColumnFilterBoolean(filter, raw),
            _ => false,
        };
    }

    private bool applyListColumnFilterText(FwDict filter, FwDict raw)
    {
        var value = raw["value"].toStr();
        if (value.Length == 0)
            return false;

        var op = raw["op"].toStr("contains");
        var sqlOp = op switch
        {
            "equals" or "equal" => "=",
            "not_equals" or "not_equal" => "<>",
            "not_contains" or "not_starts_with" or "not_ends_with" => "NOT LIKE",
            _ => "LIKE",
        };
        var paramValue = op switch
        {
            "equals" or "equal" or "not_equals" or "not_equal" => value,
            "starts_with" or "not_starts_with" => value + "%",
            "ends_with" or "not_ends_with" => "%" + value,
            _ => "%" + value + "%",
        };

        list_where += $" AND {db.sqlTextExpr(sqlListColumnFilterField(filter))} {sqlOp} {addListColumnFilterParam(filter, "text", paramValue)}";
        return true;
    }

    private bool applyListColumnFilterDateRange(FwDict filter, FwDict raw)
    {
        var (from, to) = quickListColumnFilterDateRange(raw["quick"].toStr());
        from ??= parseListColumnFilterDate(raw["from"].toStr());
        to ??= parseListColumnFilterDate(raw["to"].toStr());
        if (from == null && to == null)
            return false;

        var field = sqlListColumnFilterField(filter);
        var isDateOnly = filter["is_date_only"].toBool() || filter["field_storage_type"].toStr() == "date";
        if (from != null)
        {
            var value = isDateOnly ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Unspecified) : listColumnFilterDateBoundary(filter, from.Value.Date);
            list_where += $" AND {field} >= {addListColumnFilterParam(filter, "from", value)}";
        }
        if (to != null)
        {
            var value = isDateOnly ? DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Unspecified) : listColumnFilterDateBoundary(filter, to.Value.Date.AddDays(1));
            list_where += $" AND {field} {(isDateOnly ? "<=" : "<")} {addListColumnFilterParam(filter, "to", value)}";
        }
        return true;
    }

    private (DateTime? from, DateTime? to) quickListColumnFilterDateRange(string quick)
    {
        if (quick.Length == 0)
            return (null, null);

        var today = DateUtils.convertTimezone(DateTime.UtcNow, DateUtils.TZ_UTC, fw.userTimezone).Date;
        return quick switch
        {
            "today" => (today, today),
            "week" => (today.AddDays(-6), today),
            "30" => (today.AddDays(-29), today),
            _ => (null, null),
        };
    }

    private bool applyListColumnFilterNumber(FwDict filter, FwDict raw)
    {
        var field = sqlListColumnFilterField(filter);
        var applied = false;
        if (tryGetListColumnFilterNumber(raw["equal"], out var equal) || tryGetListColumnFilterNumber(raw["value"], out equal))
            applied |= appendListColumnFilterCompare(filter, field, "=", "eq", equal);
        if (tryGetListColumnFilterNumber(raw["not_equal"], out var notEqual) || tryGetListColumnFilterNumber(raw["not_equals"], out notEqual))
            applied |= appendListColumnFilterCompare(filter, field, "<>", "neq", notEqual);
        if (tryGetListColumnFilterNumber(raw["gte"], out var gte))
            applied |= appendListColumnFilterCompare(filter, field, ">=", "gte", gte);
        if (tryGetListColumnFilterNumber(raw["lte"], out var lte))
            applied |= appendListColumnFilterCompare(filter, field, "<=", "lte", lte);
        if (tryGetListColumnFilterNumber(raw["from"], out var from) && tryGetListColumnFilterNumber(raw["to"], out var to))
            applied |= appendListColumnFilterBetween(filter, field, ">=", "<=", "from", "to", from, to);
        if (tryGetListColumnFilterNumber(raw["not_between_from"], out var nbFrom) && tryGetListColumnFilterNumber(raw["not_between_to"], out var nbTo))
            applied |= appendListColumnFilterBetween(filter, field, "<", ">", "notfrom", "notto", nbFrom, nbTo, "OR");
        return applied;
    }

    private bool applyListColumnFilterBoolean(FwDict filter, FwDict raw)
    {
        var value = raw["value"].toStr().Trim().ToLowerInvariant();
        if (value.Length == 0 || value == "all")
            return false;
        if (value == "blank" || value == "not_blank")
            return appendListColumnFilterBlank(filter, value == "not_blank");

        list_where += $" AND {sqlListColumnFilterField(filter)} = {addListColumnFilterParam(filter, "bool", value is "1" or "true" or "yes" or "y" ? 1 : 0)}";
        return true;
    }

    private bool appendListColumnFilterCompare(FwDict filter, string field, string op, string suffix, decimal value)
    {
        list_where += $" AND {field} {op} {addListColumnFilterParam(filter, suffix, value)}";
        return true;
    }

    private bool appendListColumnFilterBetween(
        FwDict filter,
        string field,
        string firstOp,
        string secondOp,
        string firstSuffix,
        string secondSuffix,
        decimal firstValue,
        decimal secondValue,
        string join = "AND")
    {
        var first = addListColumnFilterParam(filter, firstSuffix, firstValue);
        var second = addListColumnFilterParam(filter, secondSuffix, secondValue);
        list_where += $" AND ({field} {firstOp} {first} {join} {field} {secondOp} {second})";
        return true;
    }

    private bool appendListColumnFilterBlank(FwDict filter, bool isNotBlank)
    {
        var field = sqlListColumnFilterField(filter);
        var textExpr = db.sqlTextExpr(field);
        list_where += isNotBlank
            ? $" AND ({field} IS NOT NULL AND {textExpr} <> '')"
            : $" AND ({field} IS NULL OR {textExpr} = '')";
        return true;
    }

    private bool appendListColumnFilterIn(FwDict filter, StrList values)
    {
        if (values.Count == 0)
            return false;

        var sqlParams = new StrList();
        foreach (var value in values.Take(LIST_COLUMN_FILTER_MULTI_VALUE_LIMIT))
            sqlParams.Add(addListColumnFilterParam(filter, "in", normalizeListColumnFilterParamValue(filter, value)));

        list_where += $" AND {sqlListColumnFilterField(filter)} IN ({string.Join(",", sqlParams)})";
        return true;
    }

    private string addListColumnFilterParam(FwDict filter, string suffix, object value)
    {
        var field = filter["field"].toStr();
        var safeField = Regex.Replace(field, @"\W+", "_").Trim('_');
        if (safeField.Length == 0)
            safeField = "field";

        var param = $"cf_{safeField}_{suffix}_{list_where_params.Count}";
        list_where_params[param] = value;
        return "@" + param;
    }

    private string sqlListColumnFilterField(FwDict filter)
    {
        return db.qid(filter["filter_field"].toStr());
    }

    private static object normalizeListColumnFilterParamValue(FwDict filter, string value)
    {
        if (filter["type"].toStr() == "autocomplete")
            value = autocompleteValue(value);

        return filter["field_storage_type"].toStr() switch
        {
            "int" => value.toLong(),
            "float" => value.toFloat(),
            "decimal" => value.toDecimal(),
            _ => value,
        };
    }

    private static StrList listColumnFilterValuesFrom(object? raw)
    {
        var values = new StrList();
        if (raw is IList list && raw is not string)
            foreach (var item in list)
                addListColumnFilterValue(values, item);
        else
            foreach (var item in raw.toStr().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                addListColumnFilterValue(values, item);
        return new StrList(values.Take(LIST_COLUMN_FILTER_MULTI_VALUE_LIMIT));
    }

    private static void addListColumnFilterValue(StrList values, object? value)
    {
        var str = value.toStr().Trim();
        if (str.Length > 0)
            values.Add(str);
    }

    private static string autocompleteValue(string value)
    {
        var separatorIndex = value.IndexOf(FormUtils.AUTOCOMPLETE_SEPARATOR, StringComparison.Ordinal);
        return separatorIndex < 0 ? value : value[(separatorIndex + FormUtils.AUTOCOMPLETE_SEPARATOR.Length)..].Trim();
    }

    private static string autocompleteLabel(string value)
    {
        var separatorIndex = value.IndexOf(FormUtils.AUTOCOMPLETE_SEPARATOR, StringComparison.Ordinal);
        return separatorIndex < 0 ? value : value[..separatorIndex].Trim();
    }

    private DateTime? parseListColumnFilterDate(string value)
    {
        if (value.Length == 0)
            return null;

        var sql = DateUtils.Str2SQL(value, fw.userDateFormat);
        return sql.Length == 0 ? null : DateUtils.SQL2Date(sql);
    }

    private object listColumnFilterDateBoundary(FwDict filter, DateTime userLocalDate)
    {
        var local = DateTime.SpecifyKind(userLocalDate, DateTimeKind.Unspecified);
        var utc = DateUtils.convertTimezone(local, fw.userTimezone, DateUtils.TZ_UTC);
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        var storageType = filter["field_storage_type"].toStr();
        var filterField = filter["filter_field"].toStr();
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
        return str.Length > 0 && decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
