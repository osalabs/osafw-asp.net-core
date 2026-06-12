using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace osafw.Parsers;

public static class MarkdownChunker
{
    private static readonly Regex HeadingRx = new(@"^#{1,6}\s+(.+)$", RegexOptions.Multiline);

    public static IEnumerable<RawBlock> SplitToBlocks(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            yield break;

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var crumbs = new List<string>();
        var buffer = new List<string>();

        IEnumerable<RawBlock> Flush()
        {
            if (buffer.Count == 0)
                yield break;

            var text = string.Join("\n", buffer).Trim();
            buffer.Clear();

            if (text.Length > 0)
                yield return new RawBlock(text, 0, string.Join(" > ", crumbs));
        }

        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^(#+)\s+(.+)$");
            if (match.Success)
            {
                foreach (var block in Flush())
                    yield return block;

                int level = match.Groups[1].Value.Length;
                string title = match.Groups[2].Value.Trim();
                if (crumbs.Count >= level)
                    crumbs.RemoveRange(level - 1, crumbs.Count - (level - 1));
                crumbs.Add(title);
            }

            buffer.Add(line);
        }

        foreach (var block in Flush())
            yield return block;
    }

    public static string FirstTitle(string markdown)
    {
        var match = HeadingRx.Match(markdown ?? string.Empty);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}
