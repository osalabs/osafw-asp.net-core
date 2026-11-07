using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace osafw.Tests;

[TestClass]
public class FwCronTests
{
    [TestMethod]
    public void CalculateNextRun_UsesInclusiveStartAndExclusiveEnd()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 10, DateTimeKind.Utc);
        var start = now.Date.AddMinutes(2);
        var end = start.AddMinutes(1);

        Assert.AreEqual(start, FwCron.calculateNextRun("* * * * *", now, end, TimeZoneInfo.Utc, start));
        Assert.IsNull(FwCron.calculateNextRun("* * * * *", start, end, TimeZoneInfo.Utc, start));
        Assert.IsNull(FwCron.calculateNextRun("not-a-cron", now, null));
        Assert.IsNull(FwCron.calculateNextRun("0 * * * * *", now, null));
        Assert.AreEqual(now.Date.AddMinutes(1), FwCron.calculateNextRun("* * * * *", now, null));
    }

    [TestMethod]
    public void CalculateNextRun_UsesCronosDaylightSavingBehavior()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        var spring = new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc);
        Assert.AreEqual(spring.AddHours(8), FwCron.calculateNextRun("30 2 * * *", spring, null, zone));

        var fall = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var fixedTime = FwCron.calculateNextRun("30 1 * * *", fall, null, zone);
        Assert.AreEqual(fall.AddHours(6.5), fixedTime);
        Assert.AreEqual(fall.AddDays(1).AddHours(7.5), FwCron.calculateNextRun("30 1 * * *", fixedTime!.Value, null, zone));

        var firstRepeatedHour = fall.AddHours(6.5);
        Assert.AreEqual(fall.AddHours(7), FwCron.calculateNextRun("*/30 1 * * *", firstRepeatedHour, null, zone));
    }

    [TestMethod]
    public void ParseResult_ReturnsEmptyForUnreadableOrUnrecognizedEnvelope()
    {
        Assert.HasCount(0, FwCron.parseResult(null));
        Assert.HasCount(0, FwCron.parseResult("{\"status\":\"succeeded\",\"message\":\"Completed\"}garbage"));
        Assert.HasCount(0, FwCron.parseResult("{\"status\":\"future-status\",\"message\":\"Completed\"}"));
        Assert.AreEqual("Completed", FwCron.parseResult("{\"status\":\"succeeded\",\"message\":\"Completed\"}")["message"]);
    }
}
