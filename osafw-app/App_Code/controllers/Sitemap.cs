// Sitemap controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com
using System.Xml.Linq;

namespace osafw;

public class SitemapController : FwController
{
    protected Spages model = null!;
    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Spages>();
        model0 = model;
        base_url = "/sitemap";
        // override layout
        fw.G["PAGE_LAYOUT"] = fw.G["PAGE_LAYOUT_PUBLIC"];
    }

    public override void checkAccess()
    {
        // Sitemaps are available to visitors; the model filters content for the audience.
    }

    public FwDict IndexAction()
    {
        fw.response.Headers.CacheControl = "no-cache";
        if (fw.route.format == "xml")
        {
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var root = new XElement(ns + "urlset");
            foreach (var page in model.listIndexable(0))
            {
                string url = model.canonicalUrl(page);
                if (url.Length > 0)
                {
                    root.Add(new XElement(ns + "url", new XElement(ns + "loc", url)));
                }
            }

            fw.response.ContentType = "application/xml; charset=utf-8";
            rw(new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString());
            return null!;
        }

        fw.cache_control = "private, no-store";
        fw.response.Headers.CacheControl = fw.cache_control;
        return new FwDict
        {
            ["cms"] = true,
            ["hide_sidebar"] = true,
            ["pages"] = model.listIndexable()
        };
    }

    public FwDict XmlAction()
    {
        fw.route.format = "xml";
        return IndexAction();
    }
}
