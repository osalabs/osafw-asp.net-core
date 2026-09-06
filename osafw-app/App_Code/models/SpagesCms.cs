using System;
using System.Linq;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace osafw;

public partial class Spages
{
    public const string RevisionTable = "spages_revisions";
    public const string RedirectTable = "spages_redirects";
    public const string ContentFields = "iname parent_id url idesc idesc_left idesc_right head_att_id template prio meta_keywords meta_description meta_title custom_head custom_css custom_js redirect_url content_json access_level nav_visible nav_title noindex image_alt image_decorative status is_home is_snippet snippet_key";
    private bool? cmsReady;
    private DateTime? requestTime;
    private Dictionary<int, FwDict>? currentPublications;
    // SQL Server's legacy DateTime parameter precision differs from SQLite. Publication boundaries use whole UTC seconds.
    private DateTime now => requestTime ??= new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    public bool isEnabled() => fw.config("SPAGES_ENABLED") == null || fw.config("SPAGES_ENABLED").toBool();
    public bool isCmsReady() => cmsReady ??= (db.dbtype is DB.DBTYPE_SQLSRV or DB.DBTYPE_SQLITE) && db.tableSchemaFull(table_name).ContainsKey("draft_json");
    public int authorLevel() => Math.Clamp(fw.config("SPAGES_AUTHOR_LEVEL").toInt(Users.ACL_MANAGER), Users.ACL_MEMBER, Users.ACL_SITEADMIN);
    public int publisherLevel() => Math.Max(authorLevel(), Math.Clamp(fw.config("SPAGES_PUBLISHER_LEVEL").toInt(Users.ACL_MANAGER), Users.ACL_MEMBER, Users.ACL_SITEADMIN));
    public bool canAuthor() => isEnabled() && fw.userId > 0 && fw.userAccessLevel >= authorLevel() && !fw.model<Users>().isReadOnly();
    public bool canPublish() => canAuthor() && fw.userAccessLevel >= publisherLevel();
    public bool canPreview() => canAuthor() && (fw.userAccessLevel >= Users.ACL_SITEADMIN || fw.model<Users>().isAccessByRolesResourcePermission(fw.userId, "AdminSpages", Permissions.PERMISSION_VIEW));

    public void requireAuthor(bool publisher = false)
    {
        if (!(publisher ? canPublish() : canAuthor())) throw new AuthException("You do not have permission to " + (publisher ? "publish" : "edit") + " pages.");
        if (!isCmsReady()) throw new UserException("Apply the Spages CMS schema update first. This CMS requires SQL Server or SQLite.");
    }

    /// <summary>Read an explicit editing draft. Raw legacy columns remain a compatibility publication projection.</summary>
    public FwDict draft(int id)
    {
        var row = db.row(table_name, DB.h("id", id));
        if (row.Count == 0) throw new NotFoundException();
        var result = Utils.jsonDecodeDict(row["draft_json"].toStr()) ?? new FwDict(row);
        foreach (var key in Utils.qw("id edit_version workflow review_note add_time add_users_id upd_time upd_users_id")) result[key] = row[key];
        if (result["workflow"].toStr() == "scheduled" && publicationDate(db.value(RevisionTable, DB.h("spages_id", id, "kind", "published", "cancelled", 0), "effective_time", "effective_time desc"), DateTime.MaxValue) <= now) result["workflow"] = "published";
        if (result["content_json"].toStr().Length == 0) result["content_json"] = SpagesContent.fromMarkdown(result).ToJsonString();
        if (result["template"].toStr().Length == 0) result["template"] = legacyLayout(result);
        return result;
    }

    public static string legacyLayout(FwDict page) => page["idesc_left"].toStr().Length > 0
        ? (page["idesc_right"].toStr().Length > 0 ? "three-column" : "sidebar-left")
        : (page["idesc_right"].toStr().Length > 0 ? "sidebar-right" : "article");

    /// <summary>Idempotent data conversion, explicitly invoked after additive schema installation. No public GET performs writes.</summary>
    public int migrateLegacy()
    {
        requireAuthor(true);
        if (fw.userAccessLevel < Users.ACL_SITEADMIN) throw new AuthException();
        int count = 0;
        foreach (var row in db.array(table_name, []))
        {
            if (!string.IsNullOrEmpty(row["draft_json"].toStr())) continue;
            db.begin();
            try
            {
                var item = FormUtils.filter(row, ContentFields);
                var originalItem = new FwDict(item);
                foreach (var field in Utils.qw("pub_time add_time add_users_id upd_time upd_users_id")) originalItem[field] = row[field];
                var original = Utils.jsonEncode(originalItem);
                item["template"] = legacyLayout(item);
                item["content_json"] = SpagesContent.fromMarkdown(item, legacyAttachmentId).ToJsonString();
                var encoded = Utils.jsonEncode(item);
                int changed = db.update(table_name, DB.h("draft_json", encoded, "content_json", item["content_json"], "template", item["template"], "workflow", row["status"].toInt() == STATUS_ACTIVE ? "published" : "draft", "edit_version", 1), DB.h("id", row["id"], "edit_version", 0));
                if (changed != 1) { db.rollback(); continue; }
                addRevision(row["id"].toInt(), "original", original, now, "Original Markdown before CMS conversion");
                addRevision(row["id"].toInt(), "published", encoded, publicationDate(row["pub_time"], DateTime.UnixEpoch), "Initial CMS revision");
                db.commit();
                count++;
            }
            catch { db.rollback(); clearCmsCache(); throw; }
        }
        clearCmsCache();
        return count;
    }

    /// <summary>Save/coalesce a working draft using compare-and-swap. Explicit saves additionally append immutable history.</summary>
    public int saveDraft(int id, FwDict input, int expectedVersion, bool autosave = false)
    {
        requireAuthor();
        if (id > 0) requireConverted(id);
        var item = FormUtils.filter(input, ContentFields);
        var old = id > 0 ? draft(id) : new FwDict();
        if (id > 0)
            foreach (var field in Utils.qw(ContentFields)) if (!item.ContainsKey(field)) item[field] = old[field];
        foreach (var field in Utils.qw("iname url idesc idesc_left idesc_right template meta_keywords meta_description meta_title custom_head custom_css custom_js redirect_url content_json nav_title image_alt snippet_key")) item[field] = item[field].toStr();
        item["head_att_id"] = item["head_att_id"].toInt() > 0 ? item["head_att_id"].toInt() : null;
        item["iname"] = item["iname"].toStr().Trim();
        if (item["iname"].toStr().Length is 0 or > 64) throw new UserException("Enter a page title of 1–64 characters.");
        item["is_home"] = old["is_home"].toInt();
        item["is_snippet"] = id > 0 ? old["is_snippet"].toInt() : item["is_snippet"].toInt() == 1 ? 1 : 0;
        item["status"] = STATUS_ACTIVE;
        item["access_level"] = Math.Clamp(item["access_level"].toInt(), 0, 100);
        item["parent_id"] = Math.Max(0, item["parent_id"].toInt());
        item["prio"] = item["prio"].toInt();
        item["nav_visible"] = item["nav_visible"].toBool() ? 1 : 0;
        item["noindex"] = item["noindex"].toBool() ? 1 : 0;
        item["image_decorative"] = item["image_decorative"].toBool() ? 1 : 0;
        if (!fw.model<Users>().isAccessLevel(Users.ACL_SITEADMIN))
            foreach (string field in Utils.qw("custom_head custom_css custom_js")) item[field] = old[field].toStr();
        if (item["is_home"].toBool()) { item["parent_id"] = 0; item["url"] = ""; }
        else if (!item["is_snippet"].toBool())
        {
            if (string.IsNullOrWhiteSpace(item["url"].toStr())) item["url"] = Regex.Replace(item["iname"].toStr().ToLowerInvariant(), @"[^\p{L}\p{N}]+", "-").Trim('-');
            if (!Regex.IsMatch(item["url"].toStr(), @"^[\p{L}\p{N}][\p{L}\p{N}_-]*$")) throw new UserException("Use letters, numbers, hyphens, and underscores in the page URL.");
        }
        if (item["is_snippet"].toBool())
        {
            string key = item["snippet_key"].toStr().Trim().ToLowerInvariant();
            if (!Regex.IsMatch(key, @"^[a-z][a-z0-9_-]{0,63}$")) throw new UserException("A snippet key starts with a letter and contains lowercase letters, digits, hyphens, or underscores.");
            if (id > 0 && old["snippet_key"].toStr().Length > 0 && old["snippet_key"].toStr() != key) throw new UserException("Snippet keys are stable; create another snippet to use a different key.");
            item["snippet_key"] = key;
        }
        string template = item["template"].toStr("article");
        if (item["is_snippet"].toBool() && template != "article") throw new UserException("Snippets use the main content region of the Article layout.");
        SpagesContent.layout(template);
        item["template"] = template;
        var doc = SpagesContent.parse(item["content_json"].toStr());
        if (item["is_snippet"].toBool() && SpagesContent.snippetKeys(doc).Count > 0) throw new UserException("Snippets cannot contain other snippets.");
        item["content_json"] = doc.ToJsonString();
        if (!string.IsNullOrWhiteSpace(item["redirect_url"].toStr())) localPath(item["redirect_url"].toStr());
        var json = Utils.jsonEncode(item);
        db.begin();
        try
        {
            lockPublicationNamespace();
            if (item["is_snippet"].toBool() && db.valuep($"SELECT COUNT(*) FROM {qTable()} WHERE is_snippet=1 AND snippet_key=@key AND id<>@id", DB.h("key", item["snippet_key"], "id", id)).toInt() > 0) throw new UserException("This snippet key is already used.");
            if (id == 0)
            {
                id = db.insert(table_name, DB.h("iname", item["iname"], "url", "", "status", STATUS_INACTIVE, "is_snippet", item["is_snippet"], "snippet_key", item["snippet_key"].toStr(), "draft_json", json, "edit_version", 1, "workflow", "draft", "add_users_id", fw.userId));
            }
            else
            {
                int changed = db.update(table_name, DB.h("draft_json", json, "edit_version", expectedVersion + 1, "workflow", "draft", "upd_time", now, "upd_users_id", fw.userId), DB.h("id", id, "edit_version", expectedVersion));
                if (changed != 1) throw new SpagesConflictException();
            }
            if (!autosave) addRevision(id, "saved", json, now, "Draft saved", captureSnippetVersions(item, publications()));
            db.commit();
        }
        catch { db.rollback(); clearCmsCache(); throw; }
        clearCmsCache();
        return id;
    }

    public FwList history(int id)
    {
        return new FwList(db.array(RevisionTable, DB.h("spages_id", id), "id desc"));
    }

    public FwDict revision(int pageId, int revisionId)
    {
        var row = db.row(RevisionTable, DB.h("id", revisionId, "spages_id", pageId));
        if (row.Count == 0) throw new NotFoundException();
        var item = Utils.jsonDecodeDict(row["snapshot_json"].toStr()) ?? throw new UserException("Invalid revision.");
        item["id"] = pageId;
        item["revision_id"] = revisionId;
        item["snippet_versions"] = row["snippet_versions"];
        if (item["content_json"].toStr().Length == 0) item["content_json"] = SpagesContent.fromMarkdown(item).ToJsonString();
        if (item["template"].toStr().Length == 0) item["template"] = legacyLayout(item);
        return item;
    }

    public int restoreRevision(int id, int revisionId, int expectedVersion) => saveDraft(id, revision(id, revisionId), expectedVersion);

    private void requireConverted(int id)
    {
        var row = db.row(table_name, DB.h("id", id));
        if (row.Count == 0) throw new NotFoundException();
        if (row["draft_json"].toStr().Length == 0)
            throw new UserException("A Site Admin must convert existing pages to blocks from the Pages list before editing or changing publication. Its current publication is unchanged.");
    }

    /// <summary>Workflow transitions act on an expected working version; scheduled publication does not overwrite the live projection.</summary>
    public int transition(int id, int expectedVersion, string action, string note = "", DateTime? publishAt = null)
    {
        requireAuthor(action is "publish" or "changes" or "unpublish" or "cancel");
        requireConverted(id);
        var item = draft(id);
        if (item["edit_version"].toInt() != expectedVersion) throw new SpagesConflictException();
        if (action is not ("submit" or "changes" or "publish" or "unpublish" or "cancel")) throw new UserException("Unknown publishing action.");
        if (action == "changes" && item["workflow"].toStr() != "in_review") throw new UserException("Only a submitted draft can be sent back for changes.");
        if (action == "changes" && string.IsNullOrWhiteSpace(note)) throw new UserException("Describe the requested changes.");
        var when = publishAt ?? now;
        if (when.Kind == DateTimeKind.Local) when = when.ToUniversalTime();
        when = new DateTime(when.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        if (when < now) when = now;
        int revisionId = 0;
        db.begin();
        try
        {
            lockPublicationNamespace();
            if (db.update(table_name, DB.h("edit_version", expectedVersion + 1, "upd_time", now, "upd_users_id", fw.userId), DB.h("id", id, "edit_version", expectedVersion)) != 1) throw new SpagesConflictException();
            if (action is "publish" or "unpublish" or "cancel")
            {
                db.exec($"UPDATE {db.qid(RevisionTable)} SET cancelled=1 WHERE spages_id=@id AND kind IN ('published','withdrawn') AND effective_time>@now AND cancelled=0", DB.h("id", id, "now", now));
                db.exec($"UPDATE {db.qid(RedirectTable)} SET status=127 WHERE revision_id IN (SELECT id FROM {db.qid(RevisionTable)} WHERE spages_id=@id AND cancelled=1)", DB.h("id", id));
                currentPublications = null;
            }
            var before = publications(when);
            string workflow = action switch { "submit" => "in_review", "changes" => "changes_requested", "publish" => when > now ? "scheduled" : "published", _ => "draft" };
            if (action == "publish")
            {
                item["status"] = STATUS_ACTIVE;
                var after = new Dictionary<int, FwDict>(before) { [id] = item };
                validatePublication(id, item, after, when);
                revisionId = addRevision(id, "published", Utils.jsonEncode(FormUtils.filter(item, ContentFields)), when, note, captureSnippetVersions(item, before));
                if (when <= now)
                {
                    createPathRedirects(before, after, when, revisionId);
                    var projection = FormUtils.filter(item, ContentFields);
                    projection["pub_time"] = when;
                    db.update(table_name, projection, DB.h("id", id));
                }
            }
            else if (action == "unpublish")
            {
                if (item["is_home"].toBool()) throw new UserException("The home page cannot be withdrawn; disable CMS homepage ownership instead.");
                if (item["is_snippet"].toBool() && usages(item["snippet_key"].toStr(), true).Count > 0) throw new UserException("This snippet is used by published or scheduled pages. Replace those references first.");
                item["status"] = STATUS_INACTIVE;
                revisionId = addRevision(id, "withdrawn", Utils.jsonEncode(FormUtils.filter(item, ContentFields)), now, note);
                db.update(table_name, DB.h("status", STATUS_INACTIVE), DB.h("id", id));
            }
            else if (action != "cancel") revisionId = addRevision(id, action == "submit" ? "submitted" : "changes_requested", Utils.jsonEncode(FormUtils.filter(item, ContentFields)), now, note, captureSnippetVersions(item, publications()));
            db.update(table_name, DB.h("workflow", workflow, "review_note", note.Length > 1000 ? note[..1000] : note), DB.h("id", id));
            if (action is "publish" or "unpublish" or "cancel")
            {
                currentPublications = null;
                rebuildFutureRedirects();
                validateTimeline();
            }
            db.commit();
        }
        catch { db.rollback(); clearCmsCache(); throw; }
        clearCmsCache();
        if (action is "publish" or "unpublish")
            try { fw.model<RagSources>().queueSpage(id); } catch (Exception ex) { fw.logger(LogLevel.WARN, "CMS indexing queue unavailable:", ex.Message); }
        return revisionId;
    }

    private int addRevision(int id, string kind, string snapshot, DateTime effective, string note, string snippets = "") => db.insert(RevisionTable, DB.h("spages_id", id, "kind", kind, "snapshot_json", snapshot, "effective_time", effective, "note", note.Length > 1000 ? note[..1000] : note, "snippet_versions", snippets, "add_users_id", fw.userId));

    private void lockPublicationNamespace()
    {
        // A short transaction lock serializes cross-page path/key validation on both supported providers.
        db.exec($"UPDATE {qTable()} SET prio=prio WHERE id=(SELECT MIN(id) FROM {qTable()})");
    }

    private void rebuildFutureRedirects()
    {
        db.exec($"UPDATE {db.qid(RedirectTable)} SET status=127 WHERE revision_id>0 AND effective_time>@now", DB.h("now", now));
        var before = publications(now);
        foreach (var group in db.arrayp($"SELECT id,effective_time FROM {db.qid(RevisionTable)} WHERE kind IN ('published','withdrawn') AND cancelled=0 AND effective_time>@now ORDER BY effective_time,id", DB.h("now", now)).GroupBy(x => publicationDate(x["effective_time"], now)))
        {
            var after = publications(group.Key);
            createPathRedirects(before, after, group.Key, group.Last()["id"].toInt());
            before = after;
        }
    }

    private void validateTimeline()
    {
        var boundaries = db.arrayp($"SELECT DISTINCT effective_time FROM {db.qid(RevisionTable)} WHERE cancelled=0 AND kind IN ('published','withdrawn') AND effective_time>@now", DB.h("now", now)).Select(x => publicationDate(x["effective_time"], now)).Prepend(now);
        foreach (var boundary in boundaries)
        {
            var pages = publications(boundary);
            var paths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var content in pages.Values.Where(x => isVisible(x, pages, 100) && effectiveAccess(x, pages) > 0))
                foreach (int attachment in pageAttachmentIds(content)) attachmentUrl(attachment, content, true, false, pages);
            foreach (var page in pages.Values.Where(x => !x["is_snippet"].toBool() && isVisible(x, pages, 100)))
            {
                int id = page["id"].toInt();
                string path = publishedUrl(id, pages);
                if (paths.TryGetValue(path, out int owner) && owner != id) throw new UserException("A scheduled or published page would own the same URL: " + path);
                paths[path] = id;
                foreach (var key in SpagesContent.snippetKeys(SpagesContent.parse(page["content_json"].toStr())))
                {
                    var snippet = pages.Values.FirstOrDefault(x => x["is_snippet"].toBool() && x["snippet_key"].toStr() == key);
                    if (snippet == null || !isVisible(snippet, pages, 100) || effectiveAccess(snippet, pages) > effectiveAccess(page, pages)) throw new UserException("The change would make snippet '" + key + "' unavailable to '" + page["iname"].toStr() + "'.");
                }
                var alias = redirectAt(path, boundary);
                if (alias.Count > 0 && alias["spages_id"].toInt() != id) throw new UserException("A redirect already owns this page URL: " + path);
                if (page["redirect_url"].toStr().Length > 0) validateRedirect(path, page["redirect_url"].toStr(), pages, boundary);
            }
        }
    }

    private void clearCmsCache()
    {
        currentPublications = null;
        fw.cache.requestRemoveWithPrefix(cache_prefix);
        FwCache.remove("home_page");
    }

    private static DateTime publicationDate(object? value, DateTime fallback)
    {
        if (value is DateTime date) return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return DateTime.TryParse(value.toStr(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : fallback;
    }

    /// <summary>One request/as-of publication view. A withdrawal is a release, so earlier versions never reappear.</summary>
    public Dictionary<int, FwDict> publications(DateTime? at = null)
    {
        DateTime asOf = at ?? now;
        if (at == null && currentPublications != null) return currentPublications;
        var rows = db.array(table_name, []);
        var results = new Dictionary<int, FwDict>();
        var releases = new Dictionary<int, FwDict>();
        if (isCmsReady())
        {
            var releaseRows = db.arrayp($@"SELECT r.* FROM {db.qid(RevisionTable)} r
WHERE r.cancelled=0 AND r.kind IN ('published','withdrawn') AND r.effective_time<=@now
AND NOT EXISTS (SELECT 1 FROM {db.qid(RevisionTable)} n WHERE n.spages_id=r.spages_id AND n.cancelled=0 AND n.kind IN ('published','withdrawn') AND n.effective_time<=@now AND (n.effective_time>r.effective_time OR (n.effective_time=r.effective_time AND n.id>r.id)))", DB.h("now", asOf));
            foreach (var release in releaseRows) releases[release["spages_id"].toInt()] = release;
        }
        foreach (var row in rows)
        {
            int id = row["id"].toInt();
            FwDict page;
            if (releases.TryGetValue(id, out var release))
            {
                page = Utils.jsonDecodeDict(release["snapshot_json"].toStr()) ?? [];
                page["pub_time"] = release["effective_time"];
                page["revision_id"] = release["id"];
            }
            else
            {
                if (row["draft_json"].toStr().Length > 0) continue;
                page = new(row);
            }
            page["id"] = id;
            page["add_time"] = row["add_time"];
            if (page["status"].toInt() != STATUS_ACTIVE || publicationDate(page["pub_time"], DateTime.UnixEpoch) > asOf) continue;
            results[id] = page;
        }
        if (at == null) currentPublications = results;
        return results;
    }

    public bool isVisible(FwDict page, Dictionary<int, FwDict>? pages = null, int? audience = null)
    {
        if (!isEnabled() || page.Count == 0) return false;
        pages ??= publications();
        int level = audience ?? (fw.userId > 0 ? fw.userAccessLevel : 0);
        var seen = new HashSet<int>();
        var current = page;
        while (current.Count > 0)
        {
            if (current["status"].toInt() != STATUS_ACTIVE || current["access_level"].toInt() > level || !seen.Add(current["id"].toInt()) || seen.Count > 20) return false;
            int parent = current["parent_id"].toInt();
            if (parent == 0) return true;
            if (!pages.TryGetValue(parent, out current!)) return false;
        }
        return false;
    }

    public FwDict published(int id, int? audience = null)
    {
        var pages = publications();
        return pages.TryGetValue(id, out var page) && isVisible(page, pages, audience) ? new(page) : [];
    }

    public FwList publishedPages(int? audience = null)
    {
        var pages = publications();
        var result = new FwList();
        foreach (var page in pages.Values.Where(x => !x["is_snippet"].toBool() && isVisible(x, pages, audience)).OrderByDescending(x => x["prio"].toInt()).ThenBy(x => x["iname"].toStr()))
        {
            if (page["is_home"].toBool() && !fw.config("SPAGES_HOME_ENABLED").toBool()) continue;
            var item = new FwDict(page);
            item["full_url"] = publishedUrl(page["id"].toInt(), pages);
            result.Add(item);
        }
        return result;
    }

    public string publishedUrl(int id, Dictionary<int, FwDict>? pages = null)
    {
        pages ??= publications();
        var parts = new List<string>();
        var seen = new HashSet<int>();
        while (id > 0)
        {
            if (!seen.Add(id) || seen.Count > 20 || !pages.TryGetValue(id, out var row) || row["is_snippet"].toBool()) return "";
            if (!row["is_home"].toBool()) parts.Insert(0, row["url"].toStr());
            id = row["parent_id"].toInt();
        }
        return "/" + string.Join("/", parts);
    }

    public FwDict publishedByPath(string path, int? audience = null)
    {
        path = localPath(path);
        var pages = publications();
        foreach (var page in pages.Values)
            if (!page["is_snippet"].toBool() && string.Equals(publishedUrl(page["id"].toInt(), pages), path, StringComparison.OrdinalIgnoreCase) && isVisible(page, pages, audience))
            {
                var item = new FwDict(page);
                item["full_url"] = path;
                var breadcrumbs = new FwList();
                int id = page["id"].toInt();
                var seen = new HashSet<int>();
                while (id > 0 && seen.Add(id) && pages.TryGetValue(id, out var parent))
                {
                    if (!parent["is_home"].toBool()) breadcrumbs.Insert(0, DB.h("iname", parent["iname"], "url", publishedUrl(id, pages), "is_current", id == page["id"].toInt()));
                    id = parent["parent_id"].toInt();
                }
                item["breadcrumbs"] = breadcrumbs;
                if (breadcrumbs.Count > 0) item["top_page"] = breadcrumbs[0];
                return item;
            }
        return [];
    }

    public override bool isAccess(int id = 0, string action = "")
    {
        if (action is "edit" or "add" or "delete" or "link") return canAuthor();
        return published(id).Count > 0;
    }

    /// <summary>Page-owned files are public only while referenced by an eligible publication. Editors can inspect draft files.</summary>
    public bool isAttachmentVisible(int attachmentId, int pageId, string action = "")
    {
        if (canPreview()) return db.row(table_name, DB.h("id", pageId)).Count > 0;
        var page = published(pageId);
        if (page.Count == 0) return false;
        return pageAttachmentIds(page).Contains(attachmentId);
    }

    private HashSet<int> pageAttachmentIds(FwDict page)
    {
        var doc = SpagesContent.parse(page["content_json"].toStr());
        var ids = SpagesContent.attachmentIds(doc);
        if (page["head_att_id"].toInt() > 0) ids.Add(page["head_att_id"].toInt());
        // Legacy Markdown and inline links may still point at existing library files.
        var html = new HtmlParser().ParseDocument(string.Join("", SpagesContent.Regions.Select(region => SpagesContent.renderRegion(doc, region, new(_ => "", id => "/__cms_attachment/" + id)))));
        foreach (var link in html.QuerySelectorAll("a[href],img[src]"))
        {
            int id = legacyAttachmentId(link.GetAttribute(link.LocalName == "img" ? "src" : "href") ?? "");
            if (id > 0) ids.Add(id);
        }
        return ids;
    }

    public int effectiveAccess(FwDict page, Dictionary<int, FwDict>? pages = null)
    {
        pages ??= publications();
        int level = 0;
        var seen = new HashSet<int>();
        while (page.Count > 0)
        {
            level = Math.Max(level, page["access_level"].toInt());
            if (!seen.Add(page["id"].toInt()) || seen.Count > 20) return 101;
            int parent = page["parent_id"].toInt();
            if (parent == 0) return level;
            if (!pages.TryGetValue(parent, out page!)) return 101;
        }
        return 101;
    }

    public static string localPath(string path)
    {
        path = path.Trim();
        if (!path.StartsWith('/') || path.StartsWith("//") || path.Contains('\\') || path.Any(char.IsControl) || path.Contains('?') || path.Contains('#') || path.Length > 450 || path.Split('/').Any(x => x is "." or "..")) throw new UserException("Use an app-local path without a query or fragment (maximum 450 characters).");
        var decoded = Uri.UnescapeDataString(path);
        if (decoded != path && (decoded.Contains('\\') || decoded.Contains('%') || decoded.Contains('?') || decoded.Contains('#') || decoded.StartsWith("//") || decoded.Any(char.IsControl) || decoded.Split('/').Any(x => x is "." or "..") || decoded.Count(x => x == '/') != path.Count(x => x == '/'))) throw new UserException("Unsafe local URL.");
        return decoded.Length > 1 ? decoded.TrimEnd('/') : decoded;
    }

    private void validatePublication(int id, FwDict item, Dictionary<int, FwDict> pages, DateTime when)
    {
        int parent = item["parent_id"].toInt();
        if (parent > 0 && (!pages.TryGetValue(parent, out var parentPage) || parentPage["is_snippet"].toBool())) throw new UserException("Publish the parent page first.");
        string path = item["is_snippet"].toBool() ? "" : publishedUrl(id, pages);
        if (!item["is_snippet"].toBool())
        {
            if (path.Length == 0) throw new UserException("Page hierarchy contains a cycle or is too deep.");
            localPath(path);
            var first = path.Trim('/').Split('/')[0];
            bool reserved = first.Length > 0 && (new[] { "admin", "dev", "att", "api", "my", "assets", "upload", "sitemap.xml", "robots.txt" }.Contains(first.ToLowerInvariant()) || typeof(FW).Assembly.GetTypes().Any(t => t.Name.Equals(first + "Controller", StringComparison.OrdinalIgnoreCase)));
            if (reserved && path != getFullUrl(id)) throw new UserException("This path belongs to an application route.");
            foreach (var other in pages.Where(x => x.Key != id && !x.Value["is_snippet"].toBool()))
                if (string.Equals(path, publishedUrl(other.Key, pages), StringComparison.OrdinalIgnoreCase)) throw new UserException("This page URL is already published or scheduled.");
        }
        var doc = SpagesContent.parse(item["content_json"].toStr());
        var context = contentContext(item, pages, true, false);
        SpagesContent.validate(doc, item["template"].toStr(), context, item["is_snippet"].toBool());
        if (item["head_att_id"].toInt() > 0)
        {
            context.ImageAttachment!(item["head_att_id"].toInt());
            if (!item["image_decorative"].toBool() && string.IsNullOrWhiteSpace(item["image_alt"].toStr())) throw new UserException("Add alternative text for the page image or mark it decorative.");
        }
        if (item["redirect_url"].toStr().Length > 0) validateRedirect(path, item["redirect_url"].toStr(), pages);
    }

    private SpagesContent.RenderContext contentContext(FwDict page, Dictionary<int, FwDict> pages, bool publishing = false, bool preview = false)
    {
        return new(
            key => renderSnippet(key, page, pages, publishing, preview),
            id => attachmentUrl(id, page, publishing, preview, pages), publishing,
            id => attachmentUrl(id, page, publishing, preview, pages, true));
    }

    private string attachmentUrl(int id, FwDict page, bool publishing, bool preview, Dictionary<int, FwDict> pages, bool image = false)
    {
        if (id <= 0) throw new UserException("Select an attachment.");
        var att = fw.model<Att>();
        var row = att.one(id);
        if (row.Count == 0 || row["status"].toInt() != STATUS_ACTIVE) throw new UserException("This attachment is unavailable.");
        if (image && !row["is_image"].toBool()) throw new UserException("Choose a decoded image file for an image block.");
        int entity = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_SPAGE);
        bool owned = row["fwentities_id"].toInt() == entity && row["item_id"].toInt() == page["id"].toInt();
        if (!owned) att.checkAccess(id, Att.ACCESS_ACTION_LINK, entity, page["id"].toInt());
        if (publishing && effectiveAccess(page, pages) > 0 && !owned) throw new UserException("Restricted pages need page-owned uploads; shared library files are public.");
        return fw.config("ROOT_URL").toStr() + "/Att/" + row["icode"].toStr();
    }

    /// <summary>Render an eligible named snippet for an application template; missing/restricted snippets produce no output.</summary>
    public string renderSnippet(string key)
    {
        var pages = publications();
        var snippet = pages.Values.FirstOrDefault(x => x["is_snippet"].toBool() && x["snippet_key"].toStr().Equals(key, StringComparison.OrdinalIgnoreCase));
        return snippet != null && isVisible(snippet, pages) ? renderSnippet(key, snippet, pages) : "";
    }

    public string renderSnippet(string key, FwDict page, Dictionary<int, FwDict>? pages = null, bool publishing = false, bool preview = false)
    {
        pages ??= publications();
        var snippet = pages.Values.FirstOrDefault(x => x["is_snippet"].toBool() && string.Equals(x["snippet_key"].toStr(), key, StringComparison.OrdinalIgnoreCase));
        if (snippet == null)
        {
            if (preview) return "<aside class=\"spage-callout\">Snippet unavailable in this preview: " + SpagesContent.escape(key) + "</aside>";
            throw new UserException("Publish the '" + key + "' snippet before using it.");
        }
        if (effectiveAccess(snippet, pages) > effectiveAccess(page, pages)) throw new UserException("This snippet is more restricted than the page.");
        if (!publishing && !preview && !isVisible(snippet, pages)) return "";
        var doc = SpagesContent.parse(snippet["content_json"].toStr());
        if (SpagesContent.snippetKeys(doc).Count > 0) throw new UserException("Nested snippets are not supported.");
        return SpagesContent.renderRegion(doc, "main", contentContext(snippet, pages, publishing, preview));
    }

    private int legacyAttachmentId(string url)
    {
        var prefix = fw.config("ROOT_URL").toStr().TrimEnd('/') + "/Att/";
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return 0;
        string code = url[prefix.Length..].Split('?', '#')[0];
        if (!Regex.IsMatch(code, @"^[a-zA-Z0-9_-]+$")) return 0;
        return db.value("att", DB.h("icode", code, "status", STATUS_ACTIVE), "id").toInt();
    }

    private string captureSnippetVersions(FwDict item, Dictionary<int, FwDict> pages)
    {
        var versions = new FwDict();
        foreach (string key in SpagesContent.snippetKeys(SpagesContent.parse(item["content_json"].toStr())))
        {
            var snippet = pages.Values.FirstOrDefault(x => x["is_snippet"].toBool() && x["snippet_key"].toStr() == key);
            if (snippet != null) versions[key] = DB.h("id", snippet["id"], "revision_id", snippet["revision_id"]);
        }
        return Utils.jsonEncode(versions);
    }

    public FwList usages(string key, bool includeScheduled = false)
    {
        var pages = publications();
        if (includeScheduled)
            foreach (var r in db.arrayp($"SELECT * FROM {db.qid(RevisionTable)} WHERE kind='published' AND cancelled=0 AND effective_time>@now", DB.h("now", now)))
            {
                var item = Utils.jsonDecodeDict(r["snapshot_json"].toStr()) ?? [];
                item["id"] = r["spages_id"];
                pages = new(pages) { [-r["id"].toInt()] = item };
            }
        var result = new FwList();
        var seen = new HashSet<int>();
        foreach (var item in pages.Values)
            if (!item["is_snippet"].toBool() && SpagesContent.snippetKeys(SpagesContent.parse(item["content_json"].toStr())).Contains(key) && seen.Add(item["id"].toInt())) result.Add(new FwDict(item));
        return result;
    }

    /// <summary>Public and preview rendering share this data builder. Historical previews pin recorded snippet revisions.</summary>
    public FwDict pageState(FwDict item, bool preview = false, bool historical = false)
    {
        var pages = publications();
        if (historical && Utils.jsonDecodeDict(item["snippet_versions"].toStr()) is FwDict versions)
        {
            pages = pages.Where(x => !x.Value["is_snippet"].toBool()).ToDictionary(x => x.Key, x => x.Value);
            foreach (var value in versions.Values.OfType<FwDict>()) pages[value["id"].toInt()] = revision(value["id"].toInt(), value["revision_id"].toInt());
        }
        var page = new FwDict(item);
        string template = page["template"].toStr();
        if (!SpagesContent.layouts().ContainsKey(template)) template = legacyLayout(page);
        page["template"] = template;
        var doc = page["content_json"].toStr().Length > 0 ? SpagesContent.parse(page["content_json"].toStr()) : SpagesContent.fromMarkdown(page);
        var context = contentContext(page, pages, false, preview);
        foreach (var region in SpagesContent.Regions) page["html_" + region] = SpagesContent.renderRegion(doc, region, context);
        page["html_after_content"] = doc["slots"]?["after_content"] is JsonValue slot && slot.ToString().Length > 0 ? context.Snippet!(slot.ToString()) : "";
        foreach (var name in SpagesContent.layout(template).Slots)
            page["html_slot_" + name] = doc["slots"]?[name] is JsonValue value && value.ToString().Length > 0 ? context.Snippet!(value.ToString()) : "";
        page["is_col1"] = template is "article" or "landing";
        page["is_col2_left"] = template == "sidebar-left";
        page["is_col2_right"] = template == "sidebar-right";
        page["is_col3"] = template == "three-column";
        if (page["head_att_id"].toInt() > 0) page["head_att_id_url"] = context.ImageAttachment!(page["head_att_id"].toInt());
        var nav = publishedPages();
        page["is_landing"] = template == "landing";
        return DB.h("cms", true, "page", page, "pages", new FwList(nav.Where(x => x["nav_visible"].toInt(1) == 1)), "subpages", new FwList(nav.Where(x => x["parent_id"].toInt() == page["id"].toInt())), "hide_sidebar", true, "hide_std_sidebar", true, "is_preview", preview, "is_page_published", !preview, "is_can_edit", canAuthor(), "meta_keywords", page["meta_keywords"], "meta_description", page["meta_description"], "meta_title", page["meta_title"], "canonical_url", preview ? "" : canonicalUrl(page), "noindex", preview || page["noindex"].toBool() || effectiveAccess(page) > 0);
    }

    public void showCmsPage(string path)
    {
        if (!isEnabled()) throw new NotFoundException();
        var page = publishedByPath(path);
        if (page.Count == 0)
        {
            var redirect = resolveRedirect(path);
            if (redirect.Length > 0) { fw.response.StatusCode = 301; fw.response.Headers.Location = fw.config("ROOT_URL").toStr() + redirect; return; }
            throw new NotFoundException();
        }
        if (page["redirect_url"].toStr().Length > 0) { fw.response.StatusCode = 301; fw.response.Headers.Location = fw.config("ROOT_URL").toStr() + localPath(page["redirect_url"].toStr()); return; }
        fw.cache_control = fw.userId > 0 || effectiveAccess(page) > 0 ? "private, no-store" : "no-cache";
        fw.response.Headers.CacheControl = fw.cache_control;
        fw.parser("/home/spage", pageState(page));
    }

    /// <summary>Uses a deployment-configured origin; never trusts the request Host header for search engine metadata.</summary>
    public string canonicalUrl(FwDict page)
    {
        string origin = fw.config("SPAGES_PUBLIC_ORIGIN").toStr().TrimEnd('/');
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0) return "";
        return origin + fw.config("ROOT_URL").toStr().TrimEnd('/') + publishedUrl(page["id"].toInt());
    }

    /// <summary>Effective page text, including current shared snippets, for search and optional downstream retrieval.</summary>
    public string publishedText(FwDict page)
    {
        var state = pageState(page, true);
        var rendered = (FwDict)state["page"]!;
        return page["iname"].toStr() + "\n" + string.Join("\n", new[] { "main", "left", "right", "after_content" }.Select(x => SpagesContent.plainText(rendered["html_" + x].toStr())));
    }

    private void createPathRedirects(Dictionary<int, FwDict> before, Dictionary<int, FwDict> after, DateTime when, int revisionId)
    {
        foreach (var page in before.Where(x => !x.Value["is_snippet"].toBool()))
        {
            string oldPath = publishedUrl(page.Key, before), newPath = publishedUrl(page.Key, after);
            if (oldPath.Length == 0 || newPath.Length == 0 || oldPath == newPath || oldPath == "/") continue;
            db.insert(RedirectTable, DB.h("source_url", oldPath, "spages_id", page.Key, "revision_id", revisionId, "effective_time", when, "add_users_id", fw.userId));
        }
    }

    public string resolveRedirect(string source)
    {
        if (!isCmsReady()) return "";
        source = localPath(source);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { source };
        string path = source;
        for (int i = 0; i < 10; i++)
        {
            var redirect = redirectAt(path, now);
            if (redirect.Count == 0) return path == source ? "" : path;
            if (redirect["spages_id"].toInt() > 0)
            {
                var target = published(redirect["spages_id"].toInt());
                if (target.Count == 0) return "";
                path = publishedUrl(target["id"].toInt());
            }
            else path = localPath(redirect["target_url"].toStr());
            if (!seen.Add(path)) return "";
            if (publishedByPath(path).Count > 0) return path;
        }
        return "";
    }

    private DBRow redirectAt(string path, DateTime at) => db.rowp($"SELECT * FROM {db.qid(RedirectTable)} WHERE LOWER(source_url)=LOWER(@path) AND status=0 AND effective_time<=@at ORDER BY effective_time DESC,id DESC", DB.h("path", path, "at", at));

    private void validateRedirect(string source, string target, Dictionary<int, FwDict>? pages = null, DateTime? boundary = null)
    {
        source = localPath(source); target = localPath(target);
        if (source.Equals(target, StringComparison.OrdinalIgnoreCase)) throw new UserException("A redirect cannot point to itself.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { source };
        string path = target;
        for (int i = 0; i < 10; i++)
        {
            if (!seen.Add(path)) throw new UserException("This redirect would create a loop.");
            var page = (pages ?? publications()).Values.FirstOrDefault(x => !x["is_snippet"].toBool() && publishedUrl(x["id"].toInt(), pages).Equals(path, StringComparison.OrdinalIgnoreCase));
            if (page != null && page["redirect_url"].toStr().Length > 0) { path = localPath(page["redirect_url"].toStr()); continue; }
            if (page != null) return;
            var row = redirectAt(path, boundary ?? now);
            if (row.Count == 0) return;
            path = row["spages_id"].toInt() > 0 ? publishedUrl(row["spages_id"].toInt(), pages) : localPath(row["target_url"].toStr());
            if (path.Length == 0) return;
        }
        throw new UserException("Redirect chain is too long.");
    }

    public int saveRedirect(string source, string target)
    {
        requireAuthor(true);
        source = localPath(source); target = localPath(target);
        db.begin();
        try
        {
            lockPublicationNamespace();
            if (isReservedPath(source)) throw new UserException("This path belongs to an application route.");
            if (publishedByPath(source, 100).Count > 0) throw new UserException("A published page already owns this URL.");
            validateRedirect(source, target);
            db.exec($"UPDATE {db.qid(RedirectTable)} SET status=127 WHERE LOWER(source_url)=LOWER(@source) AND revision_id=0", DB.h("source", source));
            int id = db.insert(RedirectTable, DB.h("source_url", source, "target_url", target, "effective_time", now, "add_users_id", fw.userId));
            validateTimeline();
            db.commit(); return id;
        }
        catch { db.rollback(); clearCmsCache(); throw; }
    }

    public void removeRedirect(int id)
    {
        requireAuthor(true);
        db.update(RedirectTable, DB.h("status", STATUS_DELETED), DB.h("id", id, "revision_id", 0));
    }

    private static bool isReservedPath(string path)
    {
        string first = path.Trim('/').Split('/')[0];
        return first.Length == 0 || new[] { "admin", "dev", "att", "api", "my", "assets", "upload", "sitemap.xml", "robots.txt" }.Contains(first.ToLowerInvariant()) || typeof(FW).Assembly.GetTypes().Any(t => t.Name.Equals(first + "Controller", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class SpagesConflictException : UserException
{
    public SpagesConflictException() : base("Someone saved a newer version. Your unsaved changes are still in this editor; reload the current draft before reapplying them.") { }
}
