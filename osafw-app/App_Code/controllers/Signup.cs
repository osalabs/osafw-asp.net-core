// Signup controller (register new user)
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com


using System;
using System.Collections;

namespace osafw
{
    public class SignupController : FwController
    {
        protected Users model;
        public static new string route_default_action = "index";

        public override void init(FW fw)
        {
            base.init(fw);
            model = new();
            model.init(fw);
            model0 = model;
            required_fields = "email pwd";
            base_url = "/Signup";
            // override layout
            fw.G["PAGE_LAYOUT"] = fw.G["PAGE_LAYOUT_PUBLIC"];

            if (!Utils.f2bool(fw.config("IS_SIGNUP")))
                fw.redirect((string)fw.config("UNLOGGED_DEFAULT_URL"));
        }

        public void IndexAction()
        {
            fw.routeRedirect("ShowForm");
        }

        public Hashtable ShowFormAction()
        {
            Hashtable ps = new();
            Hashtable item = new();

            if (isGet())
            {
            }
            else
                // and merge new values from the form
                Utils.mergeHash(item, reqh("item"));

            ps["i"] = item;
            ps["hide_sidebar"] = true;
            return ps;
        }

        public void SaveAction(string form_id = "")
        {
            var item = reqh("item");
            int id = Utils.f2int(form_id);

            try
            {
                Validate(item);
                // load old record if necessary
                // Dim itemdb As Hashtable = model.one(id)

                var itemdb = FormUtils.filter(item, Utils.qw("email pwd fname lname"));

                if (id == 0)
                {
                    item["access_level"] = 0;
                    item["add_users_id"] = 0;
                }
                id = modelAddOrUpdate(id, itemdb);

                fw.sendEmailTpl((string)itemdb["email"], "signup.txt", itemdb);

                model.doLogin(id);
                fw.redirect((string)fw.config("LOGGED_DEFAULT_URL"));
            }
            catch (ApplicationException ex)
            {
                this.setFormError(ex);
                fw.routeRedirect("ShowForm", null, new string[] { id.ToString() });
            }
        }

        public bool Validate(Hashtable item)
        {
            string msg = "";
            bool result = true;
            result &= validateRequired(item, Utils.qw(required_fields));
            if (!result)
                msg = "Please fill in all required fields";

            if (result && model.isExists(item["email"], 0))
            {
                result = false;
                fw.FormErrors["email"] = "EXISTS";
            }
            if (result && !FormUtils.isEmail((string)item["email"]))
            {
                result = false;
                fw.FormErrors["email"] = "WRONG";
            }

            if (result && (string)item["pwd"] != (string)item["pwd2"])
            {
                result = false;
                fw.FormErrors["pwd2"] = "WRONG";
            }

            if (!result)
                throw new ApplicationException(msg);
            return true;
        }
    }
}