using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace osafw.Tests
{
    [TestClass]
    public class FwExtensionsTests
    {
        #region toBool

        [TestMethod]
        public void toBool_Null_ReturnsFalse()
        {
            object input = null;
            Assert.IsFalse(input.toBool());
        }

        [TestMethod]
        public void toBool_BoolTrue_ReturnsTrue()
        {
            object input = true;
            Assert.IsTrue(input.toBool());
        }

        [TestMethod]
        public void toBool_BoolFalse_ReturnsFalse()
        {
            object input = false;
            Assert.IsFalse(input.toBool());
        }

        [TestMethod]
        public void toBool_ICollection_Empty_ReturnsFalse()
        {
            // List implementing ICollection with count=0
            ICollection<int> input = new List<int>();
            Assert.IsFalse(input.toBool());
        }

        [TestMethod]
        public void toBool_ICollection_NonEmpty_ReturnsTrue()
        {
            ICollection<int> input = new List<int> { 1, 2 };
            Assert.IsTrue(input.toBool());
        }

        [TestMethod]
        public void toBool_ZeroAsNumber_ReturnsFalse()
        {
            object input = 0;
            Assert.IsFalse(input.toBool());
        }

        [TestMethod]
        public void toBool_NonZeroAsNumber_ReturnsTrue()
        {
            object input = 42;
            Assert.IsTrue(input.toBool());
        }

        [TestMethod]
        public void toBool_ZeroAsString_ReturnsFalse()
        {
            object input = "0";
            Assert.IsFalse(input.toBool());
        }

        [TestMethod]
        public void toBool_NonZeroAsString_ReturnsTrue()
        {
            object input = "123";
            Assert.IsTrue(input.toBool());
        }

        [TestMethod]
        public void toBool_TrueString_ReturnsTrue()
        {
            object input = "true";
            Assert.IsTrue(input.toBool());
        }

        [TestMethod]
        public void toBool_FalseString_ReturnsFalse()
        {
            object input = "false";
            Assert.IsFalse(input.toBool());
        }

        [TestMethod]
        public void toBool_InvalidString_ReturnsFalse()
        {
            object input = "not_a_bool";
            Assert.IsFalse(input.toBool());
        }

        [TestMethod]
        public void toBool_EmptyString_ReturnsFalse()
        {
            object input = "";
            Assert.IsFalse(input.toBool());
        }

        #endregion

        #region toDate / toDateOrNull

        [TestMethod]
        public void toDate_Null_ReturnsMinValue()
        {
            object input = null;
            Assert.AreEqual(DateTime.MinValue, input.toDate());
        }

        [TestMethod]
        public void toDate_AlreadyDateTime_ReturnsSameValue()
        {
            var now = DateTime.Now;
            object input = now;
            Assert.AreEqual(now, input.toDate());
        }

        [TestMethod]
        public void toDate_StringValidDefaultFormat_ReturnsParsed()
        {
            var expected = new DateTime(2025, 1, 2);
            // The exact result depends on your local culture/timezone if no style is specified
            object input = expected.Date.ToShortDateString();
            var result = input.toDate();
            Assert.AreEqual(new DateTime(2025, 1, 2), result.Date);
        }

        [TestMethod]
        public void toDate_StringValidExactFormat_ReturnsParsed()
        {
            object input = "2025-01-02";
            var result = input.toDate("yyyy-MM-dd");
            Assert.AreEqual(new DateTime(2025, 1, 2), result);
        }

        [TestMethod]
        public void toDate_StringInvalidNoFormat_ReturnsMinValue()
        {
            object input = "invalid_date";
            var result = input.toDate();
            Assert.AreEqual(DateTime.MinValue, result);
        }

        [TestMethod]
        public void toDateOrNull_Null_ReturnsNull()
        {
            object input = null;
            Assert.IsNull(input.toDateOrNull());
        }

        [TestMethod]
        public void toDateOrNull_AlreadyDateTime_ReturnsSameValue()
        {
            var now = DateTime.Now;
            object input = now;
            DateTime? result = input.toDateOrNull();
            Assert.IsNotNull(result);
            Assert.AreEqual(now, result.Value);
        }

        [TestMethod]
        public void toDateOrNull_StringValidDefault_ReturnsParsed()
        {
            var expected = new DateTime(2025, 1, 2);
            // The exact result depends on your local culture/timezone if no style is specified
            object input = expected.Date.ToShortDateString();
            var result = input.toDateOrNull();
            Assert.IsNotNull(result);
            Assert.AreEqual(new DateTime(2025, 1, 2), result.Value);
        }

        [TestMethod]
        public void toDateOrNull_StringValidExact_ReturnsParsed()
        {
            object input = "2025-01-02";
            var result = input.toDateOrNull("yyyy-MM-dd");
            Assert.IsNotNull(result);
            Assert.AreEqual(new DateTime(2025, 1, 2), result.Value);
        }

        [TestMethod]
        public void toDateOrNull_StringInvalidNoFormat_ReturnsNull()
        {
            object input = "invalid_date";
            var result = input.toDateOrNull();
            Assert.IsNull(result);
        }

        #endregion

        #region toDecimal

        [TestMethod]
        public void toDecimal_Null_ReturnsDefault()
        {
            object input = null;
            Assert.AreEqual(0m, input.toDecimal());
        }

        [TestMethod]
        public void toDecimal_AlreadyDecimal_ReturnsSameValue()
        {
            decimal dec = 123.456m;
            object input = dec;
            Assert.AreEqual(dec, input.toDecimal());
        }

        [TestMethod]
        public void toDecimal_ValidString_ReturnsParsed()
        {
            object input = "123.45";
            Assert.AreEqual(123.45m, input.toDecimal());
        }

        [TestMethod]
        public void toDecimal_InvalidString_ReturnsDefault()
        {
            object input = "invalid";
            Assert.AreEqual(999m, input.toDecimal(999m));
        }

        [TestMethod]
        public void toDecimal_EmptyString_ReturnsDefault()
        {
            object input = "";
            Assert.AreEqual(0m, input.toDecimal());
        }

        #endregion

        #region toDouble

        [TestMethod]
        public void toDouble_Null_ReturnsDefault()
        {
            object input = null;
            Assert.AreEqual(0.0, input.toDouble());
        }

        [TestMethod]
        public void toDouble_AlreadyDouble_ReturnsSameValue()
        {
            double d = 123.456;
            object input = d;
            Assert.AreEqual(d, input.toDouble());
        }

        [TestMethod]
        public void toDouble_ValidString_ReturnsParsed()
        {
            object input = "123.45";
            Assert.AreEqual(123.45, input.toDouble());
        }

        [TestMethod]
        public void toDouble_InvalidString_ReturnsDefault()
        {
            object input = "invalid";
            Assert.AreEqual(999.99, input.toDouble(999.99));
        }

        [TestMethod]
        public void toDouble_EmptyString_ReturnsDefault()
        {
            object input = "";
            Assert.AreEqual(0.0, input.toDouble());
        }

        #endregion

        #region toFloat

        [TestMethod]
        public void toFloat_Null_ReturnsDefault()
        {
            object input = null;
            Assert.AreEqual(0.0f, input.toFloat());
        }

        [TestMethod]
        public void toFloat_AlreadyFloat_ReturnsSameValue()
        {
            float f = 123.456f;
            object input = f;
            Assert.AreEqual(f, input.toFloat());
        }

        [TestMethod]
        public void toFloat_ValidString_ReturnsParsed()
        {
            object input = "123.45";
            Assert.AreEqual(123.45f, input.toFloat(), 0.00001f);
        }

        [TestMethod]
        public void toFloat_InvalidString_ReturnsDefault()
        {
            object input = "invalid";
            Assert.AreEqual(999.99f, input.toFloat(999.99f), 0.00001f);
        }

        [TestMethod]
        public void toFloat_EmptyString_ReturnsDefault()
        {
            object input = "";
            Assert.AreEqual(0.0f, input.toFloat());
        }

        #endregion

        #region toInt

        [TestMethod]
        public void toInt_Null_ReturnsDefault()
        {
            object input = null;
            Assert.AreEqual(0, input.toInt());
        }

        [TestMethod]
        public void toInt_AlreadyInt_ReturnsSameValue()
        {
            int i = 123;
            object input = i;
            Assert.AreEqual(i, input.toInt());
        }

        [TestMethod]
        public void toInt_ValidString_ReturnsParsed()
        {
            object input = "123";
            Assert.AreEqual(123, input.toInt());
        }

        [TestMethod]
        public void toInt_InvalidString_ReturnsDefault()
        {
            object input = "invalid";
            Assert.AreEqual(999, input.toInt(999));
        }

        [TestMethod]
        public void toInt_EmptyString_ReturnsDefault()
        {
            object input = "";
            Assert.AreEqual(0, input.toInt());
        }

        #endregion

        #region toLong

        [TestMethod]
        public void toLong_Null_ReturnsDefault()
        {
            object input = null;
            Assert.AreEqual(0L, input.toLong());
        }

        [TestMethod]
        public void toLong_AlreadyLong_ReturnsSameValue()
        {
            long l = 123456789;
            object input = l;
            Assert.AreEqual(l, input.toLong());
        }

        [TestMethod]
        public void toLong_ValidString_ReturnsParsed()
        {
            object input = "123456789";
            Assert.AreEqual(123456789L, input.toLong());
        }

        [TestMethod]
        public void toLong_InvalidString_ReturnsDefault()
        {
            object input = "invalid";
            Assert.AreEqual(999999L, input.toLong(999999L));
        }

        [TestMethod]
        public void toLong_EmptyString_ReturnsDefault()
        {
            object input = "";
            Assert.AreEqual(0L, input.toLong());
        }

        #endregion

        #region toStr

        [TestMethod]
        public void toStr_Null_ReturnsEmpty()
        {
            object input = null;
            Assert.AreEqual(string.Empty, input.toStr());
        }

        [TestMethod]
        public void toStr_NonNullObject_ReturnsToString()
        {
            object input = 123;
            Assert.AreEqual("123", input.toStr());
        }

        [TestMethod]
        public void toStr_EmptyString_ReturnsEmpty()
        {
            object input = "";
            Assert.AreEqual("", input.toStr());
        }

        #endregion
    }
}