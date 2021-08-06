using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw
{
    public class FwHooks
    {
        // called from FW.run before request dispatch
        public static void initRequest(FW fw) {
            ArrayList main_menu = FwCache.getValue("main_menu") as ArrayList;

            /*if (main_menu == null || main_menu.Count == 0)
            {
                // create main menu if not yet
                main_menu = (fw.modelOf(typeof(FwS)) as FwSettings).get_main_menu();
                FwCache.setValue("main_menu", main_menu);
            }*/

            //fw.G("main_menu") = main_menu

            //also force set XSS
            /*if (fw.SESSION("XSS") == "") {
                fw.SESSION("XSS", Utils.getRandStr(16));
            }
            if (fw.model(Of Users).meId() > 0 Then fw.model(Of Users).loadMenuItems();*/
        }
    }
}
