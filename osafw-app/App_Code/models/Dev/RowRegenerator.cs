#if isRowRegeneration
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace osafw;

/// <summary>Replaces model Row classes from current schema metadata; review and revert changes in Git.</summary>
internal static class DevRowRegenerator
{
    private static readonly object Sync = new();
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static FwDict regenerate(FW fw)
    {
        var siteRoot = fw.config("site_root").toStr();
        if (string.IsNullOrWhiteSpace(siteRoot))
            throw new UserException("The application source root is unavailable.");
        var modelsRoot = Path.GetFullPath(Path.Combine(siteRoot, "App_Code", "models"));
        if (!Directory.Exists(modelsRoot))
            throw new UserException("The application model source directory is unavailable.");
        for (var directory = new DirectoryInfo(modelsRoot); directory != null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UserException("Model source regeneration does not follow linked directories.");

        StrList updated = [], unchanged = [], failed = [];
        FwList skipped = [];
        var models = DevEntityBuilder.listModels().ToHashSet(StringComparer.Ordinal);
        lock (Sync)
        {
            var sources = Directory.EnumerateFiles(modelsRoot, "*.cs", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = false,
            }).GroupBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal);
            foreach (var group in sources)
            {
                var name = group.Key!;
                if (!models.Contains(name)) continue;
                try
                {
                    if (group.Count() != 1)
                        throw new UserException("More than one source file matches the model name.");
                    if (regenerateFile(fw, name, group.Single())) updated.Add(name);
                    else unchanged.Add(name);
                }
                catch (UserException ex)
                {
                    skipped.Add(new FwDict { ["model"] = name, ["reason"] = ex.Message });
                }
                catch (Exception ex)
                {
                    failed.Add(name);
                    fw.logger(LogLevel.ERROR, "Row regeneration failed for", name, ex);
                }
            }
        }
        return new FwDict { ["success"] = failed.Count == 0, ["updated"] = updated, ["unchanged"] = unchanged, ["skipped"] = skipped, ["failed"] = failed };
    }

    private static bool regenerateFile(FW fw, string modelName, string path)
    {
        // Hold the file while reading metadata and writing so another request/editor cannot overwrite our input.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var source = reader.ReadToEnd();
        if (source.StartsWith('\uFEFF'))
            throw new UserException("Convert the source to UTF-8 without BOM before regeneration.");
        var tree = CSharpSyntaxTree.ParseText(source);
        if (tree.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new UserException("The source has C# parse errors.");
        var models = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(model => model.Identifier.ValueText == modelName).ToList();
        if (models.Count != 1)
            throw new UserException("The source must contain one matching model class.");
        var rows = models[0].Members.OfType<ClassDeclarationSyntax>().Where(row => row.Identifier.ValueText == "Row").ToList();
        if (rows.Count != 1)
            throw new UserException("The model must contain one direct nested Row class.");
        var row = rows[0];
        if (row.Modifiers.Any(SyntaxKind.PartialKeyword) || row.TypeParameterList != null
            || row.DescendantTrivia(descendIntoTrivia: true).Any(trivia => trivia.IsDirective))
            throw new UserException("Partial, generic or conditional Row classes require manual regeneration.");

        var model = fw.model(modelName);
        if (string.IsNullOrWhiteSpace(model.table_name))
            throw new UserException("The model does not define a database table.");
        var fields = DevEntityBuilder.tableschema2fields(model.getDB().reloadTableSchemaFull(model.table_name));
        var generated = DevCodeGen.buildRowClass(new FwDict { ["fields"] = fields });
        if (generated.Length == 0)
            throw new UserException("The database returned no columns for this model.");

        var start = tree.GetText().Lines.GetLineFromPosition(row.SpanStart).Start;
        var indentation = source[start..row.SpanStart];
        if (indentation.Any(ch => ch != ' ' && ch != '\t'))
        {
            start = row.SpanStart;
            indentation = "";
        }
        var lines = generated.TrimEnd('\r', '\n').ReplaceLineEndings("\n").Split('\n');
        var replacement = string.Join("\r\n", lines.Select(line => indentation + (line.StartsWith("    ", StringComparison.Ordinal) ? line[4..] : line)));
        var output = (source[..start] + replacement + source[row.Span.End..]).ReplaceLineEndings("\r\n");
        if (output == source) return false;

        try { write(stream, output); }
        catch
        {
            write(stream, source);
            throw;
        }
        return true;
    }

    private static void write(FileStream stream, string source)
    {
        stream.Position = 0;
        var bytes = Utf8.GetBytes(source);
        stream.Write(bytes);
        stream.SetLength(bytes.Length);
        stream.Flush(flushToDisk: true);
    }
}
#endif
