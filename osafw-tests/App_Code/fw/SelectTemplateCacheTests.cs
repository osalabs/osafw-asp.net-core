using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace osafw.Tests;

[TestClass]
public class SelectTemplateCacheTests
{
    [TestMethod]
    public void LookupPreservesFirstUsableLabelAndReloadsReplacedOrDeletedFiles()
    {
        using var scope = FwConfig.beginScope();
        var root = Path.Combine(Path.GetTempPath(), "select-labels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        FwConfig.GetCurrentSettings()["template"] = root;
        var path = Path.Combine(root, "status.sel");
        try
        {
            File.WriteAllText(path, "malformed\na|\na|`First`\na|Second\n|Empty key\nb|Label|with separator");
            Assert.AreEqual("First", FormUtils.selectTplName("/status.sel", "a"));
            Assert.AreEqual("Empty key", FormUtils.selectTplName("/status.sel", ""));
            Assert.AreEqual("Label|with separator", FormUtils.selectTplName("/status.sel", "b"));
            Assert.AreEqual("", FormUtils.selectTplName("/status.sel", "missing"));
            Parallel.For(0, 100, _ => Assert.AreEqual("First", FormUtils.selectTplName("./status.sel", "a", "/")));
            File.WriteAllText(path, "a|Replacement");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
            Assert.AreEqual("Replacement", FormUtils.selectTplName("/status.sel", "a"));
            File.Delete(path);
            Assert.AreEqual("", FormUtils.selectTplName("/status.sel", "a"));
            File.WriteAllText(path, "a|Restored");
            Assert.AreEqual("Restored", FormUtils.selectTplName("/status.sel", "a"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ReadFailureRetainsLastGoodLabelsAndSiblingPrefixIsRejected()
    {
        using var scope = FwConfig.beginScope();
        var parent = Path.Combine(Path.GetTempPath(), "select-labels-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "templates");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(parent, "templates-other"));
        FwConfig.GetCurrentSettings()["template"] = root;
        var path = Path.Combine(root, "status.sel");
        try
        {
            File.WriteAllText(path, "a|Original");
            Assert.AreEqual("Original", FormUtils.selectTplName("/status.sel", "a"));
            File.WriteAllText(path, "a|Changed value");
            using (var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.AreEqual("Original", FormUtils.selectTplName("/status.sel", "a"));
            Assert.AreEqual("Changed value", FormUtils.selectTplName("/status.sel", "a"));
            File.WriteAllText(Path.Combine(parent, "templates-other", "private.sel"), "a|Outside");
            Assert.AreEqual("", FormUtils.selectTplName("/../templates-other/private.sel", "a"));
            Assert.AreEqual("", FormUtils.selectTplName("status.sel", "a"));
            for (var i = 0; i < 140; i++)
            {
                File.WriteAllText(Path.Combine(root, i + ".sel"), "a|" + i);
                Assert.AreEqual(i.ToString(), FormUtils.selectTplName("/" + i + ".sel", "a"));
            }
            Assert.AreEqual("Changed value", FormUtils.selectTplName("/status.sel", "a"));
        }
        finally { Directory.Delete(parent, true); }
    }
}
