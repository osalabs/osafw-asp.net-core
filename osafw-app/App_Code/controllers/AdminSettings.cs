// Site Settings Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace osafw;

public class AdminSettingsController : FwAdminController
{
    public static new int access_level = Users.ACL_ADMIN;

    private static readonly HashSet<string> allowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "", FW.ACTION_INDEX, FW.ACTION_SHOW, FW.ACTION_SHOW_FORM, FW.ACTION_SAVE,
        "Reveal", "Migrate"
    };

    protected Settings model = null!;

    public override void init(FW fw)
    {
        FwSettingsProtection.markSensitiveRequest(fw);
        base.init(fw);
        model = fw.model<Settings>();
        model0 = model;

        base_url = "/Admin/Settings";
        required_fields = "";
        save_fields = "ivalue";
        save_fields_checkboxes = "";

        search_fields = "icode iname idesc";
        list_sortdef = "iname asc";
        list_sortmap = Utils.qh("id|id iname|iname upd_time|upd_time");
    }

    public override void checkAccess()
    {
        access_actions_to_permissions = new FwDict
        {
            ["Reveal"] = Permissions.PERMISSION_VIEW,
            ["Migrate"] = Permissions.PERMISSION_EDIT,
        };
        base.checkAccess();

        if (!allowedActions.Contains(fw.route.action.toStr()) || export_format.Length > 0)
            throw new AuthException();
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

        list_where += @" and access_level<=@settings_access_level
 and (@settings_access_level>=100 or (input<>90 and icode not in ('mail.password','AWSAccessKey','AWSSecretKey','OPENAI_API_KEY','API_KEY')))";
        list_where_params["settings_access_level"] = fw.userAccessLevel;

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
        ps["settings_categories"] = listVisibleCategories();
        ps["is_site_admin"] = isSiteAdmin();

        return ps;
    }

    private FwList listVisibleCategories()
    {
        return db.arrayp($@"
select icat
  from {db.qid("settings")}
 where access_level<=@settings_access_level
   and (@settings_access_level>=100 or (input<>90 and icode not in ('mail.password','AWSAccessKey','AWSSecretKey','OPENAI_API_KEY','API_KEY')))
 group by icat
 order by case when icat='' then 0 else 1 end, icat", DB.h("settings_access_level", fw.userAccessLevel));
    }

    private bool hasIcatFilter()
    {
        return list_filter.ContainsKey("icat");
    }

    public override FwDict? ShowAction(int id)
    {
        var row = settingForAccess(id);
        var ps = base.ShowAction(id) ?? [];
        ps["i"] = safeProjection(row, false);
        return ps;
    }

    public override FwDict ShowFormAction(int id = 0)
    {
        if (id == 0)
        {
            fw.redirect(base_url);
            return null!;
        }

        var stored = settingForAccess(id);
        var ps = base.ShowFormAction(id) ?? [];
        var item = safeProjection(stored, true);
        if (!isGet() && !isProtectedEditor(stored))
        {
            var submitted = reqh("item");
            if (submitted.ContainsKey("ivalue"))
                item["ivalue"] = submitted["ivalue"];
        }
        ps["i"] = item;
        ps["is_readonly"] = is_readonly || !stored["is_user_edit"].toBool();
        prepareFormItem(stored, item, ps);

        return ps;
    }

    public override FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;
        route_return = FW.ACTION_INDEX;
        enforcePost();
        checkReadOnly();

        FwDict item = reqh("item");
        var setting = settingForAccess(id, true);
        Validate(id, item, setting);

        FwDict itemdb = prepareSaveFields(setting, item);
        if (itemdb.Count > 0)
            model.update(id, itemdb);
        fw.flash("record_updated", 1);

        if (isProtectedEditor(setting))
            item.Remove("ivalue");

        return this.afterSave(true, id);
    }

    public override void Validate(int id, FwDict item)
    {
        Validate(id, item, id > 0 ? model.one(id) : []);
    }

    public FwDict RevealAction(int id)
    {
        enforcePost();
        var setting = settingForAccess(id);
        if (Settings.maskPolicy(setting) != Settings.MASK_REVEAL)
            throw new AuthException();

        noStore();
        string value = Settings.isCredential(setting)
            ? model.readSecret(setting["icode"].toStr())
            : model.getValue(setting["icode"].toStr());
        return new FwDict
        {
            ["_json"] = new FwDict { ["value"] = value },
        };
    }

    public FwDict? MigrateAction()
    {
        enforcePost();
        checkReadOnly();
        requireSiteAdmin();

        int count = model.migratePlaintextCredentials();
        fw.flash("success", $"Migrated {count} credential settings.");
        return afterSave(true, new FwDict { ["updated"] = count });
    }

    private void Validate(int id, FwDict item, FwDict setting)
    {
        bool result = true;

        if (id == 0)
            throw new UserException("Wrong Settings ID");

        int basis = item.ContainsKey("basis") ? item["basis"].toInt(-1) : setting["basis"].toInt(Settings.BASIS_VALUE);
        if (basis != Settings.BASIS_INHERIT && basis != Settings.BASIS_VALUE)
        {
            addFormError("basis", "INVALID");
            result = false;
        }
        if (basis == Settings.BASIS_INHERIT)
        {
            validateCheckResult(result);
            return;
        }

        int input = setting["input"].toInt();
        if (!isProtectedEditor(setting) && requiresSubmittedValue(input) && !item.ContainsKey("ivalue"))
        {
            addFormError("ivalue", "REQUIRED");
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

    private FwDict settingForAccess(int id, bool isEdit = false)
    {
        var row = model.one(id);
        model.authorize(row, isEdit);
        return row;
    }

    private FwDict safeProjection(FwDict row, bool isEditor)
    {
        var result = new FwDict(row)
        {
            ["ivalue_display"] = model.display(row),
        };
        if (isProtectedEditor(row) || row["basis"].toInt() == Settings.BASIS_INHERIT)
            result["ivalue"] = "";
        else if (!isEditor && Settings.maskPolicy(row) != Settings.MASK_NONE)
            result["ivalue"] = "";
        return result;
    }

    private bool isSiteAdmin()
    {
        return fw.userAccessLevel >= Users.ACL_SITEADMIN;
    }

    private void requireSiteAdmin()
    {
        if (!isSiteAdmin())
            throw new AuthException();
    }

    private void noStore()
    {
        fw.cache_control = "no-store";
        fw.response.Headers.CacheControl = "no-store";
        fw.response.Headers.Pragma = "no-cache";
    }

    private static bool isProtectedEditor(FwDict setting)
    {
        return Settings.isCredential(setting) || Settings.maskPolicy(setting) != Settings.MASK_NONE;
    }

    private void prepareListRow(FwDict row)
    {
        row["ivalue_display"] = model.display(row);
        row["is_switch_on"] = row["input"].toInt() == Settings.INPUT_SWITCH
            && model.getValue(row["icode"].toStr()).toBool();
        row.Remove("ivalue");
    }

    private void prepareFormItem(FwDict stored, FwDict item, FwDict ps)
    {
        int input = stored["input"].toInt();
        item["ivalue_display"] = model.display(stored);
        ps["editor_input"] = isProtectedEditor(stored) ? Settings.INPUT_CREDENTIAL : input;
        ps["is_basis_inherit"] = stored["basis"].toInt() == Settings.BASIS_INHERIT;
        ps["is_basis_value"] = stored["basis"].toInt() == Settings.BASIS_VALUE;
        ps["can_reveal"] = Settings.maskPolicy(stored) == Settings.MASK_REVEAL;

        var meta = inputMetadata(stored);
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
        int oldBasis = setting["basis"].toInt(Settings.BASIS_VALUE);
        int basis = item.ContainsKey("basis") ? item["basis"].toInt(-1) : oldBasis;
        FwDict itemdb = [];

        if (basis == Settings.BASIS_INHERIT)
        {
            itemdb["basis"] = Settings.BASIS_INHERIT;
            return itemdb;
        }

        if (isProtectedEditor(setting))
        {
            string value = item["ivalue"].toStr();
            if (item["clear"].toBool())
            {
                itemdb["basis"] = Settings.BASIS_VALUE;
                itemdb["ivalue"] = "";
            }
            else if (value.Length > 0)
            {
                itemdb["basis"] = Settings.BASIS_VALUE;
                itemdb["ivalue"] = value;
            }
            else if (oldBasis != Settings.BASIS_VALUE)
            {
                throw new UserException("Enter a value, select Clear, or keep Use default.");
            }
            return itemdb;
        }

        itemdb["basis"] = Settings.BASIS_VALUE;
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

        addFormError("ivalue", "INVALID");
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
                addFormError("ivalue", "INVALID");
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
            addFormError("ivalue", "NUMBER");
            return false;
        }

        if (!enforceRange)
            return true;

        var meta = inputMetadata(setting);
        if (meta.TryGetValue("min", out object? minRaw) && tryParseDecimal(minRaw.toStr(), out decimal min) && value < min)
        {
            addFormError("ivalue", "MIN");
            return false;
        }

        if (meta.TryGetValue("max", out object? maxRaw) && tryParseDecimal(maxRaw.toStr(), out decimal max) && value > max)
        {
            addFormError("ivalue", "MAX");
            return false;
        }

        if (meta.TryGetValue("step", out object? stepRaw) && tryParseDecimal(stepRaw.toStr(), out decimal step) && step > 0)
        {
            decimal baseValue = meta.TryGetValue("min", out object? rangeMinRaw) && tryParseDecimal(rangeMinRaw.toStr(), out decimal rangeMin) ? rangeMin : 0;
            if ((value - baseValue) % step != 0)
            {
                addFormError("ivalue", "STEP");
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

}
