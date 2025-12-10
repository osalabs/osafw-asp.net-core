// Windows Authenticaiton Login controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

// To enable Windows Authenticaiton support:
// install Microsoft.AspNetCore.Authentication.Negotiate (uncomment in csproj)
// uncomment in Startup.cs
// check HttpMiddleware.cs
// uncomment windows login button in template/index/form.html

using System;
using System.Collections;
using System.Text.RegularExpressions;

namespace osafw;

public class WinLoginController : FwController
{
    protected Users model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<Users>();
        model.init(fw);
        db = model.getDB();
    }

    public override void checkAccess()
    {
        //true - allow access to all, including visitors
    }

    public void IndexAction()
    {
        string username = "";
        var identity = fw.context?.User?.Identity;
        bool isAuthenticated = identity?.IsAuthenticated ?? false;
        string authUser = identity?.Name ?? string.Empty;
        logger(LogLevel.INFO, "Win Login isAuthenticated=", isAuthenticated);
        logger(LogLevel.INFO, "Win Login authUser=", authUser);

        if (!isAuthenticated || String.IsNullOrEmpty(authUser))
        {
            logger(LogLevel.INFO, "Win Login not authenticated or empty user");
            //fw.flash("error", "Windows Authentication is not available, please login with email/password");
            fw.redirect("/Login");
        }

        var m = Regex.Match(authUser, @"\\(.+)$"); // extract name without domain
        if (m.Success)
            username = m.Groups[1].Value;

        if (String.IsNullOrEmpty(username))
        {
            fw.flash("error", "Windows Authentication is not available, please login with email/password");
            fw.redirect("/Login");
        }
        else
        {
            // if we have username - it means it's authenticated
            // try to find such a user
            var user = fw.model<Users>().oneByLogin(username);
            int usersId = 0;
            if (user.Count > 0)
            {
                if (user["status"].toInt() != Users.STATUS_ACTIVE)
                {
                    fw.flash("error", "Login disabled, please contact Site Administrator");
                    fw.redirect("/Login");
                }
                usersId = user["id"].toInt();
            }
            else
            {
                try
                {
                    // if user not found - add it with minimum level
                    usersId = fw.model<Users>().add(new FwRow {
                        {"email",  username+"@company.tld"},
                        {"access_level", Users.ACL_MEMBER},
                        {"login", username},
                        {"fname", username},
                        {"status", FwModel.STATUS_ACTIVE}
                    });
                }
                catch (Exception ex)
                {
                    logger(LogLevel.ERROR, "Exception when tried to add new Windows user", ex.Message);
                    //if any error happens - redirect to login page
                    fw.flash("error", "Cannot login with Windows Login username (" + authUser + "), please login with email/password");
                    fw.redirect("/Login");
                }
            }
            model.doLogin(usersId);
            fw.redirect(fw.config("LOGGED_DEFAULT_URL").toStr());
        }
    }

    // for debug
    //public void DumpAction()
    //{
    //    rw("IsAuthenticated:" + fw.context.User.Identity.IsAuthenticated);
    //    rw("Name:" + fw.context.User.Identity.Name);
    //    rw("-----");
    //    rw("FORM:");
    //    rw(FW.dumper(fw.FORM));

    //    rw("SERVER:");
    //    var server_vars = fw.context.Features.Get<Microsoft.AspNetCore.Http.Features.IServerVariablesFeature>();
    //    var v = Utils.qw("ALL_RAW AUTH_TYPE AUTH_USER LOGON_USER");
    //    foreach (var str in v)
    //    {
    //        rw(str + " = " + server_vars[str]);
    //    }
    //    rw("done");
    //}
}
