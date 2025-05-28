using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace osafw;

public class FwLogger : IDisposable
{
    public LogLevel log_level;
    public string log_file = "";
    public string site_root = "";
    public long log_max_size = 0;

    private FileStream? floggerFS;
    private StreamWriter? floggerSW;

    public FwLogger() { }

    public FwLogger(LogLevel log_level, string log_file, string site_root, long log_max_size = 0)
    {
        this.log_level = log_level;
        this.log_file = log_file;
        this.site_root = site_root;
        this.log_max_size = log_max_size;
    }

    public void Log(params object[] args)
    {
        if (args.Length == 0) return;
        Log(LogLevel.DEBUG, ref args);
    }

    public void Log(LogLevel level, params object[] args)
    {
        if (args.Length == 0) return;
        Log(level, ref args);
    }

    public void Log(LogLevel level, ref object[] args)
    {
        if (level > log_level) return;

        StringBuilder str_prefix = new(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        str_prefix.Append(' ').Append(level.ToString()).Append(' ');
        str_prefix.Append(Environment.ProcessId).Append(' ');

        StringBuilder str_stack = new();
        StackTrace st = new(true);

        try
        {
            int i = 1;
            StackFrame sf = st.GetFrame(i);
            string fname = sf.GetFileName() ?? "";
            while (sf.GetMethod().Name == "logger" || fname.EndsWith(Path.DirectorySeparatorChar + "DB.vb"))
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
            str.Append(FW.dumper(dmp_obj));

        var strlog = str_prefix + str_stack.ToString() + str.ToString();

        Debug.WriteLine(strlog);

        if (!string.IsNullOrEmpty(log_file))
        {
            try
            {
                RotateIfNeeded();
                if (floggerFS == null)
                {
                    floggerFS = new FileStream(log_file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    floggerSW = new StreamWriter(floggerFS);
                    floggerSW.AutoFlush = true;
                }
                floggerFS.Seek(0, SeekOrigin.End);
                floggerSW.WriteLine(strlog);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WARN logger can't write to log file. Reason:" + ex.Message);
            }
        }
#if isSentry
        try
        {
            var sentry_str = str.ToString();

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

            if (level <= LogLevel.WARN)
            {
                if (args.Length > 0 && args[0] is Exception ex)
                {
                    Sentry.SentrySdk.CaptureException(ex, scope =>
                    {
                        scope.Level = sentryLevel;
                        scope.SetExtra("message", sentry_str);
                    });
                }
                else
                    Sentry.SentrySdk.CaptureMessage(sentry_str, sentryLevel);

                Sentry.SentrySdk.AddBreadcrumb(str_stack.ToString() + sentry_str, null, null, null, breadcrumbLevel);
            }
            else
            {
                Sentry.SentrySdk.AddBreadcrumb(str_stack.ToString() + sentry_str, null, null, null, breadcrumbLevel);
            }
        }
        catch (Exception)
        {
        }
#endif
    }

    private void RotateIfNeeded()
    {
        if (log_max_size <= 0 || string.IsNullOrEmpty(log_file)) return;

        try
        {
            if (floggerFS != null)
                floggerFS.Flush();

            FileInfo fi = new FileInfo(log_file);
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
            RotateIfNeeded();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("exception in FwLogger.Dispose:" + ex.Message);
        }

        floggerSW?.Dispose();
        floggerFS?.Dispose();
    }
}
