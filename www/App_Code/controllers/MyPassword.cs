// MyPassword controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw
{
    public class MyPasswordController : FwController
    {
        public static new int access_level = Users.ACL_MEMBER;

        protected Users model = new Users();

        public override void init(FW fw)
        {
            base.init(fw);
            model.init(fw);
        }

        public void IndexAction()
        {
            fw.routeRedirect("ShowForm", "MyPassword");
        }

        public Hashtable ShowFormAction()
        {
            if (reqs("result") == "record_updated")
                fw.G["green_msg"] = "Login/Password has been changed";

            Hashtable ps = new();
            Hashtable item = reqh("item");
            int id = Users.id;

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
                var itemdb = model.one(id);
                // and merge new values from the form
                Utils.mergeHash(itemdb, item);
                item = itemdb;
            }

            ps["id"] = id;
            ps["i"] = item;
            ps["ERR"] = fw.FERR;
            return ps;
        }

        public void SaveAction()
        {
            int id = Users.id;
            try
            {
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

                fw.redirect("/My/Password/" + id + "/edit");
            }
            catch (ApplicationException)
            {
                fw.routeRedirect("ShowForm");
            }
        }

        public bool Validate(int id, Hashtable item)
        {
            bool result = true;
            result &= validateRequired(item, Utils.qw("email old_pwd pwd pwd2"));
            if (!result)
                fw.FERR["REQ"] = 1;

            if (result && model.isExists(item["email"], id))
            {
                result = false;
                fw.FERR["email"] = "EXISTS";
            }
            if (result && !FormUtils.isEmail((string)item["email"]))
            {
                result = false;
                fw.FERR["email"] = "WRONG";
            }

            if (result && model.cleanPwd((string)item["pwd"]) != model.cleanPwd((string)item["pwd2"]))
            {
                result = false;
                fw.FERR["pwd2"] = "NOTEQUAL";
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
                    fw.FERR["old_pwd"] = "WRONG";
                }
            }

            if (fw.FERR.Count > 0 && !fw.FERR.ContainsKey("REQ"))
                fw.FERR["INVALID"] = 1;

            if (!result)
                throw new ApplicationException("");
            return true;
        }
    }
}