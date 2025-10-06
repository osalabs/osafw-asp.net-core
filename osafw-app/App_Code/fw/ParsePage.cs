// ParsePage for ASP.NET - framework template engine
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com
/*
Parses file templates and replaces <~tags> with values from hashtable

 Supports features:
 - SESSION, GLOBAL (from fw.G), SUBHASHES, SUBARRAYS, PARSEPAGE.TOP, PARSEPAGE.PARENT
 - <~tag if="var"> - var tested for true value (1, true, >"", but not "0")
 - CSRF shield - all vars escaped, if var shouldn't be escaped use "noescape" attr: < ~raw_variable noescape >
  - attrs["select") ]an contain strings with separator ","(or custom defined) for multiple select
  - <~#commented_tag> - comment tags that doesn't need to be parsed(quickly replaced by empty string)
- <~subfolder/*.html> - will include all html files in folder (basically read all files per mask and parse them as a single template)
  - <~subfolder/*.*> - will include all files in folder

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
  nolang - do not parse lang strings in ``, files only, not "inline" templates (this automatically applied for .js files)
  htmlescape - replace special symbols by their html equivalents (such as <>,",')

 multi-language support `text` => replaced by language string from $site_templ/lang/$lang.txt according to fw.config('lang') (english by default)
  example: <b>`Hello`</b>  -> become -> <b>Hola</b>
  lang.txt line format:
            english string === lang string
            Hello === Hola
  note: .js files are not parsed for `lang` strings as `` is used for ES6 template strings

 support modifiers:
  htmlescape
  date          - format as datetime, sample "d M yyyy HH:mm", see https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings
        <~var date>         output "M/d/yyyy" - date only (default, formatted per user settings actually)
        <~var date="short"> output "M/d/yyyy hh:mm" - date and time short (to mins)
        <~var date="long">  output "M/d/yyyy hh:mm:ss" - date and time long
        <~var date="sql">   output "yyyy-MM-dd hh:mm:ss" - sql date and time
  url           - add http:// to begin of string if absent
  number_format - FormatNumber(value, 2) => 12345.12
  currency      - FormatCurrency(value, 2) => $12,345.12
  bytes         - Utils.bytes2str(value) => 12.1 MB
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
  markdown      - convert markdown text to html using Markdig (optional). Note: may wrap tag with <p>
  noparse       - doesn't parse file and just include file by tag path as is, ignores all other attrs except if
*/

using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace osafw;

public class ParsePageOptions
{
    public string TemplatesRoot { get; set; } = "";
    public bool IsCheckFileModifications { get; set; } = false;
    public string Lang { get; set; } = "en";
    public bool IsLangUpdate { get; set; } = true;
    public Func<Hashtable> GlobalsGetter { get; set; } = () => []; // by default - return empty hashtable
    public ISession Session { get; set; }
    public Action<LogLevel, string[]> Logger { get; set; }

    // date formats - if empty - use defaults
    public string DateFormat { get; set; } = "";
    public string DateFormatShort { get; set; } = "";
    public string DateFormatLong { get; set; } = "";
    public string InputTimezone { get; set; } = ""; // timezone of the input date values (i.e. read from db)
    public string OutputTimezone { get; set; } = ""; // timezone for parsed date output (i.e. to display on site)
}

public class ParsePage
{
    private static readonly Regex RX_NOTS = new(@"^(\S+)", RegexOptions.Compiled);
    private static readonly Regex RX_LANG = new("`(.+?)`", RegexOptions.Compiled);
    private static readonly Regex RX_FULL_TAGS = new("<~([^>]+)>", RegexOptions.Compiled);

    private static readonly Regex RX_ATTRS1 = new(@"((?:\S+\=" + (char)34 + "[^" + (char)34 + "]*" + (char)34 + @")|(?:\S+\='[^']*')|(?:[^'" + (char)34 + @"\s]+)|(?:\S+\=\S*))", RegexOptions.Compiled);
    private static readonly Regex RX_ATTRS2 = new(@"^([^\s\=]+)=(['" + (char)34 + @"]?)(.*?)\2$", RegexOptions.Compiled);

    private static readonly Regex RX_LAST_SLASH = new(@"[^\/]+$", RegexOptions.Compiled);
    private static readonly Regex RX_EXT = new(@"\.[^\/]+$", RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<string, Hashtable> FILE_CACHE = new();
    private static readonly ConcurrentDictionary<string, Hashtable> LANG_CACHE = new();
    private static readonly ConcurrentDictionary<string, Dictionary<string, Func<object, object>>> class_mapping_cache = new(); // cache for object property/field getters

    private static readonly string[] IFOPERS = ["if", "unless", "ifne", "ifeq", "ifgt", "iflt", "ifge", "ifle"];

    // defaults for date formats
    private const string DATE_FORMAT_DEF = "M/d/yyyy";
    private const string DATE_FORMAT_SHORT = "M/d/yyyy HH:mm";
    private const string DATE_FORMAT_LONG = "M/d/yyyy HH:mm:ss";
    private const string DATE_FORMAT_SQL = "yyyy-MM-dd HH:mm:ss";
    private const string DATE_TIMEZONE_DEF = "UTC";
    // "d M yyyy HH:mm"

    // current date formats - may be changed in constructor based on user settings
    private static string DateFormat = DATE_FORMAT_DEF;
    private static string DateFormatShort = DATE_FORMAT_SHORT;
    private static string DateFormatLong = DATE_FORMAT_LONG;
    private static string InputTimezone = DATE_TIMEZONE_DEF;
    private static string OutputTimezone = DATE_TIMEZONE_DEF;

    // for dynamic load of Markdig markdown converter
    private static System.Reflection.MethodInfo mMarkdownToHtml;
    private static object MarkdownPipeline;

    private readonly Func<Hashtable> globalsGetter;
    private readonly ISession session;
    private readonly Action<LogLevel, string[]> loggerAction;
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
    private static readonly char path_separator = Path.DirectorySeparatorChar;

    public ParsePage(ParsePageOptions options = null)
    {
        if (options != null)
        {
            TMPL_PATH = options.TemplatesRoot;
            is_check_file_modifications = options.IsCheckFileModifications;
            lang = options.Lang ?? "en";
            lang_update = options.IsLangUpdate;
            globalsGetter = options.GlobalsGetter ?? (() => []); // by default - return empty hashtable
            session = options.Session;
            loggerAction = options.Logger ?? ((level, messages) => { }); // by default - no logging

            // set date formats based on user settings
            if (!string.IsNullOrEmpty(options.DateFormat))
                DateFormat = options.DateFormat;
            if (!string.IsNullOrEmpty(options.DateFormatShort))
                DateFormatShort = options.DateFormatShort;
            if (!string.IsNullOrEmpty(options.DateFormatLong))
                DateFormatLong = options.DateFormatLong;
            if (!string.IsNullOrEmpty(options.InputTimezone))
                InputTimezone = options.InputTimezone;
            if (!string.IsNullOrEmpty(options.OutputTimezone))
                OutputTimezone = options.OutputTimezone;

            if (!LANG_CACHE.ContainsKey(lang) && !string.IsNullOrEmpty(TMPL_PATH))
                load_lang();
        }
        lang_evaluator = new MatchEvaluator(this.lang_replacer);
    }

    public string parse_json(object hf)
    {
        return Utils.jsonEncode(hf);
    }


    public string parse_page(string bdir, string tpl_name, Hashtable hf)
    {
        this.basedir = bdir;
        this.data_top = hf;
        Hashtable parent_hf = [];
        // Return _parse_page(tpl_name, hf, "", "", parent_hf)

        // Dim start_time = DateTime.Now
        var result = _parse_page(tpl_name, hf, "", parent_hf);
        // Dim end_timespan As TimeSpan = DateTime.Now - start_time
        // logger("ParsePage speed: " & String.Format("{0:0.000}", 1 / end_timespan.TotalSeconds) & "/s")
        return result;
    }

    public string parse_string(string tpl, Hashtable hf)
    {
        basedir = "/";
        Hashtable parent_hf = [];
        return _parse_page("", hf, tpl, parent_hf);
    }

    private string _parse_page(string tpl_name, Hashtable hf, string page, Hashtable parent_hf, Hashtable parent_attrs = null)
    {
        if (tpl_name == null)
        {
            logger(LogLevel.DEBUG, "ParsePage - layout template (tpl_name) is null");
            return "";
        }

        if (!string.IsNullOrEmpty(tpl_name) && !tpl_name.StartsWith('/'))
            tpl_name = basedir + "/" + tpl_name;

        logger(LogLevel.TRACE, $"ParsePage - Parsing template = {tpl_name}, pagelen={page.Length}");
        if (page.Length < 1)
            page = precache_file(TMPL_PATH + tpl_name);
        if (page.Length == 0)
            return "";

        parent_attrs ??= [];
        //parse lang if caller attrs doesn't have "nolang" key and tpl_name is not .js file
        if (!parent_attrs.ContainsKey("nolang") && !tpl_name.EndsWith(".js"))
            parse_lang(ref page);

        string page_orig = page;
        MatchCollection tags_full = get_full_tags(ref page);
        if (tags_full.Count == 0)
            return page; // no tags - return early without parsing

        // parse tags on page

        sort_tags(tags_full);
        Hashtable TAGSEEN = [];
        string tag_full;
        string tag;
        Hashtable attrs;
        object tag_value;
        string v;

        foreach (Match tag_match in tags_full)
        {
            tag_full = tag_match.Groups[1].Value;
            if (TAGSEEN.ContainsKey(tag_full))
                continue; // each tag (tag_full) parsed just once and replaces all occurences of the tag in the page
            TAGSEEN.Add(tag_full, 1);

            tag = tag_full;
            attrs = [];
            get_tag_attrs(ref tag, attrs);

            // skip # commented tags and tags that not pass if
            if (tag[0] != '#' && _attr_if(attrs, hf))
            {
                string inline_tpl = "";

                if (attrs.Count > 0)
                {
                    if (attrs.ContainsKey("inline"))
                        inline_tpl = get_inline_tpl(ref page_orig, ref tag, ref tag_full);

                    if (attrs.ContainsKey("session"))
                        tag_value = hfvalue(tag, session != null ? session : new Hashtable());
                    else if (attrs.ContainsKey("global"))
                        tag_value = hfvalue(tag, globalsGetter());
                    else
                        tag_value = hfvalue(tag, hf, parent_hf);
                }
                else
                    tag_value = hfvalue(tag, hf, parent_hf);

                // logger("ParsePage - tag: " & tag_full & ", found=" & is_found_last_hfvalue)
                if (tag_value.ToString().Length > 0)
                {
                    string value;
                    if (attrs.ContainsKey("repeat"))
                        value = _attr_repeat(ref tag, ref tag_value, ref tpl_name, ref inline_tpl, hf);
                    else if (attrs.ContainsKey("select"))
                    {
                        // this is special case for '<select>' HTML tag when options passed as ArrayList
                        value = _attr_select(tag, tpl_name, hf, attrs);
                    }
                    else if (attrs.ContainsKey("selvalue"))
                    {
                        // # this is special case for '<select>' HTML tag
                        value = _attr_select_name(tag, tpl_name, hf, attrs);
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
                    v = _attr_select(tag, tpl_name, hf, attrs);
                    tag_replace(ref page, ref tag_full, ref v, attrs);
                }
                else if (attrs.ContainsKey("selvalue"))
                {
                    // # this is special case for '<select>' HTML tag
                    v = _attr_select_name(tag, tpl_name, hf, attrs);
                    if (!attrs.ContainsKey("noescape"))
                        v = Utils.htmlescape(v);
                    tag_replace(ref page, ref tag_full, ref v, attrs);
                }
                else if (attrs.ContainsKey("radio"))
                {
                    // # this is special case for '<index type=radio>' HTML tag
                    v = _attr_radio(tag_tplpath(tag, tpl_name), hf, attrs);
                    tag_replace(ref page, ref tag_full, ref v, attrs);
                }
                else if (attrs.ContainsKey("noparse"))
                {
                    // # no need to parse file - just include as is
                    var path = tag_tplpath(tag, tpl_name);
                    if (!path.StartsWith('/'))
                        path = basedir + "/" + path;
                    path = TMPL_PATH + path;
                    var file_content = precache_file(path);
                    tag_replace(ref page, ref tag_full, ref file_content, []);
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
                        v = _parse_page(tag_tplpath(tag, tpl_name), hf, inline_tpl, parent_hf, attrs);
                    tag_replace(ref page, ref tag_full, ref v, attrs);
                }
            }
            else
            {
                string tmp_value = "";
                tag_replace(ref page, ref tag_full, ref tmp_value, attrs);
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

    /// <summary>
    /// read precached file and split it into lines (ignores empty lines)
    /// </summary>
    /// <param name="filename"></param>
    /// <returns>empty array if no content</returns>
    private string[] precache_file_lines(string filename)
    {
        var content = precache_file(filename);
        if (string.IsNullOrEmpty(content))
            return [];
        return Regex.Split(content, "[\r\n]+");
    }

    private string precache_file(string filename)
    {
        // if filename contains a mask - load all files via recursive calls and return as a single template
        if (filename.Contains('*'))
        {
            string folder = Path.GetDirectoryName(filename);
            string mask = Path.GetFileName(filename);
            string[] files = Directory.GetFiles(folder, mask);
            StringBuilder sb = new();
            foreach (var file in files)
                sb.Append(precache_file(file));
            return sb.ToString();
        }

        string modtime = "";
        string file_data = "";
        //For Windows - replace Unix-style separators / to \
        if (path_separator == '\\')
            filename = filename.Replace('/', path_separator);
        // logger("preacaching [" & filename & "]")

        // check and get from cache
        if (FILE_CACHE.TryGetValue(filename, out Hashtable cached_item))
        {
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

        // logger("ParsePage - try load file " & filename)
        // get from fs(if not in cache)
        if (File.Exists(filename))
        {
            file_data = Utils.getFileContent(filename);
            if (is_check_file_modifications && string.IsNullOrEmpty(modtime))
                modtime = File.GetLastWriteTime(filename).ToString();
        }

        // get from fs(if not in cache)
        Hashtable cache = [];
        cache["data"] = file_data;
        cache["modtime"] = modtime;

        FILE_CACHE[filename] = cache;
        // logger("END preacaching [" & filename & "]")
        return file_data;
    }

    private static MatchCollection get_full_tags(ref string page)
    {
        return RX_FULL_TAGS.Matches(page);
    }

    private static void sort_tags(MatchCollection full_tags)
    {
        //TODO implement
    }

    // Note: also strip tag to short tag
    private static void get_tag_attrs(ref string tag, Hashtable attrs)
    {
        // If Regex.IsMatch(tag, "\s") Then
        if (tag.Contains(' '))
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
    private object hfvalue(string tag, object hf, Hashtable parent_hf = null)
    {
        object tag_value = "";
        object ptr;
        is_found_last_hfvalue = true;

        try
        {
            if (tag.Contains('['))
            {
                string[] parts = tag.Split("[");
                int start_pos = 0;
                string parts0 = parts[0].ToUpper();

                if (parts0 == "GLOBAL")
                {
                    ptr = globalsGetter();
                    start_pos = 1;
                }
                else if (parts0 == "SESSION")
                {
                    ptr = session != null ? session : new Hashtable();
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
                    // if ptr is null - break
                    if (ptr == null || ptr is string str)
                    {
                        ptr = ""; // no such key in array/hash/object or ptr is not array/hash/object at all
                        break;
                    }

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
                    else if (ptr is IDictionary dict)
                    {
                        if (dict.Contains(k))
                            ptr = dict[k];
                        else
                        {
                            ptr = ""; // no such key in hash
                            break;
                        }
                    }
                    else if (ptr is IList list)
                    {
                        ptr = list[k.toInt()];
                    }
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
                        // try to get property/field value from arbitrary object
                        ptr = valueFromObjectByName(ptr, k);
                        if (ptr == null)
                        {
                            ptr = ""; // no such property/field in object
                            break;
                        }
                    }
                }
                tag_value = ptr;
            }
            else if (hf is Hashtable hashtable)
            {
                // special name tags - ROOT_URL and ROOT_DOMAIN - hardcoded here because of too frequent usage in the site
                if (tag == "ROOT_URL" || tag == "ROOT_DOMAIN")
                    tag_value = globalsGetter()[tag];
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
            else if (hf != null)
            {
                // try to get property/field value from arbitrary object
                ptr = valueFromObjectByName(hf, tag);
                if (ptr != null)
                    tag_value = ptr;
                else
                    is_found_last_hfvalue = false;
            }
            else if (tag == "ROOT_URL" || tag == "ROOT_DOMAIN")
                tag_value = globalsGetter()[tag]; // special name tags, see above
            else
                is_found_last_hfvalue = false;
        }
        catch (Exception ex)
        {
            logger(LogLevel.DEBUG, "ParsePage - error in hvalue for tag [", tag, "]:", ex.Message);
        }

        tag_value ??= "";

        return tag_value;
    }

    private string _attr_sub(string tag, string tpl_name, Hashtable hf, Hashtable attrs, string inline_tpl, Hashtable parent_hf, object tag_value)
    {
        Hashtable sub_hf = null;
        string sub = (string)attrs["sub"];
        if (!string.IsNullOrEmpty(sub))
            // if sub attr contains name - use it to get value from hf (instead using tag_value)
            tag_value = hfvalue(sub, hf, parent_hf);

        if (tag_value is DBRow row)
            sub_hf = row.toHashtable();
        else if (tag_value is Hashtable ht)
            sub_hf = ht;
        else if (tag_value != null)
            sub_hf = objectToHashtable(tag_value);

        if (sub_hf == null)
        {
            logger(LogLevel.DEBUG, "ParsePage - not a collection passed for a SUB tag=", tag, ", sub=" + sub);
            sub_hf = [];
        }

        return _parse_page(tag_tplpath(tag, tpl_name), sub_hf, inline_tpl, parent_hf, attrs);
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
        eqvalue ??= "";

        // detect if eqvalue is integer
        if (Int32.TryParse(eqvalue.ToString(), out int zzz))
            eqvalue = zzz;

        object ravalue;
        bool is_numeric_comparison = false;
        if (attrs.ContainsKey("value") || attrs.ContainsKey("vvalue"))
        {
            if (attrs.ContainsKey("vvalue"))
            {
                ravalue = hfvalue((string)attrs["vvalue"], hf);
                ravalue ??= "";
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
            else if (eqvalue is not bool)
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
        else if (oper == "ifeq" && (is_numeric_comparison && eqvalue.toInt() == ravalue.toInt() || !is_numeric_comparison && String.Compare(eqvalue.ToString(), ravalue.ToString()) == 0))
            result = true;
        else if (oper == "ifne" && (is_numeric_comparison && eqvalue.toInt() != ravalue.toInt() || !is_numeric_comparison && String.Compare(eqvalue.ToString(), ravalue.ToString()) != 0))
            result = true;
        else if (oper == "iflt" && (is_numeric_comparison && eqvalue.toInt() < ravalue.toInt() || !is_numeric_comparison && String.Compare(eqvalue.ToString(), ravalue.ToString()) < 0))
            result = true;
        else if (oper == "ifgt" && (is_numeric_comparison && eqvalue.toInt() > ravalue.toInt() || !is_numeric_comparison && String.Compare(eqvalue.ToString(), ravalue.ToString()) > 0))
            result = true;
        else if (oper == "ifge" && (is_numeric_comparison && eqvalue.toInt() >= ravalue.toInt() || !is_numeric_comparison && String.Compare(eqvalue.ToString(), ravalue.ToString()) >= 0))
            result = true;
        else if (oper == "ifle" && (is_numeric_comparison && eqvalue.toInt() <= ravalue.toInt() || !is_numeric_comparison && String.Compare(eqvalue.ToString(), ravalue.ToString()) <= 0))
            result = true;

        return result;
    }

    private static string get_inline_tpl(ref string hpage, ref string tag, ref string tag_full)
    {
        // logger("ParsePage - get_inline_tpl: ", tag, " | ", tag_full)
        string re = Regex.Escape("<~" + tag_full + ">") + "(.*?)" + Regex.Escape("</~" + tag + ">");

        Match inline_match = Regex.Match(hpage, re, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        string inline_tpl = inline_match.Groups[1].Value;

        return inline_tpl;
    }

    // return ready HTML
    private string _attr_repeat(ref string tag, ref object tag_val_array, ref string tpl_name, ref string inline_tpl, Hashtable parent_hf)
    {
        // Validate: if input doesn't contain array - return "" - nothing to repeat
        if (tag_val_array is not IList)
        {
            if (tag_val_array != null && tag_val_array.ToString() != "")
                logger(LogLevel.DEBUG, "ParsePage - Not an ArrayList passed to repeat tag=", tag);
            return "";
        }

        StringBuilder value = new();
        parent_hf ??= [];

        string ttpath = tag_tplpath(tag, tpl_name);

        IList list = (IList)tag_val_array;
        for (int i = 0; i <= list.Count - 1; i++)
        {
            var row = proc_repeat_modifiers(list, i);
            value.Append(_parse_page(ttpath, row, inline_tpl, parent_hf));
        }
        return value.ToString();
    }

    private static Hashtable proc_repeat_modifiers(IList uftag, int i)
    {
        Hashtable uftagi1;
        if (uftag[i] is DBRow row)
            uftagi1 = row;
        else if (uftag[i] is Hashtable ht)
            uftagi1 = ht;
        else
            uftagi1 = objectToHashtable(uftag[i]) ?? [];

        Hashtable uftagi = (Hashtable)uftagi1.Clone(); // make a shallow copy as we modify this level
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

        if (tag.StartsWith("./"))
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
        {
            value = value_ref;
        }

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
                    var precision = (!string.IsNullOrEmpty((string)hattrs["number_format"]) ? hattrs["number_format"].toInt() : 2);
                    bool groupdigits = !hattrs.ContainsKey("nfthousands") || !string.IsNullOrEmpty((string)hattrs["nfthousands"]); // default - group digits, but if nfthousands empty - don't

                    value = value.toFloat().ToString("N" + precision, CultureInfo.InvariantCulture);
                    if (!groupdigits)
                    {
                        value = value.Replace(NumberFormatInfo.InvariantInfo.NumberGroupSeparator, "");
                    }

                    attr_count -= 1;
                }
                if (attr_count > 0 && hattrs.ContainsKey("currency"))
                {
                    value = value.toFloat().ToString("C2");
                    attr_count -= 1;
                }
                if (attr_count > 0 && hattrs.ContainsKey("bytes"))
                {
                    // format numeric value (bytes) to human-readable string
                    value = Utils.bytes2str(value.toLong());
                    attr_count -= 1;
                }
                if (attr_count > 0 && hattrs.ContainsKey("date"))
                {
                    string dformat = (string)hattrs["date"];
                    switch (dformat)
                    {
                        case "":
                            {
                                dformat = DateFormat;
                                break;
                            }

                        case "short":
                            {
                                dformat = DateFormatShort;
                                break;
                            }

                        case "long":
                            {
                                dformat = DateFormatLong;
                                break;
                            }

                        case "sql":
                            {
                                dformat = DATE_FORMAT_SQL;
                                break;
                            }
                    }
                    if (DateTime.TryParse(value, out DateTime dt))
                    {
                        // convert to specified timezone
                        dt = convertTimezone(dt, InputTimezone, OutputTimezone);

                        value = dt.ToString(dformat, DateTimeFormatInfo.InvariantInfo);
                    }

                    attr_count -= 1;
                }
                if (attr_count > 0 && hattrs.ContainsKey("trim"))
                {
                    value = value.Trim();
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
                // logger("WARN", "ParsePage - 'count' attribute used on non-array value")
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
                    value = value.Trim();
                    attr_count -= 1;
                }

                if (attr_count > 0 && hattrs.ContainsKey("markdown"))
                {
                    // try to dynamically load Markdig, equivalent to:
                    // var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                    // or var pipeline = new MarkdownPipelineBuilder().Configure("common+gfm-pipetables+emphasisextras+listextras+footers+citations+attributes+abbreviations+figures+bootstrap+medialinks+autoidentifiers+tasklists+autolinks").Build();
                    // var result = Markdown.ToHtml(value, pipeline);
                    try
                    {
                        if (mMarkdownToHtml == null)
                        {
                            System.Reflection.Assembly aMarkdig = System.Reflection.Assembly.Load("Markdig");
                            Type tMarkdown = aMarkdig.GetType("Markdig.Markdown");
                            Type tMarkdownPipeline = aMarkdig.GetType("Markdig.MarkdownPipeline");
                            Type tMarkdownPipelineBuilder = aMarkdig.GetType("Markdig.MarkdownPipelineBuilder");
                            Type tMarkdownExtensions = aMarkdig.GetType("Markdig.MarkdownExtensions");
                            Type tMarkdownParserContext = aMarkdig.GetType("Markdig.MarkdownParserContext");

                            mMarkdownToHtml = tMarkdown.GetMethod("ToHtml", [typeof(string), tMarkdownPipeline, tMarkdownParserContext]);

                            var configureMethod = tMarkdownExtensions.GetMethod("Configure");

                            var pipelineBuilder = Activator.CreateInstance(tMarkdownPipelineBuilder);
                            configureMethod.Invoke(null, [pipelineBuilder, "common+hardlinebreak+gfm-pipetables+emphasisextras+listextras+footers+citations+abbreviations+figures+bootstrap+medialinks+autoidentifiers+tasklists+autolinks+customcontainers+attributes"]);

                            MarkdownPipeline = tMarkdownPipelineBuilder.GetMethod("Build").Invoke(pipelineBuilder, null);
                        }

                        if (mMarkdownToHtml != null)
                            value = (string)mMarkdownToHtml.Invoke(null, [value, MarkdownPipeline, null]);
                    }
                    catch (Exception ex)
                    {
                        logger(LogLevel.WARN, @"error parsing markdown, install Markdig package");
                        logger(LogLevel.DEBUG, ex.Message);
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

                // logger(restr)
                // logger(hpage_ref)
                // logger(value)
                // escape $0-$9 and ${...}, i.e. all $ chars replaced with $$
                value = Regex.Replace(value, @"\$", "$$$$");
                hpage_ref = Regex.Replace(hpage_ref, restr, value, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                return; // special case - if inline - exit now, no more replace necessary
            }
        }

        hpage_ref = hpage_ref.Replace("<~" + tag_full + ">", value);
    }

    // if attrs["multi"] defined - attrs["select"] can contain strings with separator in attrs["multi"] (default ",") for multiple select
    private string _attr_select(string tag, string tpl_name, Hashtable hf, Hashtable attrs)
    {
        StringBuilder result = new();

        string sel_value = hfvalue((string)attrs["select"] ?? "", hf).toStr();
        //fw.logger($"_attr_select: tag={tag}, tpl_name={tpl_name}", attrs, hf[tag]);

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
            asel = sel_value.Split(multi_delim);
            // trim all elements, so it would be simplier to compare
            for (int i = asel.GetLowerBound(0); i <= asel.GetUpperBound(0); i++)
            {
                asel[i] = asel[i].Trim();
            }
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
                {
                    value = ((string)item["id"]).Trim();
                }
                else
                {
                    value = ((string)item["iname"]).Trim();
                }

                // check for selected value before escaping
                string selected;
                if (Array.IndexOf(asel, value) != -1)
                {
                    selected = " selected";
                }
                else
                {
                    selected = "";
                }

                value = Utils.htmlescape(value);
                _replace_commons(ref desc);

                result.Append("<option value=\"").Append(value).Append('"').Append(selected).Append('>').Append(desc).Append("</option>" + System.Environment.NewLine);
            }
        }
        else
        {
            // just read from the plain text file
            var tpl_path = tag_tplpath(tag, tpl_name);
            if (!tpl_path.StartsWith('/'))
                tpl_path = basedir + "/" + tpl_path;

            string[] lines = precache_file_lines(TMPL_PATH + "/" + tpl_path);
            if (lines.Length == 0)
            {
                logger(LogLevel.TRACE, $"ParsePage - NOR an ArrayList of Hashtables NEITHER .sel template file passed for a select tag={tag}");
                return "";
            }

            foreach (string line in lines)
            {
                if (line.Length < 2)
                    continue;
                // line.chomp()
                string[] arr = line.Split("|", 2);
                string value = arr[0].Trim();
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

    private string _attr_radio(string tpl_path, Hashtable hf, Hashtable attrs)
    {
        StringBuilder result = new();
        string sel_value = (string)hfvalue((string)attrs["radio"], hf) ?? "";
        string name = (string)attrs["name"];
        string delim = (string)attrs["delim"]; // delimiter class

        if (!tpl_path.StartsWith('/'))
            tpl_path = basedir + "/" + tpl_path;

        string[] lines = precache_file_lines(TMPL_PATH + "/" + tpl_path);
        if (lines.Length == 0)
        {
            logger(LogLevel.TRACE, $"ParsePage - NOR an ArrayList of Hashtables NEITHER .sel template file passed for a radio tag tpl path={tpl_path}");
            return "";
        }

        int i = 0;
        foreach (string line in lines)
        {
            // line.chomp()
            if (line.Length < 2)
                continue;

            string[] arr = line.Split("|", 2);
            string value = arr[0];
            string desc = arr[1];

            if (desc.Length < 1)
                continue;

            Hashtable parent_hf = [];
            desc = _parse_page("", hf, desc, parent_hf, attrs);

            if (!attrs.ContainsKey("noescape"))
            {
                value = Utils.htmlescape(value);
                desc = Utils.htmlescape(desc);
            }

            string name_id = name + "$" + i.ToString();
            string str_checked = "";

            if (value == sel_value)
            {
                str_checked = " checked='checked' ";
            }

            // 'Bootstrap 3 style
            // If delim = "inline" Then
            // result.Append("<label class='radio-inline'><input type='radio' name=""").Append(name).Append(""" id=""").Append(name_id).Append(""" value=""").Append(value).Append("""").Append(str_checked).Append(">").Append(desc).Append("</label>")
            // Else
            // result.Append("<div class='radio'><label><input type='radio' name=""").Append(name).Append(""" id=""").Append(name_id).Append(""" value=""").Append(value).Append("""").Append(str_checked).Append(">").Append(desc).Append("</label></div>")
            // End If

            // Bootstrap 4 style
            // result.Append("<div class='custom-control custom-radio ").Append(delim).Append("'><input class='custom-control-input' type='radio' name=\"").Append(name).Append("\" id=\"").Append(name_id).Append("\" value=\"").Append(value).Append("\"").Append(str_checked).Append("><label class='custom-control-label' for='").Append(name_id).Append("'>").Append(desc).Append("</label></div>");

            // Bootstrap 5 style
            result.Append("<div class='form-check ").Append(delim).Append("'><input class='form-check-input' type='radio' name=\"").Append(name).Append("\" id=\"").Append(name_id).Append("\" value=\"").Append(value).Append('"').Append(str_checked).Append("><label class='form-check-label' for='").Append(name_id).Append("'>").Append(desc).Append("</label></div>");

            i += 1;
        }
        return result.ToString();
    }
    private string _attr_select_name(string tag, string tpl_name, Hashtable hf, Hashtable attrs)
    {
        string result = "";
        string sel_value = hfvalue((string)attrs["selvalue"], hf).toStr();
        sel_value ??= "";

        if (hfvalue(tag, hf) is ICollection seloptions)
        {
            string value;
            // hf(tag) is ArrayList of Hashes with "id" and "iname" keys, for example rows returned from db.array('select id, iname from ...')
            // "id" key is optional, if not present - iname will be used for values too

            string desc;
            foreach (Hashtable item in seloptions)
            {
                if (item.ContainsKey("id"))
                {
                    value = ((string)item["id"]).Trim();
                }
                else
                {
                    value = ((string)item["iname"]).Trim();
                }
                desc = (string)item["iname"];

                if (desc.Length < 1 | value != sel_value) continue;

                _replace_commons(ref desc);

                result = desc;
                break;
            }
        }
        else
        {
            var tpl_path = tag_tplpath(tag, tpl_name);
            if (!tpl_path.StartsWith('/'))
                tpl_path = basedir + "/" + tpl_path;

            string[] lines = precache_file_lines(TMPL_PATH + "/" + tpl_path);
            if (lines.Length == 0)
            {
                logger(LogLevel.TRACE, $"ParsePage - NOR an ArrayList of Hashtables NEITHER .sel template file passed for a selvalue tag={tag}");
                return "";
            }

            foreach (string line in lines)
            {
                if (line.Length < 2)
                    continue;
                // line.chomp()
                string[] arr = line.Split("|", 2);
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
        if (!lang_parse || page == null)
            return; // don't parse langs if told so or page is null to avoid Regex exception

        page = RX_LANG.Replace(page, lang_evaluator);
    }

    private string lang_replacer(Match m)
    {
        var value = m.Groups[1].Value.Trim();
        // logger("checking:", lang, value)
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
        Hashtable cache = (Hashtable)LANG_CACHE.GetOrAdd(lang, _ => []);
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
        // logger("in=[" & str & "], out=[" & result & "]")
        return result;
    }

    private void load_lang()
    {
        // logger("load lang: " & TMPL_PATH & "\" & lang & ".txt")
        var lines = Utils.getFileLines(TMPL_PATH + @"\lang\" + lang + ".txt");

        LANG_CACHE.GetOrAdd(lang, _ => []);

        foreach (string line1 in lines)
        {
            string line = line1.Trim();
            if (string.IsNullOrEmpty(line) || !line.Contains("==="))
            {
                continue;
            }
            string[] pair = line.Split("===", 2);
            // logger("added to cache:", Trim(pair(0)))
            ((Hashtable)LANG_CACHE[lang])[pair[0].Trim()] = pair[1].TrimStart();
        }
    }

    // add new language string to the lang file (for futher translation)
    private void add_lang(string str)
    {
        logger(LogLevel.DEBUG, "ParsePage notice - updating lang [" + lang + "] with: " + str);
        string filedata = str + " === " + System.Environment.NewLine;
        Utils.setFileContent(TMPL_PATH + @"\lang\" + lang + ".txt", ref filedata, true);

        // also add to lang cache
        LANG_CACHE.GetOrAdd(lang, _ => []);
        ((Hashtable)LANG_CACHE[lang])[str.Trim()] = "";
    }

    private void logger(LogLevel level, params string[] args)
    {
        loggerAction?.Invoke(level, args);
    }

    private DateTime convertTimezone(DateTime dt, string from_tz, string to_tz)
    {
        if (from_tz == to_tz)
            return dt;
        try
        {
            TimeZoneInfo tzi_from = TimeZoneInfo.FindSystemTimeZoneById(from_tz);
            TimeZoneInfo tzi_to = TimeZoneInfo.FindSystemTimeZoneById(to_tz);
            DateTime dt_utc = TimeZoneInfo.ConvertTimeToUtc(dt, tzi_from);
            DateTime dt_to = TimeZoneInfo.ConvertTimeFromUtc(dt_utc, tzi_to);
            return dt_to;
        }
        catch (Exception ex)
        {
            // invalid timezone
            logger(LogLevel.TRACE, "ParsePage - invalid timezone conversion from " + from_tz + " to " + to_tz + ": " + ex.Message);
            return dt;
        }
    }


    /// <summary>
    /// Get readable public instance properties and fields (lowercased for further comparison) of object,
    /// cached by class name in class_mapping_cache. Values are getter delegates to fetch from object instance.
    /// </summary>
    /// <param name="o"></param>
    /// <returns>Dictionary of property/field name to getter delegate</returns>
    private static Dictionary<string, Func<object, object>> objectMembers(object o)
    {
        Type type = o.GetType();
        return class_mapping_cache.GetOrAdd(type.FullName, _ =>
        {
            var dict = new Dictionary<string, Func<object, object>>(StringComparer.OrdinalIgnoreCase);

            // properties
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                    continue; // skip write-only and indexer properties

                string key = prop.Name;
                if (!dict.ContainsKey(key))
                {
                    dict[key] = (obj) =>
                    {
                        try { return prop.GetValue(obj); }
                        catch { return null; }
                    };
                }
            }

            // fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                string key = field.Name;
                if (!dict.ContainsKey(key))
                {
                    dict[key] = (obj) =>
                    {
                        try { return field.GetValue(obj); }
                        catch { return null; }
                    };
                }
            }

            return dict;
        });
    }

    private static object valueFromObjectByName(object o, string name)
    {
        var getters = objectMembers(o);
        if (getters.TryGetValue(name, out var getter))
        {
            return getter(o);
        }
        return null;
    }

    /// <summary>
    /// Convert object public properties and fields to Hashtable
    /// </summary>
    /// <param name="o"></param>
    /// <returns></returns>
    private static Hashtable objectToHashtable(object o)
    {
        Hashtable result = [];
        // return empty hashtable if null or it's a simple type
        if (o == null || o is string || o is ValueType)
            return result;

        var getters = objectMembers(o);
        foreach (var kvp in getters)
        {
            try
            {
                var val = kvp.Value(o);
                if (val != null)
                    result[kvp.Key] = val;
            }
            catch { }
        }

        return result;
    }

}
