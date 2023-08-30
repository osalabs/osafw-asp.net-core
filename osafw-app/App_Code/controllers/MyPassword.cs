// MyPassword controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;

namespace osafw;

public class MyPasswordController : FwController
{
    public static new int access_level = Users.ACL_MEMBER;

    protected Users model = new();

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);

        base_url = "/My/Password";

        is_readonly = false;//allow update my stuff
    }

    public void IndexAction()
    {
        fw.redirect(base_url + "/new");
    }

    public Hashtable ShowFormAction()
    {
        if (reqs("result") == "record_updated")
            fw.G["green_msg"] = "Login/Password has been changed";

        Hashtable ps = new();
        Hashtable item = reqh("item");
        int id = fw.userId;

        if (isGet())
        {
            if (id > 0)
                item = model.one(id);
            else
                // set defaults here
                item = new Hashtable();
        }
        else
        {
            // read from db
            Hashtable itemdb = model.one(id);
            // and merge new values from the form
            Utils.mergeHash(itemdb, item);
            item = itemdb;
        }

        ps["id"] = id;
        ps["i"] = item;
        ps["ERR"] = fw.FormErrors;
        return ps;
    }

    public void SaveAction()
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        int id = fw.userId;

        Validate(id, reqh("item"));
        // load old record if necessary
        // Dim itemdb As Hashtable = Users.one(id)

        var itemdb = FormUtils.filter(reqh("item"), Utils.qw("email pwd"));
        itemdb["pwd"] = itemdb["pwd"].ToString().Trim();

        if (id > 0)
        {
            model.update(id, itemdb);

            fw.logEvent("chpwd");
            fw.flash("record_updated", 1);
        }

        afterSave(true, id);
    }

    public void Validate(int id, Hashtable item)
    {
        bool result = true;
        result &= validateRequired(item, Utils.qw("email old_pwd pwd pwd2"));
        if (!result)
            fw.FormErrors["REQ"] = 1;

        if (result && model.isExists(item["email"], id))
        {
            result = false;
            fw.FormErrors["email"] = "EXISTS";
        }
        if (result && !FormUtils.isEmail((string)item["email"]))
        {
            result = false;
            fw.FormErrors["email"] = "WRONG";
        }

        if (result && model.cleanPwd((string)item["pwd"]) != model.cleanPwd((string)item["pwd2"]))
        {
            result = false;
            fw.FormErrors["pwd2"] = "NOTEQUAL";
        }

        // uncomment if project requires good password strength
        // If result AndAlso item.ContainsKey("pwd") AndAlso model.scorePwd(item["pwd"]) <= 60 Then
        // result = False
        // fw.FERR["pwd") ] "BAD"
        // End If

        if (result)
        {
            Hashtable itemdb = model.one(id);
            if (!fw.model<Users>().checkPwd((string)item["old_pwd"], (string)itemdb["pwd"]))
            {
                result = false;
                fw.FormErrors["old_pwd"] = "WRONG";
            }
        }

        this.validateCheckResult();
    }

    public Hashtable SetupMFAAction()
    {
        int id = fw.userId;

        //generate secret and save to session only (will be saved to db after validation)
        fw.Session("mfa_secret", model.generateMFASecret());

        Hashtable ps = new();
        return ps;
    }

    public Hashtable SaveMFAAction()
    {
        route_onerror = "SetupMFA"; //set route to go if error happens
        checkXSS();

        if (string.IsNullOrEmpty(fw.Session("mfa_secret")))
            fw.redirect(base_url); //no code generated yet

        int id = fw.userId;
        string mfa_code = reqs("mfa_code");

        if (!model.isValidMFACode(fw.Session("mfa_secret"), mfa_code))
            throw new UserException("MFA Code is not valid");

        // code is valid, generate recovery codes and save
        // generate 5 recovery codes as random 8-digit numbers using Utils.getRandStr(8) and concatenate into comma-separated string
        var hashed_codes = new List<string>(5);
        ArrayList recovery_codes = new();
        for (int i = 0; i < 5; i++)
        {
            var code = Utils.getRandStr(8);
            hashed_codes.Add(model.hashPwd(code));
            recovery_codes.Add(DB.h("code", code));
        }            

        // save to db
        model.update(id, new Hashtable {
            { "mfa_secret" , fw.Session("mfa_secret") },
            { "mfa_added" , DateTime.Now },
            { "mfa_recovery" , string.Join(" ",hashed_codes) },
        });
        fw.Session("mfa_secret", "");

        return new Hashtable()
        {
            { "recovery_codes" , recovery_codes },
        };
    }
}