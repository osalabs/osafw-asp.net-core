using osafw_asp_net_core.fw;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp_net_core.controllers
{
    public class TestController : FwController
    {
        public override Hashtable IndexAction() {
            rw("hello world from controller");
            return new Hashtable();
        }
    }
}
