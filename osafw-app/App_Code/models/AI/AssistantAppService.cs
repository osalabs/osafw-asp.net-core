using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace osafw;

public sealed class AssistantAppService
{
    internal const string ANONYMOUS_OWNER_SESSION_KEY = "assistant_owner_token";
    private const int DEFAULT_MAX_FILES_PER_MESSAGE = 5;

    private readonly FW fw;

    private sealed record AssistantOwnerScope(int usersId, string ownerToken);

    public AssistantAppService(FW fw)
    {
        this.fw = fw;
    }

    public AssistantRuntimeStatus RuntimeStatus()
    {
        bool isAssistantEnabled = fw.model<Settings>().readBool("ASSISTANT_ENABLED");
        bool isAssistantTablesReady = isTablesReady();
        bool isOpenAiConfigured = fw.model<LLM>().isConfigured();
        bool isWorkerEnabled = fw.config("ASSISTANT_WORKER_ENABLED").toBool();
        string message = "";

        if (!isAssistantEnabled)
            message = "Assistant is disabled.";
        else if (!isOpenAiConfigured)
            message = "Please contact administrator to configure AI Assistant.";
        else if (!isAssistantTablesReady)
            message = "Assistant tables are not installed.";
        else if (!isWorkerEnabled)
            message = "Assistant worker is not enabled. Enable appSettings.ASSISTANT_WORKER_ENABLED on a host that should process assistant chat runs.";

        return new AssistantRuntimeStatus
        {
            enabled = isAssistantEnabled,
            tables_ready = isAssistantTablesReady,
            openai_configured = isOpenAiConfigured,
            worker_enabled = isWorkerEnabled,
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
        return mapThread(thread, messages, [], run, owner, includeEvents: false);
    }

    public AssistantPollingResponse PollThread(int threadId, int usersId, string sharedIcode = "", int lastMessageId = 0, int lastEventId = 0)
    {
        ensureTablesReady();
        var owner = resolveOwnerScope(usersId, false);
        bool isSharedAccess = !string.IsNullOrWhiteSpace(sharedIcode);
        var thread = isSharedAccess
            ? requireSharedThread(sharedIcode)
            : requireThread(threadId, owner);
        var run = fw.model<AssistantRuns>().latestByThread(thread.id);
        var messages = fw.model<AssistantMessages>().listByThread(thread.id, lastMessageId);
        var events = !isSharedAccess && run != null
            ? fw.model<AssistantRunsEvents>().listByRun(run.id, lastEventId)
            : [];

        return new AssistantPollingResponse
        {
            thread = mapThreadState(thread, run, owner),
            run = mapRun(run),
            messages = mapMessages(messages),
            events = mapEvents(events),
            last_message_id = messages.Count > 0 ? messages[^1].id : lastMessageId,
            last_event_id = !isSharedAccess && events.Count > 0 ? events[^1].id : lastEventId,
        };
    }

    public List<AssistantThreadPreviewDto> ListHistory(int usersId, string search = "")
    {
        if (!isTablesReady())
            return [];

        var owner = resolveOwnerScope(usersId, true);
        var rows = fw.model<AssistantThreads>().listHistoryByOwner(owner.usersId, owner.ownerToken, search, 50);
        var previewMap = fw.model<AssistantMessages>().listFirstUserPreviewByThreadIds(rows.Select(static row => row.id));
        var previews = new List<AssistantThreadPreviewDto>(rows.Count);
        foreach (var row in rows)
        {
            string title = string.IsNullOrWhiteSpace(row.iname) ? "New chat" : row.iname;
            string preview = previewMap.TryGetValue(row.id, out var previewText) ? previewText : string.Empty;
            if (isRedundantHistoryPreview(title, preview))
                preview = string.Empty;

            previews.Add(new AssistantThreadPreviewDto
            {
                id = row.id,
                iname = title,
                preview = preview,
                last_message_at = formatDate(row.last_message_at ?? row.add_time),
                last_run_status_id = row.last_run_status,
                last_run_status = AssistantRuns.StatusToCode(row.last_run_status),
                is_shared = !string.IsNullOrWhiteSpace(row.icode),
            });
        }
        return previews;
    }

    private static bool isRedundantHistoryPreview(string title, string preview)
    {
        string normalizedTitle = compactHistoryText(title);
        string normalizedPreview = compactHistoryText(preview);
        if (normalizedTitle.Length == 0 || normalizedPreview.Length == 0)
            return false;

        return string.Equals(normalizedTitle, normalizedPreview, StringComparison.OrdinalIgnoreCase)
            || normalizedPreview.StartsWith(normalizedTitle, StringComparison.OrdinalIgnoreCase)
            || normalizedTitle.StartsWith(normalizedPreview, StringComparison.OrdinalIgnoreCase);
    }

    private static string compactHistoryText(string value)
    {
        return string.Join(" ", (value ?? string.Empty).Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
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
        if (!status.enabled || !status.tables_ready || !status.openai_configured || !status.worker_enabled)
            throw new UserException(status.message);

        bool isTextProvided = !string.IsNullOrWhiteSpace(prompt);
        bool isClarificationProvided = clarificationAnswers != null && clarificationAnswers.Count > 0;
        bool isFilesProvided = files != null && files.Count > 0;
        if (!isTextProvided && !isClarificationProvided && !isFilesProvided)
            throw new UserException("Please enter a message.");
        if (isFilesProvided)
            validateAssistantFiles(files!);

        var owner = resolveOwnerScope(usersId, true);
        AssistantThreads.Row? existingThread = null;
        if (threadId > 0)
            existingThread = requireThread(threadId, owner);

        int effectiveThreadId = existingThread?.id
            ?? fw.model<AssistantThreads>().addThread(usersId, owner.ownerToken, buildDefaultThreadName(prompt));

        if (fw.model<AssistantRuns>().queuedOrProcessingByThread(effectiveThreadId) != null)
            throw new UserException("Assistant response is already queued.");

        string userContent = buildUserMessageContent(prompt, clarificationAnswers, isFilesProvided);
        int messageId = fw.model<AssistantMessages>().addMessage(
            effectiveThreadId,
            AssistantMessages.ROLE_USER,
            AssistantMessages.TYPE_MESSAGE,
            userContent,
            usersId: usersId
        );

        if (isFilesProvided)
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

    public (AssistantThreadDto thread, AssistantRunDto run) RetryLastResponse(int usersId, int threadId)
    {
        var status = RuntimeStatus();
        if (!status.enabled || !status.tables_ready || !status.openai_configured || !status.worker_enabled)
            throw new UserException(status.message);

        var owner = resolveOwnerScope(usersId, false);
        var thread = requireThread(threadId, owner);
        if (!isOwnerThread(thread, owner))
            throw new AuthException("Assistant thread not found.");

        var activeRun = fw.model<AssistantRuns>().queuedOrProcessingByThread(thread.id);
        if (activeRun != null)
            throw new UserException("Assistant response is already queued.");

        var userMessage = fw.model<AssistantMessages>().latestByThread(thread.id, AssistantMessages.ROLE_USER)
            ?? throw new UserException("Assistant user message not found.");

        int runId = fw.model<AssistantRuns>().queueRun(thread.id, userMessage.id);
        fw.model<AssistantThreads>().touch(thread.id);
        fw.model<AssistantThreads>().updateLastRunStatus(thread.id, AssistantRuns.STATUS_QUEUED);
        fw.model<AssistantRunsEvents>().addEvent(runId, AssistantRunsEvents.TYPE_STATUS, "Queued");

        var run = fw.model<AssistantRuns>().oneTyped(runId) ?? throw new ApplicationException("Assistant run was not saved.");
        var refreshedThread = requireThread(thread.id, owner);
        var messages = fw.model<AssistantMessages>().listByThread(thread.id);
        return (mapThread(refreshedThread, messages, [], run, owner), mapRun(run)!);
    }

    public void SubmitFeedback(int usersId, int threadId, int runId, int messageId, string feedbackType, string comment)
    {
        ensureTablesReady();
        if (string.IsNullOrWhiteSpace(feedbackType) && string.IsNullOrWhiteSpace(comment))
            throw new UserException("Feedback cannot be empty.");

        AssistantMessages.Row? message = messageId > 0
            ? fw.model<AssistantMessages>().oneTyped(messageId)
            : null;
        if (messageId > 0 && message == null)
            throw new UserException("Assistant message not found.");

        int effectiveThreadId = threadId > 0 ? threadId : message?.assistant_threads_id ?? 0;
        AssistantRuns.Row? requestedRun = null;
        if (messageId <= 0 && runId > 0)
        {
            requestedRun = fw.model<AssistantRuns>().oneTyped(runId)
                ?? throw new UserException("Assistant run not found.");
            if (effectiveThreadId <= 0)
                effectiveThreadId = requestedRun.assistant_threads_id;
        }

        if (effectiveThreadId > 0)
        {
            var owner = resolveOwnerScope(usersId, false);
            _ = requireThread(effectiveThreadId, owner);
        }
        if (message != null && effectiveThreadId > 0 && message.assistant_threads_id != effectiveThreadId)
            throw new UserException("Assistant message not found.");
        if (requestedRun != null && effectiveThreadId > 0 && requestedRun.assistant_threads_id != effectiveThreadId)
            throw new UserException("Assistant run not found.");

        int resolvedRunId = 0;
        if (messageId > 0)
        {
            resolvedRunId = fw.model<AssistantRuns>().idByResultMessage(messageId);
            if (resolvedRunId <= 0)
                throw new UserException("Assistant response run not found.");
        }
        else
        {
            resolvedRunId = requestedRun?.id ?? 0;
        }

        fw.model<AssistantFeedback>().addFeedback(usersId, effectiveThreadId, resolvedRunId, messageId, feedbackType, comment);
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

    public void BindSourcesToRunEvidence(int runId, AssistantResult result)
    {
        if (result == null)
            return;

        var evidence = listRunEvidenceSources(runId);
        if (evidence.Count == 0)
        {
            result.sources = [];
            downgradeUncitedConfidence(result);
            return;
        }

        List<AssistantSource> valid = [];
        foreach (var source in result.sources ?? new List<AssistantSource>())
        {
            if (!tryFindEvidenceSource(evidence, source, out var bound))
                continue;

            bindSourceToEvidence(source, bound);
            valid.Add(source);
        }

        result.sources = valid
            .GroupBy(static source => (source.source_id.GetValueOrDefault(), source.chunk_id.GetValueOrDefault()))
            .Select(static group => group.First())
            .ToList();
        if (result.sources.Count == 0)
            downgradeUncitedConfidence(result);
    }

    public void BindLinksToRunNavigation(int runId, AssistantResult result)
    {
        if (result == null)
            return;

        var navigation = listRunNavigationLinks(runId);
        if (navigation.Count == 0)
        {
            result.links = [];
            return;
        }

        List<AssistantLink> valid = [];
        foreach (var link in result.links ?? new List<AssistantLink>())
        {
            if (!navigation.TryGetValue((link.url ?? string.Empty).Trim(), out var bound))
                continue;

            valid.Add(new AssistantLink
            {
                label = string.IsNullOrWhiteSpace(bound.label) ? link.label : bound.label,
                url = bound.url,
                description = string.IsNullOrWhiteSpace(bound.description) ? link.description : bound.description,
                action = string.IsNullOrWhiteSpace(bound.action) ? link.action : bound.action,
                confidence = bound.confidence ?? link.confidence
            });
        }

        result.links = valid
            .GroupBy(static link => link.url, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToList();
    }

    private bool isTablesReady()
    {
        try
        {
            var tables = dbTables();
            return requiredTables().All(required => tables.Contains(required, StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private string[] requiredTables()
    {
        return
        [
            fw.model<AssistantThreads>().table_name,
            fw.model<AssistantMessages>().table_name,
            fw.model<AssistantRuns>().table_name,
            fw.model<AssistantRunsEvents>().table_name,
            fw.model<AssistantFeedback>().table_name,
            fw.model<KBArticles>().table_name,
            fw.model<RagSources>().table_name,
            fw.model<RagChunks>().table_name
        ];
    }

    private static void bindSourceToEvidence(AssistantSource source, AssistantSource bound)
    {
        string trustedName = bound.name ?? string.Empty;
        string trustedUrl = bound.url ?? string.Empty;

        source.source_id = bound.source_id;
        source.chunk_id = bound.chunk_id;
        source.source_type = bound.source_type ?? string.Empty;
        source.name = trustedName;
        source.url = trustedUrl;
        source.article_name = trustedName;
        source.article_url = trustedUrl;
        source.filename = trustedName;
        source.file_url = trustedUrl;
        source.page = bound.page;
        source.section = bound.section ?? string.Empty;
        source.score = bound.score;
    }

    private Dictionary<string, AssistantSource> listRunEvidenceSources(int runId)
    {
        if (runId <= 0)
            return [];

        Dictionary<string, AssistantSource> result = [];
        foreach (var row in fw.model<AssistantRunsEvents>().listByRun(runId))
        {
            if (row.event_type != AssistantRunsEvents.TYPE_EVIDENCE || string.IsNullOrWhiteSpace(row.payload_json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(row.payload_json);
                if (!doc.RootElement.TryGetProperty("evidence", out var evidence) || evidence.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in evidence.EnumerateArray())
                {
                    int sourceId = jsonInt(item, "source_id");
                    int chunkId = jsonInt(item, "chunk_id");
                    if (sourceId <= 0 && chunkId <= 0)
                        continue;

                    var source = new AssistantSource
                    {
                        source_id = sourceId,
                        chunk_id = chunkId,
                        source_type = jsonString(item, "source_type"),
                        name = jsonString(item, "title"),
                        url = jsonString(item, "url"),
                        page = jsonInt(item, "page"),
                        section = jsonString(item, "section"),
                        score = jsonDouble(item, "score")
                    };
                    result[evidenceKey(sourceId, chunkId)] = source;
                    if (sourceId > 0)
                        result.TryAdd(evidenceKey(sourceId, 0), source);
                }
            }
            catch
            {
                // Ignore malformed evidence events; they should not make citations valid.
            }
        }

        return result;
    }

    private Dictionary<string, AssistantLink> listRunNavigationLinks(int runId)
    {
        if (runId <= 0)
            return [];

        Dictionary<string, AssistantLink> result = new(StringComparer.Ordinal);
        foreach (var row in fw.model<AssistantRunsEvents>().listByRun(runId))
        {
            if (row.event_type != AssistantRunsEvents.TYPE_NAVIGATION || string.IsNullOrWhiteSpace(row.payload_json))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(row.payload_json);
                if (!doc.RootElement.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var item in links.EnumerateArray())
                {
                    string url = jsonString(item, "url").Trim();
                    if (!AssistantNavigationCatalog.IsAppLocalUrl(url))
                        continue;

                    result[url] = new AssistantLink
                    {
                        label = jsonString(item, "label"),
                        url = url,
                        description = jsonString(item, "description"),
                        action = jsonString(item, "action"),
                        confidence = jsonDouble(item, "score")
                    };
                }
            }
            catch
            {
                // Ignore malformed navigation events; they should not make model-invented links valid.
            }
        }

        return result;
    }

    private static bool tryFindEvidenceSource(Dictionary<string, AssistantSource> evidence, AssistantSource source, out AssistantSource bound)
    {
        int sourceId = source.source_id.GetValueOrDefault();
        int chunkId = source.chunk_id.GetValueOrDefault();
        if (evidence.TryGetValue(evidenceKey(sourceId, chunkId), out var exact))
        {
            bound = exact;
            return true;
        }
        if (sourceId > 0 && evidence.TryGetValue(evidenceKey(sourceId, 0), out var sourceOnly))
        {
            bound = sourceOnly;
            return true;
        }

        bound = new AssistantSource();
        return false;
    }

    private static string evidenceKey(int sourceId, int chunkId)
    {
        return sourceId + ":" + chunkId;
    }

    private static void downgradeUncitedConfidence(AssistantResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.information) || !string.IsNullOrWhiteSpace(result.explanation))
            result.confidence = Math.Min(result.confidence, 0.25);
    }

    private static string jsonString(JsonElement item, string name)
    {
        return item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static int jsonInt(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
            return 0;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out int result) => result,
            JsonValueKind.String when int.TryParse(value.GetString(), out int result) => result,
            _ => 0
        };
    }

    private static double jsonDouble(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value))
            return 0;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out double result) => result,
            JsonValueKind.String when double.TryParse(value.GetString(), out double result) => result,
            _ => 0
        };
    }

    private HashSet<string> dbTables()
    {
        return fw.db.tables().Select(static table => table.ToString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void ensureTablesReady()
    {
        if (!isTablesReady())
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
        string ownerToken = fw.Session(ANONYMOUS_OWNER_SESSION_KEY);
        if (usersId <= 0 && string.IsNullOrWhiteSpace(ownerToken) && createAnonymousToken)
        {
            ownerToken = Guid.NewGuid().ToString("N");
            fw.Session(ANONYMOUS_OWNER_SESSION_KEY, ownerToken);
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

        string ext = Path.GetExtension(file.FileName);
        var embeddingService = new DocumentEmbeddingService(fw);
        if (embeddingService.isAttachmentIndexable(ext, file.Length))
        {
            try
            {
                fw.model<RagSources>().queueAssistantUpload(attId, FwEntities.ICODE_ASSISTANT_MESSAGE, messageId);
                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                fw.logger(LogLevel.WARN, "Assistant attachment indexing queue failed:", ex.Message);
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
        int maxFiles = Math.Max(1, fw.model<Settings>().readInt("ASSISTANT_MAX_FILES_PER_MESSAGE", DEFAULT_MAX_FILES_PER_MESSAGE));
        int count = files.Count(file => file != null && file.Length > 0);
        if (count > maxFiles)
            throw new UserException("Upload up to " + maxFiles + " files per assistant message.");
    }

    private long maxIndexedFileBytes()
    {
        return new DocumentEmbeddingService(fw).MaxIndexedFileBytes();
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
        var indexedIds = fw.model<RagChunks>().listIndexedEntityItemIds(FwEntities.ICODE_ASSISTANT_MESSAGE, ids);
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

    private AssistantThreadDto mapThread(AssistantThreads.Row thread, List<AssistantMessages.Row> messages, List<AssistantRunsEvents.Row> events, AssistantRuns.Row? run, AssistantOwnerScope owner, bool includeEvents = true)
    {
        var messageIds = messages.Select(static message => message.id).ToList();
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
            events = includeEvents ? mapEvents(events) : [],
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
        var messageIds = messages.Select(static message => message.id).ToList();
        attachments ??= loadAttachmentsByMessageId(messageIds);
        var runIdsByMessageId = fw.model<AssistantRuns>().listResultRunIdsByMessageIds(messageIds);
        return messages.Select(message => mapMessage(message, attachments, runIdsByMessageId)).ToList();
    }

    private AssistantMessageDto mapMessage(AssistantMessages.Row message, Dictionary<int, List<AssistantAttachmentDto>> attachments, Dictionary<int, int>? runIdsByMessageId = null)
    {
        int runId = runIdsByMessageId != null && runIdsByMessageId.TryGetValue(message.id, out int mappedRunId)
            ? mappedRunId
            : 0;

        return new AssistantMessageDto
        {
            id = message.id,
            assistant_runs_id = runId,
            role = message.role,
            message_type = message.message_type,
            content_markdown = message.content_markdown,
            add_time = formatDate(message.add_time),
            confidence = message.confidence,
            sources = decodeSources(message.sources_json),
            links = decodeLinks(message.payload_json, message.message_type),
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

    private static List<AssistantLink> decodeLinks(string json, string messageType)
    {
        if (messageType != AssistantMessages.TYPE_RESULT || string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<AssistantResult>(json)?.links ?? [];
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

    private string buildUserMessageContent(string prompt, FwDict? clarificationAnswers, bool isFilesProvided)
    {
        return fw.parsePage("/assistant/prompts", "user_message.md", new FwDict
        {
            { "prompt", prompt.Trim() },
            { "clarification_json", clarificationAnswers != null && clarificationAnswers.Count > 0 ? Utils.jsonEncode(clarificationAnswers, true) : string.Empty },
            { "has_files", isFilesProvided ? "1" : string.Empty },
        }).Trim();
    }
}
