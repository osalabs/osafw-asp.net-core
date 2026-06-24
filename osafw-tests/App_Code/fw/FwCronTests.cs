using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace osafw.Tests
{
    [TestClass]
    public class FwCronTests
    {
        [TestMethod]
        public void CalculateNextRun_ReturnsNullForInvalidCron()
        {
            var start = DateTime.UtcNow;
            DateTime? noEndDate = null;

            var result = FwCron.calculateNextRun("not-a-cron", start, noEndDate);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void CalculateNextRun_HonorsEndDate()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 10, DateTimeKind.Utc);
            var endDate = start.AddSeconds(10);

            var result = FwCron.calculateNextRun("* * * * *", start, endDate);

            Assert.IsNull(result, "Next run beyond the window must be null");
        }

        [TestMethod]
        public void CalculateNextRun_ComputesNextOccurrence()
        {
            var start = DateTime.UtcNow.AddMinutes(-2);
            DateTime? noEndDate = null;

            var result = FwCron.calculateNextRun("* * * * *", start, noEndDate);

            Assert.IsTrue(result.HasValue, "Cron should provide a future occurrence");
            Assert.IsTrue(result.Value > start, "Next run must be after the start date");
            Assert.IsTrue(result.Value < DateTime.UtcNow.AddMinutes(2), "Next run should be near-future for * * * * *");
        }
    }
}
