using System;
using System.Collections.Generic;

namespace osafw.Parsers;

public sealed record RawBlock(string Text, int Page = 0, string Section = "");

public sealed class ParseResult
{
    public string Markdown { get; init; } = string.Empty;
    public IReadOnlyList<RawBlock> Blocks { get; init; } = Array.Empty<RawBlock>();
}
