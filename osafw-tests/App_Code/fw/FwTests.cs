using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace osafw.Tests
{
    [TestClass]
    public class FwTests
    {
        private class StubUsers : Users
        {
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

        private sealed class ThrowingSettings : Settings
        {
            public override DBRow oneByIcode(string icode) => throw new System.InvalidOperationException("settings unavailable");
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
            var configuration = new ConfigurationBuilder().Build();

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
            var configuration = new ConfigurationBuilder().Build();

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
        public void ResolveTestEmailRecipient_PrefersConfiguredTestEmail()
        {
            var fw = CreateFwForHost("test-email-configured");
            TestHelpers.RegisterModel(fw, (Settings)new StubSettings());
            fw.config()["test_email"] = " configured@example.test ";
            fw.Session("login", "session@example.test");

            Assert.AreEqual("configured@example.test", fw.resolveTestEmailRecipient());
        }

        [TestMethod]
        public void ResolveTestEmailRecipient_FallsBackToSessionLoginWhenConfigBlank()
        {
            var fw = CreateFwForHost("test-email-session-fallback");
            TestHelpers.RegisterModel(fw, (Settings)new StubSettings());
            fw.config()["test_email"] = " ";
            fw.Session("login", " session@example.test ");

            Assert.AreEqual("session@example.test", fw.resolveTestEmailRecipient());
        }

        [TestMethod]
        public void ResolveTestEmailRecipient_PrefersTrimmedSiteSetting()
        {
            var fw = CreateFwForHost("test-email-site-setting");
            TestHelpers.RegisterModel(fw, (Settings)new StubSettings { TestEmail = " site@example.test " });
            fw.config()["test_email"] = "configured@example.test";
            fw.Session("login", "session@example.test");

            Assert.AreEqual("site@example.test", fw.resolveTestEmailRecipient());
        }

        [TestMethod]
        public void ResolveTestEmailRecipient_CurrentUserSiteSettingOverridesConfig()
        {
            var fw = CreateFwForHost("test-email-current-user");
            TestHelpers.RegisterModel(fw, (Settings)new StubSettings { TestEmail = " current_user " });
            fw.config()["test_email"] = "configured@example.test";
            fw.Session("login", " session@example.test ");

            Assert.AreEqual("session@example.test", fw.resolveTestEmailRecipient());
        }

        [TestMethod]
        public void ResolveTestEmailRecipient_DoesNotHideSettingsReadFailure()
        {
            var fw = CreateFwForHost("test-email-settings-failure");
            TestHelpers.RegisterModel(fw, (Settings)new ThrowingSettings());
            fw.config()["test_email"] = "configured@example.test";

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
