// Configuration check controller for Developers
//  - perform basic testing of configuration
//  WARNING: better to remove this file on production
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021  Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.IO;

namespace osafw
{
    public class DevConfigureController : FwController
    {
        public static new int access_level = Users.ACL_VISITOR;

        protected DemoDicts model;

        public Hashtable IndexAction()
        {
            Hashtable ps = new();

            ps["hide_sidebar"] = true;
            ps["config_file_name"] = fw.config("config_override");

            ps["is_db_config"] = false;
            var configdb = (Hashtable)fw.config("db");
            if (configdb != null && configdb["main"] != null && !string.IsNullOrEmpty((string)((Hashtable)configdb["main"])["connection_string"]))
                ps["is_db_config"] = true;

            DB db;
            ps["is_db_conn"] = false;
            ps["is_db_tables"] = false;
            if ((bool)ps["is_db_config"])
            {
                try
                {
                    db = new DB(fw);
                    db.connect();
                    ps["is_db_conn"] = true;

                    try
                    {
                        var value = db.value("menu_items", new Hashtable(), "count(*)"); // just a last table in database.sql script
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
            if (isWritable(fw.config("template") + "/lang", true) && !Utils.f2bool(fw.config("IS_DEV")))
                ps["is_write_langok"] = false;

            // obsolete in .net 4
            // If System.Security.SecurityManager.IsGranted(writePermission) Then ps["is_write_dirs") ] True

            ps["is_error_log"] = false;
            ps["is_error_log"] = isWritable((string)fw.config("log"));

            ps["error_log_size"] = Utils.bytes2str(Utils.fileSize((string)fw.config("log")));

            return ps;
        }

        private static bool isWritable(string dir_or_filepath, bool is_dir = false)
        {
            var result = false;
            try
            {
                var path = dir_or_filepath + (is_dir ? "": "osafw_writable_check.txt");
                string V = "osafw";
                FW.setFileContent(path, ref V);
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

    }

}