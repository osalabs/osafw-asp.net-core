using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
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
            ArrayList pager1 = FormUtils.getPager(100, 1);
            Assert.IsNotNull(pager1, "Pager should not be null when paging is required");
            Assert.AreEqual(4, pager1.Count, "Pager should have correct number of pages when paging is required");

            ArrayList pager2 = FormUtils.getPager(100, 1, 20);
            Assert.IsNotNull(pager2, "Pager should not be null when paging is required with custom page size");
            Assert.AreEqual(5, pager2.Count, "Pager should have correct number of pages when paging is required with custom page size");

            ArrayList pager3 = FormUtils.getPager(30, 1);
            Assert.IsNotNull(pager3, "Pager should not be null when count is more than default page size");
            Assert.AreEqual(2, pager3.Count, "Pager should not be null when count is more than default page size");

            ArrayList pager4 = FormUtils.getPager(10, 1);
            Assert.IsNull(pager4, "Pager should be null when no paging is required");
        }

        [TestMethod]
        public void FilterTest()
        {
            // Case 1: Filter fields from null item
            Hashtable item1 = null;
            Hashtable result1 = FormUtils.filter(item1, new string[] { "field1", "field2" });
            Assert.IsNotNull(result1, "Result should not be null when item is null");
            CollectionAssert.AreEquivalent(new string[] { }, result1.Keys.Cast<string>().ToArray(), "Result should be empty when item is null");

            // Case 2: Filter existing fields from item
            Hashtable item2 = new Hashtable { { "field1", "value1" }, { "field2", "value2" } };
            Hashtable result2 = FormUtils.filter(item2, new string[] { "field1", "field2" });
            Assert.IsNotNull(result2, "Result should not be null when filtering existing fields");
            CollectionAssert.AreEquivalent(new string[] { "field1", "field2" }, result2.Keys.Cast<string>().ToArray(), "Result should contain all existing fields");

            // Case 3: Filter non-existing fields from item
            Hashtable item3 = new Hashtable { { "field1", "value1" }, { "field2", "value2" } };
            Hashtable result3 = FormUtils.filter(item3, new string[] { "field1", "field3" });
            Assert.IsNotNull(result3, "Result should not be null when filtering non-existing fields");
            CollectionAssert.AreEquivalent(new string[] { "field1" }, result3.Keys.Cast<string>().ToArray(), "Result should contain only existing fields");

            // Case 4: Filter existing fields when is_exists is false
            Hashtable item4 = new Hashtable { { "field1", "value1" }, { "field2", "value2" } };
            Hashtable result4 = FormUtils.filter(item4, new string[] { "field1", "field2" }, false);
            Assert.IsNotNull(result4, "Result should not be null when filtering existing fields with is_exists false");
            CollectionAssert.AreEquivalent(new string[] { "field1", "field2" }, result4.Keys.Cast<string>().ToArray(), "Result should contain all fields when is_exists is false");

            // Case 5: Filter non-existing fields when is_exists is true
            Hashtable item5 = new Hashtable { { "field1", "value1" }, { "field2", "value2" } };
            Hashtable result5 = FormUtils.filter(item5, new string[] { "field1", "field3" }, true);
            Assert.IsNotNull(result5, "Result should not be null when filtering non-existing fields with is_exists true");
            CollectionAssert.AreEquivalent(new string[] { "field1" }, result5.Keys.Cast<string>().ToArray(), "Result should contain only existing fields when is_exists is true");
        }

        [TestMethod]
        public void FilterCheckboxesTest()
        {
            // Case 1: Populate itemdb with values from item when item is not null
            Hashtable itemdb1 = new Hashtable();
            Hashtable item1 = new Hashtable { { "field1", "value1" }, { "field2", "value2" } };
            FormUtils.filterCheckboxes(itemdb1, item1, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb1["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("value2", itemdb1["field2"], "Itemdb should contain value from item for existing field");

            // Case 2: Populate itemdb with default values for non-existing fields when item is not null
            Hashtable itemdb2 = new Hashtable();
            Hashtable item2 = new Hashtable { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb2, item2, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb2["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("0", itemdb2["field2"], "Itemdb should contain default value for non-existing field");

            // Case 3: Populate itemdb with default values for all fields when item is null
            Hashtable itemdb3 = new Hashtable() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb3, null, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb3["field1"], "Itemdb should be same when item is null");
            Assert.AreEqual(1, itemdb3.Count, "Itemdb should be same when item is null");

            // Case 4: Populate itemdb with custom default value for non-existing fields when item is not null
            Hashtable itemdb4 = new Hashtable();
            Hashtable item4 = new Hashtable { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb4, item4, new string[] { "field1", "field2" }, false, "custom_default");
            Assert.AreEqual("value1", itemdb4["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("custom_default", itemdb4["field2"], "Itemdb should contain custom default value for non-existing field");

            // Case 5: Populate itemdb with default values when fields array is null
            Hashtable itemdb5 = new Hashtable();
            Hashtable item5 = new Hashtable { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb5, item5, (IList)null);
            Assert.AreEqual(0, itemdb5.Count, "Itemdb should be empty when fields array is null");

            // Case 6: Populate itemdb with default values when fields array is empty
            Hashtable itemdb6 = new Hashtable();
            Hashtable item6 = new Hashtable { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb6, item6, new string[] { });
            Assert.AreEqual(0, itemdb6.Count, "Itemdb should be empty when fields array is empty");

            // Case 7: Populate itemdb with default values when item is null and fields array is null
            Hashtable itemdb7 = new Hashtable();
            FormUtils.filterCheckboxes(itemdb7, null, (IList)null);
            Assert.AreEqual(0, itemdb7.Count, "Itemdb should be empty when item is null and fields array is null");
        }

        [TestMethod]
        public void filterCheckboxesOverloadTest()
        {
            // Case 1: Populate itemdb with values from item when item is not null
            Hashtable itemdb1 = new Hashtable();
            Hashtable item1 = new Hashtable { { "field1", "value1" }, { "field2", "value2" } };
            FormUtils.filterCheckboxes(itemdb1, item1, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb1["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("value2", itemdb1["field2"], "Itemdb should contain value from item for existing field");

            // Case 2: Populate itemdb with default values for non-existing fields when item is not null
            Hashtable itemdb2 = new Hashtable();
            Hashtable item2 = new Hashtable { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb2, item2, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb2["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("0", itemdb2["field2"], "Itemdb should contain default value for non-existing field");

            // Case 3: Populate itemdb with default values for all fields when item is null
            Hashtable itemdb3 = new Hashtable() { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb3, null, new string[] { "field1", "field2" });
            Assert.AreEqual("value1", itemdb3["field1"], "Itemdb should be same when item is null");
            Assert.AreEqual(1, itemdb3.Count, "Itemdb should be same when item is null");

            // Case 4: Populate itemdb with custom default value for non-existing fields when item is not null
            Hashtable itemdb4 = new Hashtable();
            Hashtable item4 = new Hashtable { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb4, item4, new string[] { "field1", "field2" }, false, "custom_default");
            Assert.AreEqual("value1", itemdb4["field1"], "Itemdb should contain value from item for existing field");
            Assert.AreEqual("custom_default", itemdb4["field2"], "Itemdb should contain custom default value for non-existing field");

            // Case 5: Populate itemdb with default values when fields array is null
            Hashtable itemdb5 = new Hashtable();
            Hashtable item5 = new Hashtable { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb5, item5, (IList)null);
            Assert.AreEqual(0, itemdb5.Count, "Itemdb should be empty when fields array is null");

            // Case 6: Populate itemdb with default values when fields array is empty
            Hashtable itemdb6 = new Hashtable();
            Hashtable item6 = new Hashtable { { "field1", "value1" } };
            FormUtils.filterCheckboxes(itemdb6, item6, new string[] { });
            Assert.AreEqual(0, itemdb6.Count, "Itemdb should be empty when fields array is empty");

            // Case 7: Populate itemdb with default values when item is null and fields array is null
            Hashtable itemdb7 = new Hashtable();
            FormUtils.filterCheckboxes(itemdb7, null, (IList)null);
            Assert.AreEqual(0, itemdb7.Count, "Itemdb should be empty when item is null and fields array is null");
        }

        [TestMethod]
        public void FilterNullableTest()
        {
            // Case 1: Check if value is empty '' and make it null for existing field
            Hashtable itemdb1 = new Hashtable { { "field1", "" }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb1, "field1");
            Assert.IsNull(itemdb1["field1"], "Value should be null for field with empty string");

            // Case 2: Do not change value for existing field with non-empty value
            Hashtable itemdb2 = new Hashtable { { "field1", "value1" }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb2, "field1");
            Assert.AreEqual("value1", itemdb2["field1"], "Value should remain unchanged for field with non-empty value");

            // Case 3: Do not change value for field not in itemdb
            Hashtable itemdb3 = new Hashtable { { "field2", "value2" } };
            FormUtils.filterNullable(itemdb3, "field1");
            Assert.IsFalse(itemdb3.ContainsKey("field1"), "Field should not be added if not in itemdb");

            // Case 4: Do not change value for field with null value
            Hashtable itemdb4 = new Hashtable { { "field1", null }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb4, "field1");
            Assert.IsNull(itemdb4["field1"], "Value should remain null for field with null value");

            // Case 5: Do not change value for field with non-string value
            Hashtable itemdb5 = new Hashtable { { "field1", "123" }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb5, "field1");
            Assert.AreEqual("123", itemdb5["field1"], "Value should remain unchanged for field with non-string value");

            // Case 6: Do not change value for empty field names string
            Hashtable itemdb6 = new Hashtable { { "field1", "" }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb6, "");
            Assert.AreEqual("", itemdb6["field1"], "Value should remain unchanged for empty field names string");

            // Case 7: Do not change value for null field names string
            Hashtable itemdb7 = new Hashtable { { "field1", "" }, { "field2", "value2" } };
            FormUtils.filterNullable(itemdb7, null);
            Assert.AreEqual("", itemdb7["field1"], "Value should remain unchanged for null field names string");
        }

        [TestMethod]
        public void Multi2IdsTest()
        {
            // Case 1: Convert multiple values to comma-separated string
            Hashtable items1 = new Hashtable { { "id1", "value1" }, { "id2", "value2" }, { "id3", "value3" } };
            string result1 = FormUtils.multi2ids(items1);
            Assert.AreEqual("id1,id2,id3", result1, "Result should be comma-separated string of keys");

            // Case 2: Return empty string for null input
            Hashtable items2 = null;
            string result2 = FormUtils.multi2ids(items2);
            Assert.AreEqual("", result2, "Result should be empty string for null input");

            // Case 3: Return empty string for empty input
            Hashtable items3 = new Hashtable();
            string result3 = FormUtils.multi2ids(items3);
            Assert.AreEqual("", result3, "Result should be empty string for empty input");

            // Case 4: Return comma-separated string with single value
            Hashtable items4 = new Hashtable { { "id1", "value1" } };
            string result4 = FormUtils.multi2ids(items4);
            Assert.AreEqual("id1", result4, "Result should be single key for single value");

            // Case 5: Ensure consistent order in the result
            Hashtable items5 = new Hashtable { { "id3", "value3" }, { "id1", "value1" }, { "id2", "value2" } };
            string result5 = FormUtils.multi2ids(items5);
            Assert.AreEqual("id1,id2,id3", result5, "Result should be sorted to keep order consistent");
        }

        [TestMethod]
        public void Ids2MultiTest()
        {
            // Case 1: Convert comma-separated string to hashtable with keys
            string str1 = "id1,id2,id3";
            Hashtable result1 = FormUtils.ids2multi(str1);
            Assert.AreEqual(3, result1.Count, "Result should contain three keys");
            Assert.IsTrue(result1.ContainsKey("id1"), "Result should contain key 'id1'");
            Assert.IsTrue(result1.ContainsKey("id2"), "Result should contain key 'id2'");
            Assert.IsTrue(result1.ContainsKey("id3"), "Result should contain key 'id3'");

            // Case 2: Throw error for null input
            string str2 = null;
            Assert.ThrowsException<NullReferenceException>(() => FormUtils.ids2multi(str2), "Error for null input");

            // Case 3: Return empty hashtable for empty input
            string str3 = "";
            Hashtable result3 = FormUtils.ids2multi(str3);
            Assert.IsNotNull(result3, "Result should not be null for empty input");
            Assert.AreEqual(0, result3.Count, "Result should be empty hashtable for empty input");

            // Case 4: Convert single id string to hashtable with single key
            string str4 = "id1";
            Hashtable result4 = FormUtils.ids2multi(str4);
            Assert.AreEqual(1, result4.Count, "Result should contain one key");
            Assert.IsTrue(result4.ContainsKey("id1"), "Result should contain key 'id1'");

            // Case 5: Convert comma-separated string with duplicate ids to hashtable with unique keys
            string str5 = "id1,id2,id1,id3,id2";
            Hashtable result5 = FormUtils.ids2multi(str5);
            Assert.AreEqual(3, result5.Count, "Result should contain three unique keys");
            Assert.IsTrue(result5.ContainsKey("id1"), "Result should contain key 'id1'");
            Assert.IsTrue(result5.ContainsKey("id2"), "Result should contain key 'id2'");
            Assert.IsTrue(result5.ContainsKey("id3"), "Result should contain key 'id3'");
        }

        [TestMethod]
        public void Col2CommaStrTest()
        {
            // Case 1: Convert ArrayList to comma-separated string
            ArrayList col1 = new ArrayList { "value1", "value2", "value3" };
            string result1 = FormUtils.col2comma_str(col1);
            Assert.AreEqual("value1,value2,value3", result1, "Result should be comma-separated string of values");

            // Case 2: Throw error for null input
            ArrayList col2 = null;
            Assert.ThrowsException<NullReferenceException>(() => FormUtils.col2comma_str(col2), "Error for null input");

            // Case 3: Return empty string for empty input
            ArrayList col3 = new ArrayList();
            string result3 = FormUtils.col2comma_str(col3);
            Assert.AreEqual("", result3, "Result should be empty string for empty input");

            // Case 4: Convert ArrayList with single value to comma-separated string
            ArrayList col4 = new ArrayList { "value1" };
            string result4 = FormUtils.col2comma_str(col4);
            Assert.AreEqual("value1", result4, "Result should be single value for single-value input");

            // Case 5: Convert ArrayList with numeric values to comma-separated string
            ArrayList col5 = new ArrayList { 1, 2, 3 };
            string result5 = FormUtils.col2comma_str(col5);
            Assert.AreEqual("1,2,3", result5, "Result should be comma-separated string of numeric values");
        }

        [TestMethod]
        public void Comma_str2colTest()
        {
            //TODO
            //bug ?

            // Case 1: Test with empty input
            string input1 = "";
            ArrayList result1 = FormUtils.comma_str2col(input1);
            Assert.AreEqual(0, result1.Count, "Result should be empty for empty input");

            // Case 2: Test with input containing single item
            string input2 = "item";
            ArrayList result2 = FormUtils.comma_str2col(input2);
            CollectionAssert.AreEqual(new ArrayList { "item" }, result2, "Result should contain single item for input with single item");

            // Case 3: Test with input containing multiple items
            string input3 = "item1,item2,item3";
            ArrayList result3 = FormUtils.comma_str2col(input3);
            CollectionAssert.AreEqual(new ArrayList { "item1", "item2", "item3" }, result3, "Result should contain multiple items for input with multiple items");

            // Case 4: Test with input containing spaces around commas
            string input4 = "item1,  item2 , item3";
            ArrayList result4 = FormUtils.comma_str2col(input4);
            CollectionAssert.AreEqual(new ArrayList { "item1", "item2", "item3" }, result4, "Result should contain items without leading or trailing spaces");

            // Case 5: Test with input containing only spaces
            string input5 = "   ";
            ArrayList result5 = FormUtils.comma_str2col(input5);
            Assert.AreEqual(0, result5.Count, "Result should be empty for input containing only spaces");
        }

        [TestMethod]
        public void dateForComboTest()
        {
            // Case 1: Test with valid date components
            Hashtable item1 = new Hashtable
            {
                { "fdate_combo_day", "17" },
                { "fdate_combo_mon", "1" },
                { "fdate_combo_year", "2023" }
            };
            double? result1 = FormUtils.dateForCombo(item1, "fdate_combo") as double?;
            Assert.AreEqual(new DateTime(2023, 1, 17).ToOADate(), result1, "Result should be correct OADate for valid date components");

            // Case 2: Test with missing day component
            Hashtable item2 = new Hashtable
            {
                { "fdate_combo_day", "" },
                { "fdate_combo_mon", "1" },
                { "fdate_combo_year", "2023" }
            };
            object result2 = FormUtils.dateForCombo(item2, "fdate_combo");
            Assert.IsNull(result2, "Result should be null for missing day component");

            // Case 3: Test with missing month component
            Hashtable item3 = new Hashtable
            {
                { "fdate_combo_day", "17" },
                { "fdate_combo_mon", "" },
                { "fdate_combo_year", "2023" }
            };
            object result3 = FormUtils.dateForCombo(item3, "fdate_combo");
            Assert.IsNull(result3, "Result should be null for missing month component");

            // Case 4: Test with missing year component
            Hashtable item4 = new Hashtable
            {
                { "fdate_combo_day", "17" },
                { "fdate_combo_mon", "1" },
                { "fdate_combo_year", "" }
            };
            object result4 = FormUtils.dateForCombo(item4, "fdate_combo");
            Assert.IsNull(result4, "Result should be null for missing year component");

            // Case 5: Test with invalid date components
            Hashtable item5 = new Hashtable
            {
                { "fdate_combo_day", "32" }, // Day component out of range
                { "fdate_combo_mon", "13" }, // Month component out of range
                { "fdate_combo_year", "-2023" } // Negative year component
            };
            object result5 = FormUtils.dateForCombo(item5, "fdate_combo");
            Assert.IsNull(result5, "Result should be null for invalid date components");

            // Case 6: Test with incorrect parameter names
            Hashtable item6 = new Hashtable
            {
                { "day", "17" },
                { "month", "1" },
                { "year", "2023" }
            };
            object result6 = FormUtils.dateForCombo(item6, "fdate_combo");
            Assert.IsNull(result6, "Result should be null for incorrect parameter names");

            // Case 7: Test with null item
            Hashtable item7 = null;
            object result7 = FormUtils.dateForCombo(item7, "fdate_combo");
            Assert.IsNull(result7, "Result should be null for null item");

            // Case 8: Test with null field_prefix
            object result8 = FormUtils.dateForCombo(item1, null);
            Assert.IsNull(result8, "Result should be null for null field_prefix");
        }
    }
}