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
        public string TestEmail { get; init; } = "";

        public override DBRow oneByIcode(string icode)
        {
            return icode == Settings.ICODE_TEST_EMAIL
                ? new DBRow(new FwDict { ["ivalue"] = TestEmail })
                : [];
        }
    }

    private sealed class FakeSmtpServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly Task serverTask;
        private readonly bool rejectConnection;

        public int Port { get; }
        public List<string> Recipients { get; } = [];
        public string MessageData { get; private set; } = "";

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
            catch (AggregateException) when (serverTask.IsFaulted && serverTask.Exception?.InnerException is SocketException) { }
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
        TestHelpers.RegisterModel(scope.Fw, (Settings)new StubSettings());
        scope.Fw.config()["test_email"] = "";
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

        var configuredMail = scope.Fw.config("mail") as FwDict;
        Assert.IsNotNull(configuredMail);
        Assert.AreEqual("configured.example.test", configuredMail["host"]);
        Assert.AreEqual(2525, configuredMail["port"].toInt());
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
        TestHelpers.RegisterModel(scope.Fw, (Settings)new StubSettings { TestEmail = "safe@example.test" });
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

    private static FwTestScope CreateScope(string host, int port, bool isTest = false)
    {
        var settings = new Dictionary<string, string?>
        {
            ["appSettings:mail_from"] = "sender@example.test",
            ["appSettings:is_test"] = isTest.ToString(),
            ["appSettings:mail:host"] = host,
            ["appSettings:mail:port"] = port.ToString(),
            ["appSettings:mail:is_ssl"] = "false",
            ["appSettings:mail:username"] = "",
            ["appSettings:mail:password"] = "",
        };
        return new FwTestScope(_ => new RejectingDb(), settings, TestHelpers.CreateHttpContext("email-tests"));
    }
}
