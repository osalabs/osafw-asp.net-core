// Framework Settings persistence and protection
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;

namespace osafw;

/// <summary>Shared Settings storage, validation, protection and cache behavior; applications extend Settings.</summary>
public abstract class FwSettings : FwModel<FwSettings.Row>
{
    public const int INPUT_TEXT = 0;
    public const int INPUT_TEXTAREA = 10;
    public const int INPUT_SELECT = 20;
    public const int INPUT_SELECT_MULTI = 21;
    public const int INPUT_CHECKBOX = 30;
    public const int INPUT_RADIO = 40;
    public const int INPUT_DATE = 50;
    public const int INPUT_NUMBER = 60;
    public const int INPUT_SWITCH = 70;
    public const int INPUT_RANGE = 80;
    public const int INPUT_CREDENTIAL = 90;

    public const int MASK_NONE = 0;
    public const int MASK_HIDDEN = 10;
    public const int MASK_EDGES = 20;
    public const int MASK_SUFFIX = 30;
    public const int MASK_REVEAL = 40;
    public const int BASIS_INHERIT = 0;
    public const int BASIS_VALUE = 1;
    public const int MAX_TRANSFER_BYTES = 1048576;
    private static readonly object storeLock = new();

    public const string PROTECTED_PREFIX = "enc:v1:";
    private static readonly ConcurrentDictionary<string, Lazy<ServiceProvider>> providers = new();

    public class Row
    {
        public int id { get; set; }
        public string icat { get; set; } = string.Empty;
        public string icode { get; set; } = string.Empty;
        public string ivalue { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public int input { get; set; }
        public string allowed_values { get; set; } = string.Empty;
        public int is_user_edit { get; set; }
        public int access_level { get; set; } = Users.ACL_SITEADMIN;
        public int mask { get; set; }
        public int basis { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    protected FwSettings() : base()
    {
        table_name = "settings";

        field_status = "";
        is_log_fields_changed = false;
    }

    /// <summary>
    /// Reads a stored site setting, or fw.config(icode) when missing or inherited. Explicit empty values remain empty.
    /// </summary>
    public virtual string read(string icode)
    {
        return read(icode, fw.config(icode).toStr());
    }

    /// <summary>
    /// Reads a site setting as an integer.
    /// </summary>
    public virtual int readi(string icode)
    {
        return read(icode).toInt();
    }

    public virtual string read(string icode, string defaultValue)
    {
        return getValue(icode, defaultValue);
    }

    public virtual bool readBool(string icode)
    {
        return readBool(icode, fw.config(icode).toBool());
    }

    public virtual bool readBool(string icode, bool defaultValue)
    {
        var value = read(icode, defaultValue ? "1" : "0");
        return value.toBool();
    }

    public virtual int readInt(string icode)
    {
        return readInt(icode, fw.config(icode).toInt());
    }

    public virtual int readInt(string icode, int defaultValue)
    {
        var value = read(icode, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return string.IsNullOrEmpty(value) ? defaultValue : value.toInt(defaultValue);
    }

    public virtual long readLong(string icode)
    {
        return readLong(icode, fw.config(icode).toLong());
    }

    public virtual long readLong(string icode, long defaultValue)
    {
        var value = read(icode, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return string.IsNullOrEmpty(value) ? defaultValue : value.toLong(defaultValue);
    }

    /// <summary>
    /// Reads a site setting as a nullable date.
    /// </summary>
    public virtual object? readd(string icode)
    {
        return read(icode).toDateOrNull();
    }

    /// <summary>
    /// Writes a site setting value by icode.
    /// </summary>
    public void write(string icode, string value)
    {
        this.setValue(icode, value);
    }

    // just return first row by icode field
    public override DBRow oneByIcode(string icode)
    {
        return snapshot().TryGetValue(icode, out var row) ? new DBRow(row) : [];
    }

    public string getValue(string icode)
    {
        return getValue(icode, fw.config(icode).toStr());
    }

    public string getValue(string icode, string defaultValue)
    {
        return effective(oneByIcode(icode), defaultValue);
    }

    public void setValue(string icode, string ivalue)
    {
        var item = this.oneByIcode(icode);
        FwDict fields = [];
        if (item.TryGetValue("id", out string? value))
        {
            // exists - update
            fields["ivalue"] = ivalue;
            update(value.toInt(), fields);
        }
        else
        {
            // not exists - add new
            fields["icode"] = icode;
            fields["ivalue"] = ivalue;
            fields["is_user_edit"] = "0"; // all auto-added settings is not user-editable by default
            this.add(fields);
        }
    }

    /// <summary>
    /// Lists categories currently used by settings rows for the admin settings tabs.
    /// </summary>
    public FwList listCategories()
    {
        FwList rows = db.arrayp($@"
            select icat
            from {qTable()}
            group by icat
            order by
                case when icat='' then 0 else 1 end,
                icat", DB.h());

        return rows;
    }

    // check if item exists for a given icode
    public override bool isExists(object uniq_key, int not_id)
    {
        return isExistsByField(uniq_key, not_id, "icode");
    }

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
                result.Add(row["icode"].toStr(), new FwDict(row));

            FwCache.setValue(mapKey, result, expire_seconds: 0);
            return result;
        }
    }

    /// <summary>Invalidates this database's values and dependent menu data. Overrides must call base to clear shared caches.</summary>
    public virtual void invalidate()
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
        if (row.Count == 0)
            return defaultValue;
        if (row["basis"].toInt(BASIS_VALUE) == BASIS_INHERIT)
            return isCredential(row) ? "" : defaultValue;

        var value = row["ivalue"].toStr();
        return isCredential(row) ? unprotect(row["icode"].toStr(), value) : value;
    }

    /// <summary>Reads a stored credential for a trusted runtime consumer. Missing/inherited credentials are unconfigured; configuration is never a fallback.</summary>
    public virtual string readSecret(string code)
    {
        markSensitiveRequest();
        var row = oneByIcode(code);
        if (row.Count == 0)
            return "";

        if (!isCredential(row))
            throw new InvalidOperationException("The setting is not defined as a credential.");

        return effective(row, "");
    }

    public static bool isCredential(FwDict row)
    {
        return row["input"].toInt() == INPUT_CREDENTIAL;
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
            return isCredential(row) ? "Not configured" : "Use default";

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
                ? protect(row["icode"].toStr(), value) : value
        };
    }

    /// <summary>Validates a value for trusted writes and imports without including it in an error.</summary>
    public void validateValue(FwDict row, string value)
    {
        var error = valueError(row, value);
        if (error.Length > 0)
            throw new UserException("Invalid setting value (" + error + ").");
    }

    /// <summary>Returns a form validation code, or empty when the value matches its control metadata.</summary>
    public string valueError(FwDict row, string value)
    {
        var input = row["input"].toInt();
        var metadata = Utils.qh(row["allowed_values"].toStr(), "");

        if (input == INPUT_TEXT && metadata["local_url"].toBool()
            && !Utils.isAppUrl(value, fw.config("ROOT_DOMAIN").toStr()))
            return "INVALID";

        if (input == INPUT_SWITCH && value != "0" && value != "1")
            return "INVALID";

        if (input is INPUT_NUMBER or INPUT_RANGE)
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                return "NUMBER";

            if (decimal.TryParse(metadata["min"].toStr(), CultureInfo.InvariantCulture, out var low) && number < low)
                return "MIN";

            if (decimal.TryParse(metadata["max"].toStr(), CultureInfo.InvariantCulture, out var high) && number > high)
                return "MAX";

            if (decimal.TryParse(metadata["step"].toStr(), CultureInfo.InvariantCulture, out var increment) && increment > 0)
            {
                decimal.TryParse(metadata["min"].toStr(), CultureInfo.InvariantCulture, out var origin);
                if ((number - origin) % increment != 0)
                    return "STEP";
            }
        }

        if (input is INPUT_SELECT or INPUT_RADIO or INPUT_CHECKBOX or INPUT_SELECT_MULTI)
        {
            var values = input is INPUT_CHECKBOX or INPUT_SELECT_MULTI ? value.Split(',') : [value];
            if (values.Any(option => option.Length > 0 && !metadata.ContainsKey(option)))
                return "INVALID";
        }

        return "";
    }

    public override int add(FwDict item)
    {
        lock (storeLock)
        {
            var fields = new FwDict(item);
            var code = fields["icode"].toStr();
            if (code.Length == 0)
                throw new UserException("A setting requires a code.");

            if (!fields.ContainsKey("basis"))
                fields["basis"] = BASIS_VALUE;
            if (!fields.ContainsKey("access_level"))
                fields["access_level"] = Users.ACL_SITEADMIN;
            if (!fields.ContainsKey("mask"))
                fields["mask"] = isCredential(fields) ? MASK_HIDDEN : MASK_NONE;
            if (isCredential(fields))
                fields["access_level"] = Users.ACL_SITEADMIN;

            var prepared = prepareValue(fields, fields["ivalue"].toStr(), fields["basis"].toInt());
            fields["ivalue"] = prepared["ivalue"];

            try
            {
                return base.add(fields);
            }
            finally
            {
                invalidate();
            }
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
            foreach (var entry in fields)
                merged[entry.Key] = entry.Value;

            if (row["icode"].toStr() != merged["icode"].toStr() || isCredential(row) != isCredential(merged))
                throw new UserException("Setting identity and credential classification cannot be changed through value updates.");

            if (isCredential(merged))
            {
                fields["access_level"] = Users.ACL_SITEADMIN;
                if (maskPolicy(merged) == MASK_HIDDEN)
                    fields["mask"] = MASK_HIDDEN;
            }

            if (fields.ContainsKey("ivalue") || fields.ContainsKey("basis"))
            {
                var basis = fields.ContainsKey("basis") ? fields["basis"].toInt() : BASIS_VALUE;
                if (!fields.ContainsKey("ivalue") && basis == BASIS_VALUE)
                    throw new UserException("An explicit value is required.");

                var prepared = prepareValue(merged, fields["ivalue"].toStr(), basis);
                foreach (var entry in prepared)
                    fields[entry.Key] = entry.Value;
            }

            try
            {
                return base.update(id, fields);
            }
            finally
            {
                invalidate();
            }
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
        markSensitiveRequest();
        lock (storeLock)
        {
            var result = new Transfer();
            foreach (FwDict row in db.array(table_name, []))
            {
                var basis = row["basis"].toInt();
                result.settings.Add(new ValueChange
                {
                    icode = row["icode"].toStr(),
                    basis = basis,
                    ivalue = basis == BASIS_INHERIT ? null : effective(row, "")
                });
            }

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>Applies a same-app restore by code; target definitions and their security metadata are never imported.</summary>
    public int importJson(string json)
    {
        markSensitiveRequest();
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MAX_TRANSFER_BYTES)
            throw new UserException("Settings import is too large.");

        Transfer transfer;
        try
        {
            transfer = JsonSerializer.Deserialize<Transfer>(json) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new UserException("Invalid Settings JSON.");
        }

        if (transfer.version != 1 || transfer.settings == null)
            throw new UserException("Unsupported Settings transfer format.");

        return writeBatch(transfer.settings);
    }

    /// <summary>Trusted system operation. Controllers must authorize every row or restrict full restore to Site Admin.</summary>
    public int writeBatch(IReadOnlyCollection<ValueChange> changes)
    {
        markSensitiveRequest();
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

            db.begin();
            try
            {
                foreach (var entry in prepared)
                {
                    entry.fields["upd_time"] = DB.NOW;
                    if (fw.isLogged)
                        entry.fields["upd_users_id"] = fw.userId;

                    db.update(table_name, entry.fields, DB.h("id", entry.id));
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
            finally
            {
                invalidate();
            }

            return prepared.Count;
        }
    }

    /// <summary>Registers the database key store; only session-cookie protection may tolerate missing storage during bootstrap.</summary>
    public static void configureProtection(IServiceCollection services, FwDict settings, bool isRequired = true)
    {
        var definition = (settings["db"] as FwDict)?["main"] as FwDict ?? [];
        var name = settings["DATA_PROTECTION_APPLICATION_NAME"].toStr();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("DATA_PROTECTION_APPLICATION_NAME must be configured before using protected data.");

        var builder = services.AddDataProtection().SetApplicationName(name);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Durable Settings protection requires Windows DPAPI.");
        builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
        services.Configure<KeyManagementOptions>(options =>
            options.XmlRepository = new FwKeysXmlRepository(() => new DB(new FwDict(definition), "main"), isRequired));
    }

    /// <summary>Excludes request bodies and cookies from Sentry events for credential consumers.</summary>
    public static void markSensitiveRequest()
    {
#if isSentry
        Sentry.SentrySdk.ConfigureScope(scope => scope.SetTag("osafw_sensitive_settings", "1"));
#endif
    }

    private IDataProtector protector(string code)
    {
        // The web provider may hold a temporary bootstrap key. Persisted credentials always use a strict provider.
        var identity = fw.db.cacheIdentity + ":" + fw.config("DATA_PROTECTION_APPLICATION_NAME").toStr();
        var settings = new FwDict(fw.config());
        var provider = providers.GetOrAdd(identity, _ => new Lazy<ServiceProvider>(() =>
        {
            var services = new ServiceCollection();
            configureProtection(services, settings);
            return services.BuildServiceProvider();
        })).Value.GetRequiredService<IDataProtectionProvider>();

        return provider.CreateProtector("OSAFW.Settings.v1", code);
    }

    /// <summary>Accepts plaintext only. Empty means an intentional clear and needs no encryption key.</summary>
    public string protect(string code, string value)
    {
        markSensitiveRequest();
        if (value.Length == 0)
            return "";
        try
        {
            return PROTECTED_PREFIX + protector(code).Protect(value);
        }
        catch (Exception)
        {
            // Neither backend exceptions nor supplied values belong in diagnostics.
            throw new CryptographicException("Unable to persist a protected setting. Check durable key storage.");
        }
    }

    /// <summary>Rejects plaintext and unreadable ciphertext; never treats it as missing/default.</summary>
    public string unprotect(string code, string value)
    {
        markSensitiveRequest();
        if (value.Length == 0)
            return "";
        if (!value.StartsWith(PROTECTED_PREFIX, StringComparison.Ordinal))
            throw new CryptographicException("The setting value is not protected.");
        try
        {
            return protector(code).Unprotect(value[PROTECTED_PREFIX.Length..]);
        }
        catch (Exception)
        {
            throw new CryptographicException("A protected setting cannot be read. Restore the matching keys or replace its value.");
        }
    }
}
