// Forgotten Password controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class PasswordController : FwController
{
    protected Users model = new();

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);
        base_url = "/Password"; // base url for the controller
                                // override layout
        fw.G["PAGE_LAYOUT"] = fw.G["PAGE_LAYOUT_PUBLIC"];
    }

    public override void checkAccess()
    {
        //true - allow access to all, including visitors
    }

    public Hashtable IndexAction()
    {
        Hashtable ps = [];

        Hashtable item = reqh("item");
        if (isGet())
            // set defaults here
            item = [];
        else
        {
        }

        ps["i"] = item;
        ps["hide_sidebar"] = true;
        return ps;
    }

    public void SaveAction()
    {
        route_onerror = FW.ACTION_INDEX; //set route to go if error happens

        string login = reqh("item")["login"].ToString().Trim();

        if (login.Length == 0)
            throw new UserException("Please enter your Email");

        var user = model.oneByEmail(login);
        if (user.Count == 0 || user["status"].toInt() != Users.STATUS_ACTIVE)
            throw new UserException("Not a valid Email");

        model.sendPwdReset(user["id"].toInt());

        fw.redirect(base_url + "/(Sent)");
    }

    public Hashtable SentAction()
    {
        Hashtable ps = [];
        ps["hide_sidebar"] = true;
        return ps;
    }
}