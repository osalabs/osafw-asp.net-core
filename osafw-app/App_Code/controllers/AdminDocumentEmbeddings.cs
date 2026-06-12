using System;
using System.Linq;
using System.Text.Json;

namespace osafw;

public class AdminDocumentEmbeddingsController : FwController
{
    public static new int access_level = Users.ACL_MANAGER;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Admin/DocumentEmbeddings";
    }

    /// <summary>
    /// Allows managers to inspect embedding state without requiring a generated RBAC resource.
    /// </summary>
    public override void checkAccess()
    {
        if (fw.userAccessLevel < access_level)
            throw new AuthException("Bad access - Not authorized");
    }

    public FwDict IndexAction()
    {
        var ps = new FwDict
        {
            ["title"] = "Document Embeddings",
            ["base_url"] = base_url,
            ["f"] = reqh("f"),
            ["tables_ready"] = areTablesReady(),
            ["vector_mode"] = fw.config("ASSISTANT_VECTOR_MODE").toStr(DocChunks.VECTOR_MODE_AUTO),
            ["embedding_model"] = fw.config("ASSISTANT_EMBEDDING_MODEL").toStr(LLM.MODEL_TEXT_EMBEDDING_3_SMALL),
        };

        if (!ps["tables_ready"].toBool())
            return ps;

        var f = ps["f"] as FwDict ?? [];
        string search = f["s"].toStr().Trim();
        string entity = f["entity"].toStr().Trim();
        string backend = f["backend"].toStr().Trim();

        string where = "d.status<>@status_deleted";
        var args = DB.h("@status_deleted", FwModel.STATUS_DELETED);
        if (!string.IsNullOrWhiteSpace(search))
        {
            where += " and (d.iname like @search or d.section like @search or d.idesc like @search)";
            args["@search"] = "%" + search + "%";
        }
        if (!string.IsNullOrWhiteSpace(entity))
        {
            where += " and e.icode=@entity";
            args["@entity"] = entity;
        }
        if (!string.IsNullOrWhiteSpace(backend))
        {
            where += " and d.vector_backend=@backend";
            args["@backend"] = backend;
        }

        string sql = $@"select d.id,
                               d.fwentities_id,
                               d.item_id,
                               d.chunk_index,
                               d.iname,
                               d.page,
                               d.section,
                               d.embedding_dim,
                               d.embedding_model,
                               d.vector_backend,
                               d.add_time,
                               e.icode as entity_icode
                          from {db.qid("doc_chunks")} d
                     left join {db.qid("fwentities")} e on e.id=d.fwentities_id
                         where {where}
                      order by d.add_time desc, d.id desc";
        ps["rows"] = db.arrayp(db.limit(sql, 100), args);
        ps["entities"] = db.arrayp($@"select distinct e.icode as id, e.icode as iname
                                        from {db.qid("doc_chunks")} d
                                        join {db.qid("fwentities")} e on e.id=d.fwentities_id
                                       where d.status<>@status_deleted
                                    order by e.icode", DB.h("@status_deleted", FwModel.STATUS_DELETED));
        ps["backend_options"] = new FwList
        {
            DB.h("id", DocChunks.VECTOR_MODE_JSON, "iname", "JSON"),
            DB.h("id", DocChunks.VECTOR_MODE_NATIVE, "iname", "Native")
        };
        ps["chunk_count"] = db.valuep("select count(*) from doc_chunks where status<>@status_deleted", DB.h("@status_deleted", FwModel.STATUS_DELETED)).toInt();
        return ps;
    }

    public FwDict ShowAction(int id)
    {
        if (!areTablesReady())
            throw new UserException("Assistant tables are not installed.");

        string sql = $@"select d.*, e.icode as entity_icode
                          from {db.qid("doc_chunks")} d
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

        return new FwDict
        {
            ["title"] = "Document Embedding",
            ["i"] = item,
            ["embedding_preview"] = preview,
            ["base_url"] = base_url,
        };
    }

    public FwDict? DeleteEntityAction()
    {
        enforcePost();
        string entityIcode = reqs("entity_icode");
        int itemId = reqi("item_id");
        if (string.IsNullOrWhiteSpace(entityIcode) || itemId <= 0)
            throw new UserException("Entity and item id are required.");

        fw.model<DocChunks>().deleteByEntity(entityIcode, itemId);
        fw.flash("success", "Document embeddings deleted.");
        fw.redirect(base_url);
        return null;
    }

    private bool areTablesReady()
    {
        try
        {
            var tables = db.tables().Select(static table => table.ToString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return tables.Contains("doc_chunks") && tables.Contains("fwentities");
        }
        catch
        {
            return false;
        }
    }
}
