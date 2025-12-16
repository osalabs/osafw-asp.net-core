using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Reflection;

namespace osafw.Tests
{
    [TestClass]
    public class FwLogTypesTests
    {
        [TestMethod]
        public void Constructor_SetsTableName()
        {
            var model = new FwLogTypes();

            Assert.AreEqual("log_types", model.table_name);
        }

        [TestMethod]
        public void Constants_AreStable()
        {
            var expected = new Dictionary<string, object>
            {
                { nameof(FwLogTypes.ITYPE_SYSTEM), 0 },
                { nameof(FwLogTypes.ITYPE_USER), 10 },
                { nameof(FwLogTypes.ICODE_ADDED), "added" },
                { nameof(FwLogTypes.ICODE_UPDATED), "updated" },
                { nameof(FwLogTypes.ICODE_DELETED), "deleted" },
                { nameof(FwLogTypes.ICODE_COMMENT), "comment" },
                { nameof(FwLogTypes.ICODE_USERS_SIMULATE), "simulate" },
                { nameof(FwLogTypes.ICODE_USERS_LOGIN), "login" },
                { nameof(FwLogTypes.ICODE_USERS_LOGIN_FAIL), "login_fail" },
                { nameof(FwLogTypes.ICODE_USERS_LOGOFF), "logoff" },
                { nameof(FwLogTypes.ICODE_USERS_CHPWD), "chpwd" },
            };

            foreach (var kvp in expected)
            {
                var field = typeof(FwLogTypes).GetField(kvp.Key, BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(field, $"Field {kvp.Key} should exist");
                Assert.AreEqual(kvp.Value, field!.GetRawConstantValue(), $"Field {kvp.Key} should keep its value");
            }
        }
    }
}
