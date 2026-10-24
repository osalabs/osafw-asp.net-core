using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace osafw;

/// <summary>
/// Explicit one-time migration of known legacy appsettings values into the Settings store.
/// This command never installs a runtime configuration fallback.
/// </summary>
internal static class FwSettingsMigration
{
    internal const int EXIT_SUCCESS = 0;
    internal const int EXIT_ERROR = 1;
    internal const int EXIT_USAGE = 2;

    private const string COMMAND = "settings-migrate";
    private const string OVERRIDE_OPTION = "--legacy-override";
    private static readonly HashSet<string> knownCodes = new(Settings.DEFAULT_VALUES.Keys, StringComparer.Ordinal);

    internal static bool isCommand(string[] args)
    {
        return args.Length > 0 && string.Equals(args[0], COMMAND, StringComparison.OrdinalIgnoreCase);
    }

    internal static int run(
        string[] args,
        IConfiguration configuration,
        TextWriter? output = null,
        TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        if (!tryParse(args, out var sourceFile, out var legacyOverride))
        {
            error.WriteLine("Error: use settings-migrate <legacy-json-file> [--legacy-override <name>].");
            return EXIT_USAGE;
        }

        if (string.IsNullOrWhiteSpace(configuration["appSettings:DATA_PROTECTION_APPLICATION_NAME"]))
        {
            error.WriteLine("Error: DATA_PROTECTION_APPLICATION_NAME must be explicitly configured before migration.");
            return EXIT_ERROR;
        }

        string json;
        try
        {
            var info = new FileInfo(sourceFile);
            if (!info.Exists || info.Length > Settings.MAX_TRANSFER_BYTES)
                throw new IOException();
            json = File.ReadAllText(sourceFile, new UTF8Encoding(false, true));
        }
        catch (Exception)
        {
            error.WriteLine("Error: the legacy Settings file could not be read.");
            return EXIT_ERROR;
        }

        FW? fw = null;
        try
        {
            fw = new FW(null, configuration);
            var count = migrate(fw, json, legacyOverride);
            output.WriteLine(count);
            return EXIT_SUCCESS;
        }
        catch (Exception)
        {
            error.WriteLine("Error: Settings migration failed. Verify the schema, durable keys, and target configuration.");
            return EXIT_ERROR;
        }
        finally
        {
            if (fw != null)
            {
                try { fw.endRequest(); }
                catch { }
                try { fw.Dispose(); }
                catch { }
            }
        }
    }

    /// <summary>
    /// Migrates known scalar values through the Settings batch-write boundary. Existing explicit
    /// values win, except plaintext credentials are converted and legacy test_email fallback is
    /// materialized when its existing database value is empty.
    /// </summary>
    internal static int migrate(FW fw, string json, string? legacyOverride = null)
    {
        ArgumentNullException.ThrowIfNull(fw);
        if (Encoding.UTF8.GetByteCount(json) > Settings.MAX_TRANSFER_BYTES)
            throw new UserException("Legacy Settings input is too large.");

        Dictionary<string, string> legacyValues;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            legacyValues = readLegacyValues(document.RootElement, legacyOverride);
        }
        catch (JsonException)
        {
            throw new UserException("Legacy Settings input is invalid.");
        }

        var model = fw.model<Settings>();
        model.is_log_changes = false;
        var rows = fw.db.array(model.table_name, [])
            .Select(row => row.toFwDict())
            .ToDictionary(row => row["icode"].toStr(), StringComparer.Ordinal);
        var changes = new Dictionary<string, Settings.ValueChange>(StringComparer.Ordinal);

        foreach (var row in rows.Values)
        {
            if (!row.ContainsKey("basis") || !row.ContainsKey("access_level") || !row.ContainsKey("mask"))
                throw new UserException("Settings schema upgrade is required.");

            var code = row["icode"].toStr();
            var currentValue = row["ivalue"].toStr();
            var isCredential = Settings.isCredential(row);
            if (isCredential && currentValue.Length > 0
                && !currentValue.StartsWith(FwSettingsProtection.PREFIX, StringComparison.Ordinal))
            {
                changes[code] = new Settings.ValueChange
                {
                    icode = code,
                    basis = Settings.BASIS_VALUE,
                    ivalue = currentValue
                };
                continue;
            }

            if (!knownCodes.Contains(code))
                continue;

            if (!legacyValues.TryGetValue(code, out var legacyValue))
                continue;

            var basis = row["basis"].toInt();
            if (basis == Settings.BASIS_INHERIT
                || code == Settings.ICODE_TEST_EMAIL && currentValue.Length == 0 && legacyValue.Length > 0)
            {
                changes[code] = new Settings.ValueChange
                {
                    icode = code,
                    basis = Settings.BASIS_VALUE,
                    ivalue = legacyValue
                };
            }
        }

        inferAwsCredentialSource(rows, changes);

        if (changes.Count == 0)
            return 0;

        return model.writeBatch(changes.Values.ToList());
    }

    private static void inferAwsCredentialSource(
        Dictionary<string, FwDict> rows,
        Dictionary<string, Settings.ValueChange> changes)
    {
        var isAwsCredentialMigrated = changes.ContainsKey("AWSAccessKey") || changes.ContainsKey("AWSSecretKey");
        if (!isAwsCredentialMigrated)
            return;

        var hasAccessKey = resultingValueIsNonEmpty("AWSAccessKey", rows, changes);
        var hasSecretKey = resultingValueIsNonEmpty("AWSSecretKey", rows, changes);
        if (!hasAccessKey || !hasSecretKey)
            return;

        if (!rows.TryGetValue("AWS_CREDENTIAL_SOURCE", out var sourceRow))
            throw new UserException("Settings schema upgrade is required.");
        if (sourceRow["basis"].toInt() != Settings.BASIS_INHERIT)
            return;

        changes["AWS_CREDENTIAL_SOURCE"] = new Settings.ValueChange
        {
            icode = "AWS_CREDENTIAL_SOURCE",
            basis = Settings.BASIS_VALUE,
            ivalue = "static"
        };
    }

    private static bool resultingValueIsNonEmpty(
        string code,
        Dictionary<string, FwDict> rows,
        Dictionary<string, Settings.ValueChange> changes)
    {
        if (changes.TryGetValue(code, out var change))
            return change.basis == Settings.BASIS_VALUE && !string.IsNullOrEmpty(change.ivalue);
        return rows.TryGetValue(code, out var row)
            && row["basis"].toInt() == Settings.BASIS_VALUE
            && row["ivalue"].toStr().Length > 0;
    }

    private static Dictionary<string, string> readLegacyValues(JsonElement root, string? legacyOverride)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException();

        var appSettings = root.TryGetProperty("appSettings", out var section) ? section : root;
        if (appSettings.ValueKind != JsonValueKind.Object)
            throw new JsonException();

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        flattenKnownValues(appSettings, "", result);

        if (!string.IsNullOrEmpty(legacyOverride))
        {
            if (!appSettings.TryGetProperty("override", out var overrides)
                || overrides.ValueKind != JsonValueKind.Object
                || !overrides.TryGetProperty(legacyOverride, out var selected)
                || selected.ValueKind != JsonValueKind.Object)
                throw new UserException("The requested legacy override was not found.");

            flattenKnownValues(selected, "", result);
        }

        return result;
    }

    private static void flattenKnownValues(
        JsonElement element,
        string prefix,
        Dictionary<string, string> values)
    {
        foreach (var property in element.EnumerateObject())
        {
            var code = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                flattenKnownValues(property.Value, code, values);
                continue;
            }

            if (!knownCodes.Contains(code) || property.Value.ValueKind == JsonValueKind.Null)
                continue;

            values[code] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? "",
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                JsonValueKind.Number => property.Value.GetRawText(),
                _ => throw new JsonException()
            };
        }
    }

    private static bool tryParse(string[] args, out string sourceFile, out string? legacyOverride)
    {
        sourceFile = "";
        legacyOverride = null;

        if (!isCommand(args) || args.Length < 2 || string.IsNullOrWhiteSpace(args[1])
            || args[1].StartsWith("--", StringComparison.Ordinal))
            return false;

        sourceFile = args[1];
        if (args.Length == 2)
            return true;
        if (args.Length != 4 || !string.Equals(args[2], OVERRIDE_OPTION, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(args[3]) || args[3].StartsWith("--", StringComparison.Ordinal))
            return false;

        legacyOverride = args[3].Trim();
        return true;
    }
}
