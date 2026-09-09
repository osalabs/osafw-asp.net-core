using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace osafw;

/// <summary>
/// Plans and applies schema-driven replacements of safely recognizable generated model Row classes.
/// </summary>
internal sealed class DevRowRegenerator
{
    private const int MaxSelectedModels = 50;
    private const long MaxSourceBytes = 2 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly object ApplySync = new();
    private static readonly HashSet<string> GeneratedRowTypes = new(StringComparer.Ordinal)
    {
        "int", "int?", "long", "long?", "ulong", "ulong?", "bool", "bool?",
        "decimal", "decimal?", "double", "double?", "DateTime", "DateTime?",
        "DateTimeOffset", "DateTimeOffset?", "string", "string?",
    };
    private readonly FW fw;
    private readonly string siteRoot;
    private readonly string modelsRoot;
    private readonly string modelsRootPrefix;
    private readonly StringComparison pathComparison;

    internal DevRowRegenerator(FW fw)
    {
        this.fw = fw ?? throw new ArgumentNullException(nameof(fw));
        var configuredSiteRoot = fw.config("site_root").toStr().Trim();
        if (configuredSiteRoot.Length == 0)
            throw new UserException("The application source root is unavailable.");
        siteRoot = Path.GetFullPath(configuredSiteRoot);
        modelsRoot = Path.GetFullPath(Path.Combine(siteRoot, "App_Code", "models"));
        modelsRootPrefix = modelsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    /// <summary>
    /// Lists compiled models with one ordinary, non-reparse source file whose file name matches the model class.
    /// </summary>
    internal FwList listCandidates()
    {
        var result = new FwList();
        foreach (var candidate in discoverCandidates().Values.OrderBy(x => x.ModelName, StringComparer.Ordinal))
        {
            result.Add(new FwDict
            {
                ["model_name"] = candidate.ModelName,
                ["source_file"] = candidate.RelativePath,
            });
        }

        return result;
    }

    /// <summary>
    /// Builds reviewable source diffs without writing files.
    /// </summary>
    internal FwList preview(IEnumerable<string> selectedModelNames)
    {
        var plans = planSelected(selectedModelNames);
        var result = new FwList(plans.Count);
        foreach (var plan in plans)
            result.Add(plan.toFwDict());
        return result;
    }

    /// <summary>
    /// Replans every selected model, rejects stale preview hashes, then holds every source exclusively through write and flush.
    /// </summary>
    /// <remarks>
    /// Writes occur in place while all selected streams are locked. A process or operating-system failure during a write can
    /// still leave partial source that must be restored from version control.
    /// </remarks>
    internal int apply(IEnumerable<string> selectedModelNames, IReadOnlyDictionary<string, string> previewHashes)
    {
        ArgumentNullException.ThrowIfNull(previewHashes);
        var plans = planSelected(selectedModelNames);
        foreach (var plan in plans)
        {
            if (!plan.IsChanged)
                throw new UserException($"{plan.ModelName} no longer needs regeneration. Preview and review the selection again.");
            if (!previewHashes.TryGetValue(plan.ModelName, out var suppliedHash)
                || suppliedHash == null
                || suppliedHash.Length != plan.PreviewHash.Length
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(plan.PreviewHash),
                    Encoding.ASCII.GetBytes(suppliedHash)))
            {
                throw new UserException($"The preview for {plan.ModelName} is stale. Preview and review the selection again.");
            }
        }

        lock (ApplySync)
            return applyWithExclusiveFiles(plans);
    }

    private int applyWithExclusiveFiles(List<RowPlan> plans)
    {
        var lockedFiles = new List<LockedSource>(plans.Count);
        try
        {
            ensureSafeRootChain();
            foreach (var plan in plans)
            {
                ensureSafeRegularFile(plan.SourcePath);
                try
                {
                    var stream = new FileStream(plan.SourcePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    try
                    {
                        lockedFiles.Add(new LockedSource(plan, stream, readStrictSource(stream, plan.SourcePath)));
                    }
                    catch
                    {
                        stream.Dispose();
                        throw;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new UserException($"{plan.ModelName} could not be locked for regeneration. Close editors or other source writers and try again.");
                }
            }

            // Every selected file is locked before validation or the first write.
            foreach (var locked in lockedFiles)
            {
                var currentSource = StrictUtf8.GetString(locked.OriginalBytes);
                if (!string.Equals(hashSource(currentSource), locked.Plan.SourceHash, StringComparison.Ordinal))
                    throw new UserException($"{locked.Plan.ModelName} changed after preview validation. No model source was updated.");
            }

            var written = new List<LockedSource>(lockedFiles.Count);
            try
            {
                foreach (var locked in lockedFiles)
                {
                    written.Add(locked);
                    writeLockedSource(locked.Stream, StrictUtf8.GetBytes(locked.Plan.UpdatedSource));
                }
            }
            catch (Exception applyError)
            {
                var rollbackErrors = new List<Exception>();
                foreach (var locked in written.AsEnumerable().Reverse())
                {
                    try
                    {
                        writeLockedSource(locked.Stream, locked.OriginalBytes);
                    }
                    catch (Exception rollbackError)
                    {
                        rollbackErrors.Add(rollbackError);
                    }
                }

                if (rollbackErrors.Count > 0)
                {
                    rollbackErrors.Insert(0, applyError);
                    throw new AggregateException("Typed Row regeneration failed and one or more locked source files could not be restored.", rollbackErrors);
                }

                throw;
            }

            return written.Count;
        }
        finally
        {
            foreach (var locked in lockedFiles)
                locked.Stream.Dispose();
        }
    }

    private List<RowPlan> planSelected(IEnumerable<string> selectedModelNames)
    {
        ArgumentNullException.ThrowIfNull(selectedModelNames);
        var selected = selectedModelNames
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        if (selected.Count == 0)
            throw new UserException("Select at least one model source file.");
        if (selected.Count > MaxSelectedModels)
            throw new UserException($"Select no more than {MaxSelectedModels} model source files at once.");

        var candidates = discoverCandidates();
        var plans = new List<RowPlan>(selected.Count);
        foreach (var modelName in selected)
        {
            if (!candidates.TryGetValue(modelName, out var candidate))
                throw new UserException($"{modelName} is not an available model source file.");
            plans.Add(plan(candidate));
        }

        return plans;
    }

    private RowPlan plan(ModelSource candidate)
    {
        ensureSafeRegularFile(candidate.SourcePath);
        var source = readStrictSource(candidate.SourcePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse, SourceCodeKind.Regular));
        var errors = syntaxTree.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).Take(3).ToList();
        if (errors.Count > 0)
            throw new UserException($"{candidate.ModelName} has C# parse errors and cannot be regenerated safely.");

        var root = syntaxTree.GetRoot();
        var modelClasses = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(x => string.Equals(x.Identifier.ValueText, candidate.ModelName, StringComparison.Ordinal))
            .ToList();
        if (modelClasses.Count != 1)
            throw new UserException($"{candidate.ModelName} must contain exactly one matching model class.");

        var rowClasses = modelClasses[0].Members
            .OfType<ClassDeclarationSyntax>()
            .Where(x => string.Equals(x.Identifier.ValueText, "Row", StringComparison.Ordinal))
            .ToList();
        if (rowClasses.Count != 1 || !isRecognizableGeneratedRow(rowClasses[0]))
        {
            throw new UserException(
                $"{candidate.ModelName}.Row is missing, ambiguous, or customized. Regenerate it manually after reviewing its custom members.");
        }

        var model = fw.model(candidate.ModelName);
        var tableName = model.table_name.Trim();
        if (tableName.Length == 0)
            throw new UserException($"{candidate.ModelName} does not define a database table.");

        var schema = model.getDB().reloadTableSchemaFull(tableName);
        var entity = new FwDict
        {
            ["model_name"] = candidate.ModelName,
            ["table"] = tableName,
            ["fields"] = DevEntityBuilder.tableschema2fields(schema),
        };
        var generatedRow = DevCodeGen.buildRowClass(entity);
        if (generatedRow.Length == 0)
            throw new UserException($"No typed Row properties could be generated for {candidate.ModelName}.");

        var rowClass = rowClasses[0];
        var rowLine = syntaxTree.GetText().Lines.GetLineFromPosition(rowClass.SpanStart);
        var replacementStart = rowLine.Start;
        var prefix = source[replacementStart..rowClass.SpanStart];
        var indentation = prefix;
        if (prefix.Any(ch => ch != ' ' && ch != '\t'))
        {
            replacementStart = rowClass.SpanStart;
            indentation = string.Empty;
        }

        var newline = detectNewline(source);
        var replacement = reindentGeneratedRow(generatedRow.TrimEnd('\r', '\n'), indentation, newline);
        var existingRow = source[replacementStart..rowClass.Span.End];
        var updatedSource = source[..replacementStart] + replacement + source[rowClass.Span.End..];
        var sourceHash = hashSource(source);
        var previewHash = hashPreview(candidate.ModelName, source, updatedSource);

        return new RowPlan(
            candidate.ModelName,
            candidate.SourcePath,
            candidate.RelativePath,
            updatedSource,
            sourceHash,
            previewHash,
            !string.Equals(source, updatedSource, StringComparison.Ordinal),
            buildDiff(candidate.RelativePath, existingRow, replacement, newline));
    }

    private Dictionary<string, ModelSource> discoverCandidates()
    {
        if (!Directory.Exists(modelsRoot))
            throw new UserException("The application model source directory is unavailable.");
        ensureSafeRootChain();

        var compiledModels = DevEntityBuilder.listModels().ToHashSet(StringComparer.Ordinal);
        var matches = new Dictionary<string, List<ModelSource>>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(modelsRoot);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            ensureInsideModelsRoot(directory, allowRoot: true);
            ensureNoReparsePoint(directory);

            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                ensureInsideModelsRoot(childDirectory);
                if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) == 0)
                    pending.Push(childDirectory);
            }

            foreach (var sourcePath in Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly))
            {
                ensureSafeRegularFile(sourcePath);
                var modelName = Path.GetFileNameWithoutExtension(sourcePath);
                if (!compiledModels.Contains(modelName))
                    continue;

                var relative = Path.GetRelativePath(modelsRoot, sourcePath).Replace(Path.DirectorySeparatorChar, '/');
                if (!matches.TryGetValue(modelName, out var sources))
                {
                    sources = [];
                    matches[modelName] = sources;
                }
                sources.Add(new ModelSource(modelName, Path.GetFullPath(sourcePath), relative));
            }
        }

        return matches
            .Where(x => x.Value.Count == 1)
            .ToDictionary(x => x.Key, x => x.Value[0], StringComparer.Ordinal);
    }

    private static bool isRecognizableGeneratedRow(ClassDeclarationSyntax row)
    {
        if (row.AttributeLists.Count != 0
            || row.TypeParameterList != null
            || row.ParameterList != null
            || row.BaseList != null
            || row.ConstraintClauses.Count != 0
            || row.Modifiers.Count != 1
            || !row.Modifiers[0].IsKind(SyntaxKind.PublicKeyword)
            || row.OpenBraceToken.IsMissing
            || row.CloseBraceToken.IsMissing
            || row.DescendantTrivia(descendIntoTrivia: true).Any(trivia =>
                trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsDirective
                || trivia.IsKind(SyntaxKind.SkippedTokensTrivia)))
            return false;

        foreach (var member in row.Members)
        {
            if (member is not PropertyDeclarationSyntax property
                || property.Modifiers.Count != 1
                || !property.Modifiers[0].IsKind(SyntaxKind.PublicKeyword)
                || property.ExplicitInterfaceSpecifier != null
                || property.ExpressionBody != null
                || !GeneratedRowTypes.Contains(property.Type.ToString())
                || !isGeneratedPropertyInitializer(property)
                || property.AccessorList == null
                || property.AccessorList.Accessors.Count != 2
                || property.AccessorList.Accessors[0].Kind() != SyntaxKind.GetAccessorDeclaration
                || property.AccessorList.Accessors[1].Kind() != SyntaxKind.SetAccessorDeclaration)
                return false;

            foreach (var accessor in property.AccessorList.Accessors)
            {
                if (accessor.AttributeLists.Count != 0
                    || accessor.Modifiers.Count != 0
                    || accessor.Body != null
                    || accessor.ExpressionBody != null
                    || accessor.SemicolonToken.IsMissing)
                    return false;
            }

            foreach (var list in property.AttributeLists)
            {
                foreach (var attribute in list.Attributes)
                {
                    if (attribute.Name is not IdentifierNameSyntax name
                        || (name.Identifier.ValueText != "DBName" && name.Identifier.ValueText != "DBNameAttribute"))
                        return false;
                }
            }
        }

        return row.Members.Count > 0;
    }

    private static bool isGeneratedPropertyInitializer(PropertyDeclarationSyntax property)
    {
        if (property.Initializer == null)
            return true;
        if (!string.Equals(property.Type.ToString(), "string", StringComparison.Ordinal))
            return false;

        var value = property.Initializer.Value;
        if (value is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            return literal.Token.ValueText.Length == 0;

        return value is MemberAccessExpressionSyntax member
            && member.Expression is PredefinedTypeSyntax predefined
            && predefined.Keyword.IsKind(SyntaxKind.StringKeyword)
            && member.Name.Identifier.ValueText == "Empty";
    }

    private string readStrictSource(string sourcePath)
    {
        ensureSafeRegularFile(sourcePath);
        var info = new FileInfo(sourcePath);
        if (info.Length > MaxSourceBytes)
            throw new UserException($"{Path.GetFileName(sourcePath)} is too large for browser regeneration.");

        var bytes = File.ReadAllBytes(sourcePath);
        if (bytes.AsSpan().StartsWith(Utf8Bom))
            throw new UserException($"{Path.GetFileName(sourcePath)} must be UTF-8 without a byte-order mark before regeneration.");

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new UserException($"{Path.GetFileName(sourcePath)} is not valid UTF-8 source.");
        }
    }

    private static byte[] readStrictSource(FileStream stream, string sourcePath)
    {
        if (stream.Length > MaxSourceBytes)
            throw new UserException($"{Path.GetFileName(sourcePath)} is too large for browser regeneration.");

        stream.Position = 0;
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (bytes.AsSpan().StartsWith(Utf8Bom))
            throw new UserException($"{Path.GetFileName(sourcePath)} must be UTF-8 without a byte-order mark before regeneration.");

        try
        {
            _ = StrictUtf8.GetString(bytes);
            return bytes;
        }
        catch (DecoderFallbackException)
        {
            throw new UserException($"{Path.GetFileName(sourcePath)} is not valid UTF-8 source.");
        }
    }

    private void ensureSafeRegularFile(string sourcePath)
    {
        ensureInsideModelsRoot(sourcePath);
        if (!File.Exists(sourcePath))
            throw new UserException("A selected model source file is unavailable.");
        ensureNoReparsePoint(sourcePath);
    }

    private void ensureInsideModelsRoot(string path, bool allowRoot = false)
    {
        var fullPath = Path.GetFullPath(path);
        if ((allowRoot && string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), modelsRoot.TrimEnd(Path.DirectorySeparatorChar), pathComparison))
            || fullPath.StartsWith(modelsRootPrefix, pathComparison))
            return;
        throw new UserException("A model source path resolved outside the application model directory.");
    }

    private static void ensureNoReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new UserException("Model source regeneration does not follow reparse points.");
    }

    private void ensureSafeRootChain()
    {
        var current = new DirectoryInfo(modelsRoot);
        while (current != null)
        {
            ensureNoReparsePoint(current.FullName);
            if (string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar), siteRoot.TrimEnd(Path.DirectorySeparatorChar), pathComparison))
                return;
            current = current.Parent;
        }

        throw new UserException("The model source directory is outside the application source root.");
    }

    private static void writeLockedSource(FileStream stream, byte[] source)
    {
        stream.Position = 0;
        stream.SetLength(0);
        stream.Write(source);
        stream.Flush(flushToDisk: true);
    }

    private static string hashSource(string source)
    {
        return Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(source)));
    }

    private static string hashPreview(string modelName, string source, string updatedSource)
    {
        var payload = modelName + "\0" + source + "\0" + updatedSource;
        return Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(payload)));
    }

    private static string detectNewline(string source)
    {
        var newlineIndex = source.IndexOf('\n');
        return newlineIndex > 0 && source[newlineIndex - 1] == '\r' ? "\r\n" : "\n";
    }

    private static string normalizeNewlines(string value, string newline)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", newline, StringComparison.Ordinal);
    }

    private static string reindentGeneratedRow(string value, string indentation, string newline)
    {
        var lines = normalizeNewlines(value, "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            lines[index] = indentation + (line.StartsWith("    ", StringComparison.Ordinal) ? line[4..] : line);
        }

        return string.Join(newline, lines);
    }

    private static string buildDiff(string relativePath, string existingRow, string replacement, string newline)
    {
        var sb = new StringBuilder();
        sb.Append("--- ").Append(relativePath).Append(newline);
        sb.Append("+++ ").Append(relativePath).Append(newline);
        sb.Append("@@ typed Row @@").Append(newline);
        appendDiffLines(sb, existingRow, '-', newline);
        appendDiffLines(sb, replacement, '+', newline);
        return sb.ToString();
    }

    private static void appendDiffLines(StringBuilder sb, string value, char prefix, string newline)
    {
        foreach (var line in normalizeNewlines(value, "\n").Split('\n'))
            sb.Append(prefix).Append(line).Append(newline);
    }

    private sealed record ModelSource(string ModelName, string SourcePath, string RelativePath);

    private sealed record LockedSource(RowPlan Plan, FileStream Stream, byte[] OriginalBytes);

    private sealed record RowPlan(
        string ModelName,
        string SourcePath,
        string RelativePath,
        string UpdatedSource,
        string SourceHash,
        string PreviewHash,
        bool IsChanged,
        string Diff)
    {
        internal FwDict toFwDict()
        {
            return new FwDict
            {
                ["model_name"] = ModelName,
                ["source_file"] = RelativePath,
                ["preview_hash"] = PreviewHash,
                ["is_changed"] = IsChanged,
                ["diff"] = Diff,
            };
        }
    }
}
