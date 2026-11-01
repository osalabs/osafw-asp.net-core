using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace osafw.Tests
{
    [TestClass]
    public class FwTests
    {
        private class StubUsers : Users
        {
            public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];

            public override DBRow one(int id) => new DBRow(new FwDict
            {
                ["id"] = id.toStr(),
                ["email"] = "user@example.com",
                ["access_level"] = Users.ACL_MEMBER.toStr(),
                ["lang"] = "en",
                ["ui_theme"] = "",
                ["ui_mode"] = "",
                ["date_format"] = DateUtils.DATE_FORMAT_DMY.toStr(),
                ["time_format"] = DateUtils.TIME_FORMAT_24.toStr(),
                ["timezone"] = "Central Standard Time",
                ["fname"] = "Test",
                ["lname"] = "User",
                ["att_id"] = "0",
            });
        }

        private sealed class StubSettings : Settings
        {
            public string TestEmail { get; init; } = "";

            public override DBRow oneByIcode(string icode)
            {
                return icode == Settings.ICODE_TEST_EMAIL
                    ? new DBRow(new FwDict { ["ivalue"] = TestEmail })
                    : [];
            }
        }

        private sealed class DisplaySettings : Settings
        {
            public override DBRow oneByIcode(string code) => code switch
            {
                "SITE_NAME" => new DBRow(DB.h("ivalue", "Stored site")),
                "support_email" => new DBRow(DB.h("ivalue", "support@stored.test")),
                "mail_from" => new DBRow(DB.h("ivalue", "sender@stored.test")),
                "is_mfa_enforced" => new DBRow(DB.h("ivalue", "1")),
                _ => [],
            };
        }

        private sealed class ThrowingSettings : Settings
        {
            public override DBRow oneByIcode(string icode) => throw new System.InvalidOperationException("settings unavailable");
        }

        private sealed class ApplicationSettings : Settings
        {
            public int GlobalsCalls { get; private set; }

            public override string read(string code) => code == "custom.title" ? "Application value" : "";

            public override void applyGlobals()
            {
                GlobalsCalls++;
                fw.G["SITE_NAME"] = "Application site";
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void SettingsShortcutSharesInitializedModelWithoutDatabaseReads(bool shortcutFirst)
        {
            using var scope = new FwTestScope(_ => new RejectingDb());
            var fw = scope.Fw;

            var first = shortcutFirst ? fw.settings : fw.model<Settings>();

            Assert.AreSame(first, fw.settings);
            Assert.AreSame(first, fw.model<Settings>());
            Assert.AreSame(first, fw.model(typeof(Settings)));
            Assert.AreSame(first, fw.model(nameof(Settings)));
            Assert.AreSame(fw.db, first.getDB());
        }

        [TestMethod]
        public void SettingsShortcutUsesApplicationOverridesAndExistingModelRegistration()
        {
            using var scope = new FwTestScope(_ => new RejectingDb(), new Dictionary<string, string?>
            {
                ["appSettings:template"] = TestHelpers.TemplateRoot(),
            });
            var fw = scope.Fw;
            _ = fw.settings;
            var applicationSettings = new ApplicationSettings();
            applicationSettings.init(fw);
            TestHelpers.RegisterModel(fw, (Settings)applicationSettings);

            Assert.AreSame(applicationSettings, fw.settings);
            Assert.AreEqual("Application value", fw.settings.read("custom.title"));
            string email = fw.parsePage("/emails", "signup.txt", []);

            StringAssert.Contains(email, "Application site");
            Assert.AreEqual(1, applicationSettings.GlobalsCalls);
        }

        public sealed class RuntimeTypeModel : FwModel
        {
            public int InitCalls { get; private set; }

            public override void init(FW fw)
            {
                base.init(fw);
                InitCalls++;
            }
        }

        public static class OtherModels
        {
            public sealed class RuntimeTypeModel : FwModel { }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void Model_ByRuntimeTypeInitializesAndSharesRequestCache(bool runtimeFirst)
        {
            var fw = TestHelpers.CreateFw();

            var first = runtimeFirst
                ? (RuntimeTypeModel)fw.model(typeof(RuntimeTypeModel))
                : fw.model<RuntimeTypeModel>();

            Assert.AreSame(first, fw.model(typeof(RuntimeTypeModel)));
            Assert.AreSame(first, fw.model<RuntimeTypeModel>());
            Assert.AreEqual(1, first.InitCalls);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void Model_ByRuntimeTypeSharesClassNameCache(bool runtimeFirst)
        {
            var fw = TestHelpers.CreateFw();

            var first = runtimeFirst ? fw.model(typeof(Users)) : fw.model(nameof(Users));

            Assert.AreSame(first, fw.model(typeof(Users)));
            Assert.AreSame(first, fw.model(nameof(Users)));
            Assert.AreSame(first, fw.model<Users>());
        }

        [TestMethod]
        public void Model_ByRuntimeTypeRejectsIncompatibleCachedType()
        {
            var fw = TestHelpers.CreateFw();
            var original = fw.model<RuntimeTypeModel>();

            Assert.ThrowsExactly<System.InvalidOperationException>(() => fw.model(typeof(OtherModels.RuntimeTypeModel)));

            Assert.AreSame(original, fw.model(typeof(RuntimeTypeModel)));
            Assert.AreEqual(1, original.InitCalls);
        }

        [TestMethod]
        [DataRow(typeof(string))]
        [DataRow(typeof(FwModel))]
        public void Model_ByRuntimeTypeRejectsInvalidType(System.Type modelType)
        {
            var fw = TestHelpers.CreateFw();

            var exception = Assert.ThrowsExactly<System.ArgumentException>(() => fw.model(modelType));

            Assert.AreEqual("modelType", exception.ParamName);
        }

        [TestMethod]
        public void Model_ByRuntimeTypeRejectsNull()
        {
            var fw = TestHelpers.CreateFw();

            var exception = Assert.ThrowsExactly<System.ArgumentNullException>(() => fw.model((System.Type)null!));

            Assert.AreEqual("modelType", exception.ParamName);
        }

        [TestMethod]
        public void FormatUserDateTime_FormatsIsoAndLocal()
        {
            var context = TestHelpers.CreateHttpContext();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["appSettings:timezone"] = "UTC" })
                .Build();

            var fw = new FW(context, configuration);

            var dt = new System.DateTime(2024, 1, 1, 12, 0, 0, System.DateTimeKind.Utc);

            var iso = fw.formatUserDateTime(dt, true);
            var local = fw.formatUserDateTime(dt);

            Assert.AreEqual("2024-01-01T12:00:00+00:00", iso);
            Assert.AreEqual("1/1/2024 12:00 PM", local);
        }

        [TestMethod]
        public void FormatUserDateTime_AcceptsDateTimeOffset()
        {
            var context = TestHelpers.CreateHttpContext();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["appSettings:timezone"] = "UTC" })
                .Build();

            var fw = new FW(context, configuration);
            var dto = new System.DateTimeOffset(2024, 1, 1, 15, 0, 0, System.TimeSpan.FromHours(3));

            var formatted = fw.formatUserDateTime(dto, true);

            Assert.AreEqual("2024-01-01T12:00:00+00:00", formatted);
        }

        [TestMethod]
        public void FormatUserDateTime_HonorsUserFormatsAndSqlInput()
        {
            var context = TestHelpers.CreateHttpContext();
            var configuration = new ConfigurationBuilder().Build();

            var fw = new FW(context, configuration);
            fw.G["date_format"] = DateUtils.DATE_FORMAT_DMY;
            fw.G["time_format"] = DateUtils.TIME_FORMAT_24;
            fw.G["timezone"] = DateUtils.TZ_UTC;

            var formatted = fw.formatUserDateTime("2024-02-03 15:30:00");

            Assert.AreEqual("3/2/2024 15:30", formatted);
        }

        [TestMethod]
        public void UserSessionPropertiesReflectSessionValues()
        {
            var context = TestHelpers.CreateHttpContext();
            var configuration = new ConfigurationBuilder().Build();
            var fw = new FW(context, configuration);

            Assert.IsFalse(fw.isLogged);
            context.Session.SetString("user_id", "9");

            Assert.IsTrue(fw.isLogged);
            Assert.AreEqual(9, fw.userId);
        }

        [TestMethod]
        public void ResolveTestEmailRecipient_UsesSiteSetting()
        {
            var fw = CreateFwForHost("test-email-configured");
            TestHelpers.RegisterModel(fw, (Settings)new StubSettings { TestEmail = " configured@example.test " });
            fw.settings.init(fw);
            fw.Session("login", "session@example.test");

            Assert.AreEqual("configured@example.test", fw.resolveTestEmailRecipient());
        }

        [TestMethod]
        public void ResolveTestEmailRecipient_FallsBackToSessionLoginWhenConfigBlank()
        {
            var fw = CreateFwForHost("test-email-session-fallback");
            TestHelpers.RegisterModel(fw, (Settings)new StubSettings());
            fw.settings.init(fw);
            fw.Session("login", " session@example.test ");

            Assert.AreEqual("session@example.test", fw.resolveTestEmailRecipient());
        }

        [TestMethod]
        public void ResolveTestEmailRecipient_PrefersTrimmedSiteSetting()
        {
            var fw = CreateFwForHost("test-email-site-setting");
            TestHelpers.RegisterModel(fw, (Settings)new StubSettings { TestEmail = " site@example.test " });
            fw.settings.init(fw);
            fw.Session("login", "session@example.test");

            Assert.AreEqual("site@example.test", fw.resolveTestEmailRecipient());
        }

        [TestMethod]
        public void ResolveTestEmailRecipient_CurrentUserSiteSettingUsesSession()
        {
            var fw = CreateFwForHost("test-email-current-user");
            TestHelpers.RegisterModel(fw, (Settings)new StubSettings { TestEmail = " current_user " });
            fw.settings.init(fw);
            fw.Session("login", " session@example.test ");

            Assert.AreEqual("session@example.test", fw.resolveTestEmailRecipient());
        }

        [TestMethod]
        public void ResolveTestEmailRecipient_DoesNotHideSettingsReadFailure()
        {
            var fw = CreateFwForHost("test-email-settings-failure");
            TestHelpers.RegisterModel(fw, (Settings)new ThrowingSettings());
            fw.settings.init(fw);

            var error = Assert.ThrowsExactly<System.InvalidOperationException>(() => fw.resolveTestEmailRecipient());

            Assert.AreEqual("settings unavailable", error.Message);
        }

        [TestMethod]
        public void Constructor_DoesNotCreateFlashSessionKeyWhenFlashIsMissing()
        {
            var context = TestHelpers.CreateHttpContext();
            var configuration = new ConfigurationBuilder().Build();

            _ = new FW(context, configuration);

            Assert.IsFalse(context.Session.Keys.Contains("_flash"));
        }

        [TestMethod]
        public void Constructor_ClonesCustomConfigurationWithoutDatabaseDefinitions()
        {
            var configuration = new Dictionary<string, string?>
            {
                ["appSettings:db:main:connection_string"] = "Server=secret.example;Password=db-secret;",
                ["appSettings:db:main:type"] = "SQL",
                ["appSettings:override:Other:db:main:connection_string"] = "Server=other.example;Password=other-secret;",
                ["appSettings:custom:title"] = "Application title",
            };
            using var scope = new FwTestScope(_ => new RejectingDb(), configuration);
            var fw = scope.Fw;

            Assert.IsFalse(fw.G.ContainsKey("db"));
            Assert.IsFalse(fw.G.ContainsKey("override"));
            Assert.IsTrue(fw.config().ContainsKey("db"));
            Assert.IsTrue(fw.config().ContainsKey("override"));

            var globals = (FwDict)fw.G["custom"]!;
            Assert.AreEqual("Application title", globals["title"]);
            globals["title"] = "Request title";
            Assert.AreEqual("Application title", fw.config("custom.title"));
        }

        [TestMethod]
        public void PresentationGlobalsPreserveRealLayoutEmailAndCspConsumers()
        {
            using var scope = new FwTestScope(_ => new RejectingDb(), new Dictionary<string, string?>
            {
                ["appSettings:template"] = TestHelpers.TemplateRoot(),
                ["appSettings:SITE_NAME"] = "Configured site",
                ["appSettings:SITE_VERSION"] = "42",
                ["appSettings:ASSETS_URL"] = "/static",
                ["appSettings:ROOT_DOMAIN"] = "https://example.test",
                ["appSettings:AWSRegion"] = "us-east-1",
                ["appSettings:S3Bucket"] = "public-images",
            }, TestHelpers.CreateHttpContext("example.test"));
            var fw = scope.Fw;
            var settings = new DisplaySettings();
            settings.init(fw);
            TestHelpers.RegisterModel(fw, (Settings)settings);

            // Email parsing also loads current site display values, without a preceding web render.
            string email = fw.parsePage("/emails", "signup.txt", []);
            StringAssert.Contains(email, "Stored site");
            StringAssert.Contains(email, "https://example.test");
            Assert.IsFalse(email.Contains("Configured site", System.StringComparison.Ordinal));

            string layout = fw.parsePage("/home/index", fw.G["PAGE_LAYOUT_PUBLIC"].toStr(), []);
            StringAssert.Contains(layout, "Stored site");
            StringAssert.Contains(layout, "/static/img/logo.png");
            StringAssert.Contains(layout, "?v42");
            string csp = fw.parsePage("/layout", "content_security_policy.html", []);
            StringAssert.Contains(csp, "public-images.s3.us-east-1.amazonaws.com");
            string loginHelp = fw.parsePage("/login/index", "login_problem.html", []);
            StringAssert.Contains(loginHelp, "mailto:support@stored.test");

            Assert.AreEqual(fw.config("mail_from"), fw.G["mail_from"]);
            Assert.AreEqual(fw.config("is_mfa_enforced"), fw.G["is_mfa_enforced"]);
            Assert.AreEqual(fw.config("timezone"), fw.G["timezone"]);
        }

        [TestMethod]
        public void DeveloperDocumentationReadsFeatureSettingsIntoPageState()
        {
            using var scope = new FwTestScope(_ => new RejectingDb(), new Dictionary<string, string?>
            {
                ["appSettings:template"] = TestHelpers.TemplateRoot(),
                ["appSettings:is_mfa_enforced"] = "false",
            }, TestHelpers.CreateHttpContext());
            var fw = scope.Fw;
            fw.Session("access_level", Users.ACL_SITEADMIN.toStr());
            var settings = new DisplaySettings();
            settings.init(fw);
            TestHelpers.RegisterModel(fw, (Settings)settings);
            var users = new StubUsers();
            users.init(fw);
            TestHelpers.RegisterModel(fw, (Users)users);
            var controller = new DevManageController();
            controller.init(fw);

            var ps = controller.DocsAction()!;
            string overview = fw.parsePage("/dev/manage/docs", "tab_overview.md", ps);

            Assert.IsTrue(ps["is_mfa_enforced"].toBool());
            StringAssert.Contains(overview, "sender@stored.test");
            StringAssert.Contains(overview, "Authentication (MFA) enforced:** Yes");
        }

        [TestMethod]
        public void Constructor_ConsumesExistingFlashSessionKeyOnce()
        {
            var context = TestHelpers.CreateHttpContext();
            var configuration = new ConfigurationBuilder().Build();
            context.Session.SetString("_flash", Utils.serialize(new FwDict { ["notice"] = "saved" }));

            var fw = new FW(context, configuration);

            Assert.AreEqual("saved", (fw.G["_flash"] as FwDict)?["notice"]);
            Assert.IsFalse(context.Session.Keys.Contains("_flash"));
        }

        [TestMethod]
        public void ReloadSession_ClearsWholeSessionWhenRequested()
        {
            var context = TestHelpers.CreateHttpContext();
            var configuration = new ConfigurationBuilder().Build();
            var fw = new FW(context, configuration);
            var users = new StubUsers();
            users.init(fw);

            fw.SessionDict("_filter_AdminReports.Show.Sample", new FwDict { ["from_date"] = "11/3/2026" });
            fw.Session("custom_key", "custom");
            fw.Session("XSS", "old-token");

            users.reloadSession(9, is_clear: true);

            Assert.IsNull(fw.SessionDict("_filter_AdminReports.Show.Sample"));
            Assert.AreEqual("", fw.Session("custom_key"));
            Assert.AreEqual("9", fw.Session("user_id"));
            Assert.AreEqual("user@example.com", fw.Session("login"));
            Assert.AreEqual("Central Standard Time", fw.Session("timezone"));
            Assert.AreNotEqual("", fw.Session("XSS"));
            Assert.AreNotEqual("old-token", fw.Session("XSS"));
        }

        private static FW CreateFwForHost(string host)
        {
            var context = TestHelpers.CreateHttpContext(host);
            var configuration = new ConfigurationBuilder().Build();
            return new FW(context, configuration);
        }
    }
}
