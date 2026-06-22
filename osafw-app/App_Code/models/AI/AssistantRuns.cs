using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

public class AssistantRuns : FwModel<AssistantRuns.Row>
{
    private static readonly SemaphoreSlim QueueSignal = new(0);
    private static int hasPendingSignal;

    public const int STATUS_QUEUED = 0;
    public const int STATUS_CANCELLED = 10;
    public const int STATUS_PROCESSING = 20;
    public const int STATUS_COMPLETED = 30;
    public const int STATUS_FAILED = 40;
    public const int STATUS_WAITING_FOR_USER = 50;
    public const int DEFAULT_RUN_TIMEOUT_SECONDS = 120;
    public const string TIMEOUT_ERROR_MESSAGE = "Assistant response timed out. Try again.";

    public class Row
    {
        public int id { get; set; }
        public int assistant_threads_id { get; set; }
        public int assistant_messages_id { get; set; }
        public int? result_messages_id { get; set; }
        public int? activity_logs_id { get; set; }
        public string worker_id { get; set; } = string.Empty;
        public string error_message { get; set; } = string.Empty;
        public string clarification_json { get; set; } = string.Empty;
        public int attempt_no { get; set; }
        public DateTime? claimed_at { get; set; }
        public DateTime? started_at { get; set; }
        public DateTime? completed_at { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int? add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int? upd_users_id { get; set; }
    }

    public AssistantRuns()
    {
        table_name = "assistant_runs";
        is_log_changes = false;
    }

    public int queueRun(int threadId, int messageId)
    {
        int runId = add(DB.h(
            "assistant_threads_id", threadId,
            "assistant_messages_id", messageId,
            "status", STATUS_QUEUED
        ));
        NotifyQueued();
        return runId;
    }

    public static void NotifyQueued()
    {
        if (Interlocked.Exchange(ref hasPendingSignal, 1) == 0)
            QueueSignal.Release();
    }

    public static async Task<bool> WaitForQueuedRunAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref hasPendingSignal, 0) == 1)
            return true;

        bool signaled = await QueueSignal.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (signaled)
            Interlocked.Exchange(ref hasPendingSignal, 0);
        return signaled;
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

    public Row? claimNextQueued(string workerId)
    {
        if (db.dbtype == DB.DBTYPE_SQLSRV)
            return claimNextQueuedSqlServer(workerId);

        var row = db.rowp<Row>(
            db.limit($@"select *
                         from {qTable()}
                        where status=@status_queued
                     order by id", 1),
            DB.h("@status_queued", STATUS_QUEUED));
        if (row == null || row.id <= 0)
            return null;

        int affected = db.update(table_name, DB.h(
            "status", STATUS_PROCESSING,
            "worker_id", workerId,
            "claimed_at", DB.NOW,
            "started_at", DB.NOW,
            "attempt_no", row.attempt_no + 1
        ), DB.h(
            "id", row.id,
            "status", STATUS_QUEUED
        ));
        if (affected <= 0)
            return null;

        removeCache(row.id);
        return oneTyped(row.id);
    }

    private Row? claimNextQueuedSqlServer(string workerId)
    {
        string sql = $@"
;with next_run as (
    select top (1) *
      from {qTable()} with (rowlock, readpast, updlock)
     where status=@status_queued
     order by id
)
update next_run
   set status=@status_processing,
       worker_id=@worker_id,
       claimed_at={db.sqlNOW()},
       started_at=coalesce(started_at, {db.sqlNOW()}),
       attempt_no=coalesce(attempt_no, 0) + 1,
       upd_time={db.sqlNOW()}
output inserted.*;";
        var row = db.rowp<Row>(sql, DB.h(
            "@status_queued", STATUS_QUEUED,
            "@status_processing", STATUS_PROCESSING,
            "@worker_id", workerId
        ));
        if (row != null && row.id > 0)
            removeCache(row.id);
        return row;
    }

    public void markCompleted(int id, int resultMessageId = 0, int activityLogsId = 0)
    {
        var fields = DB.h(
            "status", STATUS_COMPLETED,
            "completed_at", DB.NOW,
            "error_message", "",
            "clarification_json", ""
        );
        if (resultMessageId > 0)
            fields["result_messages_id"] = resultMessageId;
        if (activityLogsId > 0)
            fields["activity_logs_id"] = activityLogsId;
        update(id, fields);
    }

    public void markFailed(int id, string errorMessage)
    {
        update(id, DB.h(
            "status", STATUS_FAILED,
            "completed_at", DB.NOW,
            "error_message", string.IsNullOrWhiteSpace(errorMessage) ? "Assistant run failed." : errorMessage
        ));
    }

    public void markWaitingForUser(int id, string clarificationJson)
    {
        update(id, DB.h(
            "status", STATUS_WAITING_FOR_USER,
            "completed_at", DB.NOW,
            "clarification_json", clarificationJson ?? string.Empty,
            "error_message", ""
        ));
    }

    public void markCancelled(int id, string errorMessage = "")
    {
        update(id, DB.h(
            "status", STATUS_CANCELLED,
            "completed_at", DB.NOW,
            "error_message", errorMessage ?? string.Empty
        ));
    }

    public Row? latestByThread(int threadId)
    {
        string sql = $@"select *
                          from {qTable()}
                         where assistant_threads_id=@assistant_threads_id
                           and status<>@status_deleted
                      order by id desc";
        return db.rowp<Row>(db.limit(sql, 1), DB.h("@assistant_threads_id", threadId, "@status_deleted", STATUS_DELETED));
    }

    public Row? activeByThread(int threadId)
    {
        string sql = $@"select *
                          from {qTable()}
                         where assistant_threads_id=@assistant_threads_id
                           and status in (@status_queued, @status_processing, @status_waiting)
                      order by id desc";
        return db.rowp<Row>(db.limit(sql, 1), DB.h(
            "@assistant_threads_id", threadId,
            "@status_queued", STATUS_QUEUED,
            "@status_processing", STATUS_PROCESSING,
            "@status_waiting", STATUS_WAITING_FOR_USER
        ));
    }

    public Row? queuedOrProcessingByThread(int threadId)
    {
        string sql = $@"select *
                          from {qTable()}
                         where assistant_threads_id=@assistant_threads_id
                           and status in (@status_queued, @status_processing)
                      order by id desc";
        return db.rowp<Row>(db.limit(sql, 1), DB.h(
            "@assistant_threads_id", threadId,
            "@status_queued", STATUS_QUEUED,
            "@status_processing", STATUS_PROCESSING
        ));
    }

    public int idByResultMessage(int messageId)
    {
        if (messageId <= 0)
            return 0;

        string sql = db.limit($@"select id
                                  from {qTable()}
                                 where result_messages_id=@result_messages_id
                                   and status<>@status_deleted
                              order by id desc", 1);
        return db.valuep(sql, DB.h("@result_messages_id", messageId, "@status_deleted", STATUS_DELETED)).toInt();
    }

    public Dictionary<int, int> listResultRunIdsByMessageIds(IEnumerable<int> messageIds)
    {
        var ids = messageIds.Where(static id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        string sql = $@"select id, result_messages_id
                          from {qTable()}
                         where result_messages_id in (@message_ids)
                           and status<>@status_deleted
                      order by id";
        var rows = db.arrayp(sql, DB.h(
            "message_ids", ids,
            "@status_deleted", STATUS_DELETED
        ));

        Dictionary<int, int> result = [];
        foreach (DBRow row in rows)
        {
            int messageId = row["result_messages_id"].toInt();
            int runId = row["id"].toInt();
            if (messageId > 0 && runId > 0)
                result[messageId] = runId;
        }
        return result;
    }

    public int failTimedOutActiveRuns(int timeoutSeconds = DEFAULT_RUN_TIMEOUT_SECONDS)
    {
        int effectiveTimeout = Math.Clamp(timeoutSeconds <= 0 ? DEFAULT_RUN_TIMEOUT_SECONDS : timeoutSeconds, 30, 1800);
        DateTime cutoff = db.Now().AddSeconds(-effectiveTimeout);
        string sql = $@"
select *
  from {qTable()}
 where status in (@active_statuses)
   and (
        (status=@status_queued and add_time<@cutoff)
        or (
            status=@status_processing
            and coalesce(claimed_at, started_at, add_time)<@cutoff
        )
   )
 order by id";
        var rows = db.arrayp<Row>(sql, DB.h(
            "active_statuses", new List<int> { STATUS_QUEUED, STATUS_PROCESSING },
            "@status_queued", STATUS_QUEUED,
            "@status_processing", STATUS_PROCESSING,
            "@cutoff", cutoff
        ));

        int affected = 0;
        foreach (var row in rows)
        {
            string updateSql = $@"
update {qTable()}
   set status=@status_failed,
       completed_at={db.sqlNOW()},
       error_message=@timeout_error,
       upd_time={db.sqlNOW()}
 where id=@id
   and status in (@active_statuses)
   and (
        (status=@status_queued and add_time<@cutoff)
        or (
            status=@status_processing
            and coalesce(claimed_at, started_at, add_time)<@cutoff
        )
   )";
            int rowAffected = db.exec(updateSql, DB.h(
                "@id", row.id,
                "@status_failed", STATUS_FAILED,
                "@timeout_error", TIMEOUT_ERROR_MESSAGE,
                "active_statuses", new List<int> { STATUS_QUEUED, STATUS_PROCESSING },
                "@status_queued", STATUS_QUEUED,
                "@status_processing", STATUS_PROCESSING,
                "@cutoff", cutoff
            ));
            if (rowAffected <= 0)
                continue;

            affected++;
            removeCache(row.id);
            fw.model<AssistantRunsEvents>().addEvent(row.id, AssistantRunsEvents.TYPE_ERROR, TIMEOUT_ERROR_MESSAGE);
            var latest = latestByThread(row.assistant_threads_id);
            if (latest?.id == row.id)
                fw.model<AssistantThreads>().updateLastRunStatus(row.assistant_threads_id, STATUS_FAILED);
        }

        if (affected > 0)
            removeCacheAll();
        return affected;
    }

    public FwList listDiagnostics(int timeoutSeconds = DEFAULT_RUN_TIMEOUT_SECONDS, int limit = 12)
    {
        int effectiveTimeout = Math.Clamp(timeoutSeconds <= 0 ? DEFAULT_RUN_TIMEOUT_SECONDS : timeoutSeconds, 30, 1800);
        DateTime cutoff = db.Now().AddSeconds(-effectiveTimeout);
        string sql = $@"
select r.*, t.iname as thread_name
  from {qTable()} r
  left join {fw.model<AssistantThreads>().qTable()} t on t.id=r.assistant_threads_id
 where r.status in (@diagnostic_statuses)
 order by case when r.status in (@active_statuses) then 0 else 1 end,
          r.id desc";
        var rows = db.arrayp(db.limit(sql, Math.Max(1, limit)), DB.h(
            "diagnostic_statuses", new List<int> { STATUS_QUEUED, STATUS_PROCESSING, STATUS_FAILED },
            "active_statuses", new List<int> { STATUS_QUEUED, STATUS_PROCESSING }
        ));
        foreach (FwDict row in rows)
        {
            int status = row["status"].toInt();
            DateTime? started = row["claimed_at"].toDateOrNull();
            if (!started.HasValue)
                started = row["started_at"].toDateOrNull();
            if (!started.HasValue)
                started = row["add_time"].toDateOrNull();

            row["status_code"] = StatusToCode(status);
            row["is_timed_out"] = (status == STATUS_QUEUED || status == STATUS_PROCESSING)
                && started.HasValue
                && started.Value < cutoff;
        }
        return rows;
    }

    public static string StatusToCode(int? status)
    {
        return status switch
        {
            STATUS_QUEUED => "queued",
            STATUS_CANCELLED => "cancelled",
            STATUS_PROCESSING => "processing",
            STATUS_COMPLETED => "completed",
            STATUS_FAILED => "failed",
            STATUS_WAITING_FOR_USER => "waiting_for_user",
            _ => string.Empty,
        };
    }
}
