using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
{
    public class FormUtils
    {
        // arr is ArrayList of Hashes with "id" and "iname" keys, for example rows returned from db.array('select id, iname from...')
        // "id" key is optional, if not present - iname will be used for values too
        // isel may contain multiple comma-separated values
        public static String selectOptions(ArrayList arr, String isel, bool is_multi = false)
        {
            return "";
        }
    }
}
