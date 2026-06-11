using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class FwDynamicControllerColumnFilterTests
{
    private class StubModel : FwModel
    {
        public StubModel() : base() => table_name = "stub";
    }

    private class LookupOptionsModel : FwModel
    {
        public int ListSelectOptionsCalls { get; private set; }
        public object? LastSelectedId { get; private set; }

        public override FwList listSelectOptions(FwDict? def = null, object? selected_id = null, bool valueFromIname = false, FwDict? baseWhere = null, string? inameSql = null)
        {
            ListSelectOptionsCalls++;
            LastSelectedId = selected_id;
            return
            [
                new FwDict { ["id"] = "12", ["iname"] = "Selected Lookup" },
                new FwDict { ["id"] = "13", ["iname"] = "Other Lookup" },
            ];
        }
    }

    private class TestDynamicController : FwDynamicController
    {
        public string WhereSql => list_where;
        public FwDict WhereParams => list_where_params;

        public void InitForTest(FW fw, FwDict config)
        {
            fw.G["controller.action"] = "DynamicColumnFilterTests";
            init(fw);
            base_url = "/Admin/DynamicColumnFilterTests";
            model0 = new StubModel();
            model0.init(fw);
            loadControllerConfig(config);
        }

        protected override FwDict getListUserView()
        {
            return [];
        }

        public void ApplySearch(FwDict search)
        {
            list_filter_search = search;
            setListSearchAdvanced();
        }

        public void BuildHeaders(FwDict? search = null)
        {
            list_filter_search = search ?? [];
            setViewList(false);
        }

        public FwDict HeaderFor(string fieldName)
        {
            foreach (FwDict header in list_headers)
                if (header["field_name"].toStr() == fieldName)
                    return header;

            throw new InvalidOperationException($"Header not found: {fieldName}");
        }
    }

    private class TestVueController : FwVueController
    {
        public void InitForTest(FW fw, FwDict config)
        {
            fw.G["controller.action"] = "VueColumnFilterTests";
            init(fw);
            base_url = "/Admin/VueColumnFilterTests";
            model0 = new StubModel();
            model0.init(fw);
            loadControllerConfig(config);
        }

        protected override FwDict getListUserView()
        {
            return [];
        }

        public void BuildHeaders()
        {
            list_filter_search = [];
            setViewList(false);
        }

        public FwDict HeaderFor(string fieldName)
        {
            foreach (FwDict header in list_headers)
                if (header["field_name"].toStr() == fieldName)
                    return header;

            throw new InvalidOperationException($"Header not found: {fieldName}");
        }
    }

    private static TestDynamicController BuildController(FW? fw = null, FwDict? config = null, bool registerLookupOptions = true)
    {
        fw ??= TestHelpers.CreateFw();
        if (registerLookupOptions)
            TestHelpers.RegisterModel(fw, new LookupOptionsModel());

        var controller = new TestDynamicController();
        controller.InitForTest(fw, config ?? BuildConfig());
        return controller;
    }

    private static FwDict BuildConfig(bool enabled = true, FwDict? fields = null)
    {
        var config = new FwDict
        {
            ["is_dynamic_index"] = true,
            ["view_list_defaults"] = "name event_utc status amount flag lookup_id lookup_model_id lookup_model_alt_id inline_status demo_dicts_iname dict_link_auto_iname",
            ["view_list_map"] = new FwDict
            {
                ["name"] = "Name",
                ["event_utc"] = "Event",
                ["status"] = "Status",
                ["amount"] = "Amount",
                ["flag"] = "Flag",
                ["lookup_id"] = "Lookup",
                ["lookup_model_id"] = "Lookup Model",
                ["lookup_model_alt_id"] = "Lookup Model Alt",
                ["inline_status"] = "Inline Status",
                ["demo_dicts_iname"] = "Demo Dict",
                ["dict_link_auto_iname"] = "Linked Dict",
            },
            ["showform_fields"] = new FwList
            {
                new FwDict { ["field"] = "name", ["type"] = "input" },
                new FwDict { ["field"] = "event_utc", ["type"] = "datetime_local" },
                new FwDict { ["field"] = "status", ["type"] = "select", ["lookup_tpl"] = "/common/sel/status.sel" },
                new FwDict { ["field"] = "amount", ["type"] = "number" },
                new FwDict { ["field"] = "flag", ["type"] = "cb" },
            },
        };

        var filterConfig = new FwDict { ["enabled"] = enabled };
        if (fields != null)
            filterConfig["fields"] = fields;
        config["list_column_filters"] = filterConfig;
        return config;
    }

    private static FwDict ExplicitFilterFields()
    {
        return new FwDict
        {
            ["name"] = new FwDict { ["type"] = "text" },
            ["event_utc"] = new FwDict { ["type"] = "date_range", ["field_storage_type"] = "datetime" },
            ["status"] = new FwDict { ["type"] = "multi_select", ["field_storage_type"] = "int" },
            ["amount"] = new FwDict { ["type"] = "number_conditions", ["field_storage_type"] = "decimal" },
            ["flag"] = new FwDict { ["type"] = "boolean" },
            ["lookup_id"] = new FwDict { ["type"] = "autocomplete", ["field_storage_type"] = "int" },
            ["lookup_model_id"] = new FwDict { ["type"] = "multi_select", ["field_storage_type"] = "int", ["lookup_model"] = nameof(LookupOptionsModel) },
            ["lookup_model_alt_id"] = new FwDict { ["type"] = "multi_select", ["field_storage_type"] = "int", ["lookup_model"] = nameof(LookupOptionsModel) },
            ["inline_status"] = new FwDict
            {
                ["type"] = "multi_select",
                ["options"] = new FwDict
                {
                    ["A"] = "Active",
                    ["I"] = "Inactive",
                },
            },
            ["demo_dicts_iname"] = new FwDict { ["type"] = "text" },
            ["dict_link_auto_iname"] = new FwDict
            {
                ["type"] = "autocomplete",
                ["filter_field"] = "lookup_id",
                ["field_storage_type"] = "int",
                ["autocomplete_url"] = "/Admin/DynamicColumnFilterTests/(Autocomplete)?model=LookupOptionsModel&q=",
            },
        };
    }

    private static FwDict FilterFor(FwDict header)
    {
        return header["filter"] as FwDict ?? throw new InvalidOperationException("Expected column filter metadata.");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "osafw-app", "App_Data", "template")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    [TestMethod]
    public void OptInDisabled_KeepsLegacySearchAndDoesNotLoadLookupOptions()
    {
        var fw = TestHelpers.CreateFw();
        var lookupOptions = new LookupOptionsModel();
        TestHelpers.RegisterModel(fw, lookupOptions);
        var controller = BuildController(fw, BuildConfig(enabled: false, fields: ExplicitFilterFields()), registerLookupOptions: false);

        controller.BuildHeaders();
        controller.ApplySearch(new FwDict { ["name"] = "^Acme" });

        Assert.AreEqual(0, lookupOptions.ListSelectOptionsCalls);
        Assert.IsFalse(controller.HeaderFor("lookup_model_id").ContainsKey("filter"));
        StringAssert.Contains(controller.WhereSql, "LIKE 'Acme%'");
    }

    [TestMethod]
    public void EnabledWithoutExplicitFields_InfersFiltersFromDynamicFormDefinitions()
    {
        var controller = BuildController(config: BuildConfig(enabled: true));

        controller.BuildHeaders();

        Assert.AreEqual("text", FilterFor(controller.HeaderFor("name"))["type"]);
        Assert.AreEqual("date_range", FilterFor(controller.HeaderFor("event_utc"))["type"]);
        Assert.AreEqual("multi_select", FilterFor(controller.HeaderFor("status"))["type"]);
        Assert.AreEqual("number_conditions", FilterFor(controller.HeaderFor("amount"))["type"]);
        Assert.AreEqual("boolean", FilterFor(controller.HeaderFor("flag"))["type"]);
        Assert.AreEqual("none", FilterFor(controller.HeaderFor("demo_dicts_iname"))["type"]);
    }

    [TestMethod]
    public void JsonTextStartsWith_UsesParameterizedLike()
    {
        var controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));

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
        var controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));

        controller.ApplySearch(new FwDict
        {
            ["name"] = Utils.jsonEncode(new FwDict { ["type"] = "text", ["op"] = "not_starts_with", ["value"] = "Acme" })
        });

        StringAssert.Contains(controller.WhereSql, "NOT LIKE @cf_name_text_0");
        Assert.AreEqual("Acme%", controller.WhereParams["cf_name_text_0"]);

        controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));
        controller.ApplySearch(new FwDict
        {
            ["name"] = Utils.jsonEncode(new FwDict { ["type"] = "text", ["op"] = "not_ends_with", ["value"] = "Acme" })
        });

        StringAssert.Contains(controller.WhereSql, "NOT LIKE @cf_name_text_0");
        Assert.AreEqual("%Acme", controller.WhereParams["cf_name_text_0"]);
    }

    [TestMethod]
    public void LegacySearchSyntaxStillWorksForDynamicFilters()
    {
        var controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));

        controller.ApplySearch(new FwDict { ["name"] = "^Acme" });

        StringAssert.Contains(controller.WhereSql, "LIKE 'Acme%'");
    }

    [TestMethod]
    public void JsonDateRange_UsesLocalDayBoundariesAndExclusiveDatetimeTo()
    {
        var controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));

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
        var controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));

        controller.ApplySearch(new FwDict
        {
            ["status"] = Utils.jsonEncode(new FwDict { ["type"] = "multi_select", ["values"] = new ObjList { "0", "10" } }),
            ["amount"] = Utils.jsonEncode(new FwDict { ["type"] = "number_conditions", ["not_between_from"] = "3", ["not_between_to"] = "7" }),
            ["flag"] = Utils.jsonEncode(new FwDict { ["type"] = "boolean", ["value"] = "1" })
        });

        StringAssert.Contains(controller.WhereSql, "[status] IN (@cf_status_in_0,@cf_status_in_1)");
        StringAssert.Contains(controller.WhereSql, "([amount] < @cf_amount_notfrom_2 OR [amount] > @cf_amount_notto_3)");
        StringAssert.Contains(controller.WhereSql, "[flag] = @cf_flag_bool_4");
        Assert.AreEqual("0", controller.WhereParams["cf_status_in_0"]);
        Assert.AreEqual("10", controller.WhereParams["cf_status_in_1"]);
        Assert.AreEqual(3m, controller.WhereParams["cf_amount_notfrom_2"]);
        Assert.AreEqual(7m, controller.WhereParams["cf_amount_notto_3"]);
        Assert.AreEqual(1, controller.WhereParams["cf_flag_bool_4"]);
    }

    [TestMethod]
    public void JsonNumberNotEqual_UsesParameterizedPredicate()
    {
        var controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));

        controller.ApplySearch(new FwDict
        {
            ["amount"] = Utils.jsonEncode(new FwDict { ["type"] = "number_conditions", ["not_equal"] = "7" })
        });

        StringAssert.Contains(controller.WhereSql, "[amount] <> @cf_amount_neq_0");
        Assert.AreEqual(7m, controller.WhereParams["cf_amount_neq_0"]);
    }

    [TestMethod]
    public void ExplicitComplexAliasFilter_UsesConfiguredFilterField()
    {
        var controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));

        controller.ApplySearch(new FwDict
        {
            ["dict_link_auto_iname"] = Utils.jsonEncode(new FwDict { ["type"] = "autocomplete", ["values"] = new ObjList { "Alpha ::: 12" } })
        });

        StringAssert.Contains(controller.WhereSql, "[lookup_id] IN (@cf_dict_link_auto_iname_in_0)");
        Assert.AreEqual("12", controller.WhereParams["cf_dict_link_auto_iname_in_0"]);
    }

    [TestMethod]
    public void UnknownColumnFilter_IsIgnored()
    {
        var controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));

        controller.ApplySearch(new FwDict
        {
            ["unknown"] = Utils.jsonEncode(new FwDict { ["type"] = "text", ["value"] = "Acme" })
        });

        Assert.AreEqual(" 1=1 ", controller.WhereSql);
        Assert.AreEqual(0, controller.WhereParams.Count);
    }

    [TestMethod]
    public void InvalidJsonFallsBackToLegacySearch()
    {
        var controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));

        controller.ApplySearch(new FwDict { ["name"] = "{not-json" });

        StringAssert.Contains(controller.WhereSql, "LIKE '%{not-json%'");
    }

    [TestMethod]
    public void MultiSelectHeader_LoadsLookupModelOptionsOnce_ForRepeatedLookupSource()
    {
        var fw = TestHelpers.CreateFw();
        var lookupOptions = new LookupOptionsModel();
        TestHelpers.RegisterModel(fw, lookupOptions);
        var controller = BuildController(fw, BuildConfig(fields: ExplicitFilterFields()), registerLookupOptions: false);

        controller.BuildHeaders();

        Assert.AreEqual(1, lookupOptions.ListSelectOptionsCalls);
        var header = controller.HeaderFor("lookup_model_id");
        var altHeader = controller.HeaderFor("lookup_model_alt_id");
        var filter = FilterFor(header);
        var altFilter = FilterFor(altHeader);
        Assert.IsInstanceOfType(filter["options"], typeof(FwList));
        Assert.AreEqual(2, ((FwList)filter["options"]!).Count);
        Assert.AreSame(filter["options"], altFilter["options"]);
    }

    [TestMethod]
    public void MultiSelectHeader_ReusesSelectedLookupModelOptions_WhenSelectedValueNeedsLabel()
    {
        var fw = TestHelpers.CreateFw();
        var lookupOptions = new LookupOptionsModel();
        TestHelpers.RegisterModel(fw, lookupOptions);
        var controller = BuildController(fw, BuildConfig(fields: ExplicitFilterFields()), registerLookupOptions: false);

        controller.BuildHeaders(new FwDict
        {
            ["lookup_model_id"] = Utils.jsonEncode(new FwDict { ["type"] = "multi_select", ["values"] = new ObjList { "12" } }),
            ["lookup_model_alt_id"] = Utils.jsonEncode(new FwDict { ["type"] = "multi_select", ["values"] = new ObjList { "12" } })
        });

        Assert.AreEqual(1, lookupOptions.ListSelectOptionsCalls);
        Assert.AreEqual("12", lookupOptions.LastSelectedId);
        var header = controller.HeaderFor("lookup_model_id");
        var altHeader = controller.HeaderFor("lookup_model_alt_id");
        var filter = FilterFor(header);
        var altFilter = FilterFor(altHeader);
        Assert.AreEqual(1, filter["values_count"]);
        Assert.IsInstanceOfType(filter["selected_options"], typeof(FwList));
        Assert.AreEqual("Selected Lookup", ((FwList)filter["selected_options"]!)[0]["iname"]);
        Assert.AreSame(filter["options"], altFilter["options"]);
    }

    [TestMethod]
    public void MultiSelectHeader_NormalizesInlineOptions()
    {
        var controller = BuildController(config: BuildConfig(fields: ExplicitFilterFields()));

        controller.BuildHeaders();

        var header = controller.HeaderFor("inline_status");
        if (FilterFor(header)["options"] is FwList options)
        {
            Assert.AreEqual(2, options.Count);
            return;
        }

        Assert.Fail("Expected inline filter options to be loaded.");
    }

    [TestMethod]
    public void LookupTplHeader_LoadsTemplateOptions()
    {
        var fw = TestHelpers.CreateFw(new Dictionary<string, string?>
        {
            ["appSettings:template"] = Path.Combine(RepoRoot(), "osafw-app", "App_Data", "template"),
        });
        var controller = BuildController(fw, BuildConfig(enabled: true), registerLookupOptions: false);

        controller.BuildHeaders();

        var options = FilterFor(controller.HeaderFor("status"))["options"] as FwList;
        Assert.IsNotNull(options);
        Assert.IsTrue(options.Count > 0);
    }

    [TestMethod]
    public void ServerFilterCell_RendersNestedFilterMetadata()
    {
        var fw = TestHelpers.CreateFw(new Dictionary<string, string?>
        {
            ["appSettings:template"] = Path.Combine(RepoRoot(), "osafw-app", "App_Data", "template"),
        });
        var controller = BuildController(fw, BuildConfig(fields: ExplicitFilterFields()));

        controller.BuildHeaders(new FwDict
        {
            ["inline_status"] = Utils.jsonEncode(new FwDict { ["type"] = "multi_select", ["values"] = new ObjList { "A" } })
        });

        var html = fw.parsePage("/common/list/filters", "cell.html", controller.HeaderFor("inline_status"));

        StringAssert.Contains(html, "data-column-filter-type=\"multi_select\"");
        StringAssert.Contains(html, "name=\"search[inline_status]\"");
        StringAssert.Contains(html, "fw-column-filter-summary is-active");
        StringAssert.Contains(html, "<option value=\"A\" selected>Active</option>");
    }

    [TestMethod]
    public void ServerFilterCell_RendersControllerLocalCustomTemplate()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "column-filter-template-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "common", "list", "filters"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "test", "index"));
            File.Copy(
                Path.Combine(RepoRoot(), "osafw-app", "App_Data", "template", "common", "list", "filters", "cell.html"),
                Path.Combine(tempRoot, "common", "list", "filters", "cell.html"));
            File.WriteAllText(Path.Combine(tempRoot, "test", "index", "driver.html"), "<~/common/list/filters/cell>");
            File.WriteAllText(Path.Combine(tempRoot, "test", "index", "list_filter_custom.html"), "<span data-custom-filter=\"<~filter[field]>\"><~filter[selected_options] repeat inline><~iname></~filter[selected_options]></span>");

            var fw = TestHelpers.CreateFw(new Dictionary<string, string?> { ["appSettings:template"] = tempRoot });
            var fields = ExplicitFilterFields();
            ((FwDict)fields["inline_status"]!)["template"] = "custom";
            var controller = BuildController(fw, BuildConfig(fields: fields));
            controller.BuildHeaders(new FwDict
            {
                ["inline_status"] = Utils.jsonEncode(new FwDict { ["type"] = "multi_select", ["values"] = new ObjList { "A" } })
            });

            var html = fw.parsePage("/test/index", "driver.html", controller.HeaderFor("inline_status"));

            StringAssert.Contains(html, "data-custom-filter=\"inline_status\"");
            StringAssert.Contains(html, "Active");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [TestMethod]
    public void VueControllerHeadersExposeFilterMetadata()
    {
        var fw = TestHelpers.CreateFw();
        TestHelpers.RegisterModel(fw, new LookupOptionsModel());
        var controller = new TestVueController();
        controller.InitForTest(fw, BuildConfig(fields: ExplicitFilterFields()));

        controller.BuildHeaders();

        var header = controller.HeaderFor("dict_link_auto_iname");
        var filter = FilterFor(header);
        Assert.AreEqual("autocomplete", filter["type"]);
        Assert.AreEqual("/Admin/DynamicColumnFilterTests/(Autocomplete)?model=LookupOptionsModel&q=", filter["autocomplete_url"]);
        Assert.IsFalse(header.ContainsKey("filter_type"));
        Assert.IsFalse(header.ContainsKey("filter_options"));

        var multiFilter = FilterFor(controller.HeaderFor("inline_status"));
        Assert.IsInstanceOfType(multiFilter["options"], typeof(FwList));
    }
}
