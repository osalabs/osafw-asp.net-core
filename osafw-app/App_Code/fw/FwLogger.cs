// FW Logger
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

//if you use Sentry https://docs.sentry.io/platforms/dotnet/guides/aspnetcore/
//  install Sentry.AspNetCore (uncomment in csproj)
//  in appsettings.json set your Sentry.Dsn
//  in Program - uncomment webBuilder.UseSentry();
//  uncomment define below
// #define isSentry

using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace osafw;

/// <summary>
/// Logger levels, ex: logger(LogLevel.ERROR, "Something happened")
/// </summary>
public enum LogLevel : int
{
    OFF,             // no logging occurs
    FATAL,           // severe error, current request (or even whole application) aborted (notify admin)
    ERROR,           // error happened, but current request might still continue (notify admin)
    WARN,            // potentially harmful situations for further investigation, request processing continues
    INFO,            // default for production (easier maintenance/support), progress of the application at coarse-grained level (fw request processing: request start/end, sql, route/external redirects, sql, fileaccess, third-party API)
    NOTICE,          // normal, but noticeable condition (for Sentry logged as bradcrumbs)
    DEBUG,           // default for development (default for logger("msg") call), fine-grained level
    TRACE,           // very detailed dumps (in-module details like fw core, despatcher, parse page, ...)
    ALL              // just log everything
}

public class FwLogger : IDisposable
{
    public LogLevel log_level;
    public string log_file = "";
    public string site_root = "";
    public long log_max_size = 0;

    private FileStream floggerFS;
    private StreamWriter floggerSW;

    public static string dumper(object dmp_obj, int level = 0) // TODO better type detection(suitable for all collection types)
    {
        StringBuilder str = new();
        if (dmp_obj == null)
            return "[Nothing]";
        if (dmp_obj == DBNull.Value)
            return "[DBNull]";
        if (level > 10)
            return "[Too Much Recursion]";

        try
        {
            Type type = dmp_obj.GetType();
            TypeCode typeCode = Type.GetTypeCode(type);
            string intend = new StringBuilder().Insert(0, "    ", level).Append(' ').ToString();

            level += 1;
            if (typeCode.ToString() == "Object")
            {
                str.Append(System.Environment.NewLine);
                if (dmp_obj is IList list)
                {
                    str.Append(intend + "[" + System.Environment.NewLine);
                    foreach (object v in list)
                        str.Append(intend + " " + dumper(v, level) + System.Environment.NewLine);
                    str.Append(intend + "]" + System.Environment.NewLine);
                }
                else if (dmp_obj is IDictionary dictionary)
                {
                    str.Append(intend + "{" + System.Environment.NewLine);
                    foreach (object k in dictionary.Keys)
                        str.Append(intend + " " + k + " => " + dumper(dictionary[k], level) + System.Environment.NewLine);
                    str.Append(intend + "}" + System.Environment.NewLine);
                }
                else if (dmp_obj is ISession session)
                {
                    str.Append(intend + "{" + System.Environment.NewLine);
                    foreach (string k in session.Keys)
                        str.Append(intend + " " + k + " => " + dumper(session.GetString(k), level) + System.Environment.NewLine);
                    str.Append(intend + "}" + System.Environment.NewLine);
                }
                else
                    str.Append(intend + Utils.jsonEncode(dmp_obj, true) + System.Environment.NewLine);
            }
            else
                str.Append(dmp_obj.ToString());
        }
        catch (Exception ex)
        {
            str.Append("***cannot dump object***" + ex.Message);
        }

        return str.ToString();
    }

    public FwLogger() { }

    public FwLogger(LogLevel log_level, string log_file, string site_root, long log_max_size = 0)
    {
        this.log_level = log_level;
        this.log_file = log_file;
        this.site_root = site_root;
        this.log_max_size = log_max_size;
    }

    public void setScope(string env, string email)
    {
        env = env == "" ? "production" : env;

#if isSentry
        //configure Sentry logging
        Sentry.SentrySdk.ConfigureScope(scope =>
        {
            scope.User = new Sentry.SentryUser { Email = email };
            scope.Environment = env;
            scope.SetTag("ProcessId", Environment.ProcessId.ToString());
        });
#endif
    }

    public void log(params object[] args)
    {
        if (args.Length == 0) return;
        log(LogLevel.DEBUG, ref args);
    }

    public void log(LogLevel level, params object[] args)
    {
        if (args.Length == 0) return;
        log(level, ref args);
    }

    public void log(LogLevel level, ref object[] args)
    {
        if (level > log_level) return;

        StringBuilder str_prefix = new(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        str_prefix.Append(' ').Append(level.ToString()).Append(' ');
        str_prefix.Append(Environment.ProcessId).Append(':').Append(Environment.CurrentManagedThreadId).Append(' ');

        StringBuilder str_stack = new();
        StackTrace st = new(true);

        try
        {
            int i = 1;
            StackFrame sf = st.GetFrame(i);
            string fname = sf.GetFileName() ?? "";
            // skip logger methods, DB internals and FW.getDB's compiler-generated closure method
            // as we want to know line where the logged thing was actually called from
            while (sf.GetMethod().Name == "logger" || fname.EndsWith(Path.DirectorySeparatorChar + "DB.cs") || sf.GetMethod().Name.StartsWith("<getDB>"))
            {
                i += 1;
                sf = st.GetFrame(i);
                fname = sf.GetFileName() ?? "";
            }
            fname = sf.GetFileName();
            if (fname != null)
                str_stack.Append(fname.Replace(site_root, "").Replace(Path.DirectorySeparatorChar + "App_Code", ""));
            str_stack.Append(':').Append(sf.GetMethod().Name).Append(' ').Append(sf.GetFileLineNumber()).Append(" # ");
        }
        catch (Exception ex)
        {
            str_stack.Append(" ... #" + ex.Message);
        }

        StringBuilder str = new();
        foreach (object dmp_obj in args)
            str.Append(dumper(dmp_obj));

        var strlog = str_prefix + str_stack.ToString() + str.ToString();

        Debug.WriteLine(strlog);

        if (!string.IsNullOrEmpty(log_file))
        {
            try
            {
                rotateIfNeeded();
                if (floggerFS == null)
                {
                    floggerFS = new FileStream(log_file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    floggerSW = new StreamWriter(floggerFS);
                    floggerSW.AutoFlush = true;
                }
                // force seek to end just in case other process added to file
                floggerFS.Seek(0, SeekOrigin.End);
                floggerSW.WriteLine(strlog);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WARN logger can't write to log file. Reason:" + ex.Message);
            }
        }
#if isSentry
        // send to Sentry
        try
        {
            var sentry_str = str.ToString();

            //convert LogLevel to Sentry.SentryLevel and Sentry.BreadcrumbLevel
            Sentry.SentryLevel sentryLevel = Sentry.SentryLevel.Error;
            Sentry.BreadcrumbLevel breadcrumbLevel = Sentry.BreadcrumbLevel.Error;

            if (level == LogLevel.FATAL)
            {
                sentryLevel = Sentry.SentryLevel.Fatal;
                breadcrumbLevel = Sentry.BreadcrumbLevel.Critical;
            }
            else if (level == LogLevel.ERROR)
            {
                sentryLevel = Sentry.SentryLevel.Error;
                breadcrumbLevel = Sentry.BreadcrumbLevel.Error;
            }
            else if (level == LogLevel.WARN)
            {
                sentryLevel = Sentry.SentryLevel.Warning;
                breadcrumbLevel = Sentry.BreadcrumbLevel.Warning;
            }
            else
            {
                sentryLevel = Sentry.SentryLevel.Info;
                breadcrumbLevel = Sentry.BreadcrumbLevel.Info;
            }

            //log to Sentry as separate events only WARN, ERROR, FATAL
            if (level <= LogLevel.WARN)
            {
                if (args.Length > 0 && args[0] is Exception ex)
                {
                    //if first argument is an exception - send it as exception with strlog as an additional info
                    Sentry.SentrySdk.CaptureException(ex, scope =>
                    {
                        scope.Level = sentryLevel;
                        scope.SetExtra("message", sentry_str);
                    });
                }
                else
                    Sentry.SentrySdk.CaptureMessage(sentry_str, sentryLevel);

                //also add as a breadcrumb for the future events
                Sentry.SentrySdk.AddBreadcrumb(str_stack.ToString() + sentry_str, null, null, null, breadcrumbLevel);
            }
            else
            {
                //log to Sentry as breadcrumbs only
                Sentry.SentrySdk.AddBreadcrumb(str_stack.ToString() + sentry_str, null, null, null, breadcrumbLevel);
            }
        }
        catch (Exception)
        {
            // make sure we don't break the app if Sentry fails
        }
#endif
    }

    private void rotateIfNeeded()
    {
        if (log_max_size <= 0 || string.IsNullOrEmpty(log_file)) return;

        try
        {
            floggerFS?.Flush();

            FileInfo fi = new(log_file);
            if (!fi.Exists) return;
            if (fi.Length <= log_max_size) return;

            floggerSW?.Dispose();
            floggerFS?.Dispose();
            floggerSW = null;
            floggerFS = null;

            string to_path = log_file + ".1";
            File.Delete(to_path);
            File.Move(log_file, to_path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("WARN logger can't rotate log file. Reason:" + ex.Message);
        }
    }

    public void Dispose()
    {
        try
        {
            rotateIfNeeded();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("exception in FwLogger.Dispose:" + ex.Message);
        }

        floggerSW?.Dispose();
        floggerFS?.Dispose();
    }
}
