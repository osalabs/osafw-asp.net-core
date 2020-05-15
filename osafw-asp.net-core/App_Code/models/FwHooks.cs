using osafw_asp_net_core.fw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp_net_core.models
{
    public class FwHooks
    {
        // called from FW.run before request dispatch
        public static void initRequest(FW fw) {
            //Dim main_menu As ArrayList = FwCache.get_value("main_menu")

            //If IsNothing(main_menu) OrElse main_menu.Count = 0 Then
            //    'create main menu if not yet
            //    main_menu = fw.model(Of Settings).get_main_menu()
            //    FwCache.set_value("main_menu", main_menu)
            //End If

            //fw.G("main_menu") = main_menu

            //also force set XSS
            /*if (fw.SESSION("XSS") == "") {
                fw.SESSION("XSS", Utils.getRandStr(16));
            }
            if (fw.model(Of Users).meId() > 0 Then fw.model(Of Users).loadMenuItems();*/
        }
    }
}
