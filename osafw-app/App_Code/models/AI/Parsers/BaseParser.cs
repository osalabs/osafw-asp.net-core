using System.Threading;
using System.Threading.Tasks;

namespace osafw.Parsers;

public abstract class BaseParser : IDocumentParser
{
    public abstract bool CanParse(string extension);
    public abstract Task<ParseResult> ParseAsync(string path, CancellationToken ct = default);

    protected static string PP(string value) => TextPipeline.PostProcess(value);
}
