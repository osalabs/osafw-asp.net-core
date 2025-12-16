using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace osafw.Tests
{
    [TestClass()]
    public class FromUtilsTests
    {
        [TestMethod()]
        public void IsEmailTest()
        {
            // Test FromUtils.isEmail function
            Assert.IsTrue(FormUtils.isEmail("test@test.com"));
            Assert.IsTrue(FormUtils.isEmail("test@test.asad.com"));
            Assert.IsTrue(FormUtils.isEmail("test.test@test.asad.com"));
            Assert.IsFalse(FormUtils.isEmail("testtest.com"));
            Assert.IsFalse(FormUtils.isEmail("testtest"));
            Assert.IsFalse(FormUtils.isEmail("!!!"));
        }

        [TestMethod()]
        public void IsPhoneTest()
        {
            // (xxx) xxx-xxxx
            // xxx xxx xx xx
            // xxx-xxx-xx-xx
            // xxxxxxxxxx

            // Test FormUtils.isPhone function
            Assert.IsTrue(FormUtils.isPhone("123-456-7890"));
            Assert.IsTrue(FormUtils.isPhone("123-456-78-90"));
            Assert.IsTrue(FormUtils.isPhone("1234567890"));
            Assert.IsTrue(FormUtils.isPhone("123 456 78 90"));
            Assert.IsTrue(FormUtils.isPhone("123 456 7890"));
            Assert.IsFalse(FormUtils.isPhone("123.456.7890"));
            Assert.IsTrue(FormUtils.isPhone("(123) 456-7890"));
            Assert.IsFalse(FormUtils.isPhone("123-456-7890 ext123"));

        }

        [TestMethod]
        public void GetPagerTest()
        {
            FwList pager1 = FormUtils.getPager(100, 1);
            Assert.IsNotNull(pager1, "Pager should not be null when paging is required");
            Assert.HasCount(4, pager1, "Pager should have correct number of pages when paging is required");

            FwList pager2 = FormUtils.getPager(100, 1, 20);
            Assert.IsNotNull(pager2, "Pager should not be null when paging is required with custom page size");
            Assert.HasCount(5, pager2, "Pager should have correct number of pages when paging is required with custom page size");

            FwList pager3 = FormUtils.getPager(30, 1);
            Assert.IsNotNull(pager3, "Pager should not be null when count is more than default page size");
            Assert.HasCount(2, pager3, "Pager should not be null when count is more than default page size");

            FwList pager4 = FormUtils.getPager(10, 1);
            Assert.IsNotNull(pager4, "Pager should not be null even when no paging is required");
            Assert.AreEqual(0, pager4.Count, "Pager should be empty when no paging is required");
        }

        [TestMethod]
        public void FilterTest()
        {
            // Case 1: Filter fields from null item
            FwDict? item1 = null;
            FwDict result1 = FormUtils.filter(item1!, new string[] { "field1", "field2" });
            Assert.IsNotNull(result1, "Result should not be null when item is null");
            CollectionAssert.AreEquivalent(Array.Empty<string>(), result1.Keys.Cast<string>().ToArray(), "Result should be empty when item is null");

            // Case 2: Filter existing fields from item
            FwDict item2 = new() { { "field1", "value1" }, { "field2", "value2" } };
            FwDict result2 = FormUtils.filter(item2, new string[] { "field1", "field2" });
            Assert.IsNotNull(result2, "Result should not be null when filtering existing fields");
            CollectionAssert.AreEquivalent(new string[] { "field1", "field2" }, result2.Keys.Cast<string>().ToArray(), "Result should contain all existing fields");

            // Case 3: Filter non-existing fields from item
            FwDict item3 = new() { { "field1", "value1" }, { "field2", "value2" } };
            FwDict result3 = FormUtils.filter(item3, new string[] { "field1", "field3" });
            Assert.IsNotNull(result3, "Result should not be null when filtering non-existing fields");
            CollectionAssert.AreEquivalent(new string[] { "field1" }, result3.Keys.Cast<string>().ToArray(), "Result should contain only existing fields");

            // Case 4: Filter existing fields when is_exists is false
            FwDict item4 = new() { { "field1", "value1" }, { "field2", "value2" } };
            FwDict result4 = FormUtils.filter(item4, new string[] { "field1", "field2" }, false);
            Assert.IsNotNull(result4, "Result should not be null when filtering existing fields with is_exists false");
            CollectionAssert.AreEquivalent(new string[] { "field1", "field2" }, result4.Keys.Cast<string>().ToArray(), "Result should contain all fields when is_exists is false");

            // Case 5: Filter non-existing fields when is_exists is true
            FwDict item5 = new() { { "field1", "value1" }, { "field2", "value2" } };
            FwDict result5 = FormUtils.filter(item5, new string[] { "field1", "field3" }, true);
            Assert.IsNotNull(result5, "Result should not be null when filtering non-existing fields with is_exists true");
            CollectionAssert.AreEquivalent(new string[] { "field1" }, result5.Keys.Cast<string>().ToArray(), "Result should contain only existing fields when is_exists is true");
        }

        [TestMethod]
        public void FilterCheckboxesTest()
        {
            // Case 1: Populate itemdb with values from item when item is not null
            FwDict itemdb1 = [];
            FwDict item1 = new() { { "field1", "value1" }, { "field2", "value2" } };
            FormUtils.filterCheckboxes(itemdb1, item1, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb1["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("value2", itemdb1["field2"], "Itemdb should contain value from item for existing field");

            // Case 2: Populate itemdb with default values for non-existing fields when item is not null
            FwDict itemdb2 = [];
            FwDict item2 = new() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb2, item2, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb2["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("0", itemdb2["field2"], "Itemdb should contain default value for non-existing field");

            // Case 3: Populate itemdb with default values for all fields when item is null
            FwDict itemdb3 = new() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb3, null!, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb3["field1"], "Itemdb should be same when item is null");
            Assert.HasCount(1, itemdb3, "Itemdb should be same when item is null");

            // Case 4: Populate itemdb with custom default value for non-existing fields when item is not null
            FwDict itemdb4 = [];
            FwDict item4 = new() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb4, item4, new string[] { "field1", "field2" }, false, "custom_default");
            Assert.AreEqual("value1", itemdb4["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("custom_default", itemdb4["field2"], "Itemdb should contain custom default value for non-existing field");

            // Case 5: Populate itemdb with default values when fields array is null
            FwDict itemdb5 = [];
            FwDict item5 = new() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb5, item5, (IList)null!);
            Assert.IsEmpty(itemdb5, "Itemdb should be empty when fields array is null");

            // Case 6: Populate itemdb with default values when fields array is empty
            FwDict itemdb6 = [];
            FwDict item6 = new() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb6, item6, Array.Empty<string>());
            Assert.IsEmpty(itemdb6, "Itemdb should be empty when fields array is empty");

            // Case 7: Populate itemdb with default values when item is null and fields array is null
            FwDict itemdb7 = [];
            FormUtils.filterCheckboxes(itemdb7, null!, (IList)null!);
            Assert.IsEmpty(itemdb7, "Itemdb should be empty when item is null and fields array is null");
        }

        [TestMethod]
        public void filterCheckboxesOverloadTest()
        {
            // Case 1: Populate itemdb with values from item when item is not null
            FwDict itemdb1 = [];
            FwDict item1 = new() { { "field1", "value1" }, { "field2", "value2" } };
            FormUtils.filterCheckboxes(itemdb1, item1, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb1["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("value2", itemdb1["field2"], "Itemdb should contain value from item for existing field");

            // Case 2: Populate itemdb with default values for non-existing fields when item is not null
            FwDict itemdb2 = [];
            FwDict item2 = new() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb2, item2, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb2["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("0", itemdb2["field2"], "Itemdb should contain default value for non-existing field");

            // Case 3: Populate itemdb with default values for all fields when item is null
            FwDict itemdb3 = new() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb3, null!, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb3["field1"], "Itemdb should be same when item is null");
            Assert.HasCount(1, itemdb3, "Itemdb should be same when item is null");

            // Case 4: Populate itemdb with custom default value for non-existing fields when item is not null
            FwDict itemdb4 = [];
            FwDict item4 = new() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb4, item4, new string[] { "field1", "field2" }, false, "custom_default");
            Assert.AreEqual("value1", itemdb4["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("custom_default", itemdb4["field2"], "Itemdb should contain custom default value for non-existing field");

            // Case 5: Populate itemdb with default values when fields array is null
            FwDict itemdb5 = [];
            FwDict item5 = new() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb5, item5, (IList)null!);
            Assert.IsEmpty(itemdb5, "Itemdb should be empty when fields array is null");

            // Case 6: Populate itemdb with default values when fields array is empty
            FwDict itemdb6 = [];
            FwDict item6 = new() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb6, item6, Array.Empty<string>());
            Assert.IsEmpty(itemdb6, "Itemdb should be empty when fields array is empty");

            // Case 7: Populate itemdb with default values when item is null and fields array is null
            FwDict itemdb7 = [];
            FormUtils.filterCheckboxes(itemdb7, null!, (IList)null!);
            Assert.IsEmpty(itemdb7, "Itemdb should be empty when item is null and fields array is null");
        }

        [TestMethod]
        public void FilterNullableTest()
        {
            // Case 1: Check if value is empty '' and make it null for existing field
            FwDict itemdb1 = new() { { "field1", "" }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb1, "field1");
            Assert.IsNull(itemdb1["field1"], "Value should be null for field with empty string");

            // Case 2: Do not change value for existing field with non-empty value
            FwDict itemdb2 = new() { { "field1", "value1" }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb2, "field1");
            Assert.AreEqual("value1", itemdb2["field1"], "Value should remain unchanged for field with non-empty value");

            // Case 3: Do not change value for field not in itemdb
            FwDict itemdb3 = new() { { "field2", "value2" } };
            FormUtils.filterNullable(itemdb3, "field1");
            Assert.IsFalse(itemdb3.ContainsKey("field1"), "Field should not be added if not in itemdb");

            // Case 4: Do not change value for field with null value
            FwDict itemdb4 = new() { { "field1", null }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb4, "field1");
            Assert.IsNull(itemdb4["field1"], "Value should remain null for field with null value");

            // Case 5: Do not change value for field with non-string value
            FwDict itemdb5 = new() { { "field1", "123" }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb5, "field1");
            Assert.AreEqual("123", itemdb5["field1"], "Value should remain unchanged for field with non-string value");

            // Case 6: Do not change value for empty field names string
            FwDict itemdb6 = new() { { "field1", "" }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb6, "");
            Assert.AreEqual("", itemdb6["field1"], "Value should remain unchanged for empty field names string");

            // Case 7: Do not change value for null field names string
            FwDict itemdb7 = new() { { "field1", "" }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb7, (string)null!);
            Assert.AreEqual("", itemdb7["field1"], "Value should remain unchanged for null field names string");
        }

        [TestMethod]
        public void Multi2IdsTest()
        {
            // Case 1: Convert multiple values to comma-separated string
            FwDict items1 = new() { { "id1", "value1" }, { "id2", "value2" }, { "id3", "value3" } };
            string result1 = FormUtils.multi2ids(items1);
            Assert.AreEqual("id1,id2,id3", result1, "Result should be comma-separated string of keys");

            // Case 2: Return empty string for null input
            FwDict? items2 = null;
            string result2 = FormUtils.multi2ids(items2 ?? []);
            Assert.AreEqual("", result2, "Result should be empty string for null input");

            // Case 3: Return empty string for empty input
            FwDict items3 = [];
            string result3 = FormUtils.multi2ids(items3);
            Assert.AreEqual("", result3, "Result should be empty string for empty input");

            // Case 4: Return comma-separated string with single value
            FwDict items4 = new() { { "id1", "value1" } };
            string result4 = FormUtils.multi2ids(items4);
            Assert.AreEqual("id1", result4, "Result should be single key for single value");

            // Case 5: Ensure consistent order in the result
            FwDict items5 = new() { { "id3", "value3" }, { "id1", "value1" }, { "id2", "value2" } };
            string result5 = FormUtils.multi2ids(items5);
            Assert.AreEqual("id1,id2,id3", result5, "Result should be sorted to keep order consistent");
        }

        [TestMethod]
        public void Ids2MultiTest()
        {
            // Case 1: Convert comma-separated string to hashtable with keys
            string str1 = "id1,id2,id3";
            FwDict result1 = FormUtils.ids2multi(str1);
            Assert.HasCount(3, result1, "Result should contain three keys");
            Assert.IsTrue(result1.ContainsKey("id1"), "Result should contain key 'id1'");
            Assert.IsTrue(result1.ContainsKey("id2"), "Result should contain key 'id2'");
            Assert.IsTrue(result1.ContainsKey("id3"), "Result should contain key 'id3'");

            // Case 2: For null input - empty hashtable
            string? str2 = null;
            FwDict result2 = FormUtils.ids2multi(str2!);
            Assert.IsEmpty(result2, "Result should not be null for null input");

            // Case 3: Return empty hashtable for empty input
            string str3 = "";
            FwDict result3 = FormUtils.ids2multi(str3);
            Assert.IsNotNull(result3, "Result should not be null for empty input");
            Assert.IsEmpty(result3, "Result should be empty hashtable for empty input");

            // Case 4: Convert single id string to hashtable with single key
            string str4 = "id1";
            FwDict result4 = FormUtils.ids2multi(str4);
            Assert.HasCount(1, result4, "Result should contain one key");
            Assert.IsTrue(result4.ContainsKey("id1"), "Result should contain key 'id1'");

            // Case 5: Convert comma-separated string with duplicate ids to hashtable with unique keys
            string str5 = "id1,id2,id1,id3,id2";
            FwDict result5 = FormUtils.ids2multi(str5);
            Assert.HasCount(3, result5, "Result should contain three unique keys");
            Assert.IsTrue(result5.ContainsKey("id1"), "Result should contain key 'id1'");
            Assert.IsTrue(result5.ContainsKey("id2"), "Result should contain key 'id2'");
            Assert.IsTrue(result5.ContainsKey("id3"), "Result should contain key 'id3'");
        }

        [TestMethod]
        public void Col2CommaStrTest()
        {
            // Case 1: Convert StrList to comma-separated string
            StrList col1 = ["value1", "value2", "value3"];
            string result1 = FormUtils.col2comma_str(col1);
            Assert.AreEqual("value1,value2,value3", result1, "Result should be comma-separated string of values");

            // Case 2: Throw error for null input
            StrList? col2 = null;
            Assert.AreEqual(string.Empty, FormUtils.col2comma_str(col2!), "Null input should return empty string");

            // Case 3: Return empty string for empty input
            StrList col3 = [];
            string result3 = FormUtils.col2comma_str(col3);
            Assert.AreEqual("", result3, "Result should be empty string for empty input");

            // Case 4: Convert FwList with single value to comma-separated string
            StrList col4 = ["value1"];
            string result4 = FormUtils.col2comma_str(col4);
            Assert.AreEqual("value1", result4, "Result should be single value for single-value input");

            // Case 5: Convert FwList with numeric values to comma-separated string
            IntList col5 = [1, 2, 3];
            string result5 = FormUtils.col2comma_str(col5);
            Assert.AreEqual("1,2,3", result5, "Result should be comma-separated string of numeric values");
        }

        [TestMethod]
        public void Comma_str2colTest()
        {
            // Case 1: Test with empty input
            string input1 = "";
            StrList result1 = FormUtils.comma_str2col(input1);
            Assert.IsEmpty(result1, "Result should be empty for empty input");

            // Case 2: Test with input containing single item
            string input2 = "item";
            StrList result2 = FormUtils.comma_str2col(input2);
            CollectionAssert.AreEqual(new StrList { "item" }, result2, "Result should contain single item for input with single item");

            // Case 3: Test with input containing multiple items
            string input3 = "item1,item2,item3";
            StrList result3 = FormUtils.comma_str2col(input3);
            CollectionAssert.AreEqual(new StrList { "item1", "item2", "item3" }, result3, "Result should contain multiple items for input with multiple items");

            // Case 4: Test with input containing spaces around commas
            string input4 = "item1,  item2 , item3";
            StrList result4 = FormUtils.comma_str2col(input4);
            CollectionAssert.AreEqual(new StrList { "item1", "item2", "item3" }, result4, "Result should contain items without leading or trailing spaces");

            // Case 5: Test with input containing only spaces
            string input5 = "   ";
            StrList result5 = FormUtils.comma_str2col(input5);
            Assert.IsEmpty(result5, "Result should be empty for input containing only spaces");
        }

        [TestMethod]
        public void dateForComboTest()
        {
            // Case 1: Test with valid date components
            FwDict item1 = new()
            {
                { "fdate_combo_day", "17" },
                { "fdate_combo_mon", "1" },
                { "fdate_combo_year", "2023" }
            };
            string result1 = FormUtils.dateForCombo(item1, "fdate_combo");
            Assert.AreEqual(new DateTime(2023, 1, 17).ToString("yyyy-MM-dd"), result1, "Result should be correct SQL date for valid date components");

            // Case 2: Test with missing day component
            FwDict item2 = new()
            {
                { "fdate_combo_day", "" },
                { "fdate_combo_mon", "1" },
                { "fdate_combo_year", "2023" }
            };
            string result2 = FormUtils.dateForCombo(item2, "fdate_combo");
            Assert.IsEmpty(result2, "Result should be empty string for missing day component");

            // Case 3: Test with missing month component
            FwDict item3 = new()
            {
                { "fdate_combo_day", "17" },
                { "fdate_combo_mon", "" },
                { "fdate_combo_year", "2023" }
            };
            string result3 = FormUtils.dateForCombo(item3, "fdate_combo");
            Assert.IsEmpty(result3, "Result should be empty string for missing month component");

            // Case 4: Test with missing year component
            FwDict item4 = new()
            {
                { "fdate_combo_day", "17" },
                { "fdate_combo_mon", "1" },
                { "fdate_combo_year", "" }
            };
            string result4 = FormUtils.dateForCombo(item4, "fdate_combo");
            Assert.IsEmpty(result4, "Result should be empty string for missing year component");

            // Case 5: Test with invalid date components
            FwDict item5 = new()
            {
                { "fdate_combo_day", "32" }, // Day component out of range
                { "fdate_combo_mon", "13" }, // Month component out of range
                { "fdate_combo_year", "-2023" } // Negative year component
            };
            string result5 = FormUtils.dateForCombo(item5, "fdate_combo");
            Assert.IsEmpty(result5, "Result should be empty string for invalid date components");

            // Case 6: Test with incorrect parameter names
            FwDict item6 = new()
            {
                { "day", "17" },
                { "month", "1" },
                { "year", "2023" }
            };
            string result6 = FormUtils.dateForCombo(item6, "fdate_combo");
            Assert.IsEmpty(result6, "Result should be empty string for incorrect parameter names");

            // Case 7: Test with null item
            FwDict? item7 = null;
            string result7 = FormUtils.dateForCombo(item7!, "fdate_combo");
            Assert.IsEmpty(result7, "Result should be empty string for null item");

            // Case 8: Test with null field_prefix
            string result8 = FormUtils.dateForCombo(item1, null!);
            Assert.IsEmpty(result8, "Result should be empty string for null field_prefix");
        }

        [TestMethod]
        public void LookupListsReturnExpectedValues()
        {
            CollectionAssert.AreEqual(new[] { "No|No", "Yes|Yes" }, FormUtils.getYesNo());
            CollectionAssert.AreEqual(new[] { "N|No", "Y|Yes" }, FormUtils.getYN());
            Assert.IsTrue(FormUtils.getStates().Length > 10);
        }

        [TestMethod]
        public void RadioOptionsBuildsInputsWithSelection()
        {
            var html = FormUtils.radioOptions("color", Utils.qw("red|Red green|Green"), "green", "<br>");

            StringAssert.Contains(html, "name=\"color\"");
            StringAssert.Contains(html, "id=\"color1\"");
            StringAssert.Contains(html, "checked");
            StringAssert.Contains(html, "Green</label>");
        }

        [TestMethod]
        public void SelectOptionsSupportsMultiSelectAndClass()
        {
            FwList options = [
                new FwDict { { "id", "1" }, { "iname", "One" }, { "class", "primary" } },
                new FwDict { { "id", "2" }, { "iname", "Two" } }
            ];

            var html = FormUtils.selectOptions(options, "1,2", true);

            StringAssert.Contains(html, "value=\"1\"");
            StringAssert.Contains(html, "class=\"primary\"");
            StringAssert.Contains(html, "selected");
        }

        [TestMethod]
        public void SelectTemplatesResolveNamesAndOptions()
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "formutils-tpl", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            var tplPath = Path.Combine(tmpDir, "status.sel");
            File.WriteAllText(tplPath, "a|Active\nb|`Inactive`");

            var previousTemplate = FwConfig.settings.TryGetValue("template", out object? value) ? value : null;
            FwConfig.settings["template"] = tmpDir;

            try
            {
                var name = FormUtils.selectTplName("/status.sel", "b");
                var options = FormUtils.selectTplOptions("/status.sel");

                Assert.AreEqual("Inactive", name);
                Assert.AreEqual(2, options.Count);
                Assert.AreEqual("a", (options[0] as FwDict)?["id"]);
            }
            finally
            {
                if (previousTemplate != null)
                    FwConfig.settings["template"] = previousTemplate;
                else
                    FwConfig.settings.Remove("template");

                if (Directory.Exists(tmpDir))
                    Directory.Delete(tmpDir, true);
            }
        }

        [TestMethod]
        public void CleanInput_RemovesUnsupportedCharacters()
        {
            var input = @"abc!@#$%^&*()+=[]{}|;':"",<>?";
            var cleaned = FormUtils.cleanInput(input);

            Assert.IsFalse(cleaned.Contains("!"));
            StringAssert.Contains(cleaned, "abc");
        }

        [TestMethod]
        public void ComboForDateSplitsAndRecombines()
        {
            FwDict item = [];
            var success = FormUtils.comboForDate("2024-02-05", item, "dt");

            Assert.IsTrue(success);
            Assert.AreEqual(5, item["dt_day"]);
            Assert.AreEqual(2, item["dt_mon"]);
            Assert.AreEqual(2024, item["dt_year"]);
        }

        [TestMethod]
        public void TimeConversionsRoundTrip()
        {
            Assert.AreEqual("01:05", FormUtils.intToTimeStr(3900));
            Assert.AreEqual(3660, FormUtils.timeStrToInt("01:01"));

            FwDict item = new() { { "occurred", new DateTime(2024, 1, 1, 3, 15, 10) } };
            Assert.IsTrue(FormUtils.timeToForm(item, "occurred"));
            Assert.AreEqual(3, item["occurred_hh"]);
            Assert.AreEqual(15, item["occurred_mm"]);

            item["occurred_hh"] = 6;
            item["occurred_mm"] = 30;
            item["occurred_ss"] = 0;
            Assert.IsTrue(FormUtils.formToTime(item, "occurred"));
            Assert.AreEqual(6, ((DateTime)item["occurred"]).Hour);
            Assert.IsFalse(FormUtils.formToTime(new FwDict() { { "broken_hh", "99" }, { "broken_mm", "99" }, { "broken_ss", "99" } }, "broken"));
        }

        [TestMethod]
        public void FormTimeAndDateHelpersNormalizeStrings()
        {
            Assert.AreEqual("", FormUtils.dateToFormTime(""));
            Assert.AreEqual("13:45", FormUtils.dateToFormTime("2024-01-01 13:45:59"));

            var combined = FormUtils.formTimeToDate("2024-01-01", "01:30");
            Assert.AreEqual(new DateTime(2024, 1, 1, 1, 30, 0), combined);
        }

        [TestMethod]
        public void AutocompleteParsingExtractsLeadingId()
        {
            Assert.AreEqual(123, FormUtils.getIdFromAutocomplete("123 - Test Value"));
            Assert.AreEqual(0, FormUtils.getIdFromAutocomplete("invalid"));
        }

        [TestMethod]
        public void ListOrderingHelpersRespectFlagsAndPrio()
        {
            FwList rows = [
                new FwDict { { "is_checked", true }, { "prio", 2 }, { "iname", "B" } },
                new FwDict { { "is_checked", false }, { "prio", 1 }, { "iname", "A" } }
            ];

            var checkedOnly = FormUtils.listCheckedOrderByPrioIname(rows);
            Assert.AreEqual(1, checkedOnly.Count);
            Assert.AreEqual("B", (checkedOnly[0] as FwDict)?["iname"]);

            var ordered = FormUtils.listOrderByPrioIname(rows);
            Assert.AreEqual("B", (ordered[0] as FwDict)?["iname"]);
            Assert.AreEqual("A", (ordered[1] as FwDict)?["iname"]);
        }

        [TestMethod]
        public void ChangeDetectionTracksDifferences()
        {
            var oldItem = new FwDict { { "name", "before" }, { "date", new DateTime(2024, 1, 1) } };
            var newItem = new FwDict { { "name", "after" }, { "date", new DateTime(2024, 1, 2) }, { "new", "value" } };

            var changes = FormUtils.changesOnly(newItem, oldItem);
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual("after", changes["name"]);
            Assert.AreEqual(new DateTime(2024, 1, 2), changes["date"]);

            Assert.IsTrue(FormUtils.isChanged(newItem, oldItem, "name"));
            Assert.IsFalse(FormUtils.isChanged(newItem, oldItem, "missing"));

            Assert.IsTrue(FormUtils.isChangedDate("2024-01-01", "2024-01-02"));
            Assert.IsFalse(FormUtils.isChangedDate("2024-01-01", "2024-01-01"));
        }

        [TestMethod]
        public void SqlOrderByQuotesAndInverts()
        {
            var db = new DB("conn", DB.DBTYPE_SQLSRV);
            var sortmap = new FwDict { { "name", "nm" }, { "created", "created desc" } };

            var asc = FormUtils.sqlOrderBy(db, "name", "asc", sortmap);
            var desc = FormUtils.sqlOrderBy(db, "created", "desc", sortmap);

            StringAssert.StartsWith(asc.Trim(), "[nm]");
            StringAssert.EndsWith(desc.Trim(), "asc");
            StringAssert.Contains(desc, "[created]");
        }
    }
}
