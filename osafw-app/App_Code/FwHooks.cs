// Fw Hooks class
// global framework hooks can be set here
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net-core
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

namespace osafw;

public sealed class FwHooks
{
    /// <summary>
    /// Applies application-owned defaults before deployment configuration is loaded.
    /// </summary>
    /// <remarks>
    /// This hook must stay pure and DB-independent because startup, requests and offline
    /// commands all use it. Assign fresh dictionaries so cached configuration buckets do
    /// not share mutable application policy state.
    /// </remarks>
    public static void configure(FwDict defaults)
    {
        // Keys: resolved route prefixes without slashes (e.g. v1); values: true placeholders (presence only). Skips the framework XSS/CSRF token check.
        defaults["no_xss_prefixes"] = new FwDict
        {
            ["v1"] = true,
        };
        // Keys: resolved controller names without the Controller suffix; values: true placeholders (presence only). Skips the framework XSS/CSRF token check.
        defaults["no_xss"] = new FwDict
        {
            ["Login"] = true,
        };
        // Keys: URL prefixes with a leading slash (e.g. /Admin); values: true placeholders (presence only). Selects controller and template namespaces.
        defaults["route_prefixes"] = new FwDict
        {
            ["/Sys"] = true,
            ["/Admin"] = true,
            ["/My"] = true,
            ["/Dev"] = true,
        };
        // Keys: exact URL paths; values: /path or Controller::Action, optionally prefixed by an HTTP method (e.g. DELETE /Login/1). Rewrites dispatch.
        defaults["routes"] = new FwDict
        {
            ["/Logoff"] = "DELETE /Login/1",
        };

        // Boolean: true enables public registration through SignupController; false disables it.
        defaults["IS_SIGNUP"] = false;
        // One exact additional API origin (scheme://host[:port], no trailing slash); empty allows only the built-in same-host origins.
        defaults["API_ALLOW_ORIGIN"] = "";
        // TimeZoneInfo ID (e.g. Central Standard Time): default timezone for user date/time display and input.
        defaults["timezone"] = "Central Standard Time";
    }

    // called from FW.run before request dispatch
    public static void initRequest(FW fw)
    {
        // var mainMenu = FwCache.get_value("main_menu") as FwList;
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
