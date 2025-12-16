using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Runtime.Serialization;

namespace osafw.Tests
{
    [TestClass]
    public class UploadUtilsTests
    {
        private string tempRoot = null!;
        private FW fw = null!;

        [TestInitialize]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "uploadutils-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempRoot);

            fw = (FW)FormatterServices.GetUninitializedObject(typeof(FW));

            // isolate config for predictable paths
            var settings = FwConfig.settings;
            settings.Clear();
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
            Assert.AreEqual(0, Directory.GetFiles(dir).Length);
        }

        [TestMethod]
        public void MimeMapping_UsesKnownContentTypes()
        {
            Assert.AreEqual("image/png", UploadUtils.mimeMapping("logo.png"));
            Assert.AreEqual("application/octet-stream", UploadUtils.mimeMapping("unknown.ext"));
        }
    }
}
