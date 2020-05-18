using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
{
    public class TestController : FwController
    {
        public override Hashtable IndexAction() {
            rw("hello world from controller");
            ArrayList test = new ArrayList();
            test.Add("Test log");
            fw.logger(test);
            return new Hashtable();
        }
    }
}
