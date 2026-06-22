using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace osafw;

public class AdminRagChunksController : FwDynamicController
{
    public static new int access_level = Users.ACL_SITEADMIN;
    // Maximum vector-ranked chunks shown in the admin RAG chunk grid.
    private const int ADMIN_VECTOR_SEARCH_LIMIT = 500;

    protected RagChunks model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Admin/RagChunks";
        loadControllerConfig();
        model = model0 as RagChunks ?? throw new FwConfigUndefinedModelException();
        db = model.getDB();
        list_view = model.adminListViewSql();
        config["list_view"] = list_view;
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
        if (!model.isTablesReady())
        {
            var psNotReady = new FwDict
            {
                ["count"] = 0,
                ["f"] = reqh("f"),
                ["tables_ready"] = false,
            };
            setRagIndexMetadata(psNotReady, false);
            return psNotReady;
        }

        var ps = base.IndexAction();
        ps["tables_ready"] = true;
        ps["is_list_filter_search_placeholder_custom"] = true;
        ps["list_filter_search_placeholder"] = "Vector search chunk content";
        setRagIndexMetadata(ps, true);
        return ps;
    }

    public override void setListSearch()
    {
        string query = list_filter["s"].toStr().Trim();
        string entity = list_filter["entity"].toStr().Trim();
        string originalSearchFields = search_fields;
        if (!string.IsNullOrWhiteSpace(query))
            search_fields = string.Empty;

        try
        {
            base.setListSearch();
        }
        finally
        {
            search_fields = originalSearchFields;
        }

        if (!string.IsNullOrWhiteSpace(query))
            applyVectorSearchFilter(query, entity);

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

    private void applyVectorSearchFilter(string query, string entity)
    {
        List<int> chunkIds;
        try
        {
            chunkIds = model.listChunkIdsByVectorSearchAsync(query, ADMIN_VECTOR_SEARCH_LIMIT, entity, fw.context.RequestAborted)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            fw.flash("error", "RAG vector search failed: " + ex.Message);
            chunkIds = [];
        }

        if (chunkIds.Count == 0)
        {
            list_where += " and 1=0";
            return;
        }

        list_where += " and id in (@ids)";
        list_where_params["ids"] = chunkIds;
        var cases = chunkIds
            .Where(static id => id > 0)
            .Select(static (id, index) => "when " + id + " then " + index);
        list_orderby = "case id " + string.Join(" ", cases) + " else " + chunkIds.Count + " end";
    }

    public override FwDict ShowAction(int id)
    {
        if (!model.isTablesReady())
            throw new UserException("Assistant tables are not installed.");

        var item = model.oneWithSource(id);
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
        ps["is_readonly"] = true;
        ps["tab"] = form_tab;
        ps["rbac"] = rbac;
        return ps;
    }

    public FwDict? DeleteEntityAction()
    {
        enforcePost();
        if (!model.isTablesReady())
            throw new UserException("Assistant tables are not installed.");

        string entityIcode = reqs("entity_icode");
        int itemId = reqi("item_id");
        if (string.IsNullOrWhiteSpace(entityIcode) || itemId <= 0)
            throw new UserException("Entity and item id are required.");

        fw.model<RagSources>().deleteByEntity(entityIcode, itemId);
        fw.flash("success", "RAG sources and chunks deleted.");
        fw.redirect(base_url);
        return null;
    }

    public FwDict? RequeueSourceAction(int id)
    {
        enforcePost();
        if (!model.isTablesReady())
            throw new UserException("Assistant tables are not installed.");

        bool requeued = fw.model<RagSources>().requeueSource(id);
        fw.flash(requeued ? "success" : "error", requeued ? "RAG source queued for retry." : "RAG source could not be queued.");
        fw.redirect(base_url);
        return null;
    }

    private void setRagIndexMetadata(FwDict ps, bool includeDatabaseState)
    {
        ps["vector_mode"] = fw.model<Settings>().read("ASSISTANT_VECTOR_MODE", RagChunks.VECTOR_MODE_AUTO);
        ps["embedding_model"] = LLM.MODEL_TEXT_EMBEDDING_3_SMALL;
        int runTimeoutSeconds = Math.Clamp(
            fw.model<Settings>().readInt("ASSISTANT_RUN_TIMEOUT_SECONDS", AssistantRuns.DEFAULT_RUN_TIMEOUT_SECONDS),
            30,
            1800
        );
        ps["assistant_run_timeout_seconds"] = runTimeoutSeconds;
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
            ps["failed_source_count"] = 0;
            ps["diagnostic_sources"] = new FwList();
            ps["diagnostic_runs"] = new FwList();
            ps["recent_evidence_events"] = new FwList();
            return;
        }

        var sources = fw.model<RagSources>();
        ps["entities"] = model.listEntityOptions();
        ps["chunk_count"] = model.countActive();
        ps["source_count"] = sources.countActive();
        ps["queued_count"] = sources.countQueued();
        ps["failed_source_count"] = sources.countFailed();
        ps["diagnostic_sources"] = sources.listDiagnostics();
        ps["diagnostic_runs"] = fw.model<AssistantRuns>().listDiagnostics(runTimeoutSeconds);
        ps["recent_evidence_events"] = fw.model<AssistantRunsEvents>().listRecentEvidence();
    }
}
