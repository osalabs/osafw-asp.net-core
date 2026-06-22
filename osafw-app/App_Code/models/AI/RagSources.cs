using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace osafw;

public class RagSources : FwModel<RagSources.Row>
{
    public const string SOURCE_TYPE_KB_ARTICLE = "kb_article";
    public const string SOURCE_TYPE_KB_ATTACHMENT = "kb_attachment";
    public const string SOURCE_TYPE_SPAGE = "spage";
    public const string SOURCE_TYPE_ASSISTANT_UPLOAD = "assistant_upload";

    public const string INDEX_STATUS_PENDING = "pending";
    public const string INDEX_STATUS_PROCESSING = "processing";
    public const string INDEX_STATUS_INDEXED = "indexed";
    public const string INDEX_STATUS_FAILED = "failed";
    public const string INDEX_STATUS_SKIPPED = "skipped";
    public const string INDEX_STATUS_STALE = "stale";

    public const string PARSER_VERSION = "rag-source-v1";
    public const int MAX_RETRY_ATTEMPTS = 5;
    public const int RETRY_BACKOFF_BASE_MINUTES = 5;
    public const int RETRY_BACKOFF_MULTIPLIER = 3;
    public const int RETRY_BACKOFF_MAX_MINUTES = 240;

    public class Row
    {
        public int id { get; set; }
        public string source_type { get; set; } = string.Empty;
        public string source_key { get; set; } = string.Empty;
        public int fwentities_id { get; set; }
        public int item_id { get; set; }
        public int att_id { get; set; }
        public string iname { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
        public string content_hash { get; set; } = string.Empty;
        public string source_version { get; set; } = string.Empty;
        public string acl_snapshot { get; set; } = string.Empty;
        public string index_status { get; set; } = string.Empty;
        public int index_attempt_no { get; set; }
        public DateTime? queued_at { get; set; }
        public DateTime? next_retry_at { get; set; }
        public DateTime? last_indexed_at { get; set; }
        public string last_error { get; set; } = string.Empty;
        public string metadata_json { get; set; } = string.Empty;
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int? add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int? upd_users_id { get; set; }
    }

    public RagSources()
    {
        table_name = "rag_sources";
        is_log_changes = false;
    }

    /// <summary>
    /// Queues the article body and supported article attachments without calling the embedding provider.
    /// </summary>
    public bool queueKBArticle(int kbId)
    {
        if (!canQueueSources() || kbId <= 0)
            return false;

        var article = fw.model<KBArticles>().one(kbId);
        if (article.Count == 0)
            return false;

        if (article["status"].toInt() != STATUS_ACTIVE)
        {
            deleteByEntity(FwEntities.ICODE_KB, kbId);
            return true;
        }

        queueKBArticleBody(article);
        queueKBArticleAttachments(kbId);
        return true;
    }

    public bool queueKBArticleBody(int kbId)
    {
        if (!canQueueSources() || kbId <= 0)
            return false;

        var article = fw.model<KBArticles>().one(kbId);
        if (article.Count == 0)
            return false;

        if (article["status"].toInt() != STATUS_ACTIVE)
        {
            deleteByEntity(FwEntities.ICODE_KB, kbId);
            return true;
        }

        queueKBArticleBody(article);
        return true;
    }

    private void queueKBArticleBody(FwDict article)
    {
        int kbId = article["id"].toInt();
        int kbEntityId = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_KB);
        string title = article["iname"].toStr();
        string text = KBArticleText(article);
        queueSource(
            SOURCE_TYPE_KB_ARTICLE,
            kbEntityId,
            kbId,
            0,
            title,
            fw.model<KBArticles>().getFullUrl(kbId),
            HashText(text),
            Utils.jsonEncode(DB.h("access_level", article["access_level"].toInt(Users.ACL_MEMBER))),
            Utils.jsonEncode(DB.h("icode", article["icode"].toStr()))
        );
    }

    public bool queueKBArticleAttachments(int kbId)
    {
        if (!canQueueSources() || kbId <= 0)
            return false;

        int kbEntityId = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_KB);
        var atts = fw.model<Att>().listByEntity(FwEntities.ICODE_KB, kbId);
        var embeddingService = new DocumentEmbeddingService(fw);
        HashSet<int> currentSupportedAttIds = [];
        foreach (FwDict att in atts)
        {
            int attId = att["id"].toInt();
            if (attId <= 0)
                continue;

            string ext = att["ext"].toStr();
            if (!embeddingService.CanIndexAttachment(ext, att["fsize"].toLong()))
                continue;

            currentSupportedAttIds.Add(attId);
            queueSource(
                SOURCE_TYPE_KB_ATTACHMENT,
                kbEntityId,
                kbId,
                attId,
                att["fname"].toStr(att["iname"].toStr()),
                fw.model<KBArticles>().getFullUrl(kbId),
                HashText(string.Join("|", attId, att["fname"], att["fsize"], att["upd_time"], att["add_time"])),
                Utils.jsonEncode(DB.h("kb_articles_id", kbId)),
                Utils.jsonEncode(DB.h("ext", ext, "fsize", att["fsize"].toLong()))
            );
        }

        deleteMissingKBAttachmentSources(kbEntityId, kbId, currentSupportedAttIds);
        return true;
    }

    public bool queueSpage(int spageId)
    {
        if (!canQueueSources() || spageId <= 0)
            return false;

        var page = fw.model<Spages>().one(spageId);
        if (page.Count == 0)
            return false;

        if (!fw.model<Spages>().isPublished(page))
        {
            deleteByEntity(FwEntities.ICODE_SPAGE, spageId);
            return true;
        }

        int spageEntityId = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_SPAGE);
        string text = SpageText(page);
        queueSource(
            SOURCE_TYPE_SPAGE,
            spageEntityId,
            spageId,
            0,
            page["iname"].toStr(),
            fw.model<Spages>().getFullUrl(spageId),
            HashText(text),
            string.Empty,
            Utils.jsonEncode(DB.h("template", page["template"].toStr(), "url", page["url"].toStr()))
        );
        return true;
    }

    public bool queueAssistantUpload(int attId, string entityIcode, int itemId)
    {
        if (!canQueueSources() || attId <= 0 || itemId <= 0 || string.IsNullOrWhiteSpace(entityIcode))
            return false;

        var att = fw.model<Att>().one(attId);
        if (att.Count == 0)
            return false;

        var embeddingService = new DocumentEmbeddingService(fw);
        if (!embeddingService.CanIndexAttachment(att["ext"].toStr(), att["fsize"].toLong()))
            return false;

        int entityId = fw.model<FwEntities>().idByIcodeOrAdd(entityIcode);
        queueSource(
            SOURCE_TYPE_ASSISTANT_UPLOAD,
            entityId,
            itemId,
            attId,
            att["fname"].toStr(att["iname"].toStr()),
            "/Att/" + att["icode"].toStr(),
            HashText(string.Join("|", attId, att["fname"], att["fsize"], att["upd_time"], att["add_time"])),
            string.Empty,
            Utils.jsonEncode(DB.h("ext", att["ext"].toStr(), "fsize", att["fsize"].toLong()))
        );
        return true;
    }

    public int queueSource(string sourceType, int fwentitiesId, int itemId, int attId, string title, string url, string contentHash, string aclSnapshot = "", string metadataJson = "")
    {
        if (!canQueueSources() || fwentitiesId <= 0 || itemId <= 0 || string.IsNullOrWhiteSpace(sourceType))
            return 0;

        string key = BuildSourceKey(sourceType, fwentitiesId, itemId, attId);
        var existing = oneBySourceKey(key);
        var fields = DB.h(
            "source_type", sourceType,
            "source_key", key,
            "fwentities_id", fwentitiesId,
            "item_id", itemId,
            "att_id", attId,
            "iname", title ?? string.Empty,
            "url", url ?? string.Empty,
            "content_hash", contentHash ?? string.Empty,
            "source_version", PARSER_VERSION,
            "acl_snapshot", aclSnapshot ?? string.Empty,
            "metadata_json", metadataJson ?? string.Empty,
            "last_error", string.Empty,
            "index_attempt_no", 0,
            "next_retry_at", null,
            "queued_at", DB.NOW,
            "index_status", existing.Count > 0 && existing["content_hash"].toStr() == (contentHash ?? string.Empty)
                ? INDEX_STATUS_PENDING
                : INDEX_STATUS_STALE,
            "status", STATUS_ACTIVE
        );

        int sourceId;
        if (existing.Count == 0)
            sourceId = add(fields);
        else
        {
            update(existing["id"].toInt(), fields);
            sourceId = existing["id"].toInt();
        }

        // The hosted worker processes sources first but waits on the assistant queue signal.
        AssistantRuns.NotifyQueued();
        return sourceId;
    }

    public Row? claimNextPending(string workerId = "")
    {
        if (!isTablesReady())
            return null;

        if (db.dbtype == DB.DBTYPE_SQLSRV)
            return claimNextPendingSqlServer(workerId);

        string sql = db.limit($@"select *
                                  from {qTable()}
                                 where status<>@status_deleted
                                   and (
                                        index_status in (@claim_statuses)
                                        or (
                                            index_status=@failed
                                            and coalesce(index_attempt_no, 0)<@max_attempts
                                            and (next_retry_at is null or next_retry_at<={db.sqlNOW()})
                                        )
                                   )
                              order by case when index_status=@failed then 1 else 0 end, next_retry_at, queued_at, id", 1);
        var row = db.rowp<Row>(sql, DB.h(
            "@status_deleted", STATUS_DELETED,
            "claim_statuses", new List<string> { INDEX_STATUS_PENDING, INDEX_STATUS_STALE },
            "@failed", INDEX_STATUS_FAILED,
            "@max_attempts", MAX_RETRY_ATTEMPTS
        ));
        if (row == null || row.id <= 0)
            return null;

        string updateSql = $@"
update {qTable()}
   set index_status=@processing,
       index_attempt_no=coalesce(index_attempt_no, 0) + 1,
       next_retry_at=null,
       upd_time={db.sqlNOW()}
 where id=@id
   and status<>@status_deleted
   and (
        index_status in (@claim_statuses)
        or (
            index_status=@failed
            and coalesce(index_attempt_no, 0)<@max_attempts
            and (next_retry_at is null or next_retry_at<={db.sqlNOW()})
        )
   )";
        int affected = db.exec(updateSql, DB.h(
            "@id", row.id,
            "@processing", INDEX_STATUS_PROCESSING,
            "@status_deleted", STATUS_DELETED,
            "claim_statuses", new List<string> { INDEX_STATUS_PENDING, INDEX_STATUS_STALE },
            "@failed", INDEX_STATUS_FAILED,
            "@max_attempts", MAX_RETRY_ATTEMPTS
        ));
        if (affected <= 0)
            return null;

        return oneTyped(row.id);
    }

    private Row? claimNextPendingSqlServer(string workerId)
    {
        string sql = $@"
;with next_source as (
    select top (1) *
      from {qTable()} with (rowlock, readpast, updlock)
     where status<>@status_deleted
       and (
            index_status in (@pending, @stale)
            or (
                index_status=@failed
                and coalesce(index_attempt_no, 0)<@max_attempts
                and (next_retry_at is null or next_retry_at<={db.sqlNOW()})
            )
       )
     order by case when index_status=@failed then 1 else 0 end, next_retry_at, queued_at, id
)
update next_source
   set index_status=@processing,
       index_attempt_no=coalesce(index_attempt_no, 0) + 1,
       next_retry_at=null,
       upd_time={db.sqlNOW()}
output inserted.*;";
        return db.rowp<Row>(sql, DB.h(
            "@status_deleted", STATUS_DELETED,
            "@pending", INDEX_STATUS_PENDING,
            "@stale", INDEX_STATUS_STALE,
            "@failed", INDEX_STATUS_FAILED,
            "@processing", INDEX_STATUS_PROCESSING,
            "@max_attempts", MAX_RETRY_ATTEMPTS
        ));
    }

    /// <summary>
    /// Returns abandoned indexing claims to the normal stale queue after a worker crash or shutdown.
    /// </summary>
    public int requeueStaleProcessingSources(int staleAfterMinutes = 30)
    {
        if (!isTablesReady())
            return 0;

        if (db.dbtype == DB.DBTYPE_SQLSRV)
            return requeueStaleProcessingSourcesSqlServer(staleAfterMinutes);

        string sql = $@"
update {qTable()}
   set index_status=@stale,
       queued_at={db.sqlNOW()},
       upd_time={db.sqlNOW()}
 where status<>@status_deleted
   and index_status=@processing
   and (
       upd_time is null
       or upd_time < @cutoff
   )";

        var cutoff = db.Now().AddMinutes(-(staleAfterMinutes <= 0 ? 30 : staleAfterMinutes));
        int affected = db.exec(sql, DB.h(
            "@stale", INDEX_STATUS_STALE,
            "@status_deleted", STATUS_DELETED,
            "@processing", INDEX_STATUS_PROCESSING,
            "@cutoff", cutoff
        ));
        if (affected > 0)
            removeCacheAll();
        return affected;
    }

    private int requeueStaleProcessingSourcesSqlServer(int staleAfterMinutes)
    {
        string sql = $@"
update {qTable()}
   set index_status=@stale,
       queued_at={db.sqlNOW()},
       upd_time={db.sqlNOW()}
 where status<>@status_deleted
   and index_status=@processing
   and (
       upd_time is null
       or upd_time < dateadd(minute, -@stale_after_minutes, {db.sqlNOW()})
   )";

        int affected = db.exec(sql, DB.h(
            "@stale", INDEX_STATUS_STALE,
            "@status_deleted", STATUS_DELETED,
            "@processing", INDEX_STATUS_PROCESSING,
            "@stale_after_minutes", staleAfterMinutes <= 0 ? 30 : staleAfterMinutes
        ));
        if (affected > 0)
            removeCacheAll();
        return affected;
    }

    public Row? oneTyped(int id)
    {
        if (id <= 0)
            return null;
        return db.row<Row>(table_name, DB.h("id", id));
    }

    public DBRow oneBySourceKey(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return [];
        return db.row(table_name, DB.h("source_key", sourceKey, "status", db.opNOT(STATUS_DELETED)));
    }

    public void markIndexed(int id)
    {
        if (id <= 0)
            return;
        update(id, DB.h(
            "index_status", INDEX_STATUS_INDEXED,
            "last_error", string.Empty,
            "index_attempt_no", 0,
            "next_retry_at", null,
            "last_indexed_at", DB.NOW
        ));
    }

    public void markSkipped(int id, string reason)
    {
        if (id <= 0)
            return;
        fw.model<RagChunks>().deleteBySource(id);
        update(id, DB.h(
            "index_status", INDEX_STATUS_SKIPPED,
            "last_error", reason ?? string.Empty,
            "index_attempt_no", 0,
            "next_retry_at", null,
            "last_indexed_at", DB.NOW
        ));
    }

    public void markFailed(int id, string error)
    {
        if (id <= 0)
            return;
        var row = oneTyped(id);
        int attemptNo = Math.Max(1, row?.index_attempt_no ?? 1);
        DateTime? nextRetryAt = attemptNo < MAX_RETRY_ATTEMPTS
            ? db.Now().AddMinutes(retryDelayMinutes(attemptNo))
            : null;
        update(id, DB.h(
            "index_status", INDEX_STATUS_FAILED,
            "last_error", trimError(error),
            "next_retry_at", nextRetryAt,
            "queued_at", DB.NOW
        ));
    }

    public bool requeueSource(int id)
    {
        if (id <= 0 || !isTablesReady())
            return false;

        int affected = db.update(table_name, DB.h(
            "index_status", INDEX_STATUS_STALE,
            "index_attempt_no", 0,
            "next_retry_at", null,
            "queued_at", DB.NOW,
            "upd_time", DB.NOW
        ), DB.h(
            "id", id,
            "status", db.opNOT(STATUS_DELETED)
        ));
        if (affected <= 0)
            return false;

        removeCache(id);
        AssistantRuns.NotifyQueued();
        return true;
    }

    public void deleteByEntity(string entityIcode, int itemId)
    {
        if (!isTablesReady())
            return;

        int entityId = fw.model<FwEntities>().idByIcode(entityIcode);
        if (entityId <= 0 || itemId <= 0)
            return;

        var rows = db.array(table_name, DB.h("fwentities_id", entityId, "item_id", itemId, "status", db.opNOT(STATUS_DELETED)));
        foreach (FwDict row in rows)
            fw.model<RagChunks>().deleteBySource(row["id"].toInt());

        db.del(table_name, DB.h("fwentities_id", entityId, "item_id", itemId));
    }

    private void deleteMissingKBAttachmentSources(int kbEntityId, int kbId, HashSet<int> currentSupportedAttIds)
    {
        var rows = db.array(table_name, DB.h(
            "source_type", SOURCE_TYPE_KB_ATTACHMENT,
            "fwentities_id", kbEntityId,
            "item_id", kbId,
            "status", db.opNOT(STATUS_DELETED)
        ));
        foreach (FwDict row in rows)
        {
            int sourceId = row["id"].toInt();
            int attId = row["att_id"].toInt();
            if (sourceId <= 0 || currentSupportedAttIds.Contains(attId))
                continue;

            fw.model<RagChunks>().deleteBySource(sourceId);
            db.del(table_name, DB.h("id", sourceId));
        }
    }

    public bool isTablesReady()
    {
        try
        {
            var tables = db.tables().Select(static table => table.ToString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return tables.Contains(table_name) && tables.Contains(fw.model<RagChunks>().table_name);
        }
        catch
        {
            return false;
        }
    }

    public int countActive()
    {
        return db.value(table_name, DB.h("status", db.opNOT(STATUS_DELETED)), "count(*)").toInt();
    }

    public int countQueued()
    {
        return db.value(table_name, DB.h(
            "status", db.opNOT(STATUS_DELETED),
            "index_status", db.opIN(new StrList { INDEX_STATUS_PENDING, INDEX_STATUS_STALE })
        ), "count(*)").toInt();
    }

    public int countFailed()
    {
        return db.value(table_name, DB.h(
            "status", db.opNOT(STATUS_DELETED),
            "index_status", INDEX_STATUS_FAILED
        ), "count(*)").toInt();
    }

    public FwList listDiagnostics(int limit = 12)
    {
        string sql = $@"
select rs.*, e.icode as entity_icode
  from {qTable()} rs
  left join {fw.model<FwEntities>().qTable()} e on e.id=rs.fwentities_id
 where rs.status<>@status_deleted
   and rs.index_status in (@diagnostic_statuses)
 order by case rs.index_status
          when @processing then 0
          when @failed then 1
          when @stale then 2
          else 3
        end,
        rs.next_retry_at,
        rs.queued_at desc,
        rs.id desc";
        var rows = db.arrayp(db.limit(sql, Math.Max(1, limit)), DB.h(
            "@status_deleted", STATUS_DELETED,
            "diagnostic_statuses", new List<string> { INDEX_STATUS_PROCESSING, INDEX_STATUS_FAILED, INDEX_STATUS_STALE },
            "@processing", INDEX_STATUS_PROCESSING,
            "@failed", INDEX_STATUS_FAILED,
            "@stale", INDEX_STATUS_STALE
        ));
        foreach (FwDict row in rows)
        {
            string status = row["index_status"].toStr();
            row["status_label"] = status;
            row["can_requeue"] = status == INDEX_STATUS_FAILED || status == INDEX_STATUS_PROCESSING || status == INDEX_STATUS_STALE;
        }
        return rows;
    }

    public static string BuildSourceKey(string sourceType, int fwentitiesId, int itemId, int attId)
    {
        return string.Join(":", sourceType?.Trim().ToLowerInvariant() ?? string.Empty, fwentitiesId, itemId, attId);
    }

    public static string HashText(string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static int retryDelayMinutes(int attemptNo)
    {
        int exponent = Math.Max(0, attemptNo - 1);
        double minutes = RETRY_BACKOFF_BASE_MINUTES * Math.Pow(RETRY_BACKOFF_MULTIPLIER, exponent);
        return Math.Min(RETRY_BACKOFF_MAX_MINUTES, Math.Max(RETRY_BACKOFF_BASE_MINUTES, (int)Math.Round(minutes)));
    }

    public static string KBArticleText(FwDict article)
    {
        if (article == null || article.Count == 0)
            return string.Empty;
        return string.Join("\n\n", new[]
        {
            article["iname"].toStr(),
            article["idesc"].toStr(),
            article["content_markdown"].toStr()
        }).Trim();
    }

    public static string SpageText(FwDict page)
    {
        if (page == null || page.Count == 0)
            return string.Empty;
        return string.Join("\n\n", new[]
        {
            page["iname"].toStr(),
            page["meta_description"].toStr(),
            page["idesc"].toStr(),
            page["idesc_left"].toStr(),
            page["idesc_right"].toStr()
        }).Trim();
    }

    private bool canQueueSources()
    {
        return fw.model<Settings>().readBool("ASSISTANT_ENABLED")
            && fw.model<LLM>().isConfigured()
            && isTablesReady();
    }

    private static string trimError(string error)
    {
        error = error ?? string.Empty;
        return error.Length > 2000 ? error[..2000] : error;
    }
}
