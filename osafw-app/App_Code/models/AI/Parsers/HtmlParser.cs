using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Parsers;

public sealed class HtmlParser : BaseParser
{
    public override bool CanParse(string ext) => ext is ".htm" or ".html";

    public override async Task<ParseResult> ParseAsync(string path, CancellationToken ct = default)
    {
        string html = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        html = Regex.Replace(html, @"<(script|style|noscript|template|svg|canvas)[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</h([1-6])>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<h([1-6])[^>]*>", m => "\n" + new string('#', int.Parse(m.Groups[1].Value)) + " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(p|div|section|article|br|li|tr)[^>]*>", "\n", RegexOptions.IgnoreCase);
        string text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " "));
        string md = PP(text);
        return new ParseResult { Markdown = md, Blocks = MarkdownChunker.SplitToBlocks(md).ToList() };
    }
}
