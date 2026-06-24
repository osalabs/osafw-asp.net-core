using DocumentFormat.OpenXml.Packaging;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace osafw.Parsers;

public sealed class DocxParser : BaseParser
{
    public override bool CanParse(string ext) => ext == ".docx";

    public override Task<ParseResult> ParseAsync(string path, CancellationToken ct = default)
    {
        string md = PP(docxToMarkdown(path));
        var blocks = MarkdownChunker.SplitToBlocks(md).ToList();
        return Task.FromResult(new ParseResult { Markdown = md, Blocks = blocks });
    }

    private static string docxToMarkdown(string path)
    {
        var sb = new StringBuilder();
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null)
            return string.Empty;

        foreach (var paragraph in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            int level = headingLevel(paragraph);
            string text = string.Concat(paragraph.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text)).Trim();
            if (text.Length == 0)
                continue;

            if (level > 0)
                sb.AppendLine(new string('#', level) + " " + text);
            else
                sb.AppendLine(text);
        }

        foreach (var table in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>())
        {
            var rows = table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>()
                .Select(r => r.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>()
                    .Select(c => c.InnerText.Trim().Replace("\r", " ").Replace("\n", " "))
                    .ToArray())
                .Where(r => r.Length > 0)
                .ToList();
            if (rows.Count == 0)
                continue;

            sb.AppendLine("| " + string.Join(" | ", rows[0]) + " |");
            sb.AppendLine("| " + string.Join(" | ", rows[0].Select(_ => "---")) + " |");
            foreach (var row in rows.Skip(1))
                sb.AppendLine("| " + string.Join(" | ", row) + " |");
        }

        return sb.ToString();
    }

    private static int headingLevel(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
    {
        var value = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
        if (value.StartsWith("Heading") && int.TryParse(value.Replace("Heading", ""), out int level))
            return level;
        return 0;
    }
}
