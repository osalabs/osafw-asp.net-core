using System;

namespace osafw;

public class KBArticles : FwModel<KBArticles.Row>
{
    public class Row
    {
        public int id { get; set; }
        public string icode { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public string content_markdown { get; set; } = string.Empty;
        public int access_level { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int? add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int? upd_users_id { get; set; }
    }

    public const int STATUS_NEEDS_REVIEW = 20;

    public KBArticles()
    {
        table_name = "kb_articles";
    }

    public override bool isAccess(int id = 0, string action = "")
    {
        if (fw.model<Users>().isSiteAdmin())
            return true;

        var row = one(id);
        return row.Count > 0 && checkAccess(row);
    }

    public bool checkAccess(FwDict article)
    {
        if (article.Count == 0)
            return false;

        int requiredAccess = article["access_level"].toInt(Users.ACL_MEMBER);
        return fw.userAccessLevel >= requiredAccess;
    }

    public void checkAccess(int id)
    {
        if (!isAccess(id))
            throw new AuthException("Knowledge base article is not available.");
    }

    public string buildAccessWhere(string alias = "t")
    {
        alias = string.IsNullOrWhiteSpace(alias) ? "" : alias.Trim().TrimEnd('.') + ".";
        if (fw.model<Users>().isSiteAdmin())
            return "1=1";

        return $"{alias}access_level<=@current_access_level";
    }

    public FwList listSearch(string search = "", int offset = 0, int limit = 25)
    {
        string sql = $@"select t.*
                          from {qTable()} t
                         where t.status=@status_active
                           and {buildAccessWhere("t")}
                           and (
                                @search=''
                                or t.iname like @search_like
                                or t.idesc like @search_like
                           )
                      order by coalesce(t.upd_time, t.add_time) desc, t.id desc";
        sql = db.limit(sql, Math.Max(1, limit), Math.Max(0, offset));
        return db.arrayp(sql, DB.h(
            "@status_active", STATUS_ACTIVE,
            "@current_access_level", fw.userAccessLevel,
            "@search", search ?? string.Empty,
            "@search_like", "%" + (search ?? string.Empty).Trim() + "%"
        ));
    }

    public string getFullUrl(int id)
    {
        return $"/Admin/KBArticles/{id}";
    }

    public bool reindexKBArticle(int id)
    {
        try
        {
            var article = one(id);
            if (article.Count == 0)
                return false;

            var svc = new DocumentEmbeddingService(fw);
            if (article["status"].toInt() == STATUS_ACTIVE)
                svc.IndexKBArticleAsync(id).GetAwaiter().GetResult();
            else
                svc.DeleteKBArticleEmbeddings(id);

            return true;
        }
        catch (Exception ex)
        {
            fw.logger(LogLevel.WARN, "KB reindex failed:", ex.Message);
            return false;
        }
    }

    public override void delete(int id, bool is_perm = false)
    {
        try
        {
            new DocumentEmbeddingService(fw).DeleteKBArticleEmbeddings(id);
        }
        catch (Exception ex)
        {
            fw.logger(LogLevel.WARN, "KB embedding cleanup failed:", ex.Message);
        }

        base.delete(id, is_perm);
    }
}
