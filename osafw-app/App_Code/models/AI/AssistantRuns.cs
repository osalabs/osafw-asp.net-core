using System;
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

        update(row.id, DB.h(
            "status", STATUS_PROCESSING,
            "worker_id", workerId,
            "claimed_at", DB.NOW,
            "started_at", DB.NOW,
            "attempt_no", row.attempt_no + 1
        ));
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
        return db.rowp<Row>(sql, DB.h(
            "@status_queued", STATUS_QUEUED,
            "@status_processing", STATUS_PROCESSING,
            "@worker_id", workerId
        ));
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

    public int requeueStaleProcessingRuns(int staleAfterMinutes = 30)
    {
        if (db.dbtype != DB.DBTYPE_SQLSRV)
            return 0;

        string sql = $@"
update {qTable()}
   set status=@status_queued,
       worker_id='',
       error_message='',
       clarification_json='',
       claimed_at=null,
       started_at=null,
       completed_at=null,
       upd_time={db.sqlNOW()}
 where status=@status_processing
   and claimed_at is not null
   and claimed_at < dateadd(minute, -@stale_after_minutes, {db.sqlNOW()})";

        return db.exec(sql, DB.h(
            "@status_queued", STATUS_QUEUED,
            "@status_processing", STATUS_PROCESSING,
            "@stale_after_minutes", staleAfterMinutes <= 0 ? 30 : staleAfterMinutes
        ));
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
