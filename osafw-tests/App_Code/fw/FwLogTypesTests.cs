using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            Assert.AreEqual(0, value(FwLogTypes.ITYPE_SYSTEM));
            Assert.AreEqual(10, value(FwLogTypes.ITYPE_USER));
            Assert.AreEqual("added", value(FwLogTypes.ICODE_ADDED));
            Assert.AreEqual("updated", value(FwLogTypes.ICODE_UPDATED));
            Assert.AreEqual("deleted", value(FwLogTypes.ICODE_DELETED));
            Assert.AreEqual("comment", value(FwLogTypes.ICODE_COMMENT));
            Assert.AreEqual("simulate", value(FwLogTypes.ICODE_USERS_SIMULATE));
            Assert.AreEqual("login", value(FwLogTypes.ICODE_USERS_LOGIN));
            Assert.AreEqual("login_fail", value(FwLogTypes.ICODE_USERS_LOGIN_FAIL));
            Assert.AreEqual("logoff", value(FwLogTypes.ICODE_USERS_LOGOFF));
            Assert.AreEqual("chpwd", value(FwLogTypes.ICODE_USERS_CHPWD));
        }

        private static T value<T>(T value) => value;
    }
}
