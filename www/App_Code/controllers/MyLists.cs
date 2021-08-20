// User Lists Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Linq;

namespace osafw
{
    public class MyListsController : FwAdminController
    {
        public static new int access_level = Users.ACL_MEMBER;

        protected UserLists model;

        public override void init(FW fw)
        {
            base.init(fw);
            model = fw.model<UserLists>();
            model0 = model;

            // initialization
            base_url = "/My/Lists";
            required_fields = "entity iname";
            save_fields = "entity iname idesc status";

            search_fields = "iname idesc";
            list_sortdef = "iname asc";   // default sorting: name, asc|desc direction
            list_sortmap = Utils.qh("id|id iname|iname add_time|add_time");

            related_id = reqs("related_id");
        }

        public override Hashtable initFilter(string session_key = null)
        {
            var result = base.initFilter(session_key);
            if (!this.list_filter.ContainsKey("entity"))
                this.list_filter["entity"] = related_id;
            return this.list_filter;
        }

        public override void setListSearch()
        {
            list_where = " status<>127 and add_users_id = " + db.qi(Users.id); // only logged user lists

            base.setListSearch();

            if (!string.IsNullOrEmpty((string)list_filter["entity"]))
                this.list_where += " and entity=" + db.q(list_filter["entity"]);
        }
        public override void getListRows()
        {
            base.getListRows();

            foreach (Hashtable row in this.list_rows)
                row["ctr"] = model.countItems(Utils.f2int(row["id"]));
        }

        public override Hashtable ShowFormAction(string form_id = "")
        {
            this.form_new_defaults = new();
            this.form_new_defaults["entity"] = related_id;
            return base.ShowFormAction(form_id);
        }

        public override Hashtable SaveAction(string form_id = "")
        {
            if (this.save_fields == null)
                throw new Exception("No fields to save defined, define in Controller.save_fields");

            Hashtable item = reqh("item");
            int id = Utils.f2int(form_id);
            var success = true;
            var is_new = (id == 0);

            try
            {
                Validate(id, item);
                // load old record if necessary
                // Dim item_old As Hashtable = model0.one(id)

                Hashtable itemdb = FormUtils.filter(item, this.save_fields);
                FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes);

                id = this.modelAddOrUpdate(id, itemdb);

                if (is_new && item.ContainsKey("item_id"))
                {
                    // item_id could contain comma-separated ids
                    var hids = Utils.commastr2hash((string)item["item_id"]);
                    if (hids.Count > 0)
                    {
                        // if item id passed - link item with the created list
                        foreach (string sitem_id in hids.Keys)
                        {
                            var item_id = Utils.f2int(sitem_id);
                            if (item_id > 0)
                                model.addItems(id, item_id);
                        }
                    }
                }
            }
            catch (ApplicationException ex)
            {
                success = false;
                this.setFormError(ex);
            }

            if (!string.IsNullOrEmpty(return_url))
                fw.redirect(return_url);

            return this.afterSave(success, id, is_new);
        }

        public Hashtable ToggleListAction(string form_id)
        {
            var user_lists_id = Utils.f2int(form_id);
            var item_id = reqi("item_id");
            var ps = new Hashtable()
            {
                {"_json",true},
                {"success",true}
            };

            try
            {
                var user_lists = fw.model<UserLists>().one(user_lists_id);
                if (item_id == 0 || user_lists.Count == 0 || Utils.f2int(user_lists["add_users_id"]) != Users.id)
                    throw new ApplicationException("Wrong Request");

                var res = fw.model<UserLists>().toggleItemList(user_lists_id, item_id);
                ps["iname"] = user_lists["iname"];
                ps["action"] = (res ? "added" : "removed");
            }
            catch (ApplicationException ex)
            {
                ps["success"] = false;
                ps["err_msg"] = ex.Message;
            }

            return ps;
        }

        // request item_id - could be one id, or comma-separated ids
        public Hashtable AddToListAction(string form_id)
        {
            var user_lists_id = Utils.f2int(form_id);
            Hashtable items = Utils.commastr2hash(reqs("item_id"));

            var ps = new Hashtable()
            {
                {"_json",true},
                {"success",true}
            };

            try
            {
                var user_lists = fw.model<UserLists>().one(user_lists_id);
                if (user_lists.Count == 0 || Utils.f2int(user_lists["add_users_id"]) != Users.id)
                    throw new ApplicationException("Wrong Request");

                foreach (string key in items.Keys)
                {
                    var item_id = Utils.f2int(key);
                    if (item_id > 0)
                        fw.model<UserLists>().addItemList(user_lists_id, item_id);
                }
            }
            catch (ApplicationException ex)
            {
                ps["success"] = false;
                ps["err_msg"] = ex.Message;
            }

            return ps;
        }

        // request item_id - could be one id, or comma-separated ids
        public Hashtable RemoveFromListAction(string form_id)
        {
            var user_lists_id = Utils.f2int(form_id);
            Hashtable items = Utils.commastr2hash(reqs("item_id"));
            var ps = new Hashtable()
            {
                {"_json",true},
                {"success",true}
            };

            try
            {
                var user_lists = fw.model<UserLists>().one(user_lists_id);
                if (user_lists.Count == 0 || Utils.f2int(user_lists["add_users_id"]) != Users.id)
                    throw new ApplicationException("Wrong Request");

                foreach (string key in items.Keys)
                {
                    var item_id = Utils.f2int(key);
                    if (item_id > 0)
                        fw.model<UserLists>().delItemList(user_lists_id, item_id);
                }
            }
            catch (ApplicationException ex)
            {
                ps["success"] = false;
                ps["err_msg"] = ex.Message;
            }

            return ps;
        }
    }
}