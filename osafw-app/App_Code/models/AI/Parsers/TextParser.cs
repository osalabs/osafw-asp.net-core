using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Parsers;

public sealed class TextParser : BaseParser
{
    public override bool CanParse(string ext) => ext is ".txt" or ".md" or ".markdown" or ".rtf";

    public override async Task<ParseResult> ParseAsync(string path, CancellationToken ct = default)
    {
        string raw = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".rtf")
            raw = rtfToPlain(raw);

        string md = ext is ".md" or ".markdown" ? raw : promoteHeadings(raw);
        md = PP(md);
        var blocks = MarkdownChunker.SplitToBlocks(md).ToList();
        return new ParseResult { Markdown = md, Blocks = blocks };
    }

    private static string promoteHeadings(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (Regex.IsMatch(line, @"^[A-Z0-9][A-Z0-9 \-]{2,}$"))
                lines[i] = "# " + line;
            else if (i + 1 < lines.Length && Regex.IsMatch(lines[i + 1], @"^[-=]{3,}$"))
            {
                lines[i] = "# " + line;
                lines[i + 1] = string.Empty;
            }
        }
        return string.Join("\n", lines);
    }

    private static string rtfToPlain(string rtf)
    {
        string text = Regex.Replace(rtf ?? string.Empty, @"\\'[0-9a-fA-F]{2}", " ");
        text = Regex.Replace(text, @"\\[a-zA-Z]+\d* ?", " ");
        text = text.Replace("{", " ").Replace("}", " ");
        return Regex.Replace(text, @"\s{2,}", " ").Trim();
    }
}
