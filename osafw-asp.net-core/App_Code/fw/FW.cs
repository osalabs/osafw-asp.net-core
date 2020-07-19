using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
{
    public class FW : IDisposable
    {
        public DB db;

        public static Hashtable METHOD_ALLOWED = Utils.qh("GET POST PUT DELETE");

        public Hashtable FORM;
        public Hashtable G;    // for storing global vars - used in template engine, also stores "_flash"
        public Hashtable FERR; // for storing form id's with error messages, put to hf("ERR") for parser

        private HttpContext context;
        private HttpRequest req;
        private HttpResponse resp;

        public string request_url; // current request url (relative to application url)
        public string cur_controller_path; // store /Prefix/Controller - to use in parser a default path for templates
        public string cur_method;
        public string cur_controller;
        public string cur_action;
        public string cur_id;
        public string cur_action_more;
        public string cur_format;
        public ArrayList cur_params;
        public FwLogger fwLogger;

        private System.IO.FileStream floggerFS;
        private System.IO.StreamWriter floggerSW;

        public static FW Current;
        public FwCache cache = new FwCache(); // request level cache

        private readonly Hashtable models = new Hashtable();

        public FW(HttpContext _context, IConfiguration conf_settings)
        {
            context = _context;
            req = context.Request;
            resp = context.Response;
            fwLogger = new FwLogger();
            FwConfig.init(req, conf_settings);

            db = new DB(this);
            DB.SQL_QUERY_CTR = 0; // reset query counter

            G = (Hashtable)config().Clone(); // by default G contains conf

            // override default lang with user's lang
            if (getSessionString("lang") != "") {
                G["lang"] = getSessionString("lang");
            }

            FERR = new Hashtable(); // reset errors
            parse_form();

            // save flash to current var and update session as flash is used only for nearest request
            if (getSessionHashtable("_flash") != null)
            {
                G["_flash"] = getSessionHashtable("_flash").Clone();
            }
            setSessionHashtable("_flash", new Hashtable());
        }


        public int? getSessionInt(String name) {
            if (context.Session == null) {
                logger(FwLogger.LogLevel.ERROR, "CONTEXT SESSION IS NULL");
                return null;
            }
            return context.Session.GetInt32(name);
        }
        public String? getSessionString(String name)
        {
            if (context.Session == null)
            {
                logger(FwLogger.LogLevel.ERROR, "CONTEXT SESSION IS NULL");
                return null;
            }
            return context.Session.GetString(name);
        }
        public Hashtable? getSessionHashtable(String name)
        {
            if (context.Session == null)
            {
                logger(FwLogger.LogLevel.ERROR, "CONTEXT SESSION IS NULL");
                return null;
            }
            String data = context.Session.GetString(name);
            return data == null ? null : JsonConvert.DeserializeObject<Hashtable>(data);
        }

        public void setSessionInt(String name, int value)
        {
            if (context.Session == null)
            {
                logger(FwLogger.LogLevel.ERROR, "CONTEXT SESSION IS NULL");
                return;
            }
            context.Session.SetInt32(name, value);
        }

        public void setSessionString(String name, String value)
        {
            if (context.Session == null)
            {
                logger(FwLogger.LogLevel.ERROR, "CONTEXT SESSION IS NULL");
                return;
            }
            context.Session.SetString(name, value);
        }

        public void setSessionHashtable(String name, Hashtable value)
        {
            if (context.Session == null)
            {
                logger(FwLogger.LogLevel.ERROR, "CONTEXT SESSION IS NULL");
                return;
            }
            String data = JsonConvert.SerializeObject(value);
            context.Session.SetString(name, data);
        }

        public void logger(params Object[] args)
        {
            if (args.Length == 0) return;
            fwLogger.logger(FwLogger.LogLevel.DEBUG, ref args);
        }
        public void logger(FwLogger.LogLevel level, params Object[] args)
        {
            if (args.Length == 0) return;
            fwLogger.logger(level, ref args);
        }

        // parse query string, form and json in request body into fw.FORM
        private async void parse_form()
        {
            Hashtable f = new Hashtable();

            string s = "";
            foreach (string key in req.Query.Keys)
            {
                s = key;
                if (s != null)
                {
                    f[s] = req.Query[s];
                }
            }

            if (req.HasFormContentType)
            {
                foreach (string key in req.Form.Keys)
                {
                    s = key;
                    if (s != null)
                    {
                        f[s] = req.Form[s];
                    }
                }
            }
            // after perpare_FORM - grouping for names like XXX[YYYY] -> FORM{XXX}=@{YYYY1, YYYY2, ...}
            Hashtable SQ = new Hashtable();
            string k;
            string sk;
            string v;
            ArrayList rem_keys = new ArrayList();

            foreach (string key in f.Keys)
            {
                s = key;
                Match m = Regex.Match(s, @"^([^\]]+)\[([^\]]+)\]$");
                if (m.Groups.Count > 1)
                {
                    k = m.Groups[1].ToString();
                    sk = m.Groups[2].ToString();
                    v = (string)f[s];
                    if (!SQ.ContainsKey(k))
                    {
                        SQ[k] = new Hashtable();
                    }
                    //SQ.Item[k].item[sk] = v;
                    rem_keys.Add(s);
                }
            }

            foreach (string key in rem_keys)
            {
                f.Remove(key);
            }
            foreach (string key in SQ.Keys)
            {
                f[s] = SQ[s];
            }

            // also parse json in request body if any
            if (req.ContentLength != null &&
                req.ContentLength.Value > 0 &&
                req.ContentType != null &&
                req.ContentType.Substring(0, new string("application/json").Length) == "application/json")
            {
                try
                {
                    // also could try this with Utils.json_decode
                    if (req.Body.CanSeek)
                    {
                        req.Body.Seek(0, SeekOrigin.Begin);
                    }

                    if (req.Body.CanRead)
                    {
                        using (StreamReader stream = new StreamReader(req.Body))
                        {
                            string json = await stream.ReadToEndAsync();
                            Hashtable h = JsonConvert.DeserializeObject<Hashtable>(json);
                            // logger(LogLevel.TRACE, "REQUESTED JSON:", h)
                            Utils.mergeHash(ref f, ref h);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // logger(LogLevel.WARN, "Request JSON parse error")
                }
            }
            // logger(f)
            FORM = f;
        }

        // begin processing one request
        public static void run(HttpContext context, IConfiguration settings)
        {
            FW fw = new FW(context, settings);
            FW.Current = fw;

            FwHooks.initRequest(fw);
            fw.dispatch();
            //fw.Finalize()
        }

        // return model object by model ty[e
        public FwModel modelOf(Type model)
        {
            return this.model((String)model.Name);
        }

        // return model object by model name
        public FwModel model(string model_name)
        {
            if (!models.ContainsKey(model_name))
            {
                FwModel m = (FwModel)Activator.CreateInstance(Type.GetType("osafw_asp.net_core.fw." + model_name, false, true));
                // initialize
                m.init(this);
                models[model_name] = m;
            }
            return (FwModel)models[model_name];
        }

        public async void rw(string str)
        {
            await this.resp.WriteAsync(str);
            await this.resp.WriteAsync(System.Environment.NewLine);
        }

        // return all the settings
        public static Hashtable config() {
            return FwConfig.settings;
        }

        // return just particular setting
        public static Object config(String name) {
            if (FwConfig.settings.ContainsKey(name))
            {
                return FwConfig.settings[name];
            }
            return null;
        }

        public void call_controller(Type calledType, MethodInfo mInfo, Object[] args = null) {
            // check if method assept agrs and not pass it if no args expected
            ParameterInfo[] parameters = mInfo.GetParameters();
            if (parameters.Length == 0)
            {
                args = null;
            }

            FwController new_controller = (FwController)Activator.CreateInstance(calledType);
            new_controller.init(this);
            Hashtable ps = null;
            try
            {
                ps = (Hashtable)mInfo.Invoke(new_controller, args);
            }
            catch (TargetInvocationException ex) {
                // ignore redirect exception
                if (ex.InnerException == null) {// || Not(TypeOf(ex.InnerException) Is RedirectException) Then
                    throw new Exception("this keeps stack, also see http://weblogs.asp.net/fmarguerie/rethrowing-exceptions-and-preserving-the-full-call-stack-trace");
                }
                //Throw ex.InnerException
            }
            //if (ps != null) parser(ps);
        }

        public static String dumper(Object dmp_obj, int level = 0) // TODO better type detection(suitable for all collection types)
        {
            StringBuilder str = new StringBuilder();
            if (dmp_obj == null) return "[Nothing]";
            if (level > 10) return "[Too Much Recursion]";

            try
            {
                Type type = dmp_obj.GetType();
                TypeCode typeCode = Type.GetTypeCode(type);
                String intend = new StringBuilder().Insert(0, "    ", level).Append(" ").ToString();

                level += 1;
                if (typeCode.ToString() == "Object") {
                    str.Append(System.Environment.NewLine);
                    if (dmp_obj is ArrayList)
                    {   // ArrayList
                        str.Append(intend + "[" + System.Environment.NewLine);
                        ArrayList _dmp_obj = (ArrayList)dmp_obj;
                        foreach (Object v in _dmp_obj)
                        {
                            str.Append(intend + " " + dumper(v, level) + System.Environment.NewLine);
                        }
                        str.Append(intend + "]" + System.Environment.NewLine);
                    }
                    else if (dmp_obj is Hashtable)
                    {   // Hashtable
                        str.Append(intend + "{" + System.Environment.NewLine);
                        Hashtable _dmp_obj = (Hashtable)dmp_obj;
                        foreach (Object k in _dmp_obj.Keys)
                        {
                            str.Append(intend + " " + k.ToString() + " => " + dumper(_dmp_obj[k], level) + System.Environment.NewLine);
                        }
                        str.Append(intend + "}" + System.Environment.NewLine);
                    } else {
                        str.Append(intend + type.ToString() + "==" + typeCode.ToString() + System.Environment.NewLine);
                    }
                } else {
                    str.Append(dmp_obj.ToString());
                }
            }
            catch (Exception ex) {
                str.Append("***cannot dump object***");
            }

            return str.ToString();
        }

        public async void dispatch()
        {
            DateTime start_time = DateTime.Now;

            string url = req.Path;
            // cut the App path from the begin
            if (req.PathBase.Value.Length > 0 && req.PathBase.Value != "/")
            {
                url = url.Replace(req.PathBase, "");
            }
            url = Regex.Replace(url, @"\/$", ""); // cut last / if any
            request_url = url;

            // logger(LogLevel.TRACE, "REQUESTING ", url)

            // init defaults
            cur_controller = "Home";
            cur_action = "Index";
            cur_id = "";
            cur_action_more = "";
            cur_format = "html";
            cur_method = req.Method;
            cur_params = new ArrayList();

            // check if method override exits
            if (FORM.ContainsKey("_method"))
            {
                if (METHOD_ALLOWED.ContainsKey(FORM["_method"]))
                {
                    cur_method = (string)FORM["_method"];
                }
            }
            if (cur_method == "HEAD")
            {
                cur_method = "GET"; // for website processing HEAD is same as GET, IIS will send just headers
            }

            string cur_action_raw = "";
            string controller_prefix = "";


            // process config special routes (redirects, rewrites)
            Hashtable routes = new Hashtable(); // As Hashtable = Me.config("routes")
            bool is_routes_found = false;

            /*

            For Each route As String In routes.Keys
                If url = route Then
                    Dim rdest As String = routes(route)
                    Dim m1 As Match = Regex.Match(rdest, "^(?:(GET|POST|PUT|DELETE) )?(.+)")
                    If m1.Success Then
                        'override method
                        If m1.Groups(1).Value > "" Then cur_method = m1.Groups(1).Value
                        If m1.Groups(2).Value.Substring(0, 1) = "/" Then
                            'if started from / - this is redirect url
                            url = m1.Groups(2).Value
                        Else
                            'it's a direct class-method to call, no further REST processing required
                            is_routes_found = True
                            Dim sroute As String() = Split(m1.Groups(2).Value, "::", 2)
                            cur_controller = Utils.routeFixChars(sroute(0))
                            If UBound(sroute) > 0 Then cur_action_raw = sroute(1)
                            Exit For
                        End If
                    Else
                        logger(LogLevel.WARN, "Wrong route destination: " & rdest)
                    End If
                End If
            Next
            */

            if (!is_routes_found)
            {
                // TODO move prefix cut to separate func

                /*string prefix_rx = FwConfig.getRoutePrefixesRX()*/
                cur_controller_path = "";
                /*
                Dim m_prefix As Match = Regex.Match(url, prefix_rx)
                If m_prefix.Success Then
                    'convert from /Some/Prefix to SomePrefix
                    controller_prefix = Utils.routeFixChars(m_prefix.Groups(1).Value)
                    cur_controller_path = "/" & controller_prefix
                    url = m_prefix.Groups(2).Value
                End If
                */


                // detect REST urls
                // GET   /controller[/.format]       Index
                // POST  /controller                 Save     (save new record - Create)
                // PUT   /controller                 SaveMulti (update multiple records)
                // GET   /controller/new             ShowForm (show new form - ShowNew)
                // GET   /controller/{id}[.format]   Show     (show in format - not for editing)
                // GET   /controller/{id}/edit       ShowForm (show edit form - ShowEdit)
                // GET   /controller/{id}/delete     ShowDelete
                // POST/PUT  /controller/{id}        Save     (save changes to exisitng record - Update    Note:Request.Form should contain data
                // POST/DELETE  /controller/{id}            Delete    Note:Request.Form should NOT contain any data

                // /controller/(Action)              Action    call for arbitrary action from the controller
                Match m = Regex.Match(url, @"^/([^/]+)(?:/(new|\.\w+)|/([\d\w_-]+)(?:\.(\w+))?(?:/(edit|delete))?)?/?$");
                if (m.Success)
                {
                    cur_controller = Utils.routeFixChars(m.Groups[1].Value);
                    if (String.IsNullOrEmpty(cur_controller))
                    {
                        throw new Exception("Wrong request");
                    }

                    // capitalize first letter - TODO - URL-case-insensitivity should be an option!
                    cur_controller = cur_controller.Substring(0, 1).ToUpper() + cur_controller.Substring(1);
                    cur_id = m.Groups[3].Value;
                    cur_format = m.Groups[4].Value;
                    cur_action_more = m.Groups[5].Value;
                    if (m.Groups[2].Value != "")
                    {
                        if (m.Groups[2].Value == "new")
                        {
                            cur_action_more = "new";
                        }
                        else
                        {
                            cur_format = m.Groups[2].Value.Substring(1);
                        }
                    }

                    // match to method (GET/POST)
                    if (cur_method == "GET")
                    {
                        if (cur_action_more == "new")
                        {
                            cur_action_raw = "ShowForm";
                        }
                        else if (cur_id != "" && cur_action_more == "edit")
                        {
                            cur_action_raw = "ShowForm";
                        }
                        else if (cur_id != "" && cur_action_more == "delete")
                        {
                            cur_action_raw = "ShowDelete";
                        }
                        else if (cur_id != "")
                        {
                            cur_action_raw = "Show";
                        }
                        else
                        {
                            cur_action_raw = "Index";
                        }
                    }
                    else if (cur_method == "POST")
                    {
                        if (cur_id != "")
                        {
                            if (req.Form.Count > 0 || req.Body.Length > 0) // POST form or body payload
                            {
                                cur_action_raw = "Save";
                            }
                            else
                            {
                                cur_action_raw = "Delete";
                            }
                        }
                        else
                        {
                            cur_action_raw = "Save";
                        }
                    }
                    else if (cur_method == "PUT")
                    {
                        if (cur_id != "")
                        {
                            cur_action_raw = "Save";
                        }
                        else
                        {
                            cur_action_raw = "SaveMulti";
                        }
                    }
                    else if (cur_method == "DELETE" && cur_id != "")
                    {
                        cur_action_raw = "Delete";
                    }
                    else
                    {
                        // logger(LogLevel.WARN, "Wrong Route Params")
                        // logger(LogLevel.WARN, cur_method)
                        // logger(LogLevel.WARN, url)
                        // err_msg("Wrong Route Params")
                        return;
                    }

                    // logger(LogLevel.TRACE, "REST controller.action=", cur_controller, ".", cur_action_raw)

                }
                else
                {
                    // otherwise detect controller/action/id.format/more_action
                    string[] parts = url.Split("/");
                    // logger(parts)
                    int ub = parts.Length;
                    if (ub >= 1) cur_controller = Utils.routeFixChars(parts[0]);
                    if (ub >= 2) cur_action_raw = parts[1];
                    if (ub >= 3) cur_id = parts[2];
                    if (ub >= 4) cur_action_more = parts[3];
                }
            }

            cur_controller_path = cur_controller_path + "/" + cur_controller;
            // add controller prefix if any
            cur_controller = controller_prefix + cur_controller;
            cur_action = Utils.routeFixChars(cur_action_raw);
            if (String.IsNullOrEmpty(cur_action)) cur_action = "Index";

            string[] args = { cur_id }; // TODO - add rest of possible params from parts

            try
            {
                //Dim auth_check_controller = _auth(cur_controller, cur_action)
                Type calledType = Type.GetType("osafw_asp.net_core.fw." + cur_controller + "Controller", false, true); // case ignored
                if (calledType == null)
                {
                    // logger(LogLevel.DEBUG, "No controller found for controller=[", cur_controller, "], using default Home")
                    // no controller found - call default controller with default action
                    calledType = Type.GetType("HomeController", true);
                    cur_controller_path = "/Home";
                    cur_controller = "Home";
                    cur_action = "NotFound";
                }
                else
                {
                    // controller found
                    /*If auth_check_controller = 1 Then
                        'but need's check access level on controller level
                        Dim field = calledType.GetField("access_level", BindingFlags.Public Or BindingFlags.Static)
                        If field IsNot Nothing Then
                            Dim current_level As Integer = -1
                            If SESSION("access_level") IsNot Nothing Then current_level = SESSION("access_level")

                            If current_level<Utils.f2int(field.GetValue(Nothing)) Then Throw New AuthException("Bad access - Not authorized (2)")
                        End If
                    End If*/

                    //MethodInfo mInfo = calledType.GetMethod(cur_action + "Action");
                    //call_controller(calledType, mInfo, args);
                }

                //logger(LogLevel.TRACE, "TRY controller.action=", cur_controller, ".", cur_action)
                MethodInfo mInfo = calledType.GetMethod(cur_action + "Action");
                if (mInfo == null)
                {
                    //logger(LogLevel.DEBUG, "No method found for controller.action=[", cur_controller, ".", cur_action, "], checking route_default_action")
                    // no method found - try to get default action
                    bool what_to_do = false;
                    FieldInfo pInfo = calledType.GetField("route_default_action");
                    if (pInfo != null)
                    {
                        String pvalue = (String)pInfo.GetValue(null);
                        if (pvalue == "index")
                        {
                            // = index - use IndexAction for unknown actions
                            cur_action = "Index";
                            mInfo = calledType.GetMethod(cur_action + "Action");
                            what_to_do = true;
                        }
                        else if (pvalue == "show")
                        {
                            // = show - assume action is id and use ShowAction
                            if (cur_id != "") cur_params.Add(cur_id); // cur_id is a first param in this case. TODO - add all rest of params from split("/") here
                            if (cur_action_more != "") cur_params.Add(cur_action_more); // cur_action_more is a second param in this case

                            cur_id = cur_action_raw;
                            args[0] = cur_id;

                            cur_action = "Show";
                            mInfo = calledType.GetMethod(cur_action + "Action");
                            what_to_do = true;
                        }
                    }
                }

                // save to globals so it can be used in templates
                //G("controller") = cur_controller
                //G("action") = cur_action
                //G("controller.action") = cur_controller & "." & cur_action

                // logger(LogLevel.TRACE, "FINAL controller.action=", cur_controller, ".", cur_action)
                //logger(LogLevel.TRACE, "cur_method=" , cur_method)
                //logger(LogLevel.TRACE, "cur_controller=" , cur_controller)
                //logger(LogLevel.TRACE, "cur_action=" , cur_action)
                //logger(LogLevel.TRACE, "cur_format=" , cur_format)
                //logger(LogLevel.TRACE, "cur_id=" , cur_id)
                //logger(LogLevel.TRACE, "cur_action_more=" , cur_action_more)

                // logger(LogLevel.INFO, "REQUEST START [", cur_method, " ", url, "] => ", cur_controller, ".", cur_action)

                if (mInfo == null)
                {
                    // if no method - just call FW.parser(hf) - show template from /cur_controller/cur_action dir
                    // logger(LogLevel.DEBUG, "DEFAULT PARSER")
                    // parser(New Hashtable)
                }
                else
                {
                    call_controller(calledType, mInfo, args);
                }
                //logger(LogLevel.INFO, "NO EXCEPTION IN dispatch")
            }
            catch (Exception ex)
            {

            }

            /*
                Catch Ex As RedirectException
                    'not an error, just exit via Redirect
                    logger(LogLevel.INFO, "Redirected...")

                Catch Ex As AuthException 'not authorized for the resource requested
                    logger(LogLevel.DEBUG, Ex.Message)
                    'if not logged - just redirect to login 
                    If SESSION("is_logged") <> True Then
                        redirect(config("UNLOGGED_DEFAULT_URL"), False)
                    Else
                        err_msg(Ex.Message)
                    End If

                Catch Ex As ApplicationException

                    'get very first exception
                    Dim msg As String = Ex.Message
                    Dim iex As Exception = Ex
                    While iex.InnerException IsNot Nothing
                        iex = iex.InnerException
                        msg = iex.Message
                    End While

                    If TypeOf (iex) Is RedirectException Then
                        'not an error, just exit via Redirect - TODO - remove here as already handled above?
                        logger(LogLevel.DEBUG, "Redirected...")
                    ElseIf TypeOf(iex) Is UserException Then
                        'no need to log/report detailed user exception
                        logger(LogLevel.INFO, "UserException: " & msg)
                        err_msg(msg, iex)
                    Else
                        'it's ApplicationException, so just warning
                        logger(LogLevel.WARN, "===== ERROR DUMP APP =====")
                        logger(LogLevel.WARN, Ex.Message)
                        logger(LogLevel.WARN, Ex.ToString())
                        logger(LogLevel.WARN, "REQUEST FORM:", FORM)
                        logger(LogLevel.WARN, "SESSION:", SESSION)

                        'send_email_admin("App Exception: " & Ex.ToString() & vbCrLf & vbCrLf & _
                        '                 "Request: " & req.Path & vbCrLf & vbCrLf & _
                        '                 "Form: " & dumper(FORM) & vbCrLf & vbCrLf & _
                        '                 "Session:" & dumper(SESSION))

                        err_msg(msg, Ex)
                    End If

                Catch Ex As Exception
                    'it's general Exception, so something more severe occur, log as error and notify admin
                    logger(LogLevel.ERROR, "===== ERROR DUMP =====")
                    logger(LogLevel.ERROR, Ex.Message)
                    logger(LogLevel.ERROR, Ex.ToString())
                    logger(LogLevel.ERROR, "REQUEST FORM:", FORM)
                    logger(LogLevel.ERROR, "SESSION:", SESSION)

                    send_email_admin("Exception: " & Ex.ToString() & vbCrLf & vbCrLf &
                                     "Request: " & req.Path & vbCrLf & vbCrLf &
                                     "Form: " & dumper(FORM) & vbCrLf & vbCrLf &
                                     "Session:" & dumper(SESSION))

                    If Me.config("log_level") >= LogLevel.DEBUG Then
                        Throw
                    Else
                        err_msg("Server Error. Please, contact site administrator!", Ex)
                    End If
                End Try
                */
            TimeSpan end_timespan = DateTime.Now - start_time;
            // logger(LogLevel.INFO, "REQUEST END   [", cur_method, " ", url, "] in ", end_timespan.TotalSeconds, "s, ", String.Format("{0:0.000}", 1 / end_timespan.TotalSeconds), "/s, ", DB.SQL_QUERY_CTR, " SQL")*/

        }


        #region "IDisposable Support"
        private bool disposedValue; // To detect redundant calls

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //dispose managed state (managed objects).
                }

                // free unmanaged resources (unmanaged objects) and override Finalize() below.
                try
                {
                    db.Dispose(); //this will return db connections to pool

                    long log_length = 0;
                    if (floggerFS != null) log_length = floggerFS.Length;

                    if (floggerSW != null) floggerSW.Close(); //no need to close floggerFS as StreamWriter closes it
                    if (floggerFS != null)
                    {
                        floggerFS.Close();

                        // check if log file too large and need to be rotated
                        int max_log_size = Utils.f2int(config("log_max_size"));
                        if (max_log_size > 0 && log_length > max_log_size)
                        {
                            String to_path = config("log") + ".1";
                            File.Delete(to_path);
                            File.Move((String)config("log"), to_path);
                        }
                    }
                    // TODO: set large fields to null.
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("exception in Dispose:" + ex.Message);
                }
            }
            disposedValue = true;
        }

        // override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
        protected void Finalize()
        {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(false);
            this.Finalize();
        }

        // This code added by Visual Basic to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(true);
            // uncomment the following line if Finalize() is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion

    }
}
