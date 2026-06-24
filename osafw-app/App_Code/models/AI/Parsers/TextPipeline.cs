using System;
using System.Text;
using System.Text.RegularExpressions;

namespace osafw.Parsers;

public static class TextPipeline
{
    public static string PostProcess(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string clean = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        clean = Regex.Replace(clean, @"[ \t]{2,}", " ");
        clean = Regex.Replace(clean, @"\n{3,}", "\n\n");
        clean = Regex.Replace(clean, @"^[\s\u2022\-\*]+", "* ", RegexOptions.Multiline);
        return wrapLongLines(clean.Trim(), 120);
    }

    private static string wrapLongLines(string text, int maxWidth)
    {
        if (maxWidth <= 0 || string.IsNullOrEmpty(text))
            return text;

        var sb = new StringBuilder(text.Length);
        foreach (var line in text.Split('\n'))
        {
            int start = 0;
            while (start < line.Length)
            {
                int len = Math.Min(maxWidth, line.Length - start);
                int end = start + len;
                if (end < line.Length)
                {
                    int lastSpace = line.LastIndexOfAny([' ', '\t'], end - 1, len);
                    if (lastSpace > start)
                        end = lastSpace;
                }

                sb.Append(line, start, end - start).Append(Environment.NewLine);
                start = end;
                while (start < line.Length && char.IsWhiteSpace(line[start]))
                    start++;
            }

            if (line.Length == 0)
                sb.AppendLine();
        }

        return sb.ToString().Trim();
    }
}
