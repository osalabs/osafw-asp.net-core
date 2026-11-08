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

    private sealed class DefaultSettings(bool isInherited) : Settings
    {
        public override DBRow oneByIcode(string code) => isInherited
            ? new DBRow(DB.h("basis", BASIS_INHERIT)) : [];
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
        private readonly string? rejectRecipient;

        public int Port { get; }
        public List<string> Recipients { get; } = [];
        public string MessageData { get; private set; } = "";
        public bool ConnectionAccepted { get; private set; }

        public FakeSmtpServer(bool rejectConnection = false, string? rejectRecipient = null)
        {
            this.rejectConnection = rejectConnection;
            this.rejectRecipient = rejectRecipient;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            serverTask = ServeAsync();
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
                    var recipient = line[8..].Trim();
                    Recipients.Add(recipient);
                    await writer.WriteLineAsync(recipient == rejectRecipient ? $"550 recipient rejected {recipient}" : "250 recipient ok");
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
            try
            {
                serverTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException) when (serverTask.IsFaulted
                && serverTask.Exception?.InnerException is SocketException or ObjectDisposedException)
            {
                // Stopping a listener with no connection cancels its pending accept.
            }
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

        var configuredMail = (StubSettings)scope.Fw.settings;
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
    [DataRow(false)]
    [DataRow(true)]
    public void SendEmail_LogsSmtpErrorsAndRespectsPiiSettingForMessageDetails(bool isLogPii)
    {
        using var server = new FakeSmtpServer(rejectRecipient: "<safe@example.test>");
        var logPath = Path.Combine(Path.GetTempPath(), "fw-mail-log-" + Guid.NewGuid().ToString("N") + ".log");

        try
        {
            using (var scope = CreateScope("127.0.0.1", server.Port, isTest: true))
            {
                ((StubSettings)scope.Fw.settings).TestEmail = "safe@example.test";
                scope.Fw.config()["log_pii"] = isLogPii;
                scope.Fw.flogger.log_level = LogLevel.DEBUG;
                scope.Fw.flogger.log_file = logPath;
                Assert.IsFalse(scope.Fw.sendEmail(
                    "private-sender@example.test",
                    "private-to@example.test",
                    "private subject",
                    "private body",
                    aCC: new StrList { "private-cc@example.test" },
                    reply_to: "private-reply@example.test"));
                StringAssert.Contains(scope.Fw.last_error_send_email, "recipient rejected");
            }

            var log = File.ReadAllText(logPath);
            StringAssert.Contains(log, "Sending email.");
            StringAssert.Contains(log, "send_email error:");
            StringAssert.Contains(log, "recipient rejected");
            foreach (var detail in new[]
            {
                "private-sender@example.test", "private-to@example.test", "private-cc@example.test",
                "private-reply@example.test", "private subject", "private body"
            })
            {
                Assert.AreEqual(isLogPii, log.Contains(detail, StringComparison.Ordinal), detail);
            }
        }
        finally
        {
            File.Delete(logPath);
        }
    }

    [TestMethod]
    public void SendEmail_TestModeRedirectsAndSuppressesOriginalCcAndBcc()
    {
        using var server = new FakeSmtpServer();
        using var scope = CreateScope("127.0.0.1", server.Port, isTest: true);
        ((StubSettings)scope.Fw.settings).TestEmail = "safe@example.test";
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
        var settings = (StubSettings)scope.Fw.settings;
        Assert.ThrowsExactly<AuthException>(() => scope.Fw.callRoute());
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
        var settings = (StubSettings)scope.Fw.settings;
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
        var settings = (StubSettings)scope.Fw.settings;
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
        var settings = (StubSettings)scope.Fw.settings;
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
        var settings = (StubSettings)scope.Fw.settings;
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
    [DataRow(false)]
    [DataRow(true)]
    public void AdminSendEmail_RendersConsumerDefaultsForMissingOrInheritedSettings(bool isInherited)
    {
        using var scope = new FwTestScope(_ => new RejectingDb(), new Dictionary<string, string?>
        {
            ["appSettings:template"] = TestHelpers.TemplateRoot(),
        }, TestHelpers.CreateHttpContext());
        var fw = scope.Fw;
        var settings = new DefaultSettings(isInherited);
        settings.init(fw);
        TestHelpers.RegisterModel(fw, (Settings)settings);
        TestHelpers.RegisterModel(fw, (Users)new StubUsers());
        fw.Session("access_level", Users.ACL_SITEADMIN.toStr());
        fw.route.method = "GET";
        var controller = new AdminSendEmailController();
        controller.init(fw);

        var ps = controller.ShowFormAction();
        var item = (FwDict)ps["i"]!;
        string html = fw.parsePage("/admin/sendemail/showform", "form_right.html", ps);

        Assert.AreEqual(587, item["port"].toInt());
        Assert.IsTrue(item["is_ssl"].toBool());
        StringAssert.Contains(html, "value=\"587\"");
        StringAssert.Contains(html, "checked");
        Assert.IsFalse(item.ContainsKey("password"));
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

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ScheduledReportHtml_SendsOneMessageForTheGroupWithFrozenInputs(bool isTest)
    {
        using var server = new FakeSmtpServer();
        var db = new ScheduledDeliveryDb();
        using var scope = CreateScheduledScope(server, db, isTest);
        var job = ScheduledJob("html");

        var result = scope.Fw.model<FwScheduledReports>().run(job);

        Assert.AreEqual("REPORT_SENT", result["message"]);
        Assert.AreEqual(1, db.RenderQueryCount);
        Assert.AreEqual(new DateTime(2026, 2, 1), db.LastBindings["@from_date"]);
        Assert.AreEqual(DBNull.Value, db.LastBindings["@blank_date"]);
        Assert.AreEqual(0, db.LastBindings["@account_id"]);
        Assert.AreEqual(DateUtils.DATE_FORMAT_DMY, scope.Fw.userDateFormat);
        Assert.AreEqual(FW.ACTION_SHOW, scope.Fw.route.action);
        StringAssert.Contains(server.MessageData, "Content-Type: text/html");
        StringAssert.Contains(server.MessageData, "Report marker");
        StringAssert.Contains(server.MessageData, "From: sender@example.test");
        CollectionAssert.AreEqual(isTest
            ? new[] { "<sink@example.test>" }
            : new[] { "<alpha@example.test>", "<beta@example.test>" }, server.Recipients);
    }

    [TestMethod]
    public void ScheduledReportHtml_PartialRecipientRejectionLeavesDeliveryUncertain()
    {
        using var server = new FakeSmtpServer(rejectRecipient: "<beta@example.test>");
        using var scope = CreateScheduledScope(server, new ScheduledDeliveryDb());

        Assert.ThrowsExactly<FwCronUncertainException>(() => scope.Fw.model<FwScheduledReports>().run(ScheduledJob("html")));
        CollectionAssert.AreEqual(new[] { "<alpha@example.test>", "<beta@example.test>" }, server.Recipients);
        StringAssert.Contains(server.MessageData, "Report marker", "The sink accepted a message for at least one recipient.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [TestCategory("Browser"), TestCategory("PdfBrowser")]
    public void ScheduledReportPdf_AttachesRealPdfAndRemovesTemporaryAttachment(bool isSample)
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("This integration test uses installed Windows Edge.");

        using var tmpScope = new ReportTempScope();
        using var server = new FakeSmtpServer();
        var db = new ScheduledDeliveryDb();
        using var scope = CreateScheduledScope(server, db);
        var job = ScheduledJob("pdf", isSample);
        var tmp = Utils.getTmpDir();
        var before = Directory.GetFiles(tmp, "*.pdf");

        scope.Fw.model<FwScheduledReports>().run(job);

        Assert.AreEqual(1, db.RenderQueryCount);
        StringAssert.Contains(server.MessageData, "Content-Type: application/pdf");
        StringAssert.Contains(server.MessageData, "Subject: Report " + (isSample ? "Sample" : "scheduled_sales"));
        StringAssert.Contains(server.MessageData, "Report PDF attached");
        StringAssert.Contains(server.MessageData, "From: sender@example.test");
        StringAssert.Contains(server.MessageData, isSample ? "Sample.pdf" : "scheduled_sales.pdf");
        StringAssert.Contains(server.MessageData, "JVBERi0", "The attachment body starts with the base64 PDF signature.");
        CollectionAssert.AreEquivalent(before, Directory.GetFiles(tmp, "*.pdf"));
    }

    [TestMethod]
    public void ScheduledReportPdf_RenderFailureDoesNotConnectToSmtp()
    {
        using var tmpScope = new ReportTempScope();
        using var server = new FakeSmtpServer();
        using var scope = CreateScheduledScope(server, new ScheduledDeliveryDb());
        scope.Fw.config()["pdf_converter"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");

        Assert.ThrowsExactly<FileNotFoundException>(() => scope.Fw.model<FwScheduledReports>().run(ScheduledJob("pdf")));
        Assert.IsFalse(server.ConnectionAccepted);
    }

    [TestMethod]
    [DataRow("recreated")]
    [DataRow("renamed")]
    [DataRow("shadowed")]
    [DataRow("inactive")]
    [DataRow("lost_access")]
    [DataRow("changed_type")]
    [DataRow("unsafe_language")]
    [DataRow("unsplittable_recipient")]
    [DataRow("missing")]
    [DataRow("duplicate")]
    [DataRow("unknown")]
    public void ScheduledReport_RejectsInvalidIdentityAndParameterContractsBeforeSending(string invalidity)
    {
        using var server = new FakeSmtpServer();
        var db = new ScheduledDeliveryDb { Invalidity = invalidity };
        using var scope = CreateScheduledScope(server, db);
        var job = ScheduledJob("html");
        if (invalidity == "shadowed")
            job.icode = "report_Sample:V7mQ2x9KaB";

        var reports = scope.Fw.model<FwScheduledReports>();
        var payload = reports.payload(job.icode, job.payload!);
        var parameters = (FwList)payload["params"]!;
        if (invalidity == "missing")
            parameters.RemoveAt(0);
        else if (invalidity == "duplicate")
            parameters.Add(new FwDict(parameters[0]));
        else if (invalidity == "unknown")
            parameters[0]["name"] = "unknown";
        else if (invalidity == "unsafe_language")
            ((FwDict)payload["input_context"]!)["lang"] = "../unsafe";
        else if (invalidity == "unsplittable_recipient")
            payload["recipients"] = "\"mary jane\"@example.test";
        job.payload = FwScheduledReports.encodePayload(payload);

        Assert.ThrowsExactly<FwCronPauseException>(() => reports.run(job));
        Assert.AreEqual(0, db.RenderQueryCount);
        Assert.IsFalse(server.ConnectionAccepted);
    }

    [TestMethod]
    public void ScheduledReport_LiveContextDoesNotChangeSessionOrSend()
    {
        using var server = new FakeSmtpServer();
        using var scope = CreateScheduledScope(server, new ScheduledDeliveryDb(), isOffline: false);
        scope.Fw.Session("user_id", "12");

        Assert.ThrowsExactly<FwCronPauseException>(() => scope.Fw.model<FwScheduledReports>().run(ScheduledJob("html")));

        Assert.AreEqual(12, scope.Fw.userId);
        Assert.IsFalse(server.ConnectionAccepted);
    }

    [TestMethod]
    public void ReportEmailTo_UsesConfiguredSender()
    {
        using var server = new FakeSmtpServer();
        using var scope = CreateScheduledScope(server, new ScheduledDeliveryDb(), isOffline: false);
        var fw = scope.Fw;
        fw.model<Users>().reloadSession(7);
        fw.Session("XSS", "form-token");
        fw.FORM["XSS"] = "form-token";
        fw.FORM["f"] = DB.h("to_emails", "alpha@example.test");
        fw.request.Method = "POST";
        fw.setRoute(new FwRoute
        {
            controller = "AdminReports",
            action = "SendEmail",
            method = "POST",
            id = "scheduled_sales",
        });

        fw.callRoute();

        Assert.AreEqual(302, fw.response.StatusCode);
        Assert.AreEqual("/Admin/Reports/scheduled_sales", fw.response.Headers.Location.ToString());
        StringAssert.Contains(server.MessageData, "From: sender@example.test");
        StringAssert.Contains(server.MessageData, "Subject: Report scheduled_sales");
        StringAssert.Contains(server.MessageData, "Report marker");
        StringAssert.Contains(server.MessageData, "Content-Type: text/html");
    }

    private sealed class ReportTempScope : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "fw-scheduled-report-" + Guid.NewGuid().ToString("N"));
        private readonly string? previousTmp = Environment.GetEnvironmentVariable("TMP");
        private readonly string? previousTemp = Environment.GetEnvironmentVariable("TEMP");
        private readonly string? previousTmpDir = Environment.GetEnvironmentVariable("TMPDIR");

        public ReportTempScope()
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("TMP", root);
            Environment.SetEnvironmentVariable("TEMP", root);
            Environment.SetEnvironmentVariable("TMPDIR", root);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("TMP", previousTmp);
            Environment.SetEnvironmentVariable("TEMP", previousTemp);
            Environment.SetEnvironmentVariable("TMPDIR", previousTmpDir);
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
                // Edge may briefly retain its own temp files after the PDF has been delivered.
            }
        }
    }

    private static FwCron.Row ScheduledJob(string format, bool isSample = false)
    {
        var payload = new FwDict
        {
            ["source"] = isSample ? "hardcoded" : "custom",
            ["run_as_users_id"] = 7,
            ["format"] = format,
            ["recipients"] = "alpha@example.test;ALPHA@example.test;beta@example.test",
            ["input_context"] = DB.h("date_format", DateUtils.DATE_FORMAT_DMY, "time_format", DateUtils.TIME_FORMAT_24, "lang", "en"),
            ["params"] = isSample
                ? new FwList
                {
                    DB.h("name", "from_date", "type", "date", "value", "1/2/2026"),
                    DB.h("name", "to_date", "type", "date", "value", ""),
                    DB.h("name", "events_id", "type", "int", "value", ""),
                    DB.h("name", "fwentities_id", "type", "int", "value", ""),
                    DB.h("name", "users_id", "type", "int", "value", ""),
                    DB.h("name", "s", "type", "text", "value", ""),
                }
                : new FwList
                {
                    DB.h("name", "from_date", "type", "date", "value", "1/2/2026"),
                    DB.h("name", "blank_date", "type", "date", "value", ""),
                    DB.h("name", "account_id", "type", "int", "value", "0"),
                },
        };
        if (!isSample)
            payload["fwreports_id"] = 42;

        return new FwCron.Row
        {
            icode = isSample ? "report_Sample:V7mQ2x9KaB" : "report_scheduled_sales:V7mQ2x9KaB",
            timezone = "UTC",
            last_started_utc = new DateTime(2026, 11, 7, 7, 0, 0, DateTimeKind.Utc),
            payload = FwScheduledReports.encodePayload(payload),
        };
    }

    private static FwTestScope CreateScheduledScope(FakeSmtpServer server, ScheduledDeliveryDb db, bool isTest = false, bool isOffline = true)
    {
        var scope = new FwTestScope(_ => db, new Dictionary<string, string?>
        {
            ["appSettings:is_test"] = isTest.ToString(),
            ["appSettings:site_root"] = Path.Combine(TestHelpers.RepoRoot(), "osafw-app"),
            ["appSettings:template"] = TestHelpers.TemplateRoot(),
        }, context: isOffline ? null : TestHelpers.CreateHttpContext("email-tests"));
        TestHelpers.RegisterModel(scope.Fw, (Settings)new StubSettings
        {
            Host = "127.0.0.1",
            Port = server.Port,
            TestEmail = "sink@example.test",
        });
        scope.Fw.settings.init(scope.Fw);
        var users = new ScheduledDeliveryUsers(db);
        users.init(scope.Fw);
        TestHelpers.RegisterModel(scope.Fw, (Users)users);
        return scope;
    }

    private sealed class ScheduledDeliveryUsers(ScheduledDeliveryDb testDb) : Users
    {
        public override DBRow one(int id)
        {
            return id == 7 ? new DBRow(DB.h("id", id, "status", testDb.Invalidity == "inactive" ? STATUS_INACTIVE : STATUS_ACTIVE,
                "access_level", testDb.Invalidity == "lost_access" ? ACL_ADMIN : ACL_SITEADMIN,
                "fname", "Creator", "email", "creator@example.test", "date_format", DateUtils.DATE_FORMAT_MDY)) : [];
        }

        public override bool isReadOnly(int id = -1) => false;
        public override FwDict getRBAC(int? users_id = null, string? resource_icode = null) => [];
    }

    private sealed class ScheduledDeliveryDb : RejectingDb
    {
        public string Invalidity { get; init; } = "";
        public int RenderQueryCount { get; private set; }
        public FwDict LastBindings { get; private set; } = [];

        public override DBRow row(string table, FwDict where, string order_by = "")
        {
            if (table != "fwreports")
                throw Unexpected("row from " + table);
            if (Invalidity == "renamed")
                return [];

            return new DBRow(DB.h("id", Invalidity == "recreated" ? 43 : 42, "status", FwModel.STATUS_ACTIVE,
                "icode", "scheduled_sales", "iname", "Scheduled Sales", "access_level", Users.ACL_SITEADMIN,
                "sql_template", "select @from_date as result_date, @blank_date as optional_date, @account_id as account_id",
                "params_json", Invalidity == "changed_type"
                    ? """{"from_date":{"type":"text"}}"""
                    : """{"from_date":{"type":"date"},"blank_date":{"type":"date","default":"today"}}"""));
        }

        public override DBList arrayp(string sql, FwDict? @params = null)
        {
            if (sql.Contains("activity_logs", StringComparison.Ordinal))
            {
                RenderQueryCount++;
                return [];
            }

            if (sql.Contains("log_types", StringComparison.Ordinal)
                || sql.Contains("fwentities", StringComparison.Ordinal)
                || sql.Contains("users", StringComparison.Ordinal))
                return [];

            throw Unexpected("arrayp");
        }

        public override DBList arrayp(string sql, FwDict? @params, int limit)
        {
            if (!sql.Contains("@from_date", StringComparison.Ordinal))
                throw Unexpected("custom report query");

            RenderQueryCount++;
            LastBindings = @params ?? [];
            return [new DBRow(DB.h("result", "Report marker"))];
        }
    }

    private static FwTestScope CreateScope(string host, int port, bool isTest = false)
    {
        var config = new Dictionary<string, string?>
        {
            ["appSettings:is_test"] = isTest.ToString(),
        };
        var scope = new FwTestScope(_ => new RejectingDb(), config, TestHelpers.CreateHttpContext("email-tests"));
        TestHelpers.RegisterModel(scope.Fw, (Settings)new StubSettings { Host = host, Port = port });
        scope.Fw.settings.init(scope.Fw);
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
        fw.settings.init(fw);
        TestHelpers.RegisterModel(fw, (Users)new StubUsers());
        fw.Session("user_id", "7");
        fw.Session("access_level", accessLevel.toStr());
        fw.Session("XSS", "token");
        fw.route.controller = "AdminSendEmail";
        fw.route.action = FW.ACTION_SAVE;
        fw.route.method = method;
        fw.request.Method = method;
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
