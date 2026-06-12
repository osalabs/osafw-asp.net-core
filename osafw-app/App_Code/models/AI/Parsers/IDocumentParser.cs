using System.Threading;
using System.Threading.Tasks;

namespace osafw.Parsers;

public interface IDocumentParser
{
    bool CanParse(string extension);
    Task<ParseResult> ParseAsync(string path, CancellationToken ct = default);
}
