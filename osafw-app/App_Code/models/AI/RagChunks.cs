using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

public class RagChunks : FwModel<RagChunks.Row>
{
    public const string VECTOR_MODE_AUTO = "auto";
    public const string VECTOR_MODE_JSON = "json";
    public const string VECTOR_MODE_NATIVE = "native";
    public const int DEFAULT_EMBEDDING_DIMENSION = 1536;

    public class Row
    {
        public int id { get; set; }
        public int rag_sources_id { get; set; }
        public int fwentities_id { get; set; }
        public int item_id { get; set; }
        public int att_id { get; set; }
        public string source_type { get; set; } = string.Empty;
        public string source_title { get; set; } = string.Empty;
        public string source_url { get; set; } = string.Empty;
        public int chunk_index { get; set; }
        public string iname { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public int page { get; set; }
        public string section { get; set; } = string.Empty;
        public string embedding_json { get; set; } = string.Empty;
        public double embedding_norm { get; set; }
        public int embedding_dim { get; set; }
        public string embedding_model { get; set; } = string.Empty;
        public string vector_backend { get; set; } = string.Empty;
        public string metadata_json { get; set; } = string.Empty;
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int? add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int? upd_users_id { get; set; }
    }

    private bool? nativeVectorAvailable;
    private bool? nativeVectorColumnAvailable;

    public RagChunks()
    {
        table_name = "rag_chunks";
        is_log_changes = false;
    }

    public void addEmbedding(ChunkEmbedding record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Embedding == null || record.Embedding.Count == 0)
            throw new ArgumentException("Embedding vector is required.", nameof(record));
        if (record.RagSourcesId <= 0)
            throw new ArgumentException("RagSourcesId is required for embedding records.", nameof(record));
        if (record.ItemId <= 0)
            throw new ArgumentException("ItemId is required for embedding records.", nameof(record));

        int fwentitiesId = record.FwEntitiesId > 0
            ? record.FwEntitiesId
            : fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_ATT);
        string embeddingModel = string.IsNullOrWhiteSpace(record.EmbeddingModel)
            ? LLM.MODEL_TEXT_EMBEDDING_3_SMALL
            : record.EmbeddingModel.Trim();
        if (string.IsNullOrWhiteSpace(embeddingModel))
            embeddingModel = LLM.MODEL_TEXT_EMBEDDING_3_SMALL;

        string embeddingJson = JsonSerializer.Serialize(record.Embedding);
        int id = add(DB.h(
            "rag_sources_id", record.RagSourcesId,
            "fwentities_id", fwentitiesId,
            "item_id", record.ItemId,
            "att_id", record.AttId,
            "source_type", record.SourceType ?? string.Empty,
            "source_title", record.SourceTitle ?? string.Empty,
            "source_url", record.SourceUrl ?? string.Empty,
            "chunk_index", record.ChunkIndex,
            "iname", record.Filename ?? string.Empty,
            "page", record.Page,
            "section", record.Section ?? string.Empty,
            "idesc", record.Text ?? string.Empty,
            "embedding_json", embeddingJson,
            "embedding_norm", vectorNorm(record.Embedding),
            "embedding_dim", record.Embedding.Count,
            "embedding_model", embeddingModel,
            "vector_backend", resolveVectorBackend(record.Embedding.Count),
            "metadata_json", record.MetadataJson ?? string.Empty,
            "status", STATUS_ACTIVE
        ));

        updateNativeVectorColumn(id, embeddingJson, record.Embedding.Count);
    }

    public string adminListViewSql()
    {
        return $@"(select d.id, d.rag_sources_id, d.fwentities_id, d.item_id, d.att_id, d.source_type, d.source_title, d.source_url, d.chunk_index, d.iname, d.idesc, d.page, d.section, d.embedding_dim, d.embedding_model, d.vector_backend, d.metadata_json, d.status, d.add_time, d.upd_time, rs.index_status, rs.last_error, rs.last_indexed_at, rs.content_hash, e.icode as entity_icode
                    from {qTable()} d
               left join {fw.model<RagSources>().qTable()} rs on rs.id=d.rag_sources_id
               left join {fw.model<FwEntities>().qTable()} e on e.id=d.fwentities_id) t";
    }

    public async Task<List<ChunkSearchResult>> listByQueryAsync(string query, int limit = 3, CancellationToken cancellationToken = default)
    {
        return await listByQueryFilteredAsync(query, limit, null, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<FwList> listAssistantSearchResultsAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        HashSet<int> entityIds = [];
        int kbEntityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_KB);
        if (kbEntityId > 0)
            entityIds.Add(kbEntityId);
        int spageEntityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_SPAGE);
        if (spageEntityId > 0)
            entityIds.Add(spageEntityId);
        if (entityIds.Count == 0)
            return [];

        var results = await listByQueryFilteredAsync(query, limit, entityIds, null, cancellationToken).ConfigureAwait(false);
        traceRetrieval(query, "knowledge_base", results);
        return toAssistantResults(results);
    }

    public async Task<FwList> listAssistantThreadSearchResultsAsync(string query, IEnumerable<int> messageIds, int limit = 5, CancellationToken cancellationToken = default)
    {
        var ids = messageIds.Where(static id => id > 0).Distinct().ToHashSet();
        if (ids.Count == 0)
            return [];

        int messageEntityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_ASSISTANT_MESSAGE);
        if (messageEntityId <= 0)
            return [];

        var results = await listByQueryFilteredAsync(query, limit, [messageEntityId], ids, cancellationToken).ConfigureAwait(false);
        traceRetrieval(query, "thread_files", results);
        return toAssistantResults(results);
    }

    private async Task<List<ChunkSearchResult>> listByQueryFilteredAsync(string query, int limit, HashSet<int>? allowedEntityIds, HashSet<int>? allowedItemIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var vectorResults = new List<ChunkSearchResult>();
        var embeddingModel = LLM.MODEL_TEXT_EMBEDDING_3_SMALL;
        try
        {
            var queryEmbedding = await fw.model<LLM>().embeddingForTextAsync(query, embeddingModel, cancellationToken).ConfigureAwait(false);
            vectorResults = listByEmbedding(queryEmbedding, embeddingModel, Math.Max(limit * 3, limit), allowedEntityIds, allowedItemIds);
        }
        catch (Exception ex)
        {
            fw.logger(LogLevel.WARN, "RAG vector retrieval failed; using keyword retrieval only: ", ex.Message);
        }

        var keywordResults = listByKeyword(query, Math.Max(limit * 3, limit), allowedEntityIds, allowedItemIds);
        return mergeHybridResults(vectorResults, keywordResults, limit);
    }

    private List<ChunkSearchResult> listByEmbedding(List<float> queryEmbedding, string embeddingModel, int limit, HashSet<int>? allowedEntityIds, HashSet<int>? allowedItemIds)
    {
        if (queryEmbedding == null || queryEmbedding.Count == 0)
            return [];

        string backend = resolveVectorBackend(queryEmbedding.Count);
        if (backend == VECTOR_MODE_NATIVE)
        {
            try
            {
                return mapSearchRows(listByNativeVectorQuery(queryEmbedding, embeddingModel, limit, allowedEntityIds, allowedItemIds), "vector");
            }
            catch (Exception ex)
            {
                fw.logger(LogLevel.WARN, "Native vector query failed; falling back to JSON scoring:", ex.Message);
            }
        }

        return mapSearchRows(listByJsonQuery(JsonSerializer.Serialize(queryEmbedding), vectorNorm(queryEmbedding), queryEmbedding.Count, embeddingModel, limit, allowedEntityIds, allowedItemIds), "vector");
    }

    public DBList listByJsonQuery(string qJson, double qNorm, int dimension, string embeddingModel, int limit, HashSet<int>? allowedEntityIds = null, HashSet<int>? allowedItemIds = null)
    {
        var sql = db.dbtype switch
        {
            DB.DBTYPE_MYSQL => buildMySqlJsonQuerySql(limit),
            DB.DBTYPE_SQLITE => buildSqliteJsonQuerySql(limit),
            _ => buildSqlServerJsonQuerySql(limit),
        };

        sql = addSearchFilters(sql, allowedEntityIds, allowedItemIds);
        var @params = buildSearchParams(qJson, qNorm, dimension, embeddingModel, limit, allowedEntityIds, allowedItemIds);
        return db.arrayp(sql, @params);
    }

    private DBList listByNativeVectorQuery(List<float> queryEmbedding, string embeddingModel, int limit, HashSet<int>? allowedEntityIds, HashSet<int>? allowedItemIds)
    {
        string qJson = JsonSerializer.Serialize(queryEmbedding);
        int dimension = queryEmbedding.Count;
        string distance = $"VECTOR_DISTANCE('cosine', CAST(@JsonQueryVector AS vector({dimension})), d.embedding_vector)";
        string sql = $@"select d.id,
                               d.rag_sources_id,
                               d.fwentities_id,
                               d.item_id,
                               d.att_id,
                               d.source_type,
                               d.source_title,
                               d.source_url,
                               d.iname,
                               d.page,
                               d.section,
                               d.idesc,
                               (1.0 - {distance}) as CosineSim
                          from {qTable()} d
                         where d.status=@status_active
                           and d.embedding_dim=@EmbeddingDim
                           and d.embedding_model=@EmbeddingModel
                           and d.embedding_vector is not null
                      ##FILTERS##
                      order by {distance}";
        sql = addSearchFilters(sql, allowedEntityIds, allowedItemIds);
        sql = db.limit(sql, Math.Max(1, limit));
        return db.arrayp(sql, buildSearchParams(qJson, vectorNorm(queryEmbedding), dimension, embeddingModel, limit, allowedEntityIds, allowedItemIds));
    }

    private List<ChunkSearchResult> listByKeyword(string query, int limit, HashSet<int>? allowedEntityIds, HashSet<int>? allowedItemIds)
    {
        string trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return [];

        string sql = buildKeywordQuerySql(limit);
        var @params = buildKeywordParams(trimmed, limit, allowedEntityIds, allowedItemIds);
        sql = sql.Replace("##TERM_FILTERS##", @params["__term_filters"].toStr());
        @params.Remove("__term_filters");
        sql = addSearchFilters(sql, allowedEntityIds, allowedItemIds);
        return mapSearchRows(db.arrayp(sql, @params), "keyword");
    }

    private string resolveVectorBackend(int dimension)
    {
        string configured = fw.model<Settings>().read("ASSISTANT_VECTOR_MODE", VECTOR_MODE_AUTO).Trim().ToLowerInvariant();
        if (configured == VECTOR_MODE_JSON)
            return VECTOR_MODE_JSON;
        if (configured == VECTOR_MODE_NATIVE)
            return isNativeVectorAvailable(dimension) ? VECTOR_MODE_NATIVE : VECTOR_MODE_JSON;

        return isNativeVectorAvailable(dimension) ? VECTOR_MODE_NATIVE : VECTOR_MODE_JSON;
    }

    public bool isNativeVectorAvailable(int dimension = DEFAULT_EMBEDDING_DIMENSION)
    {
        return isNativeVectorTypeAvailable() && isNativeVectorColumnAvailable(dimension);
    }

    public bool isTablesReady()
    {
        try
        {
            var tables = db.tables().Select(static table => table.ToString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return tables.Contains(table_name)
                && tables.Contains(fw.model<RagSources>().table_name)
                && tables.Contains(fw.model<FwEntities>().table_name);
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

    public int countByEntity(string entityIcode, int itemId)
    {
        if (itemId <= 0)
            return 0;

        int entityId = fw.model<FwEntities>().idByIcode(entityIcode);
        if (entityId <= 0)
            return 0;

        return db.value(table_name, DB.h("fwentities_id", entityId, "item_id", itemId, "status", db.opNOT(STATUS_DELETED)), "count(*)").toInt();
    }

    public FwList listEntityOptions()
    {
        string sql = $@"select distinct e.icode as id, e.icode as iname
                          from {qTable()} d
                          join {fw.model<FwEntities>().qTable()} e on e.id=d.fwentities_id
                         where d.status<>@status_deleted
                      order by e.icode";
        return db.arrayp(sql, DB.h("@status_deleted", STATUS_DELETED));
    }

    public FwDict oneWithSource(int id)
    {
        if (id <= 0)
            return [];

        string sql = $@"select d.*, rs.index_status, rs.last_error, rs.last_indexed_at, rs.content_hash, e.icode as entity_icode
                          from {qTable()} d
                     left join {fw.model<RagSources>().qTable()} rs on rs.id=d.rag_sources_id
                     left join {fw.model<FwEntities>().qTable()} e on e.id=d.fwentities_id
                         where d.id=@id";
        return db.rowp(sql, DB.h("@id", id));
    }

    public bool isNativeVectorTypeAvailable()
    {
        if (db.dbtype != DB.DBTYPE_SQLSRV)
            return false;
        if (nativeVectorAvailable.HasValue)
            return nativeVectorAvailable.Value;

        try
        {
            nativeVectorAvailable = db.valuep("select case when TYPE_ID(N'vector') is null then 0 else 1 end").toBool();
        }
        catch
        {
            nativeVectorAvailable = false;
        }

        return nativeVectorAvailable.Value;
    }

    private bool isNativeVectorColumnAvailable(int dimension)
    {
        if (db.dbtype != DB.DBTYPE_SQLSRV || !isNativeVectorTypeAvailable())
            return false;
        if (nativeVectorColumnAvailable.HasValue)
            return nativeVectorColumnAvailable.Value;

        try
        {
            nativeVectorColumnAvailable = db.valuep($"select case when COL_LENGTH(N'dbo.{table_name}', N'embedding_vector') is null then 0 else 1 end").toBool();
        }
        catch
        {
            nativeVectorColumnAvailable = false;
        }

        return nativeVectorColumnAvailable.Value;
    }

    private string buildSqlServerJsonQuerySql(int limit)
    {
        return $@"
with q as (
    select [key] as k, try_cast(value as float) as val
      from openjson(@JsonQueryVector)
),
scores as (
    select d.id,
           sum(q.val * try_cast(v.value as float)) as dot
      from {qTable()} d
      cross apply openjson(d.embedding_json) v
      join q on q.k = v.[key]
     where d.status=@status_active
       and d.embedding_dim=@EmbeddingDim
       and d.embedding_model=@EmbeddingModel
  ##FILTERS##
  group by d.id
)
select top (@Limit)
       d.id,
       d.rag_sources_id,
       d.fwentities_id,
       d.item_id,
       d.att_id,
       d.source_type,
       d.source_title,
       d.source_url,
       d.iname,
       d.page,
       d.section,
       d.idesc,
       (s.dot / nullif(d.embedding_norm * @QueryNorm, 0)) as CosineSim
  from scores s
  join {qTable()} d on d.id=s.id
 order by CosineSim desc, d.id";
    }

    private string buildSqliteJsonQuerySql(int limit)
    {
        return $@"
with q as (
    select key as k, cast(value as real) as val
      from json_each(@JsonQueryVector)
),
scores as (
    select d.id,
           sum(q.val * cast(v.value as real)) as dot
      from {qTable()} d
      join json_each(d.embedding_json) v
      join q on q.k = v.key
     where d.status=@status_active
       and d.embedding_dim=@EmbeddingDim
       and d.embedding_model=@EmbeddingModel
  ##FILTERS##
  group by d.id
)
select d.id,
       d.rag_sources_id,
       d.fwentities_id,
       d.item_id,
       d.att_id,
       d.source_type,
       d.source_title,
       d.source_url,
       d.iname,
       d.page,
       d.section,
       d.idesc,
       (s.dot / nullif(d.embedding_norm * @QueryNorm, 0)) as CosineSim
  from scores s
  join {qTable()} d on d.id=s.id
 order by CosineSim desc, d.id
 limit @Limit";
    }

    private string buildMySqlJsonQuerySql(int limit)
    {
        return $@"
select d.id,
       d.rag_sources_id,
       d.fwentities_id,
       d.item_id,
       d.att_id,
       d.source_type,
       d.source_title,
       d.source_url,
       d.iname,
       d.page,
       d.section,
       d.idesc,
       (sum(q.val * v.val) / nullif(d.embedding_norm * @QueryNorm, 0)) as CosineSim
  from {qTable()} d
  join json_table(@JsonQueryVector, '$[*]' columns (ord for ordinality, val double path '$')) q
  join json_table(d.embedding_json, '$[*]' columns (ord for ordinality, val double path '$')) v on v.ord=q.ord
 where d.status=@status_active
   and d.embedding_dim=@EmbeddingDim
   and d.embedding_model=@EmbeddingModel
##FILTERS##
 group by d.id, d.rag_sources_id, d.fwentities_id, d.item_id, d.att_id, d.source_type, d.source_title, d.source_url, d.iname, d.page, d.section, d.idesc, d.embedding_norm
 order by CosineSim desc, d.id
 limit @Limit";
    }

    private string buildKeywordQuerySql(int limit)
    {
        string sql = $@"
select d.id,
       d.rag_sources_id,
       d.fwentities_id,
       d.item_id,
       d.att_id,
       d.source_type,
       d.source_title,
       d.source_url,
       d.iname,
       d.page,
       d.section,
       d.idesc,
       (
         case when d.iname like @KeywordLike then 4.0 else 0 end
         + case when d.source_title like @KeywordLike then 3.0 else 0 end
         + case when d.section like @KeywordLike then 2.0 else 0 end
         + case when d.idesc like @KeywordLike then 1.0 else 0 end
       ) as KeywordScore
  from {qTable()} d
 where d.status=@status_active
   and (
        d.iname like @KeywordLike
        or d.source_title like @KeywordLike
        or d.section like @KeywordLike
        or d.idesc like @KeywordLike
        ##TERM_FILTERS##
   )
##FILTERS##
 order by KeywordScore desc, d.id";
        return db.limit(sql, Math.Max(1, limit));
    }

    private string addSearchFilters(string sql, HashSet<int>? allowedEntityIds, HashSet<int>? allowedItemIds)
    {
        string filters = string.Empty;
        if (allowedEntityIds != null && allowedEntityIds.Count > 0)
            filters += " and d.fwentities_id in (@allowed_entity_ids)";
        if (allowedItemIds != null && allowedItemIds.Count > 0)
            filters += " and d.item_id in (@allowed_item_ids)";

        int kbEntityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_KB);
        if (kbEntityId > 0)
        {
            filters += $@" and (
                d.fwentities_id<>@kb_entity_id
                or exists (
                    select 1
                      from {fw.model<KBArticles>().qTable()} k
                     where k.id=d.item_id
                       and k.status=@status_active
                       and {fw.model<KBArticles>().buildAccessWhere("k")}
                )
            )";
        }

        return sql.Replace("##FILTERS##", filters);
    }

    private FwDict buildSearchParams(string qJson, double qNorm, int dimension, string embeddingModel, int limit, HashSet<int>? allowedEntityIds, HashSet<int>? allowedItemIds)
    {
        var @params = DB.h(
            "@JsonQueryVector", qJson,
            "@QueryNorm", qNorm,
            "@EmbeddingDim", dimension,
            "@EmbeddingModel", embeddingModel,
            "@Limit", Math.Max(1, limit),
            "@status_active", STATUS_ACTIVE,
            "@kb_entity_id", fw.model<FwEntities>().idByIcode(FwEntities.ICODE_KB),
            "@current_access_level", fw.userAccessLevel
        );
        if (allowedEntityIds != null && allowedEntityIds.Count > 0)
            @params["allowed_entity_ids"] = allowedEntityIds.ToList();
        if (allowedItemIds != null && allowedItemIds.Count > 0)
            @params["allowed_item_ids"] = allowedItemIds.ToList();
        return @params;
    }

    private FwDict buildKeywordParams(string query, int limit, HashSet<int>? allowedEntityIds, HashSet<int>? allowedItemIds)
    {
        var @params = DB.h(
            "@KeywordLike", "%" + query + "%",
            "@Limit", Math.Max(1, limit),
            "@status_active", STATUS_ACTIVE,
            "@kb_entity_id", fw.model<FwEntities>().idByIcode(FwEntities.ICODE_KB),
            "@current_access_level", fw.userAccessLevel
        );
        if (allowedEntityIds != null && allowedEntityIds.Count > 0)
            @params["allowed_entity_ids"] = allowedEntityIds.ToList();
        if (allowedItemIds != null && allowedItemIds.Count > 0)
            @params["allowed_item_ids"] = allowedItemIds.ToList();

        string termFilters = buildKeywordTermFilters(query, @params);
        @params["__term_filters"] = termFilters;
        return @params;
    }

    private static string buildKeywordTermFilters(string query, FwDict @params)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static term => term.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
        if (terms.Count == 0)
            return string.Empty;

        var parts = new List<string>();
        for (int i = 0; i < terms.Count; i++)
        {
            string param = "@TermLike" + i;
            @params[param] = "%" + terms[i] + "%";
            parts.Add($"or d.iname like {param} or d.source_title like {param} or d.section like {param} or d.idesc like {param}");
        }
        return string.Join("\n        ", parts);
    }

    private List<ChunkSearchResult> mapSearchRows(DBList rows, string defaultMode)
    {
        List<ChunkSearchResult> result = new(rows.Count);
        var kbModel = fw.model<KBArticles>();
        int kbEntityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_KB);
        int spageEntityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_SPAGE);
        Dictionary<int, FwDict> kbCache = [];
        Dictionary<int, FwDict> spageCache = [];

        foreach (DBRow row in rows)
        {
            int chunkId = row["id"].toInt();
            int sourceId = row["rag_sources_id"].toInt();
            int fwentitiesId = row["fwentities_id"].toInt();
            int itemId = row["item_id"].toInt();
            var citation = new ChunkCitation
            {
                ChunkId = chunkId,
                SourceId = sourceId,
                SourceType = row["source_type"].toStr(),
                SourceTitle = row["source_title"].toStr(),
                SourceUrl = row["source_url"].toStr(),
                File = row["iname"].toStr(),
                Page = row["page"].toInt(),
                Section = row["section"].toStr(),
                FwEntitiesId = fwentitiesId,
                ItemId = itemId,
                AttId = row["att_id"].toInt()
            };
            if (fwentitiesId == kbEntityId && itemId > 0)
            {
                citation.ArticleId = itemId;
                citation.ArticleUrl = kbModel.getFullUrl(itemId);
                if (!kbCache.TryGetValue(itemId, out var kb))
                {
                    kb = kbModel.one(itemId).toFwDict();
                    kbCache[itemId] = kb;
                }
                citation.ArticleName = kb[kbModel.field_iname].toStr(citation.SourceTitle);
            }
            else if (fwentitiesId == spageEntityId && itemId > 0)
            {
                if (!spageCache.TryGetValue(itemId, out var spage))
                {
                    spage = fw.model<Spages>().one(itemId).toFwDict();
                    spageCache[itemId] = spage;
                }
                citation.SourceTitle = string.IsNullOrWhiteSpace(citation.SourceTitle) ? spage["iname"].toStr() : citation.SourceTitle;
                citation.SourceUrl = string.IsNullOrWhiteSpace(citation.SourceUrl) ? fw.model<Spages>().getFullUrl(itemId) : citation.SourceUrl;
            }

            double vectorScore = row["CosineSim"].toDouble();
            double keywordScore = row["KeywordScore"].toDouble();
            result.Add(new ChunkSearchResult
            {
                ChunkId = chunkId,
                SourceId = sourceId,
                Text = row["idesc"].toStr(),
                Citation = citation,
                Score = vectorScore > 0 ? vectorScore : keywordScore,
                VectorScore = vectorScore,
                KeywordScore = keywordScore,
                RetrievalMode = defaultMode
            });
        }

        return result;
    }

    private List<ChunkSearchResult> mergeHybridResults(List<ChunkSearchResult> vectorResults, List<ChunkSearchResult> keywordResults, int limit)
    {
        limit = Math.Max(1, limit);
        Dictionary<int, ChunkSearchResult> byChunk = [];
        double maxKeyword = keywordResults.Count == 0 ? 0 : keywordResults.Max(static item => item.KeywordScore);

        for (int i = 0; i < vectorResults.Count; i++)
        {
            var item = vectorResults[i];
            item.Score = item.VectorScore + reciprocalRank(i);
            item.RetrievalMode = "vector";
            byChunk[item.ChunkId] = item;
        }

        for (int i = 0; i < keywordResults.Count; i++)
        {
            var item = keywordResults[i];
            double normalizedKeyword = maxKeyword <= 0 ? 0 : item.KeywordScore / maxKeyword;
            if (byChunk.TryGetValue(item.ChunkId, out var existing))
            {
                existing.KeywordScore = item.KeywordScore;
                existing.Score = (existing.VectorScore * 0.65) + (normalizedKeyword * 0.35) + reciprocalRank(i);
                existing.RetrievalMode = "hybrid";
            }
            else
            {
                item.Score = (normalizedKeyword * 0.8) + reciprocalRank(i);
                item.RetrievalMode = "keyword";
                byChunk[item.ChunkId] = item;
            }
        }

        int maxPerSource = limit <= 3 ? 1 : 2;
        Dictionary<int, int> perSource = [];
        List<ChunkSearchResult> output = [];
        foreach (var item in byChunk.Values.OrderByDescending(static item => item.Score).ThenBy(static item => item.SourceId).ThenBy(static item => item.ChunkId))
        {
            int sourceId = item.SourceId > 0 ? item.SourceId : -item.ChunkId;
            perSource.TryGetValue(sourceId, out int sourceCount);
            if (sourceCount >= maxPerSource && output.Count < limit - 1)
                continue;

            perSource[sourceId] = sourceCount + 1;
            output.Add(item);
            if (output.Count >= limit)
                break;
        }

        return output;
    }

    private static double reciprocalRank(int index)
    {
        return 1.0 / (60 + index + 1);
    }

    private FwList toAssistantResults(List<ChunkSearchResult> results)
    {
        var output = new FwList(results.Count);
        foreach (var item in results)
        {
            var citation = item.Citation;
            output.Add(new FwDict
            {
                ["text"] = item.Text ?? string.Empty,
                ["chunk_id"] = item.ChunkId,
                ["source_id"] = item.SourceId,
                ["source_type"] = citation.SourceType,
                ["source_title"] = citation.SourceTitle,
                ["source_url"] = citation.SourceUrl,
                ["filename"] = citation.File,
                ["page"] = citation.Page,
                ["section"] = citation.Section,
                ["fwentities_id"] = citation.FwEntitiesId,
                ["item_id"] = citation.ItemId,
                ["att_id"] = citation.AttId,
                ["url"] = !string.IsNullOrWhiteSpace(citation.ArticleUrl) ? citation.ArticleUrl : citation.SourceUrl,
                ["article_id"] = citation.ArticleId,
                ["article_name"] = citation.ArticleName,
                ["article_url"] = citation.ArticleUrl,
                ["score"] = item.Score,
                ["vector_score"] = item.VectorScore,
                ["keyword_score"] = item.KeywordScore,
                ["retrieval_mode"] = item.RetrievalMode
            });
        }

        return output;
    }

    private void traceRetrieval(string query, string mode, List<ChunkSearchResult> results)
    {
        var payload = results.Select(static item => new
        {
            source_id = item.SourceId,
            chunk_id = item.ChunkId,
            score = item.Score,
            vector_score = item.VectorScore,
            keyword_score = item.KeywordScore,
            retrieval_mode = item.RetrievalMode
        }).ToList();
        fw.logger(LogLevel.DEBUG, "Assistant retrieval trace: query=", query, ", mode=", mode, ", results=", Utils.jsonEncode(payload));
    }

    public void deleteBySource(int sourceId)
    {
        if (sourceId <= 0)
            return;
        db.del(table_name, DB.h("rag_sources_id", sourceId));
    }

    public void deleteByEntity(string entity_icode, int item_id)
    {
        int fwentitiesId = fw.model<FwEntities>().idByIcode(entity_icode);
        if (fwentitiesId <= 0)
            return;

        db.del(table_name, DB.h("fwentities_id", fwentitiesId, "item_id", item_id));
    }

    public void deleteLegacyByEntity(int fwentitiesId, int itemId)
    {
        if (fwentitiesId <= 0 || itemId <= 0)
            return;

        db.del(table_name, DB.h("rag_sources_id", 0, "fwentities_id", fwentitiesId, "item_id", itemId));
    }

    public bool isExistsByEntity(string entity_icode, int item_id)
    {
        int fwentitiesId = fw.model<FwEntities>().idByIcode(entity_icode);
        if (fwentitiesId <= 0)
            return false;

        return db.value(table_name, DB.h("fwentities_id", fwentitiesId, "item_id", item_id, "status", STATUS_ACTIVE), "1").toBool();
    }

    public HashSet<int> listIndexedEntityItemIds(string entity_icode, IEnumerable<int> item_ids)
    {
        var ids = item_ids.Where(static id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        int fwentitiesId = fw.model<FwEntities>().idByIcode(entity_icode);
        if (fwentitiesId <= 0)
            return [];

        string sql = $@"select distinct item_id
                          from {qTable()}
                         where fwentities_id=@fwentities_id
                           and item_id in (@item_ids)
                           and status=@status_active";
        var rows = db.arrayp(sql, DB.h("@fwentities_id", fwentitiesId, "item_ids", ids, "@status_active", STATUS_ACTIVE));
        HashSet<int> result = [];
        foreach (DBRow row in rows)
        {
            int itemId = row["item_id"].toInt();
            if (itemId > 0)
                result.Add(itemId);
        }
        return result;
    }

    public string firstChunkTextByEntity(string entity_icode, int item_id)
    {
        int fwentitiesId = fw.model<FwEntities>().idByIcode(entity_icode);
        if (fwentitiesId <= 0)
            return string.Empty;

        var row = db.row(table_name, DB.h("fwentities_id", fwentitiesId, "item_id", item_id, "status", STATUS_ACTIVE), "chunk_index, id");
        return row["idesc"];
    }

    public static double cosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left == null || right == null || left.Count == 0 || left.Count != right.Count)
            return 0;

        double dot = 0;
        for (int i = 0; i < left.Count; i++)
            dot += left[i] * right[i];

        double norm = vectorNorm(left) * vectorNorm(right);
        return norm <= 0 ? 0 : dot / norm;
    }

    private void updateNativeVectorColumn(int id, string embeddingJson, int dimension)
    {
        if (id <= 0 || !isNativeVectorColumnAvailable(dimension))
            return;

        try
        {
            db.exec($@"update {qTable()}
                          set embedding_vector=CAST(@EmbeddingJson AS vector({dimension}))
                        where id=@id", DB.h("@EmbeddingJson", embeddingJson, "@id", id));
        }
        catch (Exception ex)
        {
            fw.logger(LogLevel.WARN, "Native vector column update failed; JSON embedding remains available: ", ex.Message);
        }
    }

    private static double vectorNorm(IReadOnlyList<float> values)
    {
        if (values == null || values.Count == 0)
            return 0;

        double sum = 0;
        foreach (float value in values)
            sum += value * value;
        return Math.Sqrt(sum);
    }

    public class ChunkEmbedding
    {
        public int RagSourcesId { get; set; }
        public int FwEntitiesId { get; set; }
        public int ItemId { get; set; }
        public int AttId { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string SourceTitle { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public int Page { get; set; }
        public string Section { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public List<float> Embedding { get; set; } = [];
        public string EmbeddingModel { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = string.Empty;
    }

    public class ChunkSearchResult
    {
        public int ChunkId { get; set; }
        public int SourceId { get; set; }
        public string Text { get; set; } = string.Empty;
        public ChunkCitation Citation { get; set; } = new();
        public double Score { get; set; }
        public double VectorScore { get; set; }
        public double KeywordScore { get; set; }
        public string RetrievalMode { get; set; } = string.Empty;
    }

    public class ChunkCitation
    {
        public int ChunkId { get; set; }
        public int SourceId { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string SourceTitle { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public int Page { get; set; }
        public string Section { get; set; } = string.Empty;
        public int FwEntitiesId { get; set; }
        public int ItemId { get; set; }
        public int AttId { get; set; }
        public int ArticleId { get; set; }
        public string ArticleName { get; set; } = string.Empty;
        public string ArticleUrl { get; set; } = string.Empty;
    }
}
