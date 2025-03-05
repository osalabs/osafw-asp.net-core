﻿// Sitemap controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class SitemapController : FwController
{
    protected Spages model;

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

    public Hashtable IndexAction()
    {
        Hashtable ps = [];

        Hashtable item = model.oneByFullUrl(base_url);

        ArrayList pages_tree = model.tree(" status=0 ", [], "parent_id, prio desc, iname");
        _add_full_url(pages_tree);

        ps["page"] = item;
        ps["pages_tree"] = pages_tree;
        ps["hide_sidebar"] = true; // TODO - control via item[template]
        return ps;
    }

    private void _add_full_url(ArrayList pages_tree, string parent_url = "")
    {
        if (pages_tree==null)
            return;

        foreach (Hashtable row in pages_tree)
        {
            row["full_url"] = parent_url + "/" + row["url"];
            _add_full_url((ArrayList)row["children"], (string)row["url"]);
        }
    }
}