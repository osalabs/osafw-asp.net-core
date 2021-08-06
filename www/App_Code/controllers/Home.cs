using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace osafw
{
    public class HomeController : FwController
    {
        public override Hashtable IndexAction() {
            rw("<html><form method=\"POST\" enctype=\"multipart/form-data\" action=\"/Home/Save\"><input type=\"hidden\" name=\"name1\" value=\"testdatatestdatt\"/><input type=\"file\" name=\"file1\"/><input type=\"submit\"/></form></html>");
            //ArrayList users = fw.modelOf(typeof(Users)).list();

            //fw.logger(users);

            fw.logger("Index Action");

            return new Hashtable();
        }

        public Hashtable SaveAction()
        {

            fw.logger("upload Action start");
            String uuid = Utils.uuid();
            bool is_uploaded = false;
            UploadParams up = new UploadParams(fw, "file1", Path.GetTempPath(), uuid, ".txt .xls .xlsm .xlsx");
            is_uploaded = UploadUtils.uploadSimple(up);
            return new Hashtable();
        }
    }
}
