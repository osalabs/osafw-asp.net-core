// TODO rework logger into this separate class properly

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace osafw
{
    public class FwLogger
    {
        public LogLevel log_level;
        public string log_file;
        public string site_root;

        private System.IO.FileStream floggerFS;
        private System.IO.StreamWriter floggerSW;

        //TODO constructor setting log_level, site_root, log_file
        // this.log_level = (LogLevel)Enum.Parse(typeof(LogLevel), log_level)
        /*
         *<summary>
         * Logger levels, ex: logger(LogLevel.ERROR, "Something happened")
         * </summary>
         */
        public enum LogLevel : int {
            OFF = 0,             // no logging occurs
            FATAL = 1,           // severe error, current request (or even whole application) aborted (notify admin)
            ERROR = 2,           // error happened, but current request might still continue (notify admin)
            WARN = 3,            // potentially harmful situations for further investigation, request processing continues
            INFO = 4,            // default for production (easier maintenance/support), progress of the application at coarse-grained level (fw request processing: request start/end, sql, route/external redirects, sql, fileaccess, third-party API)
            DEBUG = 5,           // default for development (default for logger("msg") call), fine-grained level
            TRACE = 6,           // very detailed dumps (in-module details like fw core, despatcher, parse page, ...)
            ALL = 7              // just log everything
        }
        // internal logger routine, just to avoid pass args by value 2 times
        public void logger(LogLevel level, ref Object[] args) {
            // skip logging if requested level more than config's debug level
            if (level > log_level) return;

            StringBuilder str = new StringBuilder(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            str.Append(" ").Append(level.ToString()).Append(" ");
            str.Append(System.Diagnostics.Process.GetCurrentProcess().Id).Append(" ");
            StackTrace st = new System.Diagnostics.StackTrace(true);

            try
            {
                int i = 1;
                StackFrame sf = st.GetFrame(i);
                // skip logger methods and DB internals as we want to know line where logged thing actually called from
                String fname = sf.GetFileName();
                String method_name = sf.GetMethod().Name;
                while (method_name == "logger" || fname.EndsWith("\\DB.vb")) {
                    i += 1;
                    sf = st.GetFrame(i);
                    method_name = sf.GetMethod().Name;
                    fname = sf.GetFileName();
                }
                fname = sf.GetFileName();
                if (fname != null) { // nothing in Release configuration
                    fname = fname.Replace(this.site_root, "");
                    fname = fname.Replace("\\App_Code", "");
                    str.Append(fname);
                }
                str.Append(':').Append(method_name).Append(' ').Append(sf.GetFileLineNumber().ToString()).Append(" # ");
            }
            catch (Exception ex)
            {
                str.Append(" ... #");
            }

            foreach (Object dmp_obj in args)
            {
                str.Append(FW.dumper(dmp_obj));
            }

            // write to debug console first
            System.Diagnostics.Debug.WriteLine(str);

            // write to log file
            if (!string.IsNullOrEmpty(this.log_file))
            {
                try
                {
                    // keep log file open to avoid overhead
                    if (floggerFS == null)
                    {
                        // open log with shared read/write so loggers from other processes can still write to it
                        floggerFS = new FileStream(log_file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                        floggerSW = new System.IO.StreamWriter(floggerFS);
                        floggerSW.AutoFlush = true;
                    }
                    // force seek to end just in case other process added to file
                    floggerFS.Seek(0, SeekOrigin.End);
                    floggerSW.WriteLine(str.ToString());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("WARN logger can't write to log file. Reason:" + ex.Message);
                }
            }

        }
    }
}
