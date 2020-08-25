using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
{
    public class TestController : FwController
    {
        public override Hashtable IndexAction() {
            rw("<html><form method=\"POST\" action=\"/Test/(Upload)\"><input type=\"file\" name=\"file1\"/><input type=\"submit\"/></form></html>");
            //ArrayList users = fw.modelOf(typeof(Users)).list();
            
            //fw.logger(users);


            return new Hashtable();
        }

        public Hashtable UploadAction()
        {

            String uuid = Utils.uuid();
            bool is_uploaded = false;
            UploadParams up = new UploadParams(fw, "file1", Path.GetTempPath(), uuid, ".xls .xlsm .xlsx");
            //is_uploaded = UploadUtils.uploadSimple(up);

            rw("!@!@!!@!@!@");
            ArrayList users = fw.modelOf(typeof(Users)).list();
            return new Hashtable();
        }
    }
}
