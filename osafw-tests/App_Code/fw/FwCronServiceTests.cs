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
        private readonly CancellationTokenSource cancellation;
        public int Calls;

        public TestCronService(CancellationTokenSource cancellation) : base(new ConfigurationBuilder().Build())
        {
            this.cancellation = cancellation;
        }

        protected override void ProcessJobs(CancellationToken ct)
        {
            Calls++;
            cancellation.Cancel();
        }

        public Task RunAsync(CancellationToken ct) => base.ExecuteAsync(ct);
    }

    [TestMethod]
    public async Task ExecuteAsync_StopsOnCancellation()
    {
        using var cts = new CancellationTokenSource();
        using var service = new TestCronService(cts);

        var error = await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.RunAsync(cts.Token));
        Assert.AreEqual(cts.Token, error.CancellationToken);
        Assert.AreEqual(1, service.Calls);
    }
}
