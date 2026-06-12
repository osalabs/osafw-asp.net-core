using System;
using System.Collections.Generic;
using System.Linq;

namespace osafw;

public class AssistantThreads : FwModel<AssistantThreads.Row>
{
    public class Row
    {
        public int id { get; set; }
        public string icode { get; set; } = string.Empty;
        public int? users_id { get; set; }
        public string owner_token { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public string provider_thread_id { get; set; } = string.Empty;
        public int? last_run_status { get; set; }
        public DateTime? last_message_at { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int? add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int? upd_users_id { get; set; }
    }

    public AssistantThreads()
    {
        table_name = "assistant_threads";
        is_log_changes = false;
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

    public Row? oneTypedByIcode(string icode)
    {
        if (string.IsNullOrWhiteSpace(icode))
            return null;

        string sql = db.limit($@"select *
                                  from {qTable()}
                                 where icode=@icode
                                   and status<>@status_deleted
                              order by id", 1);
        return db.rowp<Row>(sql, DB.h("@icode", icode.Trim(), "@status_deleted", STATUS_DELETED));
    }

    public Row? oneTypedForOwner(int id, int usersId, string ownerToken)
    {
        if (usersId <= 0 && string.IsNullOrWhiteSpace(ownerToken))
            return null;

        string sql = db.limit($@"select *
                                  from {qTable()}
                                 where id=@id
                                   and status<>@status_deleted
                                   and {buildOwnerWhereClause(usersId, ownerToken)}
                              order by id", 1);
        var @params = DB.h("@id", id, "@status_deleted", STATUS_DELETED);
        addOwnerParams(@params, usersId, ownerToken);
        return db.rowp<Row>(sql, @params);
    }

    public int addThread(int usersId, string ownerToken, string iname)
    {
        return add(DB.h(
            "users_id", usersId > 0 ? usersId : null,
            "owner_token", usersId > 0 ? string.Empty : ownerToken ?? string.Empty,
            "iname", string.IsNullOrWhiteSpace(iname) ? "New chat" : iname.Trim(),
            "last_message_at", DB.NOW
        ));
    }

    public string ensureShareIcode(int id)
    {
        var thread = oneTyped(id) ?? throw new ApplicationException("Assistant thread not found.");
        if (!string.IsNullOrWhiteSpace(thread.icode))
            return thread.icode;

        string icode = string.Empty;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string candidate = Utils.nanoid(16);
            if (oneTypedByIcode(candidate) != null)
                continue;

            icode = candidate;
            break;
        }

        if (string.IsNullOrWhiteSpace(icode))
            icode = Utils.uuid().Replace("-", "");

        update(id, DB.h("icode", icode));
        return icode;
    }

    public void touch(int id)
    {
        update(id, DB.h("last_message_at", DB.NOW));
    }

    public void updateProviderThreadId(int id, string providerThreadId)
    {
        update(id, DB.h("provider_thread_id", providerThreadId ?? string.Empty));
    }

    public void updateLastRunStatus(int id, int? runStatus)
    {
        update(id, DB.h("last_run_status", runStatus, "last_message_at", DB.NOW));
    }

    public void updateInameIfDefault(int id, string iname)
    {
        if (string.IsNullOrWhiteSpace(iname))
            return;

        string sql = $@"update {qTable()}
                           set iname=@iname,
                               upd_time={db.sqlNOW()}
                         where id=@id
                           and (iname='' or iname like 'New chat%')";
        db.updatep(sql, DB.h("@id", id, "@iname", iname.Trim()));
        removeCache(id);
    }

    public List<Row> listHistoryByOwner(int usersId, string ownerToken, string search = "", int limit = 50)
    {
        if (usersId <= 0 && string.IsNullOrWhiteSpace(ownerToken))
            return [];

        string sql = $@"select *
                          from {qTable()}
                         where status<>@status_deleted
                           and {buildOwnerWhereClause(usersId, ownerToken)}";
        var @params = DB.h("@status_deleted", STATUS_DELETED);
        addOwnerParams(@params, usersId, ownerToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += @" and (
                            iname like @search
                            or exists (
                                select 1
                                  from assistant_messages m
                                 where m.assistant_threads_id=assistant_threads.id
                                   and m.role=@role_user
                                   and m.status<>@status_deleted
                                   and m.preview_text like @search
                            )
                         )";
            @params["@search"] = "%" + search.Trim() + "%";
            @params["@role_user"] = AssistantMessages.ROLE_USER;
        }

        sql += " order by coalesce(last_message_at, add_time) desc, id desc";
        return db.arrayp<Row>(db.limit(sql, Math.Max(1, limit)), @params);
    }

    private static string buildOwnerWhereClause(int usersId, string ownerToken)
    {
        bool hasUser = usersId > 0;
        bool hasOwnerToken = !string.IsNullOrWhiteSpace(ownerToken);

        if (hasUser && hasOwnerToken)
            return "(users_id=@users_id or (coalesce(users_id, 0)=0 and owner_token=@owner_token))";
        if (hasUser)
            return "users_id=@users_id";
        return "(coalesce(users_id, 0)=0 and owner_token=@owner_token)";
    }

    private static void addOwnerParams(FwDict sqlParams, int usersId, string ownerToken)
    {
        if (usersId > 0)
            sqlParams["@users_id"] = usersId;
        if (!string.IsNullOrWhiteSpace(ownerToken))
            sqlParams["@owner_token"] = ownerToken;
    }
}
