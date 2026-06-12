using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

public class DocChunks : FwModel<DocChunks.Row>
{
    public const string VECTOR_MODE_AUTO = "auto";
    public const string VECTOR_MODE_JSON = "json";
    public const string VECTOR_MODE_NATIVE = "native";
    public const int DEFAULT_EMBEDDING_DIMENSION = 1536;

    public class Row
    {
        public int id { get; set; }
        public int fwentities_id { get; set; }
        public int item_id { get; set; }
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

    public DocChunks()
    {
        table_name = "doc_chunks";
        is_log_changes = false;
    }

    public void addEmbedding(ChunkEmbedding record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Embedding == null || record.Embedding.Count == 0)
            throw new ArgumentException("Embedding vector is required.", nameof(record));
        if (record.ItemId <= 0)
            throw new ArgumentException("ItemId is required for embedding records.", nameof(record));

        int fwentitiesId = record.FwEntitiesId > 0
            ? record.FwEntitiesId
            : fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_ATT);
        string embeddingModel = string.IsNullOrWhiteSpace(record.EmbeddingModel)
            ? fw.config("ASSISTANT_EMBEDDING_MODEL").toStr(LLM.MODEL_TEXT_EMBEDDING_3_SMALL)
            : record.EmbeddingModel.Trim();
        if (string.IsNullOrWhiteSpace(embeddingModel))
            embeddingModel = LLM.MODEL_TEXT_EMBEDDING_3_SMALL;

        db.insert(table_name, DB.h(
            "fwentities_id", fwentitiesId,
            "item_id", record.ItemId,
            "chunk_index", record.ChunkIndex,
            "iname", record.Filename ?? string.Empty,
            "page", record.Page,
            "section", record.Section ?? string.Empty,
            "idesc", record.Text ?? string.Empty,
            "embedding_json", JsonSerializer.Serialize(record.Embedding),
            "embedding_norm", vectorNorm(record.Embedding),
            "embedding_dim", record.Embedding.Count,
            "embedding_model", embeddingModel,
            "vector_backend", resolveVectorBackend(record.Embedding.Count),
            "metadata_json", record.MetadataJson ?? string.Empty,
            "status", STATUS_ACTIVE
        ));
    }

    public async Task<List<ChunkSearchResult>> listByQueryAsync(string query, int limit = 3, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var embeddingModel = fw.config("ASSISTANT_EMBEDDING_MODEL").toStr(LLM.MODEL_TEXT_EMBEDDING_3_SMALL);
        if (string.IsNullOrWhiteSpace(embeddingModel))
            embeddingModel = LLM.MODEL_TEXT_EMBEDDING_3_SMALL;

        var queryEmbedding = await fw.model<LLM>().embeddingForTextAsync(query, embeddingModel, cancellationToken).ConfigureAwait(false);
        return listByEmbedding(queryEmbedding, embeddingModel, limit, null, null);
    }

    public async Task<FwList> listAssistantSearchResultsAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        int kbEntityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_KB);
        if (kbEntityId <= 0)
            return [];

        var results = await listByQueryFilteredAsync(query, limit, [kbEntityId], null, cancellationToken).ConfigureAwait(false);

        var output = new FwList(results.Count);
        foreach (var item in results)
        {
            var citation = item.Citation;
            output.Add(new FwDict
            {
                ["text"] = item.Text ?? string.Empty,
                ["filename"] = citation.File,
                ["page"] = citation.Page,
                ["section"] = citation.Section,
                ["fwentities_id"] = citation.FwEntitiesId,
                ["item_id"] = citation.ItemId,
                ["url"] = citation.ArticleUrl,
                ["article_id"] = citation.ArticleId,
                ["article_name"] = citation.ArticleName,
                ["article_url"] = citation.ArticleUrl
            });
        }

        return output;
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
        var output = new FwList(results.Count);
        foreach (var item in results)
        {
            output.Add(new FwDict
            {
                ["text"] = item.Text ?? string.Empty,
                ["filename"] = item.Citation.File,
                ["page"] = item.Citation.Page,
                ["section"] = item.Citation.Section,
                ["fwentities_id"] = item.Citation.FwEntitiesId,
                ["item_id"] = item.Citation.ItemId
            });
        }

        return output;
    }

    private async Task<List<ChunkSearchResult>> listByQueryFilteredAsync(string query, int limit, HashSet<int>? allowedEntityIds, HashSet<int>? allowedItemIds, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var embeddingModel = fw.config("ASSISTANT_EMBEDDING_MODEL").toStr(LLM.MODEL_TEXT_EMBEDDING_3_SMALL);
        if (string.IsNullOrWhiteSpace(embeddingModel))
            embeddingModel = LLM.MODEL_TEXT_EMBEDDING_3_SMALL;

        var queryEmbedding = await fw.model<LLM>().embeddingForTextAsync(query, embeddingModel, cancellationToken).ConfigureAwait(false);
        return listByEmbedding(queryEmbedding, embeddingModel, limit, allowedEntityIds, allowedItemIds);
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
                return mapSearchRows(listByNativeVectorQuery(queryEmbedding, embeddingModel, limit, allowedEntityIds, allowedItemIds));
            }
            catch (Exception ex)
            {
                fw.logger(LogLevel.WARN, "Native vector query failed; falling back to JSON scoring:", ex.Message);
            }
        }

        return mapSearchRows(listByJsonQuery(JsonSerializer.Serialize(queryEmbedding), vectorNorm(queryEmbedding), queryEmbedding.Count, embeddingModel, limit, allowedEntityIds, allowedItemIds));
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
        string distance = $"VECTOR_DISTANCE('cosine', CAST(@JsonQueryVector AS vector({dimension})), CAST(d.embedding_json AS vector({dimension})))";
        string sql = $@"select d.id,
                               d.fwentities_id,
                               d.item_id,
                               d.iname,
                               d.page,
                               d.section,
                               d.idesc,
                               (1.0 - {distance}) as CosineSim
                          from {qTable()} d
                         where d.status=@status_active
                           and d.embedding_dim=@EmbeddingDim
                           and d.embedding_model=@EmbeddingModel
                      ##FILTERS##
                      order by {distance}";
        sql = addSearchFilters(sql, allowedEntityIds, allowedItemIds);
        sql = db.limit(sql, Math.Max(1, limit));
        return db.arrayp(sql, buildSearchParams(qJson, vectorNorm(queryEmbedding), dimension, embeddingModel, limit, allowedEntityIds, allowedItemIds));
    }

    private string resolveVectorBackend(int dimension)
    {
        string configured = fw.config("ASSISTANT_VECTOR_MODE").toStr(VECTOR_MODE_AUTO).Trim().ToLowerInvariant();
        if (configured == VECTOR_MODE_JSON)
            return VECTOR_MODE_JSON;
        if (configured == VECTOR_MODE_NATIVE)
            return isNativeVectorAvailable(dimension) ? VECTOR_MODE_NATIVE : VECTOR_MODE_JSON;

        return isNativeVectorAvailable(dimension) ? VECTOR_MODE_NATIVE : VECTOR_MODE_JSON;
    }

    public bool isNativeVectorAvailable(int dimension = 1536)
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
       d.fwentities_id,
       d.item_id,
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
       d.fwentities_id,
       d.item_id,
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
       d.fwentities_id,
       d.item_id,
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
 group by d.id, d.fwentities_id, d.item_id, d.iname, d.page, d.section, d.idesc, d.embedding_norm
 order by CosineSim desc, d.id
 limit @Limit";
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
                      from kb_articles k
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

    private List<ChunkSearchResult> mapSearchRows(DBList rows)
    {
        List<ChunkSearchResult> result = new(rows.Count);
        var kbModel = fw.model<KBArticles>();
        int kbEntityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_KB);
        Dictionary<int, FwDict> kbCache = [];

        foreach (DBRow row in rows)
        {
            int fwentitiesId = row["fwentities_id"].toInt();
            int itemId = row["item_id"].toInt();
            var citation = new ChunkCitation
            {
                File = row["iname"],
                Page = row["page"].toInt(),
                Section = row["section"],
                FwEntitiesId = fwentitiesId,
                ItemId = itemId
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
                citation.ArticleName = kb[kbModel.field_iname].toStr();
            }

            result.Add(new ChunkSearchResult
            {
                Text = row["idesc"],
                Citation = citation,
                Score = row["CosineSim"].toDouble()
            });
        }

        return result;
    }

    public void deleteByEntity(string entity_icode, int item_id)
    {
        int fwentitiesId = fw.model<FwEntities>().idByIcode(entity_icode);
        if (fwentitiesId <= 0)
            return;

        db.del(table_name, DB.h("fwentities_id", fwentitiesId, "item_id", item_id));
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
        public int FwEntitiesId { get; set; }
        public int ItemId { get; set; }
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
        public string Text { get; set; } = string.Empty;
        public ChunkCitation Citation { get; set; } = new();
        public double Score { get; set; }
    }

    public class ChunkCitation
    {
        public string File { get; set; } = string.Empty;
        public int Page { get; set; }
        public string Section { get; set; } = string.Empty;
        public int FwEntitiesId { get; set; }
        public int ItemId { get; set; }
        public int ArticleId { get; set; }
        public string ArticleName { get; set; } = string.Empty;
        public string ArticleUrl { get; set; } = string.Empty;
    }
}
