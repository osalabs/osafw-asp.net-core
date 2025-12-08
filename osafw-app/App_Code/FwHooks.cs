// Fw Hooks class
// global framework hooks can be set here
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net-core
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

namespace osafw;

public sealed class FwHooks
{

    // called from FW.run before request dispatch
    public static void initRequest(FW fw)
    {
        // var mainMenu = FwCache.get_value("main_menu") as ArrayList;
        //
        // if (mainMenu == null || mainMenu.Count == 0)
        // {
        //     // create main menu if not yet
        //     mainMenu = fw.model<Settings>().get_main_menu();
        //     FwCache.set_value("main_menu", mainMenu);
        // }
        //
        // fw.G("main_menu", mainMenu);

        // if user not logged - check permanent cookie and auto login user
        if (fw.userId == 0)
            fw.model<Users>().checkPermanentLogin();

        // also force set XSS
        if (string.IsNullOrEmpty(fw.Session("XSS"))) fw.Session("XSS", Utils.getRandStr(16));
        if (fw.userId > 0) fw.model<Users>().loadMenuItems();
        //fw.model<Users>().loadRBACMenu(); // uncomment if RBAC
    }

    // called from FW.run before fw.Finalize()
    public static void finalizeRequest(FW fw)
    {
    }
}
