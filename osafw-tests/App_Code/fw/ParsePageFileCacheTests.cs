using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class ParsePageFileCacheTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void empty_inline_template_renders_empty_without_warning(bool is_root_configured)
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), $"parsepage-empty-inline-{Guid.NewGuid():N}");
        List<string> warnings = [];
        var parser = new ParsePage(new ParsePageOptions
        {
            TemplatesRoot = is_root_configured ? templateRoot : "",
            Logger = (level, messages) =>
            {
                if (level == LogLevel.WARN)
                {
                    warnings.Add(string.Concat(messages));
                }
            },
        });

        Assert.AreEqual("", parser.parse_string("", []));
        Assert.AreEqual("", parser.parse_page("", "", []));
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void failed_read_is_quiet_and_recovers_without_clearing_shared_cache(bool is_check_modifications)
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), $"parsepage-read-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(templateRoot);

        try
        {
            var templatePath = Path.Combine(templateRoot, "page.html");
            File.WriteAllText(templatePath, "Hello <~name>");
            List<string> warnings = [];
            var options = new ParsePageOptions
            {
                TemplatesRoot = templateRoot,
                IsCheckFileModifications = is_check_modifications,
                IsLangUpdate = false,
                Logger = (level, messages) =>
                {
                    if (level == LogLevel.WARN)
                    {
                        warnings.Add(string.Concat(messages));
                    }
                },
            };
            var parser = new ParsePage(options);

            using (var lockedFile = new FileStream(templatePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<IOException>(() => File.ReadAllText(templatePath));
                Assert.AreEqual("", parser.parse_page("", "page.html", new FwDict { ["name"] = "World" }));
            }

            // A later request gets a new parser but shares the process-wide file cache.
            var nextRequestParser = new ParsePage(options);
            Assert.AreEqual("Hello World", nextRequestParser.parse_page("", "page.html", new FwDict { ["name"] = "World" }));
            Assert.AreEqual("Hello Again", parser.parse_page("", "page.html", new FwDict { ["name"] = "Again" }));
            Assert.AreEqual(0, warnings.Count);
        }
        finally
        {
            Directory.Delete(templateRoot, true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void missing_optional_include_is_quiet_and_recovers_when_created(bool is_check_modifications)
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), $"parsepage-missing-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(templateRoot);

        try
        {
            File.WriteAllText(Path.Combine(templateRoot, "page.html"), "Before<~./optional/part>After");
            List<string> warnings = [];
            var options = new ParsePageOptions
            {
                TemplatesRoot = templateRoot,
                IsCheckFileModifications = is_check_modifications,
                IsLangUpdate = false,
                Logger = (level, messages) =>
                {
                    if (level == LogLevel.WARN)
                    {
                        warnings.Add(string.Concat(messages));
                    }
                },
            };
            var parser = new ParsePage(options);
            Assert.AreEqual("BeforeAfter", parser.parse_page("", "page.html", []));

            Directory.CreateDirectory(Path.Combine(templateRoot, "optional"));
            Assert.AreEqual("BeforeAfter", parser.parse_page("", "page.html", []));
            File.WriteAllText(Path.Combine(templateRoot, "optional", "part.html"), "Included");

            Assert.AreEqual("BeforeIncludedAfter", new ParsePage(options).parse_page("", "page.html", []));
            Assert.AreEqual(0, warnings.Count);
        }
        finally
        {
            Directory.Delete(templateRoot, true);
        }
    }

    [TestMethod]
    public void failed_refresh_preserves_last_successful_entry_and_retries_changed_file()
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), $"parsepage-refresh-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(templateRoot);
        try
        {
            var templatePath = Path.Combine(templateRoot, "page.html");
            File.WriteAllText(templatePath, "Original");
            var originalTime = File.GetLastWriteTimeUtc(templatePath);
            var options = new ParsePageOptions
            {
                TemplatesRoot = templateRoot,
                IsCheckFileModifications = true,
                IsLangUpdate = false,
            };
            Assert.AreEqual("Original", new ParsePage(options).parse_page("", "page.html", []));

            File.WriteAllText(templatePath, "Replacement");
            File.SetLastWriteTimeUtc(templatePath, originalTime.AddMinutes(1));
            using (var lockedFile = new FileStream(templatePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<IOException>(() => File.ReadAllText(templatePath));
                Assert.AreEqual("", new ParsePage(options).parse_page("", "page.html", []));
            }

            // The failed refresh must not have replaced the last successful cache entry.
            File.SetLastWriteTimeUtc(templatePath, originalTime);
            Assert.AreEqual("Original", new ParsePage(options).parse_page("", "page.html", []));
            File.SetLastWriteTimeUtc(templatePath, originalTime.AddMinutes(2));
            Assert.AreEqual("Replacement", new ParsePage(options).parse_page("", "page.html", []));
        }
        finally
        {
            Directory.Delete(templateRoot, true);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Original")]
    public void successful_reads_including_empty_files_remain_cached_in_production(string original)
    {
        var templateRoot = Path.Combine(Path.GetTempPath(), $"parsepage-success-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(templateRoot);

        try
        {
            var templatePath = Path.Combine(templateRoot, "page.html");
            File.WriteAllText(templatePath, original);
            var options = new ParsePageOptions { TemplatesRoot = templateRoot, IsLangUpdate = false };
            Assert.AreEqual(original, new ParsePage(options).parse_page("", "page.html", []));

            File.WriteAllText(templatePath, "Replacement");
            Assert.AreEqual(original, new ParsePage(options).parse_page("", "page.html", []));
        }
        finally
        {
            Directory.Delete(templateRoot, true);
        }
    }
}
