// Scheduled report delivery
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net

using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace osafw;

public class FwScheduledReports : FwModel
{
    private const int MAX_PAYLOAD_BYTES = 32768;
    private const string SOURCE_CUSTOM = "custom";
    private const string SOURCE_HARDCODED = "hardcoded";

    /// <summary>Reads and validates persisted report delivery and input metadata.</summary>
    public FwDict payload(string icode, string json)
    {
        if (FwCron.reportCode(icode).Length == 0 || Encoding.UTF8.GetByteCount(json) > MAX_PAYLOAD_BYTES)
            throw new UserException("REPORT_SCHEDULE_INVALID");

        FwDict? value;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8, AllowDuplicateProperties = false });
            value = Utils.jsonDecodeOrThrow(document.RootElement.GetRawText()) as FwDict;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or OverflowException)
        {
            throw new UserException("REPORT_SCHEDULE_INVALID");
        }

        if (value == null
            || value["source"] is not string source
            || source is not (SOURCE_CUSTOM or SOURCE_HARDCODED)
            || (source == SOURCE_CUSTOM ? !isUserOrReportId(value["fwreports_id"]) : value.ContainsKey("fwreports_id"))
            || !isUserOrReportId(value["run_as_users_id"])
            || value["recipients"] is not string recipients
            || value["format"] is not string format
            || value["params"] is not IList rawParams
            || value["input_context"] is not FwDict context
            || value.Keys.Any(key => key is not ("source" or "fwreports_id" or "run_as_users_id" or "recipients" or "format" or "params" or "input_context" or "sortby" or "sortdir"))
            || (value.ContainsKey("sortby") && value["sortby"] is not string)
            || (value.ContainsKey("sortdir") && value["sortdir"] is not string))
        {
            throw new UserException("REPORT_SCHEDULE_INVALID");
        }

        if (context.Count != 3
            || context["date_format"] is not long dateFormat
            || dateFormat is not (DateUtils.DATE_FORMAT_MDY or DateUtils.DATE_FORMAT_DMY)
            || context["time_format"] is not long timeFormat
            || timeFormat is not (DateUtils.TIME_FORMAT_12 or DateUtils.TIME_FORMAT_24)
            || context["lang"] is not string language || !isValidLanguage(language))
        {
            throw new UserException("REPORT_SCHEDULE_INVALID");
        }

        var parameters = new FwList();
        foreach (var entry in rawParams)
        {
            if (entry is not FwDict row || row.Count != 3
                || row["name"] is not string name || name.Length == 0
                || row["type"] is not string type || type.Length == 0
                || row["value"] is not string)
            {
                throw new UserException("REPORT_SCHEDULE_INVALID");
            }

            parameters.Add(row);
        }

        value["params"] = parameters;
        setDeliveryFields(value, recipients, format);
        return value;
    }

    /// <summary>Updates delivery options without changing the report source or saved filters.</summary>
    public FwDict validateDelivery(FwDict payload, string recipients, string format, int run_as_users_id)
    {
        if (!isActiveSiteAdmin(fw.model<Users>().one(run_as_users_id)))
            throw new UserException("REPORT_RUN_AS");

        var result = new FwDict(payload);
        setDeliveryFields(result, recipients, format);
        result["run_as_users_id"] = run_as_users_id;
        checkPayloadSize(Utils.jsonEncode(result));
        return result;
    }

    private static void setDeliveryFields(FwDict fields, string recipients, string format)
    {
        if (format is not ("pdf" or "html"))
            throw new UserException("REPORT_FORMAT");

        var normalized = FwReportsBase.normalizeRecipients(recipients, true);
        if (normalized.Length == 0)
            throw new UserException("REPORT_RECIPIENTS");

        fields["recipients"] = normalized;
        fields["format"] = format;
    }

    private static bool isUserOrReportId(object? value) => value is long id && id > 0 && id <= int.MaxValue;

    private static bool isActiveSiteAdmin(DBRow user)
    {
        return user.Count > 0 && user["status"].toInt() == STATUS_ACTIVE && user["access_level"].toInt() >= Users.ACL_SITEADMIN;
    }

    private static void checkPayloadSize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MAX_PAYLOAD_BYTES)
            throw new UserException("REPORT_SCHEDULE_SIZE");
    }

    internal static string encodePayload(FwDict payload)
    {
        var json = Utils.jsonEncode(payload);
        checkPayloadSize(json);
        return json;
    }

    private static bool isValidLanguage(string language)
    {
        // ParsePage uses this key to locate its language file.
        return language.Length <= 32 && Regex.IsMatch(language, @"\A[A-Za-z0-9_-]+\z");
    }

    internal FwDict capture(FwReportsBase report, FwDict filters, FwDict item)
    {
        var language = fw.G["lang"].toStr("en");
        if (!isValidLanguage(language))
            throw new UserException("REPORT_SCHEDULE_INVALID");

        var result = new FwDict
        {
            ["source"] = SOURCE_HARDCODED,
            ["params"] = report.captureScheduledFilters(filters, false),
            ["input_context"] = new FwDict
            {
                ["date_format"] = fw.userDateFormat,
                ["time_format"] = fw.userTimeFormat,
                ["lang"] = language
            }
        };

        if (report is FwCustomReport custom)
        {
            result["source"] = SOURCE_CUSTOM;
            result["fwreports_id"] = custom.schedulingRecordId;
        }

        report.saveScheduledSort(result);
        return validateDelivery(result, item["recipients"].toStr(), item["format"].toStr(), fw.userId);
    }

    internal void applyInputContext(FwDict payload)
    {
        var context = (FwDict)payload["input_context"]!;
        foreach (var key in context.Keys)
            fw.G[key] = context[key];
    }

    /// <summary>Prevents a custom report replacement or hardcoded shadow from inheriting an existing delivery schedule.</summary>
    internal static void checkTarget(FwReportsBase report, string code, FwDict payload)
    {
        if (!report.isSchedulingSupported || report.schedulingCode != code)
            throw new UserException("REPORT_SCHEDULE_UNAVAILABLE");

        if (report is FwCustomReport custom)
        {
            if (payload["source"].toStr() != SOURCE_CUSTOM || custom.schedulingRecordId != payload["fwreports_id"].toInt())
                throw new UserException("REPORT_SCHEDULE_UNAVAILABLE");
        }
        else if (payload["source"].toStr() != SOURCE_HARDCODED)
        {
            throw new UserException("REPORT_SCHEDULE_UNAVAILABLE");
        }
    }

    /// <summary>Runs one claimed report job in its own fresh offline FW context.</summary>
    internal FwDict run(FwCron.Row job)
    {
        if (!fw.isOffline)
            throw new FwCronPauseException("REPORT_OFFLINE_REQUIRED");

        FwReportsBase report;
        FwDict saved;
        try
        {
            saved = payload(job.icode, job.payload.toStr());
            var users = fw.model<Users>();
            var ownerId = saved["run_as_users_id"].toInt();
            if (!isActiveSiteAdmin(users.one(ownerId)))
                throw new FwCronPauseException("REPORT_RUN_AS");

            users.reloadSession(ownerId);
            if (!users.isSiteAdmin())
                throw new FwCronPauseException("REPORT_RUN_AS");

            applyInputContext(saved);
            fw.G["timezone"] = job.timezone;
            var code = FwCron.reportCode(job.icode);
            fw.setRoute(new FwRoute
            {
                controller = "AdminReports",
                controller_path = "/Admin/Reports",
                method = "GET",
                action = FW.ACTION_SHOW,
                id = code
            });

            var format = saved["format"].toStr();
            var filters = new FwDict { ["format"] = format };
            var is_hardcoded = saved["source"].toStr() == SOURCE_HARDCODED;
            if (!is_hardcoded && FwReportsBase.isHardcodedReport(code))
                throw new UserException("REPORT_SCHEDULE_UNAVAILABLE");

            report = FwReportsBase.createScheduledInstance(fw, code, filters, is_hardcoded);
            checkTarget(report, code, saved);
            report.applyScheduledFilters(saved, job.last_started_utc);
        }
        catch (AuthException)
        {
            throw new FwCronPauseException("REPORT_UNAUTHORIZED");
        }
        catch (UserException)
        {
            // User exceptions may include underlying SQL, submitted values, or paths.
            throw new FwCronPauseException("REPORT_SCHEDULE_INVALID");
        }

        report.ps["is_scheduled_report"] = true;
        report.getData();

        var recipients = saved["recipients"].toStr();
        var emailFormat = saved["format"].toStr();
        if (!report.sendReportEmail(recipients, emailFormat, true))
            throw new FwCronUncertainException("REPORT_DELIVERY_UNCERTAIN");

        return new FwDict
        {
            ["message"] = "REPORT_SENT",
            ["data"] = new FwDict
            {
                ["row_count"] = report.list_count,
                ["recipient_count"] = recipients.Split(';').Length,
                ["format"] = emailFormat
            }
        };
    }
}
