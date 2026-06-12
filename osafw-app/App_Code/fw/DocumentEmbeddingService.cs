using osafw.Parsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
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

    public async Task<bool> ProcessNextQueuedSourceAsync(string workerId = "", CancellationToken cancellationToken = default)
    {
        var source = fw.model<RagSources>().claimNextPending(workerId);
        if (source == null || source.id <= 0)
            return false;

        await IndexSourceAsync(source.id, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task IndexSourceAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        var source = fw.model<RagSources>().oneTyped(sourceId);
        if (source == null || source.id <= 0)
            return;

        try
        {
            var chunks = source.source_type switch
            {
                RagSources.SOURCE_TYPE_KB_ARTICLE => await buildKBArticleSourceChunksAsync(source, cancellationToken).ConfigureAwait(false),
                RagSources.SOURCE_TYPE_KB_ATTACHMENT => await buildAttachmentSourceChunksAsync(source, cancellationToken).ConfigureAwait(false),
                RagSources.SOURCE_TYPE_SPAGE => await buildSpageSourceChunksAsync(source, cancellationToken).ConfigureAwait(false),
                RagSources.SOURCE_TYPE_ASSISTANT_UPLOAD => await buildAttachmentSourceChunksAsync(source, cancellationToken).ConfigureAwait(false),
                _ => []
            };

            if (chunks.Count > 0)
            {
                fw.model<RagChunks>().deleteBySource(source.id);
                foreach (var chunk in chunks)
                    fw.model<RagChunks>().addEmbedding(chunk);
                fw.model<RagChunks>().deleteLegacyByEntity(source.fwentities_id, source.item_id);
                fw.model<RagSources>().markIndexed(source.id);
            }
            else
            {
                fw.model<RagSources>().markSkipped(source.id, "No indexable text found.");
            }
        }
        catch (Exception ex)
        {
            fw.model<RagSources>().markFailed(source.id, ex.Message);
            fw.logger(LogLevel.WARN, "RAG source indexing failed: source_id=", source.id, ", error=", ex.Message);
        }
    }

    public async Task IndexKBArticleAsync(int kbId, CancellationToken cancellationToken = default)
    {
        if (!fw.model<RagSources>().queueKBArticle(kbId))
            return;

        int entityId = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_KB);
        var source = fw.model<RagSources>().oneBySourceKey(RagSources.BuildSourceKey(RagSources.SOURCE_TYPE_KB_ARTICLE, entityId, kbId, 0));
        if (source.Count > 0)
            await IndexSourceAsync(source["id"].toInt(), cancellationToken).ConfigureAwait(false);
    }

    public async Task IndexAttachmentToEntityAsync(int attId, string entityIcode, int itemId, bool clearExisting = true, CancellationToken cancellationToken = default)
    {
        if (attId <= 0 || itemId <= 0 || string.IsNullOrWhiteSpace(entityIcode))
            return;

        if (entityIcode == FwEntities.ICODE_ASSISTANT_MESSAGE)
        {
            if (!fw.model<RagSources>().queueAssistantUpload(attId, entityIcode, itemId))
                return;
        }
        else
        {
            var att = fw.model<Att>().one(attId);
            int entityId = fw.model<FwEntities>().idByIcodeOrAdd(entityIcode);
            fw.model<RagSources>().queueSource(
                RagSources.SOURCE_TYPE_KB_ATTACHMENT,
                entityId,
                itemId,
                attId,
                att["fname"].toStr(att["iname"].toStr()),
                string.Empty,
                RagSources.HashText(string.Join("|", attId, att["fname"], att["fsize"], att["upd_time"], att["add_time"])),
                string.Empty,
                Utils.jsonEncode(DB.h("ext", att["ext"].toStr(), "fsize", att["fsize"].toLong()))
            );
        }

        int fwentitiesId = fw.model<FwEntities>().idByIcode(entityIcode);
        string sourceType = entityIcode == FwEntities.ICODE_ASSISTANT_MESSAGE
            ? RagSources.SOURCE_TYPE_ASSISTANT_UPLOAD
            : RagSources.SOURCE_TYPE_KB_ATTACHMENT;
        var source = fw.model<RagSources>().oneBySourceKey(RagSources.BuildSourceKey(sourceType, fwentitiesId, itemId, attId));
        if (source.Count > 0)
        {
            int sourceId = source["id"].toInt();
            await IndexSourceAsync(sourceId, cancellationToken).ConfigureAwait(false);
            var indexed = fw.model<RagSources>().oneTyped(sourceId);
            if (clearExisting && indexed?.index_status == RagSources.INDEX_STATUS_SKIPPED)
                fw.model<RagChunks>().deleteLegacyByEntity(fwentitiesId, itemId);
        }
    }

    public void DeleteKBArticleEmbeddings(int kbId)
    {
        fw.model<RagSources>().deleteByEntity(FwEntities.ICODE_KB, kbId);
    }

    private async Task<List<RagChunks.ChunkEmbedding>> buildKBArticleSourceChunksAsync(RagSources.Row source, CancellationToken cancellationToken)
    {
        var kb = fw.model<KBArticles>().one(source.item_id);
        if (kb.Count == 0 || kb["status"].toInt() != FwModel.STATUS_ACTIVE)
            return [];

        string text = RagSources.KBArticleText(kb);
        return string.IsNullOrWhiteSpace(text)
            ? []
            : await buildTextChunksAsync(source, text, kb["iname"].toStr(), "KB Article", cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<RagChunks.ChunkEmbedding>> buildSpageSourceChunksAsync(RagSources.Row source, CancellationToken cancellationToken)
    {
        var page = fw.model<Spages>().one(source.item_id);
        if (page.Count == 0 || !fw.model<Spages>().isPublished(page))
            return [];

        string text = htmlToPlainText(RagSources.SpageText(page));
        return string.IsNullOrWhiteSpace(text)
            ? []
            : await buildTextChunksAsync(source, text, page["iname"].toStr(), "Static Page", cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<RagChunks.ChunkEmbedding>> buildAttachmentSourceChunksAsync(RagSources.Row source, CancellationToken cancellationToken)
    {
        if (source.att_id <= 0)
            return [];

        var att = fw.model<Att>().one(source.att_id);
        if (att.Count == 0)
            return [];

        string ext = normalizeExtension(att["ext"].toStr());
        if (!IsSupported(ext))
            return [];

        string filepath = resolveAttachmentPath(att);
        if (string.IsNullOrWhiteSpace(filepath) || !File.Exists(filepath))
            return [];

        return await buildFileChunksAsync(source, filepath, ext, att["fname"].toStr(att["iname"].toStr()), cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<RagChunks.ChunkEmbedding>> buildFileChunksAsync(RagSources.Row source, string filepath, string ext, string filename, CancellationToken cancellationToken)
    {
        var records = new List<RagChunks.ChunkEmbedding>();
        var parser = parsers.FirstOrDefault(p => p.CanParse(ext));
        if (parser == null)
            return records;

        var parsed = await parser.ParseAsync(filepath, cancellationToken).ConfigureAwait(false);
        var blocks = parsed.Blocks.Count > 0 ? parsed.Blocks : MarkdownChunker.SplitToBlocks(parsed.Markdown).ToList();
        int chunkIndex = 0;
        foreach (var block in limitBlocks(blocks))
            chunkIndex = await appendBlockChunksAsync(source, block, filename, chunkIndex, records, cancellationToken).ConfigureAwait(false);

        return records;
    }

    private async Task<List<RagChunks.ChunkEmbedding>> buildTextChunksAsync(RagSources.Row source, string text, string filename, string section, CancellationToken cancellationToken)
    {
        var records = new List<RagChunks.ChunkEmbedding>();
        var block = new RawBlock(text, 0, section);
        await appendBlockChunksAsync(source, block, filename, 0, records, cancellationToken).ConfigureAwait(false);
        return records;
    }

    private async Task<int> appendBlockChunksAsync(RagSources.Row source, RawBlock block, string filename, int chunkIndex, List<RagChunks.ChunkEmbedding> records, CancellationToken cancellationToken)
    {
        var embeddingModel = LLM.MODEL_TEXT_EMBEDDING_3_SMALL;

        int maxChunks = maxIndexChunks();
        foreach (var chunk in TokenAwareChunks(block.Text))
        {
            if (chunkIndex >= maxChunks)
                return chunkIndex;

            cancellationToken.ThrowIfCancellationRequested();
            var embedding = await fw.model<LLM>().embeddingForTextAsync(chunk, embeddingModel, cancellationToken).ConfigureAwait(false);
            records.Add(new RagChunks.ChunkEmbedding
            {
                RagSourcesId = source.id,
                FwEntitiesId = source.fwentities_id,
                ItemId = source.item_id,
                AttId = source.att_id,
                SourceType = source.source_type,
                SourceTitle = source.iname,
                SourceUrl = source.url,
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

    private static string htmlToPlainText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string html = Regex.Replace(text, @"<(script|style|noscript|template|svg|canvas)[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(p|div|section|article|br|li|tr|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        return Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"[ \t]{2,}", " ").Trim();
    }

    private static string normalizeExtension(string ext)
    {
        ext = (ext ?? string.Empty).Trim().ToLowerInvariant();
        if (ext.Length > 0 && !ext.StartsWith('.'))
            ext = "." + ext;
        return ext;
    }
}
