using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace osafw
{
    public class TestController : FwController
    {
        public Hashtable IndexAction()
        {
            rw("<html><form method=\"POST\" action=\"/Test/(Upload)\"><input type=\"file\" name=\"file1\"/><input type=\"submit\"/></form></html>");
            //ArrayList users = fw.modelOf(typeof(Users)).list();

            //fw.logger(users);


            return new Hashtable();
        }

        public Hashtable UploadAction()
        {

            String uuid = Utils.uuid();
            bool is_uploaded = false;
            UploadParams up = new (fw, "file1", Path.GetTempPath(), uuid, ".xls .xlsm .xlsx");
            //is_uploaded = UploadUtils.uploadSimple(up);

            rw("!@!@!!@!@!@");
            ArrayList users = fw.model<Users>().list();
            return new Hashtable();
        }

        public void BenchmarkAction()
        {
            DateTime start_time = DateTime.Now;
            for (int i = 0; i < 1000; i++)
            {
                var rows = db.arrayp("select * from event_log", DB.h()).toArrayList();
                foreach (Hashtable row in rows)
                {
                    row["id_str"] = row["id"] + "ok";
                }
            }
            TimeSpan end_timespan = DateTime.Now - start_time;
            rw("benchmark1: " + end_timespan.TotalSeconds);

            start_time = DateTime.Now;
            for (int i = 0; i < 1000; i++)
            {
                var rows = db.array("select * from event_log");
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
                flag = Utils.f2bool(value);
                rw(value + " --> " + flag);
                //if (flag)
                //    rw(value + " --> "+ flag);
                //else
                //    rw("Unable to parse '"+value??"<null>"+"'.");
            }
        }

    }
}
