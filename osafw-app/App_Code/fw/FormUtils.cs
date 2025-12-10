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

    // arr is FwList of Hashes with "id" and "iname" keys, for example rows returned from db.array('select id, iname from ...')
    // "id" key is optional, if not present - iname will be used for values too
    // isel may contain multiple comma-separated values
    public static string selectOptions(FwList arr, string isel, bool is_multi = false)
    {
        isel ??= "";

        string[] asel;
        if (is_multi)
            asel = isel.Split(",", StringSplitOptions.None);
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
        foreach (FwDict item in arr)
        {
            text = Utils.htmlescape(item["iname"].toStr());
            if (item.ContainsKey("id"))
                val = item["id"].toStr();
            else
                val = item["iname"].toStr();

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

        var template = FwConfig.settings["template"].toStr();

        // translate to absolute path, without any ../
        var path = System.IO.Path.GetFullPath(template + tpl_path);

        // path traversal validation - check if path is a subpath of FwConfig.settings["template"]
        if (!path.StartsWith(template))
            return "";


        string[] lines = Utils.getFileLines(path);
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

    public static FwList selectTplOptions(string tpl_path, string base_path = "")
    {
        FwList result = [];

        if (!tpl_path.StartsWith('/'))
        {
            if (string.IsNullOrEmpty(base_path))
                return result; // base_path required for relative tpl_path

            tpl_path = base_path + "/" + tpl_path;
        }

        var template = FwConfig.settings["template"].toStr();

        // translate to absolute path, without any ../
        var path = System.IO.Path.GetFullPath(template + tpl_path);

        // path traversal validation - check if path is a subpath of FwConfig.settings["template"]
        if (!path.StartsWith(template))
            return result;


        string[] lines = Utils.getFileLines(path);
        foreach (var line in lines)
        {
            if (line.Length < 2)
                continue;

            string[] arr = line.Split("|", 2);
            string value = arr[0];
            string desc = arr[1];

            // desc = ParsePage.RX_LANG.Replace(desc, "$1")
            desc = new Regex("`(.+?)`", RegexOptions.Compiled).Replace(desc, "$1");
            result.Add(new FwDict() { { "id", value }, { "iname", desc } });
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
    public static FwList getPager(long count, int pagenum, object? pagesize1 = null)
    {
        int pagesize = pagesize1.toInt(MAX_PAGE_ITEMS);

        FwList pager = [];
        const int PAD_PAGES = 5;

        if (count > pagesize)
        {
            int page_count = (int)Math.Ceiling(count / (double)pagesize);

            var from_page = pagenum - PAD_PAGES;
            if (from_page < 0)
                from_page = 0;

            var to_page = pagenum + PAD_PAGES;
            if (to_page > page_count - 1)
                to_page = page_count - 1;

            for (int i = from_page; i <= to_page; i++)
            {
                FwDict pager_item = [];
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
    public static FwDict filter(FwDict item, Array fields, bool is_exists = true)
    {
        FwDict result = [];
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
    public static FwDict filter(FwDict item, string fields, bool is_exists = true)
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
    public static bool filterCheckboxes(FwDict itemdb, FwDict item, IList fields, bool is_existing_fields_only = false, string default_value = "0")
    {
        if (fields == null || fields.Count.Equals(0))
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
    public static bool filterCheckboxes(FwDict itemdb, FwDict item, string fields, bool is_existing_fields_only = false, string default_value = "0")
    {
        if (string.IsNullOrEmpty(fields)) return false;

        if (item != null)
        {
            FwDict hfields = Utils.qh(fields, default_value);
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

    /// <summary>
    /// overload for filterNullable - for qw string
    /// </summary>
    /// <param name="itemdb"></param>
    /// <param name="names"></param>
    public static void filterNullable(FwDict itemdb, string names)
    {
        if (string.IsNullOrEmpty(names)) return;
        var anames = Utils.qw(names);

        filterNullable(itemdb, anames);
    }

    /// <summary>
    /// for each name in $names - check if value is empty '' and make it null
    /// not necessary in this framework As DB knows field types, it's here just for compatibility with php framework
    /// </summary>
    /// <param name="itemdb"></param>
    /// <param name="names"></param>
    public static void filterNullable(FwDict itemdb, IList names)
    {
        if (names == null || names.Count == 0) return;
        foreach (string fld in names)
        {
            if (itemdb.ContainsKey(fld) && itemdb[fld].toStr() == "")
                itemdb[fld] = null;
        }
    }


    // join ids from the FORM to comma-separated string (return sorted to keep order consistent)
    // sample:
    // many <input name="dict_link_multi[<~id>]"...>
    // itemdb("dict_link_multi") = FormUtils.multi2ids(reqh("dict_link_multi"))
    public static string multi2ids(FwDict items)
    {
        if (items == null || items.Count == 0)
            return "";

        return string.Join(",", [.. items.Keys.Cast<string>().OrderBy(key => key)]);
    }

    // input: comma separated string
    // output: hashtable, keys=ids from input
    public static FwDict ids2multi(string str)
    {
        FwList col = comma_str2col(str);
        FwDict result = [];
        foreach (string id in col)
            result[id] = 1;
        return result;
    }

    public static string col2comma_str(FwList col)
    {
        return string.Join(",", col.ToArray());
    }

    /**
     * convert comma-separated string to arraylist, trimming each element
     * if str is empty - return empty arraylist
     * @param string str
     * @return FwList
     */
    public static FwList comma_str2col(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return [];
        return new FwList(str.Split(",").Select(s => s.Trim()).ToList());
    }

    /// <summary>
    /// return SQL date for combo date selection or Nothing if wrong date
    /// sample:
    ///   <select name="item[fdate_combo_day]">
    ///   <select name="item[fdate_combo_mon]">
    ///   <select name="item[fdate_combo_year]">
    ///   itemdb["fdate_combo"] = FormUtils.dateForCombo(item, "fdate_combo")
    /// </summary>
    /// <param name="item"></param>
    /// <param name="field_prefix"></param>
    /// <returns></returns>
    public static string dateForCombo(FwDict item, string field_prefix)
    {
        string result = "";
        if (item == null)
            return result;

        int day = item[field_prefix + "_day"].toInt();
        int mon = item[field_prefix + "_mon"].toInt();
        int year = item[field_prefix + "_year"].toInt();

        if (day > 0 && mon > 0 && year > 0)
        {
            try
            {
                result = new DateTime(year, mon, day).ToString("yyyy-MM-dd");
            }
            catch (Exception)
            {
                result = "";
            }
        }

        return result;
    }

    public static bool comboForDate(string value, FwDict item, string field_prefix)
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
            result = a[0].toInt() * 3600 + a[1].toInt() * 60;
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

        var idPart = s.Split([" - "], StringSplitOptions.None).FirstOrDefault();

        return int.TryParse(idPart, out int result) ? result : 0;
    }

    // convert time from field to 2 form fields with HH and MM suffixes
    // IN: hashtable to make changes in, field_name
    // OUT: false if item(field_name) wrong datetime
    public static bool timeToForm(FwDict item, string field_name)
    {
        if (DateTime.TryParse(item[field_name].toStr(), out DateTime dt))
        {
            item[field_name + "_hh"] = dt.Hour;
            item[field_name + "_mm"] = dt.Minute;
            item[field_name + "_ss"] = dt.Second;
            return true;
        }
        else
            return false;
    }

    // opposite to timeToForm
    // OUT: false if can't create time from input item
    public static bool formToTime(FwDict item, string field_name)
    {
        bool result = true;
        int hh = item[field_name + "_hh"].toInt();
        int mm = item[field_name + "_mm"].toInt();
        int ss = item[field_name + "_ss"].toInt();
        try
        {
            item[field_name] = new DateTime(1, 1, 1, hh, mm, ss);
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
            var dt = datestr.toDate();
            if (Utils.isDate(dt))
                result = dt.ToString("HH:mm", System.Globalization.DateTimeFormatInfo.InvariantInfo);
        }
        return result;
    }

    // date and time(HH:MM) fields to date object (or datestr if no time)
    // example: fields("dtfield") = FormUtils.formTimeToDate(itemdb("datefield"), reqh("item")("timefield"))
    public static object formTimeToDate(object datestr, string timestr)
    {
        var result = datestr;
        var timeint = FormUtils.timeStrToInt(timestr);
        var dt = datestr.toDate();
        if (Utils.isDate(dt))
            // if date set - add time
            result = dt.AddSeconds(timeint);
        return result;
    }

    //filter list of rows by is_checked=true, then return ordered by prio,iname
    public static FwList listCheckedOrderByPrioIname(FwList rows)
    {
        return new FwList((from FwDict h in rows
                              where h["is_checked"].toBool()
                              orderby h["prio"].toInt() ascending, h["iname"] ascending
                              select h).ToList());
    }

    // do not filter by checked only, but checked first: ordered by is_checked desc,prio,iname
    public static FwList listOrderByPrioIname(FwList rows)
    {
        return new FwList((from FwDict h in rows
                              orderby h["is_checked"].toBool() descending, h["prio"].toInt() ascending, h["iname"] ascending
                              select h).ToList());
    }

    /// ****** helpers to detect changes

    /// <summary>
    /// leave in only those item keys, which are apsent/different from itemold
    /// </summary>
    /// <param name="item"></param>
    /// <param name="itemold"></param>
    /// TODO: if itemold has a bit field, it returned from db as "True", but item from the form as "1" - so it's always different
    public static FwDict changesOnly(FwDict item, FwDict itemold)
    {
        var result = new FwDict();

        foreach (var key in item.Keys)
        {
            object? vnew = item[key];
            object? vold = itemold.ContainsKey(key) ? itemold[key] : null;

            // If both are dates, compare only the date part.
            var dtNew = vnew.toDate();
            var dtOld = vold.toDate();
            if (Utils.isDate(dtNew) && Utils.isDate(dtOld))
            {
                if (dtNew.Date != dtOld.Date)
                    result[key] = vnew;
            }
            // Handle non-date values and the case where one value is a date and the other is not.
            else if (!itemold.ContainsKey(key) || vnew.toStr() != vold.toStr())
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
    public static bool isChanged(FwDict item1, FwDict item2, string fields)
    {
        var result = false;
        var afields = Utils.qw(fields);
        foreach (var fld in afields)
        {
            if (item1.ContainsKey(fld) && item2.ContainsKey(fld) && item1[fld].toStr() != item2[fld].toStr())
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
        var dt1 = date1.toDate();
        var dt2 = date2.toDate();

        if (Utils.isDate(dt1) || Utils.isDate(dt2))
        {
            if (Utils.isDate(dt1) && Utils.isDate(dt2))
            {
                // both set - compare dates
                if (DateUtils.Date2SQL(dt1) != DateUtils.Date2SQL(dt2))
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
    public static string sqlOrderBy(DB db, string sortby, string sortdir, FwDict sortmap)
    {
        string orderby = sortmap[sortby].toStr().Trim();
        if (string.IsNullOrEmpty(orderby))
            throw new Exception("No orderby defined for [" + sortby + "], define in list_sortmap");

        string[] aorderby = orderby.Split(",");
        if (sortdir == "desc")
        {
            // if sortdir is desc, i.e. opposite to default - invert order for orderby fields
            // go thru each order field
            for (int i = 0; i <= aorderby.Length - 1; i++)
            {
                string field = string.Empty;
                string order = string.Empty;
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
                string field = string.Empty;
                string order = string.Empty;
                Utils.split2(@"\s+", aorderby[i].Trim(), ref field, ref order);
                aorderby[i] = db.qid(field) + " " + order;
            }
        }
        return string.Join(", ", aorderby);
    }
}