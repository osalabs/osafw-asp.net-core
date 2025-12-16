using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace osafw.Tests
{
    [TestClass]
    public class FwEntitiesTests
    {
        [TestMethod]
        public void IdByIcodeOrAdd_ReturnsExistingIdWhenFound()
        {
            var sut = new FakeFwEntities
            {
                NextRow = new DBRow { { "id", "42" } }
            };

            var id = sut.idByIcodeOrAdd("existing");

            Assert.AreEqual(42, id);
            Assert.IsNull(sut.AddedRow);
        }

        [TestMethod]
        public void IdByIcodeOrAdd_AddsWhenMissing()
        {
            var sut = new FakeFwEntities
            {
                NextRow = []
            };

            var id = sut.idByIcodeOrAdd("custom_code");

            Assert.AreEqual(FakeFwEntities.GeneratedId, id);
            Assert.IsNotNull(sut.AddedRow);
            Assert.AreEqual("custom_code", sut.AddedRow!["icode"]);
            Assert.AreEqual("Custom Code", sut.AddedRow!["iname"]);
        }

        private class FakeFwEntities : FwEntities
        {
            public const int GeneratedId = 7;
            public DBRow? NextRow { get; set; }
            public FwDict? AddedRow { get; private set; }

            public override DBRow oneByIcode(string icode)
            {
                return NextRow ?? [];
            }

            public override int add(FwDict item)
            {
                AddedRow = new FwDict(item);
                return GeneratedId;
            }
        }
    }
}
