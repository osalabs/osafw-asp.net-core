using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace osafw.Tests
{
    [TestClass]
    public class FwRouteTests
    {
        [TestMethod]
        public void DefaultConstructor_SetsEmptyStringsAndParams()
        {
            var route = new FwRoute();

            Assert.IsEmpty(route.controller_path);
            Assert.IsEmpty(route.method);
            Assert.IsEmpty(route.prefix);
            Assert.IsEmpty(route.controller);
            Assert.IsEmpty(route.action);
            Assert.IsEmpty(route.action_raw);
            Assert.IsEmpty(route.id);
            Assert.IsEmpty(route.action_more);
            Assert.IsEmpty(route.format);
            Assert.IsNotNull(route.@params);
            Assert.AreEqual(0, route.@params.Count);
        }

        [TestMethod]
        public void Route_AllowsPopulatingProperties()
        {
            var route = new FwRoute
            {
                controller_path = "/Admin/Users",
                method = "GET",
                prefix = "/Admin",
                controller = "Users",
                action = "Show",
                action_raw = "Show",
                id = "42",
                action_more = FW.ACTION_MORE_EDIT,
                format = "json",
            };

            route.@params.Add("q");
            route.@params.Add("page");

            Assert.AreEqual("/Admin/Users", route.controller_path);
            Assert.AreEqual("GET", route.method);
            Assert.AreEqual("/Admin", route.prefix);
            Assert.AreEqual("Users", route.controller);
            Assert.AreEqual("Show", route.action);
            Assert.AreEqual("Show", route.action_raw);
            Assert.AreEqual("42", route.id);
            Assert.AreEqual(FW.ACTION_MORE_EDIT, route.action_more);
            Assert.AreEqual("json", route.format);
            CollectionAssert.AreEqual(new[] { "q", "page" }, route.@params);
        }
    }
}
