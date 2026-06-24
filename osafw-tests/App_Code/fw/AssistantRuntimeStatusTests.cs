using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace osafw.Tests;

[TestClass]
public class AssistantRuntimeStatusTests
{
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
    public void RuntimeStatus_ReportsDisabledWorkerFromAppConfig()
    {
        var fw = createFwWithWorker(false);

        var status = new AssistantAppService(fw).RuntimeStatus();

        Assert.IsFalse(status.worker_enabled);
    }

    [TestMethod]
    public void RuntimeStatus_ReportsEnabledWorkerFromAppConfig()
    {
        var fw = createFwWithWorker(true);

        var status = new AssistantAppService(fw).RuntimeStatus();

        Assert.IsTrue(status.worker_enabled);
    }

    private static FW createFwWithWorker(bool isWorkerEnabled)
    {
        var fw = TestHelpers.CreateFw(new Dictionary<string, string?>
        {
            ["appSettings:ASSISTANT_WORKER_ENABLED"] = isWorkerEnabled ? "true" : "false"
        });

        var settings = new StaticSettings(new Dictionary<string, string>
        {
            ["ASSISTANT_ENABLED"] = "1",
            ["OPENAI_API_KEY"] = "sk-test"
        });
        settings.init(fw);
        TestHelpers.RegisterModel(fw, (Settings)settings);

        return fw;
    }
}
