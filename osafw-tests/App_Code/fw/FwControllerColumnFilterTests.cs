using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace osafw.Tests;

[TestClass]
public class FwControllerColumnFilterTests
{
    private class StubModel : FwModel
    {
        public StubModel() : base() => table_name = "stub";
    }

    private class TestController : FwController
    {
        public TestController(FW fw) : base(fw)
        {
            fw.G["controller.action"] = "ColumnFilterTests";
            model0 = new StubModel();
            is_dynamic_index = true;
            view_list_defaults = "name event_utc status amount flag lookup_id";
            view_list_map = new FwDict
            {
                ["name"] = "Name",
                ["event_utc"] = "Event",
                ["status"] = "Status",
                ["amount"] = "Amount",
                ["flag"] = "Flag",
                ["lookup_id"] = "Lookup",
            };
            list_column_filters = new FwDict
            {
                ["enabled"] = true,
                ["fields"] = new FwDict
                {
                    ["name"] = new FwDict { ["type"] = "text" },
                    ["event_utc"] = new FwDict { ["type"] = "date_range", ["field_storage_type"] = "datetime" },
                    ["status"] = new FwDict { ["type"] = "multi_select", ["field_storage_type"] = "int" },
                    ["amount"] = new FwDict { ["type"] = "number_conditions", ["field_storage_type"] = "decimal" },
                    ["flag"] = new FwDict { ["type"] = "boolean" },
                    ["lookup_id"] = new FwDict { ["type"] = "autocomplete", ["field_storage_type"] = "int" },
                }
            };
            is_list_column_filters = true;
        }

        public string WhereSql => list_where;
        public FwDict WhereParams => list_where_params;

        public override string getViewListUserFields()
        {
            return view_list_defaults;
        }

        public void ApplySearch(FwDict search)
        {
            list_filter_search = search;
            setListSearchAdvanced();
        }
    }

    private static TestController BuildController()
    {
        var fw = TestHelpers.CreateFw();
        return new TestController(fw);
    }

    [TestMethod]
    public void JsonTextStartsWith_UsesParameterizedLike()
    {
        var controller = BuildController();

        controller.ApplySearch(new FwDict
        {
            ["name"] = Utils.jsonEncode(new FwDict { ["type"] = "text", ["op"] = "starts_with", ["value"] = "Acme" })
        });

        StringAssert.Contains(controller.WhereSql, "LIKE @cf_name_text_0");
        Assert.AreEqual("Acme%", controller.WhereParams["cf_name_text_0"]);
    }

    [TestMethod]
    public void JsonTextNotStartsAndNotEndsWith_UseParameterizedNotLike()
    {
        var controller = BuildController();

        controller.ApplySearch(new FwDict
        {
            ["name"] = Utils.jsonEncode(new FwDict { ["type"] = "text", ["op"] = "not_starts_with", ["value"] = "Acme" })
        });

        StringAssert.Contains(controller.WhereSql, "NOT LIKE @cf_name_text_0");
        Assert.AreEqual("Acme%", controller.WhereParams["cf_name_text_0"]);

        controller = BuildController();
        controller.ApplySearch(new FwDict
        {
            ["name"] = Utils.jsonEncode(new FwDict { ["type"] = "text", ["op"] = "not_ends_with", ["value"] = "Acme" })
        });

        StringAssert.Contains(controller.WhereSql, "NOT LIKE @cf_name_text_0");
        Assert.AreEqual("%Acme", controller.WhereParams["cf_name_text_0"]);
    }

    [TestMethod]
    public void LegacyStartsWith_SyntaxStillWorks()
    {
        var controller = BuildController();

        controller.ApplySearch(new FwDict { ["name"] = "^Acme" });

        StringAssert.Contains(controller.WhereSql, "LIKE 'Acme%'");
    }

    [TestMethod]
    public void LegacyEndsWith_SyntaxStillWorks()
    {
        var controller = BuildController();

        controller.ApplySearch(new FwDict { ["name"] = "$Acme" });

        StringAssert.Contains(controller.WhereSql, "LIKE '%Acme'");
    }

    [TestMethod]
    public void JsonDateRange_UsesLocalDayBoundariesAndExclusiveDatetimeTo()
    {
        var controller = BuildController();

        controller.ApplySearch(new FwDict
        {
            ["event_utc"] = Utils.jsonEncode(new FwDict { ["type"] = "date_range", ["from"] = "2026-01-01", ["to"] = "2026-01-31" })
        });

        StringAssert.Contains(controller.WhereSql, "[event_utc] >= @cf_event_utc_from_0");
        StringAssert.Contains(controller.WhereSql, "[event_utc] < @cf_event_utc_to_1");
        Assert.AreEqual(new DateTime(2026, 1, 1), controller.WhereParams["cf_event_utc_from_0"]);
        Assert.AreEqual(new DateTime(2026, 2, 1), controller.WhereParams["cf_event_utc_to_1"]);
    }

    [TestMethod]
    public void JsonMultiSelect_NumberAndBooleanPredicates_AreParameterized()
    {
        var controller = BuildController();

        controller.ApplySearch(new FwDict
        {
            ["status"] = Utils.jsonEncode(new FwDict { ["type"] = "multi_select", ["values"] = new ObjList { "0", "10" } }),
            ["amount"] = Utils.jsonEncode(new FwDict { ["type"] = "number_conditions", ["not_between_from"] = "3", ["not_between_to"] = "7" }),
            ["flag"] = Utils.jsonEncode(new FwDict { ["type"] = "boolean", ["value"] = "1" })
        });

        StringAssert.Contains(controller.WhereSql, "[status] IN (@cf_status_in_0,@cf_status_in_1)");
        StringAssert.Contains(controller.WhereSql, "([amount] < @cf_amount_notfrom_2 OR [amount] > @cf_amount_notto_3)");
        StringAssert.Contains(controller.WhereSql, "[flag] = @cf_flag_bool_4");
        Assert.AreEqual(0L, controller.WhereParams["cf_status_in_0"]);
        Assert.AreEqual(10L, controller.WhereParams["cf_status_in_1"]);
        Assert.AreEqual(3m, controller.WhereParams["cf_amount_notfrom_2"]);
        Assert.AreEqual(7m, controller.WhereParams["cf_amount_notto_3"]);
        Assert.AreEqual(1, controller.WhereParams["cf_flag_bool_4"]);
    }

    [TestMethod]
    public void JsonNumberNotEqual_UsesParameterizedPredicate()
    {
        var controller = BuildController();

        controller.ApplySearch(new FwDict
        {
            ["amount"] = Utils.jsonEncode(new FwDict { ["type"] = "number_conditions", ["not_equal"] = "7" })
        });

        StringAssert.Contains(controller.WhereSql, "[amount] <> @cf_amount_neq_0");
        Assert.AreEqual(7m, controller.WhereParams["cf_amount_neq_0"]);
    }

    [TestMethod]
    public void UnknownColumnFilter_IsIgnored()
    {
        var controller = BuildController();

        controller.ApplySearch(new FwDict
        {
            ["unknown"] = Utils.jsonEncode(new FwDict { ["type"] = "text", ["value"] = "Acme" })
        });

        Assert.AreEqual(" 1=1 ", controller.WhereSql);
        Assert.AreEqual(0, controller.WhereParams.Count);
    }

    [TestMethod]
    public void AutocompleteFilter_UsesSelectedIdFromFormattedValue()
    {
        var controller = BuildController();

        controller.ApplySearch(new FwDict
        {
            ["lookup_id"] = Utils.jsonEncode(new FwDict { ["type"] = "autocomplete", ["values"] = new ObjList { "Alpha ::: 12" } })
        });

        StringAssert.Contains(controller.WhereSql, "[lookup_id] IN (@cf_lookup_id_in_0)");
        Assert.AreEqual(12L, controller.WhereParams["cf_lookup_id_in_0"]);
    }
}
