// Site Settings Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class AdminSettingsController : FwAdminController
{
    public static new int access_level = Users.ACL_ADMIN;

    protected Settings model = null!;

    public override void init(FW fw)
    {
        Settings.markSensitiveRequest();
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
        access_actions_to_permissions = new FwDict
        {
            ["Reveal"] = Permissions.PERMISSION_VIEW,
        };
    }

    // Generic CRUD, navigation and export actions do not use the Settings editor's row permissions or masking.
    public override FwDict NextAction(string form_id) => throw new AuthException();
    public override void ShowDeleteAction(int id) => throw new AuthException();
    public override FwDict? DeleteAction(int id) => throw new AuthException();
    public override FwDict? RestoreDeletedAction(int id) => throw new AuthException();
    public override FwDict? SaveMultiAction() => throw new AuthException();
    public override FwDict QuickSearchAction() => throw new AuthException();
    public override FwDict GoAction() => throw new AuthException();
    public override void exportList() => throw new AuthException();

    public override void getListRows()
    {
        base.getListRows();

        foreach (FwDict row in list_rows)
            prepareListRow(row);
    }

    public override void setListSearch()
    {
        base.setListSearch();

        list_where += @"
            and access_level <= @settings_access_level
            and (
                @settings_access_level >= @settings_site_admin_level
                or input <> @settings_credential_input
            )";
        list_where_params["settings_access_level"] = fw.userAccessLevel;
        list_where_params["settings_site_admin_level"] = Users.ACL_SITEADMIN;
        list_where_params["settings_credential_input"] = Settings.INPUT_CREDENTIAL;

        if (list_filter.ContainsKey("icat"))
        {
            list_where += @"
                and icat = @icat";
            list_where_params["icat"] = list_filter["icat"].toStr();
        }
    }

    public override FwDict setListPS(FwDict? ps = null)
    {
        ps = base.setListPS(ps);

        ps["has_icat_filter"] = list_filter.ContainsKey("icat");
        ps["settings_categories"] = listVisibleCategories();
        ps["is_site_admin"] = fw.model<Users>().isSiteAdmin();

        return ps;
    }

    private FwList listVisibleCategories()
    {
        return db.arrayp($@"
            select icat
            from {model.qTable()}
            where access_level <= @settings_access_level
                and (
                    @settings_access_level >= @settings_site_admin_level
                    or input <> @settings_credential_input
                )
            group by icat
            order by
                case when icat = '' then 0 else 1 end,
                icat",
            DB.h(
                "settings_access_level", fw.userAccessLevel,
                "settings_site_admin_level", Users.ACL_SITEADMIN,
                "settings_credential_input", Settings.INPUT_CREDENTIAL));
    }

    public override FwDict? ShowAction(int id)
    {
        var row = settingForAccess(id);
        var ps = base.ShowAction(id) ?? [];

        ps["i"] = safeProjection(row);

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
        var item = safeProjection(stored);

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
        Validate(id, item);

        FwDict itemdb = prepareSaveFields(setting, item);
        if (itemdb.Count > 0)
            model.update(id, itemdb);

        fw.flash("record_updated", 1);

        if (isProtectedEditor(setting))
            item.Remove("ivalue");

        return afterSave(true, id);
    }

    public FwDict RevealAction(int id)
    {
        enforcePost();
        var setting = settingForAccess(id);
        if (Settings.maskPolicy(setting) != Settings.MASK_REVEAL)
            throw new AuthException();

        fw.cache_control = "no-store";
        fw.response.Headers.CacheControl = "no-store";
        fw.response.Headers.Pragma = "no-cache";

        string value = Settings.isCredential(setting)
            ? model.readSecret(setting["icode"].toStr())
            : model.getValue(setting["icode"].toStr());
        return new FwDict
        {
            ["_json"] = new FwDict { ["value"] = value },
        };
    }

    public override void Validate(int id, FwDict item)
    {
        if (id == 0)
            throw new UserException("Wrong Settings ID");

        var setting = model.one(id);
        int basis = item.ContainsKey("basis") ? item["basis"].toInt(-1) : setting["basis"].toInt(Settings.BASIS_VALUE);
        if (basis != Settings.BASIS_INHERIT && basis != Settings.BASIS_VALUE)
        {
            addFormError("basis", "INVALID");
            validateCheckResult(false);
        }

        bool isValid = true;
        int input = setting["input"].toInt();
        if (basis != Settings.BASIS_INHERIT && !isProtectedEditor(setting)
            && requiresSubmittedValue(input) && !item.ContainsKey("ivalue"))
        {
            addFormError("ivalue", "REQUIRED");
            isValid = false;
        }

        var fields = prepareSaveFields(setting, item);
        if (fields.ContainsKey("ivalue"))
        {
            string error = model.valueError(setting, fields["ivalue"].toStr());
            if (error.Length > 0)
            {
                addFormError("ivalue", error);
                isValid = false;
            }
        }

        validateCheckResult(isValid);
    }

    private FwDict settingForAccess(int id, bool isEdit = false)
    {
        var row = model.one(id);
        model.authorize(row, isEdit);

        return row;
    }

    private FwDict safeProjection(FwDict row)
    {
        var result = new FwDict(row)
        {
            ["ivalue_display"] = model.display(row),
        };

        if (isProtectedEditor(row) || row["basis"].toInt() == Settings.BASIS_INHERIT)
            result["ivalue"] = "";

        return result;
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
        ps["editor_input"] = isProtectedEditor(stored) ? Settings.INPUT_CREDENTIAL : input;
        ps["is_basis_inherit"] = stored["basis"].toInt() == Settings.BASIS_INHERIT;
        ps["is_basis_value"] = stored["basis"].toInt() == Settings.BASIS_VALUE;
        ps["can_reveal"] = Settings.maskPolicy(stored) == Settings.MASK_REVEAL;

        var meta = Utils.qh(stored["allowed_values"].toStr(), "");
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
                throw new UserException("Enter a value, select Clear, or keep Use default.");

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

    private static bool requiresSubmittedValue(int input)
    {
        return input != Settings.INPUT_CHECKBOX
            && input != Settings.INPUT_SELECT_MULTI
            && input != Settings.INPUT_SWITCH
            && input != Settings.INPUT_CREDENTIAL;
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

            result.Add(new FwDict
            {
                ["id"] = id,
                ["iname"] = name,
                ["is_selected"] = isSelected,
            });
        }

        return result;
    }
}
