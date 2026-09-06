// Sitemap controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Linq;
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
        //true - allow access to all, including visitors
    }

    public FwDict IndexAction()
    {
        if (!model.isEnabled()) throw new NotFoundException();
        if (model.isCmsReady())
        {
            fw.response.Headers.CacheControl = "no-cache";
            if (fw.route.format == "xml")
            {
                XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
                var root = new XElement(ns + "urlset");
                foreach (var page in model.publishedPages(0).Where(x => !x["noindex"].toBool() && x["redirect_url"].toStr().Length == 0))
                {
                    if (page["is_home"].toBool() && !fw.config("SPAGES_HOME_ENABLED").toBool()) continue;
                    string url = model.canonicalUrl(page);
                    if (url.Length > 0) root.Add(new XElement(ns + "url", new XElement(ns + "loc", url)));
                }
                fw.response.ContentType = "application/xml; charset=utf-8";
                rw(new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString());
                return null!;
            }
            fw.cache_control = "private, no-store";
            fw.response.Headers.CacheControl = fw.cache_control;
            return DB.h("cms", true, "hide_sidebar", true, "pages", model.publishedPages());
        }
        FwDict ps = [];

        FwDict item = model.oneByFullUrl(base_url);

        FwList pages_tree = model.tree(" status=0 ", [], "parent_id, prio desc, iname");
        _add_full_url(pages_tree);

        ps["page"] = item;
        ps["pages_tree"] = pages_tree;
        ps["hide_sidebar"] = true; // TODO - control via item[template]
        return ps;
    }

    private void _add_full_url(FwList? pages_tree, string parent_url = "")
    {
        if (pages_tree == null)
            return;

        foreach (FwDict row in pages_tree)
        {
            var urlPart = row["url"].toStr();
            row["full_url"] = parent_url + "/" + urlPart;
            _add_full_url((FwList?)row["children"], urlPart);
        }
    }

    public FwDict XmlAction()
    {
        fw.route.format = "xml";
        return IndexAction();
    }
}
