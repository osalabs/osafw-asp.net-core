using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

#pragma warning disable CA1416 // System.Drawing supported on Windows only

namespace osafw.Tests;

[TestClass]
public class SecurityAttachmentTests
{
    private string tempRoot = null!;
    private string host = null!;

    private sealed class EmptyAttLinks : AttLinks
    {
        public override FwList listActiveByAtt(int att_id) => [];
    }

    private sealed class TestAttLinks : AttLinks
    {
        public Dictionary<int, FwList> ActiveLinksByAtt { get; } = [];

        public override FwList listActiveByAtt(int att_id)
        {
            return ActiveLinksByAtt.TryGetValue(att_id, out var rows) ? rows : [];
        }
    }

    private sealed class SecurityFwEntities : FwEntities
    {
        public Dictionary<int, string> Codes { get; } = [];

        public override DBRow one(int id)
        {
            return Codes.TryGetValue(id, out var code)
                ? new DBRow(DB.h("id", id, "icode", code, "status", STATUS_ACTIVE))
                : [];
        }
    }

    private sealed class ParentAccessRows : FwModel
    {
        public HashSet<int> AllowedIds { get; } = [];
        public List<(int Id, string Action)> Checks { get; } = [];

        public ParentAccessRows()
        {
            table_name = "parent_access_rows";
        }

        public override void checkAccess(int id = 0, string action = "")
        {
            Checks.Add((id, action));
            if (!AllowedIds.Contains(id))
                throw new AuthException();
        }
    }

    private sealed class PlainParentRows : FwModel
    {
        public PlainParentRows()
        {
            table_name = "plain_parent_rows";
        }

        public override DBRow one(int id)
        {
            return new DBRow(DB.h("id", id, "status", STATUS_ACTIVE));
        }
    }

    private sealed class TestAtt : Att
    {
        public Dictionary<int, FwDict> Rows { get; } = [];
        public List<FwDict> Updates { get; } = [];

        public override DBRow one(int id)
        {
            return Rows.TryGetValue(id, out var row) ? new DBRow(new FwDict(row)) : [];
        }

        public override DBRow oneByIcode(string icode)
        {
            foreach (var row in Rows.Values)
            {
                if (row["icode"].toStr() == icode)
                    return new DBRow(new FwDict(row));
            }

            return [];
        }

        public override bool update(int id, FwDict item)
        {
            Updates.Add(new FwDict(item));
            if (!Rows.TryGetValue(id, out var row))
            {
                row = DB.h("id", id);
                Rows[id] = row;
            }

            Utils.mergeHash(row, item);
            return true;
        }
    }

    private sealed class TestAttController : AttController
    {
        private readonly Att att;

        public TestAttController(Att att)
        {
            this.att = att;
        }

        public override void init(FW fw)
        {
            base.init(fw);
            model = att;
            model.init(fw);
        }
    }

    [TestInitialize]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "security-attachment-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        host = "security-attachment-" + Guid.NewGuid().ToString("N") + ".test";
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, true);

        FwConfig.init(null, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(), "");
    }

    [TestMethod]
    public void CheckAccess_RejectsObjectBoundAttachmentWhenParentDenied()
    {
        var fw = createFw();
        var att = createAtt(fw);
        att.Rows[1] = DB.h("id", 1, "status", FwModel.STATUS_ACTIVE, "fwentities_id", 7, "item_id", 10);
        att.Rows[2] = DB.h("id", 2, "status", FwModel.STATUS_ACTIVE, "fwentities_id", 7, "item_id", 20);
        var entities = createEntities(fw);
        entities.Codes[7] = "parent_access_rows";
        var parent = new ParentAccessRows();
        parent.AllowedIds.Add(10);
        parent.init(fw);
        TestHelpers.RegisterModel(fw, parent);

        att.checkAccess(1);
        Assert.ThrowsExactly<AuthException>(() => att.checkAccess(2));

        CollectionAssert.Contains(parent.Checks, (10, Att.ACCESS_ACTION_VIEW));
        CollectionAssert.Contains(parent.Checks, (20, Att.ACCESS_ACTION_VIEW));
    }

    [TestMethod]
    public void CheckAccess_RejectsObjectBoundAttachmentWithoutExplicitParentPolicy()
    {
        var fw = createFw();
        var att = createAtt(fw);
        att.Rows[3] = DB.h("id", 3, "status", FwModel.STATUS_ACTIVE, "fwentities_id", 8, "item_id", 30);
        var entities = createEntities(fw);
        entities.Codes[8] = "plain_parent_rows";
        var parent = new PlainParentRows();
        parent.init(fw);
        TestHelpers.RegisterModel(fw, parent);

        Assert.ThrowsExactly<AuthException>(() => att.checkAccess(3));
    }

    [TestMethod]
    public void CheckAccess_LinkRejectsSameTargetAttachmentWhenParentDenied()
    {
        var fw = createFw();
        var att = createAtt(fw);
        att.Rows[4] = DB.h("id", 4, "status", FwModel.STATUS_ACTIVE);
        var links = new TestAttLinks();
        links.ActiveLinksByAtt[4] = [DB.h("att_id", 4, "fwentities_id", 7, "item_id", 40, "status", FwModel.STATUS_ACTIVE)];
        links.init(fw);
        TestHelpers.RegisterModel(fw, (AttLinks)links);
        var entities = createEntities(fw);
        entities.Codes[7] = "parent_access_rows";
        var parent = new ParentAccessRows();
        parent.init(fw);
        TestHelpers.RegisterModel(fw, parent);

        Assert.ThrowsExactly<AuthException>(() => att.checkAccess(4, Att.ACCESS_ACTION_LINK, 7, 40));

        CollectionAssert.Contains(parent.Checks, (40, Att.ACCESS_ACTION_LINK));
    }

    [TestMethod]
    public void RedirectS3_DoesNotIssueRedirectBeforeAuthorization()
    {
        var fw = createFw();
        var att = createAtt(fw);
        var item = DB.h(
            "id", 9,
            "icode", "s3code",
            "status", FwModel.STATUS_ACTIVE,
            "is_s3", 1,
            "fwentities_id", 7,
            "item_id", 20,
            "fname", "private.pdf",
            "ext", ".pdf");
        att.Rows[9] = item;
        var entities = createEntities(fw);
        entities.Codes[7] = "parent_access_rows";
        var parent = new ParentAccessRows();
        parent.init(fw);
        TestHelpers.RegisterModel(fw, parent);

        Assert.ThrowsExactly<AuthException>(() => att.redirectS3(item, "", "inline"));

        Assert.AreNotEqual(StatusCodes.Status302Found, fw.response.StatusCode);
        Assert.IsTrue(StringValues.IsNullOrEmpty(fw.response.Headers.Location));
    }

    [TestMethod]
    public void ShowPreview_NonImageRequiresAuthorizationBeforeFallbackRedirect()
    {
        var fw = createFw();
        fw.FORM["preview"] = "1";
        var att = createAtt(fw);
        att.Rows[10] = DB.h(
            "id", 10,
            "icode", "doccode",
            "status", FwModel.STATUS_ACTIVE,
            "is_image", 0,
            "fwentities_id", 7,
            "item_id", 20,
            "fname", "private.pdf",
            "ext", ".pdf");
        var entities = createEntities(fw);
        entities.Codes[7] = "parent_access_rows";
        var parent = new ParentAccessRows();
        parent.init(fw);
        TestHelpers.RegisterModel(fw, parent);
        var controller = new TestAttController(att);
        controller.init(fw);

        Assert.ThrowsExactly<AuthException>(() => controller.ShowAction("doccode"));

        Assert.AreNotEqual(StatusCodes.Status302Found, fw.response.StatusCode);
        Assert.IsTrue(StringValues.IsNullOrEmpty(fw.response.Headers.Location));
    }

    [TestMethod]
    public void GetUrl_ForS3AttachmentReturnsAuthorizedAppRoute()
    {
        var fw = createFw();
        var att = createAtt(fw);
        var item = DB.h("id", 12, "icode", "s3code", "is_s3", 1);

        var url = att.getUrl(item, "s");

        Assert.AreEqual("/Att/s3code?size=s", url);
    }

    [TestMethod]
    public void TransmitFile_ForcesRiskyHtmlDownload()
    {
        var fw = createFw();
        fw.response.Body = new MemoryStream();
        var att = createAtt(fw);
        att.Rows[11] = DB.h(
            "id", 11,
            "status", FwModel.STATUS_ACTIVE,
            "fname", "payload.html",
            "ext", ".html",
            "is_image", 0);
        var path = UploadUtils.getUploadImgPath(fw, "att", 11, "", ".html");
        File.WriteAllText(path, "<script>alert(1)</script>");

        att.transmitFile(11, "", "inline");

        StringAssert.StartsWith(fw.response.Headers.ContentDisposition.ToString(), "attachment;");
        Assert.AreEqual("application/octet-stream", fw.response.Headers.ContentType.ToString());
    }

    [TestMethod]
    public void TransmitFile_UsesStoredExtensionWhenFilenameLooksSafe()
    {
        var fw = createFw();
        fw.response.Body = new MemoryStream();
        var att = createAtt(fw);
        att.Rows[13] = DB.h(
            "id", 13,
            "status", FwModel.STATUS_ACTIVE,
            "fname", "photo.png",
            "ext", ".html",
            "is_image", 0);
        var path = UploadUtils.getUploadImgPath(fw, "att", 13, "", ".html");
        File.WriteAllText(path, "<script>alert(1)</script>");

        att.transmitFile(13, "", "inline");

        StringAssert.StartsWith(fw.response.Headers.ContentDisposition.ToString(), "attachment;");
        Assert.AreEqual("application/octet-stream", fw.response.Headers.ContentType.ToString());
    }

    [TestMethod]
    public void UploadOne_SafeImageStillUploadsAndCreatesImageMetadata()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("System.Drawing is not supported on non-Windows platforms in this environment.");

        var fw = createFw();
        var att = createAtt(fw);
        var file = createPngFormFile("file1", "safe.png", 32, 24);
        fw.request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });

        var result = att.uploadOne(21, file, true);

        Assert.IsNotNull(result);
        Assert.AreEqual(".png", result["ext"]);
        Assert.AreEqual(1, result["is_image"].toInt());
        Assert.IsTrue(File.Exists(result["filepath"].toStr()));
        Assert.AreEqual(1, att.Updates.Count);
    }

    [TestMethod]
    public void UploadOne_RejectsOversizedImageDimensionsBeforeResize()
    {
        var fw = createFw();
        var att = createAtt(fw);
        var file = createFormFile("file1", "huge.png", createPngHeader(50000, 50000), "image/png");
        fw.request.Form = new FormCollection(new Dictionary<string, StringValues>(), new FormFileCollection { file });

        Assert.ThrowsExactly<UserException>(() => att.uploadOne(31, file, true));

        Assert.AreEqual(0, att.Updates.Count);
        Assert.IsFalse(File.Exists(UploadUtils.getUploadImgPath(fw, "att", 31, "", ".png")));
    }

    [TestMethod]
    public void AttachmentContentPolicy_DowngradesActiveS3UploadMetadata()
    {
        Assert.AreEqual("attachment", UploadUtils.dispositionForAttachment("payload.svg", "inline", "image/svg+xml"));
        Assert.AreEqual("application/octet-stream", UploadUtils.contentTypeForAttachment("payload.svg", "image/svg+xml"));
        Assert.AreEqual("inline", UploadUtils.dispositionForAttachment("photo.png", "inline", "image/png"));
        Assert.AreEqual("image/png", UploadUtils.contentTypeForAttachment("photo.png", "image/png"));
    }

    private TestAtt createAtt(FW fw)
    {
        var att = new TestAtt();
        att.init(fw);
        TestHelpers.RegisterModel(fw, (Att)att);
        var links = new EmptyAttLinks();
        links.init(fw);
        TestHelpers.RegisterModel(fw, (AttLinks)links);
        return att;
    }

    private SecurityFwEntities createEntities(FW fw)
    {
        var entities = new SecurityFwEntities();
        entities.init(fw);
        TestHelpers.RegisterModel(fw, (FwEntities)entities);
        return entities;
    }

    private FW createFw()
    {
        var context = new DefaultHttpContext
        {
            Session = new TestHelpers.FakeSession(),
        };
        context.Request.Host = new HostString(host);

        var fw = new FW(context, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
        var settings = FwConfig.GetCurrentSettings();
        settings["site_root"] = tempRoot;
        settings["UPLOAD_DIR"] = "/upload";
        settings["ASSETS_URL"] = "/assets";
        return fw;
    }

    private static IFormFile createPngFormFile(string name, string fileName, int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        using var buffer = new MemoryStream();
        bitmap.Save(buffer, ImageFormat.Png);
        return createFormFile(name, fileName, buffer.ToArray(), "image/png");
    }

    private static IFormFile createFormFile(string name, string fileName, byte[] bytes, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, name, fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static byte[] createPngHeader(int width, int height)
    {
        var bytes = new byte[33];
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        bytes[4] = 0x0D;
        bytes[5] = 0x0A;
        bytes[6] = 0x1A;
        bytes[7] = 0x0A;
        bytes[11] = 0x0D;
        bytes[12] = 0x49;
        bytes[13] = 0x48;
        bytes[14] = 0x44;
        bytes[15] = 0x52;
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        bytes[24] = 8;
        bytes[25] = 2;
        return bytes;
    }
}

#pragma warning restore CA1416
