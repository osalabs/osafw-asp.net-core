using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace osafw;

public sealed record AssistantToolRegistration(string Name, string CapabilityGroup, bool RequiresApproval, AITool Tool);

public sealed class AssistantClarificationRequestedException : Exception
{
    public AssistantClarificationDto Clarification { get; }

    public AssistantClarificationRequestedException(AssistantClarificationDto clarification)
        : base(clarification?.prompt ?? "Clarification requested.")
    {
        Clarification = clarification ?? new AssistantClarificationDto();
    }
}

public sealed class AssistantToolRuntime
{
    private readonly FW fw;
    private readonly int threadId;
    private readonly int runId;
    private readonly int userId;

    public AssistantToolRuntime(FW fw, int threadId, int runId, int userId)
    {
        this.fw = fw;
        this.threadId = threadId;
        this.runId = runId;
        this.userId = userId;
    }

    public FW Fw => fw;
    public int ThreadId => threadId;
    public int RunId => runId;
    public int UserId => userId;
    public bool HasPersistedRun => runId > 0;

    public void AddProgress(string message, string eventType = AssistantRunsEvents.TYPE_PROGRESS, object? payload = null)
    {
        if (!HasPersistedRun)
            return;

        fw.model<AssistantRunsEvents>().addEvent(runId, eventType, message, payload == null ? "" : Utils.jsonEncode(payload));
    }

    public void LogToolCall(string toolName, object? args = null)
    {
        if (HasPersistedRun)
            fw.model<AssistantRunsEvents>().addEvent(runId, AssistantRunsEvents.TYPE_TOOL, "Tool: " + toolName, args == null ? string.Empty : Utils.jsonEncode(args));
        fw.logger(LogLevel.DEBUG, "Assistant tool call [", toolName, "] args=", args == null ? "{}" : Utils.jsonEncode(args));
    }

    public void LogToolResult(string toolName, string summary)
    {
        fw.logger(LogLevel.DEBUG, "Assistant tool result [", toolName, "] ", summary ?? string.Empty);
    }

    public void RecordRetrievalEvidence(string toolName, string query, FwList results)
    {
        if (!HasPersistedRun || results.Count == 0)
            return;

        var evidence = results
            .OfType<FwDict>()
            .Select(static item => new
            {
                source_id = item["source_id"].toInt(),
                chunk_id = item["chunk_id"].toInt(),
                source_type = item["source_type"].toStr(),
                title = item["source_title"].toStr(item["article_name"].toStr(item["filename"].toStr())),
                url = item["url"].toStr(item["source_url"].toStr(item["article_url"].toStr())),
                page = item["page"].toInt(),
                section = item["section"].toStr(),
                score = item["score"].toDouble(),
                vector_score = item["vector_score"].toDouble(),
                keyword_score = item["keyword_score"].toDouble(),
                retrieval_mode = item["retrieval_mode"].toStr()
            })
            .Where(static item => item.source_id > 0 || item.chunk_id > 0)
            .ToList();
        if (evidence.Count == 0)
            return;

        fw.model<AssistantRunsEvents>().addEvent(
            runId,
            AssistantRunsEvents.TYPE_EVIDENCE,
            "Evidence: " + toolName,
            Utils.jsonEncode(new { tool = toolName, query = query ?? string.Empty, evidence })
        );
    }

    public void RequestClarification(AssistantClarificationDto clarification)
    {
        throw new AssistantClarificationRequestedException(clarification);
    }
}

public sealed class AssistantToolCatalog
{
    private readonly AssistantToolRuntime runtime;

    public AssistantToolCatalog(AssistantToolRuntime runtime)
    {
        this.runtime = runtime;
    }

    public List<AssistantToolRegistration> Build()
    {
        var ragTool = new AssistantRagTool(runtime);
        var threadSearchTool = new AssistantThreadAttachmentSearchTool(runtime);
        var contactSearchTool = new AssistantContactSearchTool(runtime);
        var clarificationTool = new AssistantClarificationTool(runtime);
        var progressTool = new AssistantProgressTool(runtime);

        return
        [
            Register(ragTool.search, "search_knowledge_base", "rag"),
            Register(threadSearchTool.search, "search_thread_attachments", "thread_files"),
            Register(contactSearchTool.search, "search_contacts", "contacts"),
            Register(clarificationTool.request, "request_clarification", "clarification"),
            Register(progressTool.report, "report_progress", "progress"),
        ];
    }

    private static AssistantToolRegistration Register(Delegate method, string name, string capabilityGroup, bool requiresApproval = false)
    {
        var tool = AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
        {
            Name = name,
            Description = capabilityGroup + " tool"
        });
        return new AssistantToolRegistration(name, capabilityGroup, requiresApproval, tool);
    }
}

public sealed class AssistantRagTool
{
    private readonly AssistantToolRuntime runtime;

    public AssistantRagTool(AssistantToolRuntime runtime) => this.runtime = runtime;

    [Description("Search published knowledge base articles and return relevant chunks with citations.")]
    public async Task<FwList> search(
        [Description("Semantic search phrase built from the user request.")] string query,
        [Description("Maximum number of chunks to return.")] int k = 10)
    {
        runtime.LogToolCall("search_knowledge_base", new { query, k });
        runtime.AddProgress("Searching knowledge base.");
        var output = await runtime.Fw.model<RagChunks>().listAssistantSearchResultsAsync(query, Math.Clamp(k, 1, 20)).ConfigureAwait(false);
        runtime.RecordRetrievalEvidence("search_knowledge_base", query, output);
        runtime.LogToolResult("search_knowledge_base", "Returned " + output.Count + " matches.");
        return output;
    }
}

public sealed class AssistantThreadAttachmentSearchTool
{
    private readonly AssistantToolRuntime runtime;

    public AssistantThreadAttachmentSearchTool(AssistantToolRuntime runtime) => this.runtime = runtime;

    [Description("Search uploaded files attached to the current thread and return relevant chunks.")]
    public async Task<FwList> search(
        [Description("Semantic search phrase for files uploaded in the current thread.")] string query,
        [Description("Maximum number of chunks to return.")] int k = 5)
    {
        runtime.LogToolCall("search_thread_attachments", new { query, k });
        runtime.AddProgress("Searching uploaded files in this thread.");

        var messageIds = listThreadIndexedMessageIds();
        if (messageIds.Count == 0)
            return [];

        var filtered = await runtime.Fw.model<RagChunks>().listAssistantThreadSearchResultsAsync(query, messageIds, Math.Clamp(k, 1, 10)).ConfigureAwait(false);
        var urlByMessageId = listAttachmentUrlByMessageIds(messageIds);
        foreach (FwDict item in filtered)
        {
            int messageId = item["item_id"].toInt();
            if (urlByMessageId.TryGetValue(messageId, out string? url))
                item["url"] = url;
        }

        runtime.RecordRetrievalEvidence("search_thread_attachments", query, filtered);
        runtime.LogToolResult("search_thread_attachments", "Returned " + filtered.Count + " matches.");
        return filtered;
    }

    private HashSet<int> listThreadIndexedMessageIds()
    {
        string sql = @"
select distinct m.id
  from assistant_messages m
  join att_links al on al.item_id=m.id
 where al.fwentities_id=@fwentities_id
   and m.assistant_threads_id=@assistant_threads_id
   and al.status<>@status_deleted
   and m.status<>@status_deleted";

        int messageEntityId = runtime.Fw.model<FwEntities>().idByIcode(FwEntities.ICODE_ASSISTANT_MESSAGE);
        if (messageEntityId <= 0)
            return [];

        var rows = runtime.Fw.db.arrayp(sql, DB.h(
            "@fwentities_id", messageEntityId,
            "@assistant_threads_id", runtime.ThreadId,
            "@status_deleted", FwModel.STATUS_DELETED
        ));

        var messageIds = rows.Select(static row => row["id"].toInt()).Where(static id => id > 0);
        return runtime.Fw.model<RagChunks>().listIndexedEntityItemIds(FwEntities.ICODE_ASSISTANT_MESSAGE, messageIds);
    }

    private Dictionary<int, string> listAttachmentUrlByMessageIds(IEnumerable<int> messageIds)
    {
        var ids = messageIds.Where(static id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        string sql = @"
select al.item_id as assistant_messages_id,
       a.id,
       a.icode
  from att_links al
  join att a on a.id=al.att_id
 where al.fwentities_id=@fwentities_id
   and al.item_id in (@item_ids)
   and al.status<>@status_deleted
   and a.status<>@status_deleted
 order by al.item_id, a.id";

        int messageEntityId = runtime.Fw.model<FwEntities>().idByIcode(FwEntities.ICODE_ASSISTANT_MESSAGE);
        if (messageEntityId <= 0)
            return [];

        var rows = runtime.Fw.db.arrayp(sql, DB.h(
            "@fwentities_id", messageEntityId,
            "item_ids", ids,
            "@status_deleted", FwModel.STATUS_DELETED
        ));

        Dictionary<int, string> result = [];
        foreach (DBRow row in rows)
        {
            int messageId = row["assistant_messages_id"].toInt();
            if (messageId <= 0 || result.ContainsKey(messageId))
                continue;

            result[messageId] = "/Att/" + row["icode"];
        }
        return result;
    }
}

public sealed class AssistantContactSearchTool
{
    private readonly AssistantToolRuntime runtime;

    public AssistantContactSearchTool(AssistantToolRuntime runtime) => this.runtime = runtime;

    [Description("Search active users/contacts by name, email, login, title, or city using simple LIKE matching.")]
    public FwList search(
        [Description("Contact name, email, login, title, or city phrase.")] string query,
        [Description("Maximum number of contacts to return.")] int k = 5)
    {
        runtime.LogToolCall("search_contacts", new { query, k });
        string search = (query ?? string.Empty).Trim();
        if (search.Length == 0)
            return [];

        string sql = $@"select id, fname, lname, iname, email, login, title, city, state
                          from {runtime.Fw.db.qid("users")}
                         where status=@status_active
                           and (
                                fname like @search
                                or lname like @search
                                or iname like @search
                                or email like @search
                                or login like @search
                                or title like @search
                                or city like @search
                           )
                      order by fname, lname, email, id";
        var rows = runtime.Fw.db.arrayp(runtime.Fw.db.limit(sql, Math.Clamp(k, 1, 10)), DB.h(
            "@status_active", FwModel.STATUS_ACTIVE,
            "@search", "%" + search + "%"
        ));

        var output = new FwList(rows.Count);
        bool canLinkUsers = runtime.Fw.userAccessLevel >= Users.ACL_MANAGER;
        foreach (FwDict row in rows)
        {
            string name = (row["fname"].toStr() + " " + row["lname"].toStr()).Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = row["iname"].toStr(row["email"].toStr(row["login"].toStr()));

            output.Add(new FwDict
            {
                ["source_type"] = "user_contact",
                ["users_id"] = row["id"].toInt(),
                ["name"] = name,
                ["title"] = row["title"].toStr(),
                ["email"] = row["email"].toStr(),
                ["city"] = row["city"].toStr(),
                ["state"] = row["state"].toStr(),
                ["url"] = canLinkUsers ? "/Admin/Users/" + row["id"].toInt() : string.Empty
            });
        }

        runtime.LogToolResult("search_contacts", "Returned " + output.Count + " matches.");
        return output;
    }
}

public sealed class AssistantClarificationTool
{
    private readonly AssistantToolRuntime runtime;

    public AssistantClarificationTool(AssistantToolRuntime runtime) => this.runtime = runtime;

    [Description("Pause the run and ask the user for structured clarification using select or short text fields.")]
    public string request(
        [Description("Short title shown on the clarification card.")] string title,
        [Description("Prompt explaining what the assistant needs from the user.")] string prompt,
        [Description("JSON array of fields.")] string fields_json,
        [Description("Optional label for the submit button.")] string submit_label = "Continue")
    {
        runtime.LogToolCall("request_clarification", new { title, prompt, submit_label });
        var clarification = new AssistantClarificationDto
        {
            title = title ?? string.Empty,
            prompt = prompt ?? string.Empty,
            submit_label = string.IsNullOrWhiteSpace(submit_label) ? "Continue" : submit_label,
            fields = parseFields(fields_json)
        };

        runtime.AddProgress("Waiting for clarification from the user.");
        runtime.RequestClarification(clarification);
        return "waiting_for_user";
    }

    private static List<AssistantClarificationField> parseFields(string fieldsJson)
    {
        if (string.IsNullOrWhiteSpace(fieldsJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<AssistantClarificationField>>(fieldsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

public sealed class AssistantProgressTool
{
    private readonly AssistantToolRuntime runtime;

    public AssistantProgressTool(AssistantToolRuntime runtime) => this.runtime = runtime;

    [Description("Emit a short progress update that is safe to show to the user while work is in progress.")]
    public string report([Description("Short progress text safe for the user interface.")] string message)
    {
        runtime.LogToolCall("report_progress", new { message });
        runtime.AddProgress(message);
        return "ok";
    }
}
