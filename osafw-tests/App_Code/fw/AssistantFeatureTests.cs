using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace osafw.Tests;

[TestClass]
public class AssistantFeatureTests
{
    private sealed class AccessUsers : Users
    {
        private readonly int accessLevel;

        public AccessUsers(int accessLevel)
        {
            this.accessLevel = accessLevel;
        }

        public override bool isAccessLevel(int min_acl) => accessLevel >= min_acl;

        public override bool isReadOnly(int id = -1) => false;

        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private sealed class StaticSettings : Settings
    {
        private readonly Dictionary<string, string> values;

        public StaticSettings(Dictionary<string, string>? values = null)
        {
            this.values = values ?? [];
        }

        public override DBRow oneByIcode(string icode)
        {
            return values.TryGetValue(icode, out string? value)
                ? new DBRow(new FwDict { ["id"] = "1", ["icode"] = icode, ["ivalue"] = value })
                : [];
        }
    }

    [TestMethod]
    public void LLM_NormalizesFencedJsonWithoutCallingProvider()
    {
        string normalized = LLM.normalizeJsonResponse("""
        ```json
        {"answer":"ok"}
        ```
        """);

        Assert.AreEqual("{\"answer\":\"ok\"}", normalized);
    }

    [TestMethod]
    public void LLM_IsNotConfiguredWithoutOpenAiKey()
    {
        var fw = TestHelpers.CreateFw();
        registerSettings(fw);
        var llm = new LLM();
        llm.init(fw);

        Assert.IsFalse(llm.isConfigured());
    }

    [TestMethod]
    public void RagChunks_VectorModeJsonBypassesNativeDetection()
    {
        var fw = TestHelpers.CreateFw();
        registerSettings(fw, new Dictionary<string, string> { ["ASSISTANT_VECTOR_MODE"] = RagChunks.VECTOR_MODE_JSON });
        var chunks = new RagChunks();
        chunks.init(fw);

        string backend = invokePrivate<string>(chunks, "resolveVectorBackend", 1536);

        Assert.AreEqual(RagChunks.VECTOR_MODE_JSON, backend);
    }

    [TestMethod]
    public void RagChunks_JsonFallbackSqlUsesProviderJsonFunctions()
    {
        var fw = TestHelpers.CreateFw();
        var chunks = new RagChunks();
        chunks.init(fw);

        string sqlServer = invokePrivate<string>(chunks, "buildSqlServerJsonQuerySql", false, false);
        string mySql = invokePrivate<string>(chunks, "buildMySqlJsonQuerySql", false, false);
        string sqlite = invokePrivate<string>(chunks, "buildSqliteJsonQuerySql", false, false);
        string sqlServerIdsOnly = invokePrivate<string>(chunks, "buildSqlServerJsonQuerySql", true, false);
        string sqlServerIdsOnlyWithScore = invokePrivate<string>(chunks, "buildSqlServerJsonQuerySql", true, true);
        string mySqlIdsOnlyWithScore = invokePrivate<string>(chunks, "buildMySqlJsonQuerySql", true, true);

        StringAssert.Contains(sqlServer.ToLowerInvariant(), "openjson");
        StringAssert.Contains(mySql.ToLowerInvariant(), "json_table");
        StringAssert.Contains(sqlite.ToLowerInvariant(), "json_each");
        StringAssert.Contains(sqlServer, "CosineSim");
        StringAssert.Contains(mySql, "CosineSim");
        StringAssert.Contains(sqlite, "CosineSim");
        Assert.IsFalse(sqlServerIdsOnly.Contains("source_title"));
        StringAssert.Contains(sqlServerIdsOnlyWithScore, ">= @MinScore");
        StringAssert.Contains(mySqlIdsOnlyWithScore.ToLowerInvariant(), " having ");
        StringAssert.Contains(mySqlIdsOnlyWithScore, ">= @MinScore");
    }

    [TestMethod]
    public void RagChunks_CosineSimilarityOrdersFixtureVectors()
    {
        var query = new List<float> { 1, 0 };
        var rows = new[]
        {
            new { Id = 2, Name = "far", Vector = new List<float> { 0, 1 } },
            new { Id = 1, Name = "near", Vector = new List<float> { 0.9f, 0.1f } },
            new { Id = 3, Name = "same", Vector = new List<float> { 1, 0 } },
        };

        var ordered = rows
            .OrderByDescending(row => RagChunks.cosineSimilarity(query, row.Vector))
            .ThenBy(row => row.Id)
            .Select(row => row.Name)
            .ToList();

        CollectionAssert.AreEqual(new[] { "same", "near", "far" }, ordered);
    }

    [TestMethod]
    public void DocumentEmbeddingService_TokenAwareChunksOverlapsLongText()
    {
        string text = string.Concat(Enumerable.Range(0, 240).Select(i => (char)('a' + (i % 26))));

        var chunks = DocumentEmbeddingService.TokenAwareChunks(text, maxTokens: 25, overlap: 5).ToList();

        Assert.IsTrue(chunks.Count > 1);
        Assert.IsTrue(chunks.All(chunk => chunk.Length <= 100));
        Assert.AreEqual(text.Substring(80, 20), chunks[1][..20]);
    }

    [TestMethod]
    public void DocumentEmbeddingService_isAttachmentIndexableHonorsConfiguredByteLimit()
    {
        var fw = TestHelpers.CreateFw();
        registerSettings(fw, new Dictionary<string, string> { ["ASSISTANT_MAX_INDEXED_FILE_BYTES"] = "10" });
        var service = new DocumentEmbeddingService(fw);

        Assert.IsTrue(service.isAttachmentIndexable(".txt", 10));
        Assert.IsFalse(service.isAttachmentIndexable(".txt", 11));
        Assert.IsFalse(service.isAttachmentIndexable(".exe", 1));
    }

    [TestMethod]
    public void DocumentEmbeddingService_TrimSummaryRemovesMarkdownFence()
    {
        string normalized = invokePrivateStatic<string>(typeof(DocumentEmbeddingService), "trimSummary", """
        ```markdown
        ## Summary

        Uploaded file summary.
        ```
        """);

        Assert.AreEqual("## Summary\r\n\r\nUploaded file summary.", normalized.Replace("\r\n", "\n").Replace("\n", "\r\n"));
    }

    [TestMethod]
    public void DocumentEmbeddingService_KBSummaryTemplatesUseUploadedFileDetails()
    {
        var documentType = typeof(DocumentEmbeddingService).GetNestedType("ParsedAttachmentDocument", BindingFlags.NonPublic);
        Assert.IsNotNull(documentType);
        var constructor = documentType!.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single(ctor => ctor.GetParameters().Length == 4);
        var documents = (IList)System.Activator.CreateInstance(typeof(List<>).MakeGenericType(documentType))!;
        documents.Add(constructor.Invoke(
        [
            7,
            "guide.txt",
            "Install the package. Configure settings. Restart the app.",
            new List<string> { "Install", "Configure" }
        ]));

        var fw = TestHelpers.CreateFw(new Dictionary<string, string?>
        {
            ["appSettings:template"] = Path.Combine(repoRoot(), "osafw-app", "App_Data", "template")
        });
        var service = new DocumentEmbeddingService(fw);

        string system = invokePrivate<string>(service, "renderKBAttachmentSummarySystemPrompt");
        string user = invokePrivate<string>(service, "renderKBAttachmentSummaryUserPrompt", "Guide", documents);
        string fallback = invokePrivate<string>(service, "renderKBAttachmentSummaryFallback", "Guide", documents);

        StringAssert.Contains(system, "Markdown summaries");
        StringAssert.Contains(user, "Create Markdown content");
        StringAssert.Contains(user, "guide.txt");
        StringAssert.Contains(user, "Install the package.");
        StringAssert.Contains(fallback, "guide.txt");
        StringAssert.Contains(fallback, "Install");
        StringAssert.Contains(fallback, "Configure");
        StringAssert.Contains(fallback, "Install the package.");
    }

    [TestMethod]
    public void KBArticles_DynamicConfigAllowsBlankContentAndAddsKbFileUploads()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Data", "template", "admin", "kbarticles", "config.json")));
        var root = doc.RootElement;

        Assert.AreEqual("iname", root.GetProperty("required_fields").GetString());

        var formFields = root.GetProperty("showform_fields").EnumerateArray().ToList();
        var content = formFields.Single(field => field.TryGetProperty("field", out var value) && value.GetString() == "content_markdown");
        Assert.IsFalse(content.TryGetProperty("required", out _));
        string contentClasses = content.GetProperty("class_control").GetString() ?? string.Empty;
        StringAssert.Contains(contentClasses, "markdown");
        StringAssert.Contains(contentClasses, "autoresize");
        string loadScript = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Data", "template", "admin", "kbarticles", "showform", "load_script.html"));
        StringAssert.Contains(loadScript, "<~/common/markdown_editor>");

        var upload = formFields.Single(field => field.TryGetProperty("field", out var value) && value.GetString() == "kb_files");
        Assert.AreEqual("att_files_edit", upload.GetProperty("type").GetString());
        Assert.AreEqual(AttCategories.CAT_GENERAL, upload.GetProperty("att_category").GetString());
        Assert.AreEqual("kb_files", upload.GetProperty("att_post_prefix").GetString());
        Assert.AreEqual(FwEntities.ICODE_KB, upload.GetProperty("fwentity").GetString());
        Assert.IsTrue(upload.GetProperty("multiple").GetBoolean());

        var showFiles = root.GetProperty("show_fields").EnumerateArray().Single(field => field.TryGetProperty("field", out var value) && value.GetString() == "kb_files");
        Assert.AreEqual("att_files", showFiles.GetProperty("type").GetString());
        Assert.AreEqual(AttCategories.CAT_GENERAL, showFiles.GetProperty("att_category").GetString());
    }

    [TestMethod]
    public void AdminKBArticles_SaveAttFilesOverridesDynamicEntityBinding()
    {
        var method = typeof(AdminKBArticlesController).GetMethod(nameof(AdminKBArticlesController.SaveAttFilesAction), [typeof(int)]);
        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(AdminKBArticlesController), method!.DeclaringType);

        string source = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "controllers", "AdminKBArticles.cs"));
        StringAssert.Contains(source, "idByIcodeOrAdd(FwEntities.ICODE_KB)");
        StringAssert.Contains(source, "model.queueReindex(id)");
        StringAssert.Contains(source, "syncKbFilesFromRequest(id)");
    }

    [TestMethod]
    public void KBArticles_BuildAccessWhereFiltersForNonSiteAdmins()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_MEMBER.ToString());
        var users = new AccessUsers(Users.ACL_MEMBER);
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var kb = new KBArticles();
        kb.init(fw);

        string where = kb.buildAccessWhere("k");

        Assert.AreEqual("k.access_level<=@current_access_level", where);
    }

    [TestMethod]
    public void KBArticles_BuildAccessWhereAllowsSiteAdmins()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_SITEADMIN.ToString());
        var users = new AccessUsers(Users.ACL_SITEADMIN);
        users.init(fw);
        TestHelpers.RegisterModel(fw, (Users)users);
        var kb = new KBArticles();
        kb.init(fw);

        Assert.AreEqual("1=1", kb.buildAccessWhere("k"));
    }

    [TestMethod]
    public void FW_OfflineSessionPreservesAccessLevelForBackgroundRuns()
    {
        var fw = FW.initOffline(new ConfigurationBuilder().Build());

        fw.Session("user_id", "42");
        fw.Session("access_level", Users.ACL_MANAGER.ToString());
        fw.SessionInt("assistant_test_int", 7);
        fw.SessionBool("assistant_test_bool", true);

        Assert.AreEqual(42, fw.userId);
        Assert.AreEqual(Users.ACL_MANAGER, fw.userAccessLevel);
        Assert.AreEqual(7, fw.SessionInt("assistant_test_int"));
        Assert.IsTrue(fw.SessionBool("assistant_test_bool"));

        fw.SessionRemove("access_level");

        Assert.AreEqual(0, fw.userAccessLevel);
    }

    [TestMethod]
    public void AssistantToolCatalog_DoesNotExposeGenericLookupTool()
    {
        var fw = TestHelpers.CreateFw();
        var runtime = new AssistantToolRuntime(fw, threadId: 1, runId: 1, userId: 1);

        var names = new AssistantToolCatalog(runtime).Build().Select(static tool => tool.Name).ToList();

        Assert.IsFalse(names.Contains("lookup_values"));
        CollectionAssert.Contains(names, "search_knowledge_base");
        CollectionAssert.Contains(names, "search_thread_attachments");
        CollectionAssert.Contains(names, "search_contacts");
        CollectionAssert.Contains(names, "find_app_navigation");
    }

    [TestMethod]
    public void AssistantNavigationCatalog_FiltersByAccessAndBuildsPrefillUrl()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_ADMIN.ToString());
        var catalog = AssistantNavigationCatalog.Parse("""
        {
          "version": 1,
          "controllers": [
            {
              "url": "/Admin/Users",
              "controller": "AdminUsers",
              "label": "Users",
              "description": "Manage users and employees.",
              "keywords": ["users", "employees"],
              "min_access_level": 90,
              "actions": ["list", "new"],
              "list_filters": [
                { "name": "status", "label": "Status", "type": "select", "options": { "0": "Active" } }
              ],
              "prefill_fields": [
                { "name": "fname", "label": "First Name", "type": "text" },
                { "name": "lname", "label": "Last Name", "type": "text" }
              ]
            },
            {
              "url": "/Admin/RagChunks",
              "controller": "AdminRagChunks",
              "label": "RAG Diagnostics",
              "description": "Site admin diagnostics.",
              "keywords": ["rag"],
              "min_access_level": 100,
              "actions": ["list"],
              "list_filters": [],
              "prefill_fields": []
            },
            {
              "url": "https://evil.example.test",
              "controller": "External",
              "label": "External",
              "description": "External URL should not be returned.",
              "keywords": ["external"],
              "min_access_level": 1,
              "actions": ["list"],
              "list_filters": [],
              "prefill_fields": []
            }
          ]
        }
        """);

        var rows = catalog.find(fw, "add new employee", "new", prefillJson: """{"fname":"John","lname":"Smith","pwd":"secret"}""");

        Assert.AreEqual(1, rows.Count);
        var row = (FwDict)rows[0]!;
        Assert.AreEqual("/Admin/Users/new?item%5Bfname%5D=John&item%5Blname%5D=Smith", row["url"]);
        Assert.AreEqual("new", row["action"]);
        StringAssert.Contains(Utils.jsonEncode(row["warnings"] ?? new List<string>()), "Unsupported prefill field: pwd");
        Assert.AreEqual(0, catalog.find(fw, "external", "list").Count);
    }

    [TestMethod]
    public void AssistantNavigationCatalog_BuildsValidatedListFilterUrl()
    {
        var fw = TestHelpers.CreateFw();
        fw.Session("access_level", Users.ACL_ADMIN.ToString());
        var catalog = AssistantNavigationCatalog.Parse("""
        {
          "version": 1,
          "controllers": [
            {
              "url": "/Admin/Users",
              "controller": "AdminUsers",
              "label": "Users",
              "description": "Manage users and employees.",
              "keywords": ["users", "employees"],
              "min_access_level": 90,
              "actions": ["list"],
              "list_filters": [
                { "name": "s", "label": "Search", "type": "text" },
                { "name": "status", "label": "Status", "type": "select", "options": { "0": "Active", "10": "Inactive" } }
              ],
              "prefill_fields": []
            }
          ]
        }
        """);

        var rows = catalog.find(fw, "show active users", "list", filtersJson: """{"status":"0","bad":"x"}""");

        Assert.AreEqual(1, rows.Count);
        var row = (FwDict)rows[0]!;
        Assert.AreEqual("/Admin/Users?dofilter=1&f%5Bstatus%5D=0", row["url"]);
        StringAssert.Contains(Utils.jsonEncode(row["warnings"] ?? new List<string>()), "Unsupported filter field: bad");
    }

    [TestMethod]
    public void AssistantNavigationCatalog_IsValidJsonAndIncludesFrameworkScreens()
    {
        string json = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Data", "template", "assistant", "prompts", "navigation_catalog.json"));

        var catalog = AssistantNavigationCatalog.Parse(json);

        Assert.IsTrue(catalog.controllers.Count > 5);
        Assert.IsTrue(catalog.controllers.Any(static item => item.url == "/Admin/Users"));
        Assert.IsTrue(catalog.controllers.Any(static item => item.url == "/Admin/KBArticles"));
        Assert.IsTrue(catalog.controllers.All(static item => item.url.StartsWith("/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AssistantRunProcessor_LoadsAssistantPromptsFromPromptsFolder()
    {
        string processor = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantRunProcessor.cs"));

        StringAssert.Contains(processor, "fw.parsePage(\"/assistant/prompts\", \"chat_system.md\"");
        StringAssert.Contains(processor, "fw.parsePage(\"/assistant/prompts\", \"tool_policy.md\"");
        StringAssert.Contains(processor, "fw.parsePage(\"/assistant/prompts\", \"navigation.md\"");
        StringAssert.Contains(processor, "fw.parsePage(\"/assistant/prompts\", \"clarification_prompt.md\"");
        StringAssert.Contains(processor, "fw.parsePage(\"/assistant/prompts\", \"memory_compaction.md\"");
    }

    [TestMethod]
    public void AssistantAppService_BindsFinalLinksToNavigationEvents()
    {
        string service = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantAppService.cs"));
        string runtime = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantAgentRuntime.cs"));

        StringAssert.Contains(service, "public void BindLinksToRunNavigation");
        StringAssert.Contains(service, "AssistantRunsEvents.TYPE_NAVIGATION");
        StringAssert.Contains(service, "result.links = [];");
        StringAssert.Contains(runtime, "RecordNavigationLinks(\"find_app_navigation\"");
    }

    [TestMethod]
    public void AssistantRuns_StatusToCodeMapsPublicStates()
    {
        Assert.AreEqual("queued", AssistantRuns.StatusToCode(AssistantRuns.STATUS_QUEUED));
        Assert.AreEqual("processing", AssistantRuns.StatusToCode(AssistantRuns.STATUS_PROCESSING));
        Assert.AreEqual("completed", AssistantRuns.StatusToCode(AssistantRuns.STATUS_COMPLETED));
        Assert.AreEqual("failed", AssistantRuns.StatusToCode(AssistantRuns.STATUS_FAILED));
        Assert.AreEqual("waiting_for_user", AssistantRuns.StatusToCode(AssistantRuns.STATUS_WAITING_FOR_USER));
    }

    [TestMethod]
    public void AssistantRuns_PortableClaimUsesConditionalUpdateAndTimeoutFailure()
    {
        string source = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantRuns.cs"));
        string processor = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantRunProcessor.cs"));

        StringAssert.Contains(source, "int affected = db.update(table_name");
        StringAssert.Contains(source, "\"status\", STATUS_FAILED");
        StringAssert.Contains(source, "if (affected <= 0)");
        StringAssert.Contains(source, "removeCache(row.id);");
        StringAssert.Contains(source, "public int failTimedOutActiveRuns");
        StringAssert.Contains(source, "TIMEOUT_ERROR_MESSAGE");
        StringAssert.Contains(source, "STATUS_QUEUED, STATUS_PROCESSING");
        StringAssert.Contains(source, "removeCacheAll();");
        StringAssert.Contains(processor, "failTimedOutActiveRuns(timeoutSeconds)");
        StringAssert.Contains(processor, "CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds))");
        StringAssert.Contains(processor, "markClaimedRunTimedOut(fw, run);");
    }

    [TestMethod]
    public void RagSources_ClaimRetriesFailedSourcesWithBackoffAndAdminRequeue()
    {
        string sources = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "RagSources.cs"));
        string processor = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantRunProcessor.cs"));
        string worker = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantRunWorkerService.cs"));
        string admin = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "controllers", "AdminRagChunks.cs"));

        StringAssert.Contains(sources, "public const int MAX_RETRY_ATTEMPTS = 5;");
        StringAssert.Contains(sources, "public const int RETRY_BACKOFF_BASE_MINUTES = 5;");
        StringAssert.Contains(sources, "index_attempt_no=coalesce(index_attempt_no, 0) + 1");
        StringAssert.Contains(sources, "index_status=@failed");
        StringAssert.Contains(sources, "next_retry_at is null or next_retry_at<=");
        StringAssert.Contains(sources, "nextRetryAt = attemptNo < MAX_RETRY_ATTEMPTS");
        StringAssert.Contains(sources, "public bool requeueSource");
        StringAssert.Contains(sources, "\"index_attempt_no\", 0");
        Assert.AreEqual(5, invokePrivateStatic<int>(typeof(RagSources), "retryDelayMinutes", 1));
        Assert.AreEqual(15, invokePrivateStatic<int>(typeof(RagSources), "retryDelayMinutes", 2));
        Assert.AreEqual(240, invokePrivateStatic<int>(typeof(RagSources), "retryDelayMinutes", 5));
        StringAssert.Contains(sources, "public int requeueStaleProcessingSources");
        StringAssert.Contains(sources, "\"@stale\", INDEX_STATUS_STALE");
        StringAssert.Contains(sources, "or upd_time < @cutoff");
        StringAssert.Contains(sources, "and index_status=@processing");
        StringAssert.Contains(processor, "fw.model<RagSources>().requeueStaleProcessingSources()");
        StringAssert.Contains(worker, "MaxSourcesBeforeRunCheck = 3");
        StringAssert.Contains(worker, "processedSourcesSinceRunCheck >= MaxSourcesBeforeRunCheck");
        StringAssert.Contains(admin, "RequeueSourceAction");
        StringAssert.Contains(admin, "fw.model<RagSources>().requeueSource(id)");
    }

    [TestMethod]
    public void AssistantRetry_QueuesFreshRunForLatestUserMessageWithoutDuplicatingUserMessage()
    {
        string service = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantAppService.cs"));
        string controller = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "controllers", "Assistant.cs"));
        string template = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Data", "template", "assistant", "index", "main.html"));
        int start = service.IndexOf("public (AssistantThreadDto thread, AssistantRunDto run) RetryLastResponse", StringComparison.Ordinal);
        int end = service.IndexOf("public void SubmitFeedback", StringComparison.Ordinal);
        Assert.IsTrue(start > 0 && end > start);
        string retryMethod = service[start..end];

        StringAssert.Contains(controller, "public FwDict RetryAction(int id)");
        StringAssert.Contains(controller, "enforcePost();");
        StringAssert.Contains(retryMethod, "requireThread(threadId, owner)");
        StringAssert.Contains(retryMethod, "queuedOrProcessingByThread(thread.id)");
        StringAssert.Contains(retryMethod, "latestByThread(thread.id, AssistantMessages.ROLE_USER)");
        StringAssert.Contains(retryMethod, "queueRun(thread.id, userMessage.id)");
        Assert.IsFalse(retryMethod.Contains("addMessage("));
        StringAssert.Contains(template, "data-assistant-retry");
        StringAssert.Contains(template, "bi bi-arrow-repeat");
        StringAssert.Contains(template, "'/(Retry)/' + encodeURIComponent(thread.id)");
        StringAssert.Contains(template, "isLatestCompletedResponse(message)");
        StringAssert.Contains(template, "isReadonly()");
    }

    [TestMethod]
    public void AdminRagChunks_DiagnosticsExposeSourcesRunsEvidenceAndRequeueAction()
    {
        string controller = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "controllers", "AdminRagChunks.cs"));
        string template = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Data", "template", "admin", "ragchunks", "index", "main.html"));
        string sources = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "RagSources.cs"));
        string runs = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantRuns.cs"));
        string events = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantRunsEvents.cs"));

        StringAssert.Contains(controller, "diagnostic_sources");
        StringAssert.Contains(controller, "diagnostic_runs");
        StringAssert.Contains(controller, "recent_evidence_events");
        StringAssert.Contains(controller, "RequeueSourceAction");
        StringAssert.Contains(sources, "public FwList listDiagnostics");
        StringAssert.Contains(runs, "public FwList listDiagnostics");
        StringAssert.Contains(events, "public FwList listRecentEvidence");
        StringAssert.Contains(template, "RAG Source Diagnostics");
        StringAssert.Contains(template, "Assistant Run Diagnostics");
        StringAssert.Contains(template, "Recent Retrieval Evidence");
        StringAssert.Contains(template, "/(RequeueSource)/");
        StringAssert.Contains(template, "index_attempt_no");
        StringAssert.Contains(template, "next_retry_at");
    }

    [TestMethod]
    public void AssistantRunProcessor_SetupFailuresAreMarkedFailedAfterClaim()
    {
        string source = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantRunProcessor.cs"));

        StringAssert.Contains(source, "AssistantThreads.Row? thread = null;");
        StringAssert.Contains(source, "catch (Exception ex)");
        StringAssert.Contains(source, "markClaimedRunFailed(fw, run, thread, ex);");
        StringAssert.Contains(source, "int threadId = thread?.id ?? run.assistant_threads_id;");
    }

    [TestMethod]
    public void AssistantAppService_MapMessageIncludesProducingRunId()
    {
        var fw = TestHelpers.CreateFw();
        var service = new AssistantAppService(fw);
        var message = new AssistantMessages.Row
        {
            id = 17,
            role = AssistantMessages.ROLE_ASSISTANT,
            message_type = AssistantMessages.TYPE_RESULT,
            content_markdown = "Answer",
            add_time = new DateTime(2026, 6, 22, 10, 0, 0)
        };
        var attachments = new Dictionary<int, List<AssistantAttachmentDto>>();
        var runIdsByMessageId = new Dictionary<int, int> { [17] = 42 };

        var dto = invokePrivate<AssistantMessageDto>(service, "mapMessage", message, attachments, runIdsByMessageId);

        Assert.AreEqual(42, dto.assistant_runs_id);
    }

    [TestMethod]
    public void AssistantFeedback_UsesMessageRunIdAndReadonlyThreadsHideFeedbackButtons()
    {
        string serviceSource = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "AssistantAppService.cs"));
        string template = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Data", "template", "assistant", "index", "main.html"));

        StringAssert.Contains(serviceSource, "idByResultMessage(messageId)");
        StringAssert.Contains(serviceSource, "requestedRun = fw.model<AssistantRuns>().oneTyped(runId)");
        StringAssert.Contains(serviceSource, "throw new UserException(\"Assistant response run not found.\");");
        StringAssert.Contains(template, "body.set('run_id', button.dataset.runId || '');");
        StringAssert.Contains(template, "const canFeedback = role === 'assistant' && !isReadonly() && !!message.assistant_runs_id;");
        StringAssert.Contains(template, "data-run-id=\"' + escapeAttr(message.assistant_runs_id)");
        Assert.IsFalse(template.Contains("thread.active_run && thread.active_run.id"));
    }

    [TestMethod]
    public void AssistantAppService_MapThreadSuppressesRunEventsWhenRequested()
    {
        var fw = TestHelpers.CreateFw();
        var service = new AssistantAppService(fw);
        var thread = new AssistantThreads.Row
        {
            id = 7,
            icode = "shared-code",
            iname = "Shared",
            add_time = new DateTime(2026, 6, 22, 10, 0, 0)
        };
        var run = new AssistantRuns.Row { id = 8, assistant_threads_id = 7, status = AssistantRuns.STATUS_COMPLETED };
        var events = new List<AssistantRunsEvents.Row>
        {
            new()
            {
                id = 9,
                assistant_runs_id = 8,
                event_type = AssistantRunsEvents.TYPE_EVIDENCE,
                content = "Evidence",
                payload_json = "{\"private\":true}",
                add_time = new DateTime(2026, 6, 22, 10, 1, 0)
            }
        };

        var dto = invokePrivate<AssistantThreadDto>(
            service,
            "mapThread",
            thread,
            new List<AssistantMessages.Row>(),
            events,
            run,
            createAssistantOwnerScope(),
            false
        );

        Assert.AreEqual(0, dto.events.Count);
    }

    [TestMethod]
    public void AssistantAppService_BindSourceToEvidenceOverwritesModelCitationMetadata()
    {
        var source = new AssistantSource
        {
            source_id = 7,
            chunk_id = 42,
            source_type = "model_type",
            name = "Model supplied name",
            url = "https://attacker.example/citation",
            article_name = "Model article",
            article_url = "https://attacker.example/article",
            filename = "model.txt",
            file_url = "https://attacker.example/file",
            page = 99,
            section = "Wrong section",
            score = 0.01
        };
        var bound = new AssistantSource
        {
            source_id = 7,
            chunk_id = 42,
            source_type = RagSources.SOURCE_TYPE_KB_ARTICLE,
            name = "Trusted KB",
            url = "/Admin/KBArticles/7",
            page = 2,
            section = "Install",
            score = 0.87
        };

        invokePrivateStatic<object?>(typeof(AssistantAppService), "bindSourceToEvidence", source, bound);

        Assert.AreEqual(RagSources.SOURCE_TYPE_KB_ARTICLE, source.source_type);
        Assert.AreEqual("Trusted KB", source.name);
        Assert.AreEqual("/Admin/KBArticles/7", source.url);
        Assert.AreEqual("/Admin/KBArticles/7", source.article_url);
        Assert.AreEqual("/Admin/KBArticles/7", source.file_url);
        Assert.AreEqual("Trusted KB", source.article_name);
        Assert.AreEqual("Trusted KB", source.filename);
        Assert.AreEqual(2, source.page);
        Assert.AreEqual("Install", source.section);
        Assert.AreEqual(0.87, source.score.GetValueOrDefault(), 0.0001);
    }

    [TestMethod]
    public void AssistantShareIcodeFreshSchema_IsUniqueForNonEmptyCodesAcrossProviders()
    {
        string sqliteFresh = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "sqlite", "fwdatabase.sql"));
        string mysqlFresh = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "mysql", "fwdatabase.sql"));

        StringAssert.Contains(sqliteFresh, "CREATE UNIQUE INDEX UX_assistant_threads_icode ON assistant_threads (icode) WHERE icode <> ''");
        StringAssert.Contains(mysqlFresh, "icode_share           VARCHAR(64) GENERATED ALWAYS AS (NULLIF(icode, '')) STORED");
        StringAssert.Contains(mysqlFresh, "UNIQUE KEY UX_assistant_threads_icode (icode_share)");
    }

    [TestMethod]
    public void AssistantShareIcodeUpdateScripts_AreUniqueForNonEmptyCodesAcrossProviders()
    {
        string sqliteUpdate = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "sqlite", "updates", "upd2026-06-12-assistant-rag.sql"));
        string mysqlUpdate = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "mysql", "updates", "upd2026-06-12-assistant-rag.sql"));

        StringAssert.Contains(sqliteUpdate, "CREATE UNIQUE INDEX IF NOT EXISTS UX_assistant_threads_icode ON assistant_threads (icode) WHERE icode <> ''");
        StringAssert.Contains(mysqlUpdate, "icode_share           VARCHAR(64) GENERATED ALWAYS AS (NULLIF(icode, '')) STORED");
        StringAssert.Contains(mysqlUpdate, "UNIQUE KEY UX_assistant_threads_icode (icode_share)");
    }

    [TestMethod]
    public void AssistantOperationalSchema_IncludesRagRetryAndRunTimeoutAcrossProviders()
    {
        var files = new[]
        {
            Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "fwdatabase.sql"),
            Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "updates", "upd2026-06-12-assistant-rag.sql"),
            Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "mysql", "fwdatabase.sql"),
            Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "mysql", "updates", "upd2026-06-12-assistant-rag.sql"),
            Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "sqlite", "fwdatabase.sql"),
            Path.Combine(repoRoot(), "osafw-app", "App_Data", "sql", "sqlite", "updates", "upd2026-06-12-assistant-rag.sql"),
        };

        foreach (string file in files)
        {
            string sql = File.ReadAllText(file);
            StringAssert.Contains(sql, "index_attempt_no");
            StringAssert.Contains(sql, "next_retry_at");
            StringAssert.Contains(sql, "ASSISTANT_RUN_TIMEOUT_SECONDS");
            StringAssert.Contains(sql, "next_retry_at");
            StringAssert.Contains(sql, "queued_at");
            if (file.Contains($"{Path.DirectorySeparatorChar}updates{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                Assert.IsFalse(sql.Contains("ALTER TABLE", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(sql.Contains("INFORMATION_SCHEMA", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(sql.Contains("DROP INDEX", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [TestMethod]
    public void RagSourcesAndDocumentEmbeddingService_ApplyAttachmentIndexByteLimitBeforeQueueAndParse()
    {
        string sources = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "models", "AI", "RagSources.cs"));
        string embedding = File.ReadAllText(Path.Combine(repoRoot(), "osafw-app", "App_Code", "fw", "DocumentEmbeddingService.cs"));

        StringAssert.Contains(sources, "embeddingService.isAttachmentIndexable(ext, att[\"fsize\"].toLong())");
        StringAssert.Contains(sources, "embeddingService.isAttachmentIndexable(att[\"ext\"].toStr(), att[\"fsize\"].toLong())");
        StringAssert.Contains(embedding, "if (!isAttachmentIndexable(ext, att[\"fsize\"].toLong()))");
        StringAssert.Contains(embedding, "if (attId <= 0 || !isAttachmentIndexable(ext, att[\"fsize\"].toLong()))");
        StringAssert.Contains(embedding, "if (att.Count == 0 || !isAttachmentIndexable(att[\"ext\"].toStr(), att[\"fsize\"].toLong()))");
    }

    [TestMethod]
    public void AssistantResult_CitationPayloadRoundTrips()
    {
        var result = new AssistantResult
        {
            title = "KB answer",
            information = "Answer with citation.",
            confidence = 0.75,
            links =
            [
                new AssistantLink
                {
                    label = "Open KB",
                    url = "/Admin/KBArticles",
                    description = "Manage knowledge base articles.",
                    action = "list",
                    confidence = 0.9
                }
            ],
            sources =
            [
                new AssistantSource
                {
                    name = "Install Guide",
                    article_id = 12,
                    article_url = "/Admin/KBArticles/12",
                    source_id = 7,
                    chunk_id = 42,
                    source_type = RagSources.SOURCE_TYPE_KB_ARTICLE,
                    section = "Setup",
                    page = 1,
                }
            ]
        };

        string json = JsonSerializer.Serialize(result);
        var roundTrip = JsonSerializer.Deserialize<AssistantResult>(json);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual("KB answer", roundTrip.title);
        Assert.AreEqual(1, roundTrip.links.Count);
        Assert.AreEqual("/Admin/KBArticles", roundTrip.links[0].url);
        Assert.AreEqual(1, roundTrip.sources.Count);
        Assert.AreEqual("/Admin/KBArticles/12", roundTrip.sources[0].article_url);
        Assert.AreEqual(7, roundTrip.sources[0].source_id);
        Assert.AreEqual(42, roundTrip.sources[0].chunk_id);
    }

    [TestMethod]
    public void AssistantRuntimeStatus_MissingOpenAiKeyReturnsAdminConfigurationMessage()
    {
        var fw = TestHelpers.CreateFw();
        registerSettings(fw, new Dictionary<string, string> { ["ASSISTANT_ENABLED"] = "1" });

        var status = new AssistantAppService(fw).RuntimeStatus();

        Assert.IsTrue(status.enabled);
        Assert.IsFalse(status.openai_configured);
        Assert.AreEqual("Please contact administrator to configure AI Assistant.", status.message);
    }

    [TestMethod]
    public void RagSources_SourceKeyIncludesSourceEntityItemAndAttachment()
    {
        string key = RagSources.BuildSourceKey(RagSources.SOURCE_TYPE_KB_ATTACHMENT, 5, 12, 44);

        Assert.AreEqual("kb_attachment:5:12:44", key);
    }

    [TestMethod]
    public void RagSources_HashTextIsStableSha256Hex()
    {
        string first = RagSources.HashText("hello");
        string second = RagSources.HashText("hello");

        Assert.AreEqual(first, second);
        Assert.AreEqual(64, first.Length);
    }

    [TestMethod]
    public void AssistantMemories_SanitizeMemoryTextRedactsSecretsAndContacts()
    {
        string sanitized = AssistantMemories.SanitizeMemoryText("email a@example.com token=abc123456 phone 312-555-1212 card 4111 1111 1111 1111");

        StringAssert.Contains(sanitized, "[redacted-email]");
        StringAssert.Contains(sanitized, "token: [redacted]");
        StringAssert.Contains(sanitized, "[redacted-phone]");
        StringAssert.Contains(sanitized, "[redacted-number]");
        Assert.IsFalse(sanitized.Contains("a@example.com"));
        Assert.IsFalse(sanitized.Contains("312-555-1212"));
    }

    private static T invokePrivate<T>(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, methodName + " method not found");
        return (T)method.Invoke(target, args)!;
    }

    private static T invokePrivateStatic<T>(Type type, string methodName, params object?[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method, methodName + " method not found");
        return (T)method.Invoke(null, args)!;
    }

    private static object createAssistantOwnerScope(int usersId = 0, string ownerToken = "")
    {
        var type = typeof(AssistantAppService).GetNestedType("AssistantOwnerScope", BindingFlags.NonPublic);
        Assert.IsNotNull(type);
        var constructor = type!.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(ctor => ctor.GetParameters().Length == 2);
        return constructor.Invoke([usersId, ownerToken]);
    }

    private static string repoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "osafw-asp.net-core.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate repository root from " + Directory.GetCurrentDirectory());
    }

    private static void registerSettings(FW fw, Dictionary<string, string>? values = null)
    {
        var settings = new StaticSettings(values);
        settings.init(fw);
        TestHelpers.RegisterModel(fw, (Settings)settings);
    }
}
