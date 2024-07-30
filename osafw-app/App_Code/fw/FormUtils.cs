// Form processing framework utils
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static osafw.Utils;

namespace osafw;

public class FormUtils
{
    public const int MAX_PAGE_ITEMS = 25; //default max number of items on list screen

    public static Array getYesNo()
    {
        return qw("No|No Yes|Yes");
    }

    public static Array getYN()
    {
        return qw("N|No Y|Yes");
    }

    public static Array getStates()
    {
        return qw("AL|Alabama AK|Alaska AZ|Arizona AR|Arkansas CA|California CO|Colorado CT|Connecticut DE|Delaware DC|District&nbsp;of&nbsp;Columbia FL|Florida GA|Georgia HI|Hawaii ID|Idaho IL|Illinois IN|Indiana IA|Iowa KS|Kansas KY|Kentucky LA|Louisiana ME|Maine MD|Maryland MA|Massachusetts MI|Michigan MN|Minnesota MS|Mississippi MO|Missouri MT|Montana NE|Nebraska NV|Nevada NH|New&nbsp;Hampshire NJ|New&nbsp;Jersey NM|New&nbsp;Mexico NY|New&nbsp;York NC|North&nbsp;Carolina ND|North&nbsp;Dakota OH|Ohio OK|Oklahoma OR|Oregon PA|Pennsylvania RI|Rhode&nbsp;Island SC|South&nbsp;Carolina SD|South&nbsp;Dakota TN|Tennessee TX|Texas UT|Utah VT|Vermont VA|Virginia WA|Washington WV|West&nbsp;Virgina WI|Wisconsin WY|Wyoming");
    }

    // return radio inputs
    // arr can contain strings or strings with separator "|" for value/text ex: Jan|January,Feb|February
    // separator - what to put after each radio (for ex - "<br>")
    public static string radioOptions(string iname, Array arr, string isel, string separator = "")
    {
        StringBuilder result = new();

        isel = isel.Trim();
        int i = 0;
        string[] av;
        string val;
        string text;
        foreach (string item in arr)
        {
            if (item.Contains('|'))
            {
                av = item.Split('|');
                val = av[0];
                text = av[1];
            }
            else
            {
                val = item;
                text = item;
            }

            result.Append("<label><input type=\"radio\" name=\"" + iname + "\" id=\"" + iname + i + "\" value=\"" + val + "\"");
            if (isel == val.Trim())
                result.Append(" checked ");
            result.Append(">" + text + "</label>" + separator + Environment.NewLine);
            i += 1;
        }

        return result.ToString();
    }

    // arr is ArrayList of Hashes with "id" and "iname" keys, for example rows returned from db.array('select id, iname from ...')
    // "id" key is optional, if not present - iname will be used for values too
    // isel may contain multiple comma-separated values
    public static string selectOptions(ArrayList arr, string isel, bool is_multi = false)
    {
        if (isel == null)
            isel = "";

        string[] asel;
        if (is_multi)
            asel = isel.Split(",");
        else
        {
            asel = new string[1];
            asel[0] = isel;
        }

        int i;
        // trim all asel elements, so it would be simplier to compare
        for (i = 0; i < asel.Length; i++)
            asel[i] = asel[i].Trim();

        string val;
        string text;
        StringBuilder result = new();
        foreach (Hashtable item in arr)
        {
            text = Utils.htmlescape((string)item["iname"]);
            if (item.ContainsKey("id"))
                val = (string)item["id"];
            else
                val = (string)item["iname"];

            result.Append("<option value=\"").Append(Utils.htmlescape(val)).Append('"');
            if (item.ContainsKey("class"))
                result.Append(" class=\"" + item["class"] + "\"");
            if (Array.IndexOf(asel, val.Trim()) != -1)
                result.Append(" selected ");
            result.Append('>').Append(text).Append("</option>" + Environment.NewLine);
        }

        return result.ToString();
    }

    /// <summary>
    /// get name for the value fromt the select template
    /// file format: each line - value|description
    /// ex: selectTplName('/common/sel/status.sel', 127) => 'Deleted'
    /// ex: selectTplName('../status.sel', 127, '/admin/users/index') => 'Deleted'
    /// TODO: refactor to make common code with ParsePage?
    /// </summary>
    /// <param name="tpl_path">path </param>
    /// <param name="sel_id"></param>
    /// <param name="base_path">required if tpl_path is relative (not start with "/"), then base_path used. base_path itself is relative to template root</param>
    /// <returns></returns>
    public static string selectTplName(string tpl_path, string sel_id, string base_path = "")
    {
        string result = "";
        sel_id ??= "";

        if (!tpl_path.StartsWith('/'))
        {
            if (string.IsNullOrEmpty(base_path))
                return ""; // base_path required for relative tpl_path

            tpl_path = base_path + "/" + tpl_path;
        }

        var template = (string)FwConfig.settings["template"];

        // translate to absolute path, without any ../
        var path = System.IO.Path.GetFullPath(template + tpl_path);

        // path traversal validation - check if path is a subpath of FwConfig.settings["template"]
        if (!path.StartsWith(template))
            return "";


        string[] lines = FW.getFileLines(path);
        foreach (string line in lines)
        {
            if (line.Length < 2)
                continue;

            string[] arr = line.Split("|", 2);
            string value = arr[0];
            string desc = arr[1];

            if (desc.Length < 1 | value != sel_id)
                continue;

            // result = ParsePage.RX_LANG.Replace(desc, "$1")
            result = new Regex("`(.+?)`", RegexOptions.Compiled).Replace(desc, "$1");
            break;
        }

        return result;
    }

    /// <summary>
    /// return options for select tag from the template file
    /// file format: each line - value|description
    /// </summary>
    /// <param name="tpl_path"></param>
    /// <param name="base_path">required if tpl_path is relative (not start with "/"), then base_path used. base_path itself is relative to template root</param>
    /// <returns></returns>

    public static ArrayList selectTplOptions(string tpl_path, string base_path = "")
    {
        ArrayList result = [];

        if (!tpl_path.StartsWith('/'))
        {
            if (string.IsNullOrEmpty(base_path))
                return result; // base_path required for relative tpl_path

            tpl_path = base_path + "/" + tpl_path;
        }

        var template = (string)FwConfig.settings["template"];

        // translate to absolute path, without any ../
        var path = System.IO.Path.GetFullPath(template + tpl_path);

        // path traversal validation - check if path is a subpath of FwConfig.settings["template"]
        if (!path.StartsWith(template))
            return result;


        string[] lines = FW.getFileLines(path);
        foreach (var line in lines)
        {
            if (line.Length < 2)
                continue;

            string[] arr = line.Split("|", 2);
            string value = arr[0];
            string desc = arr[1];

            // desc = ParsePage.RX_LANG.Replace(desc, "$1")
            desc = new Regex("`(.+?)`", RegexOptions.Compiled).Replace(desc, "$1");
            result.Add(new Hashtable() { { "id", value }, { "iname", desc } });
        }

        return result;
    }

    public static string cleanInput(string strIn)
    {
        // Replace invalid characters with empty strings.
        return Regex.Replace(strIn, @"[^\w\.\,\:\\\%@\-\/ ]", "");
    }

    // ********************************* validators
    public static bool isEmail(string email)
    {
        string re = @"^[\w\.\-\+\=]+\@(?:\w[\w-]*\.?){1,4}[a-zA-Z]{2,16}$";
        return Regex.IsMatch(email, re);
    }

    // validate phones in forms:
    // (xxx) xxx-xxxx
    // xxx xxx xx xx
    // xxx-xxx-xx-xx
    // xxxxxxxxxx
    public static bool isPhone(string phone)
    {
        string re = @"^\(?\d{3}\)?[\- ]?\d{3}[\- ]?\d{2}[\- ]?\d{2}$";
        return Regex.IsMatch(phone, re);
    }

    // return pager or Nothing if no paging required
    public static ArrayList getPager(long count, int pagenum, object pagesize1 = null)
    {
        int pagesize = MAX_PAGE_ITEMS;
        if (pagesize1 != null)
        {
            pagesize = (int)pagesize1;
        }

        ArrayList pager = null;
        const int PAD_PAGES = 5;

        if (count > pagesize)
        {
            pager = new ArrayList();
            int page_count = (int)Math.Ceiling(count / (double)pagesize);

            var from_page = pagenum - PAD_PAGES;
            if (from_page < 0)
                from_page = 0;

            var to_page = pagenum + PAD_PAGES;
            if (to_page > page_count - 1)
                to_page = page_count - 1;

            for (int i = from_page; i <= to_page; i++)
            {
                Hashtable pager_item = new();
                if (pagenum == i)
                    pager_item["is_cur_page"] = 1;
                pager_item["pagenum"] = i;
                pager_item["pagenum_show"] = i + 1;
                if (i == from_page)
                {
                    if (pagenum > PAD_PAGES)
                        pager_item["is_show_first"] = true;
                    if (pagenum > 0)
                    {
                        pager_item["is_show_prev"] = true;
                        pager_item["pagenum_prev"] = pagenum - 1;
                    }
                }
                else if (i == to_page)
                {
                    if (pagenum < page_count - 1)
                    {
                        pager_item["is_show_next"] = true;
                        pager_item["pagenum_next"] = pagenum + 1;
                    }
                }

                pager.Add(pager_item);
            }
        }

        return pager;
    }

    // if is_exists (default true) - only values actually exists in input hash returned
    public static Hashtable filter(Hashtable item, Array fields, bool is_exists = true)
    {
        Hashtable result = new();
        if (item != null)
        {
            foreach (string fld in fields)
            {
                if (fld != null && (!is_exists || item.ContainsKey(fld)))
                    result[fld] = item[fld];
            }
        }
        return result;
    }
    // save as above but fields can be passed as qw string
    public static Hashtable filter(Hashtable item, string fields, bool is_exists = true)
    {
        return filter(item, Utils.qw(fields), is_exists);
    }

    /// <summary>
    /// similar to filter, but for checkboxes (as unchecked checkboxes doesn't passed from the form submit)
    /// </summary>
    /// <param name="itemdb"></param>
    /// <param name="item"></param>
    /// <param name="fields"></param>
    /// <param name="is_existing_fields_only">if true, then only process fields existing in the item. Usually used with PATCH requests</param>
    /// <param name="default_value">default value for non-exsiting fields in item</param>
    /// <returns>by ref itemdb - add fields with default_value or form value</returns>
    public static bool filterCheckboxes(Hashtable itemdb, Hashtable item, IList fields, bool is_existing_fields_only = false, string default_value = "0")
    {
        if (fields.Count.Equals(0))
            return false;

        if (item != null)
        {
            foreach (string fld in fields)
            {
                if (item.ContainsKey(fld))
                    itemdb[fld] = item[fld];
                else
                {
                    if (!is_existing_fields_only)
                        itemdb[fld] = default_value;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// similar to filter, but for checkboxes (as unchecked checkboxes doesn't passed from the form submit)
    /// </summary>
    /// <param name="itemdb"></param>
    /// <param name="item"></param>
    /// <param name="fields">qh string with default values: "field|def_value field2|def_value2"</param>
    /// <param name="is_existing_fields_only">if true, then only process fields existing in the item. Usually used with PATCH requests</param>
    /// <param name="default_value">default value for non-exsiting fields in item, if default not defined in fields qw string</param>
    /// <returns>by ref itemdb - add fields with default_value or form value</returns>
    public static bool filterCheckboxes(Hashtable itemdb, Hashtable item, string fields, bool is_existing_fields_only = false, string default_value = "0")
    {
        if (string.IsNullOrEmpty(fields)) return false;

        if (item != null)
        {
            Hashtable hfields = Utils.qh(fields, default_value);
            foreach (string fld in hfields.Keys)
            {
                if (item.ContainsKey(fld))
                    itemdb[fld] = item[fld];
                else
                {
                    if (!is_existing_fields_only)
                        itemdb[fld] = hfields[fld];// default value
                }
            }
        }
        return true;
    }

    // fore each name in $name - check if value is empty '' and make it null
    // not necessary in this framework As DB knows field types, it's here just for compatibility with php framework
    public static void filterNullable(Hashtable itemdb, string names)
    {
        if (string.IsNullOrEmpty(names)) return;

        var anames = Utils.qw(names);
        foreach (string fld in anames)
        {
            if (itemdb.ContainsKey(fld) && (string)itemdb[fld] == "")
                itemdb[fld] = null;
        }
    }


    // join ids from the FORM to comma-separated string (return sorted to keep order consistent)
    // sample:
    // many <input name="dict_link_multi[<~id>]"...>
    // itemdb("dict_link_multi") = FormUtils.multi2ids(reqh("dict_link_multi"))
    public static string multi2ids(Hashtable items)
    {
        if (items == null || items.Count == 0)
            return "";

        return string.Join(",", [.. items.Keys.Cast<string>().OrderBy(key => key)]);
    }

    // input: comma separated string
    // output: hashtable, keys=ids from input
    public static Hashtable ids2multi(string str)
    {
        ArrayList col = comma_str2col(str);
        Hashtable result = new();
        foreach (string id in col)
            result[id] = 1;
        return result;
    }

    public static string col2comma_str(ArrayList col)
    {
        return string.Join(",", col.ToArray());
    }
    public static ArrayList comma_str2col(string str)
    {
        ArrayList result;
        str = str.Trim();
        if (!string.IsNullOrEmpty(str))
            result = new ArrayList(str.Split(","));
        else
            result = new ArrayList();
        return result;
    }

    // return date for combo date selection or Nothing if wrong date
    // sample:
    // <select name="item[fdate_combo_day]">
    // <select name="item[fdate_combo_mon]">
    // <select name="item[fdate_combo_year]">
    // itemdb("fdate_combo") = FormUtils.date4combo(item, "fdate_combo")
    public static object dateForCombo(Hashtable item, string field_prefix)
    {
        object result = null;
        int day = f2int(item[field_prefix + "_day"]);
        int mon = f2int(item[field_prefix + "_mon"]);
        int year = f2int(item[field_prefix + "_year"]);

        if (day > 0 && mon > 0 && year > 0)
        {
            try
            {
                result = new DateTime(year, mon, day).ToOADate();
            }
            catch (Exception)
            {
                result = null;
            }
        }

        return result;
    }

    public static bool comboForDate(string value, Hashtable item, string field_prefix)
    {
        if (DateTime.TryParse(value, out DateTime dt))
        {
            item[field_prefix + "_day"] = dt.Day;
            item[field_prefix + "_mon"] = dt.Month;
            item[field_prefix + "_year"] = dt.Year;
            return true;
        }
        else
            return false;
    }

    // input: 0-86400 (daily time in seconds)
    // output: HH:MM
    public static string intToTimeStr(int i)
    {
        int h = (int)Math.Floor(i / (double)3600);
        int m = (int)Math.Floor((i - h * 3600) / (double)60);
        return h.ToString().PadLeft(2, '0') + ":" + m.ToString().PadLeft(2, '0');
    }

    // input: HH:MM
    // output: 0-86400 (daily time in seconds)
    public static int timeStrToInt(string hhmm)
    {
        string[] a = hhmm.Split(":", 2);
        int result = 0;
        try
        {
            result = f2int(a[0]) * 3600 + f2int(a[1]) * 60;
        }
        catch (Exception)
        {
        }
        return result;
    }

    public static int getIdFromAutocomplete(string s)
    {
        if (string.IsNullOrEmpty(s))
            return 0;

        var idPart = s.Split(new[] { " - " }, StringSplitOptions.None).FirstOrDefault();

        return int.TryParse(idPart, out int result) ? result : 0;
    }

    // convert time from field to 2 form fields with HH and MM suffixes
    // IN: hashtable to make changes in, field_name
    // OUT: false if item(field_name) wrong datetime
    public static bool timeToForm(Hashtable item, string field_name)
    {
        if (DateTime.TryParse((string)item[field_name], out DateTime dt))
        {
            item[field_name + "_hh"] = dt.Hour;
            item[field_name + "_mm"] = dt.Minute;
            item[field_name + "_ss"] = dt.Second;
            return true;
        }
        else
            return false;
    }

    // opposite to time2from
    // OUT: false if can't create time from input item
    public static bool formToTime(Hashtable item, string field_name)
    {
        bool result = true;
        int hh = f2int(item[field_name + "_hh"]);
        int mm = f2int(item[field_name + "_mm"]);
        int ss = f2int(item[field_name + "_ss"]);
        try
        {
            //TODO MIGRATE item[field_name] = DateTime.TimeSerial(hh, mm, ss);
        }
        catch (Exception)
        {
            result = false;
        }

        return result;
    }

    // datetime field to HH:MM or empty string (if no date set)
    public static string dateToFormTime(string datestr)
    {
        string result = "";
        if (!string.IsNullOrEmpty(datestr))
        {
            var dt = Utils.f2date(datestr);
            if (dt != null)
                result = ((DateTime)dt).ToString("HH:mm", System.Globalization.DateTimeFormatInfo.InvariantInfo);
        }
        return result;
    }

    // date and time(HH:MM) fields to date object (or datestr if no time)
    // example: fields("dtfield") = FormUtils.formTimeToDate(itemdb("datefield"), reqh("item")("timefield"))
    public static object formTimeToDate(object datestr, string timestr)
    {
        var result = datestr;
        var timeint = FormUtils.timeStrToInt(timestr);
        var dt = Utils.f2date(datestr);
        if (dt != null)
            // if date set - add time
            result = ((DateTime)dt).AddSeconds(timeint);
        return result;
    }

    //filter list of rows by is_checked=true, then return ordered by prio,iname
    public static ArrayList listCheckedOrderByPrioIname(ArrayList rows)
    {
        return new ArrayList((from Hashtable h in rows
                              where (bool)h["is_checked"]
                              orderby (int)h["prio"] ascending, h["iname"] ascending
                              select h).ToList());
    }

    // do not filter by checked only, but checked first: ordered by is_checked desc,prio,iname
    public static ArrayList listOrderByPrioIname(ArrayList rows)
    {
        return new ArrayList((from Hashtable h in rows
                              orderby (bool)h["is_checked"] descending, (int)h["prio"] ascending, h["iname"] ascending
                              select h).ToList());
    }

    /// ****** helpers to detect changes

    /// <summary>
    /// leave in only those item keys, which are apsent/different from itemold
    /// </summary>
    /// <param name="item"></param>
    /// <param name="itemold"></param>
    /// TODO: if itemold has a bit field, it returned from db as "True", but item from the form as "1" - so it's always different
    public static Hashtable changesOnly(Hashtable item, Hashtable itemold)
    {
        var result = new Hashtable();

        foreach (var key in item.Keys)
        {
            object vnew = item[key];
            object vold = itemold[key];

            // If both are dates, compare only the date part.
            if (Utils.f2date(vnew) is DateTime dtNew && Utils.f2date(vold) is DateTime dtOld)
            {
                if (dtNew.Date != dtOld.Date)
                    result[key] = vnew;
            }
            // Handle non-date values and the case where one value is a date and the other is not.
            else if (!itemold.ContainsKey(key) || Utils.f2str(vnew) != Utils.f2str(vold))
            {
                result[key] = vnew;
            }
        }

        return result;
    }

    /// <summary>
    /// return true if any of passed fields changed
    /// </summary>
    /// <param name="item1"></param>
    /// <param name="item2"></param>
    /// <param name="fields">qw-list of fields</param>
    /// <returns>false if no chagnes in passed fields or fields are empty</returns>
    public static bool isChanged(Hashtable item1, Hashtable item2, string fields)
    {
        var result = false;
        var afields = Utils.qw(fields);
        foreach (var fld in afields)
        {
            if (item1.ContainsKey(fld) && item2.ContainsKey(fld) && Utils.f2str(item1[fld]) != Utils.f2str(item2[fld]))
            {
                result = true;
                break;
            }
        }

        return result;
    }

    // check if 2 dates (without time) chagned
    public static bool isChangedDate(object date1, object date2)
    {
        var dt1 = Utils.f2date(date1);
        var dt2 = Utils.f2date(date2);

        if (dt1 != null || dt2 != null)
        {
            if (dt1 != null && dt2 != null)
            {
                // both set - compare dates
                if (DateUtils.Date2SQL((DateTime)dt1) != DateUtils.Date2SQL((DateTime)dt2))
                    return true;
            }
            else
                // one set, one no - chagned
                return true;
        }
        else
        {
        }

        return false;
    }

    /// <summary>
    /// return sql for order by clause for the passed form name (sortby) and direction (sortdir) using defined mapping (sortmap)
    /// </summary>
    /// <param name="db">fw.db</param>
    /// <param name="sortby">form_name field to sort by</param>
    /// <param name="sortdir">desc|[asc]</param>
    /// <param name="sortmap">mapping form_name => field_name</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string sqlOrderBy(DB db, string sortby, string sortdir, Hashtable sortmap)
    {
        string orderby = ((string)sortmap[sortby] ?? "").Trim();
        if (string.IsNullOrEmpty(orderby))
            throw new Exception("No orderby defined for [" + sortby + "], define in list_sortmap");

        string[] aorderby = orderby.Split(",");
        if (sortdir == "desc")
        {
            // if sortdir is desc, i.e. opposite to default - invert order for orderby fields
            // go thru each order field
            for (int i = 0; i <= aorderby.Length - 1; i++)
            {
                string field = null;
                string order = null;
                Utils.split2(@"\s+", aorderby[i].Trim(), ref field, ref order);

                if (order == "desc")
                    order = "asc";
                else
                    order = "desc";
                aorderby[i] = db.qid(field) + " " + order;
            }
        }
        else
        {
            // quote
            for (int i = 0; i <= aorderby.Length - 1; i++)
            {
                string field = null;
                string order = null;
                Utils.split2(@"\s+", aorderby[i].Trim(), ref field, ref order);
                aorderby[i] = db.qid(field) + " " + order;
            }
        }
        return string.Join(", ", aorderby);
    }
}