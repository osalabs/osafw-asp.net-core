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

    public override void checkAccess()
    {
    }

    public FwDict IndexAction()
    {
        var pages = fw.model<Spages>();
        fw.cache_control = "private, no-store";
        fw.response.Headers.CacheControl = fw.cache_control;
        fw.response.Headers["X-Robots-Tag"] = "noindex";
        string search = reqs("s").Trim();
        if (search.Length > 200)
        {
            search = search[..200];
        }

        int number = Math.Max(0, reqi("pagenum"));
        const int PAGE_SIZE = 15;
        var matches = new FwList();
        var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length > 0)
        {
            foreach (var page in pages.listIndexable())
            {
                var text = pages.publishedText(page);
                if (!terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                int start = Math.Max(0, text.IndexOf(terms[0], StringComparison.OrdinalIgnoreCase) - 60);
                var result = new FwDict(page);
                result["excerpt"] = (start > 0 ? "…" : "") + text.Substring(start, Math.Min(240, text.Length - start)) + (text.Length - start > 240 ? "…" : "");
                matches.Add(result);
            }
        }

        string filterUrl = "?s=" + Uri.EscapeDataString(search) + "&pagenum=";
        return new FwDict
        {
            ["hide_sidebar"] = true,
            ["s"] = search,
            ["results"] = new FwList(matches.Skip(number * PAGE_SIZE).Take(PAGE_SIZE)),
            ["count"] = matches.Count,
            ["is_searched"] = search.Length > 0,
            ["prev_url"] = number > 0 ? filterUrl + (number - 1) : "",
            ["next_url"] = (number + 1) * PAGE_SIZE < matches.Count ? filterUrl + (number + 1) : ""
        };
    }
}
