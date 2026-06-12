using osafw.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

public class DocumentEmbeddingService
{
    private const int DefaultMaxIndexChars = 200000;
    private const int DefaultMaxIndexChunks = 80;

    private readonly FW fw;
    private readonly List<IDocumentParser> parsers;

    public DocumentEmbeddingService(FW fw)
    {
        this.fw = fw ?? throw new ArgumentNullException(nameof(fw));
        parsers =
        [
            new DocxParser(),
            new HtmlParser(),
            new TextParser()
        ];
    }

    public bool IsSupported(string ext)
    {
        ext = normalizeExtension(ext);
        return parsers.Any(parser => parser.CanParse(ext));
    }

    public static IEnumerable<string> TokenAwareChunks(string text, int maxTokens = 512, int overlap = 30)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        int approxChunkSize = Math.Max(100, maxTokens * 4);
        int approxOverlap = Math.Clamp(overlap * 4, 0, approxChunkSize / 2);
        int step = Math.Max(1, approxChunkSize - approxOverlap);

        for (int i = 0; i < text.Length; i += step)
        {
            int len = Math.Min(approxChunkSize, text.Length - i);
            var chunk = text.Substring(i, len).Trim();
            if (chunk.Length > 0)
                yield return chunk;
            if (i + len >= text.Length)
                yield break;
        }
    }

    public async Task IndexKBArticleAsync(int kbId, CancellationToken cancellationToken = default)
    {
        fw.model<DocChunks>().deleteByEntity(FwEntities.ICODE_KB, kbId);

        var kb = fw.model<KBArticles>().one(kbId);
        if (kb.Count == 0 || kb["status"].toInt() != FwModel.STATUS_ACTIVE)
            return;

        int kbEntityId = fw.model<FwEntities>().idByIcodeOrAdd(FwEntities.ICODE_KB);
        int chunkIndex = 0;
        string title = kb["iname"].toStr();
        string text = (title + "\n\n" + kb["idesc"].toStr() + "\n\n" + kb["content_markdown"].toStr()).Trim();
        if (text.Length > 0)
            chunkIndex = await indexTextAsync(text, title, "KB Article", kbEntityId, kbId, chunkIndex, cancellationToken).ConfigureAwait(false);

        var atts = fw.model<Att>().listByEntity(FwEntities.ICODE_KB, kbId);
        foreach (FwDict att in atts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int attId = att["id"].toInt();
            string ext = normalizeExtension(att["ext"].toStr());
            if (!IsSupported(ext))
                continue;

            string filepath = resolveAttachmentPath(att);
            if (string.IsNullOrWhiteSpace(filepath) || !File.Exists(filepath))
                continue;

            chunkIndex = await indexFileToEntityAsync(filepath, ext, att["fname"].toStr(), kbEntityId, kbId, chunkIndex, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task IndexAttachmentToEntityAsync(int attId, string entityIcode, int itemId, bool clearExisting = true, CancellationToken cancellationToken = default)
    {
        if (attId <= 0 || itemId <= 0 || string.IsNullOrWhiteSpace(entityIcode))
            return;

        var att = fw.model<Att>().one(attId);
        if (att.Count == 0)
            return;

        string ext = normalizeExtension(att["ext"].toStr());
        if (!IsSupported(ext))
            return;

        string filepath = resolveAttachmentPath(att);
        if (string.IsNullOrWhiteSpace(filepath) || !File.Exists(filepath))
            return;

        if (clearExisting)
            fw.model<DocChunks>().deleteByEntity(entityIcode, itemId);

        int fwentitiesId = fw.model<FwEntities>().idByIcodeOrAdd(entityIcode);
        await indexFileToEntityAsync(filepath, ext, att["fname"].toStr(att["iname"].toStr()), fwentitiesId, itemId, 0, cancellationToken).ConfigureAwait(false);
    }

    public void DeleteKBArticleEmbeddings(int kbId)
    {
        fw.model<DocChunks>().deleteByEntity(FwEntities.ICODE_KB, kbId);
    }

    private async Task<int> indexFileToEntityAsync(string filepath, string ext, string filename, int fwentitiesId, int itemId, int chunkIndex, CancellationToken cancellationToken)
    {
        var parser = parsers.FirstOrDefault(p => p.CanParse(ext));
        if (parser == null)
            return chunkIndex;

        var parsed = await parser.ParseAsync(filepath, cancellationToken).ConfigureAwait(false);
        var blocks = parsed.Blocks.Count > 0 ? parsed.Blocks : MarkdownChunker.SplitToBlocks(parsed.Markdown).ToList();
        foreach (var block in limitBlocks(blocks))
            chunkIndex = await indexBlockAsync(block, filename, fwentitiesId, itemId, chunkIndex, cancellationToken).ConfigureAwait(false);

        return chunkIndex;
    }

    private async Task<int> indexTextAsync(string text, string filename, string section, int fwentitiesId, int itemId, int chunkIndex, CancellationToken cancellationToken)
    {
        var block = new RawBlock(text, 0, section);
        return await indexBlockAsync(block, filename, fwentitiesId, itemId, chunkIndex, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> indexBlockAsync(RawBlock block, string filename, int fwentitiesId, int itemId, int chunkIndex, CancellationToken cancellationToken)
    {
        var embeddingModel = LLM.MODEL_TEXT_EMBEDDING_3_SMALL;

        int maxChunks = maxIndexChunks();
        foreach (var chunk in TokenAwareChunks(block.Text))
        {
            if (chunkIndex >= maxChunks)
                return chunkIndex;

            cancellationToken.ThrowIfCancellationRequested();
            var embedding = await fw.model<LLM>().embeddingForTextAsync(chunk, embeddingModel, cancellationToken).ConfigureAwait(false);
            fw.model<DocChunks>().addEmbedding(new DocChunks.ChunkEmbedding
            {
                FwEntitiesId = fwentitiesId,
                ItemId = itemId,
                Filename = filename ?? string.Empty,
                Page = block.Page,
                Section = block.Section ?? string.Empty,
                ChunkIndex = chunkIndex++,
                Text = chunk,
                Embedding = embedding,
                EmbeddingModel = embeddingModel
            });
        }

        return chunkIndex;
    }

    private IEnumerable<RawBlock> limitBlocks(IEnumerable<RawBlock> blocks)
    {
        int maxChars = maxIndexChars();
        int usedChars = 0;
        foreach (var block in blocks)
        {
            if (usedChars >= maxChars)
                yield break;

            string text = block.Text ?? string.Empty;
            int remaining = maxChars - usedChars;
            if (text.Length > remaining)
                text = text[..remaining];
            usedChars += text.Length;

            if (!string.IsNullOrWhiteSpace(text))
                yield return block with { Text = text };
        }
    }

    private int maxIndexChars()
    {
        return Math.Max(1000, fw.model<Settings>().readInt("ASSISTANT_MAX_INDEX_CHARS", DefaultMaxIndexChars));
    }

    private int maxIndexChunks()
    {
        return Math.Max(1, fw.model<Settings>().readInt("ASSISTANT_MAX_INDEX_CHUNKS", DefaultMaxIndexChunks));
    }

    private string resolveAttachmentPath(FwDict att)
    {
        var attModel = fw.model<Att>();
        int attId = att["id"].toInt();
        string ext = normalizeExtension(att["ext"].toStr()).TrimStart('.');
        string filepath = attModel.getUploadImgPath(attId, "", ext);
        if (File.Exists(filepath))
            return filepath;

        if (att["is_s3"].toBool())
        {
            string downloaded = attModel.downloadFromS3(attId);
            if (File.Exists(downloaded))
                return downloaded;
        }

        return string.Empty;
    }

    private static string normalizeExtension(string ext)
    {
        ext = (ext ?? string.Empty).Trim().ToLowerInvariant();
        if (ext.Length > 0 && !ext.StartsWith('.'))
            ext = "." + ext;
        return ext;
    }
}
