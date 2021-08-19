// My Settings controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw
{
    public class MySettingsController : FwController
    {
        public static new int access_level = Users.ACL_MEMBER;

        protected Users model = new Users();

        public override void init(FW fw)
        {
            base.init(fw);
            model.init(fw);
            required_fields = "email"; // default required fields, space-separated
            base_url = "/My/Settings"; // base url for the controller

            save_fields = "email fname lname address1 address2 city state zip phone lang";
        }

        public void IndexAction()
        {
            fw.routeRedirect("ShowForm", null);
        }


        public Hashtable ShowFormAction()
        {
            Hashtable ps = new();
            Hashtable item = reqh("item");
            var id = model.meId();

            if (isGet())
                item = model.one(id);
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

            return ps;
        }

        public void SaveAction()
        {
            var item = reqh("item");
            var id = model.meId();

            try
            {
                Validate(id, item);
                // load old record if necessary
                // Dim itemold As Hashtable = model.one(id)

                Hashtable itemdb = FormUtils.filter(item, save_fields);
                model.update(id, itemdb);
                fw.flash("record_updated", 1);

                model.reloadSession();

                fw.redirect(base_url + "/" + id + "/edit");
            }
            catch (ApplicationException ex)
            {
                fw.G["err_msg"] = ex.Message;
                fw.routeRedirect("ShowForm");
            }
        }

        public bool Validate(int id, Hashtable item)
        {
            bool result = true;
            result &= validateRequired(item, Utils.qw(required_fields));
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

            //if (result && !SomeOtherValidation())
            //{
            //    fw.FERR["other field name"] = "HINT_ERR_CODE";
            //}

            if (fw.FERR.Count > 0 && !fw.FERR.ContainsKey("REQ"))
                fw.FERR["INVALID"] = 1;

            if (!result)
                throw new ApplicationException("");
            return true;
        }
    }
}