using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Serialization;

#pragma warning disable CA1416 // System.Drawing supported on Windows only
#pragma warning disable SYSLIB0050 // Formatter-based serialization is obsolete

namespace osafw.Tests
{
    [TestClass]
    public class ImageUtilsTests
    {
        private string tempDir = null!;

        static ImageUtilsTests()
        {
            AppContext.SetSwitch("System.Drawing.EnableUnixSupport", true);
        }

        [TestInitialize]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "imageutils-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        [TestMethod]
    public void Rotate_UsesExifOrientation()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("System.Drawing is not supported on non-Windows platforms in this environment.");

        using var bmp = new Bitmap(2, 1);
        bmp.SetPropertyItem(CreateOrientationProperty(6));

        var rotated = ImageUtils.rotate(bmp);

            Assert.IsTrue(rotated);
            Assert.AreEqual(1, bmp.Width);
            Assert.AreEqual(2, bmp.Height);
        }

        [TestMethod]
    public void Rotate_ReturnsFalseWhenNoOrientation()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("System.Drawing is not supported on non-Windows platforms in this environment.");

        using var bmp = new Bitmap(3, 3);

        var rotated = ImageUtils.rotate(bmp);

            Assert.IsFalse(rotated);
            Assert.AreEqual(3, bmp.Width);
            Assert.AreEqual(3, bmp.Height);
        }

        [TestMethod]
    public void Resize_AdjustsDimensionsAndSavesCopy()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("System.Drawing is not supported on non-Windows platforms in this environment.");

        var source = Path.Combine(tempDir, "src.jpg");
        var dest = Path.Combine(tempDir, "dest.jpg");
        using (var bmp = new Bitmap(100, 50))
        {
            bmp.Save(source, ImageFormat.Jpeg);
            }

            var resized = ImageUtils.resize(source, dest, 50, 25);

            Assert.IsTrue(resized);
            using var result = Image.FromFile(dest);
            Assert.AreEqual(50, result.Width);
            Assert.AreEqual(25, result.Height);
        }

        [TestMethod]
    public void Resize_CopiesWhenAlreadySmaller()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("System.Drawing is not supported on non-Windows platforms in this environment.");

        var source = Path.Combine(tempDir, "small.png");
        var dest = Path.Combine(tempDir, "copy.png");
        using (var bmp = new Bitmap(10, 10))
        {
            bmp.Save(source, ImageFormat.Png);
            }

            var resized = ImageUtils.resize(source, dest, 100, 100);

            Assert.IsFalse(resized);
            using var result = Image.FromFile(dest);
            Assert.AreEqual(10, result.Width);
            Assert.AreEqual(10, result.Height);
        }

        private static PropertyItem CreateOrientationProperty(short orientation)
        {
            var property = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
            property.Id = 274;
            property.Type = 3;
            property.Len = 2;
            property.Value = BitConverter.GetBytes(orientation);
            return property;
        }
    }
}
#pragma warning restore SYSLIB0050
#pragma warning restore CA1416
