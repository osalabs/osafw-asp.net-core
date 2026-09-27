using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;

namespace osafw;

public partial class Settings
{
    public const int MASK_NONE = 0;
    public const int MASK_HIDDEN = 10;
    public const int MASK_EDGES = 20;
    public const int MASK_SUFFIX = 30;
    public const int MASK_REVEAL = 40;
    public const int BASIS_INHERIT = 0;
    public const int BASIS_VALUE = 1;
    public const int MAX_TRANSFER_BYTES = 1048576;
    private static readonly object storeLock = new();

    public static readonly IReadOnlyDictionary<string, string> DEFAULT_VALUES = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SITE_NAME"] = "Site Name", ["UNLOGGED_DEFAULT_URL"] = "/", ["LOGGED_DEFAULT_URL"] = "/Main",
        ["feedback_email"] = "", ["support_email"] = "support@website.com", ["mail_from"] = "admin@website.com",
        ["admin_email"] = "", ["test_email"] = "", ["mail.host"] = "", ["mail.port"] = "587",
        ["mail.is_ssl"] = "1", ["mail.username"] = "", ["mail.password"] = "",
        ["AWS_CREDENTIAL_SOURCE"] = "sdk", ["AWSAccessKey"] = "", ["AWSSecretKey"] = "",
        ["OPENAI_API_KEY"] = "", ["API_KEY"] = "", ["is_mfa_enforced"] = "0",
        ["is_list_btn_left"] = "0", ["ui_theme"] = "0", ["ui_mode"] = "0"
    };

    private string mapKey => "settings:" + db.cacheIdentity;

    private Dictionary<string, FwDict> snapshot()
    {
        lock (storeLock)
        {
            if (FwCache.getValue(mapKey) is Dictionary<string, FwDict> cached)
                return cached;
            var rows = db.array(table_name, []);
            var result = new Dictionary<string, FwDict>(StringComparer.Ordinal);
            foreach (FwDict row in rows)
            {
                if (!row.ContainsKey("basis") || !row.ContainsKey("access_level") || !row.ContainsKey("mask"))
                    throw new UserException("Settings schema upgrade is required.");
                result.Add(row["icode"].toStr(), new FwDict(row));
            }
            FwCache.setValue(mapKey, result);
            return result;
        }
    }

    /// <summary>Invalidates only this database's values plus existing dependent menu data.</summary>
    public void invalidate()
    {
        lock (storeLock)
        {
            FwCache.remove(mapKey);
            FwCache.remove("main_menu");
            fw.cache.requestRemoveWithPrefix(cache_prefix);
        }
    }

    private string effective(FwDict row, string defaultValue)
    {
        if (row.Count == 0 || row["basis"].toInt(BASIS_VALUE) == BASIS_INHERIT)
            return defaultValue;
        var value = row["ivalue"].toStr();
        return isCredential(row) ? FwSettingsProtection.unprotect(fw, row["icode"].toStr(), value) : value;
    }

    /// <summary>Reads a credential for a trusted runtime consumer, never for generic page-state projection.</summary>
    public virtual string readSecret(string code)
    {
        FwSettingsProtection.markSensitiveRequest(fw);
        var row = oneByIcode(code);
        if (row.Count == 0)
            return "";
        if (!isCredential(row))
            throw new InvalidOperationException("The setting is not defined as a credential.");
        return effective(row, "");
    }

    public static bool isCredential(FwDict row)
    {
        return row["input"].toInt() == INPUT_CREDENTIAL
            || row["icode"].toStr() is "mail.password" or "AWSAccessKey" or "AWSSecretKey" or "OPENAI_API_KEY" or "API_KEY";
    }

    public static int maskPolicy(FwDict row)
    {
        var mask = row["mask"].toInt();
        return isCredential(row) && mask == MASK_NONE ? MASK_HIDDEN : mask;
    }

    /// <summary>Returns only permitted display text; unreadable secrets never break the whole settings page.</summary>
    public string display(FwDict row)
    {
        if (row["basis"].toInt(BASIS_VALUE) == BASIS_INHERIT)
            return "Use default";
        var mask = maskPolicy(row);
        try
        {
            var value = effective(row, "");
            if (mask == MASK_NONE)
                return value;
            if (value.Length == 0)
                return "Not configured";
            return mask switch
            {
                MASK_EDGES when value.Length > 12 => value[..4] + "..." + value[^4..],
                MASK_SUFFIX when value.Length > 8 => "***" + value[^4..],
                _ => "*** (configured)"
            };
        }
        catch (CryptographicException)
        {
            return "Unavailable: replace the value or restore its keys";
        }
    }

    /// <summary>Enforces the row floor. Runtime system reads intentionally do not use this editor authorization.</summary>
    public void authorize(FwDict row, bool is_edit = false)
    {
        if (row.Count == 0)
            throw new UserException("Setting not found.");
        var minimum = row["access_level"].toInt(Users.ACL_SITEADMIN);
        if (isCredential(row))
            minimum = Math.Max(minimum, Users.ACL_SITEADMIN);
        if (fw.userAccessLevel < Math.Max(Users.ACL_ADMIN, minimum)
            || (is_edit && !row["is_user_edit"].toBool()))
            throw new AuthException();
    }

    private FwDict prepareValue(FwDict row, string value, int basis)
    {
        if (basis != BASIS_INHERIT && basis != BASIS_VALUE)
            throw new UserException("Invalid setting basis.");
        if (basis == BASIS_VALUE)
            validateValue(row, value);
        return new FwDict
        {
            ["basis"] = basis,
            ["ivalue"] = basis == BASIS_INHERIT ? "" : isCredential(row)
                ? FwSettingsProtection.protect(fw, row["icode"].toStr(), value) : value
        };
    }

    /// <summary>Shared validation for forms, programmatic writes and full import. Errors never contain values.</summary>
    public void validateValue(FwDict row, string value)
    {
        var code = row["icode"].toStr();
        var input = row["input"].toInt();
        if (code is "UNLOGGED_DEFAULT_URL" or "LOGGED_DEFAULT_URL")
        {
            if (!Utils.isAppUrl(value, fw.config("ROOT_DOMAIN").toStr()))
                throw new UserException("The default URL must be local to this application.");
        }
        if (code is "feedback_email" or "support_email" or "mail_from" or "admin_email" or "test_email")
        {
            if (value.Length > 0 && !(code == "test_email" && value.Equals("current_user", StringComparison.OrdinalIgnoreCase)))
            {
                try { _ = new MailAddress(value); }
                catch { throw new UserException("Invalid setting email address."); }
                if (value.Contains('\r') || value.Contains('\n'))
                    throw new UserException("Invalid setting email address.");
            }
        }
        if (code == "mail.host" && (value.Contains('/') || value.Any(char.IsWhiteSpace)))
            throw new UserException("Enter an SMTP host name without a URL or whitespace.");
        if (input == INPUT_SWITCH && value != "0" && value != "1")
            throw new UserException("A switch setting requires 0 or 1.");
        if (input is INPUT_NUMBER or INPUT_RANGE)
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                throw new UserException("A numeric setting requires a number.");
            var metadata = Utils.qh(row["allowed_values"].toStr(), "");
            if (metadata.TryGetValue("min", out object? min) && decimal.TryParse(min.toStr(), CultureInfo.InvariantCulture, out var low) && number < low
                || metadata.TryGetValue("max", out object? max) && decimal.TryParse(max.toStr(), CultureInfo.InvariantCulture, out var high) && number > high)
                throw new UserException("Setting is outside its allowed range.");
            if (metadata.TryGetValue("step", out object? step) && decimal.TryParse(step.toStr(), CultureInfo.InvariantCulture, out var increment) && increment > 0)
            {
                decimal.TryParse(metadata["min"].toStr(), CultureInfo.InvariantCulture, out var origin);
                if ((number - origin) % increment != 0)
                    throw new UserException("Setting does not match its allowed step.");
            }
        }
        if (input is INPUT_SELECT or INPUT_RADIO or INPUT_CHECKBOX or INPUT_SELECT_MULTI)
        {
            var options = Utils.qh(row["allowed_values"].toStr(), "");
            var values = input is INPUT_CHECKBOX or INPUT_SELECT_MULTI ? value.Split(',') : [value];
            if (values.Any(option => option.Length > 0 && !options.ContainsKey(option)))
                throw new UserException("Invalid setting option.");
        }
        if (code == "mail.port" && (!int.TryParse(value, out var port) || port < 1 || port > 65535))
            throw new UserException("SMTP port must be between 1 and 65535.");
    }

    public override int add(FwDict item)
    {
        lock (storeLock)
        {
            var fields = new FwDict(item);
            var code = fields["icode"].toStr();
            if (code.Length == 0)
                throw new UserException("A setting requires a code.");
            if (!fields.ContainsKey("basis")) fields["basis"] = BASIS_VALUE;
            if (!fields.ContainsKey("access_level")) fields["access_level"] = Users.ACL_SITEADMIN;
            if (!fields.ContainsKey("mask")) fields["mask"] = isCredential(fields) ? MASK_HIDDEN : MASK_NONE;
            if (isCredential(fields)) fields["access_level"] = Users.ACL_SITEADMIN;
            var prepared = prepareValue(fields, fields["ivalue"].toStr(), fields["basis"].toInt());
            fields["ivalue"] = prepared["ivalue"];
            try { return base.add(fields); }
            finally { invalidate(); }
        }
    }

    public override bool update(int id, FwDict item)
    {
        lock (storeLock)
        {
            var row = db.row(table_name, DB.h("id", id));
            if (row.Count == 0)
                throw new UserException("Setting not found.");
            var fields = new FwDict(item);
            var merged = new FwDict(row);
            foreach (var entry in fields) merged[entry.Key] = entry.Value;
            if (row["icode"].toStr() != merged["icode"].toStr() || isCredential(row) != isCredential(merged))
                throw new UserException("Setting identity and credential classification require a schema migration.");
            if (isCredential(merged))
            {
                fields["access_level"] = Users.ACL_SITEADMIN;
                if (maskPolicy(merged) == MASK_HIDDEN) fields["mask"] = MASK_HIDDEN;
            }
            if (fields.ContainsKey("ivalue") || fields.ContainsKey("basis"))
            {
                var basis = fields.ContainsKey("basis") ? fields["basis"].toInt() : BASIS_VALUE;
                if (!fields.ContainsKey("ivalue") && basis == BASIS_VALUE)
                    throw new UserException("An explicit value is required.");
                var code = row["icode"].toStr();
                if (code is "AWS_CREDENTIAL_SOURCE" or "AWSAccessKey" or "AWSSecretKey")
                {
                    var rows = db.array(table_name, []).ToDictionary(value => value["icode"].toStr(), value => (FwDict)value, StringComparer.Ordinal);
                    validateCredentialGroup([new ValueChange { icode = code, basis = basis, ivalue = fields["ivalue"].toStr() }], rows);
                }
                var prepared = prepareValue(merged, fields["ivalue"].toStr(), basis);
                foreach (var entry in prepared) fields[entry.Key] = entry.Value;
            }
            try { return base.update(id, fields); }
            finally { invalidate(); }
        }
    }

    public override void delete(int id, bool is_perm = false)
    {
        throw new UserException("Site Settings cannot be deleted.");
    }

    public sealed class Transfer
    {
        public int version { get; set; } = 1;
        public List<ValueChange> settings { get; set; } = [];
    }

    public sealed class ValueChange
    {
        public string icode { get; set; } = "";
        public int basis { get; set; }
        public string? ivalue { get; set; }
    }

    /// <summary>Full plaintext transfer for a separately authorized Site Admin endpoint. Never log the result.</summary>
    public string exportJson()
    {
        FwSettingsProtection.markSensitiveRequest(fw);
        lock (storeLock)
        {
            var result = new Transfer();
            foreach (FwDict row in db.array(table_name, []))
            {
                var basis = row["basis"].toInt();
                result.settings.Add(new ValueChange
                {
                    icode = row["icode"].toStr(), basis = basis,
                    ivalue = basis == BASIS_INHERIT ? null : effective(row, "")
                });
            }
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>Applies a same-app restore by code; target definitions and their security metadata are never imported.</summary>
    public int importJson(string json)
    {
        FwSettingsProtection.markSensitiveRequest(fw);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MAX_TRANSFER_BYTES)
            throw new UserException("Settings import is too large.");
        Transfer transfer;
        try { transfer = JsonSerializer.Deserialize<Transfer>(json) ?? throw new JsonException(); }
        catch (JsonException) { throw new UserException("Invalid Settings JSON."); }
        if (transfer.version != 1 || transfer.settings == null)
            throw new UserException("Unsupported Settings transfer format.");
        return writeBatch(transfer.settings);
    }

    /// <summary>Trusted system operation. Controllers must authorize every row or restrict full restore to Site Admin.</summary>
    public int writeBatch(IReadOnlyCollection<ValueChange> changes)
    {
        FwSettingsProtection.markSensitiveRequest(fw);
        if (changes.Count == 0)
            return 0;
        lock (storeLock)
        {
            var rows = db.array(table_name, []).ToDictionary(row => row["icode"].toStr(), row => (FwDict)row, StringComparer.Ordinal);
            var codes = new HashSet<string>(StringComparer.Ordinal);
            var prepared = new List<(int id, FwDict fields)>();
            foreach (var change in changes)
            {
                if (change == null || string.IsNullOrWhiteSpace(change.icode) || !codes.Add(change.icode) || !rows.TryGetValue(change.icode, out var row))
                    throw new UserException("Settings import contains an unknown or duplicate code.");
                if (change.basis == BASIS_VALUE && change.ivalue == null)
                    throw new UserException("An explicit setting value is required.");
                prepared.Add((row["id"].toInt(), prepareValue(row, change.ivalue ?? "", change.basis)));
            }
            validateCredentialGroup(changes, rows);
            db.begin();
            try
            {
                foreach (var entry in prepared)
                {
                    entry.fields["upd_time"] = DB.NOW;
                    if (fw.isLogged) entry.fields["upd_users_id"] = fw.userId;
                    if (db.update(table_name, entry.fields, DB.h("id", entry.id)) != 1)
                        throw new UserException("A setting changed during import.");
                }
                if (is_log_changes)
                    fw.logActivity(FwLogTypes.ICODE_UPDATED, table_name, 0, "Settings batch: " + prepared.Count);
                db.commit();
            }
            catch
            {
                db.rollback();
                throw;
            }
            finally { invalidate(); }
            return prepared.Count;
        }
    }

    private void validateCredentialGroup(IReadOnlyCollection<ValueChange> changes, Dictionary<string, FwDict> rows)
    {
        if (!changes.Any(change => change.icode is "AWS_CREDENTIAL_SOURCE" or "AWSAccessKey" or "AWSSecretKey"))
            return;
        string value(string code)
        {
            var change = changes.FirstOrDefault(item => item.icode == code);
            var fallback = DEFAULT_VALUES.TryGetValue(code, out var defaultValue) ? defaultValue : "";
            return change == null ? (rows.TryGetValue(code, out var row) ? effective(row, fallback) : fallback)
                : change.basis == BASIS_INHERIT ? fallback : change.ivalue ?? "";
        }
        var mode = value("AWS_CREDENTIAL_SOURCE");
        var key = value("AWSAccessKey");
        var secret = value("AWSSecretKey");
        if (mode != "sdk" && mode != "static" || (key.Length == 0) != (secret.Length == 0)
            || mode == "static" && key.Length == 0)
            throw new UserException("Static AWS credentials require a complete key pair.");
    }

    /// <summary>Explicit, maintenance-only conversion of legacy plaintext credentials; never performed while reading.</summary>
    public int migratePlaintextCredentials()
    {
        var changes = new List<ValueChange>();
        foreach (FwDict row in db.array(table_name, []))
        {
            var value = row["ivalue"].toStr();
            if (isCredential(row) && value.Length > 0 && !value.StartsWith(FwSettingsProtection.PREFIX, StringComparison.Ordinal))
                changes.Add(new ValueChange { icode = row["icode"].toStr(), basis = BASIS_VALUE, ivalue = value });
        }
        return writeBatch(changes);
    }
}
