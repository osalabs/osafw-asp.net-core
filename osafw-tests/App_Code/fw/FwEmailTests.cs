using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass]
public class FwEmailTests
{
    private sealed class StubSettings : Settings
    {
        public string TestEmail { get; set; } = "";
        public string Host { get; init; } = "";
        public int Port { get; init; } = 587;
        public string Password { get; init; } = "";
        public int SecretReadCount { get; private set; }

        public override string read(string icode)
        {
            return icode switch
            {
                ICODE_TEST_EMAIL => TestEmail,
                "mail_from" => "sender@example.test",
                "mail.host" => Host,
                "mail.port" => Port.toStr(),
                "mail.is_ssl" => "0",
                "mail.username" => "",
                _ => "",
            };
        }

        public override string read(string icode, string defaultValue)
        {
            var value = read(icode);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        public override int readInt(string icode, int defaultValue = 0) => read(icode, defaultValue.toStr()).toInt(defaultValue);
        public override bool readBool(string icode, bool defaultValue = false) => read(icode, defaultValue ? "1" : "0").toBool();
        public override string readSecret(string code)
        {
            if (code != "mail.password")
                return "";

            SecretReadCount++;
            return Password;
        }
    }

    private sealed class TestAdminSendEmailController : AdminSendEmailController
    {
        public void MakeReadOnly() => is_readonly = true;
    }

    private sealed class StubUsers : Users
    {
        public override bool isReadOnly(int id = -1) => false;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private sealed class FakeSmtpServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly Task serverTask;
        private readonly bool rejectConnection;

        public int Port { get; }
        public List<string> Recipients { get; } = [];
        public string MessageData { get; private set; } = "";
        public bool ConnectionAccepted { get; private set; }

        public FakeSmtpServer(bool rejectConnection = false)
        {
            this.rejectConnection = rejectConnection;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            serverTask = Task.Run(ServeAsync);
        }

        private async Task ServeAsync()
        {
            using TcpClient connection = await listener.AcceptTcpClientAsync();
            ConnectionAccepted = true;
            if (rejectConnection)
                return;

            using NetworkStream stream = connection.GetStream();
            using StreamReader reader = new(stream, Encoding.ASCII, false, 1024, true);
            using StreamWriter writer = new(stream, Encoding.ASCII, 1024, true)
            {
                AutoFlush = true,
                NewLine = "\r\n",
            };

            await writer.WriteLineAsync("220 localhost test SMTP");
            var data = new StringBuilder();
            bool readingData = false;
            while (await reader.ReadLineAsync() is string line)
            {
                if (readingData)
                {
                    if (line == ".")
                    {
                        readingData = false;
                        MessageData = data.ToString();
                        await writer.WriteLineAsync("250 queued");
                    }
                    else
                        data.AppendLine(line);
                    continue;
                }

                if (line.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("HELO ", StringComparison.OrdinalIgnoreCase))
                    await writer.WriteLineAsync("250 localhost");
                else if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase))
                    await writer.WriteLineAsync("250 sender ok");
                else if (line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
                {
                    Recipients.Add(line[8..].Trim());
                    await writer.WriteLineAsync("250 recipient ok");
                }
                else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    readingData = true;
                    await writer.WriteLineAsync("354 end with dot");
                }
                else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 bye");
                    return;
                }
                else
                    await writer.WriteLineAsync("250 ok");
            }
        }

        public void Dispose()
        {
            listener.Stop();
            try { serverTask.Wait(TimeSpan.FromSeconds(5)); }
            catch (AggregateException) when (serverTask.IsFaulted
                && serverTask.Exception?.InnerException is SocketException or ObjectDisposedException) { }
        }
    }

    [TestMethod]
    public void SendEmail_MissingRecipientFailsWithoutTransport()
    {
        using var scope = CreateScope("smtp.invalid", 2525);

        Assert.IsFalse(scope.Fw.sendEmail("", "  ", "subject", "body"));
        Assert.AreEqual("Email recipient is required.", scope.Fw.last_error_send_email);
    }

    [TestMethod]
    public void SendEmail_InvalidRecipientFailsWithoutTransport()
    {
        using var scope = CreateScope("smtp.invalid", 2525);

        Assert.IsFalse(scope.Fw.sendEmail("", "not-an-address", "subject", "body"));
        Assert.IsNotEmpty(scope.Fw.last_error_send_email);
    }

    [TestMethod]
    public void SendEmail_MissingSmtpHostFails()
    {
        using var scope = CreateScope("", 2525);

        Assert.IsFalse(scope.Fw.sendEmail("", "recipient@example.test", "subject", "body"));
        Assert.AreEqual("SMTP host is required.", scope.Fw.last_error_send_email);
    }

    [TestMethod]
    public void SendEmail_TestModeWithoutResolvedRecipientFailsWithoutTransport()
    {
        using var scope = CreateScope("smtp.invalid", 2525, isTest: true);
        scope.Fw.Session("login", "");

        Assert.IsFalse(scope.Fw.sendEmail("", "original@example.test", "subject", "body"));
        Assert.AreEqual("Email recipient is required.", scope.Fw.last_error_send_email);
    }

    [TestMethod]
    public void SendEmail_PerSendSmtpOverrideDoesNotMutateConfiguration()
    {
        using var scope = CreateScope("configured.example.test", 2525);
        var options = new FwDict
        {
            ["smtp"] = new FwDict
            {
                ["host"] = "override.example.test",
                ["port"] = "0",
            },
        };

        Assert.IsFalse(scope.Fw.sendEmail("", "recipient@example.test", "subject", "body", options: options));

        var configuredMail = (StubSettings)scope.Fw.model<Settings>();
        Assert.AreEqual("configured.example.test", configuredMail.Host);
        Assert.AreEqual(2525, configuredMail.Port);
        Assert.AreEqual("SMTP port must be between 1 and 65535.", scope.Fw.last_error_send_email);
    }

    [TestMethod]
    public void SendEmail_TransportFailureReturnsFalse()
    {
        using var server = new FakeSmtpServer(rejectConnection: true);
        using var scope = CreateScope("127.0.0.1", server.Port);

        Assert.IsFalse(scope.Fw.sendEmail("", "recipient@example.test", "subject", "body"));
        Assert.IsNotEmpty(scope.Fw.last_error_send_email);
    }

    [TestMethod]
    public void SendEmail_SuccessClearsPreviousError()
    {
        using var server = new FakeSmtpServer();
        using var scope = CreateScope("127.0.0.1", server.Port);
        scope.Fw.last_error_send_email = "stale failure";

        Assert.IsTrue(scope.Fw.sendEmail("", "recipient@example.test", "subject", "body"));
        Assert.AreEqual("", scope.Fw.last_error_send_email);
        CollectionAssert.AreEqual(new[] { "<recipient@example.test>" }, server.Recipients);
    }

    [TestMethod]
    public void SendEmail_TestModeRedirectsAndSuppressesOriginalCcAndBcc()
    {
        using var server = new FakeSmtpServer();
        using var scope = CreateScope("127.0.0.1", server.Port, isTest: true);
        ((StubSettings)scope.Fw.model<Settings>()).TestEmail = "safe@example.test";
        var options = new FwDict { ["bcc"] = new StrList { "bcc@example.test" } };

        Assert.IsTrue(scope.Fw.sendEmail(
            "",
            "original@example.test",
            "subject",
            "body",
            aCC: new StrList { "cc@example.test" },
            options: options));

        Assert.IsNotEmpty(server.Recipients);
        Assert.IsTrue(server.Recipients.TrueForAll(x => x == "<safe@example.test>"));
        StringAssert.Contains(server.MessageData, "To: safe@example.test");
        Assert.IsFalse(server.MessageData.Contains("cc@example.test", StringComparison.Ordinal));
        Assert.IsFalse(server.MessageData.Contains("bcc@example.test", StringComparison.Ordinal));
        StringAssert.Contains(server.MessageData, "TEST SEND. PASSED MAIL_TO");
        StringAssert.Contains(server.MessageData, "original@example.test");
    }

    [TestMethod]
    public void AdminSendEmail_AdminCannotReuseConfiguredPasswordAgainstSubmittedHost()
    {
        using var server = new FakeSmtpServer();
        using var scope = CreateAdminSendEmailScope(server, Users.ACL_ADMIN);
        var settings = (StubSettings)scope.Fw.model<Settings>();
        var controller = new TestAdminSendEmailController();
        controller.init(scope.Fw);

        Assert.IsTrue(scope.Fw.context.Items.ContainsKey("OSAFW.SensitiveSettings"));
        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction());
        Assert.AreEqual(0, settings.SecretReadCount);
        Assert.IsFalse(server.ConnectionAccepted);
    }

    [TestMethod]
    [DataRow("GET", "token")]
    [DataRow("POST", "wrong-token")]
    public void AdminSendEmail_RejectsUnsafeRequestBeforeSmtp(string method, string formToken)
    {
        using var server = new FakeSmtpServer();
        using var scope = CreateAdminSendEmailScope(server, Users.ACL_SITEADMIN, method, formToken);
        var settings = (StubSettings)scope.Fw.model<Settings>();
        var controller = new TestAdminSendEmailController();
        controller.init(scope.Fw);

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction());
        Assert.AreEqual(0, settings.SecretReadCount);
        Assert.IsFalse(server.ConnectionAccepted);
    }

    [TestMethod]
    public void AdminSendEmail_ReadOnlySiteAdminCannotSend()
    {
        using var server = new FakeSmtpServer();
        using var scope = CreateAdminSendEmailScope(server, Users.ACL_SITEADMIN);
        var settings = (StubSettings)scope.Fw.model<Settings>();
        var controller = new TestAdminSendEmailController();
        controller.init(scope.Fw);
        controller.MakeReadOnly();

        Assert.ThrowsExactly<AuthException>(() => controller.SaveAction());
        Assert.AreEqual(0, settings.SecretReadCount);
        Assert.IsFalse(server.ConnectionAccepted);
    }

    [TestMethod]
    public void AdminSendEmail_ValidationFailureClearsSubmittedPasswordBeforeRendering()
    {
        using var server = new FakeSmtpServer();
        using var scope = CreateAdminSendEmailScope(server, Users.ACL_SITEADMIN);
        var item = (FwDict)scope.Fw.FORM["item"]!;
        item["password"] = "submitted-secret";
        item.Remove("subject");
        var settings = (StubSettings)scope.Fw.model<Settings>();
        var controller = new TestAdminSendEmailController();
        controller.init(scope.Fw);

        Assert.ThrowsExactly<ValidationException>(() => controller.SaveAction());
        Assert.IsFalse(item.ContainsKey("password"));

        scope.Fw.route.method = "GET";
        var retryPage = controller.ShowFormAction();
        Assert.IsFalse(retryPage["i"] is FwDict retryItem && retryItem.ContainsKey("password"));
        Assert.AreEqual(0, settings.SecretReadCount);
        Assert.IsFalse(server.ConnectionAccepted);
    }

    [TestMethod]
    public void AdminSendEmail_SiteAdminCanUseConfiguredSmtpWithBlankPasswordField()
    {
        using var server = new FakeSmtpServer();
        using var scope = CreateAdminSendEmailScope(server, Users.ACL_SITEADMIN);
        var settings = (StubSettings)scope.Fw.model<Settings>();
        var controller = new TestAdminSendEmailController();
        controller.init(scope.Fw);

        var result = controller.SaveAction();

        Assert.IsNotNull(result);
        Assert.IsTrue(result["is_sent"].toBool());
        Assert.AreEqual(1, settings.SecretReadCount);
        Assert.IsTrue(server.ConnectionAccepted);
        CollectionAssert.AreEqual(new[] { "<recipient@example.test>" }, server.Recipients);
        Assert.IsFalse(scope.Fw.FORM["item"] is FwDict item && item.ContainsKey("password"));
    }

    [TestMethod]
    public void AdminSendEmail_TemplatesHideToolFromOrdinaryAdminsAndNeverReflectPassword()
    {
        var repoRoot = FindRepoRoot();
        var showButton = File.ReadAllText(Path.Combine(repoRoot, "osafw-app", "App_Data", "template", "admin", "users", "showform", "btn_std_more.html"));
        var listButton = File.ReadAllText(Path.Combine(repoRoot, "osafw-app", "App_Data", "template", "admin", "users", "index", "btn_std_more.html"));
        var smtpForm = File.ReadAllText(Path.Combine(repoRoot, "osafw-app", "App_Data", "template", "admin", "sendemail", "showform", "form_right.html"));

        StringAssert.Contains(showButton, "ifeq=\"SESSION[access_level]\" value=\"100\"");
        StringAssert.Contains(listButton, "ifeq=\"SESSION[access_level]\" value=\"100\"");
        StringAssert.Contains(smtpForm, "type=\"password\"");
        Assert.IsFalse(smtpForm.Contains("i[password]", StringComparison.Ordinal));
    }

    private static FwTestScope CreateScope(string host, int port, bool isTest = false)
    {
        var config = new Dictionary<string, string?>
        {
            ["appSettings:is_test"] = isTest.ToString(),
        };
        var scope = new FwTestScope(_ => new RejectingDb(), config, TestHelpers.CreateHttpContext("email-tests"));
        TestHelpers.RegisterModel(scope.Fw, (Settings)new StubSettings { Host = host, Port = port });
        return scope;
    }

    private static FwTestScope CreateAdminSendEmailScope(
        FakeSmtpServer server,
        int accessLevel,
        string method = "POST",
        string formToken = "token")
    {
        var scope = new FwTestScope(
            _ => new RejectingDb(),
            context: TestHelpers.CreateHttpContext("email-tests"));
        var fw = scope.Fw;
        TestHelpers.RegisterModel(fw, (Settings)new StubSettings
        {
            Host = "127.0.0.1",
            Port = server.Port,
            Password = "stored-smtp-secret",
        });
        TestHelpers.RegisterModel(fw, (Users)new StubUsers());
        fw.Session("user_id", "7");
        fw.Session("access_level", accessLevel.toStr());
        fw.Session("XSS", "token");
        fw.route.controller = "AdminSendEmail";
        fw.route.action = FW.ACTION_SAVE;
        fw.route.method = method;
        fw.FORM["XSS"] = formToken;
        fw.FORM["item"] = new FwDict
        {
            ["from"] = "sender@example.test",
            ["to"] = "recipient@example.test",
            ["subject"] = "SMTP diagnostic",
            ["body"] = "test",
            ["host"] = "127.0.0.1",
            ["port"] = server.Port,
            ["username"] = "",
            ["password"] = "",
        };
        return scope;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "osafw-asp.net-core.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate repository root.");
    }
}
