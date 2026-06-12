using System;
using System.Text.RegularExpressions;

namespace osafw;

public class AssistantMemories : FwModel<AssistantMemories.Row>
{
    public class Row
    {
        public int id { get; set; }
        public int users_id { get; set; }
        public string summary { get; set; } = string.Empty;
        public string terminology_json { get; set; } = string.Empty;
        public string preferences_json { get; set; } = string.Empty;
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

    public int upsertForUser(int usersId, string summary, string terminologyJson = "", string preferencesJson = "", int sourceThreadId = 0)
    {
        summary = SanitizeMemoryText(summary);
        terminologyJson = SanitizeMemoryText(terminologyJson);
        preferencesJson = SanitizeMemoryText(preferencesJson);

        var existing = oneByUser(usersId);
        var fields = DB.h(
            "summary", summary ?? string.Empty,
            "terminology_json", terminologyJson ?? string.Empty,
            "preferences_json", preferencesJson ?? string.Empty,
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
        value = Regex.Replace(value, @"(?i)\b(api[_ -]?key|token|secret|password|pwd)\b\s*[:=]\s*['""]?[^,'""\s}\]]+", "$1: [redacted]");
        value = Regex.Replace(value, @"\b[\w.\-+%]+@[\w.\-]+\.[A-Za-z]{2,}\b", "[redacted-email]");
        value = Regex.Replace(value, @"(?<!\d)(?:\+?1[\s.-]?)?(?:\(?\d{3}\)?[\s.-]?)\d{3}[\s.-]?\d{4}(?!\d)", "[redacted-phone]");
        value = Regex.Replace(value, @"\b(?:\d[ -]*?){13,19}\b", "[redacted-number]");
        value = Regex.Replace(value, @"\b\d{3}-\d{2}-\d{4}\b", "[redacted-id]");
        return value.Trim();
    }
}
