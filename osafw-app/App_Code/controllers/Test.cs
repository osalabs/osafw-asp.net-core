using System;
using System.Collections;

namespace osafw;

public class TestController : FwController
{
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
        return new Hashtable();
    }

    public void BenchmarkAction()
    {
        DateTime start_time = DateTime.Now;
        for (int i = 0; i < 1000; i++)
        {
            var rows = db.arrayp("select * from event_log");
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
            var rows = db.array("event_log", DB.h());
            foreach (var row in rows)
            {
                row["id_str"] = row["id"] + "ok";
            }
        }
        end_timespan = DateTime.Now - start_time;
        rw("benchmark2: " + end_timespan.TotalSeconds);

        //rw(FW.dumper(rows));

        rw("done");
    }

    public void BooleanAction()
    {
        string[] values = { null, String.Empty, "True", "False",
                      "true", "false", "    true    ", "0",
                      "1", "-1", "string" };
        foreach (var value in values)
        {
            bool flag;
            //if (Boolean.TryParse(value, out flag
            flag = Utils.toBool(value);
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

    public void ReportAction()
    {
        var repcode = "sample";
        var filters = new Hashtable();
        filters["format"] = "html";

        //var report = FwReports.createInstance(fw, repcode, filters);
        //report.setFilters(); // set filters data like select/lookups
        //report.getData();
        //// show or output report according format
        ////var ps = new Hashtable();
        //report.render();

        var html = FwReports.createHtml(fw, repcode);
        rw(html);
    }
}
