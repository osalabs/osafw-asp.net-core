// FW Core
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace osafw;

public class FW : IDisposable
{
    //controller standard actions
    public const string ACTION_SUFFIX = "Action";
    public const string ACTION_INDEX = "Index";
    public const string ACTION_SHOW = "Show";
    public const string ACTION_SHOW_FORM = "ShowForm";
    public const string ACTION_SHOW_FORM_NEW = "New"; // not actual action, just a const
    public const string ACTION_SAVE = "Save";
    public const string ACTION_SAVE_MULTI = "SaveMulti";
    public const string ACTION_SHOW_DELETE = "ShowDelete";
    public const string ACTION_DELETE = "Delete";
    //additional actions used across controllers
    public const string ACTION_DELETE_RESTORE = "RestoreDeleted";
    public const string ACTION_NEXT = "Next"; // prev/next on view/edit forms
    public const string ACTION_AUTOCOMPLETE = "Autocomplete"; // autocomplete json
    public const string ACTION_USER_VIEWS = "UserViews"; // custom user views modal
    public const string ACTION_SAVE_USER_VIEWS = "SaveUserViews"; // custom user views sacve changes
    public const string ACTION_SAVE_SORT = "SaveSort"; // sort rows on list screen

    //helpers for route.action_more
    public const string ACTION_MORE_NEW = "new";
    public const string ACTION_MORE_EDIT = "edit";
    public const string ACTION_MORE_DELETE = "delete";

    public const string FW_NAMESPACE_PREFIX = "osafw.";
    public static Hashtable METHOD_ALLOWED = Utils.qh("GET POST PUT PATCH DELETE");

    private readonly Hashtable models = []; // model's singletons cache
    private readonly Hashtable controllers = []; // controller's singletons cache
    private const string ControllerActionsCacheKeyPrefix = "fw:controller-actions:";
    private ParsePage? pp_instance; // for parsePage()

    public Hashtable FORM = [];
    public Hashtable postedJson = []; // parsed JSON from request body
    public Hashtable G = []; // for storing global vars - used in template engine, also stores "_flash"
    public Hashtable FormErrors = []; // for storing form id's with error messages, put to ps['error']['details'] for parser

    public FwCache cache = new(); // cache instance
    public DB db;
    public FwLogger flogger = new();

    public HttpContext context;
    public HttpRequest request;
    public HttpResponse response;

    public string request_url; // current request url (relative to application url)
    public FwRoute route = new();
    public TimeSpan request_time; // after dispatch() - total request processing time

    public string cache_control = "no-cache"; // cache control header to add to pages, controllers can change per request
    public bool is_log_events = true; // can be set temporarly to false to prevent event logging (for batch process for ex)

    public string last_error_send_email = "";
    private static readonly char path_separator = Path.DirectorySeparatorChar;

    private DateTime start_time = DateTime.Now; //to track request time

    // shortcut for currently logged users.id
    // usage: fw.userId
    public int userId
    {
        get { return Session("user_id").toInt(); }
    }

    public int userAccessLevel
    {
        get { return Session("access_level").toInt(); }
    }

    public int userDateFormat
    {
        get { return G["date_format"].toInt(DateUtils.DATE_FORMAT_MDY); }
    }

    public int userTimeFormat
    {
        get { return G["time_format"].toInt(DateUtils.TIME_FORMAT_12); }
    }

    public string userTimezone
    {
        get { return G["timezone"].toStr(DateUtils.TZ_UTC); }
    }

    /// <summary>
    /// Convert an internal UTC datetime (DateTime or SQL string) into a user-visible string using the current user's timezone/format.
    /// </summary>
    public string formatUserDateTime(object? value, bool isISO = false)
    {
        if (value == null)
            return "";

        DateTime? dt = value switch
        {
            DateTime d => d,
            _ => DateUtils.SQL2Date(value.toStr()),
        };

        if (dt == null)
            return "";

        var utc = ((DateTime)dt).Kind == DateTimeKind.Utc
            ? (DateTime)dt
            : DateTime.SpecifyKind((DateTime)dt, DateTimeKind.Utc);

        var local = DateUtils.convertTimezone(utc, DateUtils.TZ_UTC, userTimezone);

        var format = "";
        if (isISO)
            //return in ISO format with timezone offset - for json
            format = "yyyy-MM-ddTHH:mm:sszzz";
        else
            format = DateUtils.mapDateFormat(userDateFormat) + " " + DateUtils.mapTimeFormat(userTimeFormat);

        return local.ToString(format);
    }

    // shortcut to obtain if we working under logged in user
    // usage: fw.isLogged
    public bool isLogged
    {
        get { return userId > 0; }
    }

    // helper to initialize DB instance based on configuration name
    public DB getDB(string config_name = "main")
    {
        Hashtable dbconfig = (Hashtable)config("db");
        Hashtable conf = dbconfig[config_name] as Hashtable ?? [];

        var db = new DB(conf, config_name);
        // Wrap the logger to match DB.LoggerDelegate (object?[])
        db.setLogger((level, args) => this.logger(level, args!));
        if (context != null)
            db.setContext(context);

        return db;
    }

    // begin processing one request
    public static void run(HttpContext context, IConfiguration configuration)
    {
        using FW fw = new(context, configuration);
        fw.initRequest();
        fw.dispatch();
        fw.endRequest();
    }

    public static FW initOffline(IConfiguration configuration)
    {
        FW fw = new(null, configuration);
        fw.logger(LogLevel.INFO, "OFFLINE START");
        return fw;
    }

    public FW(HttpContext? context, IConfiguration configuration)
    {
        if (context != null)
        {
            this.context = context;
            this.request = context.Request;
            this.response = context.Response;
        }

        // pass host explicitly so FwConfig can cache per-host settings
        FwConfig.init(context, configuration, context?.Request.Host.ToString());

        var env = config("config_override").toStr();
        flogger = new FwLogger((LogLevel)config("log_level"), config("log").toStr(), config("site_root").toStr(), config("log_max_size").toLong());
        flogger.setScope(env, Session("login"));

        db = getDB();
        DB.SQL_QUERY_CTR = 0; // reset query counter

        G = (Hashtable)config().Clone(); // by default G contains conf

        // per request settings
        G["request_url"] = request?.GetDisplayUrl() ?? "";
        G["current_time"] = DateTime.Now;

        // override default lang with user's lang
        if (!string.IsNullOrEmpty(Session("lang"))) G["lang"] = Session("lang");

        // override default ui_theme/ui_mode with user's settings
        if (!string.IsNullOrEmpty(Session("ui_theme"))) G["ui_theme"] = Session("ui_theme");
        if (!string.IsNullOrEmpty(Session("ui_mode"))) G["ui_mode"] = Session("ui_mode");
        // timezone/date/time format
        if (!string.IsNullOrEmpty(Session("date_format"))) G["date_format"] = Session("date_format");
        if (!string.IsNullOrEmpty(Session("time_format"))) G["time_format"] = Session("time_format");
        if (!string.IsNullOrEmpty(Session("timezone"))) G["timezone"] = Session("timezone");

        parseForm();

        // save flash to current var and update session as flash is used only for nearest request
        Hashtable? _flash = SessionHashtable("_flash");
        if (_flash != null) G["_flash"] = _flash;
        SessionHashtable("_flash", []);
    }

    public void initRequest()
    {
        try
        {
            FwHooks.initRequest(this);
        }
        catch (Exception ex)
        {
            logger(LogLevel.ERROR, "FwHooks.initRequest Exception: ", ex.Message);
            errMsg("FwHooks.initRequest Exception", ex);
            throw;
        }
    }

    public void endRequest()
    {
        try
        {
            FwHooks.finalizeRequest(this);
        }
        catch (Exception ex)
        {
            //for finalize - just log error, no need to show to user
            logger(LogLevel.ERROR, "FwHooks.finalizeRequest Exception: ", ex.ToString());
        }

        TimeSpan end_timespan = DateTime.Now - start_time;
        string msg;
        if (this.context != null)
            msg = "REQUEST END   [" + route.method + " " + request_url + "] in "; // web context
        else
            msg = "OFFLINE END   in "; // offline context
        logger(LogLevel.INFO, msg, end_timespan.TotalSeconds, "s, ", string.Format("{0:0.000}", 1 / end_timespan.TotalSeconds), "/s, ", DB.SQL_QUERY_CTR, " SQL");
    }

    // ***************** work with SESSION
    //by default Session is for strings
    public string Session(string name)
    {
        return context?.Session.GetString(name) ?? "";
    }
    public void Session(string name, string value)
    {
        context?.Session.SetString(name, value);
    }

    public int? SessionInt(string name)
    {
        return context?.Session.GetInt32(name);
    }
    public void SessionInt(string name, int value)
    {
        context?.Session.SetInt32(name, value);
    }

    public bool SessionBool(string name)
    {
        var data = context?.Session.Get(name);
        if (data == null)
        {
            return false;
        }
        return BitConverter.ToBoolean(data, 0);
    }
    public void SessionBool(string name, bool value)
    {
        context?.Session.Set(name, BitConverter.GetBytes(value));
    }

    public Hashtable? SessionHashtable(string name)
    {
        string? data = context?.Session.GetString(name);
        return data == null ? null : (Hashtable)Utils.deserialize(data);
    }
    public void SessionHashtable(string name, Hashtable value)
    {
        context?.Session.SetString(name, Utils.serialize(value));
    }


    // FLASH - used to pass something to the next request (and only on this request and only if this request does not expect json)
    // get flash value by name
    // set flash value by name - return fw in this case
    public object flash(string name, object? value = null)
    {
        if (value == null)
        {
            // read mode - return current flash
            return (this.G["_flash"] as Hashtable)?[name] ?? "";
        }
        else
        {
            if (!isJsonExpected())
            {
                // write for the next request
                Hashtable _flash = SessionHashtable("_flash") ?? [];
                _flash[name] = value;
                SessionHashtable("_flash", _flash);
            }
            return this; // for chaining
        }
    }

    // return all the settings
    public Hashtable config()
    {
        return FwConfig.settings;
    }
    // return just particular setting
    public object config(string name)
    {
        return FwConfig.settings[name];
    }

    //set G["err_msg"]
    public void setGlobalError(string str)
    {
        this.G["err_msg"] = str;
    }

    /// <summary>
    /// returns format expected by client browser
    /// </summary>
    /// <returns>"pjax", "json" or empty (usual html page)</returns>
    public string getResponseExpectedFormat()
    {
        string result = "";
        if (this.route.format == "json" || this.request.Headers.Accept.toStr().Contains("application/json"))
            result = "json";
        else if (this.route.format == "pjax" || !string.IsNullOrEmpty(this.request.Headers.XRequestedWith))
            result = "pjax";
        return result;
    }

    /// <summary>
    /// return true if browser requests json response
    /// </summary>
    /// <returns></returns>
    public bool isJsonExpected()
    {
        return getResponseExpectedFormat() == "json";
    }

    /// <summary>
    /// parse request URL and return prefix, controller, action, id, format, method
    /// if url is empty - use current request url and also set request_url property
    /// 
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    /// <exception cref="UserException"></exception>
    public FwRoute getRoute(string url = "")
    {
        var is_url_param = !string.IsNullOrEmpty(url);
        if (!is_url_param)
        {
            url = request.Path;
        }

        // cut the App path from the begin
        if (request.PathBase.Value.Length > 1) url = url.Replace(request.PathBase, "");
        url = url.TrimEnd('/'); // cut last / if any

        if (!is_url_param)
        {
            this.request_url = url;
            logger(LogLevel.TRACE, "REQUESTING ", url);
        }

        // init defaults
        var route = new FwRoute()
        {
            prefix = "",
            controller = "Home",
            action = ACTION_INDEX,
            action_raw = "",
            id = "",
            action_more = "",
            format = "html",
            method = request.Method,
            @params = []
        };

        if (!is_url_param)
        {
            // check if method override exits
            if (FORM.ContainsKey("_method"))
            {
                var form_method = FORM["_method"].toStr();
                if (METHOD_ALLOWED.ContainsKey(form_method))
                    route.method = form_method;
            }
            if (route.method == "HEAD") route.method = "GET"; // for website processing HEAD is same as GET, IIS will send just headers
        }

        string controller_prefix = ""; // prefix without "/", i.e. /Admin/Reports -> AdminReports

        // process config special routes (redirects, rewrites)
        Hashtable routes = (Hashtable)this.config("routes");
        bool is_routes_found = false;
        if (routes != null)
        {
            foreach (string route_key in routes.Keys)
            {
                if (url == route_key)
                {
                    string rdest = routes[route_key].toStr();
                    if (string.IsNullOrEmpty(rdest))
                    {
                        logger(LogLevel.WARN, "Wrong route destination: " + rdest);
                        continue;
                    }

                    string destination = rdest;
                    string overrideMethod = null;

                    int spaceIndex = destination.IndexOf(' ');
                    if (spaceIndex > 0)
                    {
                        string candidate = destination[..spaceIndex];
                        if (METHOD_ALLOWED.ContainsKey(candidate))
                        {
                            overrideMethod = candidate;
                            destination = destination[(spaceIndex + 1)..].TrimStart();
                        }
                    }

                    if (!string.IsNullOrEmpty(overrideMethod))
                        route.method = overrideMethod;

                    if (destination.StartsWith('/'))
                    {
                        // if started from / - this is redirect url
                        url = destination;
                        continue;
                    }

                    // it's a direct class-method to call, no further REST processing required
                    is_routes_found = true;
                    string[] sroute = destination.Split("::", 2);
                    route.controller = Utils.routeFixChars(sroute[0]);
                    if (sroute.Length > 1)
                        route.action_raw = sroute[1];
                    break;
                }
            }
        }

        if (!is_routes_found)
        {
            // TODO move prefix cut to separate func
            string prefix_rx = FwConfig.getRoutePrefixesRX();
            route.controller_path = "";
            Match m_prefix = Regex.Match(url, prefix_rx);
            if (m_prefix.Success)
            {
                // prefix detected - fix all prefix parts
                var prefix_parts = m_prefix.Groups[1].Value.Split('/');
                foreach (string prefix_part in prefix_parts)
                {
                    var part_fixed = Utils.routeFixChars(prefix_part);
                    controller_prefix += part_fixed;
                    route.controller_path += "/" + part_fixed;
                }
                url = m_prefix.Groups[2].Value;
            }

            // detect REST urls
            // GET   /controller[/.format]       Index
            // POST  /controller                 Save     (save new record - Create)
            // PUT   /controller                 SaveMulti (update multiple records)
            // GET   /controller/new             ShowForm (show new form - ShowNew)
            // GET   /controller/{id}[.format]   Show     (show in format - not for editing)
            // GET   /controller/{id}/edit       ShowForm (show edit form - ShowEdit)
            // GET   /controller/{id}/delete     ShowDelete
            // POST/PUT  /controller/{id}        Save     (save changes to exisitng record - Update    Note:Request.Form should contain data. Assumes whole form submit. I.e. unchecked checkboxe treated as empty value)
            // PATCH /controller                 SaveMulti (partial update multiple records)
            // PATCH /controller/{id}            Save     (save partial changes to exisitng record - Update. Can be used to update single/specific fields without affecting any other fields.)
            // DELETE /controller/{id}           Delete
            //
            // /controller/(Action)              Action    call for arbitrary action from the controller
            Match m = Regex.Match(url, @"^/([^/]+)(?:/(new|\.\w+)|/([\d\w_-]+)(?:\.(\w+))?(?:/(edit|delete))?)?/?$");
            if (m.Success)
            {
                route.controller = Utils.routeFixChars(m.Groups[1].Value);
                if (string.IsNullOrEmpty(route.controller))
                    throw new Exception("Wrong request");

                // capitalize first letter - TODO - URL-case-insensitivity should be an option!
                route.controller = string.Concat(route.controller[..1].ToUpper(), route.controller.AsSpan(1));
                route.id = m.Groups[3].Value;
                route.format = m.Groups[4].Value;
                route.action_more = m.Groups[5].Value;
                if (!string.IsNullOrEmpty(m.Groups[2].Value))
                {
                    if (m.Groups[2].Value == ACTION_MORE_NEW)
                        route.action_more = ACTION_MORE_NEW;
                    else
                        route.format = m.Groups[2].Value[1..];
                }

                // match to method (GET/POST)
                if (route.method == "GET")
                {
                    if (route.action_more == ACTION_MORE_NEW)
                        route.action_raw = ACTION_SHOW_FORM;
                    else if (!string.IsNullOrEmpty(route.id) & route.action_more == ACTION_MORE_EDIT)
                        route.action_raw = ACTION_SHOW_FORM;
                    else if (!string.IsNullOrEmpty(route.id) & route.action_more == ACTION_MORE_DELETE)
                        route.action_raw = ACTION_SHOW_DELETE;
                    else if (!string.IsNullOrEmpty(route.id))
                        route.action_raw = ACTION_SHOW;
                    else
                        route.action_raw = ACTION_INDEX;
                }
                else if (route.method == "POST")
                {
                    route.action_raw = ACTION_SAVE;
                }
                else if (route.method == "PUT")
                {
                    if (!string.IsNullOrEmpty(route.id))
                        route.action_raw = ACTION_SAVE;
                    else
                        route.action_raw = ACTION_SAVE_MULTI;
                }
                else if (route.method == "PATCH")
                {
                    if (!string.IsNullOrEmpty(route.id))
                        route.action_raw = ACTION_SAVE;
                    else
                        route.action_raw = ACTION_SAVE_MULTI;
                }
                else if (route.method == "DELETE" & !string.IsNullOrEmpty(route.id))
                    route.action_raw = ACTION_DELETE;
                else
                {
                    logger(LogLevel.WARN, route.method);
                    logger(LogLevel.WARN, url);
                    throw new UserException("Wrong Route Params");
                }

                logger(LogLevel.TRACE, "REST controller.action=", route.controller, ".", route.action_raw);
            }
            else
            {
                // otherwise detect controller/action/id.format/more_action
                string[] parts = url.Split("/");
                // logger(parts)
                int ub = parts.Length - 1;
                if (ub >= 1)
                    route.controller = Utils.routeFixChars(parts[1]);
                if (ub >= 2)
                    route.action_raw = parts[2];
                if (ub >= 3)
                    route.id = parts[3];
                if (ub >= 4)
                    route.action_more = parts[4];
            }
        }

        route.controller_path = route.controller_path + "/" + route.controller;
        // add controller prefix if any
        route.prefix = controller_prefix;
        route.controller = controller_prefix + route.controller;
        route.action = Utils.routeFixChars(route.action_raw);
        if (string.IsNullOrEmpty(route.action))
            route.action = ACTION_INDEX;

        return route;
    }

    public void dispatch()
    {
        try
        {
            route = getRoute();
            logger(LogLevel.INFO, "REQUEST START [", route.method, " ", request_url, "] => ", route.controller, ".", route.action);

            callRoute();
        }
        catch (RedirectException)
        {
            // not an error, just exit via Redirect
            logger(LogLevel.INFO, "Redirected...");
        }
        catch (AuthException Ex)
        {
            logger(LogLevel.DEBUG, Ex.Message);
            // if not logged - just redirect to login
            if (!isLogged)
                redirect(config("UNLOGGED_DEFAULT_URL").toStr(), false);
            else
                errMsg(Ex.Message);
        }
        catch (ApplicationException Ex)
        {
            // get very first exception
            string msg = Ex.Message;
            Exception iex = Ex;
            while (iex.InnerException != null)
            {
                iex = iex.InnerException;
                msg = iex.Message;
            }

            if ((iex) is RedirectException)
                // not an error, just exit via Redirect - TODO - remove here as already handled above?
                logger(LogLevel.DEBUG, "Redirected...");
            else if ((iex) is UserException)
            {
                // no need to log/report detailed user exception
                logger(LogLevel.INFO, "UserException: " + msg);
                errMsg(msg, iex);
            }
            else
            {
                // it's ApplicationException, so just warning
                logger(LogLevel.NOTICE, "REQUEST FORM:", FORM);
                logger(LogLevel.NOTICE, "SESSION:", context.Session);
                logger(LogLevel.WARN, Ex.Message, Ex.ToString());

                // send_email_admin("App Exception: " & Ex.ToString() & vbCrLf & vbCrLf & _
                // "Request: " & req.Path & vbCrLf & vbCrLf & _
                // "Form: " & dumper(FORM) & vbCrLf & vbCrLf & _
                // "Session:" & dumper(SESSION))

                errMsg(msg, Ex);
            }
        }
        catch (Exception Ex)
        {
            // it's general Exception, so something more severe occur, log as error and notify admin
            logger(LogLevel.NOTICE, "REQUEST FORM:", FORM);
            logger(LogLevel.NOTICE, "SESSION:", context.Session);
            logger(LogLevel.ERROR, Ex.Message, Ex.ToString());

            //send_email_admin("Exception: " + Ex.ToString() + System.Environment.NewLine + System.Environment.NewLine
            //    + "Request: " + req.Path + System.Environment.NewLine + System.Environment.NewLine
            //    + "Form: " + dumper(FORM) + System.Environment.NewLine + System.Environment.NewLine
            //    + "Session:" + dumper(context.Session));

            if (this.config("log_level").toInt() >= (int)LogLevel.DEBUG)
                throw;
            else
                errMsg("Server Error. Please, contact site administrator!", Ex);
        }
    }

    // simple auth check based on /controller/action - and rules filled in in Config class
    // called from Dispatcher
    // throws exception OR if is_die=false
    // return 2 - if user allowed to see page - explicitly based on fw.config
    // return 1 - if no fw.config rule, so need to further check Controller.access_level (not checking here for performance reasons)
    // return 0 - if not allowed
    public int _auth(FwRoute route, bool is_die = true)
    {
        int result = 0;

        // integrated XSS check - only for POST/PUT/PATCH/DELETE requests
        // OR for standard actions: Save, Delete, SaveMulti
        // OR if it contains XSS param
        if ((FORM.ContainsKey("XSS")
            || route.method == "POST"
            || route.method == "PUT"
            || route.method == "PATCH"
            || route.method == "DELETE"
            || route.action == ACTION_SAVE
            || route.action == ACTION_SAVE_MULTI
            || route.action == ACTION_DELETE
            || route.action == ACTION_DELETE_RESTORE)
            && !string.IsNullOrEmpty(Session("XSS")) && Session("XSS") != FORM["XSS"].toStr())
        {
            // XSS validation failed
            // first, check if we are under xss-excluded prefix
            Hashtable no_xss_prefixes = (Hashtable)this.config("no_xss_prefixes_prefixes");
            if (no_xss_prefixes == null || !no_xss_prefixes.ContainsKey(route.prefix))
            {
                // second, check if we are under xss-excluded controller
                Hashtable no_xss = (Hashtable)this.config("no_xss");
                if (no_xss == null || !no_xss.ContainsKey(route.controller))
                {
                    if (is_die)
                        throw new AuthException("XSS Error. Reload the page or try to re-login");
                    return result;
                }
            }
        }

        string path = "/" + route.controller + "/" + route.action;
        string path2 = "/" + route.controller;

        // pre-check controller's access level by url
        int current_level = userAccessLevel;

        Hashtable rules = (Hashtable)config("access_levels");
        if (rules != null && rules.ContainsKey(path))
        {
            if (current_level >= rules[path].toInt())
                result = 2;
        }
        else if (rules != null && rules.ContainsKey(path2))
        {
            if (current_level >= rules[path2].toInt())
                result = 2;
        }
        else
        {
            result = 1; // need to check Controller.access_level after _auth
        }

        if (result == 0 && is_die)
            throw new AuthException("Bad access - Not authorized");
        return result;
    }

    // parse query string, form and json in request body into fw.FORM
    private void parseForm()
    {
        if (request == null)
        {
            // offline mode FORM = [];
            return;
        }

        Hashtable input = [];

        foreach (string s in request.Query.Keys)
        {
            if (s != null)
                input[s] = request.Query[s].ToString();
        }

        if (request.HasFormContentType)
        {
            foreach (string s in request.Form.Keys)
            {
                if (s != null)
                    input[s] = request.Form[s].ToString();
            }
        }

        // after perpare_FORM - grouping for names like XXX[YYYY] -> FORM{XXX}=@{YYYY1, YYYY2, ...}
        Hashtable SQ = [];
        Hashtable f = [];
        foreach (DictionaryEntry entry in input)
        {
            if (entry.Key is not string name)
                continue;

            var value = entry.Value;
            var bracketPos = name.IndexOf('[');
            if (bracketPos > 0 && name.EndsWith("]", StringComparison.Ordinal))
            {
                var mainKey = name[..bracketPos];
                var subKey = name.Substring(bracketPos + 1, name.Length - bracketPos - 2);
                if (subKey.Length == 0)
                {
                    f[name] = value;
                    continue;
                }

                if (!SQ.ContainsKey(mainKey))
                    SQ[mainKey] = new Hashtable();

                ((Hashtable)SQ[mainKey]!)[subKey] = value;
            }
            else
            {
                f[name] = value;
            }
        }

        foreach (DictionaryEntry entry in SQ)
            f[entry.Key.toStr()] = entry.Value;

        // also parse json in request body if any
        if (request.ContentType?[.."application/json".Length] == "application/json")
        {
            postedJson = Utils.getPostedJson(this);
            // merge json into FORM, but all values should be stingified in FORM
            Utils.mergeHash(f, (Hashtable)Utils.jsonStringifyValues(postedJson));
        }

        // logger(f)
        FORM = f;
    }

    public void logger(params object[] args)
    {
        if (args.Length == 0)
            return;
        flogger.log(LogLevel.DEBUG, ref args);
    }
    public void logger(LogLevel level, params object[] args)
    {
        if (args == null || args.Length == 0)
            return;
        flogger.log(level, ref args);
    }

    public void responseWrite(string str)
    {
        // TODO debug - async/await disabled as with large response it can fail with "cannot write to the response body, response has completed"
        //await HttpResponseWritingExtensions.WriteAsync(this.response, str);
        HttpResponseWritingExtensions.WriteAsync(this.response, str).Wait();
    }

    // show page from template  /route.controller/route.action = parser('/route.controller/route.action/', $ps)
    public void parser(Hashtable ps)
    {
        this.parser((route.controller_path + "/" + route.action).ToLower(), ps);
    }

    // same as parser(ps), but with base dir param
    // output format based on requested format: json, pjax or (default) full page html
    // for automatic json response support - set hf("_json") = True OR set hf("_json")=ArrayList/Hashtable - if json requested, only _json content will be returned
    // to override:
    //   - base directory - set ps("_basedir")="/another_controller/another_action" (relative to SITE_TEMPLATES dir)
    //   - only controller base directory - set ps("_basedir_controller")="/another_controller" (relative to SITE_TEMPLATES dir)
    //   - layout template - set ps("_layout")="/another_page_layout.html" (relative to SITE_TEMPLATES dir)
    //   - (not for json) to perform route_redirect - set hf("_route_redirect")("method"), hf("_route_redirect")("controller"), hf("_route_redirect")("args")
    //   - (not for json) to perform redirect - set hf("_redirect")="url"
    public void parser(string basedir, Hashtable ps)
    {
        if (!this.response.HasStarted) this.response.Headers.CacheControl = cache_control;

        if (this.FormErrors.Count > 0)
        {
            if (!ps.ContainsKey("error"))
                ps["error"] = new Hashtable();

            if (ps["error"] is Hashtable errorTable && !errorTable.ContainsKey("details"))
                errorTable["details"] = this.FormErrors; // add form errors if any
            logger(LogLevel.DEBUG, "Form errors:", this.FormErrors);
        }

        string format = this.getResponseExpectedFormat();
        if (format == "json")
        {
            if (ps.ContainsKey("_json"))
            {
                if (ps["_json"] is bool b && b == true)
                {
                    ps.Remove("_json"); // remove internal flag
                    this.parserJson(ps);
                }
                else
                    this.parserJson(ps["_json"]);// if _json exists - return only this element content
            }
            else
            {
                var msg = @"JSON response is not enabled for this Controller.Action (set ps[""_json""])=True or ps[""_json""])=data... to enable).";
                logger(LogLevel.DEBUG, msg);

                ps = new Hashtable()
                {
                    {"success", false},
                    {"message", msg}
                };
                this.parserJson(ps);
            }
            return; // no further processing for json
        }

        if (ps.ContainsKey("_route_redirect"))
        {
            var rr = ps["_route_redirect"] as Hashtable ?? [];
            this.routeRedirect(rr["method"].toStr(), rr["controller"].toStr(), rr["args"] as object[] ?? []);
            return; // no further processing
        }

        if (ps.ContainsKey("_redirect"))
        {
            this.redirect(ps["_redirect"].toStr());
            return; // no further processing
        }

        string layout;
        if (format == "pjax")
            layout = G["PAGE_LAYOUT_PJAX"].toStr();
        else
            layout = G["PAGE_LAYOUT"].toStr();

        //override layout from parse strings
        if (ps.ContainsKey("_layout"))
            layout = ps["_layout"].toStr();

        //override full basedir
        if (ps.ContainsKey("_basedir"))
            basedir = ps["_basedir"].toStr();

        if (basedir == "")
        {
            // if basedir not passed - use current controller/action as default then check overrides
            var controller = this.route.controller;
            if (!string.IsNullOrEmpty(this.route.prefix))
            {
                basedir += "/" + this.route.prefix;
                //remove prefix from controller name (only from the start)
                controller = Regex.Replace(controller, "^" + this.route.prefix, "", RegexOptions.IgnoreCase);
            }
            basedir += "/" + controller;

            // override controller basedir only
            if (ps.ContainsKey("_basedir_controller"))
                basedir = ps["_basedir_controller"].toStr();

            basedir += "/" + this.route.action; // add action dir to controller's directory
        }
        else
        {
            // if override controller basedir - also add route action
            if (ps.ContainsKey("_basedir_controller"))
                basedir = ps["_basedir_controller"].toStr() + "/" + this.route.action;
        }


        basedir = basedir.ToLower(); // make sure it's lower case

        string page = parsePage(basedir, layout, ps);
        // no need to set content type here, as it's set in Startup.cs
        //if (!this.response.HasStarted) response.ContentType = "text/html; charset=utf-8";
        responseWrite(page);
    }

    // - show page from template  /controller/action = parser('/controller/action/', $layout, $ps)
    public void parser(string basedir, string layout, Hashtable ps)
    {
        ps["_layout"] = layout;
        parser(basedir, ps);
    }

    public void parserJson(object ps)
    {
        string page = parsePageInstance().parse_json(ps);
        //if (!this.response.HasStarted) response.Headers.Add("Content-type", "application/json; charset=utf-8");
        response.ContentType = "application/json; charset=utf-8";
        responseWrite(page);
    }

    public ParsePage parsePageInstance()
    {
        // if pp_instance not yet set - instantiate
        if (pp_instance == null)
        {
            // prepare date/time formats for ParsePage
            // see template/common/sel for available formats
            var DateFormat = DateUtils.mapDateFormat(userDateFormat);
            var DateFormatShort = DateFormat + " " + DateUtils.mapTimeFormat(userTimeFormat);
            var DateFormatLong = DateFormat + " " + DateUtils.mapTimeWithSecondsFormat(userTimeFormat);

            pp_instance = new ParsePage(new ParsePageOptions
            {
                TemplatesRoot = config("template").toStr(),
                IsCheckFileModifications = (LogLevel)config("log_level") >= LogLevel.DEBUG,
                Lang = G["lang"].toStr(),
                IsLangUpdate = config("is_lang_update").toBool(),
                GlobalsGetter = () => G,
                Session = context?.Session,
                Logger = (level, args) => logger(level, args),

                DateFormat = DateFormat,
                DateFormatShort = DateFormatShort,
                DateFormatLong = DateFormatLong,
                // data arrives in UTC; ParsePage uses its default InputTimezone (UTC) and converts output for the user
                OutputTimezone = userTimezone,
            });
        }
        return pp_instance;
    }

    public string parsePage(string basedir, string layout, Hashtable ps)
    {
        logger(LogLevel.DEBUG, "parsing page bdir=", basedir, ", tpl=", layout);
        ParsePage parser_obj = parsePageInstance();
        return parser_obj.parse_page(basedir, layout, ps);
    }

    // perform redirect
    // if is_exception=True (default) - throws RedirectException, so current request processing can end early
    public void redirect(string url, bool is_exception = true)
    {
        if (Regex.IsMatch(url, "^/"))
            url = this.config("ROOT_URL") + url;
        logger(LogLevel.DEBUG, $"Redirect to [{url}]");
        response.Redirect(url, false);
        if (is_exception)
            throw new RedirectException();
    }

    public void routeRedirect(string action, string controller, object[]? args = null)
    {
        logger(LogLevel.TRACE, $"Route Redirect to [{controller}.{action}]", args);
        setController((!string.IsNullOrEmpty(controller) ? controller : route.controller), action);

        if (args != null)
        {
            this.route.id = args[0].toStr(); //first argument goes to id
            this.route.@params = new ArrayList(args); // all arguments go to params
        }

        callRoute();
    }

    // same as above just with default controller
    public void routeRedirect(string action, object[]? args = null)
    {
        routeRedirect(action, route.controller, args);
    }

    /// <summary>
    /// set route.controller and optionally route.action, updates G too
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="action"></param>
    public void setController(string controller, string action = "")
    {
        route.controller = controller;
        // route.controller_path = controller; // TODO this won't work if redirect to controller with different prefix
        route.action = action;

        G["controller"] = route.controller;
        G["action"] = route.action;
        G["controller.action"] = route.controller + "." + route.action;
    }

    public void setRoute(FwRoute r)
    {
        this.route = r;
        setController(r.controller, r.action); //to update G
    }

    public void callRoute()
    {
        string[] args = [route.id]; // TODO - add rest of possible params from parts

        var auth_check_controller = _auth(route);

        var co = controller(route.controller, auth_check_controller == 1);
        if (co == null)
        {
            logger(LogLevel.DEBUG, "No controller found for controller=[", route.controller, "], using default Home");
            // no controller found - call default controller with default action
            route.controller_path = "/Home";
            route.controller = "Home";
            route.action = "NotFound";
            co = controller(route.controller);
            //we should always have HomeController
        }
        Type controllerClass = co.GetType();

        logger(LogLevel.TRACE, "TRY controller.action=", route.controller, ".", route.action);

        // ---------------------------------
        // choose proper overload for Action
        bool isIdNumeric = int.TryParse(route.id, out _);
        var actionMethod = resolveActionMethod(controllerClass, route.action, isIdNumeric);
        if (actionMethod == null)
        {
            logger(LogLevel.DEBUG, "No method found for controller.action=[", route.controller, ".", route.action, "], checking route_default_action");
            // no method found - try to get default action
            FieldInfo pInfo = controllerClass.GetField("route_default_action");
            if (pInfo != null)
            {
                string pvalue = pInfo.GetValue(null).toStr();
                if (pvalue == ACTION_INDEX)
                {
                    // = index - use IndexAction for unknown actions
                    route.action = ACTION_INDEX;
                    actionMethod = controllerClass.GetMethod(route.action + ACTION_SUFFIX);
                }
                else if (pvalue == ACTION_SHOW)
                {
                    // = show - assume action is id and use ShowAction
                    if (!string.IsNullOrEmpty(route.id))
                        route.@params.Add(route.id); // route.id is a first param in this case. TODO - add all rest of params from split("/") here
                    if (!string.IsNullOrEmpty(route.action_more))
                        route.@params.Add(route.action_more); // route.action_more is a second param in this case

                    route.id = route.action_raw;
                    args[0] = route.id;

                    route.action = ACTION_SHOW;
                    actionMethod = controllerClass.GetMethod(route.action + ACTION_SUFFIX);
                }
            }
        }

        // save to globals so it can be used in templates
        setController(route.controller, route.action);

        logger(LogLevel.TRACE, "FINAL controller.action=", route.controller, ".", route.action);
        // logger(LogLevel.TRACE, "route.method=" , route.method)
        // logger(LogLevel.TRACE, "route.controller=" , route.controller)
        // logger(LogLevel.TRACE, "route.action=" , route.action)
        // logger(LogLevel.TRACE, "route.format=" , route.format)
        // logger(LogLevel.TRACE, "route.id=" , route.id)
        // logger(LogLevel.TRACE, "route.action_more=" , route.action_more)

        logger(LogLevel.DEBUG, "ROUTE [", route.method, " ", request_url, "] => ", route.controller, ".", route.action);

        if (actionMethod == null)
        {
            logger(LogLevel.INFO, "No method found for controller.action=[", route.controller, ".", route.action, "], displaying static page from related templates");
            // if no method - just call FW.parser(hf) - show template from /route.controller/route.action dir
            parser([]);
        }
        else
            callController(co, actionMethod, args);
    }

    // Call controller
    public void callController(FwController controller, MethodInfo actionMethod, object[]? args = null)
    {
        //convert args to parameters with proper types
        System.Reflection.ParameterInfo[] @params = actionMethod.GetParameters();
        object[] parameters = new object[@params.Length];
        for (int i = 0; i < @params.Length; i++)
        {
            var pi = @params[i];
            if (i < args.Length)
            {
                //logger("ARG IN:", args[i].GetType().Name, args[i]);
                try
                {
                    parameters[i] = Convert.ChangeType(args[i], pi.ParameterType);
                }
                catch (Exception)
                {
                    //cannot convert, use default value for param type if param doesn't have default
                    if (!pi.HasDefaultValue)
                    {
                        parameters[i] = Activator.CreateInstance(pi.ParameterType);
                    }
                }
                //logger("ARG OUT:", parameters[i].GetType().Name, parameters[i]);
            }
        }

        Hashtable? ps = null;
        try
        {
            controller.checkAccess();
            ps = actionMethod.Invoke(controller, parameters) as Hashtable; // Call Controller Action, if returns null - no ParsePage called

            // check/override _basedir from controller for non-json requests
            if (ps != null && !isJsonExpected() && !ps.ContainsKey("_basedir_controller") && !string.IsNullOrEmpty(controller.template_basedir))
            {
                logger("TRACE", $"Controller [{controller.GetType()}] template_basedir override to [{controller.template_basedir}]");
                ps["_basedir_controller"] = controller.template_basedir;
            }

            //special case for export - IndexAction+export_format is set - call exportList without parser
            if (actionMethod.Name == (ACTION_INDEX + ACTION_SUFFIX) && controller.export_format.Length > 0)
            {
                controller.exportList();
                ps = null; //disable parser
            }
        }
        catch (TargetInvocationException ex)
        {
            Exception? iex = null;
            if (ex.InnerException != null)
            {
                iex = ex.InnerException;
                if (iex.InnerException != null)
                    iex = iex.InnerException;
            }

            if (iex != null && iex is not ApplicationException)
            {
                throw; //throw if not an ApplicationException happened - this keeps stack, also see http://weblogs.asp.net/fmarguerie/rethrowing-exceptions-and-preserving-the-full-call-stack-trace
            }

            // ignore redirect exception
            if (iex == null || iex is not RedirectException)
            {
                //if got ApplicationException - call error action handler
                ps = controller.actionError(iex, args);
            }
        }
        if (ps != null)
            parser(ps);
    }

    #region controller action method resolution with caching
    private sealed class ControllerActionCache
    {
        private readonly Dictionary<string, MethodInfo> stringHandlers;
        private readonly Dictionary<string, MethodInfo> numericHandlers;
        private readonly Dictionary<string, MethodInfo> declaredFallback;
        private readonly Dictionary<string, MethodInfo> anyFallback;

        public ControllerActionCache(
            Dictionary<string, MethodInfo> stringHandlers,
            Dictionary<string, MethodInfo> numericHandlers,
            Dictionary<string, MethodInfo> declaredFallback,
            Dictionary<string, MethodInfo> anyFallback)
        {
            this.stringHandlers = stringHandlers;
            this.numericHandlers = numericHandlers;
            this.declaredFallback = declaredFallback;
            this.anyFallback = anyFallback;
        }

        public bool TryGetString(string actionName, out MethodInfo method) => stringHandlers.TryGetValue(actionName, out method);

        public bool TryGetNumeric(string actionName, out MethodInfo method) => numericHandlers.TryGetValue(actionName, out method);

        public bool TryGetDeclaredFallback(string actionName, out MethodInfo method) => declaredFallback.TryGetValue(actionName, out method);

        public bool TryGetAnyFallback(string actionName, out MethodInfo method) => anyFallback.TryGetValue(actionName, out method);
    }

    private static MethodInfo? resolveActionMethod(Type controllerClass, string actionName, bool isIdNumeric)
    {
        if (string.IsNullOrEmpty(actionName))
            return null;

        var cacheKey = ControllerActionsCacheKeyPrefix + controllerClass.AssemblyQualifiedName;
        if (FwCache.getValue(cacheKey) is not ControllerActionCache cache)
        {
            cache = buildControllerActionCache(controllerClass);
            FwCache.setValue(cacheKey, cache, 86400); // cache for a day to avoid repeated reflection
        }

        if (!isIdNumeric && cache.TryGetString(actionName, out var stringHandler))
            return stringHandler;

        if (isIdNumeric && cache.TryGetNumeric(actionName, out var numericHandler))
            return numericHandler;

        if (cache.TryGetDeclaredFallback(actionName, out var declared))
            return declared;

        cache.TryGetAnyFallback(actionName, out var any);
        return any;
    }

    private static ControllerActionCache buildControllerActionCache(Type controllerClass)
    {
        var stringHandlers = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
        var numericHandlers = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
        var declaredFallback = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
        var anyFallback = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var method in controllerClass.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!method.Name.EndsWith(ACTION_SUFFIX, StringComparison.Ordinal))
                continue;

            var actionName = method.Name[..^ACTION_SUFFIX.Length];

            if (!anyFallback.ContainsKey(actionName))
                anyFallback[actionName] = method;

            if (method.DeclaringType == controllerClass && !declaredFallback.ContainsKey(actionName))
                declaredFallback[actionName] = method;

            var parameters = method.GetParameters();
            if (parameters.Length != 1)
                continue;

            var parameterType = parameters[0].ParameterType;
            if (parameterType == typeof(string))
                stringHandlers[actionName] = method;
            else if (parameterType == typeof(int) || parameterType == typeof(long))
                numericHandlers[actionName] = method;
        }

        return new ControllerActionCache(stringHandlers, numericHandlers, declaredFallback, anyFallback);
    }

    #endregion

    /// <summary>
    /// output file to response with given content type and disposition
    /// </summary>
    /// <param name="filepath"></param>
    /// <param name="attname">attachment name, all speсial chars replaced with underscore</param>
    /// <param name="ContentType">detected based on file extension or application/octet-stream</param>
    /// <param name="ContentDisposition"></param>
    public void fileResponse(string filepath, string attname, string ContentType = "", string ContentDisposition = "attachment")
    {
        if (string.IsNullOrEmpty(ContentType))
            ContentType = Utils.ext2mime(Path.GetExtension(filepath));

        logger(LogLevel.DEBUG, "sending file response  = ", filepath, " as ", attname, " content-type:", ContentType);
        attname = Regex.Replace(attname, @"[^\w. \-]+", "_");

        response.Headers.ContentType = ContentType;
        response.Headers.ContentLength = Utils.fileSize(filepath);
        response.Headers.ContentDisposition = $"{ContentDisposition}; filename=\"{attname}\"";
        response.SendFileAsync(filepath).Wait();
    }

    /// <summary>
    /// Send Email
    /// </summary>
    /// <param name="mail_from">if empty - config mail_from used</param>
    /// <param name="mail_to">may contain several emails delimited by ,; or space</param>
    /// <param name="mail_subject">subject</param>
    /// <param name="mail_body">body, if starts with !DOCTYPE or html tag - html email will be sent</param>
    /// <param name="filenames">optional hashtable human filename => filepath</param>
    /// <param name="aCC">optional arraylist of CC addresses (strings)</param>
    /// <param name="reply_to">optional reply to email</param>
    /// <param name="options">hashtable with options:
    ///   "read-receipt"
    ///   "smtp" - hashtable with smtp settings (host, port, is_ssl, username, password)
    ///   "bcc" - bcc email addresses - ArrayList
    /// </param>
    /// <returns>true if sent successfully, false if problem - see fw.last_error_send_email</returns>
    public bool sendEmail(string mail_from, string mail_to, string mail_subject, string mail_body, IDictionary? filenames = null, IList? aCC = null, string reply_to = "", Hashtable? options = null)
    {
        bool result = true;
        MailMessage message = null;
        options ??= [];

        try
        {
            if (mail_from.Length == 0)
                mail_from = this.config("mail_from").toStr(); // default mail from
            mail_subject = Regex.Replace(mail_subject, @"[\r\n]+", " ");

            bool is_test = this.config("is_test").toBool();
            if (is_test)
            {
                string test_email = this.Session("login") ?? ""; //in test mode - try logged user email (if logged)
                if (test_email.Length == 0)
                    test_email = this.config("test_email").toStr(); //try test_email from config

                mail_body = mail_body + System.Environment.NewLine + "TEST SEND. PASSED MAIL_TO=[" + mail_to + "]"; //add to the end of the body to preserve html
                mail_to = test_email;
                logger(LogLevel.INFO, "EMAIL SENT TO TEST EMAIL [", mail_to, "] - TEST ENABLED IN web.config");
            }

            logger(LogLevel.INFO, "Sending email. From=[", mail_from, "], ReplyTo=[", reply_to, "], To=[", mail_to, "], Subj=[", mail_subject, "]");
            logger(LogLevel.DEBUG, mail_body);

            if (!string.IsNullOrEmpty(mail_to))
            {
                message = new MailMessage();
                if (options.ContainsKey("read-receipt"))
                    message.Headers.Add("Disposition-Notification-To", mail_from);

                // detect HTML body - if it's started with <!DOCTYPE or <html tags
                if (Regex.IsMatch(mail_body, @"^\s*<(!DOCTYPE|html)[^>]*>", RegexOptions.IgnoreCase))
                    message.IsBodyHtml = true;

                message.From = new MailAddress(mail_from);
                message.Subject = mail_subject;
                message.Body = mail_body;
                // If reply_to > "" Then message.ReplyTo = New MailAddress(reply_to) '.net<4
                if (!string.IsNullOrEmpty(reply_to))
                    message.ReplyToList.Add(reply_to); // .net>=4

                // mail_to may contain several emails delimited by ;
                ArrayList amail_to = Utils.splitEmails(mail_to);
                foreach (string email1 in amail_to)
                {
                    string email = email1.Trim();
                    if (string.IsNullOrEmpty(email))
                        continue;
                    message.To.Add(new MailAddress(email));
                }

                // add CC if any
                if (aCC != null)
                {
                    if (is_test)
                    {
                        foreach (string cc in aCC)
                        {
                            logger(LogLevel.INFO, "TEST SEND. PASSED CC=[", cc, "]");
                            foreach (string email1 in amail_to)
                            {
                                string email = email1.Trim();
                                if (string.IsNullOrEmpty(email))
                                    continue;
                                message.CC.Add(new MailAddress(email));
                            }
                        }
                    }
                    else
                        foreach (string cc1 in aCC)
                        {
                            string cc = cc1.Trim();
                            if (string.IsNullOrEmpty(cc))
                                continue;
                            message.CC.Add(new MailAddress(cc));
                        }
                }

                // add BCC if any
                if (options["bcc"] is ArrayList options_bcc && !is_test)
                {
                    foreach (string bcc1 in options_bcc)
                    {
                        string bcc = bcc1.Trim();
                        if (string.IsNullOrEmpty(bcc))
                            continue;
                        message.Bcc.Add(new MailAddress(bcc));
                    }
                }

                // attach attachments if any
                if (filenames != null)
                {
                    // sort by human name
                    ArrayList fkeys = new(filenames.Keys);
                    fkeys.Sort();
                    foreach (string human_filename in fkeys)
                    {
                        string filename = filenames[human_filename].toStr();
                        System.Net.Mail.Attachment att = new(filename, Utils.ext2mime(Path.GetExtension(filename)))
                        {
                            Name = human_filename,
                            NameEncoding = System.Text.Encoding.UTF8
                        };
                        // att.ContentDisposition.FileName = human_filename
                        logger(LogLevel.DEBUG, "attachment ", human_filename, " => ", filename);
                        message.Attachments.Add(att);
                    }
                }

                using (SmtpClient client = new())
                {
                    Hashtable mailSettings = (Hashtable)this.config("mail");
                    if (options.ContainsKey("smtp"))
                    {
                        //override mailSettings from smtp options
                        Utils.mergeHash(mailSettings, options["smtp"] as Hashtable);
                    }
                    if (mailSettings.Count > 0)
                    {
                        client.Host = mailSettings["host"].toStr();
                        client.Port = mailSettings["port"].toInt();
                        client.EnableSsl = mailSettings["is_ssl"].toBool();
                        client.Credentials = new System.Net.NetworkCredential(mailSettings["username"].toStr(), mailSettings["password"].toStr());
                        client.Send(message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result = false;
            last_error_send_email = ex.Message;
            if (ex.InnerException != null)
                last_error_send_email += " " + ex.InnerException.Message;
            logger(LogLevel.ERROR, "send_email error:", last_error_send_email);
        }
        finally
        {
            message?.Dispose();
        }// important, as this will close any opened attachment files
        return result;
    }

    // shortcut for send_email from template from the /emails template dir
    public bool sendEmailTpl(string mail_to, string tpl, Hashtable hf, Hashtable? filenames = null, ArrayList? aCC = null, string reply_to = "", Hashtable? options = null)
    {
        Regex r = new(@"[\n\r]+");
        string subj_body = parsePage("/emails", tpl, hf);
        if (subj_body.Length == 0)
            throw new ApplicationException("No email template defined [" + tpl + "]");
        string[] arr = r.Split(subj_body, 2);
        return sendEmail("", mail_to, arr[0], arr[1], filenames, aCC, reply_to, options);
    }

    // send email message to site admin (usually used in case of errors)
    public void sendEmailAdmin(string msg)
    {
        this.sendEmail("", this.config("admin_email").toStr(), msg[..512], msg);
    }

    public void errMsg(string msg, Exception? Ex = null)
    {
        Hashtable ps = [];
        var tpl_dir = "/error";

        var code = 0;
        if (Ex is NotFoundException)
        {
            //Not Found
            code = 404;
            tpl_dir += "/4xx";
        }
        else if (Ex is UserException)
        {
            //Bad request from user
            code = 400;
            tpl_dir += "/4xx";
        }
        else if (Ex is AuthException)
        {
            //Forbidden
            code = 403;
            tpl_dir += "/4xx";
        }
        else
            //Server Error - ApplicationException or any other
            code = 500;

        if (code > 0 && !this.response.HasStarted)
            this.response.StatusCode = code;

        ps["_json"] = true;
        ps["title"] = msg;
        ps["error"] = new Hashtable
        {
            ["code"] = code,
            ["message"] = msg,
            ["time"] = DateTime.Now,
            //optional:
            //["category"] = Ex?.GetType().Name,
            //["details"] = new ArrayList()
        };

        //legacy response: TODO DEPRECATE
        ps["code"] = code;
        ps["err_msg"] = msg;
        ps["success"] = false;
        ps["message"] = msg;
        ps["err_time"] = DateTime.Now;

        if (this.config("IS_DEV").toBool())
        {
            ps["is_dump"] = true;
            if (Ex != null)
                ps["DUMP_STACK"] = Ex.ToString();

            ps["DUMP_SQL"] = DB.last_sql;
            ps["DUMP_FORM"] = FwLogger.dumper(FORM ?? new Hashtable());
            ps["DUMP_SESSION"] = context?.Session != null ? FwLogger.dumper(context.Session) : "null";
        }

        parser(tpl_dir, ps);
    }

    // return model object by type
    // CACHED in fw.models, so it's singletones
    public T model<T>() where T : new()
    {
        Type tt = typeof(T);
        if (!models.ContainsKey(tt.Name))
        {
            T m = new();

            // initialize
            var initMethod = typeof(T).GetMethod("init");
            initMethod?.Invoke(m, [this]);

            models[tt.Name] = m;
        }
        return (T)models[tt.Name];
    }

    // return model object by model class name
    public FwModel model(string model_name)
    {
        if (!models.ContainsKey(model_name))
        {
            Type mt = Type.GetType(FW_NAMESPACE_PREFIX + model_name) ?? throw new ApplicationException("Error initializing model: [" + FW_NAMESPACE_PREFIX + model_name + "] class not found");
            FwModel m = (FwModel)Activator.CreateInstance(mt);
            // initialize
            m.init(this);
            models[model_name] = m;
        }
        return (FwModel)models[model_name];
    }

    /// <summary>
    /// Return controller instance by controller class name
    /// </summary>
    /// <param name="controller_name">controller </param>
    /// <returns></returns>
    /// <exception cref="ApplicationException"></exception>
    public FwController? controller(string controller_name, bool is_auth_check = true)
    {
        ////validate - name should end with "Controller"
        //if (!controller_name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        //    throw new ApplicationException($"Controller class name should end on 'Controller': {controller_name}");

        if (controllers.ContainsKey(controller_name))
            return (FwController)controllers[controller_name];

        FwController c;
        Type ct = Type.GetType(FW_NAMESPACE_PREFIX + controller_name + "Controller", false, true); // case ignored
        if (ct == null)
        {
            //if no such controller class - try virtual controllers
            logger(LogLevel.TRACE, $"Controller class not found, trying Virtual Controller: {controller_name}");
            //strip "Controller" suffix from controller_name if it present
            //var controller_icode = controller_name[..^"Controller".Length];

            var controller_icode = controller_name;

            var fwcon = model<FwControllers>().oneByIcode(controller_icode);
            if (fwcon.Count == 0)
                return null; // controller class not found even in virtual controllers TODO NoControllerException?

            // check defined access level
            if (is_auth_check && userAccessLevel < fwcon["access_level"].toInt())
                throw new AuthException("Bad access - Not authorized (4)");

            c = new FwVirtualController(this, fwcon);
            //already initialized in constructor
        }
        else
        {
            c = (FwController)Activator.CreateInstance(ct);
            if (is_auth_check)
            {
                // controller found
                // but need's check access level on controller level, logged level will be 0 for visitors
                var controllerClass = c.GetType();
                var field = controllerClass.GetField("access_level", BindingFlags.Public | BindingFlags.Static);
                if (field != null)
                {
                    if (userAccessLevel < field.GetValue(null).toInt())
                        throw new AuthException("Bad access - Not authorized (2)");
                }

                //note, Role-Based Access - checked in callController right before calling action
            }

            // initialize
            c.init(this);
        }

        controllers[controller_name] = c;

        return c;
    }

    public void logActivity(string log_types_icode, string entity_icode, int item_id = 0, string iname = "", Hashtable? changed_fields = null)
    {
        if (!is_log_events)
            return;

        Hashtable? payload = null;
        if (changed_fields != null)
            payload = new Hashtable()
            {
                {"fields", changed_fields}
            };
        this.model<FwActivityLogs>().addSimple(log_types_icode, entity_icode, item_id, iname, payload);
    }

    public void rw(string str)
    {
        this.responseWrite(str + "<br>" + System.Environment.NewLine);
        this.response.Body.FlushAsync();
    }


    private bool disposedValue; // To detect redundant calls

    // IDisposable
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects).
                db.Dispose(); // this will return db connections to pool
                flogger.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override Finalize() below.
            // TODO: set large fields to null.
        }
        disposedValue = true;
    }

    // override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
    ~FW()
    {
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(false);
    }

    // This code added by Visual Basic to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(true);
        // uncomment the following line if Finalize() is overridden above.
        GC.SuppressFinalize(this);
    }
}
