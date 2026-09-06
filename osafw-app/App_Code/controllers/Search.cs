using System;
using System.Linq;

namespace osafw;

/// <summary>Small-site text search over current authorized publications. No external index or background task is required.</summary>
public class SearchController : FwController
{
    public override void init(FW fw)
    {
        base.init(fw);
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_PUBLIC");
    }

    public override void checkAccess() { }

    public FwDict IndexAction()
    {
        var pages = fw.model<Spages>();
        if (!pages.isEnabled()) throw new NotFoundException();
        fw.cache_control = "private, no-store";
        fw.response.Headers.CacheControl = fw.cache_control;
        fw.response.Headers["X-Robots-Tag"] = "noindex";
        string search = reqs("s").Trim();
        if (search.Length > 200) search = search[..200];
        int number = Math.Max(0, reqi("pagenum"));
        const int size = 15;
        var matches = new FwList();
        var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length > 0)
            foreach (var page in pages.publishedPages().Where(x => !x["noindex"].toBool() && x["redirect_url"].toStr().Length == 0))
            {
                var text = pages.publishedText(page);
                if (!terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))) continue;
                int start = Math.Max(0, text.IndexOf(terms[0], StringComparison.OrdinalIgnoreCase) - 60);
                var result = new FwDict(page);
                result["excerpt"] = (start > 0 ? "…" : "") + text.Substring(start, Math.Min(240, text.Length - start)) + (text.Length - start > 240 ? "…" : "");
                matches.Add(result);
            }
        return DB.h("hide_sidebar", true, "s", search, "results", new FwList(matches.Skip(number * size).Take(size)), "count", matches.Count, "searched", search.Length > 0,
            "prev_url", number > 0 ? "?s=" + Uri.EscapeDataString(search) + "&pagenum=" + (number - 1) : "", "next_url", (number + 1) * size < matches.Count ? "?s=" + Uri.EscapeDataString(search) + "&pagenum=" + (number + 1) : "");
    }
}
