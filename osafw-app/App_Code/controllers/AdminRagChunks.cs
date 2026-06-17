using System;
using System.Collections;
using System.Linq;
using System.Text.Json;

namespace osafw;

public class AdminRagChunksController : FwDynamicController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected RagChunks model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Admin/RagChunks";
        loadControllerConfig();
        model = model0 as RagChunks ?? throw new FwConfigUndefinedModelException();
        db = model.getDB();
        is_readonly = true;
    }

    /// <summary>
    /// Allows Site Admins to inspect chunk/vector state without requiring a generated RBAC resource.
    /// </summary>
    public override void checkAccess()
    {
        if (fw.userAccessLevel < access_level)
            throw new AuthException("Bad access - Not authorized");
    }

    public override FwDict IndexAction()
    {
        if (!areTablesReady())
        {
            var psNotReady = new FwDict
            {
                ["title"] = "RAG Chunks",
                ["base_url"] = base_url,
                ["count"] = 0,
                ["f"] = reqh("f"),
                ["tables_ready"] = false,
            };
            setRagIndexMetadata(psNotReady, false);
            return psNotReady;
        }

        var ps = base.IndexAction();
        ps["tables_ready"] = true;
        setRagIndexMetadata(ps, true);
        return ps;
    }

    public override void setListSearch()
    {
        base.setListSearch();

        string entity = list_filter["entity"].toStr().Trim();
        if (!string.IsNullOrWhiteSpace(entity))
        {
            list_where += " and entity_icode=@entity";
            list_where_params["entity"] = entity;
        }

        string backend = list_filter["backend"].toStr().Trim();
        if (!string.IsNullOrWhiteSpace(backend))
        {
            list_where += " and vector_backend=@backend";
            list_where_params["backend"] = backend;
        }
    }

    public override FwDict ShowAction(int id)
    {
        if (!areTablesReady())
            throw new UserException("Assistant tables are not installed.");

        string sql = $@"select d.*, rs.index_status, rs.last_error, rs.last_indexed_at, rs.content_hash, e.icode as entity_icode
                          from {db.qid("rag_chunks")} d
                     left join {db.qid("rag_sources")} rs on rs.id=d.rag_sources_id
                     left join {db.qid("fwentities")} e on e.id=d.fwentities_id
                         where d.id=@id";
        var item = db.rowp(sql, DB.h("@id", id));
        if (item.Count == 0)
            throw new NotFoundException();

        string embeddingJson = item["embedding_json"].toStr();
        string preview = embeddingJson;
        try
        {
            var vector = JsonSerializer.Deserialize<double[]>(embeddingJson) ?? [];
            preview = string.Join(", ", vector.Take(8).Select(static value => value.ToString("0.####")));
            if (vector.Length > 8)
                preview += ", ...";
        }
        catch
        {
            if (preview.Length > 160)
                preview = preview[..160] + "...";
        }
        item["embedding_preview"] = preview;

        var ps = new FwDict();
        setAddUpdUser(ps, item);

        if (is_dynamic_show)
        {
            if (config["form_tabs"] is IList formTabs && formTabs.Count > 1)
                ps["form_tabs"] = new FwList(formTabs);

            ps["fields"] = prepareShowFields(item, ps);
        }

        ps["id"] = id;
        ps["i"] = item;
        ps["embedding_preview"] = preview;
        setPSReturnContext(ps);
        ps["related_id"] = related_id;
        ps["base_url"] = base_url;
        ps["is_readonly"] = true;
        ps["tab"] = form_tab;
        ps["rbac"] = rbac;
        return ps;
    }

    public FwDict? DeleteEntityAction()
    {
        enforcePost();
        string entityIcode = reqs("entity_icode");
        int itemId = reqi("item_id");
        if (string.IsNullOrWhiteSpace(entityIcode) || itemId <= 0)
            throw new UserException("Entity and item id are required.");

        fw.model<RagSources>().deleteByEntity(entityIcode, itemId);
        fw.flash("success", "RAG sources and chunks deleted.");
        fw.redirect(base_url);
        return null;
    }

    private void setRagIndexMetadata(FwDict ps, bool includeDatabaseState)
    {
        ps["vector_mode"] = fw.model<Settings>().read("ASSISTANT_VECTOR_MODE", RagChunks.VECTOR_MODE_AUTO);
        ps["embedding_model"] = LLM.MODEL_TEXT_EMBEDDING_3_SMALL;
        ps["backend_options"] = new FwList
        {
            DB.h("id", RagChunks.VECTOR_MODE_JSON, "iname", "JSON"),
            DB.h("id", RagChunks.VECTOR_MODE_NATIVE, "iname", "Native")
        };

        if (!includeDatabaseState)
        {
            ps["entities"] = new FwList();
            ps["chunk_count"] = 0;
            ps["source_count"] = 0;
            ps["queued_count"] = 0;
            return;
        }

        ps["entities"] = db.arrayp($@"select distinct e.icode as id, e.icode as iname
                                        from {db.qid("rag_chunks")} d
                                        join {db.qid("fwentities")} e on e.id=d.fwentities_id
                                       where d.status<>@status_deleted
                                    order by e.icode", DB.h("@status_deleted", FwModel.STATUS_DELETED));
        ps["chunk_count"] = db.valuep("select count(*) from rag_chunks where status<>@status_deleted", DB.h("@status_deleted", FwModel.STATUS_DELETED)).toInt();
        ps["source_count"] = db.valuep("select count(*) from rag_sources where status<>@status_deleted", DB.h("@status_deleted", FwModel.STATUS_DELETED)).toInt();
        ps["queued_count"] = db.valuep("select count(*) from rag_sources where status<>@status_deleted and index_status in (@statuses)", DB.h(
            "@status_deleted", FwModel.STATUS_DELETED,
            "statuses", new StrList { RagSources.INDEX_STATUS_PENDING, RagSources.INDEX_STATUS_STALE }
        )).toInt();
    }

    private bool areTablesReady()
    {
        try
        {
            var tables = db.tables().Select(static table => table.ToString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return tables.Contains("rag_chunks") && tables.Contains("rag_sources") && tables.Contains("fwentities");
        }
        catch
        {
            return false;
        }
    }
}
