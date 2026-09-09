using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace osafw;

// A single rendering's allowlist. Only public static assets belong in this directory.
internal sealed class PdfLocalAssets
{
    internal const string Origin = "https://pdf-assets.invalid";
    internal const string DocumentUrl = Origin + "/__document__.html";
    private const long MaxAssetBytes = 10 * 1024 * 1024;
    private const long MaxTotalBytes = 50 * 1024 * 1024;
    private readonly string root;
    private long totalBytes;
    private readonly object budgetLock = new();
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".css"] = "text/css", [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif", [".webp"] = "image/webp", [".svg"] = "image/svg+xml",
        [".woff"] = "font/woff", [".woff2"] = "font/woff2", [".ttf"] = "font/ttf", [".otf"] = "font/otf"
    };

    internal PdfLocalAssets(string root)
    {
        this.root = Path.GetFullPath(root);
        if (!Directory.Exists(this.root))
            throw new DirectoryNotFoundException("The PDF asset directory does not exist.");
        rejectLinks(this.root);
    }

    internal async Task<(byte[] Body, string ContentType)> read(string url)
    {
        var uri = new Uri(url, UriKind.Absolute);
        if (uri.Scheme != "https" || uri.Host != "pdf-assets.invalid" || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
            throw new InvalidOperationException("PDF assets must use the approved local origin.");
        var relative = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
        if (relative.Contains('\\') || relative.Contains(':') || relative.Contains('\0'))
            throw new InvalidOperationException("Invalid PDF asset path.");
        var path = Path.GetFullPath(Path.Combine(root, relative));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, comparison) || !MimeTypes.TryGetValue(Path.GetExtension(path), out var contentType))
            throw new InvalidOperationException("PDF asset is outside the approved file policy.");
        rejectLinks(path);
        // Bound reads even if the file grows after metadata inspection.
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxAssetBytes)
            throw new InvalidOperationException("PDF asset is too large.");
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            lock (budgetLock)
            {
                totalBytes += read;
                if (totalBytes > MaxTotalBytes || buffer.Length + read > MaxAssetBytes)
                    throw new InvalidOperationException("PDF asset size limit exceeded.");
            }
            buffer.Write(chunk, 0, read);
        }
        return (buffer.ToArray(), contentType);
    }

    private static void rejectLinks(string path)
    {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Linked files or directories are not allowed for PDF assets.");
    }
}
