// Configuration check controller for Developers
//  - perform basic testing of configuration
//  WARNING: better to remove this file on production
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021  Oleg Savchuk www.osalabs.com

using System;
using System.IO;

namespace osafw;

public class DevConfigureController : FwController
{
    public static new int access_level = Users.ACL_VISITOR;

    public override void init(FW fw)
    {
        //base.init(fw); //not using base init as it calls getRBAC which require access to db (and we may not have it yet)
        this.fw = fw;
        this.db = fw.db;

        base_url = "/Dev/Configure"; // base url for the controller
    }

    public override void checkAccess()
    {
        //true - allow access to all, including visitors
    }

    public FwDict IndexAction()
    {
        FwDict ps = [];

        ps["hide_sidebar"] = true;
        ps["ASPNETCORE_ENVIRONMENT"] = FwConfig.getEnvironmentName();
        ps["config_file_name"] = fw.config("config_override");
        // Named overrides are optional and can have a different name from the runtime environment.
        ps["is_config_env"] = true;

        ps["is_db_config"] = false;
        var configdb = (FwDict?)fw.config("db");
        if (configdb?["main"] is FwDict mainDb
            && (!Utils.isEmpty(mainDb["connection_string"]) || mainDb.ContainsKey("connection_string_secret")))
            ps["is_db_config"] = true;

        DB db;
        ps["is_db_conn"] = false;
        ps["is_db_tz"] = false;
        ps["is_db_tables"] = false;
        if (ps["is_db_config"].toBool())
        {
            try
            {
                db = fw.getDB();
                db.connect();
                ps["is_db_conn"] = true;
                ps["db_timezone_id"] = db.getTimezoneId();
                ps["is_db_tz"] = db.isTimezoneDetectionOk();

                try
                {
                    var value = db.value("menu_items", [], "count(*)"); // just a last table in database.sql script
                    ps["is_db_tables"] = true;
                }
                catch (Exception ex)
                {
                    logger(LogLevel.WARN, "DevConfigure DB table check failed:", ex.Message);
                }
            }
            catch (Exception ex)
            {
                logger(LogLevel.WARN, "DevConfigure DB connection check failed:", ex.Message);
            }
        }

        ps["is_write_dirs"] = false;
        string upload_dir = fw.config("site_root").toStr() + fw.config("UPLOAD_DIR").toStr();
        // check if dir is writable
        ps["is_write_dirs"] = isWritable(upload_dir, true);

        ps["is_write_langok"] = true;
        if (isWritable(fw.config("template") + "/lang", true) && !fw.config("IS_DEV").toBool())
            ps["is_write_langok"] = false;

        // obsolete in .net 4
        // If System.Security.SecurityManager.IsGranted(writePermission) Then ps["is_write_dirs") ] True

        var log_path = fw.config("log").toStr();
        ps["is_error_log"] = isWritable(log_path);

        ps["error_log_size"] = Utils.fileSize(log_path);

        return ps;
    }

    private static bool isWritable(string dir_or_filepath, bool is_dir = false)
    {
        var result = false;
        try
        {
            var path = dir_or_filepath + (is_dir ? "" : "osafw_writable_check.txt");
            string V = "osafw";
            Utils.setFileContent(path, ref V);
            File.Delete(path);
            result = true;
        }
        catch (Exception)
        {
            //can't write
            //logger(ex.Message);
        }
        return result;
    }

    /// <summary>
    /// Initializes a development database from the bundled SQL scripts so a fresh clone can run the demo app.
    /// </summary>
    /// <returns>
    /// Template data containing the generated admin password when one or more SQL statements executed.
    /// </returns>
    public FwDict InitDBAction()
    {
        if (!fw.config("IS_DEV").toBool())
            throw new AuthException("Not in a DEV mode");

        enforcePost();

        return DevCli.initializeDatabase(fw);
    }

    public FwDict? PendingUpdatesAction()
    {
        var pendingRows = fw.model<FwUpdates>().listPending();
        fw.Session("FW_UPDATES_CTR", pendingRows.Count.ToString());

        var adminUrl = "/Admin/FwUpdates?dofilter=1&f[status]=0";
        var adminLink = fw.isLogged
            ? adminUrl
            : "/Login?gourl=" + Utils.urlescape(adminUrl);

        fw.rw("<b>Pending framework updates</b>");
        fw.rw("<p>Framework database updates are pending. Review and apply them from Admin FwUpdates.</p>");
        foreach (var row in pendingRows)
        {
            var iname = Utils.htmlescape(row["iname"].toStr());
            fw.rw("&nbsp;&#183;&nbsp;" + iname);
        }
        fw.rw("<p><a href='/'>Home</a> or <a href='" + Utils.htmlescape(adminLink) + "'>Admin FwUpdates</a></p>");
        return null;
    }
}
