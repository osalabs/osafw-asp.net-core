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
            ArrayList users = fw.modelOf(typeof(Users)).list();
            
            fw.logger(users);
            return new Hashtable();
        }
    }
}
