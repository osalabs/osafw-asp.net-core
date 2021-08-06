using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
{
    public class ParsePage
    {
        public ParsePage(FW fw)
        {
        }

        public String parse_page(String bdir, String tpl_name, Hashtable hf)
        {
            return "";
        }
    }
}


/*
 Skip to content
Search or jump to…
Pull requests
Issues
Marketplace
Explore
 
@lebron2387 
osalabs
/
osafw-asp.net
2
13
Code
Issues
4
Pull requests
1
Actions
Projects
Wiki
Security
Insights
osafw-asp.net/www/App_Code/fw/ParsePage.vb
@osalabs
osalabs huge update, a lot of refactoring, fixes and enhancements in many areas
Latest commit 273741b 2 days ago
 History
 2 contributors
@osalabs@vladsavchuk
1124 lines (963 sloc)  49.6 KB
  
' ParsePage for ASP.NET - framework template engine
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2019 Oleg Savchuk www.osalabs.com
'
' supports:
' - SESSION, GLOBAL (from fw.G), SUBHASHES, SUBARRAYS, PARSEPAGE.TOP, PARSEPAGE.PARENT
' - <~tag if="var"> - var tested for true value (1, true, >"", but not "0")
' - CSRF shield - all vars escaped, if var shouldn't be escaped use "noescape" attr: <~raw_variable noescape>
' - 'attrs("select") can contain strings with separator ","(or custom defined) for multiple select
' - <~#commented_tag> - comment tags that doesn't need to be parsed (quickly replaced by empty string)

'# Supported attributes:

'var - tag is variable, no fileseek necessary
'ifXX - if confitions
'  ifeq="var" value="XXX" - tag/template will be parsed only if var=XXX
'  ifne="var" value="XXX" - tag/template will be parsed only if var!=XXX
'  ifgt="var" value="XXX" - tag/template will be parsed only if var>XXX
'  ifge="var" value="XXX" - tag/template will be parsed only if var>=XXX
'  iflt="var" value="XXX" - tag/template will be parsed only if var<XXX
'  ifle="var" value="XXX" - tag/template will be parsed only if var<=XXX
'  var can be ICollection (Hashtable/ArrayList/...)
'  <~tag if="ArrayList"> will fail if ArrayList.Count=0 or success if ArrayList.Count>0

'  ## old mapping
'  neq => ne
'  ge => gt
'  le => lt
'  gee => ge
'  lee => le

'vvalue - value as hf variable:
'  <~tag ifeq="var" vvalue="YYY"> - actual value got via hfvalue('YYY', $hf);

'#shortcuts
'<~tag if="var"> - tag will be shown if var is evaluated as TRUE, not using eval(), equivalent to "if ($var)"
'<~tag unless="var"> - tag will be shown if var is evaluated as TRUE, not using eval(), equivalent to "if (!$var)"
'-------------------------
'  TRUE values:
'  non-empty string, but not equal to "0" or "false"!
'  1 or other non-zero number
'  "true" string
'  true (boolean)

'  FALSE values:
' "0" or "false" string
'  0
'  false (boolean)
' ''
'  unset variable
'-------------------------

'repeat - this tag is repeat content ($hf hash should contain reference to array of hashes),
'  supported repeat vars:
'  repeat.first (0-not first, 1-first)
'  repeat.last  (0-not last, 1-last)
'  repeat.total (total number of items)
'  repeat.index  (0-based)
'  repeat.iteration (1-based)

'sub - this tag tell parser to use subhash for parse subtemplate ($hf hash should contain reference to hash), examples:
'   <~tag sub inline>...</~tag>- use $hf[tag] as hashtable for inline template
'   <~tag sub="var"> - use $hf[var] as hashtable for template in "tag.html"
'inline - this tag tell parser that subtemplate is not in file - it's between <~tag>...</~tag> , useful in combination with 'repeat' and 'if'
'global - this tag is a global var, not in $hf hash
' global[var] - also possible
'session - this tag is a $_SESSION var, not in $hf hash
' session[var] - also possible
'TODO parent - this tag is a $parent_hf var, not in current $hf hash
'select="var" [multi[=","]] - this tag tell parser to either load file with tag name and use it as value|display for <select> tag
'               or if variable with tag name exists - use it as arraylist of hashtables with id/iname keys
'             if "multi" attr defined - "var" value split by separator deinfed in multi attr (default is ",") and multiple options could be selected
'     , example:
'     <select name="item[fcombo]">
'     <option value=""> - select -
'     <~./fcombo.sel select="fcombo">  or <~fcombo_options select="fcombo">
'     </select>
'radio="var" name="YYY" [delim="inline"]- this tag tell parser to load file and use it as value|display for <input type=radio> tags, Bootsrtrap 3 style, example:
'     <~./fradio.sel radio="fradio" name="item[fradio]" delim="inline">
'selvalue="var" - display value (fetched from the .sel file) for the var (example: to display 'select' and 'radio' values in List view)
'     ex: <~../fcombo.sel selvalue="fcombo">
'TODO nolang - for subtemplates - use default language instead of current (usually english)
'htmlescape - replace special symbols by their html equivalents (such as <>,",')

'multi-language support `text` => replaced by language string from $site_templ/lang/$lang.txt according to fw.config('lang') (english by default)
'  example: <b>`Hello`</b>  -> become -> <b>Hola</b>
'  lang.txt line format:
'           english string === lang string
'           Hello === Hola
'support modifiers:
' htmlescape
' date          - format as datetime, sample "d M yyyy HH:mm", see https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings
'       <~var date>         output "M/d/yyyy" - date only (TODO - should be formatted per user settings actually)
'       <~var date="short"> output "M/d/yyyy hh:mm" - date and time short (to mins)
'       <~var date="long">  output "M/d/yyyy hh:mm:ss" - date and time long
'       <~var date="sql">   output "yyyy-MM-dd hh:mm:ss" - sql date and time
' url           - add http:// to begin of string if absent
' number_format - FormatNumber(value, 2) => 12345.12
' truncate      - truncate with options <~tag truncate="80" trchar="..." trword="1" trend="1">
' strip_tags
' trim
' nl2br
' TODO count         - for ICollection only
' lower
' upper
' capitalize        - capitalize first word, capitalize=all - capitalize all words
' default
' urlencode
' json (was var2js) - produces json-compatible string, example: {success:true, msg:""}
' markdown      - convert markdown text to html using CommonMark.NET (optional). Note: may wrap tag with <p>
' noparse       - doesn't parse file and just include file by tag path as is, ignores all other attrs except if

Imports System.IO

Public Class ParsePage
    Private Shared ReadOnly RX_NOTS As New Regex("^(\S+)", RegexOptions.Compiled)
    Private Shared ReadOnly RX_LANG As New Regex("`(.+?)`", RegexOptions.Compiled)
    Private Shared ReadOnly RX_FULL_TAGS As New Regex("<~([^>]+)>", RegexOptions.Compiled)

    Private Shared ReadOnly RX_ATTRS1 As New Regex("((?:\S+\=" + Chr(34) + "[^" + Chr(34) + "]*" + Chr(34) + ")|(?:\S+\='[^']*')|(?:[^'" + Chr(34) + "\s]+)|(?:\S+\=\S*))", RegexOptions.Compiled)
    Private Shared ReadOnly RX_ATTRS2 As New Regex("^([^\s\=]+)=(['" + Chr(34) + "]?)(.*?)\2$", RegexOptions.Compiled)

    Private Shared ReadOnly RX_ALL_DIGITS As New Regex("^\d+$", RegexOptions.Compiled)
    Private Shared ReadOnly RX_LAST_SLASH As New Regex("[^\/]+$", RegexOptions.Compiled)
    Private Shared ReadOnly RX_EXT As New Regex("\.[^\/]+$", RegexOptions.Compiled)

    Private Shared ReadOnly FILE_CACHE As New Hashtable
    Private Shared ReadOnly LANG_CACHE As New Hashtable
    Private Shared ReadOnly IFOPERS() As String = {"if", "unless", "ifne", "ifeq", "ifgt", "iflt", "ifge", "ifle"}

    Private Const DATE_FORMAT_DEF As String = "M/d/yyyy" ' for US, TODO make based on user settigns (with fallback to server's settings)
    Private Const DATE_FORMAT_SHORT As String = "M/d/yyyy HH:mm"
    Private Const DATE_FORMAT_LONG As String = "M/d/yyyy HH:mm:ss"
    Private Const DATE_FORMAT_SQL As String = "yyyy-MM-dd HH:mm:ss"
    '"d M yyyy HH:mm"

    'for dynamic load of CommonMark markdown converter
    Private Shared CommonMarkSettings As Object
    Private Shared mConvert As Reflection.MethodInfo

    Private ReadOnly fw As FW
    'checks if template files modifies and reload them, depends on config's "log_level"
    'true if level at least DEBUG, false for production as on production there are no tempalte file changes (unless during update, which leads to restart App anyway)
    Private ReadOnly is_check_file_modifications As Boolean = False
    Private ReadOnly TMPL_PATH As String
    Private basedir As String = ""
    Private data_top As Hashtable 'reference to the topmost hashtable
    Private is_found_last_hfvalue As Boolean = False
    Private ReadOnly lang As String = "en"
    Private ReadOnly lang_parse As Boolean = True 'parse lang strings in `` or not - true - parse(default), false - no
    Private ReadOnly lang_update As Boolean = True 'save unknown matches to lang file (helps during development) 
    Private ReadOnly lang_evaluator As MatchEvaluator

    Public Sub New(fw As FW)
        Me.fw = fw
        TMPL_PATH = fw.config("template")
        is_check_file_modifications = fw.config("log_level") >= LogLevel.DEBUG
        lang = fw.G("lang")
        If String.IsNullOrEmpty(lang) Then lang = fw.config("lang")
        If String.IsNullOrEmpty(lang) Then lang = "en"

        'load cache for all current lang matches 
        If LANG_CACHE(lang) Is Nothing Then
            load_lang()
        End If
        lang_evaluator = New MatchEvaluator(AddressOf Me.lang_replacer)

        lang_update = Utils.f2bool(fw.config("is_lang_update"))
    End Sub

    Public Function parse_json(ByVal hf As Object) As String
        Return Utils.jsonEncode(hf)
    End Function


    Public Function parse_page(ByVal bdir As String, ByVal tpl_name As String, ByVal hf As Hashtable) As String
        basedir = bdir
        Me.data_top = hf
        Dim parent_hf As Hashtable = New Hashtable
        'Return _parse_page(tpl_name, hf, "", "", parent_hf)

        'Dim start_time = DateTime.Now
        Dim result = _parse_page(tpl_name, hf, "", parent_hf)
        'Dim end_timespan As TimeSpan = DateTime.Now - start_time
        'fw.logger("ParsePage speed: " & String.Format("{0:0.000}", 1 / end_timespan.TotalSeconds) & "/s")
        Return result
    End Function

    Public Function parse_string(tpl As String, hf As Hashtable) As String
        basedir = "/"
        Dim parent_hf As Hashtable = New Hashtable
        Return _parse_page("", hf, tpl, parent_hf)
    End Function

    Private Function _parse_page(ByVal tpl_name As String, ByVal hf As Hashtable, ByVal page As String, ByRef parent_hf As Hashtable) As String
        If Left(tpl_name, 1) <> "/" Then tpl_name = basedir + "/" + tpl_name

        'fw.logger("DEBUG", "ParsePage - Parsing template = " + tpl_name + ", pagelen=" & page.Length)
        If page.Length < 1 Then page = precache_file(TMPL_PATH + tpl_name)

        If page.Length > 0 Then
            parse_lang(page)
            Dim page_orig As String = page
            Dim tags_full As MatchCollection = get_full_tags(page)

            If tags_full.Count > 0 Then
                sort_tags(tags_full)
                Dim TAGSEEN As Hashtable = New Hashtable
                Dim tag_match As Match
                Dim tag_full As String
                Dim tag As String
                Dim attrs As Hashtable
                Dim tag_value As Object
                Dim v As String

                For Each tag_match In tags_full
                    tag_full = tag_match.Groups(1).Value
                    If TAGSEEN.ContainsKey(tag_full) Then Continue For 'each tag (tag_full) parsed just once and replaces all occurencies of the tag in the page
                    TAGSEEN.Add(tag_full, 1)

                    tag = tag_full
                    attrs = New Hashtable
                    get_tag_attrs(tag, attrs)

                    'skip # commented tags and tags that not pass if
                    If tag(0) <> "#" AndAlso _attr_if(attrs, hf) Then
                        Dim inline_tpl As String = ""

                        If attrs.Count > 0 Then 'optimization, no need to check attrs if none passed
                            If attrs.ContainsKey("inline") Then
                                inline_tpl = get_inline_tpl(page_orig, tag, tag_full)
                            End If

                            If attrs.ContainsKey("session") Then
                                tag_value = hfvalue(tag, fw.SESSION)
                            ElseIf attrs.ContainsKey("global") Then
                                tag_value = hfvalue(tag, fw.G)
                            Else
                                tag_value = hfvalue(tag, hf, parent_hf)
                            End If
                        Else
                            tag_value = hfvalue(tag, hf, parent_hf)
                        End If

                        'fw.logger("ParsePage - tag: " & tag_full & ", found=" & is_found_last_hfvalue)
                        If tag_value.ToString().Length > 0 Then

                            Dim value As String
                            If attrs.ContainsKey("repeat") Then
                                value = _attr_repeat(tag, tag_value, tpl_name, inline_tpl, hf)
                            ElseIf attrs.ContainsKey("select") Then
                                ' this is special case for '<select>' HTML tag when options passed as ArrayList
                                value = _attr_select(tag, tpl_name, hf, attrs)
                            ElseIf attrs.ContainsKey("selvalue") Then
                                '    # this is special case for '<select>' HTML tag
                                value = _attr_select_name(tag, tpl_name, hf, attrs)
                                If Not attrs.ContainsKey("noescape") Then value = Utils.htmlescape(value)
                            ElseIf attrs.ContainsKey("sub") Then
                                value = _attr_sub(tag, tpl_name, hf, attrs, inline_tpl, parent_hf, tag_value)
                            Else
                                If attrs.ContainsKey("json") Then
                                    value = Utils.jsonEncode(tag_value)
                                Else
                                    value = tag_value.ToString()
                                End If
                                If value > "" AndAlso Not attrs.ContainsKey("noescape") Then value = Utils.htmlescape(value)
                            End If
                            tag_replace(page, tag_full, value, attrs)

                        ElseIf attrs.ContainsKey("repeat") Then
                            v = _attr_repeat(tag, tag_value, tpl_name, inline_tpl, hf)
                            tag_replace(page, tag_full, v, attrs)
                        ElseIf attrs.ContainsKey("var") Then
                            tag_replace(page, tag_full, "", attrs)
                        ElseIf attrs.ContainsKey("select") Then
                            '    # this is special case for '<select>' HTML tag
                            v = _attr_select(tag, tpl_name, hf, attrs)
                            tag_replace(page, tag_full, v, attrs)
                        ElseIf attrs.ContainsKey("selvalue") Then
                            '    # this is special case for '<select>' HTML tag
                            v = _attr_select_name(tag, tpl_name, hf, attrs)
                            If Not attrs.ContainsKey("noescape") Then v = Utils.htmlescape(v)
                            tag_replace(page, tag_full, v, attrs)
                        ElseIf attrs.ContainsKey("radio") Then
                            '    # this is special case for '<index type=radio>' HTML tag
                            v = _attr_radio(tag_tplpath(tag, tpl_name), hf, attrs)
                            tag_replace(page, tag_full, v, attrs)
                        ElseIf attrs.ContainsKey("noparse") Then
                            '   # no need to parse file - just include as is
                            Dim path = tag_tplpath(tag, tpl_name)
                            If Left(path, 1) <> "/" Then path = basedir + "/" + path
                            path = TMPL_PATH & path
                            tag_replace(page, tag_full, precache_file(path), New Hashtable)
                        Else

                            '    #also checking for sub
                            If attrs.ContainsKey("sub") Then
                                v = _attr_sub(tag, tpl_name, hf, attrs, inline_tpl, parent_hf, tag_value)
                            ElseIf is_found_last_hfvalue Then
                                'value found but empty
                                v = ""
                            Else
                                'value not found - looks like subtemplate in file
                                v = _parse_page(tag_tplpath(tag, tpl_name), hf, inline_tpl, parent_hf)
                            End If
                            tag_replace(page, tag_full, v, attrs)
                        End If

                    Else
                        tag_replace(page, tag_full, "", attrs)
                    End If

                Next tag_match
            Else
                'no tags in this template
            End If
        End If

        'FW.logger("DEBUG", "ParsePage - Parsing template = " & tpl_name & " END")
        Return page
    End Function

    Public Sub clear_cache()
        FILE_CACHE.Clear()
        LANG_CACHE.Clear()
    End Sub

    Private Function precache_file(ByVal filename As String) As String
        Dim modtime As String = ""
        Dim file_data As String = ""
        filename = Replace(filename, "/", "\")
        'fw.logger("preacaching [" & filename & "]")

        'check and get from cache
        If FILE_CACHE.ContainsKey(filename) Then
            Dim cached_item As Hashtable = FILE_CACHE(filename)
            'if debug is off - don't check modify time for better performance (but app restart would be necessary if template changed)
            If is_check_file_modifications Then
                modtime = File.GetLastWriteTime(filename)
                Dim mtmp As String = cached_item("modtime")
                If String.IsNullOrEmpty(mtmp) OrElse mtmp = modtime Then
                    Return cached_item("data")
                End If
            Else
                Return cached_item("data")
            End If
        Else
            If is_check_file_modifications Then modtime = File.GetLastWriteTime(filename)
        End If

        'fw.logger("ParsePage - try load file " & filename)
        'get from fs(if not in cache)
        If File.Exists(filename) Then
            file_data = FW.get_file_content(filename)
            If is_check_file_modifications AndAlso String.IsNullOrEmpty(modtime) Then modtime = File.GetLastWriteTime(filename)
        End If

        'get from fs(if not in cache)
        Dim cache As Hashtable = New Hashtable
        cache("data") = file_data
        cache("modtime") = modtime

        FILE_CACHE(filename) = cache
        'fw.logger("END preacaching [" & filename & "]")
        Return file_data

    End Function

    Private Function get_full_tags(ByRef page As String) As MatchCollection
        Return RX_FULL_TAGS.Matches(page)
    End Function

    Private Sub sort_tags(ByVal full_tags As MatchCollection)
        'TODO implement
    End Sub

    'Note: also strip tag to short tag
    Private Sub get_tag_attrs(ByRef tag As String, ByRef attrs As Hashtable)
        '        If Regex.IsMatch(tag, "\s") Then
        If tag.Contains(" ") Then
            Dim attrs_raw As MatchCollection = RX_ATTRS1.Matches(tag)

            tag = attrs_raw.Item(0).Value
            Dim i As Integer
            For i = 1 To attrs_raw.Count - 1
                Dim attr As String = attrs_raw.Item(i).Value
                Dim match As Match = RX_ATTRS2.Match(attr)
                If match.Success Then
                    Dim key As String = match.Groups(1).ToString()
                    Dim value As String = match.Groups(3).ToString()
                    attrs.Add(key, value)
                Else
                    attrs.Add(attr, "")
                End If
            Next
        End If
    End Sub

    'hf can be: Hashtable or HttpSessionState
    'returns: 
    '  value (string, hashtable, etc..), empty string "" 
    '  Or Nothing - tag not present in hf param (only if hf is Hashtable), file lookup will be necessary
    '  set is_found to True if tag value found hf/parent_hf (so can be used to detect if there are no tag value at all so no fileseek required)
    Private Function hfvalue(ByVal tag As String, ByVal hf As Object, Optional parent_hf As Hashtable = Nothing) As Object
        Dim tag_value As Object = ""
        Dim ptr As Object
        is_found_last_hfvalue = True

        Try

            If tag.Contains("[") Then
                Dim parts() As String = tag.Split("[")
                Dim start_pos As Integer = 0
                Dim parts0 As String = UCase(parts(0))

                If parts0 = "GLOBAL" Then
                    ptr = fw.G
                    start_pos = 1
                ElseIf parts0 = "SESSION" Then
                    ptr = fw.SESSION
                    start_pos = 1
                ElseIf parts0 = "PARSEPAGE.TOP" Then
                    ptr = Me.data_top
                    start_pos = 1
                ElseIf parts0 = "PARSEPAGE.PARENT" AndAlso parent_hf IsNot Nothing Then
                    ptr = parent_hf
                    start_pos = 1
                Else
                    ptr = hf
                End If

                Dim k As String
                For i As Integer = start_pos To parts.Length - 1
                    k = Regex.Replace(parts(i), "\].*?", "") 'remove last ]
                    If TypeOf (ptr) Is Array Then
                        If CInt(k) <= UBound(DirectCast(ptr, Array)) Then
                            ptr = DirectCast(ptr, Array).GetValue(CInt(k))
                        Else
                            ptr = "" 'out of Array bounds
                            Exit For
                        End If

                    ElseIf TypeOf (ptr) Is Hashtable Then
                        If DirectCast(ptr, Hashtable).ContainsKey(k) Then
                            ptr = DirectCast(ptr, Hashtable).Item(k)
                        Else
                            ptr = "" 'no such key in hash
                            Exit For
                        End If

                    ElseIf TypeOf (ptr) Is IList Then
                        ptr = DirectCast(ptr, IList).Item(k)

                    ElseIf TypeOf (ptr) Is HttpSessionState Then
                        If DirectCast(ptr, HttpSessionState).Item(k) IsNot Nothing Then
                            ptr = DirectCast(ptr, HttpSessionState).Item(k)
                        Else
                            ptr = "" 'no such key in session
                            Exit For
                        End If

                    Else
                        'looks like there are just no such key in array/hash OR ptr is not an array/hash at all - so return empty value
                        ptr = ""
                        Exit For
                    End If
                Next
                tag_value = ptr
            Else
                If TypeOf (hf) Is Hashtable Then
                    'special name tags - ROOT_URL and ROOT_DOMAIN - hardcoded here because of too frequent usage in the site
                    If tag = "ROOT_URL" OrElse tag = "ROOT_DOMAIN" Then
                        tag_value = fw.config(tag)
                    Else
                        If hf.ContainsKey(tag) Then
                            tag_value = hf(tag)
                        Else
                            'if no such tag in Hashtable
                            is_found_last_hfvalue = False
                        End If
                    End If
                ElseIf TypeOf (hf) Is HttpSessionState Then
                    If DirectCast(hf, HttpSessionState).Item(tag) IsNot Nothing Then
                        tag_value = DirectCast(hf, HttpSessionState).Item(tag)
                    End If

                ElseIf tag = "ROOT_URL" Then
                    tag_value = fw.config("ROOT_URL")

                ElseIf tag = "ROOT_DOMAIN" Then
                    tag_value = fw.config("ROOT_DOMAIN")
                Else
                    is_found_last_hfvalue = False
                End If
            End If
        Catch ex As Exception
            fw.logger(LogLevel.DEBUG, "ParsePage - error in hvalue for tag [", tag, "]:", ex.Message)
        End Try

        If tag_value Is Nothing Then tag_value = ""

        Return tag_value
    End Function

    Private Function _attr_sub(tag As String, tpl_name As String, hf As Hashtable, attrs As Hashtable, inline_tpl As String, parent_hf As Hashtable, tag_value As Object) As String
        If attrs("sub") > "" Then
            'if sub attr contains name - use it to get value from hf (instead using tag_value)
            tag_value = hfvalue(attrs("sub"), hf, parent_hf)
        End If
        If Not TypeOf (tag_value) Is Hashtable Then
            fw.logger(LogLevel.DEBUG, "ParsePage - not a Hash passed for a SUB tag=", tag, ", sub=" & attrs("sub"))
            tag_value = New Hashtable
        End If
        Return _parse_page(tag_tplpath(tag, tpl_name), tag_value, inline_tpl, parent_hf)
    End Function

    ' Check for misc if attrs
    Private Function _attr_if(ByVal attrs As Hashtable, ByVal hf As Hashtable) As Boolean
        If attrs.Count = 0 Then Return True ' if there are no if operation - return true anyway and early

        Dim oper As String = ""
        For i As Integer = 0 To UBound(IFOPERS)
            If attrs.ContainsKey(IFOPERS(i)) Then
                oper = IFOPERS(i)
                Exit For
            End If
        Next i
        If String.IsNullOrEmpty(oper) Then Return True ' if there are no if operation - return true anyway

        Dim eqvar As String = attrs(oper)
        If String.IsNullOrEmpty(eqvar) Then Return False 'return false if var need to be compared is empty

        Dim eqvalue As Object = hfvalue(eqvar, hf)
        If eqvalue Is Nothing Then eqvalue = ""

        'detect if eqvalue is integer
        Dim zzz As Integer = Int32.MinValue
        If Int32.TryParse(eqvalue.ToString(), zzz) Then eqvalue = zzz

        Dim ravalue As Object
        If attrs.ContainsKey("value") OrElse attrs.ContainsKey("vvalue") Then
            If attrs.ContainsKey("vvalue") Then
                ravalue = hfvalue(attrs("vvalue"), hf)
                If ravalue Is Nothing Then ravalue = ""
            Else
                ravalue = attrs("value")
            End If

            'convert ravalue to boolean if eqvalue is boolean, OR both to string otherwise
            If TypeOf (eqvalue) Is Boolean Then
                Dim ravaluestr As String = LCase(ravalue.ToString())
                If ravaluestr = "1" OrElse ravaluestr = "true" Then
                    ravalue = True
                Else
                    ravalue = False
                End If

            ElseIf TypeOf (eqvalue) Is Int32 Then
                'convert ravalue to Int32 for int comparisons
                If Not Int32.TryParse(ravalue.ToString(), zzz) Then
                    'ravalue is not an integer, so try string comparison
                    ravalue = ravalue.ToString()
                    eqvalue = eqvalue.ToString()
                End If
            ElseIf TypeOf (eqvalue) Is ICollection Then
                'if we comparing to Hashtable or ArrayList - we actually compare to .Count
                'so <~tag if="ArrayList"> will fail if ArrayList.Count=0 or success if ArrayList.Count>0
                eqvalue = DirectCast(eqvalue, ICollection).Count
            Else
                eqvalue = eqvalue.ToString()
                ravalue = ravalue.ToString()
            End If
        Else
            'special case - if no value attr - check for boolean
            ravalue = True
            'TRUE = non-empty string, but not equal "0";"false";non-0 number, true (boolean), or ICollection.Count>0
            If TypeOf (eqvalue) Is Boolean Then
                eqvalue = eqvalue
            ElseIf TypeOf (eqvalue) Is ICollection Then
                eqvalue = DirectCast(eqvalue, ICollection).Count > 0
            ElseIf Not (TypeOf (eqvalue) Is Boolean) Then
                Dim eqstr As String = eqvalue.ToString()
                eqvalue = eqstr > "" AndAlso eqstr <> "0" AndAlso LCase(eqstr) <> "false"
            Else
                eqvalue = False
            End If
        End If

        Dim result As Boolean = False
        If oper = "if" AndAlso eqvalue = True Then
            result = True
        ElseIf oper = "unless" AndAlso eqvalue = False Then
            result = True
        ElseIf oper = "ifeq" AndAlso eqvalue.ToString() = ravalue.ToString() Then
            result = True
        ElseIf oper = "ifne" AndAlso eqvalue <> ravalue Then
            result = True
        ElseIf oper = "iflt" AndAlso eqvalue < ravalue Then
            result = True
        ElseIf oper = "ifgt" AndAlso eqvalue > ravalue Then
            result = True
        ElseIf oper = "ifge" AndAlso eqvalue >= ravalue Then
            result = True
        ElseIf oper = "ifle" AndAlso eqvalue <= ravalue Then
            result = True
        End If

        Return result
    End Function

    Private Function get_inline_tpl(ByRef hpage As String, ByRef tag As String, ByRef tag_full As String) As String
        'fw.logger("ParsePage - get_inline_tpl: ", tag, " | ", tag_full)
        Dim re As String = Regex.Escape("<~" + tag_full + ">") + "(.*?)" + Regex.Escape("</~" + tag + ">")

        Dim inline_match As Match = Regex.Match(hpage, re, RegexOptions.Singleline Or RegexOptions.IgnoreCase)

        Dim inline_tpl As String = inline_match.Groups(1).Value

        Return inline_tpl
    End Function

    'return ready HTML
    Private Function _attr_repeat(ByRef tag As String, ByRef tag_val_array As Object, ByRef tpl_name As String, ByRef inline_tpl As String, parent_hf As Hashtable) As String
        'Validate: if input doesn't contain array - return "" - nothing to repeat
        If Not TypeOf (tag_val_array) Is IList Then
            If tag_val_array IsNot Nothing AndAlso tag_val_array.ToString() <> "" Then
                fw.logger(LogLevel.DEBUG, "ParsePage - Not an ArrayList passed to repeat tag=", tag)
            End If
            Return ""
        End If

        Dim value As New StringBuilder
        If parent_hf Is Nothing Then parent_hf = New Hashtable

        Dim ttpath As String = tag_tplpath(tag, tpl_name)

        For i As Integer = 0 To tag_val_array.Count - 1
            Dim row = proc_repeat_modifiers(tag_val_array, i)
            value.Append(_parse_page(ttpath, row, inline_tpl, parent_hf))
        Next
        Return value.ToString()
    End Function

    Private Function proc_repeat_modifiers(uftag As IList, i As Integer) As Hashtable
        Dim uftagi As Hashtable = uftag(i).Clone() 'make a shallow copy as we modify this level
        Dim cnt As Integer = uftag.Count

        If i = 0 Then
            uftagi("repeat.first") = 1
        Else
            uftagi("repeat.first") = 0
        End If

        If i = cnt - 1 Then
            uftagi("repeat.last") = 1
        Else
            uftagi("repeat.last") = 0
        End If


        uftagi("repeat.total") = cnt
        uftagi("repeat.index") = i
        uftagi("repeat.iteration") = i + 1
        uftagi("repeat.odd") = i Mod 2

        If i Mod 2 Then
            uftagi("repeat.even") = 0
        Else
            uftagi("repeat.even") = 1
        End If
        Return uftagi
    End Function

    Function tag_tplpath(ByVal tag As String, ByVal tpl_name As String) As String
        Dim add_path As String = ""
        Dim result As String

        'If Regex.IsMatch(tag, "^\.\/") Then
        If Left(tag, 2) = "./" Then
            add_path = tpl_name
            add_path = RX_LAST_SLASH.Replace(add_path, "")
            'result = Regex.Replace(result, "^\.\/", "")
            result = Replace(tag, "./", "", 1, 1)
        Else
            result = tag
        End If

        result = add_path + result
        If Not RX_EXT.IsMatch(tag) Then
            result &= ".html"
        End If

        Return result
    End Function

    Private Sub tag_replace(ByRef hpage_ref As String, ByRef tag_full_ref As String, ByRef value_ref As String, ByRef hattrs As Hashtable)
        If String.IsNullOrEmpty(hpage_ref) Then
            hpage_ref = ""
            Exit Sub
        End If

        Dim tag_full As String = tag_full_ref
        Dim value As Object = ""
        If value_ref.Length > 0 Then
            value = value_ref
        End If

        Dim attr_count As Integer = hattrs.Count
        If attr_count > 0 Then 'if there are some attrs passed - check and modify value

            If value.Length < 1 AndAlso hattrs.ContainsKey("default") Then
                If hattrs("default").Length > 0 Then value = hattrs("default")
                attr_count -= 1
            End If

            If value.Length > 0 AndAlso attr_count > 0 Then
                If hattrs.ContainsKey("htmlescape") Then
                    value = Utils.htmlescape(value)
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("url") Then
                    value = Utils.str2url(value)
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("number_format") Then
                    Dim precision = IIf(hattrs("number_format") > "", Utils.f2int(hattrs("number_format")), 2)
                    Dim groupdigits = IIf(hattrs.ContainsKey("nfthousands") AndAlso hattrs("nfthousands") = "", TriState.False, TriState.True) 'default - group digits, but if nfthousands empty - don't
                    value = FormatNumber(Utils.f2float(value), precision, TriState.UseDefault, TriState.False, groupdigits)
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("date") Then
                    Dim dformat As String = hattrs("date")
                    Select Case dformat
                        Case ""
                            dformat = DATE_FORMAT_DEF
                        Case "short"
                            dformat = DATE_FORMAT_SHORT
                        Case "long"
                            dformat = DATE_FORMAT_LONG
                        Case "sql"
                            dformat = DATE_FORMAT_SQL
                    End Select
                    Dim dt As DateTime
                    If DateTime.TryParse(value, dt) Then
                        value = dt.ToString(dformat, System.Globalization.DateTimeFormatInfo.InvariantInfo)
                    End If
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("trim") Then
                    value = Trim(value)
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("nl2br") Then
                    value = Regex.Replace(value, "\r?\n", "<br>")
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("lower") Then
                    value = LCase(value)
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("upper") Then
                    value = UCase(value)
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("capitalize") Then
                    value = Utils.capitalize(value, hattrs("capitalize"))
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("truncate") Then
                    value = Utils.str2truncate(value, hattrs)
                    attr_count -= 1
                End If

                'If attr_count > 0 AndAlso hattrs.ContainsKey("count") Then
                '    If TypeOf (value) Is ICollection Then
                '        value = CType(value, ICollection).Count
                '    Else
                '        fw.logger("WARN", "ParsePage - 'count' attribute used on non-array value")
                '    End If
                '   attr_count -= 1
                'End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("urlencode") Then
                    value = HttpUtility.UrlEncode(value)
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("strip_tags") Then
                    value = Regex.Replace(value, "<[^>]*(>|$)", " ")
                    value = Regex.Replace(value, "[\s\r\n]+", " ")
                    value = Trim(value)
                    attr_count -= 1
                End If
                If attr_count > 0 AndAlso hattrs.ContainsKey("markdown") Then
                    Try
                        If mConvert Is Nothing Then
                            'try to dynamic load CommonMark
                            Dim aCommonMark As Reflection.Assembly = Reflection.Assembly.Load("CommonMark")
                            Dim tCommonMarkConverter As Type = aCommonMark.GetType("CommonMark.CommonMarkConverter")
                            Dim tCommonMarkSettings As Type = aCommonMark.GetType("CommonMark.CommonMarkSettings")
                            CommonMarkSettings = tCommonMarkSettings.GetProperty("Default", tCommonMarkSettings).GetValue(Nothing, Nothing).Clone()
                            CommonMarkSettings.RenderSoftLineBreaksAsLineBreaks = True
                            'more default settings can be overriden here

                            mConvert = tCommonMarkConverter.GetMethod("Convert", New Type() {GetType(String), aCommonMark.GetType("CommonMark.CommonMarkSettings")})
                        End If

                        'equivalent of: value = CommonMarkConverter.Convert(value)
                        value = mConvert.Invoke(Nothing, New Object() {value, CommonMarkSettings})
                    Catch ex As Exception
                        fw.logger("WARN", "error parsing markdown, check bin\CommonMark.dll exists")
                        fw.logger("DEBUG", ex.Message)
                    End Try

                    attr_count -= 1
                End If
            End If

            If attr_count > 0 AndAlso hattrs.ContainsKey("inline") Then
                'get just tag without attrs
                Dim tag As String = RX_NOTS.Match(tag_full).Groups(1).Value

                'replace tag+inline tpl+close tag
                Dim restr As String = Regex.Escape("<~" + tag_full + ">") + ".*?" + Regex.Escape("</~" + tag + ">")

                'fw.logger(restr)
                'fw.logger(hpage_ref)
                'fw.logger(value)
                'escape $0-$9 and ${...}, i.e. all $ chars replaced with $$
                value = Regex.Replace(value, "\$", "$$$$")
                hpage_ref = Regex.Replace(hpage_ref, restr, value, RegexOptions.IgnoreCase Or RegexOptions.Singleline)

                attr_count -= 1
                Exit Sub 'special case - if inline - exit now, no more replace necessary
            End If
        End If

        hpage_ref = Replace(hpage_ref, "<~" & tag_full & ">", value)
    End Sub

    'if attrs("multi") defined - attrs("select") can contain strings with separator in attrs("multi") (default ",") for multiple select
    Private Function _attr_select(tag As String, tpl_name As String, ByRef hf As Hashtable, ByRef attrs As Hashtable) As String
        Dim result As New StringBuilder

        Dim sel_value As String = hfvalue(attrs("select"), hf)
        If sel_value Is Nothing Then sel_value = ""

        Dim multi_delim = "" 'by default no multiple select
        If attrs.ContainsKey("multi") Then
            If attrs("multi") > "" Then
                multi_delim = attrs("multi")
            Else
                multi_delim = ","
            End If
        End If

        Dim asel As String()
        If multi_delim > "" Then
            asel = Split(sel_value, multi_delim)
            'trim all elements, so it would be simplier to compare
            For i As Integer = LBound(asel) To UBound(asel)
                asel(i) = Trim(asel(i))
            Next
        Else
            'no multi value
            ReDim asel(0)
            asel(0) = sel_value
        End If

        Dim seloptions As Object = hfvalue(tag, hf)
        If TypeOf seloptions Is ICollection Then
            ' hf(tag) is ArrayList of Hashes with "id" and "iname" keys, for example rows returned from db.array('select id, iname from ...')
            ' "id" key is optional, if not present - iname will be used for values too
            Dim value As String, desc As String
            For Each item As Hashtable In seloptions
                desc = Utils.htmlescape(item("iname"))
                If item.ContainsKey("id") Then
                    value = Trim(item("id"))
                Else
                    value = Trim(item("iname"))
                End If

                'check for selected value before escaping
                Dim selected As String
                If Array.IndexOf(asel, value) <> -1 Then
                    selected = " selected"
                Else
                    selected = ""
                End If

                value = Utils.htmlescape(value)
                _replace_commons(desc)

                result.Append("<option value=""").Append(value).Append("""").Append(selected).Append(">").Append(desc).Append("</option>" & vbCrLf)
            Next

        Else
            'just read from the plain text file
            Dim tpl_path = tag_tplpath(tag, tpl_name)
            If Left(tpl_path, 1) <> "/" Then tpl_path = basedir + "/" + tpl_path

            If Not File.Exists(TMPL_PATH + "/" + tpl_path) Then
                fw.logger(LogLevel.DEBUG, "ParsePage - NOR an ArrayList of Hashtables NEITHER .sel template file passed for a select tag=", tag)
                Return ""
            End If

            Dim lines As String() = FW.get_file_lines(TMPL_PATH + "/" + tpl_path)
            Dim line As String

            For Each line In lines
                If line.Length < 2 Then Continue For
                '            line.chomp()
                Dim arr() As String = Split(line, "|", 2)
                Dim value As String = Trim(arr(0))
                Dim desc As String = arr(1)

                If desc.Length < 1 Then Continue For
                _replace_commons(desc)

                'check for selected value before escaping
                Dim selected As String
                If Array.IndexOf(asel, value) <> -1 Then
                    selected = " selected"
                Else
                    selected = ""
                End If

                If Not attrs.ContainsKey("noescape") Then
                    value = Utils.htmlescape(value)
                    desc = Utils.htmlescape(desc)
                End If

                result.Append("<option value=""").Append(value).Append("""").Append(selected).Append(">").Append(desc).Append("</option>")
            Next
        End If

        Return result.ToString
    End Function

    Private Function _attr_radio(ByVal tpl_path As String, ByRef hf As Hashtable, ByRef attrs As Hashtable) As String
        Dim result As New StringBuilder
        Dim sel_value As String = hfvalue(attrs("radio"), hf)
        If sel_value Is Nothing Then sel_value = ""

        Dim name As String = attrs("name")
        Dim delim As String = attrs("delim") 'delimiter class

        If Left(tpl_path, 1) <> "/" Then tpl_path = basedir + "/" + tpl_path

        Dim lines As String() = FW.get_file_lines(TMPL_PATH + "/" + tpl_path)

        Dim i As Integer = 0
        Dim line As String
        For Each line In lines
            'line.chomp()
            If line.Length < 2 Then Continue For

            Dim arr() As String = Split(line, "|", 2)
            Dim value As String = arr(0)
            Dim desc As String = arr(1)

            If desc.Length < 1 Then Continue For

            Dim parent_hf As Hashtable = New Hashtable
            desc = _parse_page("", hf, desc, parent_hf)

            If Not attrs.ContainsKey("noescape") Then
                value = Utils.htmlescape(value)
                desc = Utils.htmlescape(desc)
            End If

            Dim name_id As String = name & "$" & i.ToString
            Dim str_checked As String = ""

            If value = sel_value Then str_checked = " checked='checked' "

            ''Bootstrap 3 style
            'If delim = "inline" Then
            '    result.Append("<label class='radio-inline'><input type='radio' name=""").Append(name).Append(""" id=""").Append(name_id).Append(""" value=""").Append(value).Append("""").Append(str_checked).Append(">").Append(desc).Append("</label>")
            'Else
            '    result.Append("<div class='radio'><label><input type='radio' name=""").Append(name).Append(""" id=""").Append(name_id).Append(""" value=""").Append(value).Append("""").Append(str_checked).Append(">").Append(desc).Append("</label></div>")
            'End If

            'Bootstrap 4 style
            result.Append("<div class='custom-control custom-radio ").Append(delim).Append("'><input class='custom-control-input' type='radio' name=""").Append(name).Append(""" id=""").Append(name_id).Append(""" value=""").Append(value).Append("""").Append(str_checked).Append("><label class='custom-control-label' for='").Append(name_id).Append("'>").Append(desc).Append("</label></div>")

            i += 1
        Next
        Return result.ToString
    End Function
    Private Function _attr_select_name(tag As String, tpl_name As String, ByRef hf As Hashtable, ByRef attrs As Hashtable) As String
        Dim result As String = ""
        Dim sel_value As String = hfvalue(attrs("selvalue"), hf)
        If sel_value Is Nothing Then sel_value = ""

        Dim seloptions As Object = hfvalue(tag, hf)
        If TypeOf seloptions Is ICollection Then
            ' hf(tag) is ArrayList of Hashes with "id" and "iname" keys, for example rows returned from db.array('select id, iname from ...')
            ' "id" key is optional, if not present - iname will be used for values too

            Dim value As String, desc As String
            For Each item As Hashtable In hf(tag)
                If item.ContainsKey("id") Then
                    value = Trim(item("id"))
                Else
                    value = Trim(item("iname"))
                End If
                desc = item("iname")

                If desc.Length < 1 Or value <> sel_value Then Continue For
                _replace_commons(desc)

                result = desc
                Exit For
            Next

        Else
            Dim tpl_path = tag_tplpath(tag, tpl_name)
            If Left(tpl_path, 1) <> "/" Then tpl_path = basedir + "/" + tpl_path

            If Not File.Exists(TMPL_PATH + "/" + tpl_path) Then
                fw.logger(LogLevel.DEBUG, "ParsePage - NOR an ArrayList of Hashtables NEITHER .sel template file passed for a selvalue tag=", tag)
                Return ""
            End If

            Dim lines As String() = FW.get_file_lines(TMPL_PATH + "/" + tpl_path)

            Dim line As String
            For Each line In lines
                If line.Length < 2 Then Continue For
                '            line.chomp()
                Dim arr() As String = Split(line, "|", 2)
                Dim value As String = arr(0)
                Dim desc As String = arr(1)

                If desc.Length < 1 Or value <> sel_value Then Continue For
                _replace_commons(desc)

                result = desc
                Exit For
            Next
        End If

        Return result
    End Function

    Private Sub _replace_commons(ByRef desc As String)
        parse_lang(desc)
        'desc = Regex.Replace(desc, "<~ROOT_URL>", FW.config("ROOT_URL"))
        ' ${ $_[0] }=~ s/<~ROOT_DOMAIN>/$PATH{ROOT_DOMAIN}/ig;
        ' ${ $_[0] }=~ s/<~ROOT_URL>/$PATH{ROOT_URL}/ig;
        ' ${ $_[0] }=~ s/<~STATIC_URL>/$PATH{STATIC_URL}/ig;
    End Sub

    'parse all `multilang` strings and replace to corresponding current language string
    Private Sub parse_lang(ByRef page As String)
        If Not lang_parse Then Exit Sub 'don't parse langs if told so

        page = RX_LANG.Replace(page, lang_evaluator)
    End Sub

    Private Function lang_replacer(m As Match) As String
        Dim value = Trim(m.Groups(1).Value)
        'fw.logger("checking:", lang, value)
        Return langMap(value)
    End Function

    '
    ''' <summary>
    ''' map input string (with optional context) into output accoring to the current lang
    ''' </summary>
    ''' <param name="str">input string in default language</param>
    ''' <param name="context">optional context, for example "screename". Useful when same original string need to be translated differenly in different contexts</param>
    ''' <returns>string in output languge</returns>
    Public Function langMap(str As String, Optional context As String = "") As String
        Dim input = str
        If context > "" Then input &= "|" & context
        Dim result = LANG_CACHE(lang)(input)
        If String.IsNullOrEmpty(result) Then
            'no translation found
            If context > "" Then
                'if no value with context - try without context
                result = LANG_CACHE(lang)(str)
                If String.IsNullOrEmpty(result) Then
                    'if no such string in cache and we allowed to update lang file - add_lang
                    If result Is Nothing AndAlso lang_update Then add_lang(str)
                    'if still no translation - return original string
                    result = str
                End If
            Else
                'if no translation - return original string
                result = str
            End If
        End If
        'fw.logger("in=[" & str & "], out=[" & result & "]")
        Return result
    End Function

    Private Sub load_lang()
        'fw.logger("load lang: " & TMPL_PATH & "\" & lang & ".txt")
        Dim lines = FW.get_file_lines(TMPL_PATH & "\lang\" & lang & ".txt")

        If LANG_CACHE(lang) Is Nothing Then
            LANG_CACHE(lang) = New Hashtable
        End If

        For Each line In lines
            line = Trim(line)
            If String.IsNullOrEmpty(line) OrElse Not line.Contains("===") Then Continue For
            Dim pair = Split(line, "===", 2)
            'fw.logger("added to cache:", Trim(pair(0)))
            LANG_CACHE(lang)(Trim(pair(0))) = LTrim(pair(1))
        Next
    End Sub

    'add new language string to the lang file (for futher translation)
    Private Sub add_lang(str As String)
        fw.logger(LogLevel.DEBUG, "ParsePage notice - updating lang [" & lang & "] with: " & str)
        FW.set_file_content(TMPL_PATH & "\lang\" & lang & ".txt", str & " === " & vbCrLf, True)

        'also add to lang cache
        LANG_CACHE(lang)(Trim(str)) = ""
    End Sub

End Class
© 2021 GitHub, Inc.
Terms
Privacy
Security
Status
Docs
Contact GitHub
Pricing
API
Training
Blog
About
Loading complete
 
 */