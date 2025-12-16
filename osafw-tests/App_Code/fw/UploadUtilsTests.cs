using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace osafw.Tests
{
    [TestClass]
    public class UploadUtilsTests
    {
        private string tempRoot = null!;
        private FW fw = null!;
        private string host = null!;

        [TestInitialize]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "uploadutils-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);

            fw = CreateFwWithContext();

//            var settings = FwConfig.settings;
//            settings.Clear();
            host = $"upload-{Guid.NewGuid()}";

//            fw = (FW)FormatterServices.GetUninitializedObject(typeof(FW));

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            FwConfig.init(null, config, host);

            // isolate config for predictable paths per test host
            var settings = FwConfig.GetCurrentSettings();

            settings["site_root"] = tempRoot;
            settings["UPLOAD_DIR"] = "/upload";
            settings["ROOT_URL"] = "https://example.test";
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }

        [TestMethod]
        public void UploadSimple_SavesFileAndMetadata()
        {
            var fwWithContext = CreateFwWithContext();
            var file = CreateFormFile("file1", "sample.txt", "payload");
            fwWithContext.request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });
            var up = new UploadParams(fwWithContext, "file1", Path.Combine(tempRoot, "simple"));

            var saved = UploadUtils.uploadSimple(up);

            Assert.IsTrue(saved);
            Assert.IsTrue(File.Exists(up.full_path));
            Assert.AreEqual(".txt", up.ext);
            Assert.AreEqual("sample.txt", up.orig_filename);
        }

        [TestMethod]
        public void UploadSimple_ThrowsIfRequiredMissing()
        {
            var fwWithContext = CreateFwWithContext();
            fwWithContext.request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection());
            var up = new UploadParams(fwWithContext, "missing", tempRoot) { is_required = true };

            Assert.ThrowsExactly<UserException>(() => UploadUtils.uploadSimple(up));
        }

        [TestMethod]
        public void GetUploadFileExt_NormalizesCaseAndJpeg()
        {
            Assert.AreEqual(".txt", UploadUtils.getUploadFileExt("File.TXT"));
            Assert.AreEqual(".jpg", UploadUtils.getUploadFileExt("photo.JPEG"));
        }

        [TestMethod]
        public void IsUploadImgExtAllowed_RestrictsToKnownImageTypes()
        {
            Assert.IsTrue(UploadUtils.isUploadImgExtAllowed(".jpg"));
            Assert.IsTrue(UploadUtils.isUploadImgExtAllowed(".png"));
            Assert.IsFalse(UploadUtils.isUploadImgExtAllowed(".bmp"));
        }

        [TestMethod]
        public void GetUploadDir_CreatesModuloSubfolder()
        {
            var dir = UploadUtils.getUploadDir(fw, "docs", 1234);

            StringAssert.EndsWith(dir.Replace('\\', '/'), "upload/docs/234");
            Assert.IsTrue(Directory.Exists(dir));
        }

        [TestMethod]
        public void GetUploadUrl_BuildsSizeSpecificUrl()
        {
            var url = UploadUtils.getUploadUrl(fw, "docs", 42, ".pdf", "m");

            Assert.AreEqual("https://example.test/upload/docs/42/42_m.pdf", url);
        }

        [TestMethod]
        public void GetUploadImgPath_ResolvesExistingVariants()
        {
            var dir = UploadUtils.getUploadDir(fw, "avatars", 99);
            var original = dir + @"\99.jpg";
            var medium = dir + @"\99_m.jpg";
            File.WriteAllText(original, "orig");
            File.WriteAllText(medium, "med");

            var pathOriginal = UploadUtils.getUploadImgPath(fw, "avatars", 99, "", ".jpg");
            var pathMedium = UploadUtils.getUploadImgPath(fw, "avatars", 99, "m");

            Assert.AreEqual(original.Replace('\\', '/'), pathOriginal.Replace('\\', '/'));
            Assert.AreEqual(medium.Replace('\\', '/'), pathMedium.Replace('\\', '/'));
        }

        [TestMethod]
        public void RemoveUploadImgByPath_CleansAllSizes()
        {
        var dir = UploadUtils.getUploadDir(fw, "avatars", 101);
        var basePath = dir + @"\101";
        foreach (var suffix in new[] { "", "_l", "_m", "_s" })
        {
            File.WriteAllText(basePath + suffix + ".jpg", "x");
                File.WriteAllText(basePath + suffix + ".png", "x");
                File.WriteAllText(basePath + suffix + ".gif", "x");
            }

        var removed = UploadUtils.removeUploadImg(fw, "avatars", 101);

        Assert.IsTrue(removed);
        Assert.IsEmpty(Directory.GetFiles(dir));
    }

        [TestMethod]
        public void MimeMapping_UsesKnownContentTypes()
        {
            Assert.AreEqual("image/png", UploadUtils.mimeMapping("logo.png"));
            Assert.AreEqual("application/octet-stream", UploadUtils.mimeMapping("unknown.ext"));
        }

        [TestMethod]
        public void UploadFileSave_WritesUnderModuleFolder()
        {
            var fwWithContext = CreateFwWithContext();
            var file = CreateFormFile("file", "photo.jpg", "binary");

            var saved = UploadUtils.uploadFileSave(fwWithContext, "docs", 7, file);

            Assert.IsTrue(File.Exists(saved));
            StringAssert.Contains(saved.Replace('\\', '/'), "/upload/docs/7/7.jpg");
        }

        [TestMethod]
        public void UploadFile_UsesFormFileOverload()
        {
            var fwWithContext = CreateFwWithContext();
            var file = CreateFormFile("file1", "avatar.jpg", "binary");
            fwWithContext.request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });

            var saved = UploadUtils.uploadFile(fwWithContext, "avatars", 11, out var filepath, "file1", true);

            Assert.IsTrue(saved);
            Assert.IsTrue(File.Exists(filepath));
        }

        [TestMethod]
        public void GetUploadImgUrl_ReturnsExistingVariant()
        {
            var fwWithContext = CreateFwWithContext();
            FwConfig.GetCurrentSettings()["ROOT_URL"] = "https://example.test";
            var dir = UploadUtils.getUploadDir(fwWithContext, "avatars", 15);
            var medium = Path.Combine(dir, "15_m.jpg");
            File.WriteAllText(medium, "img");

            var url = UploadUtils.getUploadImgUrl(fwWithContext, "avatars", 15, "m");

            Assert.AreEqual("https://example.test/upload/avatars/15/15_m.jpg", url);
        }

        private static IFormFile CreateFormFile(string name, string fileName, string content)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            return new FormFile(stream, 0, stream.Length, name, fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };
        }

        private FW CreateFwWithContext()
        {
            return TestHelpers.CreateFw(new Dictionary<string, string?>
            {
                { "site_root", tempRoot },
                { "UPLOAD_DIR", "/upload" },
                { "ROOT_URL", "https://example.test" }
            });
        }
    }
}
