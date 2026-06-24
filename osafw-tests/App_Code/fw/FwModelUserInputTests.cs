using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace osafw.Tests;

[TestClass]
public class FwModelUserInputTests
{
    private sealed class NumericInputModel : FwModel
    {
        public NumericInputModel(FW fw) : base(fw)
        {
            table_name = "test_numeric_input";
            table_schema = new FwDict
            {
                ["optional_int"] = new FwDict { ["fw_type"] = "int", ["is_nullable"] = 1 },
                ["required_int"] = new FwDict { ["fw_type"] = "int", ["is_nullable"] = 0 }
            };
        }
    }

    [TestMethod]
    public void ConvertUserInput_NullableIntEmptyStringBecomesDBNull()
    {
        var model = new NumericInputModel(TestHelpers.CreateFw());
        var item = new FwDict { ["optional_int"] = "" };

        model.convertUserInput(item);

        Assert.AreSame(DBNull.Value, item["optional_int"]);
    }

    [TestMethod]
    public void ConvertUserInput_NullableIntWhitespaceIsNotEmptyStringMarker()
    {
        var model = new NumericInputModel(TestHelpers.CreateFw());
        var item = new FwDict { ["optional_int"] = "   " };

        model.convertUserInput(item);

        Assert.AreEqual("   ", item["optional_int"]);
    }

    [TestMethod]
    public void ConvertUserInput_NullableIntExplicitZeroIsPreserved()
    {
        var model = new NumericInputModel(TestHelpers.CreateFw());
        var item = new FwDict { ["optional_int"] = "0" };

        model.convertUserInput(item);

        Assert.AreEqual("0", item["optional_int"]);
    }

    [TestMethod]
    public void ConvertUserInput_NonNullableIntEmptyStringIsPreserved()
    {
        var model = new NumericInputModel(TestHelpers.CreateFw());
        var item = new FwDict { ["required_int"] = "" };

        model.convertUserInput(item);

        Assert.AreEqual("", item["required_int"]);
    }
}
