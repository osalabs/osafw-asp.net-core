// Static Pages model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;
using System.Text;
using System.Text.RegularExpressions;

namespace osafw;

public class Spages : FwModel
{
    public Spages() : base()
    {
        table_name = "spages";
    }

    // delete record, but don't allow to delete home page
    public override void delete(int id, bool is_perm = false)
    {
        var item_old = one(id);
        // home page cannot be deleted
        if ((string)item_old["is_home"] != "1")
            base.delete(id, is_perm);
    }

    public bool isExistsByUrl(string url, int parent_id, int not_id)
    {
        Hashtable where = new();
        where["parent_id"] = parent_id;
        where["url"] = url;
        where["id"] = db.opNOT(not_id);

        int val = Utils.f2int(db.value(table_name, where, "id"));
        if (val > 0)
            return true;
        else
            return false;
    }

    // retun one latest record by url (i.e. with most recent pub_time if there are more than one page with such url)
    public Hashtable oneByUrl(string url, int parent_id)
    {
        Hashtable where = new()
        {
            ["parent_id"] = parent_id,
            ["url"] = url
        };
        return db.row(table_name, where, "pub_time desc");
    }

    // return one latest record by full_url (i.e. relative url from root, without domain)
    public Hashtable oneByFullUrl(string full_url)
    {
        string[] url_parts = full_url.Split("/");
        int parent_id = 0;
        var breadcrumbs = new ArrayList();
        var item_full_url = "";


        Hashtable item = new();
        for (int i = 1; i <= url_parts.GetUpperBound(0); i++)
        {
            item = oneByUrl(url_parts[i], parent_id);
            if (item.Count == 0)
                return item;// empty hashtable
            parent_id = Utils.f2int(item["id"]);

            item_full_url += "/" + item["url"];
            breadcrumbs.Add(new Hashtable {
                { "iname", item["iname"] },
                { "url", item_full_url }
            });
        }
        // item now contains page data for the url
        if (item.Count > 0)
        {
            if (!Utils.isEmpty(item["head_att_id"]))
                item["head_att_id_url"] = fw.model<Att>().getUrl(Utils.f2int(item["head_att_id"]));
        }

        // page[top_url] used in templates navigation
        if (url_parts.GetUpperBound(0) >= 1)
            item["top_url"] = url_parts[1].ToLower();

        item["full_url"] = item_full_url;
        item["breadcrumbs"] = breadcrumbs;

        // columns
        if (!Utils.isEmpty(item["idesc_left"]))
        {
            if (!Utils.isEmpty(item["idesc_right"]))
                item["is_col3"] = true;
            else
                item["is_col2_left"] = true;
        }
        else if (!Utils.isEmpty(item["idesc_right"]))
            item["is_col2_right"] = true;
        else
            item["is_col1"] = true;

        return item;
    }

    public ArrayList listChildren(int parent_id)
    {
        var where = new Hashtable {
            { "status", db.opNOT(FwModel.STATUS_DELETED)},
            { "parent_id", parent_id}
        };
        return db.array(table_name, where, "iname");
    }

    /// <summary>
    /// Read ALL rows from db according to where, then apply getPagesTree to return tree structure
    /// </summary>
    /// <param name="where">where to apply in sql</param>
    /// <param name="orderby">order by fields to apply in sql</param>
    /// <returns>parsepage AL with hierarcy (via "children" key)</returns>
    /// <remarks></remarks>
    public ArrayList tree(string where, Hashtable list_where_params, string orderby)
    {
        ArrayList rows = db.arrayp("select * from " + db.qid(table_name) +
                                   " where " + where +
                                   " order by " + orderby, list_where_params);
        ArrayList pages_tree = getPagesTree(rows, 0);
        return pages_tree;
    }

    // return parsepage array list of rows with hierarcy (children rows added to parents as "children" key)
    // RECURSIVE!
    public ArrayList getPagesTree(ArrayList rows, int parent_id, int level = 0, string parent_url = "")
    {
        ArrayList result = new();

        foreach (Hashtable row in rows)
        {
            if (parent_id == Utils.f2int(row["parent_id"]))
            {
                Hashtable row2 = (Hashtable)row.Clone();
                row2["_level"] = level;
                // row2["_level1"] level + 1 'to easier use in templates
                row2["full_url"] = parent_url + "/" + row["url"];
                row2["children"] = getPagesTree(rows, Utils.f2int(row["id"]), level + 1, (string)row["url"]);
                result.Add(row2);
            }
        }

        return result;
    }

    /// <summary>
    /// Generate parsepage AL of plain list with levelers based on tree structure from getPagesTree()
    /// </summary>
    /// <param name="pages_tree">result of get_pages_tree()</param>
    /// <param name="level">optional, used in recursive calls</param>
    /// <returns>parsepage AL with "leveler" array added to each row with level>0</returns>
    /// <remarks>RECURSIVE</remarks>
    public ArrayList getPagesTreeList(ArrayList pages_tree, int level = 0)
    {
        ArrayList result = new();

        if (pages_tree != null)
        {
            foreach (Hashtable row in pages_tree)
            {
                result.Add(row);
                // add leveler
                if (level > 0)
                {
                    ArrayList leveler = new();
                    for (int i = 1; i <= level; i++)
                        leveler.Add(new Hashtable());
                    row["leveler"] = leveler;
                }
                // subpages
                result.AddRange(getPagesTreeList((ArrayList)row["children"], level + 1));
            }
        }

        return result;
    }

    /// <summary>
    /// Generate HTML with options for select with indents for hierarcy
    /// </summary>
    /// <param name="selected_id">selected id</param>
    /// <param name="pages_tree">result of getPagesTree()</param>
    /// <param name="level">optional, used in recursive calls</param>
    /// <returns>HTML with options</returns>
    /// <remarks>RECURSIVE</remarks>
    public string getPagesTreeSelectHtml(string selected_id, ArrayList pages_tree, int level = 0)
    {
        StringBuilder result = new();
        if (pages_tree != null)
        {
            foreach (Hashtable row in pages_tree)
            {
                result.AppendLine("<option value=\"" + row["id"] + "\"" + ((string)row["id"] == selected_id ? " selected=\"selected\" " : "") + ">" + Utils.strRepeat("&#8212; ", level) + row["iname"] + "</option>");
                // subpages
                result.Append(getPagesTreeSelectHtml(selected_id, (ArrayList)row["children"], level + 1));
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Return full url (without domain) for the page item, including url of the page
    /// </summary>
    /// <param name="id">record id</param>
    /// <returns>URL like /page/subpage/subsubpage</returns>
    /// <remarks>RECURSIVE!</remarks>
    public string getFullUrl(int id)
    {
        if (id == 0)
            return "";

        var item = one(id);
        return getFullUrl(Utils.f2int(item["parent_id"])) + "/" + item["url"];
    }

    /// <summary>
    /// return list of parent pages for the page (from topmost to immediate parent)
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public ArrayList listParents(int id)
    {
        ArrayList result = new();
        Hashtable item = one(id);
        while (item.Count > 0)
        {
            var item_id = Utils.f2int(item["id"]);
            if (item_id != id)
                result.Insert(0, item);

            item = one(Utils.f2int(item["parent_id"]));
        }
        return result;
    }


    // render page by full url
    public void showPageByFullUrl(string full_url)
    {
        Hashtable ps = new();

        // for navigation
        var pages_tree = tree("status=0", new Hashtable(), "parent_id, prio, iname"); // published only
        ps["pages"] = getPagesTreeList(pages_tree, 0);

        Hashtable item = oneByFullUrl(full_url);
        if (item.Count == 0 || Utils.f2int(item["status"]) == FwModel.STATUS_DELETED && !fw.model<Users>().isAccessLevel(Users.ACL_ADMIN))
        {
            ps["hide_std_sidebar"] = true;
            fw.parser("/error/404", ps);
            return;
        }

        if (!Utils.isEmpty(item["redirect_url"]))
            fw.redirect((string)item["redirect_url"]);

        var item_id = Utils.f2int(item["id"]);

        // subpages navigation
        ArrayList subpages = listChildrenPublished(item_id);
        ps["subpages"] = subpages;
        foreach (Hashtable subpage in subpages)
        {
            subpage["full_url"] = item["full_url"] + "/" + subpage["url"];
        }

        ps["page"] = item;
        ps["meta_keywords"] = item["meta_keywords"];
        ps["meta_description"] = item["meta_description"];
        ps["is_can_edit"] = fw.model<Users>().isAccessLevel(Users.ACL_MANAGER);
        ps["hide_std_sidebar"] = true; // TODO - control via item[template]
        fw.parser("/home/spage", ps);
    }

    public DBList listChildrenPublished(int parent_id)
    {
        return db.arrayp(@$"select * 
              from {db.qid(table_name)}
             where status=0 
               and (pub_time IS NULL OR pub_time<=GETDATE()) 
               and parent_id=@parent_id
          order by prio desc, iname", DB.h("parent_id", parent_id));
    }


    // check if item exists for a given email
    // Public Overrides Function isExists(uniq_key As Object, not_id As Integer) As Boolean
    // Return isExistsByField(uniq_key, not_id, "email")
    // End Function

    // return correct url - TODO
    public string getUrl(int id, string icode, string url = null)
    {
        if (!string.IsNullOrEmpty(url))
        {
            if (Regex.IsMatch(url, "^/"))
                url = fw.config("ROOT_URL") + url;
            return url;
        }
        else
        {
            icode = str2icode(icode);
            if (!string.IsNullOrEmpty(icode))
                return fw.config("ROOT_URL") + "/Pages/" + icode;
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
}