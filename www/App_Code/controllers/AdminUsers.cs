// Admin Users controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw
{
    public class AdminUsersController : FwDynamicController
    {
        public static new int access_level = Users.ACL_ADMIN;

        protected Users model;

        public override void init(FW fw)
        {
            base.init(fw);
            // use if config doesn't contains model name
            // model0 = fw.model(Of Users)()
            // model = model0

            base_url = "/Admin/Users";
            this.loadControllerConfig();
            model = (Users)model0;
            db = model.getDB(); // model-based controller works with model's db

            model_related = fw.model<Users>();
        }

        public override Hashtable ShowFormAction(string form_id = "")
        {
            var ps = base.ShowFormAction(form_id);
            Hashtable item = (Hashtable)ps["i"];
            ps["att"] = fw.model<Att>().one(Utils.f2int(item["att_id"]));
            return ps;
        }

        public override Hashtable SaveAction(string form_id = "")
        {
            if (this.save_fields == null)
                throw new Exception("No fields to save defined, define in Controller.save_fields");

            if (reqi("refresh") == 1)
            {
                fw.routeRedirect("ShowForm", new string[]{form_id});
                return null;
            }

            Hashtable item = reqh("item");
            int id = Utils.f2int(form_id);
            var success = true;
            var is_new = (id == 0);

            item["email"] = item["ehack"]; // just because Chrome autofills fields too agressively

            try
            {
                Validate(id, item);
                // load old record if necessary
                // Dim item_old As Hashtable = model0.one(id)

                Hashtable itemdb = FormUtils.filter(item, this.save_fields);
                FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes);
                FormUtils.filterNullable(itemdb, save_fields_nullable);

                itemdb["pwd"] = itemdb["pwd"].ToString().Trim();
                if (string.IsNullOrEmpty((string)itemdb["pwd"]))
                    itemdb.Remove("pwd");

                id = this.modelAddOrUpdate(id, itemdb);

                if (Users.id == id)
                    model.reloadSession(id);
            }
            catch (ApplicationException ex)
            {
                success = false;
                this.setFormError(ex);
            }

            return this.afterSave(success, id, is_new);
        }

        public override void Validate(int id, Hashtable item)
        {
            bool result = true;
            result = result & validateRequired(item, Utils.qw(required_fields));
            if (!result)
                fw.FERR["REQ"] = 1;

            if (result && model.isExists(item["email"], id))
            {
                result = false;
                fw.FERR["ehack"] = "EXISTS";
            }
            if (result && !FormUtils.isEmail((string)item["email"]))
            {
                result = false;
                fw.FERR["ehack"] = "WRONG";
            }

            // uncomment if project requires good password strength
            // If result AndAlso item.ContainsKey("pwd") AndAlso model.scorePwd(item["pwd"]) <= 60 Then
            // result = False
            // fw.FERR["pwd"] = "BAD"
            // End If

            // If result AndAlso Not SomeOtherValidation() Then
            // result = False
            // FW.FERR("other field name") = "HINT_ERR_CODE"
            // End If

            if (fw.FERR.Count > 0 && !fw.FERR.ContainsKey("REQ"))
                fw.FERR["INVALID"] = 1;

            if (!result)
                throw new ApplicationException("");
        }

        // cleanup session for current user and re-login as user from id
        // check access - only users with higher level may login as lower leve
        public void SimulateAction(string form_id)
        {
            int id = Utils.f2int(form_id);

            Hashtable user = model.one(id);
            if (user.Count == 0)
                throw new ApplicationException("Wrong User ID");
            if (Utils.f2int(user["access_level"]) >= Utils.f2int(fw.Session("access_level")))
                throw new ApplicationException("Access Denied. Cannot simulate user with higher access level");

            fw.logEvent("simulate", id, Users.id);

            if (model.doLogin(id))
                fw.redirect((string)fw.config("LOGGED_DEFAULT_URL"));
        }

        public Hashtable SendPwdAction(string form_id)
        {
            Hashtable ps = new Hashtable();
            int id = Utils.f2int(form_id);

            ps["success"] = model.sendPwdReset(id);
            ps["err_msg"] = fw.last_error_send_email;
            ps["_json"] = true;
            return ps;
        }

        // for migration to hashed passwords
        public void HashPasswordsAction()
        {
            rw("hashing passwords");
            var rows = db.array(model.table_name, new Hashtable(), "id");
            foreach (Hashtable row in rows)
            {
                if (row["pwd"].ToString().Substring(0, 2) == "$2")
                    continue; // already hashed
                var hashed = model.hashPwd((string)row["pwd"]);
                db.update(model.table_name, new Hashtable() { { "pwd", hashed } }, new Hashtable() { { "id", row["id"] } });
            }
            rw("done");
        }
    }
}