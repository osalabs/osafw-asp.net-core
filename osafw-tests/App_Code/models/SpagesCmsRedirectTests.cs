#if isSQLite
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace osafw.Tests;

public partial class SpagesCmsTests
{
    [TestMethod]
    [DataRow("https://www.google.com/search?q=framework#results")]
    [DataRow("http://example.net/info")]
    public void ExternalRedirectGoesThroughDraftAndPublicationActions(string destination)
    {
        int page = create("external-target");
        var current = request(Users.ACL_MANAGER);
        addXssToken(current, "external-redirect-token");
        current.FORM["item"] = new FwDict { ["redirect_url"] = destination };
        var saveController = adminController(current, "Save", "POST");
        saveController.Validate(page, new FwDict { ["iname"] = "Sample", ["url"] = "external-target", ["redirect_url"] = destination });
        saveController.SaveAction(page);
        Assert.ThrowsExactly<NotFoundException>(() => request(0).model<Spages>().showCmsPage("/external-target"));
        adminController(current, "Publish", "POST").PublishAction(page);
        var published = renderCmsResponse("/external-target");
        Assert.AreEqual(301, published.Current.response.StatusCode);
        Assert.AreEqual(destination, published.Current.response.Headers.Location.ToString());
        Assert.AreEqual("no-store", published.Current.response.Headers.CacheControl.ToString());
        Assert.IsFalse(request(0).model<Spages>().listIndexable().Any(row => row["id"].toInt() == page));
        save(page, "Unpublished changes");
        Assert.AreEqual(destination, renderCmsResponse("/external-target").Current.response.Headers.Location.ToString());
    }

    [TestMethod]
    public async Task InternationalRedirectUsesAsciiAtTheHttpHeaderBoundary()
    {
        const string DESTINATION = "https://éxample.org/café?q=résumé#été";
        const string EXPECTED = "https://xn--xample-9ua.org/caf%C3%A9?q=r%C3%A9sum%C3%A9#%C3%A9t%C3%A9";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.Run(context =>
        {
            context.Response.StatusCode = 301;
            context.Response.Headers.Location = Spages.redirectUrl(DESTINATION);
            return Task.CompletedTask;
        });
        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            using var response = await client.GetAsync(app.Urls.Single());
            Assert.AreEqual(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.AreEqual(EXPECTED, response.Headers.Location!.OriginalString);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [TestMethod]
    [DataRow("javascript:alert(1)")]
    [DataRow("data:text/html,hello")]
    [DataRow("//example.net/path")]
    [DataRow("https://user:password@example.net/")]
    [DataRow("https://example.net/\r\nLocation: https://elsewhere.test/")]
    [DataRow("https://example.net\\path")]
    [DataRow("https:///missing-host")]
    public void UnsafeRedirectDestinationsAreRejectedBeforeSaving(string destination)
    {
        int page = create("invalid-external");
        string before = cms.oneDraftOrFail(page)["redirect_url"].toStr();
        var current = request();
        addXssToken(current, "invalid-redirect-token");
        current.FORM["item"] = new FwDict { ["redirect_url"] = destination };
        Assert.ThrowsExactly<UserException>(() => adminController(current, "Save", "POST").SaveAction(page));
        Assert.AreEqual(before, request().model<Spages>().oneDraftOrFail(page)["redirect_url"].toStr());
        Assert.ThrowsExactly<UserException>(() => cms.saveDraft(page, new FwDict { ["url_aliases"] = destination }));
    }

    [TestMethod]
    public void RedirectLengthLimitIncludesEscapedUnicode()
    {
        int page = create("redirect-length");
        int revisions = cms.listRevisions(page).Count;
        foreach (string destination in new[] { "https://example.net/" + new string('a', 250), "https://example.net/" + new string('é', 80) })
        {
            var current = request();
            addXssToken(current, "redirect-length-token");
            current.FORM["item"] = new FwDict { ["redirect_url"] = destination };
            Assert.ThrowsExactly<UserException>(() => adminController(current, "Save", "POST").SaveAction(page));
        }
        Assert.AreEqual(revisions, cms.listRevisions(page).Count);
        Assert.AreEqual("", cms.oneDraftOrFail(page)["redirect_url"].toStr());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(Users.ACL_MANAGER)]
    public void ScheduledExternalRedirectRetainsCurrentContentAndAncestorAccess(int access)
    {
        int parent = create("external-parent", access: access);
        publish(parent);
        int page = create("scheduled-external", parent);
        int original = publish(page);
        const string DESTINATION = "https://example.net/scheduled";
        cms.saveDraft(page, new FwDict { ["redirect_url"] = DESTINATION });
        int scheduled = publish(page, DateTime.UtcNow.AddDays(1));
        string path = "/external-parent/scheduled-external";
        Assert.AreEqual(200, renderCmsResponse(path, access).Current.response.StatusCode);
        var before = DateTime.UtcNow.AddDays(-2);
        fw.db.update(Spages.REVISION_TABLE, DB.h("effective_time", before), DB.h("id", original));
        fw.db.update(Spages.REVISION_TABLE, DB.h("effective_time", before.AddDays(1)), DB.h("id", scheduled));
        var after = renderCmsResponse(path, access);
        Assert.AreEqual(301, after.Current.response.StatusCode);
        Assert.AreEqual(DESTINATION, after.Current.response.Headers.Location.ToString());
        Assert.AreEqual(access > 0 ? "private, no-store" : "no-store", after.Current.response.Headers.CacheControl.ToString());
        if (access > 0)
            Assert.ThrowsExactly<NotFoundException>(() => request(0).model<Spages>().showCmsPage(path));
        request().model<Spages>().updateWorkflow(page, "unpublish");
        Assert.ThrowsExactly<NotFoundException>(() => request(access).model<Spages>().showCmsPage(path));
    }

    [TestMethod]
    [DataRow("", "https://example.org")]
    [DataRow("/portal", "https://example.org")]
    [DataRow("", "https://éxample.org")]
    [DataRow("/portal", "https://xn--xample-9ua.org")]
    public void RedirectsRespectApplicationRootAndRejectAbsoluteLocalLoops(string appRoot, string origin)
    {
        config = new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["appSettings:ROOT_DOMAIN"] = origin + appRoot
        }).Build();
        cms = request(pathBase: appRoot).model<Spages>();
        int first = create("redirect-first");
        cms.saveDraft(first, new FwDict { ["redirect_url"] = "/redirect-second", ["url_aliases"] = "/redirect-old" });
        publish(first);
        int second = create("redirect-second");
        cms.saveDraft(second, new FwDict { ["redirect_url"] = "https://example.net/final?x=1#section" });
        publish(second);
        Assert.AreEqual(appRoot + "/redirect-second", renderCmsResponse("/redirect-first", pathBase: appRoot).Current.response.Headers.Location.ToString());
        Assert.AreEqual("https://example.net/final?x=1#section", renderCmsResponse("/redirect-second", pathBase: appRoot).Current.response.Headers.Location.ToString());
        Assert.AreEqual(appRoot + "/redirect-first", renderCmsResponse("/redirect-old", pathBase: appRoot).Current.response.Headers.Location.ToString());

        foreach (string path in new[] { "/redirect-first", "/REDIRECT-OLD", "/redirect-second?ignored=1#section" })
        {
            cms.saveDraft(second, new FwDict { ["redirect_url"] = origin + appRoot + path });
            Assert.ThrowsExactly<UserException>(() => publish(second));
            Assert.AreEqual("https://example.net/final?x=1#section", renderCmsResponse("/redirect-second", pathBase: appRoot).Current.response.Headers.Location.ToString());
        }
    }
}
#endif
