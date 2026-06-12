using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace osafw;

public sealed class AssistantAppService
{
    private const string AnonymousOwnerSessionKey = "assistant_owner_token";
    private const int DefaultMaxFilesPerMessage = 5;
    private const long DefaultMaxIndexedFileBytes = 5 * 1024 * 1024;

    private static readonly string[] RequiredTables =
    [
        "assistant_threads",
        "assistant_messages",
        "assistant_runs",
        "assistant_runs_events",
        "assistant_feedback",
        "kb_articles",
        "doc_chunks"
    ];

    private readonly FW fw;

    private sealed record AssistantOwnerScope(int usersId, string ownerToken);

    public AssistantAppService(FW fw)
    {
        this.fw = fw;
    }

    public AssistantRuntimeStatus RuntimeStatus()
    {
        bool enabled = fw.config("ASSISTANT_ENABLED").toBool();
        bool tablesReady = areTablesReady();
        bool openAiConfigured = fw.model<LLM>().isConfigured();
        string message = "";

        if (!enabled)
            message = "Assistant is disabled.";
        else if (!tablesReady)
            message = "Assistant tables are not installed.";
        else if (!openAiConfigured)
            message = "OpenAI API key is not configured.";

        return new AssistantRuntimeStatus
        {
            enabled = enabled,
            tables_ready = tablesReady,
            openai_configured = openAiConfigured,
            message = message
        };
    }

    public AssistantThreadDto GetThread(int threadId, int usersId)
    {
        ensureTablesReady();
        var owner = resolveOwnerScope(usersId, false);
        var thread = requireThread(threadId, owner);
        var messages = fw.model<AssistantMessages>().listByThread(thread.id);
        var run = fw.model<AssistantRuns>().latestByThread(thread.id);
        var events = run == null ? [] : fw.model<AssistantRunsEvents>().listByRun(run.id);
        return mapThread(thread, messages, events, run, owner);
    }

    public AssistantThreadDto GetSharedThread(string icode, int usersId)
    {
        ensureTablesReady();
        var owner = resolveOwnerScope(usersId, false);
        var thread = requireSharedThread(icode);
        var messages = fw.model<AssistantMessages>().listByThread(thread.id);
        var run = fw.model<AssistantRuns>().latestByThread(thread.id);
        var events = run == null ? [] : fw.model<AssistantRunsEvents>().listByRun(run.id);
        return mapThread(thread, messages, events, run, owner);
    }

    public AssistantPollingResponse PollThread(int threadId, int usersId, string sharedIcode = "", int lastMessageId = 0, int lastEventId = 0)
    {
        ensureTablesReady();
        var owner = resolveOwnerScope(usersId, false);
        var thread = !string.IsNullOrWhiteSpace(sharedIcode)
            ? requireSharedThread(sharedIcode)
            : requireThread(threadId, owner);
        var run = fw.model<AssistantRuns>().latestByThread(thread.id);
        var messages = fw.model<AssistantMessages>().listByThread(thread.id, lastMessageId);
        var events = run == null ? [] : fw.model<AssistantRunsEvents>().listByRun(run.id, lastEventId);

        return new AssistantPollingResponse
        {
            thread = mapThreadState(thread, run, owner),
            run = mapRun(run),
            messages = mapMessages(messages),
            events = mapEvents(events),
            last_message_id = messages.Count > 0 ? messages[^1].id : lastMessageId,
            last_event_id = events.Count > 0 ? events[^1].id : lastEventId,
        };
    }

    public List<AssistantThreadPreviewDto> ListHistory(int usersId, string search = "")
    {
        if (!areTablesReady())
            return [];

        var owner = resolveOwnerScope(usersId, true);
        var rows = fw.model<AssistantThreads>().listHistoryByOwner(owner.usersId, owner.ownerToken, search, 50);
        var previewMap = fw.model<AssistantMessages>().listFirstUserPreviewByThreadIds(rows.Select(static row => row.id));
        var previews = new List<AssistantThreadPreviewDto>(rows.Count);
        foreach (var row in rows)
        {
            previews.Add(new AssistantThreadPreviewDto
            {
                id = row.id,
                iname = string.IsNullOrWhiteSpace(row.iname) ? "New chat" : row.iname,
                preview = previewMap.TryGetValue(row.id, out var preview) ? preview : string.Empty,
                last_message_at = formatDate(row.last_message_at ?? row.add_time),
                last_run_status_id = row.last_run_status,
                last_run_status = AssistantRuns.StatusToCode(row.last_run_status),
                is_shared = !string.IsNullOrWhiteSpace(row.icode),
            });
        }
        return previews;
    }

    public AssistantThreadShareDto EnsureSharedThread(int usersId, int threadId)
    {
        ensureTablesReady();
        var owner = resolveOwnerScope(usersId, false);
        var thread = requireThread(threadId, owner);
        string icode = fw.model<AssistantThreads>().ensureShareIcode(thread.id);
        return new AssistantThreadShareDto
        {
            thread_id = thread.id,
            icode = icode,
            share_url = buildShareUrl(icode),
        };
    }

    public async Task<(AssistantThreadDto thread, AssistantMessageDto message, AssistantRunDto run)> CreateOrContinueTurnAsync(int usersId, int threadId, string prompt, FwDict? clarificationAnswers, IList<IFormFile>? files)
    {
        var status = RuntimeStatus();
        if (!status.enabled || !status.tables_ready || !status.openai_configured)
            throw new UserException(status.message);

        bool hasText = !string.IsNullOrWhiteSpace(prompt);
        bool hasClarification = clarificationAnswers != null && clarificationAnswers.Count > 0;
        bool hasFiles = files != null && files.Count > 0;
        if (!hasText && !hasClarification && !hasFiles)
            throw new UserException("Please enter a message.");
        if (hasFiles)
            validateAssistantFiles(files!);

        var owner = resolveOwnerScope(usersId, true);
        AssistantThreads.Row? existingThread = null;
        if (threadId > 0)
            existingThread = requireThread(threadId, owner);

        int effectiveThreadId = existingThread?.id
            ?? fw.model<AssistantThreads>().addThread(usersId, owner.ownerToken, buildDefaultThreadName(prompt));

        string userContent = buildUserMessageContent(prompt, clarificationAnswers, hasFiles);
        int messageId = fw.model<AssistantMessages>().addMessage(
            effectiveThreadId,
            AssistantMessages.ROLE_USER,
            AssistantMessages.TYPE_MESSAGE,
            userContent,
            usersId: usersId
        );

        if (hasFiles)
            await saveFilesToMessageAsync(messageId, files!).ConfigureAwait(false);

        int runId = fw.model<AssistantRuns>().queueRun(effectiveThreadId, messageId);
        fw.model<AssistantThreads>().touch(effectiveThreadId);
        fw.model<AssistantThreads>().updateLastRunStatus(effectiveThreadId, AssistantRuns.STATUS_QUEUED);
        fw.model<AssistantRunsEvents>().addEvent(runId, AssistantRunsEvents.TYPE_STATUS, "Queued");

        var thread = requireThread(effectiveThreadId, owner);
        var message = fw.model<AssistantMessages>().oneTyped(messageId) ?? throw new ApplicationException("Assistant message was not saved.");
        var run = fw.model<AssistantRuns>().oneTyped(runId) ?? throw new ApplicationException("Assistant run was not saved.");

        return (mapThread(thread, [message], [], run, owner), mapMessage(message, loadAttachmentsByMessageId([message.id])), mapRun(run)!);
    }

    public void SubmitFeedback(int usersId, int threadId, int runId, int messageId, string feedbackType, string comment)
    {
        ensureTablesReady();
        if (string.IsNullOrWhiteSpace(feedbackType) && string.IsNullOrWhiteSpace(comment))
            throw new UserException("Feedback cannot be empty.");

        if (threadId > 0)
        {
            var owner = resolveOwnerScope(usersId, false);
            _ = requireThread(threadId, owner);
        }

        fw.model<AssistantFeedback>().addFeedback(usersId, threadId, runId, messageId, feedbackType, comment);
    }

    public void EnrichAssistantSources(List<AssistantSource> sources)
    {
        if (sources == null || sources.Count == 0)
            return;

        foreach (var source in sources)
        {
            if (source == null)
                continue;

            if (string.IsNullOrWhiteSpace(source.name))
                source.name = !string.IsNullOrWhiteSpace(source.article_name) ? source.article_name : source.filename;
            if (string.IsNullOrWhiteSpace(source.url))
                source.url = !string.IsNullOrWhiteSpace(source.article_url) ? source.article_url : source.file_url;
        }
    }

    private bool areTablesReady()
    {
        try
        {
            var tables = dbTables();
            return RequiredTables.All(required => tables.Contains(required, StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private HashSet<string> dbTables()
    {
        return fw.db.tables().Select(static table => table.ToString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void ensureTablesReady()
    {
        if (!areTablesReady())
            throw new UserException("Assistant tables are not installed.");
    }

    private AssistantThreads.Row requireThread(int threadId, AssistantOwnerScope owner)
    {
        var thread = fw.model<AssistantThreads>().oneTypedForOwner(threadId, owner.usersId, owner.ownerToken);
        if (thread == null || thread.id <= 0)
            throw new AuthException("Assistant thread not found.");
        return thread;
    }

    private AssistantThreads.Row requireSharedThread(string icode)
    {
        var thread = fw.model<AssistantThreads>().oneTypedByIcode((icode ?? string.Empty).Trim());
        if (thread == null || thread.id <= 0 || string.IsNullOrWhiteSpace(thread.icode))
            throw new AuthException("Assistant thread not found.");
        return thread;
    }

    private AssistantOwnerScope resolveOwnerScope(int usersId, bool createAnonymousToken)
    {
        string ownerToken = fw.Session(AnonymousOwnerSessionKey);
        if (usersId <= 0 && string.IsNullOrWhiteSpace(ownerToken) && createAnonymousToken)
        {
            ownerToken = Guid.NewGuid().ToString("N");
            fw.Session(AnonymousOwnerSessionKey, ownerToken);
        }

        return new AssistantOwnerScope(usersId, ownerToken);
    }

    private static bool isOwnerThread(AssistantThreads.Row thread, AssistantOwnerScope owner)
    {
        if (thread.users_id.GetValueOrDefault() > 0 && owner.usersId > 0 && thread.users_id == owner.usersId)
            return true;

        return thread.users_id.GetValueOrDefault() <= 0
            && !string.IsNullOrWhiteSpace(thread.owner_token)
            && string.Equals(thread.owner_token, owner.ownerToken, StringComparison.Ordinal);
    }

    private async Task saveFilesToMessageAsync(int messageId, IList<IFormFile> files)
    {
        foreach (var file in files)
            await saveFileToMessageAsync(messageId, file).ConfigureAwait(false);
    }

    private async Task<int> saveFileToMessageAsync(int messageId, IFormFile file)
    {
        if (file == null || file.Length <= 0)
            throw new UserException("No file selected.");

        int messageEntityId = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_ASSISTANT_MESSAGE);
        var attModel = fw.model<Att>();
        int attId = attModel.add(DB.h(
            "fwentities_id", messageEntityId,
            "item_id", messageId,
            "status", FwModel.STATUS_UNDER_UPDATE
        ));

        attModel.uploadOne(attId, file, true);
        fw.model<AttLinks>().add(DB.h(
            "att_id", attId,
            "fwentities_id", messageEntityId,
            "item_id", messageId,
            "status", FwModel.STATUS_ACTIVE
        ));

        var embeddingService = new DocumentEmbeddingService(fw);
        string ext = Path.GetExtension(file.FileName);
        if (embeddingService.IsSupported(ext) && file.Length <= maxIndexedFileBytes())
        {
            try
            {
                await embeddingService.IndexAttachmentToEntityAsync(attId, FwEntities.ICODE_ASSISTANT_MESSAGE, messageId, clearExisting: false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                fw.logger(LogLevel.WARN, "Assistant attachment indexing failed:", ex.Message);
            }
        }
        else if (embeddingService.IsSupported(ext))
        {
            fw.logger(LogLevel.WARN, "Assistant attachment indexing skipped because file exceeds byte limit: ", file.FileName);
        }

        return attId;
    }

    private void validateAssistantFiles(IList<IFormFile> files)
    {
        int maxFiles = Math.Max(1, fw.config("ASSISTANT_MAX_FILES_PER_MESSAGE").toInt(DefaultMaxFilesPerMessage));
        int count = files.Count(file => file != null && file.Length > 0);
        if (count > maxFiles)
            throw new UserException("Upload up to " + maxFiles + " files per assistant message.");
    }

    private long maxIndexedFileBytes()
    {
        return Math.Max(1, fw.config("ASSISTANT_MAX_INDEXED_FILE_BYTES").toLong(DefaultMaxIndexedFileBytes));
    }

    private Dictionary<int, List<AssistantAttachmentDto>> loadAttachmentsByMessageId(IEnumerable<int> messageIds)
    {
        var ids = messageIds.Where(static id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        int messageEntityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_ASSISTANT_MESSAGE);
        if (messageEntityId <= 0)
            return [];

        string sql = @"
select a.*, al.item_id as assistant_messages_id
  from att_links al
  join att a on a.id=al.att_id
 where al.fwentities_id=@fwentities_id
   and al.item_id in (@item_ids)
   and al.status<>@status_deleted
   and a.status<>@status_deleted
 order by al.item_id, a.id";

        var rows = fw.db.arrayp(sql, DB.h(
            "@fwentities_id", messageEntityId,
            "item_ids", ids,
            "@status_deleted", FwModel.STATUS_DELETED
        ));

        Dictionary<int, List<AssistantAttachmentDto>> result = [];
        var indexedIds = fw.model<DocChunks>().listIndexedEntityItemIds(FwEntities.ICODE_ASSISTANT_MESSAGE, ids);
        foreach (FwDict row in rows)
        {
            int messageId = row["assistant_messages_id"].toInt();
            if (!result.TryGetValue(messageId, out var attachments))
            {
                attachments = [];
                result[messageId] = attachments;
            }
            attachments.Add(mapAttachment(row, indexedIds.Contains(messageId)));
        }
        return result;
    }

    private AssistantAttachmentDto mapAttachment(FwDict row, bool isIndexed)
    {
        string ext = row["ext"].toStr();
        bool isSupported = new DocumentEmbeddingService(fw).IsSupported(ext);
        long fsize = row["fsize"].toLong();
        return new AssistantAttachmentDto
        {
            id = row["id"].toInt(),
            icode = row["icode"].toStr(),
            iname = row["iname"].toStr(row["fname"].toStr()),
            url = fw.model<Att>().getUrl(row),
            ext = ext,
            fsize = fsize,
            is_image = row["is_image"].toBool(),
            is_indexed = isIndexed,
            index_status = attachmentIndexStatus(isIndexed, isSupported, fsize),
        };
    }

    private string attachmentIndexStatus(bool isIndexed, bool isSupported, long fsize)
    {
        if (isIndexed)
            return "indexed";
        if (!isSupported)
            return "not indexed: unsupported file type";
        if (fsize > maxIndexedFileBytes())
            return "not indexed: file exceeds indexing limit";
        return "not indexed";
    }

    private AssistantThreadDto mapThread(AssistantThreads.Row thread, List<AssistantMessages.Row> messages, List<AssistantRunsEvents.Row> events, AssistantRuns.Row? run, AssistantOwnerScope owner)
    {
        var messageIds = messages.Select(static message => message.id);
        return new AssistantThreadDto
        {
            id = thread.id,
            icode = thread.icode,
            iname = string.IsNullOrWhiteSpace(thread.iname) ? "New chat" : thread.iname,
            last_message_at = formatDate(thread.last_message_at ?? thread.add_time),
            last_run_status_id = thread.last_run_status,
            last_run_status = AssistantRuns.StatusToCode(thread.last_run_status),
            is_shared = !string.IsNullOrWhiteSpace(thread.icode),
            share_url = string.IsNullOrWhiteSpace(thread.icode) ? string.Empty : buildShareUrl(thread.icode),
            is_owner = isOwnerThread(thread, owner),
            is_readonly = !isOwnerThread(thread, owner),
            messages = mapMessages(messages, loadAttachmentsByMessageId(messageIds)),
            events = mapEvents(events),
            active_run = mapRun(run),
        };
    }

    private AssistantThreadDto mapThreadState(AssistantThreads.Row thread, AssistantRuns.Row? run, AssistantOwnerScope owner)
    {
        return new AssistantThreadDto
        {
            id = thread.id,
            icode = thread.icode,
            iname = string.IsNullOrWhiteSpace(thread.iname) ? "New chat" : thread.iname,
            last_message_at = formatDate(thread.last_message_at ?? thread.add_time),
            last_run_status_id = thread.last_run_status,
            last_run_status = AssistantRuns.StatusToCode(thread.last_run_status),
            is_shared = !string.IsNullOrWhiteSpace(thread.icode),
            share_url = string.IsNullOrWhiteSpace(thread.icode) ? string.Empty : buildShareUrl(thread.icode),
            is_owner = isOwnerThread(thread, owner),
            is_readonly = !isOwnerThread(thread, owner),
            active_run = mapRun(run),
        };
    }

    private List<AssistantMessageDto> mapMessages(List<AssistantMessages.Row> messages, Dictionary<int, List<AssistantAttachmentDto>>? attachments = null)
    {
        attachments ??= loadAttachmentsByMessageId(messages.Select(static message => message.id));
        return messages.Select(message => mapMessage(message, attachments)).ToList();
    }

    private AssistantMessageDto mapMessage(AssistantMessages.Row message, Dictionary<int, List<AssistantAttachmentDto>> attachments)
    {
        return new AssistantMessageDto
        {
            id = message.id,
            role = message.role,
            message_type = message.message_type,
            content_markdown = message.content_markdown,
            add_time = formatDate(message.add_time),
            confidence = message.confidence,
            sources = decodeSources(message.sources_json),
            clarification = decodeClarification(message.payload_json, message.message_type),
            attachments = attachments.TryGetValue(message.id, out var list) ? list : [],
        };
    }

    private List<AssistantRunEventDto> mapEvents(List<AssistantRunsEvents.Row> events)
    {
        return events.Select(static row => new AssistantRunEventDto
        {
            id = row.id,
            assistant_runs_id = row.assistant_runs_id,
            event_type = row.event_type,
            content = row.content,
            payload_json = row.payload_json,
            add_time = row.add_time.ToString("s"),
        }).ToList();
    }

    private AssistantRunDto? mapRun(AssistantRuns.Row? run)
    {
        if (run == null)
            return null;

        int duration = 0;
        if (run.started_at.HasValue)
        {
            var end = run.completed_at ?? DateTime.Now;
            duration = Math.Max(0, (int)(end - run.started_at.Value).TotalSeconds);
        }

        return new AssistantRunDto
        {
            id = run.id,
            assistant_threads_id = run.assistant_threads_id,
            assistant_messages_id = run.assistant_messages_id,
            status_id = run.status,
            status = AssistantRuns.StatusToCode(run.status),
            error_message = run.error_message,
            started_at = formatDate(run.started_at),
            completed_at = formatDate(run.completed_at),
            duration_seconds = duration,
            clarification = decodeClarification(run.clarification_json, AssistantMessages.TYPE_CLARIFICATION),
        };
    }

    private static List<AssistantSource> decodeSources(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<AssistantSource>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static AssistantClarificationDto? decodeClarification(string json, string messageType)
    {
        if (messageType != AssistantMessages.TYPE_CLARIFICATION || string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<AssistantClarificationDto>(json);
        }
        catch
        {
            return null;
        }
    }

    private string buildShareUrl(string icode)
    {
        string root = fw.config("ROOT_DOMAIN").toStr().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(root))
            root = "";
        return root + "/Assistant?share=" + Uri.EscapeDataString(icode ?? string.Empty);
    }

    private string formatDate(DateTime? value)
    {
        if (!value.HasValue)
            return string.Empty;
        return fw.formatUserDateTime(value.Value);
    }

    private static string buildDefaultThreadName(string prompt)
    {
        var value = AssistantMessages.buildPreviewText(prompt ?? string.Empty, 80);
        return string.IsNullOrWhiteSpace(value) ? "New chat" : value;
    }

    private static string buildUserMessageContent(string prompt, FwDict? clarificationAnswers, bool hasFiles)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(prompt))
            parts.Add(prompt.Trim());
        if (clarificationAnswers != null && clarificationAnswers.Count > 0)
            parts.Add("Clarification answers:\n```json\n" + Utils.jsonEncode(clarificationAnswers, true) + "\n```");
        if (hasFiles)
            parts.Add("Files were uploaded with this message.");
        return string.Join("\n\n", parts);
    }
}
