﻿using System;
using System.Collections;

namespace osafw;

public class TestController : FwController
{

    public static new int access_level = Users.ACL_SITEADMIN;

    public Hashtable IndexAction()
    {
        var ps = new Hashtable();
        ps["money"] = 1234.5612;
        ps["success"] = true;
        return new Hashtable { { "_json", ps } };

        //rw("<html><form method=\"POST\" action=\"/Test/(Upload)\"><input type=\"file\" name=\"file1\"/><input type=\"submit\"/></form></html>");
        ////ArrayList users = fw.modelOf(typeof(Users)).list();

        ////fw.logger(users);
        //var ps = new Hashtable();
        //ps["user"] = fw.model<Users>().one(1);

        //return ps;
    }

    public Hashtable UploadAction()
    {

        String uuid = Utils.uuid();
        UploadParams up = new(fw, "file1", Utils.getTmpDir(), uuid, ".xls .xlsm .xlsx");
        //is_uploaded = UploadUtils.uploadSimple(up);

        rw("!@!@!!@!@!@");
        return [];
    }

    private class TActitivityTypes
    {
        public int id { get; set; }
        public int? reply_id { get; set; }
        public int log_types_id { get; set; }
        public int fwentities_id { get; set; }
        public int? item_id { get; set; }
        public DateTime idate { get; set; }
        public int? users_id { get; set; }
        public string idesc { get; set; }
        public string payload { get; set; }
        public byte status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int? upd_users_id { get; set; }

    }

    public void BenchmarkAction()
    {
        DateTime start_time = DateTime.Now;
        for (int i = 0; i < 1000; i++)
        {
            var rows = db.arrayp("select * from activity_logs");
            foreach (var row in rows)
            {
                row["id_str"] = row["id"] + "ok";
            }
        }
        TimeSpan end_timespan = DateTime.Now - start_time;
        rw("benchmark1: " + end_timespan.TotalSeconds);

        start_time = DateTime.Now;
        for (int i = 0; i < 1000; i++)
        {
            var rows = db.array("activity_logs", DB.h());
            foreach (var row in rows)
            {
                row["id_str"] = row["id"] + "ok";
            }
        }
        end_timespan = DateTime.Now - start_time;
        rw("benchmark2: " + end_timespan.TotalSeconds);

        // test with generics
        start_time = DateTime.Now;
        for (int i = 0; i < 1000; i++)
        {
            var rows = db.arrayp<TActitivityTypes>("select * from activity_logs");
            foreach (var row in rows)
            {
                row.idesc = row.idesc + "ok";
            }
        }

        end_timespan = DateTime.Now - start_time;
        rw("benchmark3: " + end_timespan.TotalSeconds);


        //rw(FW.dumper(rows));

        rw("done");
    }

    public void BooleanAction()
    {
        string[] values = [ null, String.Empty, "True", "False",
                      "true", "false", "    true    ", "0",
                      "1", "-1", "string" ];
        foreach (var value in values)
        {
            bool flag;
            //if (Boolean.TryParse(value, out flag
            flag = value.toBool();
            rw(value + " --> " + flag);
            //if (flag)
            //    rw(value + " --> "+ flag);
            //else
            //    rw("Unable to parse '"+value??"<null>"+"'.");
        }
    }

    public Hashtable JsonAction()
    {
        var ps = new Hashtable();
        ps["success"] = true;
        ps["message"] = "This is Json!";
        return new Hashtable { { "_json", ps } };
    }

    public Hashtable ExceptionAction()
    {
        throw new Exception("Test exception");
    }


    public void DBGenericsAction()
    {
        //var item = fw.model<Demos>().one(1);
        //logger(item);
        var result = fw.model<Demos>().calcTotal(1);
        rw("result:" + result);
        rw("done");
    }
}
