// Static Pages model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text.Json.Nodes;
using AngleSharp.Html.Parser;

namespace osafw;
public class Spages : FwModel<Spages.Row>
{
    public class Row
    {
        public int id { get; set; }
        public int parent_id { get; set; }
        public string url { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public int? head_att_id { get; set; }
        public string idesc_left { get; set; } = string.Empty;
        public string idesc_right { get; set; } = string.Empty;
        public string meta_keywords { get; set; } = string.Empty;
        public string meta_description { get; set; } = string.Empty;
        public DateTime? pub_time { get; set; }
        public string template { get; set; } = string.Empty;
        public int prio { get; set; }
        public int is_home { get; set; }
        public string redirect_url { get; set; } = string.Empty;
        public string custom_head { get; set; } = string.Empty;
        public string custom_css { get; set; } = string.Empty;
        public string custom_js { get; set; } = string.Empty;
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
        public int is_snippet { get; set; }
        public string content_json { get; set; } = string.Empty;
        public string draft_json { get; set; } = string.Empty;
        public int workflow { get; set; }
        public string review_note { get; set; } = string.Empty;
        public int access_level { get; set; }
        public int is_nav_visible { get; set; }
        public string nav_title { get; set; } = string.Empty;
        public string meta_title { get; set; } = string.Empty;
        public int is_noindex { get; set; }
        public string url_aliases { get; set; } = string.Empty;
    }

    public Spages() : base()
    {
        table_name = "spages";
    }

    /// <summary>Read the effective publication for the current audience. Editors use oneDraftOrFail.</summary>
    public override DBRow one(int id) => new DBRow(onePublished(id));

    public override object? oneField(int id, string field_name) => one(id)[field_name];

    /// <summary>Withdraw a page while retaining its identity and immutable publication history.</summary>
    public override void delete(int id, bool is_perm = false)
    {
        updateWorkflow(id, "unpublish", "Withdrawn through the model API");
    }

    public bool isExistsByUrl(string url, int parent_id, int not_id)
    {
        FwDict where = [];
        where["parent_id"] = parent_id;
        where["url"] = url;
        where["id"] = db.opNOT(not_id);
        int val = db.value(table_name, where, "id").toInt();
        if (val > 0)
        {
            return true;
        }
        else
            return false;
    }

    /// <summary>Find an effective publication by URL segment and parent for the current audience.</summary>
    public FwDict oneByUrl(string url, int parent_id)
    {
        return listPublished().FirstOrDefault(x => x["parent_id"].toInt() == parent_id && x["url"].toStr().Equals(url, StringComparison.OrdinalIgnoreCase)) ?? [];
    }

    // return one latest record by full_url (i.e. relative url from root, without domain)
    public FwDict oneByFullUrl(string full_url)
    {
        return onePublishedByPath(full_url);
    }

    public FwList listChildren(int parent_id)
    {
        return new FwList(listPublished().Where(x => x["parent_id"].toInt() == parent_id));
    }

    /// <summary>
    /// Reads rows from a trusted SQL predicate and returns a ParsePage tree with <c>children</c> rows.
    /// </summary>
    /// <param name = "where">Trusted SQL predicate body.</param>
    /// <param name = "orderby">Trusted SQL ORDER BY body.</param>
    /// <returns>ParsePage list with hierarchy stored in each row's <c>children</c> key.</returns>
    public FwList listTreeByFilter(string where, FwDict list_where_params, string orderby)
    {
        FwList rows = db.arrayp("select * from " + db.qid(table_name) + " where " + where + " order by " + orderby, list_where_params);
        FwList pages_tree = listTree(rows, 0);
        return pages_tree;
    }

    // return parsepage array list of rows with hierarcy (children rows added to parents as "children" key)
    // RECURSIVE!
    public FwList listTree(FwList rows, int parent_id, int level = 0, string parent_url = "")
    {
        FwList result = [];
        if (level > 20) // prevent infinite loop (max 20 levels)
        {
            return result;
        }

        foreach (FwDict row in rows)
        {
            if (parent_id == row["parent_id"].toInt())
            {
                FwDict row2 = new(row);
                row2["_level"] = level;
                // row2["_level1"] level + 1 'to easier use in templates
                var full_url = parent_url + "/" + row["url"];
                row2["full_url"] = full_url;
                row2["children"] = listTree(rows, row["id"].toInt(), level + 1, full_url);
                result.Add(row2);
            }
        }

        return result;
    }

    /// <summary>
    /// Flattens a page tree for ParsePage and adds <c>leveler</c> rows for indentation.
    /// </summary>
    /// <param name = "pages_tree">Tree returned by <see cref = "listTree"/>.</param>
    /// <returns>ParsePage list with <c>leveler</c> arrays added for nested rows.</returns>
    public FwList listTreeFlat(FwList? pages_tree, int level = 0)
    {
        FwList result = [];
        if (pages_tree != null)
        {
            foreach (FwDict row in pages_tree)
            {
                result.Add(row);
                // add leveler
                if (level > 0)
                {
                    FwList leveler = [];
                    for (int i = 1; i <= level; i++)
                        leveler.Add(new FwDict());
                    row["leveler"] = leveler;
                }

                // subpages
                result.AddRange(listTreeFlat((FwList? )row["children"], level + 1));
            }
        }

        return result;
    }

    /// <summary>
    /// Renders nested page options with indentation for a select element.
    /// </summary>
    /// <param name = "pages_tree">Tree returned by <see cref = "listTree"/>.</param>
    /// <returns>HTML <c>option</c> elements.</returns>
    public string renderTreeSelectOptions(string selected_id, FwList? pages_tree, int level = 0)
    {
        StringBuilder result = new();
        if (pages_tree != null)
        {
            foreach (FwDict row in pages_tree)
            {
                result.AppendLine("<option value=\"" + row["id"] + "\"" + (row["id"].toStr() == selected_id ? " selected=\"selected\" " : "") + ">" + Utils.strRepeat("&#8212; ", level) + row["iname"] + "</option>");
                // subpages
                result.Append(renderTreeSelectOptions(selected_id, (FwList? )row["children"], level + 1));
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Builds the app-relative URL path for a page by walking its parent chain.
    /// </summary>
    /// <returns>URL like <c>/page/subpage/subsubpage</c>, or empty string when the chain is invalid.</returns>
    public string getFullUrl(int id, int level = 0)
    {
        return onePublished(id).Count > 0 ? publishedUrl(id) : "";
    }

    /// <summary>
    /// Lists parent pages from topmost ancestor to immediate parent.
    /// </summary>
    public DBList listParents(int id)
    {
        DBList result = [];
        var item = one(id);
        var seen = new System.Collections.Generic.HashSet<int>();
        while (item.Count > 0 && seen.Add(item["id"].toInt()) && seen.Count <= 20)
        {
            var item_id = item["id"].toInt();
            if (item_id != id)
            {
                result.Insert(0, item);
            }

            item = one(item["parent_id"]);
        }

        return result;
    }

    public bool isPublished(FwDict item)
    {
        return onePublished(item["id"].toInt()).Count > 0;
    }

    // render page by full url
    public void showPageByFullUrl(string full_url)
    {
        showCmsPage(full_url);
    }

    public DBList listChildrenPublished(int parent_id)
    {
        return new DBList(listChildren(parent_id).Select(x => new DBRow(x)));
    }

    // check if item exists for a given email
    // Public Overrides Function isExists(uniq_key As Object, not_id As Integer) As Boolean
    // Return isExistsByField(uniq_key, not_id, "email")
    // End Function
    // return correct url - TODO
    public string getUrl(int id, string icode, string url = "")
    {
        if (!string.IsNullOrEmpty(url))
        {
            if (Regex.IsMatch(url, "^/"))
            {
                url = fw.config("ROOT_URL") + url;
            }

            return url;
        }
        else
        {
            icode = str2icode(icode);
            if (!string.IsNullOrEmpty(icode))
            {
                return fw.config("ROOT_URL") + "/Pages/" + icode;
            }
            else
                return fw.config("ROOT_URL") + "/Pages/" + id;
        }
    }

    // TODO
    public static string str2icode(string str)
    {
        str = str.Trim();
        str = Regex.Replace(str, @"[^\w ]", " ");
        str = Regex.Replace(str, " +", "-");
        return str;
    }

    public const string REVISION_TABLE = "spages_revisions";
    public const string CONTENT_FIELDS = "iname parent_id url head_att_id template prio meta_keywords meta_description meta_title custom_head custom_css custom_js redirect_url url_aliases content_json access_level is_nav_visible nav_title is_noindex status is_home is_snippet";
    public const int AUTHOR_LEVEL = Users.ACL_MANAGER;
    public const int PUBLISHER_LEVEL = Users.ACL_MANAGER;
    public const int WORKFLOW_DRAFT = 0;
    public const int WORKFLOW_IN_REVIEW = 10;
    public const int WORKFLOW_CHANGES_REQUESTED = 20;
    public const int WORKFLOW_PUBLISHED = 30;
    public const int WORKFLOW_SCHEDULED = 40;
    public const int KIND_SAVED = 0;
    public const int KIND_SUBMITTED = 10;
    public const int KIND_CHANGES_REQUESTED = 20;
    public const int KIND_PUBLISHED = 30;
    public const int KIND_WITHDRAWN = 40;
    public const int KIND_ORIGINAL = 50;
    private DateTime? requestTime;
    private Dictionary<int, FwDict>? currentPublications;
    // A request uses one publication instant; approved publication dates use whole UTC seconds.
    private DateTime now => requestTime ??= new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    public bool isAuthor() => fw.userId > 0 && fw.userAccessLevel >= AUTHOR_LEVEL && !fw.model<Users>().isReadOnly();

    public bool isPublisher() => isAuthor() && fw.userAccessLevel >= PUBLISHER_LEVEL;

    public bool isPreviewAllowed() => isAuthor() && (fw.userAccessLevel >= Users.ACL_SITEADMIN || fw.model<Users>().isAccessByRolesResourcePermission(fw.userId, "AdminSpages", Permissions.PERMISSION_VIEW));

    public void requireAuthor(bool isPublisherRequired = false)
    {
        if (!(isPublisherRequired ? isPublisher() : isAuthor()))
        {
            throw new AuthException("You do not have permission to " + (isPublisherRequired ? "publish" : "edit") + " pages.");
        }
    }

    /// <summary>Read the working draft for an editor. Public consumers must use onePublished instead.</summary>
    public FwDict oneDraftOrFail(int id)
    {
        var row = db.row(table_name, DB.h("id", id));
        if (row.Count == 0)
        {
            throw new NotFoundException();
        }

        var item = Utils.jsonDecodeDict(row["draft_json"].toStr()) ?? throw new UserException("Convert existing pages using the Site Admin migration action before editing.");
        foreach (var field in Utils.qw("id workflow review_note add_time add_users_id upd_time upd_users_id"))
        {
            item[field] = row[field];
        }

        if (item["workflow"].toInt() == WORKFLOW_SCHEDULED && !isScheduled(id))
        {
            item["workflow"] = WORKFLOW_PUBLISHED;
        }

        return item;
    }

    /// <summary>Save the working draft. Autosaves coalesce; explicit saves append immutable history.</summary>
    public int saveDraft(int id, FwDict input, bool isAutosave = false)
    {
        requireAuthor();
        var item = FormUtils.filter(input, CONTENT_FIELDS);
        var old = id > 0 ? oneDraftOrFail(id) : new FwDict();
        if (id > 0)
        {
            foreach (var field in Utils.qw(CONTENT_FIELDS))
            {
                if (!item.ContainsKey(field))
                {
                    item[field] = old[field];
                }
            }
        }

        foreach (var field in Utils.qw("iname url template meta_keywords meta_description meta_title custom_head custom_css custom_js redirect_url url_aliases content_json nav_title"))
        {
            item[field] = item[field].toStr();
        }

        item["head_att_id"] = item["head_att_id"].toInt() > 0 ? item["head_att_id"].toInt() : null;
        item["iname"] = item["iname"].toStr().Trim();
        if (item["iname"].toStr().Length is 0 or > 64)
        {
            throw new UserException("Enter a page title of 1–64 characters.");
        }

        item["is_home"] = old["is_home"].toInt();
        item["is_snippet"] = id > 0 ? old["is_snippet"].toInt() : item["is_snippet"].toInt() == 1 ? 1 : 0;
        item["status"] = STATUS_ACTIVE;
        item["access_level"] = Math.Clamp(item["access_level"].toInt(), 0, 100);
        item["parent_id"] = Math.Max(0, item["parent_id"].toInt());
        item["prio"] = item["prio"].toInt();
        item["is_nav_visible"] = item["is_nav_visible"].toBool() ? 1 : 0;
        item["is_noindex"] = item["is_noindex"].toBool() ? 1 : 0;
        if (!fw.model<Users>().isAccessLevel(Users.ACL_SITEADMIN))
        {
            foreach (string field in Utils.qw("custom_head custom_css custom_js"))
            {
                item[field] = old[field].toStr();
            }
        }

        if (item["is_home"].toBool())
        {
            item["parent_id"] = 0;
            item["url"] = "";
        }
        else if (!item["is_snippet"].toBool())
        {
            if (string.IsNullOrWhiteSpace(item["url"].toStr()))
            {
                item["url"] = Regex.Replace(item["iname"].toStr().ToLowerInvariant(), @"[^\p{L}\p{N}]+", "-").Trim('-');
            }

            if (!Regex.IsMatch(item["url"].toStr(), @"^[\p{L}\p{N}][\p{L}\p{N}_-]*$"))
            {
                throw new UserException("Use letters, numbers, hyphens, and underscores in the page URL.");
            }
        }

        if (item["is_snippet"].toBool())
        {
            string key = item["url"].toStr().Trim().ToLowerInvariant();
            if (!Regex.IsMatch(key, @"^[a-z][a-z0-9_-]{0,63}$"))
            {
                throw new UserException("A snippet key starts with a letter and contains lowercase letters, digits, hyphens, or underscores.");
            }

            if (id > 0 && old["url"].toStr().Length > 0 && old["url"].toStr() != key)
            {
                throw new UserException("Snippet keys are stable; create another snippet to use a different key.");
            }

            item["url"] = key;
        }

        string template = item["template"].toStr("article");
        if (item["is_snippet"].toBool() && template != "article")
        {
            throw new UserException("Snippets use the main content region of the Article layout.");
        }

        SpagesContent.layout(template);
        item["template"] = template;
        var doc = SpagesContent.parse(item["content_json"].toStr());
        if (item["is_snippet"].toBool() && SpagesContent.listSnippetKeys(doc).Count > 0)
        {
            throw new UserException("Snippets cannot contain other snippets.");
        }

        item["content_json"] = doc.ToJsonString();
        if (!string.IsNullOrWhiteSpace(item["redirect_url"].toStr()))
        {
            localPath(item["redirect_url"].toStr());
        }

        item["url_aliases"] = string.Join("\n", listUrlAliases(item));
        var json = Utils.jsonEncode(item);
        db.begin();
        try
        {
            lockPublicationNamespace();
            if (item["is_snippet"].toBool() && db.valuep($"SELECT COUNT(*) FROM {qTable()} WHERE is_snippet=1 AND LOWER(url)=LOWER(@key) AND id<>@id", DB.h("key", item["url"], "id", id)).toInt() > 0)
            {
                throw new UserException("This snippet key is already used.");
            }

            if (id == 0)
            {
                id = db.insert(table_name, new FwDict
                {
                    ["iname"] = item["iname"],
                    ["url"] = item["url"],
                    ["status"] = STATUS_INACTIVE,
                    ["is_snippet"] = item["is_snippet"],
                    ["draft_json"] = json,
                    ["workflow"] = WORKFLOW_DRAFT,
                    ["add_users_id"] = fw.userId
                });
            }
            else
            {
                db.update(table_name, new FwDict
                {
                    ["draft_json"] = json,
                    ["workflow"] = WORKFLOW_DRAFT,
                    ["upd_time"] = now,
                    ["upd_users_id"] = fw.userId
                }, DB.h("id", id));
            }

            if (!isAutosave)
            {
                addRevision(id, KIND_SAVED, json, now, "Draft saved", captureSnippetVersions(item, listPublicationsByDate()));
            }

            db.commit();
        }
        catch
        {
            db.rollback();
            clearCmsCache();
            throw;
        }

        clearCmsCache();
        return id;
    }

    public FwList listRevisions(int id)
    {
        var rows = new FwList(db.array(REVISION_TABLE, DB.h("spages_id", id), "id desc"));
        foreach (var row in rows)
        {
            row["kind_label"] = row["kind"].toInt() switch
            {
                KIND_SUBMITTED => "Submitted",
                KIND_CHANGES_REQUESTED => "Changes requested",
                KIND_PUBLISHED => "Published",
                KIND_WITHDRAWN => "Withdrawn",
                KIND_ORIGINAL => "Original content",
                _ => "Draft saved"
            };
            row["is_cancelled"] = row["status"].toInt() == STATUS_DELETED;
        }

        return rows;
    }

    public FwDict oneRevisionOrFail(int pageId, int revisionId)
    {
        var row = db.row(REVISION_TABLE, DB.h("id", revisionId, "spages_id", pageId));
        if (row.Count == 0)
        {
            throw new NotFoundException();
        }

        var item = Utils.jsonDecodeDict(row["snapshot_json"].toStr()) ?? throw new UserException("Invalid revision.");
        item["id"] = pageId;
        item["revision_id"] = revisionId;
        item["snippet_versions"] = row["snippet_versions"];
        return item;
    }

    public int restoreRevision(int id, int revisionId) => saveDraft(id, oneRevisionOrFail(id, revisionId));

    /// <summary>Approve a draft snapshot or change its review state. Future releases leave the current publication visible.</summary>
    public int updateWorkflow(int id, string action, string note = "", DateTime? publishAt = null)
    {
        requireAuthor(action is "publish" or "changes" or "unpublish" or "cancel");
        if (action is not ("submit" or "changes" or "publish" or "unpublish" or "cancel"))
        {
            throw new UserException("Unknown publishing action.");
        }

        var item = oneDraftOrFail(id);
        if (action == "changes" && item["workflow"].toInt() != WORKFLOW_IN_REVIEW)
        {
            throw new UserException("Only a submitted draft can be sent back for changes.");
        }

        if (action == "changes" && string.IsNullOrWhiteSpace(note))
        {
            throw new UserException("Describe the requested changes.");
        }

        DateTime when = publishAt?.ToUniversalTime() ?? now;
        when = new DateTime(when.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        if (when < now)
        {
            when = now;
        }

        int revisionId = 0;
        bool isRelease = action is "publish" or "unpublish" or "cancel";
        db.begin();
        try
        {
            lockPublicationNamespace();
            if (isRelease)
            {
                db.exec($@"UPDATE {db.qid(REVISION_TABLE)} SET status=127
                            WHERE spages_id=@id AND kind IN ({KIND_PUBLISHED},{KIND_WITHDRAWN})
                              AND effective_time>@now AND status=0", DB.h("id", id, "now", now));
                currentPublications = null;
            }

            var before = listPublicationsByDate(when);
            int workflow = action switch
            {
                "submit" => WORKFLOW_IN_REVIEW,
                "changes" => WORKFLOW_CHANGES_REQUESTED,
                "publish" => when > now ? WORKFLOW_SCHEDULED : WORKFLOW_PUBLISHED,
                _ => WORKFLOW_DRAFT
            };
            if (action == "publish")
            {
                item["status"] = STATUS_ACTIVE;
                var after = new Dictionary<int, FwDict>(before)
                {
                    [id] = item
                };
                validatePublication(id, item, after, when);
                revisionId = addRevision(id, KIND_PUBLISHED, Utils.jsonEncode(FormUtils.filter(item, CONTENT_FIELDS)), when, note, captureSnippetVersions(item, before));
            }
            else if (action == "unpublish")
            {
                if (item["is_snippet"].toBool() && listSnippetUsages(item["url"].toStr(), true).Count > 0)
                {
                    throw new UserException("This snippet is used by published or scheduled pages. Replace those references first.");
                }

                item["status"] = STATUS_INACTIVE;
                revisionId = addRevision(id, KIND_WITHDRAWN, Utils.jsonEncode(FormUtils.filter(item, CONTENT_FIELDS)), now, note);
            }
            else if (action != "cancel")
            {
                int kind = action == "submit" ? KIND_SUBMITTED : KIND_CHANGES_REQUESTED;
                revisionId = addRevision(id, kind, Utils.jsonEncode(FormUtils.filter(item, CONTENT_FIELDS)), now, note, captureSnippetVersions(item, listPublicationsByDate()));
            }

            db.update(table_name, new FwDict
            {
                ["workflow"] = workflow,
                ["review_note"] = note.Length > 1000 ? note[..1000] : note,
                ["upd_time"] = now,
                ["upd_users_id"] = fw.userId
            }, DB.h("id", id));
            if (isRelease)
            {
                currentPublications = null;
                validateTimeline();
            }

            db.commit();
        }
        catch
        {
            db.rollback();
            clearCmsCache();
            throw;
        }

        clearCmsCache();
        if (action is "publish" or "unpublish" or "cancel")
        {
            try
            {
                fw.model<RagSources>().queueSpage(id);
            }
            catch (Exception ex)
            {
                fw.logger(LogLevel.WARN, "CMS indexing queue unavailable:", ex.Message);
            }
        }

        return revisionId;
    }

    /// <summary>Append a snapshot. Only cancellation changes an existing revision's status; its content is immutable.</summary>
    public int addRevision(int id, int kind, string snapshot, DateTime effective, string note, string snippets = "")
    {
        requireAuthor(kind is KIND_PUBLISHED or KIND_WITHDRAWN or KIND_ORIGINAL);
        return db.insert(REVISION_TABLE, new FwDict
        {
            ["spages_id"] = id,
            ["kind"] = kind,
            ["snapshot_json"] = snapshot,
            ["effective_time"] = effective,
            ["note"] = note.Length > 1000 ? note[..1000] : note,
            ["snippet_versions"] = snippets,
            ["add_time"] = now,
            ["add_users_id"] = fw.userId
        });
    }

    public bool isScheduled(int id) => db.value(REVISION_TABLE, new FwDict
    {
        ["spages_id"] = id,
        ["kind"] = KIND_PUBLISHED,
        ["status"] = STATUS_ACTIVE,
        ["effective_time"] = db.opGT(now)
    }, "id").toInt() > 0;

    public static string workflowLabel(int workflow) => workflow switch
    {
        WORKFLOW_IN_REVIEW => "In review",
        WORKFLOW_CHANGES_REQUESTED => "Changes requested",
        WORKFLOW_PUBLISHED => "Published",
        WORKFLOW_SCHEDULED => "Scheduled",
        _ => "Draft"
    };

    private void lockPublicationNamespace()
    {
        // Lock one stable page row while validating the shared path and snippet-key namespace.
        // Resolve the ID separately: MySQL rejects an UPDATE with a direct subquery of the same table.
        int id = db.value(table_name, new FwDict(), "MIN(id)").toInt();
        if (id > 0)
        {
            db.exec($"UPDATE {qTable()} SET prio=prio WHERE id=@id", DB.h("id", id));
        }
    }

    private void validateTimeline()
    {
        var boundaries = db.arrayp($@"SELECT DISTINCT effective_time FROM {db.qid(REVISION_TABLE)}
            WHERE status=0 AND kind IN ({KIND_PUBLISHED},{KIND_WITHDRAWN}) AND effective_time>@now", DB.h("now", now)).Select(x => publicationDate(x["effective_time"], now)).Prepend(now);
        foreach (var boundary in boundaries)
        {
            var pages = listPublicationsByDate(boundary);
            validatePublicationPaths(pages);
            var aliases = listAliasTargets(boundary, pages);
            var paths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var content in pages.Values.Where(x => isVisible(x, pages, 100) && effectiveAccess(x, pages) > 0))
            {
                foreach (int attachment in listPageAttachmentIds(content))
                {
                    attachmentUrl(attachment, content, true, false, pages);
                }
            }

            foreach (var page in pages.Values.Where(x => !x["is_snippet"].toBool() && isVisible(x, pages, 100)))
            {
                int id = page["id"].toInt();
                string path = publishedUrl(id, pages);
                if (paths.TryGetValue(path, out int owner) && owner != id)
                {
                    throw new UserException("A scheduled or published page would own the same URL: " + path);
                }

                paths[path] = id;
                foreach (var key in SpagesContent.listSnippetKeys(SpagesContent.parse(page["content_json"].toStr())))
                {
                    var snippet = pages.Values.FirstOrDefault(x => x["is_snippet"].toBool() && x["url"].toStr() == key);
                    if (snippet == null || !isVisible(snippet, pages, 100) || effectiveAccess(snippet, pages) > effectiveAccess(page, pages))
                    {
                        throw new UserException("The change would make snippet '" + key + "' unavailable to '" + page["iname"].toStr() + "'.");
                    }
                }

                if (aliases.TryGetValue(path, out int aliasOwner) && aliasOwner != id)
                {
                    throw new UserException("A redirect already owns this page URL: " + path);
                }

                if (page["redirect_url"].toStr().Length > 0)
                {
                    validateRedirect(path, page["redirect_url"].toStr(), pages, aliases);
                }
            }
        }
    }

    /// <summary>Check complete published ancestries before visibility filtering; a withdrawn ancestor intentionally hides its descendants.</summary>
    private void validatePublicationPaths(Dictionary<int, FwDict> pages)
    {
        foreach (var page in pages.Values.Where(page => !page["is_snippet"].toBool()))
        {
            int id = page["id"].toInt();
            var seen = new HashSet<int>();
            while (id > 0 && pages.TryGetValue(id, out var ancestor))
            {
                if (!seen.Add(id) || ancestor["is_snippet"].toBool())
                {
                    throw new UserException("Page hierarchy contains a cycle or an invalid parent.");
                }

                id = ancestor["parent_id"].toInt();
            }

            if (id > 0)
            {
                continue;
            }

            if (seen.Count > 20)
            {
                throw new UserException("Page hierarchy is too deep (maximum 20 levels).");
            }

            localPath(publishedUrl(page["id"].toInt(), pages));
        }
    }

    internal void clearCmsCache()
    {
        currentPublications = null;
        fw.cache.requestRemoveWithPrefix(cache_prefix);
        FwCache.remove("home_page");
    }

    private static DateTime publicationDate(object? value, DateTime fallback)
    {
        if (value is DateTime date)
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        return DateTime.TryParse(value.toStr(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : fallback;
    }

    /// <summary>One request/as-of publication view. A withdrawal is a release, so earlier versions never reappear.</summary>
    public Dictionary<int, FwDict> listPublicationsByDate(DateTime? at = null)
    {
        if (at == null && currentPublications != null)
        {
            return currentPublications;
        }

        var releases = db.arrayp($@"
            SELECT r.* FROM {db.qid(REVISION_TABLE)} r
             WHERE r.status=0 AND r.kind IN ({KIND_PUBLISHED},{KIND_WITHDRAWN})
               AND r.effective_time<=@at
               AND NOT EXISTS (
                   SELECT 1 FROM {db.qid(REVISION_TABLE)} n
                    WHERE n.spages_id=r.spages_id AND n.status=0
                      AND n.kind IN ({KIND_PUBLISHED},{KIND_WITHDRAWN}) AND n.effective_time<=@at
                      AND (n.effective_time>r.effective_time OR (n.effective_time=r.effective_time AND n.id>r.id)))", DB.h("at", at ?? now));
        var pages = new Dictionary<int, FwDict>();
        foreach (var release in releases)
        {
            if (release["kind"].toInt() == KIND_WITHDRAWN)
            {
                continue;
            }

            var page = Utils.jsonDecodeDict(release["snapshot_json"].toStr()) ?? new FwDict();
            if (page.Count == 0 || page["status"].toInt() != STATUS_ACTIVE)
            {
                continue;
            }

            int id = release["spages_id"].toInt();
            page["id"] = id;
            page["revision_id"] = release["id"];
            page["pub_time"] = release["effective_time"];
            page["add_time"] = release["add_time"];
            pages[id] = page;
        }

        if (at == null)
        {
            currentPublications = pages;
        }

        return pages;
    }

    public bool isVisible(FwDict page, Dictionary<int, FwDict>? pages = null, int? audience = null)
    {
        if (page.Count == 0)
        {
            return false;
        }

        pages ??= listPublicationsByDate();
        int level = audience ?? (fw.userId > 0 ? fw.userAccessLevel : 0);
        var seen = new HashSet<int>();
        var current = page;
        while (current.Count > 0)
        {
            if (current["status"].toInt() != STATUS_ACTIVE || current["access_level"].toInt() > level || !seen.Add(current["id"].toInt()) || seen.Count > 20)
            {
                return false;
            }

            int parent = current["parent_id"].toInt();
            if (parent == 0)
            {
                return true;
            }

            if (!pages.TryGetValue(parent, out current!))
            {
                return false;
            }
        }

        return false;
    }

    public FwDict onePublished(int id, int? audience = null)
    {
        var pages = listPublicationsByDate();
        return pages.TryGetValue(id, out var page) && isVisible(page, pages, audience) ? new(page) : [];
    }

    public FwList listPublished(int? audience = null)
    {
        var pages = listPublicationsByDate();
        var result = new FwList();
        foreach (var page in pages.Values.Where(x => !x["is_snippet"].toBool() && isVisible(x, pages, audience)).OrderByDescending(x => x["prio"].toInt()).ThenBy(x => x["iname"].toStr()))
        {
            var item = new FwDict(page);
            item["full_url"] = publishedUrl(page["id"].toInt(), pages);
            result.Add(item);
        }

        return result;
    }

    /// <summary>Indexable current pages for the specified audience, excluding redirect pages.</summary>
    public FwList listIndexable(int? audience = null)
    {
        return new FwList(listPublished(audience).Where(page => !page["is_noindex"].toBool() && page["redirect_url"].toStr().Length == 0));
    }

    public string publishedUrl(int id, Dictionary<int, FwDict>? pages = null)
    {
        pages ??= listPublicationsByDate();
        var parts = new List<string>();
        var seen = new HashSet<int>();
        while (id > 0)
        {
            if (!seen.Add(id) || seen.Count > 20 || !pages.TryGetValue(id, out var row) || row["is_snippet"].toBool())
            {
                return "";
            }

            if (!row["is_home"].toBool())
            {
                parts.Insert(0, row["url"].toStr());
            }

            id = row["parent_id"].toInt();
        }

        return "/" + string.Join("/", parts);
    }

    public FwDict onePublishedByPath(string path, int? audience = null)
    {
        path = localPath(path);
        var pages = listPublicationsByDate();
        foreach (var page in pages.Values)
        {
            if (!page["is_snippet"].toBool() && string.Equals(publishedUrl(page["id"].toInt(), pages), path, StringComparison.OrdinalIgnoreCase) && isVisible(page, pages, audience))
            {
                var item = new FwDict(page);
                item["full_url"] = path;
                var breadcrumbs = new FwList();
                int id = page["id"].toInt();
                var seen = new HashSet<int>();
                while (id > 0 && seen.Add(id) && pages.TryGetValue(id, out var parent))
                {
                    if (!parent["is_home"].toBool())
                    {
                        breadcrumbs.Insert(0, new FwDict
                        {
                            ["iname"] = parent["iname"],
                            ["url"] = publishedUrl(id, pages),
                            ["is_current"] = id == page["id"].toInt()
                        });
                    }

                    id = parent["parent_id"].toInt();
                }

                item["breadcrumbs"] = breadcrumbs;
                if (breadcrumbs.Count > 0)
                {
                    item["top_page"] = breadcrumbs[0];
                }

                return item;
            }
        }

        return[];
    }

    public override bool isAccess(int id = 0, string action = "")
    {
        if (action is "edit" or "add" or "delete" or "link")
        {
            return isAuthor();
        }

        return onePublished(id).Count > 0;
    }

    /// <summary>Page-owned files are public only while referenced by an eligible publication. Editors can inspect draft files.</summary>
    public bool isAttachmentVisible(int attachmentId, int pageId, string action = "")
    {
        if (isPreviewAllowed())
        {
            return db.row(table_name, DB.h("id", pageId)).Count > 0;
        }

        var page = onePublished(pageId);
        if (page.Count == 0)
        {
            return false;
        }

        return listPageAttachmentIds(page).Contains(attachmentId);
    }

    private HashSet<int> listPageAttachmentIds(FwDict page)
    {
        var doc = SpagesContent.parse(page["content_json"].toStr());
        var ids = SpagesContent.listAttachmentIds(doc);
        if (page["head_att_id"].toInt() > 0)
        {
            ids.Add(page["head_att_id"].toInt());
        }

        // Legacy Markdown and inline links may still point at existing library files.
        var html = new HtmlParser().ParseDocument(string.Join("", SpagesContent.Regions.Select(region => SpagesContent.renderRegion(doc, region, new(_ => "", id => "/__cms_attachment/" + id)))));
        foreach (var link in html.QuerySelectorAll("a[href],img[src]"))
        {
            int id = attachmentIdByUrl(link.GetAttribute(link.LocalName == "img" ? "src" : "href") ?? "");
            if (id > 0)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    public int effectiveAccess(FwDict page, Dictionary<int, FwDict>? pages = null)
    {
        pages ??= listPublicationsByDate();
        int level = 0;
        var seen = new HashSet<int>();
        while (page.Count > 0)
        {
            level = Math.Max(level, page["access_level"].toInt());
            if (!seen.Add(page["id"].toInt()) || seen.Count > 20)
            {
                return 101;
            }

            int parent = page["parent_id"].toInt();
            if (parent == 0)
            {
                return level;
            }

            if (!pages.TryGetValue(parent, out page!))
            {
                return 101;
            }
        }

        return 101;
    }

    public static string localPath(string path)
    {
        path = path.Trim();
        if (!path.StartsWith('/') || path.StartsWith("//") || path.Contains('\\') || path.Any(char.IsControl) || path.Contains('?') || path.Contains('#') || path.Length > 450 || path.Split('/').Any(x => x is "." or ".."))
        {
            throw new UserException("Use an app-local path without a query or fragment (maximum 450 characters).");
        }

        var decoded = Uri.UnescapeDataString(path);
        if (decoded != path && (decoded.Contains('\\') || decoded.Contains('%') || decoded.Contains('?') || decoded.Contains('#') || decoded.StartsWith("//") || decoded.Any(char.IsControl) || decoded.Split('/').Any(x => x is "." or "..") || decoded.Count(x => x == '/') != path.Count(x => x == '/')))
        {
            throw new UserException("Unsafe local URL.");
        }

        return decoded.Length > 1 ? decoded.TrimEnd('/') : decoded;
    }

    private void validatePublication(int id, FwDict item, Dictionary<int, FwDict> pages, DateTime when)
    {
        int parent = item["parent_id"].toInt();
        if (parent > 0 && (!pages.TryGetValue(parent, out var parentPage) || parentPage["is_snippet"].toBool()))
        {
            throw new UserException("Publish the parent page first.");
        }

        string path = item["is_snippet"].toBool() ? "" : publishedUrl(id, pages);
        if (!item["is_snippet"].toBool())
        {
            if (path.Length == 0)
            {
                throw new UserException("Page hierarchy contains a cycle or is too deep.");
            }

            localPath(path);
            var first = path.Trim('/').Split('/')[0];
            bool isReserved = first.Length > 0 && (new[]
            {
                "admin",
                "dev",
                "att",
                "api",
                "my",
                "assets",
                "upload",
                "sitemap.xml",
                "robots.txt"
            }.Contains(first.ToLowerInvariant()) || typeof(FW).Assembly.GetTypes().Any(t => t.Name.Equals(first + "Controller", StringComparison.OrdinalIgnoreCase)));
            if (isReserved && path != getFullUrl(id))
            {
                throw new UserException("This path belongs to an application route.");
            }

            foreach (var other in pages.Where(x => x.Key != id && !x.Value["is_snippet"].toBool()))
            {
                if (string.Equals(path, publishedUrl(other.Key, pages), StringComparison.OrdinalIgnoreCase))
                {
                    throw new UserException("This page URL is already published or scheduled.");
                }
            }
        }

        var doc = SpagesContent.parse(item["content_json"].toStr());
        var context = contentContext(item, pages, true, false);
        SpagesContent.validate(doc, item["template"].toStr(), context, item["is_snippet"].toBool());
        if (item["head_att_id"].toInt() > 0)
        {
            context.ImageAttachment!(item["head_att_id"].toInt());
        }
    }

    private SpagesContent.RenderContext contentContext(FwDict page, Dictionary<int, FwDict> pages, bool isPublishing = false, bool isPreview = false)
    {
        return new(key => renderSnippet(key, page, pages, isPublishing, isPreview), id => attachmentUrl(id, page, isPublishing, isPreview, pages), isPublishing, id => attachmentUrl(id, page, isPublishing, isPreview, pages, true));
    }

    /// <summary>Reject invalid media on approval; omit unavailable media during reads without exposing an unauthorized URL.</summary>
    private string attachmentUrl(int id, FwDict page, bool isPublishing, bool isPreview, Dictionary<int, FwDict> pages, bool isImage = false)
    {
        if (id <= 0)
        {
            if (!isPublishing)
            {
                return "";
            }

            throw new UserException("Select an attachment.");
        }

        var att = fw.model<Att>();
        var row = att.one(id);
        if (row.Count == 0 || row["status"].toInt() != STATUS_ACTIVE)
        {
            if (!isPublishing)
            {
                return "";
            }

            throw new UserException("This attachment is unavailable.");
        }

        if (isImage && !row["is_image"].toBool())
        {
            if (!isPublishing)
            {
                return "";
            }

            throw new UserException("Choose a decoded image file for an image block.");
        }

        int entity = fw.model<FwEntities>().idByIcode(FwEntities.ICODE_SPAGE);
        bool isOwned = row["fwentities_id"].toInt() == entity && row["item_id"].toInt() == page["id"].toInt();
        if (!isOwned)
        {
            try
            {
                att.checkAccess(id, Att.ACCESS_ACTION_LINK, entity, page["id"].toInt());
            }
            catch (AuthException) when (!isPublishing)
            {
                return "";
            }
        }

        if (isPublishing && effectiveAccess(page, pages) > 0 && !isOwned)
        {
            throw new UserException("Restricted pages need page-owned uploads; shared library files are public.");
        }

        return fw.config("ROOT_URL").toStr() + "/Att/" + row["icode"].toStr();
    }

    /// <summary>Render an eligible named snippet for an application template; missing/restricted snippets produce no output.</summary>
    public string renderSnippet(string key)
    {
        var pages = listPublicationsByDate();
        var snippet = pages.Values.FirstOrDefault(x => x["is_snippet"].toBool() && x["url"].toStr().Equals(key, StringComparison.OrdinalIgnoreCase));
        return snippet != null && isVisible(snippet, pages) ? renderSnippet(key, snippet, pages) : "";
    }

    public string renderSnippet(string key, FwDict page, Dictionary<int, FwDict>? pages = null, bool isPublishing = false, bool isPreview = false)
    {
        pages ??= listPublicationsByDate();
        var snippet = pages.Values.FirstOrDefault(x => x["is_snippet"].toBool() && string.Equals(x["url"].toStr(), key, StringComparison.OrdinalIgnoreCase));
        if (snippet == null)
        {
            if (isPreview)
            {
                return "<aside class=\"spage-callout\">Snippet unavailable in this preview: " + SpagesContent.escape(key) + "</aside>";
            }

            throw new UserException("Publish the '" + key + "' snippet before using it.");
        }

        if (effectiveAccess(snippet, pages) > effectiveAccess(page, pages))
        {
            throw new UserException("This snippet is more restricted than the page.");
        }

        if (!isPublishing && !isPreview && !isVisible(snippet, pages))
        {
            return "";
        }

        var doc = SpagesContent.parse(snippet["content_json"].toStr());
        if (SpagesContent.listSnippetKeys(doc).Count > 0)
        {
            throw new UserException("Nested snippets are not supported.");
        }

        return SpagesContent.renderRegion(doc, "main", contentContext(snippet, pages, isPublishing, isPreview));
    }

    internal int attachmentIdByUrl(string url)
    {
        var prefix = fw.config("ROOT_URL").toStr().TrimEnd('/') + "/Att/";
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        string code = url[prefix.Length..].Split('?', '#')[0];
        if (!Regex.IsMatch(code, @"^[a-zA-Z0-9_-]+$"))
        {
            return 0;
        }

        return db.value("att", DB.h("icode", code, "status", STATUS_ACTIVE), "id").toInt();
    }

    private string captureSnippetVersions(FwDict item, Dictionary<int, FwDict> pages)
    {
        var versions = new FwDict();
        foreach (string key in SpagesContent.listSnippetKeys(SpagesContent.parse(item["content_json"].toStr())))
        {
            var snippet = pages.Values.FirstOrDefault(x => x["is_snippet"].toBool() && x["url"].toStr() == key);
            if (snippet != null)
            {
                versions[key] = new FwDict
                {
                    ["id"] = snippet["id"],
                    ["revision_id"] = snippet["revision_id"]
                };
            }
        }

        return Utils.jsonEncode(versions);
    }

    public FwList listSnippetUsages(string key, bool isIncludeScheduled = false)
    {
        var pages = listPublicationsByDate();
        if (isIncludeScheduled)
        {
            foreach (var r in db.arrayp($"SELECT * FROM {db.qid(REVISION_TABLE)} WHERE kind={KIND_PUBLISHED} AND status=0 AND effective_time>@now", DB.h("now", now)))
            {
                var item = Utils.jsonDecodeDict(r["snapshot_json"].toStr()) ?? [];
                item["id"] = r["spages_id"];
                pages = new(pages)
                {
                    [-r["id"].toInt()] = item
                };
            }
        }

        var result = new FwList();
        var seen = new HashSet<int>();
        foreach (var item in pages.Values)
        {
            if (!item["is_snippet"].toBool() && SpagesContent.listSnippetKeys(SpagesContent.parse(item["content_json"].toStr())).Contains(key) && seen.Add(item["id"].toInt()))
            {
                result.Add(new FwDict(item));
            }
        }

        return result;
    }

    /// <summary>Public and preview rendering share this data builder. Historical previews pin recorded snippet revisions.</summary>
    public FwDict buildPageState(FwDict item, bool isPreview = false, bool isHistorical = false)
    {
        var pages = listPublicationsByDate();
        if (isHistorical && Utils.jsonDecodeDict(item["snippet_versions"].toStr())is FwDict versions)
        {
            pages = pages.Where(x => !x.Value["is_snippet"].toBool()).ToDictionary(x => x.Key, x => x.Value);
            foreach (var value in versions.Values.OfType<FwDict>())
            {
                pages[value["id"].toInt()] = oneRevisionOrFail(value["id"].toInt(), value["revision_id"].toInt());
            }
        }

        var page = new FwDict(item);
        string template = page["template"].toStr();
        if (template.Length == 0)
        {
            template = "article";
        }

        page["template"] = template;
        var doc = SpagesContent.parse(page["content_json"].toStr());
        var context = contentContext(page, pages, false, isPreview);
        foreach (var region in SpagesContent.Regions)
        {
            page["html_" + region] = SpagesContent.renderRegion(doc, region, context);
        }

        page["html_after_content"] = doc["slots"]?["after_content"] is JsonValue slot && slot.ToString().Length > 0 ? context.Snippet!(slot.ToString()) : "";
        foreach (var name in SpagesContent.layout(template).Slots)
        {
            page["html_slot_" + name] = doc["slots"]?[name] is JsonValue value && value.ToString().Length > 0 ? context.Snippet!(value.ToString()) : "";
        }

        page["is_col1"] = template is "article" or "landing";
        page["is_col2_left"] = template == "sidebar-left";
        page["is_col2_right"] = template == "sidebar-right";
        page["is_col3"] = template == "three-column";
        if (page["head_att_id"].toInt() > 0)
        {
            page["head_att_id_url"] = context.ImageAttachment!(page["head_att_id"].toInt());
        }

        var nav = listPublished();
        page["is_landing"] = template == "landing";
        return new FwDict
        {
            ["cms"] = true,
            ["page"] = page,
            ["pages"] = new FwList(nav.Where(x => x["is_nav_visible"].toInt(1) == 1)),
            ["subpages"] = new FwList(nav.Where(x => x["is_nav_visible"].toInt(1) == 1 && x["parent_id"].toInt() == page["id"].toInt())),
            ["hide_sidebar"] = true,
            ["hide_std_sidebar"] = true,
            ["is_preview"] = isPreview,
            ["is_page_published"] = !isPreview,
            ["is_can_edit"] = isAuthor(),
            ["meta_keywords"] = page["meta_keywords"],
            ["meta_description"] = page["meta_description"],
            ["meta_title"] = page["meta_title"],
            ["canonical_url"] = isPreview ? "" : canonicalUrl(page),
            ["is_noindex"] = isPreview || page["is_noindex"].toBool() || effectiveAccess(page) > 0
        };
    }

    public void showCmsPage(string path)
    {
        // Published aliases and redirect destinations can change, so redirects must not be stored.
        fw.cache_control = fw.userId > 0 ? "private, no-store" : "no-store";
        fw.response.Headers.CacheControl = fw.cache_control;

        var page = onePublishedByPath(path);
        if (page.Count == 0)
        {
            var redirect = resolveRedirect(path);
            if (redirect.Length > 0)
            {
                fw.response.StatusCode = 301;
                fw.response.Headers.Location = fw.config("ROOT_URL").toStr() + redirect;
                return;
            }

            throw new NotFoundException();
        }

        if (page["redirect_url"].toStr().Length > 0)
        {
            fw.response.StatusCode = 301;
            fw.response.Headers.Location = fw.config("ROOT_URL").toStr() + localPath(page["redirect_url"].toStr());
            return;
        }

        fw.cache_control = fw.userId > 0 || effectiveAccess(page) > 0 ? "private, no-store" : "no-cache";
        fw.response.Headers.CacheControl = fw.cache_control;
        fw.parser("/home/spage", buildPageState(page));
    }

    /// <summary>Uses a deployment-configured origin; never trusts the request Host header for search engine metadata.</summary>
    public string canonicalUrl(FwDict page)
    {
        string origin = fw.config("ROOT_DOMAIN").toStr().TrimEnd('/');
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
        {
            return "";
        }

        return origin + publishedUrl(page["id"].toInt());
    }

    /// <summary>Effective page text, including current shared snippets, for search and optional downstream retrieval.</summary>
    public string publishedText(FwDict page)
    {
        var state = buildPageState(page, true);
        var rendered = (FwDict)state["page"]!;
        return page["iname"].toStr() + "\n" + string.Join("\n", new[]
        {
            "main",
            "left",
            "right",
            "after_content"
        }.Select(x => SpagesContent.plainText(rendered["html_" + x].toStr())));
    }

    public string resolveRedirect(string source)
    {
        source = localPath(source);
        var pages = listPublicationsByDate();
        var aliases = listAliasTargets(now, pages);
        if (!aliases.TryGetValue(source, out int id) || !pages.TryGetValue(id, out var page) || !isVisible(page, pages))
        {
            return "";
        }

        string target = publishedUrl(id, pages);
        return target.Equals(source, StringComparison.OrdinalIgnoreCase) ? "" : target;
    }

    private void validateRedirect(string source, string target, Dictionary<int, FwDict> pages, Dictionary<string, int> aliases)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            localPath(source)
        };
        string path = localPath(target);
        for (int i = 0; i < 20; i++)
        {
            if (!seen.Add(path))
            {
                throw new UserException("This redirect would create a loop.");
            }

            var page = pages.Values.FirstOrDefault(item => !item["is_snippet"].toBool() && publishedUrl(item["id"].toInt(), pages).Equals(path, StringComparison.OrdinalIgnoreCase));
            if (page != null)
            {
                if (page["redirect_url"].toStr().Length == 0)
                {
                    return;
                }

                path = localPath(page["redirect_url"].toStr());
            }
            else if (aliases.TryGetValue(path, out int id))
            {
                path = publishedUrl(id, pages);
            }
            else
            {
                return; // A local application route may own the destination.
            }
        }

        throw new UserException("Redirect chain is too long.");
    }

    /// <summary>Validate one local path per line. Aliases belong to the draft and take effect with its publication.</summary>
    public static List<string> listUrlAliases(FwDict page)
    {
        var aliases = page["url_aliases"].toStr().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => localPath(line.Trim())).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (page["is_snippet"].toBool() && aliases.Count > 0)
        {
            throw new UserException("Snippets do not have public URL aliases.");
        }

        foreach (string alias in aliases)
        {
            if (isReservedPath(alias))
            {
                throw new UserException("This alias belongs to an application route: " + alias);
            }
        }

        return aliases;
    }

    /// <summary>
    /// Resolve aliases only on a route miss or publication validation. Historical paths are derived at actual
    /// publication boundaries, including descendant moves; cancelled schedules never contribute a path.
    /// </summary>
    private Dictionary<string, int> listAliasTargets(DateTime at, Dictionary<int, FwDict> pages)
    {
        var aliases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var timeline = new Dictionary<int, FwDict>();
        var releases = db.arrayp($@"SELECT * FROM {db.qid(REVISION_TABLE)}
            WHERE status=0 AND kind IN ({KIND_PUBLISHED},{KIND_WITHDRAWN}) AND effective_time<=@at
            ORDER BY effective_time,id", DB.h("at", at));
        foreach (var group in releases.GroupBy(row => publicationDate(row["effective_time"], DateTime.UnixEpoch)))
        {
            // Schedules sharing a boundary become visible together. Immediate approvals are separate
            // commits, even when their timestamp falls within the same second.
            foreach (var release in group.Where(row => publicationDate(row["add_time"], DateTime.UnixEpoch) < group.Key))
            {
                applyRelease(release);
            }
            capturePaths();
            foreach (var release in group.Where(row => publicationDate(row["add_time"], DateTime.UnixEpoch) >= group.Key))
            {
                applyRelease(release);
                capturePaths();
            }
        }

        foreach (var page in pages.Values.Where(page => !page["is_snippet"].toBool() && isVisible(page, pages, Users.ACL_SITEADMIN)))
        {
            foreach (string alias in listUrlAliases(page))
            {
                addAlias(alias, page["id"].toInt());
            }
        }
        return aliases;

        void applyRelease(FwDict release)
        {
            int id = release["spages_id"].toInt();
            timeline.Remove(id);
            if (release["kind"].toInt() != KIND_PUBLISHED)
                return;
            var page = Utils.jsonDecodeDict(release["snapshot_json"].toStr()) ?? new FwDict();
            page["id"] = id;
            timeline[id] = page;
        }

        void capturePaths()
        {
            foreach (var page in timeline.Values)
            {
                int id = page["id"].toInt();
                if (page["is_snippet"].toBool() || !isVisible(page, timeline, Users.ACL_SITEADMIN) || !pages.TryGetValue(id, out var current) || !isVisible(current, pages, Users.ACL_SITEADMIN))
                {
                    continue;
                }

                string path = publishedUrl(id, timeline);
                if (path.Length == 0 || isReservedPath(path))
                {
                    continue;
                }

                addAlias(path, id);
            }
        }

        void addAlias(string path, int id)
        {
            if (aliases.TryGetValue(path, out int owner) && owner != id)
            {
                throw new UserException("Another page owns this URL or alias: " + path);
            }

            aliases[path] = id;
        }
    }

    private static bool isReservedPath(string path)
    {
        string first = path.Trim('/').Split('/')[0];
        return first.Length == 0 || new[]
        {
            "admin",
            "dev",
            "att",
            "api",
            "my",
            "assets",
            "upload",
            "sitemap.xml",
            "robots.txt"
        }.Contains(first.ToLowerInvariant()) || typeof(FW).Assembly.GetTypes().Any(t => t.Name.Equals(first + "Controller", StringComparison.OrdinalIgnoreCase));
    }

}
