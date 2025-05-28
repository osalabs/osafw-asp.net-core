// Configuration check controller for Developers
//  - perform basic testing of configuration
//  WARNING: better to remove this file on production
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021  Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.IO;

namespace osafw;

public class DevConfigureController : FwController
{
    public static new int access_level = Users.ACL_VISITOR;

    protected DemoDicts model;

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

    public Hashtable IndexAction()
    {
        Hashtable ps = [];

        ps["hide_sidebar"] = true;
        var aspnet_env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        ps["ASPNETCORE_ENVIRONMENT"] = aspnet_env;
        ps["config_file_name"] = fw.config("config_override");
        ps["is_config_env"] = String.IsNullOrEmpty(aspnet_env) || aspnet_env == ps["config_file_name"].toStr();

        ps["is_db_config"] = false;
        var configdb = (Hashtable)fw.config("db");
        if (configdb != null && configdb["main"] != null && !Utils.isEmpty(((Hashtable)configdb["main"])["connection_string"]))
            ps["is_db_config"] = true;

        DB db;
        ps["is_db_conn"] = false;
        ps["is_db_tables"] = false;
        if ((bool)ps["is_db_config"])
        {
            try
            {
                db = fw.getDB();
                db.connect();
                ps["is_db_conn"] = true;

                try
                {
                    var value = db.value("menu_items", [], "count(*)"); // just a last table in database.sql script
                    ps["is_db_tables"] = true;
                }
                catch (Exception ex)
                {
                    ps["db_tables_err"] = ex.Message;
                }
            }
            catch (Exception ex)
            {
                ps["db_conn_err"] = ex.Message;
            }
        }

        ps["is_write_dirs"] = false;
        string upload_dir = (string)fw.config("site_root") + fw.config("UPLOAD_DIR");
        // check if dir is writable
        ps["is_write_dirs"] = isWritable(upload_dir, true);

        ps["is_write_langok"] = true;
        if (isWritable(fw.config("template") + "/lang", true) && !fw.config("IS_DEV").toBool())
            ps["is_write_langok"] = false;

        // obsolete in .net 4
        // If System.Security.SecurityManager.IsGranted(writePermission) Then ps["is_write_dirs") ] True

        var log_path = (string)fw.config("log");
        ps["is_error_log"] = isWritable(log_path);

        ps["error_log_size"] = Utils.bytes2str(Utils.fileSize(log_path));

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

    public Hashtable InitDBAction()
    {
        if (!fw.config("IS_DEV").toBool())
            throw new AuthException("Not in a DEV mode");

        Hashtable ps = [];
        int sql_ctr = 0;
        string[] files = ["fwdatabase.sql", "database.sql", "lookups.sql", "views.sql"];
        foreach (string file in files)
        {
            var sql_file = fw.config("site_root") + @"\App_Data\sql\" + file;
            logger("Checking sql file:", sql_file);
            if (File.Exists(sql_file))
            {
                logger("Executing sql file:", sql_file);
                sql_ctr += db.execMultipleSQL(Utils.getFileContent(sql_file));
            }
        }

        logger("Executed SQL count:", sql_ctr);
        if (sql_ctr > 0)
        {
            //if some scripts executed generate admin password
            var pwd = Utils.getRandStr(8);
            fw.model<Users>().update(1, DB.h("pwd", pwd));
            ps["pwd"] = pwd;
        }

        return ps;
    }

    public Hashtable ApplyUpdatesAction()
    {
        if (!fw.config("IS_DEV").toBool())
            throw new AuthException("Not in a DEV mode");

        fw.model<FwUpdates>().loadUpdates();

        // apply updates - if any and echo results. If error happens we stay on this page
        try
        {
            fw.model<FwUpdates>().applyPending(true);
        }
        catch (Exception ex)
        {
            fw.rw("Error: " + ex.Message);
            fw.rw("");
            fw.rw("<b>Press F5 to continue applying updates</b><br>");
            fw.rw("or go to <a href='/Admin/FwUpdates'>Admin FwUpdates</a>");
            fw.rw("or go to <a href='/Login'>Login</a><br>");
            return null;
        }

        // all success - show link back to home
        fw.rw("All updates applied successfully. <a href='/'>Back to Home</a>");
        return null;
    }
}
