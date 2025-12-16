using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace osafw.Tests
{
    [TestClass]
    public class FwLoggerTests
    {
        [TestMethod]
        public void Dumper_FormatsSpecialValuesAndCollections()
        {
            var nullDump = FwLogger.dumper(null);
            var dbNullDump = FwLogger.dumper(DBNull.Value);

            FwDict dict = [];
            dict["foo"] = "bar";
            var dictDump = FwLogger.dumper(dict);

            List<object> recursiveList = [];
            recursiveList.Add(recursiveList);
            var recursionDump = FwLogger.dumper(recursiveList);

            Assert.AreEqual("[Nothing]", nullDump);
            Assert.AreEqual("[DBNull]", dbNullDump);
            StringAssert.Contains(dictDump, "foo => bar");
            StringAssert.Contains(recursionDump, "[Too Much Recursion]");
        }

        [TestMethod]
        public void Log_IgnoresMessagesBelowLogLevel()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var logPath = Path.Combine(tempDir, "level.log");

            using (var logger = new FwLogger(LogLevel.ERROR, logPath, tempDir))
            {
                logger.log(LogLevel.DEBUG, "should be ignored");
            }

            Assert.IsFalse(File.Exists(logPath));
        }

        [TestMethod]
        public void Log_RotatesWhenFileExceedsLimit()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var logPath = Path.Combine(tempDir, "rotate.log");
            File.WriteAllText(logPath, new string('x', 2048));

            using (var logger = new FwLogger(LogLevel.DEBUG, logPath, tempDir, log_max_size: 1024))
            {
                logger.log(LogLevel.INFO, "trigger rotation");
            }

            var rotatedPath = logPath + ".1";

            Assert.IsTrue(File.Exists(rotatedPath));
            Assert.IsTrue(File.Exists(logPath));

            var newContent = File.ReadAllText(logPath);
            StringAssert.Matches(newContent, new Regex("INFO"));
        }
    }
}
