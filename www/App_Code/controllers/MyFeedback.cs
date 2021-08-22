// My Feedback controller
// when user post feedback - send it to the support_email
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw
{
    public class MyFeedbackController : FwController
    {
        public static new int access_level = Users.ACL_MEMBER;

        protected Users model = new();

        public override void init(FW fw)
        {
            base.init(fw);
            model.init(fw);
            required_fields = "iname idesc"; // default required fields, space-separated
            base_url = "/My/Feedback"; // base url for the controller

            save_fields = "iname idesc";
        }

        public void IndexAction()
        {
            throw new ApplicationException("Not Implemented");
        }


        public void SaveAction()
        {
            var item = reqh("item");
            var id = Users.id;

            try
            {
                Validate(id.ToString(), item);
                // load old record if necessary
                // Dim itemold As Hashtable = model.one(id)

                Hashtable itemdb = FormUtils.filter(item, save_fields);
                var user = fw.model<Users>().one(id);
                Hashtable ps = new()
                {
                    { "user", user },
                    { "i", itemdb },
                    { "url", return_url }
                };
                fw.sendEmailTpl((string)fw.config("support_email"), "feedback.txt", ps, null, null, (string)user["email"]);

                fw.flash("success", "Feedback sent. Thank you.");
            }
            catch (ApplicationException ex)
            {
                fw.setGlobalError(ex.Message);
            }

            fw.redirect(this.getReturnLocation());
        }

        public bool Validate(string id, Hashtable item)
        {
            bool result = true;
            result &= validateRequired(item, Utils.qw(required_fields));
            if (!result)
                fw.FERR["REQ"] = 1;

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