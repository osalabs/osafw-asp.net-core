using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass]
public class FwCronServiceTests
{
    private class TestCronService : FwCronService
    {
        public int Calls;

        public TestCronService() : base(new ConfigurationBuilder().Build())
        {
        }

        protected override TimeSpan PollingInterval => TimeSpan.FromMilliseconds(10);

        protected override void ProcessJobs(CancellationToken ct)
        {
            Calls++;
        }

        public Task RunAsync(CancellationToken ct) => base.ExecuteAsync(ct);
    }

    [TestMethod]
    public async Task ExecuteAsync_StopsOnCancellation()
    {
        var service = new TestCronService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));

        try
        {
            await service.RunAsync(cts.Token);
            Assert.Fail("Expected cancellation");
        }
        catch (TaskCanceledException)
        {
        }

        Assert.IsTrue(service.Calls > 0);
    }
}
