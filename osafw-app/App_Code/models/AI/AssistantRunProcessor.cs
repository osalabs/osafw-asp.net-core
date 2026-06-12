using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

public sealed class AssistantRunProcessor
{
    private readonly IConfiguration configuration;
    private readonly ILoggerFactory loggerFactory;

    public AssistantRunProcessor(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        this.configuration = configuration;
        this.loggerFactory = loggerFactory;
    }

    public async Task<bool> ProcessNextQueuedRunAsync(string workerId, CancellationToken cancellationToken, bool recoverStaleRuns = false)
    {
        using var fw = FW.initOffline(configuration);
        if (!fw.model<Settings>().readBool("ASSISTANT_ENABLED"))
            return false;

        if (recoverStaleRuns)
        {
            int recoveredCount = fw.model<AssistantRuns>().requeueStaleProcessingRuns();
            if (recoveredCount > 0)
                fw.logger(LogLevel.WARN, "Recovered stale assistant runs: ", recoveredCount);
        }

        var run = fw.model<AssistantRuns>().claimNextQueued(workerId);
        if (run == null || run.id <= 0)
            return false;

        try
        {
            await ProcessClaimedRunAsync(fw, run, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            fw.endRequest();
        }

        return true;
    }

    public async Task<bool> ProcessNextQueuedSourceAsync(string workerId, CancellationToken cancellationToken)
    {
        using var fw = FW.initOffline(configuration);
        if (!fw.model<Settings>().readBool("ASSISTANT_ENABLED") || !fw.model<LLM>().isConfigured())
            return false;

        try
        {
            return await new DocumentEmbeddingService(fw).ProcessNextQueuedSourceAsync(workerId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            fw.endRequest();
        }
    }

    public async Task ProcessClaimedRunAsync(FW fw, AssistantRuns.Row run, CancellationToken cancellationToken)
    {
        var thread = fw.model<AssistantThreads>().oneTyped(run.assistant_threads_id)
            ?? throw new ApplicationException("Assistant thread not found.");
        var userMessage = fw.model<AssistantMessages>().oneTyped(run.assistant_messages_id)
            ?? throw new ApplicationException("Assistant user message not found.");

        initializeUserSession(fw, thread.users_id.GetValueOrDefault());

        var appService = new AssistantAppService(fw);
        string instructions = BuildChatInstructions(fw, thread.users_id.GetValueOrDefault());
        var runtime = new AssistantToolRuntime(fw, thread.id, run.id, thread.users_id.GetValueOrDefault());
        IList<AITool> tools = new AssistantToolCatalog(runtime).Build().Select(static registration => registration.Tool).ToList();

        string apiKey = fw.model<Settings>().read("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new UserException("OpenAI API key is not configured.");

        string model = fw.model<Settings>().read("ASSISTANT_MODEL", LLM.MODEL_GPT5_MINI);
        if (string.IsNullOrWhiteSpace(model))
            model = LLM.MODEL_GPT5_MINI;

        var openAiClient = new OpenAIClient(apiKey);
#pragma warning disable OPENAI001
        ResponsesClient responsesClient = openAiClient.GetResponsesClient();
        ChatClientAgent agent = responsesClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "Assistant",
                Description = "Read-only framework knowledge assistant",
                ChatOptions = new ChatOptions
                {
                    Tools = tools,
                    ToolMode = ChatToolMode.Auto,
                    AllowMultipleToolCalls = true,
                }
            },
            model,
            null,
            loggerFactory,
            null
        );
#pragma warning restore OPENAI001

        var messages = await buildAgentMessagesAsync(fw, thread.id, instructions, cancellationToken).ConfigureAwait(false);
        AgentSession? session = await loadSessionAsync(agent, thread.provider_thread_id, cancellationToken).ConfigureAwait(false);

        fw.model<AssistantRunsEvents>().addEvent(run.id, AssistantRunsEvents.TYPE_STATUS, "Processing");
        fw.model<AssistantThreads>().updateLastRunStatus(thread.id, AssistantRuns.STATUS_PROCESSING);
        fw.logger(LogLevel.DEBUG, "Assistant run start: model=", model, ", thread_id=", thread.id, ", run_id=", run.id, ", messages=", messages.Count, ", tools=", tools.Count);

        try
        {
            var response = await agent.RunAsync<AssistantResult>(
                messages,
                session,
                null,
                new ChatClientAgentRunOptions
                {
                    ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        System.Text.Json.JsonDocument.Parse(AssistantResult.JsonSchema).RootElement.Clone(),
                        "assistant_result",
                        "Assistant result with answer and citations.")
                },
                cancellationToken
            ).ConfigureAwait(false);

            persistSession(thread.id, resolveSession(response, session), fw);
            var appResult = extractResponseValue<AssistantResult>(response) ?? fallbackResult(response);
            appService.BindSourcesToRunEvidence(run.id, appResult);
            appService.EnrichAssistantSources(appResult.sources);

            string payloadJson = Utils.jsonEncode(appResult);
            string sourcesJson = Utils.jsonEncode(appResult.sources);
            int resultMessageId = fw.model<AssistantMessages>().addMessage(
                thread.id,
                AssistantMessages.ROLE_ASSISTANT,
                AssistantMessages.TYPE_RESULT,
                string.IsNullOrWhiteSpace(appResult.information) ? appResult.explanation : appResult.information,
                payloadJson: payloadJson,
                sourcesJson: sourcesJson,
                confidence: appResult.confidence,
                usersId: thread.users_id.GetValueOrDefault()
            );

            int activityLogsId = fw.model<FwActivityLogs>().addSimple(
                FwLogTypes.ICODE_ADDED,
                FwEntities.ICODE_ASSISTANT,
                thread.id,
                buildActivityLogDescription(userMessage.preview_text),
                new FwDict
                {
                    { "assistant_threads_id", thread.id },
                    { "assistant_runs_id", run.id },
                    { "status", AssistantRuns.StatusToCode(AssistantRuns.STATUS_COMPLETED) }
                }
            );

            fw.model<AssistantRuns>().markCompleted(run.id, resultMessageId, activityLogsId);
            fw.model<AssistantThreads>().updateLastRunStatus(thread.id, AssistantRuns.STATUS_COMPLETED);
            fw.model<AssistantThreads>().updateInameIfDefault(thread.id, appResult.title);
            fw.model<AssistantRunsEvents>().addEvent(run.id, AssistantRunsEvents.TYPE_STATUS, "Completed");
            updateUserMemoryIfEnabled(fw, thread.id, thread.users_id.GetValueOrDefault());
        }
        catch (AssistantClarificationRequestedException ex)
        {
            string clarificationJson = Utils.jsonEncode(ex.Clarification);
            _ = fw.model<AssistantMessages>().addMessage(
                thread.id,
                AssistantMessages.ROLE_ASSISTANT,
                AssistantMessages.TYPE_CLARIFICATION,
                ex.Clarification.prompt,
                payloadJson: clarificationJson,
                usersId: thread.users_id.GetValueOrDefault()
            );
            fw.model<AssistantRuns>().markWaitingForUser(run.id, clarificationJson);
            fw.model<AssistantThreads>().updateLastRunStatus(thread.id, AssistantRuns.STATUS_WAITING_FOR_USER);
            fw.model<AssistantRunsEvents>().addEvent(run.id, AssistantRunsEvents.TYPE_STATUS, "Waiting for user");
        }
        catch (Exception ex)
        {
            string message = ex is UserException ? ex.Message : "Assistant run failed. Try again later.";
            fw.model<AssistantRuns>().markFailed(run.id, message);
            fw.model<AssistantThreads>().updateLastRunStatus(thread.id, AssistantRuns.STATUS_FAILED);
            fw.model<AssistantRunsEvents>().addEvent(run.id, AssistantRunsEvents.TYPE_ERROR, message);
            fw.logger(LogLevel.ERROR, "Assistant run failed:", ex.Message);
        }
    }

    public static string BuildChatInstructions(FW fw, int usersId)
    {
        AssistantMemories.Row? memory = null;
        if (fw.model<Settings>().readBool("ASSISTANT_MEMORY_ENABLED") && usersId > 0)
            memory = fw.model<AssistantMemories>().oneByUser(usersId);

        var ps = new FwDict
        {
            { "current_time", DateTime.Now },
            { "users_id", usersId },
            { "memory_summary", memory?.summary ?? string.Empty },
            { "memory_terminology_json", memory?.terminology_json ?? string.Empty },
            { "memory_preferences_json", memory?.preferences_json ?? string.Empty },
        };

        return string.Join("\n\n",
            fw.parsePage("/assistant", "chat_system.md", ps),
            fw.parsePage("/assistant", "tool_policy.md", ps),
            fw.parsePage("/assistant", "clarification_prompt.md", ps)
        );
    }

    private static void initializeUserSession(FW fw, int usersId)
    {
        if (usersId <= 0)
            return;

        var user = fw.model<Users>().one(usersId);
        if (user.Count == 0)
            return;

        fw.Session("user_id", usersId.ToString());
        fw.Session("access_level", user["access_level"]);
        if (!Utils.isEmpty(user["login"]))
            fw.Session("login", user["login"]);
        if (!Utils.isEmpty(user["timezone"]))
            fw.Session("timezone", user["timezone"]);
        if (!Utils.isEmpty(user["date_format"]))
            fw.Session("date_format", user["date_format"]);
        if (!Utils.isEmpty(user["time_format"]))
            fw.Session("time_format", user["time_format"]);
    }

    private static async Task<List<Microsoft.Extensions.AI.ChatMessage>> buildAgentMessagesAsync(FW fw, int threadId, string instructions, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, instructions)
        };

        var threadMessages = fw.model<AssistantMessages>().listByThread(threadId);
        foreach (var message in threadMessages)
        {
            if (message.role != AssistantMessages.ROLE_USER && message.role != AssistantMessages.ROLE_ASSISTANT)
                continue;

            string text = !string.IsNullOrWhiteSpace(message.content_markdown)
                ? message.content_markdown
                : message.preview_text;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            messages.Add(new Microsoft.Extensions.AI.ChatMessage(
                message.role == AssistantMessages.ROLE_ASSISTANT ? ChatRole.Assistant : ChatRole.User,
                text
            ));
        }

        return messages;
    }

    private static async Task<AgentSession?> loadSessionAsync(ChatClientAgent agent, string providerThreadId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerThreadId))
            return null;

        return await agent.CreateSessionAsync(providerThreadId, cancellationToken).ConfigureAwait(false);
    }

    private static void persistSession(int threadId, AgentSession? session, FW fw)
    {
        if (session is not ChatClientAgentSession chatSession || string.IsNullOrWhiteSpace(chatSession.ConversationId))
            return;

        fw.model<AssistantThreads>().updateProviderThreadId(threadId, chatSession.ConversationId);
    }

    private static AgentSession? resolveSession(object? response, AgentSession? fallbackSession)
    {
        if (response == null)
            return fallbackSession;

        var sessionProperty = response.GetType().GetProperty("Session", BindingFlags.Public | BindingFlags.Instance);
        if (sessionProperty?.GetValue(response) is AgentSession responseSession)
            return responseSession;

        return fallbackSession;
    }

    private static T? extractResponseValue<T>(object? response)
    {
        if (response == null)
            return default;

        var type = response.GetType();
        foreach (string name in new[] { "Value", "Result", "Response" })
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(response) is T typed)
                return typed;
        }

        return default;
    }

    private static AssistantResult fallbackResult(object? response)
    {
        string text = string.Empty;
        if (response != null)
        {
            var prop = response.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
            text = prop?.GetValue(response)?.ToString() ?? string.Empty;
        }

        return new AssistantResult
        {
            title = "Assistant response",
            explanation = string.Empty,
            information = text,
            confidence = 0,
            sources = []
        };
    }

    private static void updateUserMemoryIfEnabled(FW fw, int threadId, int usersId)
    {
        if (!fw.model<Settings>().readBool("ASSISTANT_MEMORY_ENABLED") || usersId <= 0)
            return;

        var messages = fw.model<AssistantMessages>().listByThread(threadId)
            .Where(static message => message.role == AssistantMessages.ROLE_USER || message.role == AssistantMessages.ROLE_ASSISTANT)
            .TakeLast(12)
            .Select(static message => message.role + ": " + AssistantMessages.buildPreviewText(message.content_markdown, 1200))
            .ToList();
        if (messages.Count == 0)
            return;

        var existing = fw.model<AssistantMemories>().oneByUser(usersId);
        string systemPrompt = fw.parsePage("/assistant", "memory_compaction.md", []);
        string userPrompt = "Existing memory:\n" + (existing?.summary ?? string.Empty)
            + "\n\nConversation excerpts:\n" + string.Join("\n", messages)
            + "\n\nReturn only durable preferences, terminology, and stable context worth remembering.";
        string model = fw.model<Settings>().read("ASSISTANT_MODEL", LLM.MODEL_GPT5_MINI);

        try
        {
            var draft = fw.model<LLM>().responseJson<AssistantMemoryDraft>(
                string.IsNullOrWhiteSpace(model) ? LLM.MODEL_GPT5_MINI : model,
                systemPrompt,
                userPrompt,
                AssistantMemoryDraft.JsonSchema
            );
            if (draft == null)
                return;

            fw.model<AssistantMemories>().upsertForUser(
                usersId,
                draft.summary,
                Utils.jsonEncode(draft.terminology),
                Utils.jsonEncode(draft.preferences),
                sourceThreadId: threadId
            );
        }
        catch (Exception ex)
        {
            fw.logger(LogLevel.WARN, "Assistant memory compaction failed:", ex.Message);
        }
    }

    private static string buildActivityLogDescription(string prompt)
    {
        string value = (prompt ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        if (value.Length > 180)
            value = value[..180];

        return string.IsNullOrWhiteSpace(value) ? "Assistant run completed" : "Assistant run: " + value;
    }

    private sealed class AssistantMemoryDraft
    {
        public string summary { get; set; } = string.Empty;
        public Dictionary<string, string> terminology { get; set; } = [];
        public Dictionary<string, string> preferences { get; set; } = [];

        [JsonIgnore]
        public static string JsonSchema => """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "summary": { "type": "string" },
            "terminology": {
              "type": "object",
              "additionalProperties": { "type": "string" }
            },
            "preferences": {
              "type": "object",
              "additionalProperties": { "type": "string" }
            }
          },
          "required": ["summary", "terminology", "preferences"]
        }
        """;
    }
}
