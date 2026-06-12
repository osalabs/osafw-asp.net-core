using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
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
    public void DocChunks_VectorModeJsonBypassesNativeDetection()
    {
        var fw = TestHelpers.CreateFw();
        registerSettings(fw, new Dictionary<string, string> { ["ASSISTANT_VECTOR_MODE"] = DocChunks.VECTOR_MODE_JSON });
        var chunks = new DocChunks();
        chunks.init(fw);

        string backend = invokePrivate<string>(chunks, "resolveVectorBackend", 1536);

        Assert.AreEqual(DocChunks.VECTOR_MODE_JSON, backend);
    }

    [TestMethod]
    public void DocChunks_JsonFallbackSqlUsesProviderJsonFunctions()
    {
        var fw = TestHelpers.CreateFw();
        var chunks = new DocChunks();
        chunks.init(fw);

        string sqlServer = invokePrivate<string>(chunks, "buildSqlServerJsonQuerySql", 3);
        string mySql = invokePrivate<string>(chunks, "buildMySqlJsonQuerySql", 3);
        string sqlite = invokePrivate<string>(chunks, "buildSqliteJsonQuerySql", 3);

        StringAssert.Contains(sqlServer.ToLowerInvariant(), "openjson");
        StringAssert.Contains(mySql.ToLowerInvariant(), "json_table");
        StringAssert.Contains(sqlite.ToLowerInvariant(), "json_each");
        StringAssert.Contains(sqlServer, "CosineSim");
        StringAssert.Contains(mySql, "CosineSim");
        StringAssert.Contains(sqlite, "CosineSim");
    }

    [TestMethod]
    public void DocChunks_CosineSimilarityOrdersFixtureVectors()
    {
        var query = new List<float> { 1, 0 };
        var rows = new[]
        {
            new { Id = 2, Name = "far", Vector = new List<float> { 0, 1 } },
            new { Id = 1, Name = "near", Vector = new List<float> { 0.9f, 0.1f } },
            new { Id = 3, Name = "same", Vector = new List<float> { 1, 0 } },
        };

        var ordered = rows
            .OrderByDescending(row => DocChunks.cosineSimilarity(query, row.Vector))
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
    public void AssistantResult_CitationPayloadRoundTrips()
    {
        var result = new AssistantResult
        {
            title = "KB answer",
            information = "Answer with citation.",
            confidence = 0.75,
            sources =
            [
                new AssistantSource
                {
                    name = "Install Guide",
                    article_id = 12,
                    article_url = "/Admin/KBArticles/12",
                    section = "Setup",
                    page = 1,
                }
            ]
        };

        string json = JsonSerializer.Serialize(result);
        var roundTrip = JsonSerializer.Deserialize<AssistantResult>(json);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual("KB answer", roundTrip.title);
        Assert.AreEqual(1, roundTrip.sources.Count);
        Assert.AreEqual("/Admin/KBArticles/12", roundTrip.sources[0].article_url);
    }

    private static T invokePrivate<T>(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, methodName + " method not found");
        return (T)method.Invoke(target, args)!;
    }

    private static void registerSettings(FW fw, Dictionary<string, string>? values = null)
    {
        var settings = new StaticSettings(values);
        settings.init(fw);
        TestHelpers.RegisterModel(fw, (Settings)settings);
    }
}
