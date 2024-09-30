// Fw Self Test base class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com


using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

namespace osafw;

public class FwSelfTest
{
    protected FW fw;
    protected DB db;
    public bool is_logged = false;
    public bool is_db = false; // set to true after db connection test if db connection successful

    public int ok_ctr = 0;    // number of successfull tests
    public int warn_ctr = 0;  // number of warning tests
    public int err_ctr = 0;   // number of errorneous tests
    public int total_ctr = 0; // total tests

    // Test results for self-test
    public enum Result : int
    {
        OK,
        WARN,
        ERR
    }

    public string test_email = ""; // if empty, will use "test"+mail_from
    public string existing_tables = "fwsessions fwentities users settings spages att att_links att_categories log_types activity_logs lookup_manager_tables user_views user_lists user_lists_items"; // check if these tables exists
    public string exclude_controllers = "";

    public FwSelfTest(FW fw)
    {
        this.fw = fw;
        this.db = fw.db;

        is_logged = fw.isLogged;
    }

    // '''''''''''''''''''''''''' high level tests

    /// <summary>
    /// run all tests
    /// </summary>
    public virtual void all()
    {
        configuration();
        database_tables();
        controllers();
    }

    /// <summary>
    /// test config values. Override to make additional config tests
    /// </summary>
    public virtual void configuration()
    {
        // test important config settings
        echo("<strong>Config</strong>");
        echo("hostname: " + fw.config("hostname"));
        is_notempty("site_root", fw.config("site_root"));

        // log_level: higher than debug - OK, debug - warn, trace or below - red (not for prod)
        var log_level = (LogLevel)fw.config("log_level");
        if (log_level >= LogLevel.TRACE)
        {
            plus_err();
            echo("log_level", Enum.GetName(typeof(LogLevel), log_level), Result.ERR);
        }
        else if (log_level == LogLevel.DEBUG)
        {
            plus_warn();
            echo("log_level", Enum.GetName(typeof(LogLevel), log_level), Result.WARN);
        }
        else
        {
            plus_ok();
            echo("log_level", "OK");
        }

        is_false("is_test", Utils.toBool(fw.config("is_test")), "Turned ON");
        is_false("IS_DEV", Utils.toBool(fw.config("IS_DEV")), "Turned ON");

        // template directory should exists - TODO test parser to actually see templates work?
        is_true("template", Directory.Exists((string)fw.config("template")), (string)fw.config("template"));
        is_true("access_levels", fw.config("access_levels") != null && ((Hashtable)fw.config("access_levels")).Count > 0, "Not defined");

        // UPLOAD_DIR upload dir is writeable
        try
        {
            string upload_filepath = UploadUtils.getUploadDir(fw, "selftest", 1) + "/txt";
            string file_data = "test";
            FW.setFileContent(upload_filepath, ref file_data);
            File.Delete(upload_filepath);
            plus_ok();
            echo("upload dir", "OK");
        }
        catch (Exception ex)
        {
            plus_err();
            echo("upload dir", ex.Message, Result.ERR);
        }

        // emails set
        is_notempty("mail_from", fw.config("mail_from"));
        is_notempty("support_email", fw.config("support_email"));

        // test send email to "test+mail_from"
        is_true("Send Emails", fw.sendEmail("", (string.IsNullOrEmpty(test_email) ? "test+" + fw.config("mail_from") : test_email), "test email", "test body"), "Failed");

        try
        {
            db.connect();
            is_db = true;
            plus_ok();
            echo("DB connection", "OK");
        }
        catch (Exception ex)
        {
            plus_err();
            echo("DB connection", ex.Message, Result.ERR);
        }
    }

    public virtual void database_tables()
    {
        if (is_db)
        {
            echo("<strong>DB Tables</strong>");
            // fw core db tables exists and we can read from it
            // (select count(*) from: users, settings, spages, att, att_links, att_categories)
            string[] tables = Utils.qw(existing_tables);
            foreach (var table in tables)
            {
                try
                {
                    db.valuep(db.limit("select * from " + db.qid(table), 1));
                    plus_ok();
                    echo("table " + table, "OK");
                }
                catch (Exception ex)
                {
                    plus_err();
                    echo("table " + table, ex.Message, Result.ERR);
                }
            }
        }
    }

    public virtual void controllers()
    {
        // test controllers (TODO - only for logged admin user)
        echo("<strong>Controllers</strong>");

        // get all classes ending with "Controller" and not starting with "Fw"
        var aControllers = System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.Name != "AdminSelfTestController" && t.Name.EndsWith("Controller") && !t.Name.StartsWith("Fw"))
            .OrderBy(t => t.Name)
            .ToList();

        if (aControllers.Count == 0)
        {
            plus_err();
            echo("Controllers", "None found", FwSelfTest.Result.ERR);
        }

        Hashtable hexclude = Utils.qh(exclude_controllers);

        foreach (var t in aControllers)
        {
            string controller_name = t.Name.Replace("Controller", "");
            // omit controllers we don't need to test
            if (hexclude.ContainsKey(controller_name))
                continue;

            fw.logger("Testing Controller:" + controller_name);

            Type calledType = Type.GetType(FW.FW_NAMESPACE_PREFIX + t.Name, false, true);
            if (calledType == null)
            {
                plus_err();
                echo(t.Name, "Not found", FwSelfTest.Result.ERR);
                continue;
            }

            try
            {

                // check controller have SelfTest method
                // SelfTest method should accept one argument FwSelfTest
                // and return FwSelfTest.Result
                // sample Controller.SelfTest declaration:
                //
                // Public Function SelfTest(t As FwSelfTest) As FwSelfTest.Result
                // Dim res As Boolean = True
                // res = res AndAlso t.is_true("Inner var check", (var = 1)) = FwSelfTest.Result.OK
                // Return IIf(res, FwSelfTest.Result.OK, FwSelfTest.Result.ERR)
                // End Function

                System.Reflection.MethodInfo mInfo = calledType.GetMethod("SelfTest");
                if (mInfo == null)
                {

                    // if no SelfTest - test IndexAction method
                    mInfo = calledType.GetMethod("IndexAction");
                    if (mInfo == null)
                    {
                        plus_warn();
                        echo(t.Name, "No SelfTest or IndexAction methods found", FwSelfTest.Result.WARN);
                        continue;
                    }

                    // test using IndexAction
                    // need to buffer output from controller to clear it later
                    //TODO MIGRATE
                    //fw.response.BufferOutput = true;
                    //var bufferingFeature2 = fw.context.Features.Get<IHttpResponseBodyFeature>();
                    //bufferingFeature2?.DisableBuffering();

                    fw._auth(controller_name, FW.ACTION_INDEX);
                    fw.setController(controller_name, FW.ACTION_INDEX);

                    FwController new_controller = (FwController)Activator.CreateInstance(calledType);
                    new_controller.init(fw);
                    Hashtable ps = (Hashtable)mInfo.Invoke(new_controller, null);
                    //TODO MIGRATE
                    //fw.response.Clear();
                    //fw.response.BufferOutput = false;

                    if (ps == null || ps.Count == 0)
                    {
                        plus_warn();
                        echo(t.Name, "Empty result", FwSelfTest.Result.WARN);
                    }
                    else
                    {
                        plus_ok();
                        echo(t.Name, "OK");
                    }
                }
                else
                {
                    // test using SelfTest
                    fw._auth(controller_name, "SelfTest");

                    FwController new_controller = (FwController)Activator.CreateInstance(calledType);
                    new_controller.init(fw);
                    Result res = (Result)mInfo.Invoke(new_controller, new object[] { this });
                    if (res == Result.OK)
                    {
                        plus_ok();
                        echo(t.Name, "OK");
                    }
                    else if (res == Result.WARN)
                    {
                        plus_warn();
                        echo(t.Name, "Warning", res);
                    }
                    else if (res == Result.ERR)
                    {
                        plus_err();
                        echo(t.Name, "Error", res);
                    }
                }
            }
            catch (AuthException)
            {
                // just skip controllers not authorized to current user
                fw.logger(controller_name + " controller test skipped, user no authorized");
            }

            catch (Exception ex)
            {

                if (ex.InnerException != null)
                {
                    if (ex.InnerException is RedirectException
                        || ex.InnerException.Message.Contains("Cannot redirect after HTTP headers have been sent.")
                        || ex is TargetInvocationException && ex.InnerException is InvalidOperationException && ex.InnerException.Message.Contains("StatusCode cannot be set because the response has already started.")
                        )
                    {
                        // just redirect in Controller.Index - it's OK
                        plus_ok();
                        echo(t.Name, "OK");
                    }
                    else
                    {
                        // something really wrong
                        fw.logger(ex.InnerException.ToString());
                        plus_err();
                        echo(t.Name, ex.InnerException.Message, FwSelfTest.Result.ERR);
                    }
                }
                else if (ex is RedirectException
                    || ex.Message.Contains("Cannot redirect after HTTP headers have been sent.")
                    || ex is InvalidOperationException && ex.Message.Contains("StatusCode cannot be set because the response has already started.")
                    )
                {
                    // just redirect in Controller.Index - it's OK
                    plus_ok();
                    echo(t.Name, "OK");
                }
                else
                {
                    // something really wrong
                    fw.logger(ex.ToString());
                    plus_err();
                    echo(t.Name, ex.Message, FwSelfTest.Result.ERR);
                }
            }
        }
    }


    /// <summary>
    /// default stub to output test header
    /// </summary>
    public virtual void echo_start()
    {
        echo("<h1>Site Self Test</h1>");
        // If Not is_logged Then echo("<a href='" & fw.config("ROOT_URL") & "/Login'>Login</a> as an administrator to see error details and perform additional tests")
        echo("<a href='#summary'>Test Summary</a>");
    }

    /// <summary>
    /// ouput test totals, success, warnings, errors
    /// </summary>
    public virtual void echo_totals()
    {
        echo("<a name='summary' />");
        echo("<h2>Test Summary</h2>");
        echo("Total : " + total_ctr);
        echo("Success", ok_ctr.ToString());
        echo("Warnings", warn_ctr.ToString(), Result.WARN);
        echo("Errors", err_ctr.ToString(), Result.ERR);

        // self check
        if (total_ctr != ok_ctr + warn_ctr + err_ctr)
            echo("Test count error", "total != ok+warn+err", Result.ERR);

        echo("<br><br><br><br><br>"); // add some footer spacing for easier review
    }


    // '''''''''''''''''''''''''' low level tests

    /// <summary>
    /// test of value is false and ouput OK. If true output ERROR or custom string
    /// </summary>
    /// <param name="label"></param>
    /// <param name="value"></param>
    /// <param name="err_str"></param>
    public Result is_false(string label, bool value, string err_str = "ERROR")
    {
        Result res = Result.ERR;
        total_ctr += 1;
        if (value)
            err_ctr += 1;
        else
        {
            ok_ctr += 1;
            err_str = "OK";
            res = Result.OK;
        }
        echo(label, err_str, res);
        return res;
    }

    /// <summary>
    /// test of value is true and ouput OK. If false output ERROR or custom string
    /// </summary>
    /// <param name="label"></param>
    /// <param name="value"></param>
    /// <param name="err_str"></param>
    public Result is_true(string label, bool value, string err_str = "ERROR")
    {
        Result res = Result.ERR;
        total_ctr += 1;
        if (value)
        {
            ok_ctr += 1;
            err_str = "OK";
            res = Result.OK;
        }
        else
            err_ctr += 1;
        echo(label, err_str, res);
        return res;
    }

    /// <summary>
    /// test of value is not nothing and not empty string and ouput OK. If value is empty output ERROR or custom string
    /// </summary>
    /// <param name="label"></param>
    /// <param name="value"></param>
    public Result is_notempty(string label, object value, string err_str = "EMPTY")
    {
        Result res = Result.ERR;
        total_ctr += 1;
        if (string.IsNullOrEmpty((string)value))
            err_ctr += 1;
        else
        {
            ok_ctr += 1;
            err_str = "OK";
            res = Result.OK;
        }
        echo(label, err_str, res);
        return res;
    }

    /// <summary>
    /// test of value is nothing or empty string and ouput OK. If false output ERROR or custom string
    /// </summary>
    /// <param name="label"></param>
    /// <param name="value"></param>
    public Result is_empty(string label, object value, string err_str = "EMPTY")
    {
        Result res = Result.ERR;
        total_ctr += 1;
        if (string.IsNullOrEmpty((string)value))
        {
            ok_ctr += 1;
            err_str = "OK";
            res = Result.OK;
        }
        else
            err_ctr += 1;
        echo(label, err_str, res);
        return res;
    }

    /// <summary>
    /// output test result to browser, optionally with color.
    /// </summary>
    /// <param name="label">string to use as label</param>
    /// <param name="str">test result value or just OK, ERROR</param>
    /// <param name="res">result category</param>
    public void echo(string label, string str = "", Result res = Result.OK)
    {
        if (res == Result.WARN)
            str = "<span style='background-color:#FF7200;color:#fff'>" + str + "</span>";
        else if (res == Result.ERR)
            str = "<span style='background-color:#DD0000;color:#fff'>" + str + "</span>";
        else if (!string.IsNullOrEmpty(str))
            str = "<span style='color:#009900;'>" + str + "</span>";

        // Output without parser because templates might not exists/configured
        if (!string.IsNullOrEmpty(str))
            fw.rw(label + " : " + str);
        else
            fw.rw(label);

        //TODO flush response
    }

    /// <summary>
    /// helper to add 1 to OK count
    /// </summary>
    public void plus_ok()
    {
        ok_ctr += 1;
        total_ctr += 1;
    }
    /// <summary>
    /// helper to add 1 to WARN count
    /// </summary>
    public void plus_warn()
    {
        warn_ctr += 1;
        total_ctr += 1;
    }
    /// <summary>
    /// helper to add 1 to ERR count
    /// </summary>
    public void plus_err()
    {
        err_ctr += 1;
        total_ctr += 1;
    }
}