// ParsePage for ASP.NET - framework template engine
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2019 Oleg Savchuk www.osalabs.com
/*
 supports:
 - SESSION, GLOBAL (from fw.G), SUBHASHES, SUBARRAYS, PARSEPAGE.TOP, PARSEPAGE.PARENT
 - <~tag if="var"> - var tested for true value (1, true, >"", but not "0")
 - CSRF shield - all vars escaped, if var shouldn't be escaped use "noescape" attr: < ~raw_variable noescape >
  - attrs["select") ]an contain strings with separator ","(or custom defined) for multiple select
  - <~#commented_tag> - comment tags that doesn't need to be parsed(quickly replaced by empty string)
 

 # Supported attributes:
 

 var - tag is variable, no fileseek necessary
 ifXX - if confitions
   ifeq="var" value="XXX" - tag/template will be parsed only if var=XXX
   ifne="var" value="XXX" - tag/template will be parsed only if var!=XXX
   ifgt="var" value="XXX" - tag/template will be parsed only if var>XXX
   ifge="var" value="XXX" - tag/template will be parsed only if var>=XXX
   iflt="var" value="XXX" - tag/template will be parsed only if var<XXX
   ifle="var" value="XXX" - tag/template will be parsed only if var<=XXX
   var can be ICollection (Hashtable/ArrayList/...)
   <~tag if="ArrayList"> will fail if ArrayList.Count=0 or success if ArrayList.Count>0
 
    ## old mapping
    neq => ne
    ge => gt
    le => lt
    gee => ge
    lee => le
 

  vvalue - value as hf variable:
    <~tag ifeq="var" vvalue="YYY"> - actual value got via hfvalue('YYY', $hf);
 

  #shortcuts
  <~tag if="var"> - tag will be shown if var is evaluated as TRUE, not using eval(), equivalent to "if ($var)"
  <~tag unless="var"> - tag will be shown if var is evaluated as TRUE, not using eval(), equivalent to "if (!$var)"
  -------------------------
    TRUE values:
    non-empty string, but not equal to "0" or "false"!
    1 or other non-zero number
    "true" string
    true (boolean)
 

    FALSE values:
   "0" or "false" string
    0
    false (boolean)
   ''
    unset variable
  -------------------------
 

  repeat - this tag is repeat content ($hf hash should contain reference to array of hashes),
    supported repeat vars:
    repeat.first (0-not first, 1-first)
    repeat.last  (0-not last, 1-last)
    repeat.total (total number of items)
    repeat.index  (0-based)
    repeat.iteration (1-based)
 

  sub - this tag tell parser to use subhash for parse subtemplate ($hf hash should contain reference to hash), examples:
     <~tag sub inline>...</~tag>- use $hf[tag] as hashtable for inline template
     <~tag sub="var"> - use $hf[var] as hashtable for template in "tag.html"
  inline - this tag tell parser that subtemplate is not in file - it's between < ~tag > ...</ ~tag > , useful in combination with 'repeat' and 'if'
  global - this tag is a global var, not in $hf hash
   global[var] - also possible
  session - this tag is a $_SESSION var, not in $hf hash
   session[var] - also possible
  TODO parent - this tag is a $parent_hf var, not in current $hf hash
  select="var" [multi[=","]] - this tag tell parser to either load file with tag name and use it as value|display for <select> tag
                 or if variable with tag name exists - use it as arraylist of hashtables with id/iname keys
               if "multi" attr defined - "var" value split by separator deinfed in multi attr (default is ",") and multiple options could be selected
       , example:
       <select name="item[fcombo]">
       <option value=""> - select -
       <~./fcombo.sel select="fcombo">  or <~fcombo_options select="fcombo">
       </select>
  radio="var" name="YYY" [delim="inline"]- this tag tell parser to load file and use it as value|display for <input type=radio> tags, Bootsrtrap 3 style, example:
       <~./fradio.sel radio="fradio" name="item[fradio]" delim="inline">
  selvalue="var" - display value (fetched from the .sel file) for the var (example: to display 'select' and 'radio' values in List view)
       ex: <~../fcombo.sel selvalue="fcombo">
  TODO nolang - for subtemplates - use default language instead of current (usually english)
  htmlescape - replace special symbols by their html equivalents (such as <>,",')

    'multi-language support `text` => replaced by language string from $site_templ/lang/$lang.txt according to fw.config('lang ') (english by default)
   example: <b>`Hello`</b>  -> become -> <b>Hola</b>
   lang.txt line format:
            english string === lang string
            Hello === Hola
 support modifiers:
  htmlescape
  date          - format as datetime, sample "d M yyyy HH:mm", see https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings
        <~var date>         output "M/d/yyyy" - date only (TODO - should be formatted per user settings actually)
        <~var date="short"> output "M/d/yyyy hh:mm" - date and time short (to mins)
        <~var date="long">  output "M/d/yyyy hh:mm:ss" - date and time long
        <~var date="sql">   output "yyyy-MM-dd hh:mm:ss" - sql date and time
  url           - add http:// to begin of string if absent
  number_format - FormatNumber(value, 2) => 12345.12
  truncate      - truncate with options <~tag truncate="80" trchar="..." trword="1" trend="1">
  strip_tags
  trim
  nl2br
  TODO count         - for ICollection only
  lower
  upper
  capitalize        - capitalize first word, capitalize=all - capitalize all words
  default
  urlencode
  json (was var2js) - produces json-compatible string, example: {success:true, msg:""}
  markdown      - convert markdown text to html using CommonMark.NET (optional). Note: may wrap tag with <p>
  noparse       - doesn't parse file and just include file by tag path as is, ignores all other attrs except if
*/

using Microsoft.AspNetCore.Http;
using Microsoft.VisualBasic; //TODO MIGRATE remove dependency
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace osafw
{
    public class ParsePage
    {
        private static readonly Regex RX_NOTS = new Regex(@"^(\S+)", RegexOptions.Compiled);
        private static readonly Regex RX_LANG = new Regex("`(.+?)`", RegexOptions.Compiled);
        private static readonly Regex RX_FULL_TAGS = new Regex("<~([^>]+)>", RegexOptions.Compiled);

        private static readonly Regex RX_ATTRS1 = new Regex(@"((?:\S+\=" + (char)34 + "[^" + (char)34 + "]*" + (char)34 + @")|(?:\S+\='[^']*')|(?:[^'" + (char)34 + @"\s]+)|(?:\S+\=\S*))", RegexOptions.Compiled);
        private static readonly Regex RX_ATTRS2 = new Regex(@"^([^\s\=]+)=(['" + (char)34 + @"]?)(.*?)\2$", RegexOptions.Compiled);

        private static readonly Regex RX_ALL_DIGITS = new Regex(@"^\d+$", RegexOptions.Compiled);
        private static readonly Regex RX_LAST_SLASH = new Regex(@"[^\/]+$", RegexOptions.Compiled);
        private static readonly Regex RX_EXT = new Regex(@"\.[^\/]+$", RegexOptions.Compiled);

        private static readonly Hashtable FILE_CACHE = new();
        private static readonly Hashtable LANG_CACHE = new();
        private static readonly string[] IFOPERS = new[] { "if", "unless", "ifne", "ifeq", "ifgt", "iflt", "ifge", "ifle" };

        private const string DATE_FORMAT_DEF = "M/d/yyyy"; // for US, TODO make based on user settigns (with fallback to server's settings)
        private const string DATE_FORMAT_SHORT = "M/d/yyyy HH:mm";
        private const string DATE_FORMAT_LONG = "M/d/yyyy HH:mm:ss";
        private const string DATE_FORMAT_SQL = "yyyy-MM-dd HH:mm:ss";
        // "d M yyyy HH:mm"

        // for dynamic load of CommonMark markdown converter
        private static object CommonMarkSettings;
        private static System.Reflection.MethodInfo mConvert;

        private readonly FW fw;
        // checks if template files modifies and reload them, depends on config's "log_level"
        // true if level at least DEBUG, false for production as on production there are no tempalte file changes (unless during update, which leads to restart App anyway)
        private readonly bool is_check_file_modifications = false;
        private readonly string TMPL_PATH;
        private string basedir = "";
        private Hashtable data_top; // reference to the topmost hashtable
        private bool is_found_last_hfvalue = false;
        private readonly string lang = "en";
        private readonly bool lang_parse = true; // parse lang strings in `` or not - true - parse(default), false - no
        private readonly bool lang_update = true; // save unknown matches to lang file (helps during development) 
        private readonly MatchEvaluator lang_evaluator;

        public ParsePage(FW fw)
        {
            this.fw = fw;
            TMPL_PATH = (string)fw.config("template");
            is_check_file_modifications = (LogLevel)fw.config("log_level") >= LogLevel.DEBUG;
            lang = (string)fw.G["lang"];
            if (string.IsNullOrEmpty(lang))
                lang = (string)fw.config("lang");
            if (string.IsNullOrEmpty(lang))
                lang = "en";

            // load cache for all current lang matches 
            if (LANG_CACHE[lang] == null)
                load_lang();
            lang_evaluator = new MatchEvaluator(this.lang_replacer);

            lang_update = Utils.f2bool(fw.config("is_lang_update"));
        }

        public string parse_json(object hf)
        {
            return Utils.jsonEncode(hf);
        }


        public string parse_page(string bdir, string tpl_name, Hashtable hf)
        {
            this.basedir = bdir;
            this.data_top = hf;
            Hashtable parent_hf = new();
            // Return _parse_page(tpl_name, hf, "", "", parent_hf)

            // Dim start_time = DateTime.Now
            var result = _parse_page(tpl_name, hf, "", ref parent_hf);
            // Dim end_timespan As TimeSpan = DateTime.Now - start_time
            // fw.logger("ParsePage speed: " & String.Format("{0:0.000}", 1 / end_timespan.TotalSeconds) & "/s")
            return result;
        }

        public string parse_string(string tpl, Hashtable hf)
        {
            basedir = "/";
            Hashtable parent_hf = new();
            return _parse_page("", hf, tpl, ref parent_hf);
        }

        private string _parse_page(string tpl_name, Hashtable hf, string page, ref Hashtable parent_hf)
        {
            if (tpl_name.Substring(0, 1) != "/")
                tpl_name = basedir + "/" + tpl_name;

            // fw.logger("DEBUG", "ParsePage - Parsing template = " + tpl_name + ", pagelen=" & page.Length)
            if (page.Length < 1)
                page = precache_file(TMPL_PATH + tpl_name);

            if (page.Length > 0)
            {
                parse_lang(ref page);
                string page_orig = page;
                MatchCollection tags_full = get_full_tags(ref page);

                if (tags_full.Count > 0)
                {
                    sort_tags(tags_full);
                    Hashtable TAGSEEN = new();
                    string tag_full;
                    string tag;
                    Hashtable attrs;
                    object tag_value;
                    string v;

                    foreach (Match tag_match in tags_full)
                    {
                        tag_full = tag_match.Groups[1].Value;
                        if (TAGSEEN.ContainsKey(tag_full))
                            continue; // each tag (tag_full) parsed just once and replaces all occurencies of the tag in the page
                        TAGSEEN.Add(tag_full, 1);

                        tag = tag_full;
                        attrs = new();
                        get_tag_attrs(ref tag, ref attrs);

                        // skip # commented tags and tags that not pass if
                        if (tag[0] != '#' && _attr_if(attrs, hf))
                        {
                            string inline_tpl = "";

                            if (attrs.Count > 0)
                            {
                                if (attrs.ContainsKey("inline"))
                                    inline_tpl = get_inline_tpl(ref page_orig, ref tag, ref tag_full);

                                if (attrs.ContainsKey("session"))
                                    tag_value = hfvalue(tag, fw.context.Session);
                                else if (attrs.ContainsKey("global"))
                                    tag_value = hfvalue(tag, fw.G);
                                else
                                    tag_value = hfvalue(tag, hf, parent_hf);
                            }
                            else
                                tag_value = hfvalue(tag, hf, parent_hf);

                            // fw.logger("ParsePage - tag: " & tag_full & ", found=" & is_found_last_hfvalue)
                            if (tag_value.ToString().Length > 0)
                            {
                                string value;
                                if (attrs.ContainsKey("repeat"))
                                    value = _attr_repeat(ref tag, ref tag_value, ref tpl_name, ref inline_tpl, hf);
                                else if (attrs.ContainsKey("select"))
                                    // this is special case for '<select>' HTML tag when options passed as ArrayList
                                    value = _attr_select(tag, tpl_name, ref hf, attrs);
                                else if (attrs.ContainsKey("selvalue"))
                                {
                                    // # this is special case for '<select>' HTML tag
                                    value = _attr_select_name(tag, tpl_name, ref hf, attrs);
                                    if (!attrs.ContainsKey("noescape"))
                                        value = Utils.htmlescape(value);
                                }
                                else if (attrs.ContainsKey("sub"))
                                    value = _attr_sub(tag, tpl_name, hf, attrs, inline_tpl, parent_hf, tag_value);
                                else
                                {
                                    if (attrs.ContainsKey("json"))
                                        value = Utils.jsonEncode(tag_value);
                                    else
                                        value = tag_value.ToString();
                                    if (!string.IsNullOrEmpty(value) && !attrs.ContainsKey("noescape"))
                                        value = Utils.htmlescape(value);
                                }
                                tag_replace(ref page, ref tag_full, ref value, attrs);
                            }
                            else if (attrs.ContainsKey("repeat"))
                            {
                                v = _attr_repeat(ref tag, ref tag_value, ref tpl_name, ref inline_tpl, hf);
                                tag_replace(ref page, ref tag_full, ref v, attrs);
                            }
                            else if (attrs.ContainsKey("var"))
                            {
                                string tmp_value = "";
                                tag_replace(ref page, ref tag_full, ref tmp_value, attrs);
                            }
                            else if (attrs.ContainsKey("select"))
                            {
                                // # this is special case for '<select>' HTML tag
                                v = _attr_select(tag, tpl_name, ref hf, attrs);
                                tag_replace(ref page, ref tag_full, ref v, attrs);
                            }
                            else if (attrs.ContainsKey("selvalue"))
                            {
                                // # this is special case for '<select>' HTML tag
                                v = _attr_select_name(tag, tpl_name, ref hf, attrs);
                                if (!attrs.ContainsKey("noescape"))
                                    v = Utils.htmlescape(v);
                                tag_replace(ref page, ref tag_full, ref v, attrs);
                            }
                            else if (attrs.ContainsKey("radio"))
                            {
                                // # this is special case for '<index type=radio>' HTML tag
                                v = _attr_radio(tag_tplpath(tag, tpl_name), ref hf, attrs);
                                tag_replace(ref page, ref tag_full, ref v, attrs);
                            }
                            else if (attrs.ContainsKey("noparse"))
                            {
                                // # no need to parse file - just include as is
                                var path = tag_tplpath(tag, tpl_name);
                                if (path.Substring(0, 1) != "/")
                                    path = basedir + "/" + path;
                                path = TMPL_PATH + path;
                                var file_content = precache_file(path);
                                tag_replace(ref page, ref tag_full, ref file_content, new Hashtable());
                            }
                            else
                            {

                                // #also checking for sub
                                if (attrs.ContainsKey("sub"))
                                    v = _attr_sub(tag, tpl_name, hf, attrs, inline_tpl, parent_hf, tag_value);
                                else if (is_found_last_hfvalue)
                                    // value found but empty
                                    v = "";
                                else
                                    // value not found - looks like subtemplate in file
                                    v = _parse_page(tag_tplpath(tag, tpl_name), hf, inline_tpl, ref parent_hf);
                                tag_replace(ref page, ref tag_full, ref v, attrs);
                            }
                        }
                        else
                        {
                            string tmp_value = "";
                            tag_replace(ref page, ref tag_full, ref tmp_value, attrs);
                        }

                    }
                }
                else
                {
                }
            }

            // FW.logger("DEBUG", "ParsePage - Parsing template = " & tpl_name & " END")
            return page;
        }

        public void clear_cache()
        {
            FILE_CACHE.Clear();
            LANG_CACHE.Clear();
        }

        private string precache_file(string filename)
        {
            string modtime = "";
            string file_data = "";
            filename = filename.Replace("/", @"\");
            // fw.logger("preacaching [" & filename & "]")

            // check and get from cache
            if (FILE_CACHE.ContainsKey(filename))
            {
                Hashtable cached_item = (Hashtable)FILE_CACHE[filename];
                // if debug is off - don't check modify time for better performance (but app restart would be necessary if template changed)
                if (is_check_file_modifications)
                {
                    modtime = File.GetLastWriteTime(filename).ToString();
                    string mtmp = (string)cached_item["modtime"];
                    if (string.IsNullOrEmpty(mtmp) || mtmp == modtime)
                        return (string)cached_item["data"];
                }
                else
                    return (string)cached_item["data"];
            }
            else if (is_check_file_modifications)
                modtime = File.GetLastWriteTime(filename).ToString();

            // fw.logger("ParsePage - try load file " & filename)
            // get from fs(if not in cache)
            if (File.Exists(filename))
            {
                file_data = FW.getFileContent(filename);
                if (is_check_file_modifications && string.IsNullOrEmpty(modtime))
                    modtime = File.GetLastWriteTime(filename).ToString();
            }

            // get from fs(if not in cache)
            Hashtable cache = new();
            cache["data"] = file_data;
            cache["modtime"] = modtime;

            FILE_CACHE[filename] = cache;
            // fw.logger("END preacaching [" & filename & "]")
            return file_data;
        }

        private MatchCollection get_full_tags(ref string page)
        {
            return RX_FULL_TAGS.Matches(page);
        }

        private void sort_tags(MatchCollection full_tags)
        {
        }

        // Note: also strip tag to short tag
        private void get_tag_attrs(ref string tag, ref Hashtable attrs)
        {
            // If Regex.IsMatch(tag, "\s") Then
            if (tag.Contains(" "))
            {
                MatchCollection attrs_raw = RX_ATTRS1.Matches(tag);

                tag = attrs_raw[0].Value;
                int i;
                for (i = 1; i <= attrs_raw.Count - 1; i++)
                {
                    string attr = attrs_raw[i].Value;
                    Match match = RX_ATTRS2.Match(attr);
                    if (match.Success)
                    {
                        string key = match.Groups[1].ToString();
                        string value = match.Groups[3].ToString();
                        attrs.Add(key, value);
                    }
                    else
                        attrs.Add(attr, "");
                }
            }
        }

        // hf can be: Hashtable or HttpSessionState
        // returns: 
        // value (string, hashtable, etc..), empty string "" 
        // Or Nothing - tag not present in hf param (only if hf is Hashtable), file lookup will be necessary
        // set is_found to True if tag value found hf/parent_hf (so can be used to detect if there are no tag value at all so no fileseek required)
        private object hfvalue(string tag, object hf, Hashtable parent_hf = null/* TODO Change to default(_) if this is not a reference type */)
        {
            object tag_value = "";
            object ptr;
            is_found_last_hfvalue = true;

            try
            {
                if (tag.Contains("["))
                {
                    string[] parts = tag.Split("[");
                    int start_pos = 0;
                    string parts0 = parts[0].ToUpper();

                    if (parts0 == "GLOBAL")
                    {
                        ptr = fw.G;
                        start_pos = 1;
                    }
                    else if (parts0 == "SESSION")
                    {
                        ptr = fw.context.Session;
                        start_pos = 1;
                    }
                    else if (parts0 == "PARSEPAGE.TOP")
                    {
                        ptr = this.data_top;
                        start_pos = 1;
                    }
                    else if (parts0 == "PARSEPAGE.PARENT" && parent_hf != null)
                    {
                        ptr = parent_hf;
                        start_pos = 1;
                    }
                    else
                        ptr = hf;

                    string k;
                    for (int i = start_pos; i <= parts.Length - 1; i++)
                    {
                        k = Regex.Replace(parts[i], @"\].*?", ""); // remove last ]
                        if (ptr is Array array)
                        {
                            if (System.Convert.ToInt32(k) <= array.GetUpperBound(0))
                                ptr = array.GetValue(System.Convert.ToInt32(k));
                            else
                            {
                                ptr = ""; // out of Array bounds
                                break;
                            }
                        }
                        else if (ptr is Hashtable hashtable)
                        {
                            if (hashtable.ContainsKey(k))
                                ptr = hashtable[k];
                            else
                            {
                                ptr = ""; // no such key in hash
                                break;
                            }
                        }
                        else if (ptr is IList list)
                            ptr = list[Utils.f2int(k)];
                        else if (ptr is ISession session)
                        {
                            //TODO MIGRATE - get arbitrary object from session, not just string
                            var value = session.GetString(k);
                            if (!string.IsNullOrEmpty(value))
                                ptr = value;
                            else
                            {
                                ptr = ""; // no such key in session
                                break;
                            }
                        }
                        else
                        {
                            // looks like there are just no such key in array/hash OR ptr is not an array/hash at all - so return empty value
                            ptr = "";
                            break;
                        }
                    }
                    tag_value = ptr;
                }
                else if (hf is Hashtable hashtable)
                {
                    // special name tags - ROOT_URL and ROOT_DOMAIN - hardcoded here because of too frequent usage in the site
                    if (tag == "ROOT_URL" || tag == "ROOT_DOMAIN")
                        tag_value = fw.config(tag);
                    else if (hashtable.ContainsKey(tag))
                        tag_value = hashtable[tag];
                    else
                        // if no such tag in Hashtable
                        is_found_last_hfvalue = false;
                }
                else if (hf is ISession session)
                {
                    //TODO MIGRATE - get also int from session, not just string?
                    var value = session.GetString(tag);
                    if (!string.IsNullOrEmpty(value))
                        tag_value = value;
                }
                else if (tag == "ROOT_URL")
                    tag_value = fw.config("ROOT_URL");
                else if (tag == "ROOT_DOMAIN")
                    tag_value = fw.config("ROOT_DOMAIN");
                else
                    is_found_last_hfvalue = false;
            }
            catch (Exception ex)
            {
                fw.logger(LogLevel.DEBUG, "ParsePage - error in hvalue for tag [", tag, "]:", ex.Message);
            }

            if (tag_value == null)
                tag_value = "";

            return tag_value;
        }

        private string _attr_sub(string tag, string tpl_name, Hashtable hf, Hashtable attrs, string inline_tpl, Hashtable parent_hf, object tag_value)
        {
            string sub = (string)attrs["sub"];
            if (!string.IsNullOrEmpty(sub))
                // if sub attr contains name - use it to get value from hf (instead using tag_value)
                tag_value = hfvalue(sub, hf, parent_hf);
            if (!(tag_value is Hashtable))
            {
                fw.logger(LogLevel.DEBUG, "ParsePage - not a Hash passed for a SUB tag=", tag, ", sub=" + sub);
                tag_value = new();
            }
            return _parse_page(tag_tplpath(tag, tpl_name), (Hashtable)tag_value, inline_tpl, ref parent_hf);
        }

        // Check for misc if attrs
        private bool _attr_if(Hashtable attrs, Hashtable hf)
        {
            if (attrs.Count == 0)
                return true; // if there are no if operation - return true anyway and early

            string oper = "";
            foreach (var item in IFOPERS)
            {
                if (attrs.ContainsKey(item))
                {
                    oper = item;
                    break;
                }

            }
            if (string.IsNullOrEmpty(oper))
                return true; // if there are no if operation - return true anyway

            string eqvar = (string)attrs[oper];
            if (string.IsNullOrEmpty(eqvar))
                return false; // return false if var need to be compared is empty

            object eqvalue = hfvalue(eqvar, hf);
            if (eqvalue == null)
                eqvalue = "";

            // detect if eqvalue is integer
            int zzz = Int32.MinValue;
            if (Int32.TryParse(eqvalue.ToString(), out zzz))
                eqvalue = zzz;

            object ravalue;
            bool is_numeric_comparison = false;
            if (attrs.ContainsKey("value") || attrs.ContainsKey("vvalue"))
            {
                if (attrs.ContainsKey("vvalue"))
                {
                    ravalue = hfvalue((string)attrs["vvalue"], hf);
                    if (ravalue == null)
                        ravalue = "";
                }
                else
                    ravalue = attrs["value"];

                // convert ravalue to boolean if eqvalue is boolean, OR both to string otherwise
                if ((eqvalue) is bool)
                {
                    string ravaluestr = ravalue.ToString().ToLower();
                    if (ravaluestr == "1" || ravaluestr == "true")
                        ravalue = true;
                    else
                        ravalue = false;
                }
                else if (eqvalue is Int32)
                {
                    // convert ravalue to Int32 for int comparisons
                    if (Int32.TryParse(ravalue.ToString(), out _))
                    {
                        is_numeric_comparison = true;
                    }
                    else
                    {
                        // ravalue is not an integer, so try string comparison
                        ravalue = ravalue.ToString();
                        eqvalue = eqvalue.ToString();
                    }
                }
                else if (eqvalue is ICollection collection)
                {
                    // if we comparing to Hashtable or ArrayList - we actually compare to .Count
                    // so <~tag if="ArrayList"> will fail if ArrayList.Count=0 or success if ArrayList.Count>0
                    eqvalue = collection.Count;
                    is_numeric_comparison = true;
                }
                else
                {
                    eqvalue = eqvalue.ToString();
                    ravalue = ravalue.ToString();
                }
            }
            else
            {
                // special case - if no value attr - check for boolean
                ravalue = true;
                // TRUE = non-empty string, but not equal "0";"false";non-0 number, true (boolean), or ICollection.Count>0
                if (eqvalue is bool b)
                    eqvalue = b;
                else if (eqvalue is ICollection collection)
                    eqvalue = collection.Count > 0;
                else if (!(eqvalue is bool))
                {
                    string eqstr = eqvalue.ToString();
                    eqvalue = !string.IsNullOrEmpty(eqstr) && eqstr != "0" && eqstr.ToLower() != "false";
                }
                else
                    eqvalue = false;
            }

            bool result = false;
            if (oper == "if" && (bool)eqvalue == true)
                result = true;
            else if (oper == "unless" && (bool)eqvalue == false)
                result = true;
            else if (oper == "ifeq" && eqvalue.ToString() == ravalue.ToString())
                result = true;
            else if (oper == "ifne" && eqvalue != ravalue)
                result = true;
            else if (oper == "iflt" && (is_numeric_comparison && (int)eqvalue < (int)ravalue || !is_numeric_comparison && String.Compare((string)eqvalue, (string)ravalue) < 0))
                result = true;
            else if (oper == "ifgt" && (is_numeric_comparison && (int)eqvalue > (int)ravalue || !is_numeric_comparison && String.Compare((string)eqvalue, (string)ravalue) > 0))
                result = true;
            else if (oper == "ifge" && (is_numeric_comparison && (int)eqvalue >= (int)ravalue || !is_numeric_comparison && String.Compare((string)eqvalue, (string)ravalue) >= 0))
                result = true;
            else if (oper == "ifle" && (is_numeric_comparison && (int)eqvalue <= (int)ravalue || !is_numeric_comparison && String.Compare((string)eqvalue, (string)ravalue) <= 0))
                result = true;

            return result;
        }

        private string get_inline_tpl(ref string hpage, ref string tag, ref string tag_full)
        {
            // fw.logger("ParsePage - get_inline_tpl: ", tag, " | ", tag_full)
            string re = Regex.Escape("<~" + tag_full + ">") + "(.*?)" + Regex.Escape("</~" + tag + ">");

            Match inline_match = Regex.Match(hpage, re, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            string inline_tpl = inline_match.Groups[1].Value;

            return inline_tpl;
        }

        // return ready HTML
        private string _attr_repeat(ref string tag, ref object tag_val_array, ref string tpl_name, ref string inline_tpl, Hashtable parent_hf)
        {
            // Validate: if input doesn't contain array - return "" - nothing to repeat
            if (!(tag_val_array is IList))
            {
                if (tag_val_array != null && tag_val_array.ToString() != "")
                    fw.logger(LogLevel.DEBUG, "ParsePage - Not an ArrayList passed to repeat tag=", tag);
                return "";
            }

            StringBuilder value = new StringBuilder();
            if (parent_hf == null)
                parent_hf = new();

            string ttpath = tag_tplpath(tag, tpl_name);

            IList list = (IList)tag_val_array;
            for (int i = 0; i <= list.Count - 1; i++)
            {
                var row = proc_repeat_modifiers(list, i);
                value.Append(_parse_page(ttpath, row, inline_tpl, ref parent_hf));
            }
            return value.ToString();
        }

        private Hashtable proc_repeat_modifiers(IList uftag, int i)
        {
            Hashtable uftagi = (Hashtable)((Hashtable)uftag[i]).Clone(); // make a shallow copy as we modify this level
            int cnt = uftag.Count;

            if (i == 0)
                uftagi["repeat.first"] = 1;
            else
                uftagi["repeat.first"] = 0;

            if (i == cnt - 1)
                uftagi["repeat.last"] = 1;
            else
                uftagi["repeat.last"] = 0;


            uftagi["repeat.total"] = cnt;
            uftagi["repeat.index"] = i;
            uftagi["repeat.iteration"] = i + 1;
            uftagi["repeat.odd"] = i % 2;

            if (i % 2 != 0)
                uftagi["repeat.even"] = 0;
            else
                uftagi["repeat.even"] = 1;
            return uftagi;
        }

        public string tag_tplpath(string tag, string tpl_name)
        {
            string add_path = "";
            string result;

            if (tag.Substring(0, 2) == "./")
            {
                add_path = tpl_name;
                add_path = RX_LAST_SLASH.Replace(add_path, "");
                //remove ./ from the beginning
                result = tag[2..];
            }
            else
                result = tag;

            result = add_path + result;
            if (!RX_EXT.IsMatch(tag))
                result += ".html";

            return result;
        }

        private void tag_replace(ref string hpage_ref, ref string tag_full_ref, ref string value_ref, Hashtable hattrs)
        {
            if (string.IsNullOrEmpty(hpage_ref))
            {
                hpage_ref = "";
                return;
            }

            string tag_full = tag_full_ref;
            string value = "";
            if (value_ref.Length > 0)
                value = value_ref;

            int attr_count = hattrs.Count;
            if (attr_count > 0)
            {
                if (value.Length < 1 && hattrs.ContainsKey("default"))
                {
                    if (!string.IsNullOrEmpty((string)hattrs["default"]))
                        value = (string)hattrs["default"];
                    attr_count -= 1;
                }

                if (value.Length > 0 && attr_count > 0)
                {
                    if (hattrs.ContainsKey("htmlescape"))
                    {
                        value = Utils.htmlescape(value);
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("url"))
                    {
                        value = Utils.str2url(value);
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("number_format"))
                    {
                        var precision = (!string.IsNullOrEmpty((string)hattrs["number_format"]) ? Utils.f2int(hattrs["number_format"]) : 2);
                        var groupdigits = (hattrs.ContainsKey("nfthousands") && string.IsNullOrEmpty((string)hattrs["nfthousands"]) ? TriState.False : TriState.True); // default - group digits, but if nfthousands empty - don't
                        value = Strings.FormatNumber(Utils.f2float(value), precision, TriState.UseDefault, TriState.False, groupdigits);
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("date"))
                    {
                        string dformat = (string)hattrs["date"];
                        switch (dformat)
                        {
                            case "":
                                {
                                    dformat = DATE_FORMAT_DEF;
                                    break;
                                }

                            case "short":
                                {
                                    dformat = DATE_FORMAT_SHORT;
                                    break;
                                }

                            case "long":
                                {
                                    dformat = DATE_FORMAT_LONG;
                                    break;
                                }

                            case "sql":
                                {
                                    dformat = DATE_FORMAT_SQL;
                                    break;
                                }
                        }
                        if (DateTime.TryParse(value, out DateTime dt))
                            value = dt.ToString(dformat, System.Globalization.DateTimeFormatInfo.InvariantInfo);
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("trim"))
                    {
                        value = Strings.Trim(value);
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("nl2br"))
                    {
                        value = Regex.Replace(value, @"\r?\n", "<br>");
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("lower"))
                    {
                        value = value.ToLower();
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("upper"))
                    {
                        value = value.ToUpper();
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("capitalize"))
                    {
                        value = Utils.capitalize(value, (string)hattrs["capitalize"]);
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("truncate"))
                    {
                        value = Utils.str2truncate(value, hattrs);
                        attr_count -= 1;
                    }

                    // If attr_count > 0 AndAlso hattrs.ContainsKey("count") Then
                    // If TypeOf (value) Is ICollection Then
                    // value = CType(value, ICollection).Count
                    // Else
                    // fw.logger("WARN", "ParsePage - 'count' attribute used on non-array value")
                    // End If
                    // attr_count -= 1
                    // End If
                    if (attr_count > 0 && hattrs.ContainsKey("urlencode"))
                    {
                        value = HttpUtility.UrlEncode(value);
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("strip_tags"))
                    {
                        value = Regex.Replace(value, "<[^>]*(>|$)", " ");
                        value = Regex.Replace(value, @"[\s\r\n]+", " ");
                        value = Strings.Trim(value);
                        attr_count -= 1;
                    }
                    if (attr_count > 0 && hattrs.ContainsKey("markdown"))
                    {
                        try
                        {
                            if (mConvert == null)
                            {
                                // try to dynamic load CommonMark
                                System.Reflection.Assembly aCommonMark = System.Reflection.Assembly.Load("CommonMark");
                                Type tCommonMarkConverter = aCommonMark.GetType("CommonMark.CommonMarkConverter");
                                Type tCommonMarkSettings = aCommonMark.GetType("CommonMark.CommonMarkSettings");
                                CommonMarkSettings = tCommonMarkSettings.GetProperty("Default", tCommonMarkSettings).GetValue(null, null); //TODO MIGRATE .Clone()
                                //TODO MIGRATE CommonMarkSettings.RenderSoftLineBreaksAsLineBreaks = true;
                                // more default settings can be overriden here

                                mConvert = tCommonMarkConverter.GetMethod("Convert", new Type[] { typeof(string), aCommonMark.GetType("CommonMark.CommonMarkSettings") });
                            }

                            // equivalent of: value = CommonMarkConverter.Convert(value)
                            value = (string)mConvert.Invoke(null, new object[] { value, CommonMarkSettings });
                        }
                        catch (Exception ex)
                        {
                            fw.logger("WARN", @"error parsing markdown, check bin\CommonMark.dll exists");
                            fw.logger("DEBUG", ex.Message);
                        }

                        attr_count -= 1;
                    }
                }

                if (attr_count > 0 && hattrs.ContainsKey("inline"))
                {
                    // get just tag without attrs
                    string tag = RX_NOTS.Match(tag_full).Groups[1].Value;

                    // replace tag+inline tpl+close tag
                    string restr = Regex.Escape("<~" + tag_full + ">") + ".*?" + Regex.Escape("</~" + tag + ">");

                    // fw.logger(restr)
                    // fw.logger(hpage_ref)
                    // fw.logger(value)
                    // escape $0-$9 and ${...}, i.e. all $ chars replaced with $$
                    value = Regex.Replace(value, @"\$", "$$$$");
                    hpage_ref = Regex.Replace(hpage_ref, restr, value, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    attr_count -= 1;
                    return; // special case - if inline - exit now, no more replace necessary
                }
            }

            hpage_ref = Strings.Replace(hpage_ref, "<~" + tag_full + ">", value);
        }

        // if attrs["multi") ]efined - attrs["select") ]an contain strings with separator in attrs["multi") ]default ",") for multiple select
        private string _attr_select(string tag, string tpl_name, ref Hashtable hf, Hashtable attrs)
        {
            StringBuilder result = new();

            string sel_value = (string)hfvalue((string)attrs["select"], hf);
            if (sel_value == null)
                sel_value = "";

            var multi_delim = ""; // by default no multiple select
            if (attrs.ContainsKey("multi"))
            {
                if (!string.IsNullOrEmpty((string)attrs["multi"]))
                    multi_delim = (string)attrs["multi"];
                else
                    multi_delim = ",";
            }

            string[] asel;
            if (!string.IsNullOrEmpty(multi_delim))
            {
                asel = Strings.Split(sel_value, multi_delim);
                // trim all elements, so it would be simplier to compare
                for (int i = Information.LBound(asel); i <= Information.UBound(asel); i++)
                    asel[i] = Strings.Trim(asel[i]);
            }
            else
            {
                // no multi value
                asel = new string[1];
                asel[0] = sel_value;
            }

            if (hfvalue(tag, hf) is ICollection seloptions)
            {
                string value;
                // hf(tag) is ArrayList of Hashes with "id" and "iname" keys, for example rows returned from db.array('select id, iname from ...')
                // "id" key is optional, if not present - iname will be used for values too
                string desc;
                foreach (Hashtable item in seloptions)
                {
                    desc = Utils.htmlescape((string)item["iname"]);
                    if (item.ContainsKey("id"))
                        value = Strings.Trim((string)item["id"]);
                    else
                        value = Strings.Trim((string)item["iname"]);

                    // check for selected value before escaping
                    string selected;
                    if (Array.IndexOf(asel, value) != -1)
                        selected = " selected";
                    else
                        selected = "";

                    value = Utils.htmlescape(value);
                    _replace_commons(ref desc);

                    result.Append("<option value=\"").Append(value).Append('"').Append(selected).Append('>').Append(desc).Append("</option>" + Constants.vbCrLf);
                }
            }
            else
            {
                // just read from the plain text file
                var tpl_path = tag_tplpath(tag, tpl_name);
                if (Strings.Left(tpl_path, 1) != "/")
                    tpl_path = basedir + "/" + tpl_path;

                if (!File.Exists(TMPL_PATH + "/" + tpl_path))
                {
                    fw.logger(LogLevel.DEBUG, "ParsePage - NOR an ArrayList of Hashtables NEITHER .sel template file passed for a select tag=", tag);
                    return "";
                }

                string[] lines = FW.getFileLines(TMPL_PATH + "/" + tpl_path);
                foreach (string line in lines)
                {
                    if (line.Length < 2)
                        continue;
                    // line.chomp()
                    string[] arr = Strings.Split(line, "|", 2);
                    string value = Strings.Trim(arr[0]);
                    string desc = arr[1];

                    if (desc.Length < 1)
                        continue;
                    _replace_commons(ref desc);

                    // check for selected value before escaping
                    string selected;
                    if (Array.IndexOf(asel, value) != -1)
                        selected = " selected";
                    else
                        selected = "";

                    if (!attrs.ContainsKey("noescape"))
                    {
                        value = Utils.htmlescape(value);
                        desc = Utils.htmlescape(desc);
                    }

                    result.Append("<option value=\"").Append(value).Append('"').Append(selected).Append('>').Append(desc).Append("</option>");
                }
            }

            return result.ToString();
        }

        private string _attr_radio(string tpl_path, ref Hashtable hf, Hashtable attrs)
        {
            StringBuilder result = new StringBuilder();
            string sel_value = (string)hfvalue((string)attrs["radio"], hf);
            if (sel_value == null)
                sel_value = "";

            string name = (string)attrs["name"];
            string delim = (string)attrs["delim"]; // delimiter class

            if (Strings.Left(tpl_path, 1) != "/")
                tpl_path = basedir + "/" + tpl_path;

            string[] lines = FW.getFileLines(TMPL_PATH + "/" + tpl_path);

            int i = 0;
            foreach (string line in lines)
            {
                // line.chomp()
                if (line.Length < 2)
                    continue;

                string[] arr = Strings.Split(line, "|", 2);
                string value = arr[0];
                string desc = arr[1];

                if (desc.Length < 1)
                    continue;

                Hashtable parent_hf = new();
                desc = _parse_page("", hf, desc, ref parent_hf);

                if (!attrs.ContainsKey("noescape"))
                {
                    value = Utils.htmlescape(value);
                    desc = Utils.htmlescape(desc);
                }

                string name_id = name + "$" + i.ToString();
                string str_checked = "";

                if (value == sel_value)
                    str_checked = " checked='checked' ";

                // 'Bootstrap 3 style
                // If delim = "inline" Then
                // result.Append("<label class='radio-inline'><input type='radio' name=""").Append(name).Append(""" id=""").Append(name_id).Append(""" value=""").Append(value).Append("""").Append(str_checked).Append(">").Append(desc).Append("</label>")
                // Else
                // result.Append("<div class='radio'><label><input type='radio' name=""").Append(name).Append(""" id=""").Append(name_id).Append(""" value=""").Append(value).Append("""").Append(str_checked).Append(">").Append(desc).Append("</label></div>")
                // End If

                // Bootstrap 4 style
                result.Append("<div class='custom-control custom-radio ").Append(delim).Append("'><input class='custom-control-input' type='radio' name=\"").Append(name).Append("\" id=\"").Append(name_id).Append("\" value=\"").Append(value).Append("\"").Append(str_checked).Append("><label class='custom-control-label' for='").Append(name_id).Append("'>").Append(desc).Append("</label></div>");

                i += 1;
            }
            return result.ToString();
        }
        private string _attr_select_name(string tag, string tpl_name, ref Hashtable hf, Hashtable attrs)
        {
            string result = "";
            string sel_value = (string)hfvalue((string)attrs["selvalue"], hf);
            if (sel_value == null)
                sel_value = "";

            if (hfvalue(tag, hf) is ICollection seloptions)
            {
                string value;
                // hf(tag) is ArrayList of Hashes with "id" and "iname" keys, for example rows returned from db.array('select id, iname from ...')
                // "id" key is optional, if not present - iname will be used for values too

                string desc;
                foreach (Hashtable item in seloptions)
                {
                    if (item.ContainsKey("id"))
                        value = Strings.Trim((string)item["id"]);
                    else
                        value = Strings.Trim((string)item["iname"]);
                    desc = (string)item["iname"];

                    if (desc.Length < 1 | value != sel_value)
                        continue;
                    _replace_commons(ref desc);

                    result = desc;
                    break;
                }
            }
            else
            {
                var tpl_path = tag_tplpath(tag, tpl_name);
                if (Strings.Left(tpl_path, 1) != "/")
                    tpl_path = basedir + "/" + tpl_path;

                if (!File.Exists(TMPL_PATH + "/" + tpl_path))
                {
                    fw.logger(LogLevel.DEBUG, "ParsePage - NOR an ArrayList of Hashtables NEITHER .sel template file passed for a selvalue tag=", tag);
                    return "";
                }

                string[] lines = FW.getFileLines(TMPL_PATH + "/" + tpl_path);
                foreach (string line in lines)
                {
                    if (line.Length < 2)
                        continue;
                    // line.chomp()
                    string[] arr = Strings.Split(line, "|", 2);
                    string value = arr[0];
                    string desc = arr[1];

                    if (desc.Length < 1 | value != sel_value)
                        continue;
                    _replace_commons(ref desc);

                    result = desc;
                    break;
                }
            }

            return result;
        }

        private void _replace_commons(ref string desc)
        {
            parse_lang(ref desc);
        }

        // parse all `multilang` strings and replace to corresponding current language string
        private void parse_lang(ref string page)
        {
            if (!lang_parse)
                return; // don't parse langs if told so

            page = RX_LANG.Replace(page, lang_evaluator);
        }

        private string lang_replacer(Match m)
        {
            var value = m.Groups[1].Value.Trim();
            // fw.logger("checking:", lang, value)
            return langMap(value);
        }

        // 
        /// <summary>
        /// map input string (with optional context) into output accoring to the current lang
        /// </summary>
        /// <param name="str">input string in default language</param>
        /// <param name="context">optional context, for example "screename". Useful when same original string need to be translated differenly in different contexts</param>
        /// <returns>string in output languge</returns>
        public string langMap(string str, string context = "")
        {
            var input = str;
            if (!string.IsNullOrEmpty(context))
                input += "|" + context;
            Hashtable cache = (Hashtable)LANG_CACHE[lang];
            string result = (string)cache[input];
            if (string.IsNullOrEmpty(result))
            {
                // no translation found
                if (!string.IsNullOrEmpty(context))
                {
                    // if no value with context - try without context
                    result = (string)cache[str];
                    if (string.IsNullOrEmpty(result))
                    {
                        // if no such string in cache and we allowed to update lang file - add_lang
                        if (result == null && lang_update)
                            add_lang(str);
                        // if still no translation - return original string
                        result = str;
                    }
                }
                else
                    // if no translation - return original string
                    result = str;
            }
            // fw.logger("in=[" & str & "], out=[" & result & "]")
            return result;
        }

        private void load_lang()
        {
            // fw.logger("load lang: " & TMPL_PATH & "\" & lang & ".txt")
            var lines = FW.getFileLines(TMPL_PATH + @"\lang\" + lang + ".txt");

            if (LANG_CACHE[lang] == null)
                LANG_CACHE[lang] = new Hashtable();

            foreach (string line1 in lines)
            {
                string line = line1.Trim();
                if (string.IsNullOrEmpty(line) || !line.Contains("==="))
                    continue;
                var pair = Strings.Split(line, "===", 2);
                // fw.logger("added to cache:", Trim(pair(0)))
                ((Hashtable)LANG_CACHE[lang])[Strings.Trim(pair[0])] = Strings.LTrim(pair[1]);
            }
        }

        // add new language string to the lang file (for futher translation)
        private void add_lang(string str)
        {
            fw.logger(LogLevel.DEBUG, "ParsePage notice - updating lang [" + lang + "] with: " + str);
            string filedata = str + " === " + Constants.vbCrLf;
            FW.setFileContent(TMPL_PATH + @"\lang\" + lang + ".txt", ref filedata, true);

            // also add to lang cache
            ((Hashtable)LANG_CACHE[lang])[Strings.Trim(str)] = "";
        }
    }
}