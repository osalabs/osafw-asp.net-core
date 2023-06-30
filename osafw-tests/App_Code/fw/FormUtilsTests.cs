using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}


