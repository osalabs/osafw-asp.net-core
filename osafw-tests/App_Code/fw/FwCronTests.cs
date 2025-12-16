using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace osafw.Tests
{
    [TestClass]
    public class FwCronTests
    {
        private static MethodInfo CalculateNextRun => typeof(FwCron).GetMethod("calculateNextRun", BindingFlags.NonPublic | BindingFlags.Static)!;

        [TestMethod]
        public void CalculateNextRun_ReturnsNullForInvalidCron()
        {
            var start = DateTime.UtcNow;
            DateTime? noEndDate = null;

            var result = (DateTime?)CalculateNextRun.Invoke(null, new object?[] { "not-a-cron", start, noEndDate });

            Assert.IsNull(result);
        }

        [TestMethod]
        public void CalculateNextRun_HonorsEndDate()
        {
            var start = DateTime.UtcNow;
            var endDate = start.AddSeconds(10);

            var result = (DateTime?)CalculateNextRun.Invoke(null, new object[] { "* * * * *", start, endDate });

            Assert.IsNull(result, "Next run beyond the window must be null");
        }

        [TestMethod]
        public void CalculateNextRun_ComputesNextOccurrence()
        {
            var start = DateTime.UtcNow.AddMinutes(-2);
            DateTime? noEndDate = null;

            var result = (DateTime?)CalculateNextRun.Invoke(null, new object?[] { "* * * * *", start, noEndDate });

            Assert.IsTrue(result.HasValue, "Cron should provide a future occurrence");
            Assert.IsTrue(result.Value > start, "Next run must be after the start date");
            Assert.IsTrue(result.Value < DateTime.UtcNow.AddMinutes(2), "Next run should be near-future for * * * * *");
        }
    }
}
