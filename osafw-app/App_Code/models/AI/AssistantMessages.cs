using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw;

public class AssistantMessages : FwModel<AssistantMessages.Row>
{
    public const string ROLE_USER = "user";
    public const string ROLE_ASSISTANT = "assistant";
    public const string ROLE_SYSTEM = "system";

    public const string TYPE_MESSAGE = "message";
    public const string TYPE_RESULT = "result";
    public const string TYPE_CLARIFICATION = "clarification";

    public class Row
    {
        public int id { get; set; }
        public int assistant_threads_id { get; set; }
        public string role { get; set; } = string.Empty;
        public string message_type { get; set; } = string.Empty;
        public string preview_text { get; set; } = string.Empty;
        public string content_markdown { get; set; } = string.Empty;
        public string payload_json { get; set; } = string.Empty;
        public string sources_json { get; set; } = string.Empty;
        public double? confidence { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int? add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int? upd_users_id { get; set; }
    }

    public AssistantMessages()
    {
        table_name = "assistant_messages";
        is_log_changes = false;
    }

    public int addMessage(int threadId, string role, string messageType, string contentMarkdown, string previewText = "", string payloadJson = "", string sourcesJson = "", double? confidence = null, int usersId = 0)
    {
        string normalizedMarkdown = contentMarkdown ?? string.Empty;
        var item = DB.h(
            "assistant_threads_id", threadId,
            "role", role,
            "message_type", messageType,
            "preview_text", buildPreviewText(string.IsNullOrWhiteSpace(previewText) ? normalizedMarkdown : previewText),
            "content_markdown", normalizedMarkdown,
            "payload_json", payloadJson ?? string.Empty,
            "sources_json", sourcesJson ?? string.Empty
        );
        if (confidence.HasValue)
            item["confidence"] = confidence.Value;
        if (usersId > 0)
            item["add_users_id"] = usersId;

        return add(item);
    }

    public Row? oneTyped(int id)
    {
        string sql = db.limit($@"select *
                                  from {qTable()}
                                 where id=@id
                                   and status<>@status_deleted
                              order by id", 1);
        return db.rowp<Row>(sql, DB.h("@id", id, "@status_deleted", STATUS_DELETED));
    }

    public List<Row> listByThread(int threadId, int sinceId = 0)
    {
        string sql = $@"select *
                          from {qTable()}
                         where assistant_threads_id=@assistant_threads_id
                           and status<>@status_deleted";
        var @params = DB.h("@assistant_threads_id", threadId, "@status_deleted", STATUS_DELETED);
        if (sinceId > 0)
        {
            sql += " and id>@since_id";
            @params["@since_id"] = sinceId;
        }

        sql += " order by id";
        return db.arrayp<Row>(sql, @params);
    }

    public Row? latestByThread(int threadId, string role = "")
    {
        string sql = $@"select *
                          from {qTable()}
                         where assistant_threads_id=@assistant_threads_id
                           and status<>@status_deleted";
        var @params = DB.h("@assistant_threads_id", threadId, "@status_deleted", STATUS_DELETED);
        if (!string.IsNullOrWhiteSpace(role))
        {
            sql += " and role=@role";
            @params["@role"] = role;
        }
        sql += " order by id desc";
        return db.rowp<Row>(db.limit(sql, 1), @params);
    }

    public Dictionary<int, string> listFirstUserPreviewByThreadIds(IEnumerable<int> threadIds, int maxLen = 100)
    {
        var ids = threadIds.Where(static id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        string sql = $@"
with first_user_messages as (
    select assistant_threads_id,
           preview_text,
           row_number() over(partition by assistant_threads_id order by id) as rn
      from {qTable()}
     where assistant_threads_id in (@thread_ids)
       and role=@role_user
       and status<>@status_deleted
)
select assistant_threads_id,
       preview_text
  from first_user_messages
 where rn=1";

        var rows = db.arrayp(sql, DB.h(
            "thread_ids", ids,
            "@role_user", ROLE_USER,
            "@status_deleted", STATUS_DELETED
        ));

        Dictionary<int, string> result = [];
        foreach (DBRow row in rows)
        {
            int threadId = row["assistant_threads_id"].toInt();
            if (threadId <= 0)
                continue;

            string text = row["preview_text"].Trim();
            if (text.Length > maxLen)
                text = text[..maxLen];
            result[threadId] = text;
        }

        return result;
    }

    public static string buildPreviewText(string contentMarkdown, int maxLen = 600)
    {
        if (string.IsNullOrWhiteSpace(contentMarkdown))
            return string.Empty;

        string preview = contentMarkdown
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ");

        preview = Regex.Replace(preview, @"\[(.*?)\]\((.*?)\)", "$1");
        preview = Regex.Replace(preview, @"[`*_>#-]+", " ");
        preview = Regex.Replace(preview, @"\s{2,}", " ").Trim();
        if (preview.Length > maxLen)
            preview = preview[..maxLen];

        return preview;
    }
}
