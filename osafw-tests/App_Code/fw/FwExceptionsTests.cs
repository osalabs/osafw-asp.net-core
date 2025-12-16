using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace osafw.Tests
{
    [TestClass]
    public class FwExceptionsTests
    {
        [TestMethod]
        public void AuthException_DefaultsToAccessDenied()
        {
            var ex = new AuthException();

            Assert.AreEqual("Access denied", ex.Message);
        }

        [TestMethod]
        public void ValidationException_UsesFriendlyPrompt()
        {
            var ex = new ValidationException();

            Assert.AreEqual("Please review and update your input", ex.Message);
        }

        [TestMethod]
        public void NotFoundException_ProvidesDefaultAndCustomMessages()
        {
            var defaultEx = new NotFoundException();
            var customEx = new NotFoundException("Custom not found");

            Assert.AreEqual("Not Found", defaultEx.Message);
            Assert.AreEqual("Custom not found", customEx.Message);
        }

        [TestMethod]
        public void FwConfigUndefinedModelException_ProvidesHelpfulMessage()
        {
            var ex = new FwConfigUndefinedModelException();

            StringAssert.Contains(ex.Message, "'model' is not defined");
        }

        [TestMethod]
        public void RedirectException_IsExceptionWithoutMessage()
        {
            var ex = new RedirectException();

            StringAssert.Contains(ex.Message, nameof(RedirectException));
            Assert.IsInstanceOfType(ex, typeof(Exception));
        }
    }
}
