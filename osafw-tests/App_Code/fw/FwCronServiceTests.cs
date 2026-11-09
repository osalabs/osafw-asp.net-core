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
        public bool IsFailFirstPoll { get; set; }
        protected override TimeSpan PollingInterval => TimeSpan.Zero;

        public TestCronService(CancellationTokenSource cancellation) : base(new ConfigurationBuilder().Build())
        {
            this.cancellation = cancellation;
        }

        protected override void ProcessJobs(CancellationToken ct)
        {
            Calls++;
            if (IsFailFirstPoll && Calls == 1)
                throw new InvalidOperationException("Poll failed.");
            cancellation.Cancel();
        }

        public Task RunAsync(CancellationToken ct) => base.ExecuteAsync(ct);
    }

    [TestMethod]
    [DataRow(false, 1)]
    [DataRow(true, 2)]
    public async Task ExecuteAsync_StopsOnCancellationAndRetriesFailedPoll(bool is_fail_first_poll, int expected_calls)
    {
        using var cts = new CancellationTokenSource();
        using var service = new TestCronService(cts) { IsFailFirstPoll = is_fail_first_poll };

        var error = await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.RunAsync(cts.Token));
        Assert.AreEqual(cts.Token, error.CancellationToken);
        Assert.AreEqual(expected_calls, service.Calls);
    }
}
