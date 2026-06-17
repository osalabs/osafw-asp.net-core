// Site Settings Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Globalization;
using System.Linq;

namespace osafw;

public class AdminSettingsController : FwAdminController
{
    public static new int access_level = Users.ACL_ADMIN;

    protected Settings model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Settings>();
        model0 = model;

        base_url = "/Admin/Settings";
        required_fields = "ivalue";
        save_fields = "ivalue";
        save_fields_checkboxes = "";

        search_fields = "icode iname ivalue";
        list_sortdef = "iname asc";
        list_sortmap = Utils.qh("id|id iname|iname upd_time|upd_time");
    }

    public override void getListRows()
    {
        base.getListRows();

        foreach (FwDict row in list_rows)
            prepareListRow(row);
    }

    public override void setListSearch()
    {
        base.setListSearch();

        if (hasIcatFilter())
        {
            list_where += " and icat=@icat";
            list_where_params["icat"] = list_filter["icat"].toStr();
        }
    }

    public override FwDict setListPS(FwDict? ps = null)
    {
        ps = base.setListPS(ps);

        ps["has_icat_filter"] = hasIcatFilter();
        ps["settings_categories"] = model.listCategories();

        return ps;
    }

    private bool hasIcatFilter()
    {
        return list_filter.ContainsKey("icat");
    }

    public override FwDict ShowFormAction(int id = 0)
    {
        if (id == 0)
        {
            fw.redirect(base_url);
            return null!;
        }

        // set new form defaults here if any
        // Me.form_new_defaults = New FwRow
        // item("field")="default value"
        var ps = base.ShowFormAction(id) ?? [];

        var item = ps["i"] as FwDict ?? [];
        prepareFormItem(item, ps);

        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;
        route_return = FW.ACTION_INDEX;

        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in save_fields");

        FwDict item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        var setting = model.one(id);
        Validate(id, item, setting);

        FwDict itemdb = prepareSaveFields(setting, item);

        // only update, no add new settings
        if (itemdb.Count > 0)
            model.update(id, itemdb);
        fw.flash("record_updated", 1);

        // custom code:
        // reset cache
        FwCache.remove("main_menu");

        return this.afterSave(success, id);
    }

    public override void Validate(int id, FwDict item)
    {
        Validate(id, item, id > 0 ? model.one(id) : []);
    }

    private void Validate(int id, FwDict item, FwDict setting)
    {
        bool result = true;

        if (id == 0)
            throw new UserException("Wrong Settings ID");

        int input = setting["input"].toInt();
        if (requiresSubmittedValue(input) && !hasSubmittedValue(item))
        {
            fw.FormErrors["ivalue"] = true;
            fw.FormErrors["REQUIRED"] = true;
            result = false;
        }

        if (!validateByInputType(setting, item))
            result = false;

        this.validateCheckResult(result);
    }

    public override FwDict DeleteAction(int id)
    {
        throw new UserException("Site Settings cannot be deleted");
    }

    private void prepareListRow(FwDict row)
    {
        row["ivalue_display"] = displayValue(row);
    }

    private void prepareFormItem(FwDict item, FwDict ps)
    {
        int input = item["input"].toInt();
        item["ivalue_display"] = displayValue(item);
        item["credential_display"] = maskCredential(item["ivalue"].toStr());

        var meta = inputMetadata(item);
        ps["input_min"] = meta["min"];
        ps["input_max"] = meta["max"];
        ps["input_step"] = meta["step"];
        ps["has_input_min"] = meta.ContainsKey("min");
        ps["has_input_max"] = meta.ContainsKey("max");
        ps["has_input_step"] = meta.ContainsKey("step");
        ps["settings_options"] = listInputOptions(item, input == Settings.INPUT_CHECKBOX || input == Settings.INPUT_SELECT_MULTI);
    }

    private FwDict prepareSaveFields(FwDict setting, FwDict item)
    {
        int input = setting["input"].toInt();
        FwDict itemdb = [];

        if (input == Settings.INPUT_CREDENTIAL)
        {
            string value = item["ivalue"].toStr();
            if (!string.IsNullOrWhiteSpace(value))
                itemdb["ivalue"] = value;
            return itemdb;
        }

        itemdb["ivalue"] = input switch
        {
            Settings.INPUT_CHECKBOX => FormUtils.multi2ids(reqh("ivalue_multi")),
            Settings.INPUT_SWITCH => item.ContainsKey("ivalue") ? "1" : "0",
            Settings.INPUT_NUMBER or Settings.INPUT_RANGE => item["ivalue"].toStr().Trim(),
            _ => item["ivalue"].toStr(),
        };

        return itemdb;
    }

    private bool validateByInputType(FwDict setting, FwDict item)
    {
        int input = setting["input"].toInt();
        var options = allowedOptionValues(setting);

        if (input == Settings.INPUT_SELECT || input == Settings.INPUT_RADIO)
            return validateOptionValue(item["ivalue"].toStr(), options);

        if (input == Settings.INPUT_CHECKBOX)
            return validateOptionValues(reqh("ivalue_multi").Keys.Select(x => x.toStr()), options);

        if (input == Settings.INPUT_SELECT_MULTI)
            return validateOptionValues(FormUtils.comma_str2col(item["ivalue"].toStr()), options);

        if (input == Settings.INPUT_NUMBER || input == Settings.INPUT_RANGE)
            return validateNumberValue(setting, item["ivalue"].toStr(), true);

        return true;
    }

    private bool validateOptionValue(string value, FwDict options)
    {
        if (string.IsNullOrEmpty(value))
            return true;

        if (options.ContainsKey(value))
            return true;

        fw.FormErrors["ivalue"] = "INVALID";
        return false;
    }

    private bool validateOptionValues(IEnumerable<string> values, FwDict options)
    {
        foreach (string value in values)
        {
            if (string.IsNullOrEmpty(value))
                continue;

            if (!options.ContainsKey(value))
            {
                fw.FormErrors["ivalue"] = "INVALID";
                return false;
            }
        }

        return true;
    }

    private bool validateNumberValue(FwDict setting, string rawValue, bool enforceRange)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return true;

        if (!tryParseDecimal(rawValue, out decimal value))
        {
            fw.FormErrors["ivalue"] = "NUMBER";
            return false;
        }

        if (!enforceRange)
            return true;

        var meta = inputMetadata(setting);
        if (meta.TryGetValue("min", out object? minRaw) && tryParseDecimal(minRaw.toStr(), out decimal min) && value < min)
        {
            fw.FormErrors["ivalue"] = "MIN";
            return false;
        }

        if (meta.TryGetValue("max", out object? maxRaw) && tryParseDecimal(maxRaw.toStr(), out decimal max) && value > max)
        {
            fw.FormErrors["ivalue"] = "MAX";
            return false;
        }

        if (meta.TryGetValue("step", out object? stepRaw) && tryParseDecimal(stepRaw.toStr(), out decimal step) && step > 0)
        {
            decimal baseValue = meta.TryGetValue("min", out object? rangeMinRaw) && tryParseDecimal(rangeMinRaw.toStr(), out decimal rangeMin) ? rangeMin : 0;
            if ((value - baseValue) % step != 0)
            {
                fw.FormErrors["ivalue"] = "STEP";
                return false;
            }
        }

        return true;
    }

    private static bool tryParseDecimal(string value, out decimal result)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static bool requiresSubmittedValue(int input)
    {
        return input != Settings.INPUT_CHECKBOX
            && input != Settings.INPUT_SELECT_MULTI
            && input != Settings.INPUT_SWITCH
            && input != Settings.INPUT_CREDENTIAL;
    }

    private static bool hasSubmittedValue(FwDict item)
    {
        return item.ContainsKey("ivalue") && !string.IsNullOrWhiteSpace(item["ivalue"].toStr());
    }

    private static FwDict allowedOptionValues(FwDict setting)
    {
        return Utils.qh(setting["allowed_values"].toStr(), "");
    }

    private static FwDict inputMetadata(FwDict setting)
    {
        return Utils.qh(setting["allowed_values"].toStr(), "");
    }

    private static FwList listInputOptions(FwDict setting, bool isMulti)
    {
        FwList result = [];
        string current = setting["ivalue"].toStr();
        var selected = isMulti ? FormUtils.ids2multi(current) : [];

        foreach (string token in Utils.qw(setting["allowed_values"].toStr()))
        {
            if (string.IsNullOrEmpty(token))
                continue;

            string[] parts = token.Split("|", 2);
            string id = parts[0];
            if (string.IsNullOrEmpty(id))
                continue;

            string name = parts.Length > 1 ? parts[1] : id;
            bool isSelected = isMulti ? selected.ContainsKey(id) : string.Equals(id, current, StringComparison.Ordinal);
            result.Add(new FwDict {
                ["id"] = id,
                ["iname"] = name,
                ["is_selected"] = isSelected,
            });
        }

        return result;
    }

    private static string displayValue(FwDict setting)
    {
        return setting["input"].toInt() == Settings.INPUT_CREDENTIAL
            ? maskCredential(setting["ivalue"].toStr())
            : setting["ivalue"].toStr();
    }

    public static string maskCredential(string value)
    {
        value = value.toStr();
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= 12 ? "******" : value[..6] + "..." + value[^6..];
    }
}
