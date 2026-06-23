using System;
using System.Text.RegularExpressions;

namespace osafw;

public class AssistantMemories : FwModel<AssistantMemories.Row>
{
    public const int MAX_SUMMARY_LENGTH = 2000;

    public class Row
    {
        public int id { get; set; }
        public int users_id { get; set; }
        public string summary { get; set; } = string.Empty;
        public DateTime? last_compacted_at { get; set; }
        public int? source_threads_id { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int? add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int? upd_users_id { get; set; }
    }

    public AssistantMemories()
    {
        table_name = "assistant_memories";
        is_log_changes = false;
    }

    public Row? oneByUser(int usersId)
    {
        string sql = db.limit($@"select *
                                  from {qTable()}
                                 where users_id=@users_id
                                   and status<>@status_deleted
                              order by id", 1);
        return db.rowp<Row>(sql, DB.h("@users_id", usersId, "@status_deleted", STATUS_DELETED));
    }

    public int upsertForUser(int usersId, string summary, int sourceThreadId = 0)
    {
        summary = SanitizeMemoryText(summary);
        if (!IsStorableMemorySummary(summary))
            return 0;

        var existing = oneByUser(usersId);
        var fields = DB.h(
            "summary", summary ?? string.Empty,
            "last_compacted_at", DB.NOW
        );
        if (sourceThreadId > 0)
            fields["source_threads_id"] = sourceThreadId;

        if (existing == null || existing.id == 0)
        {
            fields["users_id"] = usersId;
            return add(fields);
        }

        update(existing.id, fields);
        return existing.id;
    }

    public static string SanitizeMemoryText(string value)
    {
        value = value ?? string.Empty;
        if (value.Length == 0)
            return string.Empty;

        value = Regex.Replace(value, @"sk-[A-Za-z0-9_\-]{12,}", "[redacted-secret]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"(?i)\b(bearer)\s+[A-Za-z0-9._~+/\-]+=*", "$1 [redacted]");
        value = Regex.Replace(value, @"(?i)\b(password|pwd)\s*=\s*[^;,'""\s}\]]+", "$1: [redacted]");
        value = Regex.Replace(value, @"(?i)\b(api[_ -]?key|token|secret|password|pwd)\b\s*[:=]\s*['""]?[^;,'""\s}\]]+", "$1: [redacted]");
        value = Regex.Replace(value, @"\b(?:gh[pousr]_|xox[baprs]-|AKIA|AIza)[A-Za-z0-9_\-]{12,}\b", "[redacted-secret]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"(?<![A-Za-z0-9])[A-Za-z0-9_\-]{32,}(?![A-Za-z0-9])", "[redacted-token]");
        value = Regex.Replace(value, @"\b[\w.\-+%]+@[\w.\-]+\.[A-Za-z]{2,}\b", "[redacted-email]");
        value = Regex.Replace(value, @"(?<!\d)(?:\+?1[\s.-]?)?(?:\(?\d{3}\)?[\s.-]?)\d{3}[\s.-]?\d{4}(?!\d)", "[redacted-phone]");
        value = Regex.Replace(value, @"\b(?:\d[ -]*?){13,19}\b", "[redacted-number]");
        value = Regex.Replace(value, @"\b\d{3}-\d{2}-\d{4}\b", "[redacted-id]");
        value = value.Trim();
        return value.Length <= MAX_SUMMARY_LENGTH ? value : value[..MAX_SUMMARY_LENGTH].Trim();
    }

    public static bool IsStorableMemorySummary(string value)
    {
        value = value ?? string.Empty;
        if (value.Length == 0)
            return false;

        string stripped = Regex.Replace(value, @"(?i)\b(api[_ -]?key|bearer|token|secret|password|pwd)\s*:?\s*\[redacted\]", "");
        stripped = Regex.Replace(stripped, @"\[[^\]]*redacted[^\]]*\]", "", RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, @"[\s\p{P}\p{S}]+", "");
        return stripped.Length >= 8;
    }
}
