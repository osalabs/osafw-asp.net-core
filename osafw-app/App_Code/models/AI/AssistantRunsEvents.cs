using System;
using System.Collections.Generic;

namespace osafw;

public class AssistantRunsEvents : FwModel<AssistantRunsEvents.Row>
{
    public const string TYPE_STATUS = "status";
    public const string TYPE_PROGRESS = "progress";
    public const string TYPE_TOOL = "tool";
    public const string TYPE_ERROR = "error";
    public const string TYPE_EVIDENCE = "evidence";

    public class Row
    {
        public int id { get; set; }
        public int assistant_runs_id { get; set; }
        public string event_type { get; set; } = string.Empty;
        public string content { get; set; } = string.Empty;
        public string payload_json { get; set; } = string.Empty;
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int? add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int? upd_users_id { get; set; }
    }

    public AssistantRunsEvents()
    {
        table_name = "assistant_runs_events";
        is_log_changes = false;
    }

    public int addEvent(int runId, string eventType, string content, string payloadJson = "")
    {
        return add(DB.h(
            "assistant_runs_id", runId,
            "event_type", eventType,
            "content", content ?? string.Empty,
            "payload_json", payloadJson ?? string.Empty
        ));
    }

    public List<Row> listByRun(int runId, int sinceId = 0)
    {
        string sql = $@"select *
                          from {qTable()}
                         where assistant_runs_id=@assistant_runs_id
                           and status<>@status_deleted";
        var @params = DB.h("@assistant_runs_id", runId, "@status_deleted", STATUS_DELETED);
        if (sinceId > 0)
        {
            sql += " and id>@since_id";
            @params["@since_id"] = sinceId;
        }
        sql += " order by id";
        return db.arrayp<Row>(sql, @params);
    }
}
