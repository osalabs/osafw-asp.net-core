// MyMFA controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;
using System.Collections.Generic;

namespace osafw;

public class MyMFAController : FwController
{
    public static new int access_level = Users.ACL_VISITOR;

    protected Users model = new();
    protected int user_id = 0;

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);

        base_url = "/My/MFA";

        is_readonly = false;//allow update my stuff

        user_id = fw.userId;
        if (user_id == 0)
        {
            //if user not logged in - we are in setup mode after login
            user_id = fw.Session("mfa_login_users_id").toInt();
            if (user_id == 0)
                fw.redirect("/"); // no user - go to home
        }
    }

    public void IndexAction()
    {
        fw.redirect(base_url + "/new");
    }

    public Hashtable ShowFormAction()
    {
        var user = model.one(user_id);

        //generate secret and save to session only (will be saved to db after validation)
        var secret = model.generateMFASecret();
        fw.Session("mfa_secret", secret);

        Hashtable ps = [];
        ps["qr_code"] = model.generateMFAQRCode(secret, user["email"], (string)fw.config("SITE_NAME"));
        return ps;
    }

    public Hashtable SaveAction()
    {
        route_onerror = FW.ACTION_SHOW_FORM_NEW; //set route to go if error happens
        checkXSS();

        if (string.IsNullOrEmpty(fw.Session("mfa_secret")))
            fw.redirect(base_url); //no code generated yet

        string mfa_code = reqs("mfa_code");

        if (!model.isValidMFACode(fw.Session("mfa_secret"), mfa_code))
            throw new UserException("MFA Code is not valid");

        // code is valid, generate recovery codes and save
        // generate 5 recovery codes as random 8-digit numbers using Utils.getRandStr(8) and concatenate into comma-separated string
        var hashed_codes = new List<string>(5);
        ArrayList recovery_codes = [];
        for (int i = 0; i < 5; i++)
        {
            var code = Utils.getRandStr(8);
            hashed_codes.Add(model.hashPwd(code));
            recovery_codes.Add(DB.h("code", code));
        }

        // save to db
        model.update(user_id, new Hashtable {
            { "mfa_secret" , fw.Session("mfa_secret") },
            { "mfa_added" , DB.NOW },
            { "mfa_recovery" , string.Join(" ",hashed_codes) },
        });
        fw.Session("mfa_secret", "");

        //if we here from initial login - also log user in
        if (fw.userId == 0)
        {
            fw.Session("mfa_login_users_id", "");
            model.doLogin(user_id);
        }

        return new Hashtable()
        {
            { "recovery_codes" , recovery_codes },
        };
    }
}