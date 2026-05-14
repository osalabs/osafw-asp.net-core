using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace osafw.Tests;

[TestClass]
public class FwModelDateTimeInputTests
{
    private sealed class TestModel : FwModel
    {
        public TestModel(FW fw) : base(fw)
        {
            table_name = "test_datetime_input";
            table_schema = new FwDict
            {
                ["fdatetime_local"] = new FwDict { ["fw_type"] = "datetime" },
                ["fdatetime_offset"] = new FwDict { ["fw_type"] = "datetimeoffset" }
            };
        }
    }

    [TestMethod]
    public void ConvertUserInput_TreatsDateTimeLocalAsUserTimezoneWallTime()
    {
        var fw = TestHelpers.CreateFw();
        fw.G["timezone"] = "Eastern Standard Time";
        fw.G["date_format"] = DateUtils.DATE_FORMAT_MDY;
        fw.G["time_format"] = DateUtils.TIME_FORMAT_24;
        var model = new TestModel(fw);
        var item = new FwDict { ["fdatetime_local"] = "2024-06-01T08:30" };

        model.convertUserInput(item);

        Assert.IsInstanceOfType(item["fdatetime_local"], typeof(DateTime));
        var converted = (DateTime)item["fdatetime_local"]!;
        Assert.AreEqual(DateTimeKind.Utc, converted.Kind);
        Assert.AreEqual(new DateTime(2024, 6, 1, 12, 30, 0, DateTimeKind.Utc), converted);
    }

    [TestMethod]
    public void ConvertUserInput_ConvertsDateTimeLocalForDateTimeOffsetFields()
    {
        var fw = TestHelpers.CreateFw();
        fw.G["timezone"] = "Eastern Standard Time";
        fw.G["date_format"] = DateUtils.DATE_FORMAT_MDY;
        fw.G["time_format"] = DateUtils.TIME_FORMAT_24;
        var model = new TestModel(fw);
        var item = new FwDict { ["fdatetime_offset"] = "2024-06-01T08:30" };

        model.convertUserInput(item);

        Assert.IsInstanceOfType(item["fdatetime_offset"], typeof(DateTimeOffset));
        var converted = (DateTimeOffset)item["fdatetime_offset"]!;
        Assert.AreEqual(new DateTimeOffset(2024, 6, 1, 12, 30, 0, TimeSpan.Zero), converted);
    }

    [TestMethod]
    public void ConvertUserInput_ConvertsExplicitIsoOffsetToUtc()
    {
        var fw = TestHelpers.CreateFw();
        fw.G["timezone"] = "Eastern Standard Time";
        fw.G["date_format"] = DateUtils.DATE_FORMAT_MDY;
        fw.G["time_format"] = DateUtils.TIME_FORMAT_24;
        var model = new TestModel(fw);
        var item = new FwDict
        {
            ["fdatetime_local"] = "2024-06-01T08:30:00-05:00",
            ["fdatetime_offset"] = "2024-06-01T08:45:00-05:00"
        };

        model.convertUserInput(item);

        var datetime = (DateTime)item["fdatetime_local"]!;
        var offset = (DateTimeOffset)item["fdatetime_offset"]!;
        Assert.AreEqual(new DateTime(2024, 6, 1, 13, 30, 0, DateTimeKind.Utc), datetime);
        Assert.AreEqual(new DateTimeOffset(2024, 6, 1, 13, 45, 0, TimeSpan.Zero), offset);
    }
}
