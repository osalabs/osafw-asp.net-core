// Direct DB Access Controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace osafw;

public class AdminDBController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    private const string dbpwd = "db321";

    public FwDict IndexAction()
    {
        FwDict ps = [];
        var selected_db = reqs("db", "main");

        string sql = reqs("sql");
        FwList tablehead = [];
        FwList tablerows = [];
        int sql_ctr = 0;
        long sql_time = DateTime.Now.Ticks;

        try
        {
            if (selected_db.Length > 0)
            {
                logger("CONNECT TO", selected_db);
                db = fw.getDB(selected_db);
            }

            if (fw.SessionBool("admindb_pwd_checked") || reqs("pwd") == dbpwd)
                fw.SessionBool("admindb_pwd_checked", true);
            else if (sql.Length > 0)
                fw.setGlobalError("Wrong password");
            if (sql.Length > 0 && fw.SessionBool("admindb_pwd_checked"))
            {
                if (sql == "show tables")
                    // special case - show tables
                    show_tables(ref tablehead, ref tablerows);
                else
                {
                    // launch the query
                    string sql1 = strip_comments(sql);
                    String[] asql = DB.splitMultiSQL(sql);
                    foreach (string sqlone1 in asql)
                    {
                        var sqlone = sqlone1.Trim();
                        if (sqlone.Length > 0)
                        {
                            DbDataReader sth = db.query(sqlone);
                            tablehead = sth2head(sth);
                            tablerows = sth2table(sth);
                            db.closeQuery(sth);
                            sql_ctr += 1;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            fw.setGlobalError("Error occured: " + ex.Message);
        }

        FwList dbsources = [];
        var dbConfig = fw.config("db") as FwDict ?? [];
        foreach (string dbname in dbConfig.Keys)
            dbsources.Add(new FwDict()
            {
                {"id",dbname},
                {"iname",dbname},
                {"is_checked",dbname == selected_db}
            });

        ps["dbsources"] = dbsources;
        ps["selected_db"] = selected_db;
        ps["sql"] = sql;
        ps["sql_ctr"] = sql_ctr;
        ps["sql_time"] = (DateTime.Now.Ticks - sql_time) / (double)10 / 1000 / 1000; // 100nano/micro/milliseconds/seconds
        ps["head_fields"] = tablehead;
        ps["rows"] = tablerows;
        if (tablerows.Count > 0 || tablehead.Count > 0)
            ps["is_results"] = true;
        return ps;
    }

    public void SaveAction()
    {
        fw.routeRedirect(FW.ACTION_INDEX);
    }

    private static FwList sth2table(DbDataReader sth)
    {
        FwList result = [];
        if (sth == null || !sth.HasRows)
            return result;        

        while (sth.Read())
        {
            FwDict tblrow = [];
            var fields = new FwList();
            tblrow["fields"] = fields;

            for (int i = 0; i <= sth.FieldCount - 1; i++)
            {
                FwDict tblfld = [];
                tblfld["value"] = sth[i].toStr();

                fields.Add(tblfld);
            }
            result.Add(tblrow);
        }

        return result;
    }

    private static FwList sth2head(DbDataReader sth)
    {
        FwList result = [];
        if (sth == null)
            return result;        

        for (int i = 0; i <= sth.FieldCount - 1; i++)
        {
            FwDict tblfld = [];
            tblfld["field_name"] = sth.GetName(i);

            result.Add(tblfld);
        }

        return result;
    }

    private void show_tables(ref FwList tablehead, ref FwList tablerows)
    {
        tablehead = [];
        FwDict h = [];
        h["field_name"] = "Table";
        tablehead.Add(h);
        h = [];
        h["field_name"] = "Row Count";
        tablehead.Add(h);

        tablerows = [];

        DbConnection conn = db.connect();
        DataTable dataTable = conn.GetSchema("Tables");
        foreach (DataRow row in dataTable.Rows)
        {
            string tblname = row["TABLE_NAME"].toStr();
            if (tblname.StartsWith("MSys"))
                continue;

            FwDict tblrow = [];
            var fields = new FwList();
            tblrow["fields"] = fields;

            FwDict tblfld = [];
            tblfld["db"] = db.db_name;
            tblfld["value"] = tblname;
            tblfld["is_select_link"] = true;
            fields.Add(tblfld);

            tblfld = [];
            tblfld["value"] = get_tbl_count(tblname);
            fields.Add(tblfld);

            tblrow["db"] = db.db_name;
            tablerows.Add(tblrow);

        }
    }

    private long get_tbl_count(string tblname)
    {
        long result = -1;
        try
        {
            result = db.value(tblname, [], "count(*)").toLong();
        }
        catch (Exception)
        {
        }

        return result;
    }

    private static string strip_comments(string sql)
    {
        return Regex.Replace(sql, @"/\*.+?\*/", " ", RegexOptions.Singleline);
    }

}