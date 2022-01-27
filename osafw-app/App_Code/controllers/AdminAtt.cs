// Admin Att controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw
{
    public class AdminAttController : FwAdminController
    {
        public static new int access_level = Users.ACL_MANAGER;

        protected Att model = new();

        public override void init(FW fw)
        {
            base.init(fw);
            model.init(fw);
            model0 = model;

            required_fields = "iname"; // default required fields, space-separated
            base_url = "/Admin/Att"; // base url for the controller
        }

        public override Hashtable IndexAction()
        {
            Hashtable ps = new();

            // get filters
            Hashtable f = initFilter();

            // sorting
            if (string.IsNullOrEmpty((string)f["sortby"]))
                f["sortby"] = "iname";
            if ((string)f["sortdir"] != "desc")
                f["sortdir"] = "asc";
            Hashtable SORTSQL = Utils.qh("id|id iname|iname add_time|add_time fsize|fsize ext|ext category|att_categories_id status|status");

            list_where = " status = 0 and table_name='' ";
            if (!string.IsNullOrEmpty((string)f["s"]))
            {
                list_where += " and (iname like @iname or fname like @iname)";
                list_where_params["@iname"] = "%" + f["s"] + "%";
            }
                
            if (!string.IsNullOrEmpty((string)f["att_categories_id"]))
            {
                list_where += " and att_categories_id=@att_categories_id";
                list_where_params["@att_categories_id"] = Utils.f2int(f["att_categories_id"]);
            }                

            int count = (int)db.valuep("select count(*) from " + model.table_name + " where " + list_where, list_where_params);
            ps["count"] = count;
            if (count > 0)
            {
                int pagenum = Utils.f2int(f["pagenum"]);
                int pagesize = Utils.f2int(f["pagesize"]);
                int offset = pagenum * pagesize;
                int limit = pagesize;
                string orderby = (string)SORTSQL[f["sortby"]??""];
                if (string.IsNullOrEmpty(orderby))
                    throw new Exception("No orderby defined for [" + f["sortby"] + "]");
                if ((string)f["sortdir"] == "desc")
                {
                    if (orderby.Contains(","))
                        orderby = orderby.Replace(",", " desc,");
                    orderby += " desc";
                }

                // offset+1 because _RowNumber starts from 1
                string sql = "SELECT TOP " + limit + " * " + " FROM (" + "   SELECT *, ROW_NUMBER() OVER (ORDER BY " + orderby + ") AS _RowNumber" + "   FROM " + model.table_name + "   WHERE " + list_where + ") tmp" + " WHERE _RowNumber >= " + db.qi(offset + 1) + " ORDER BY " + orderby;

                list_rows = db.arrayp(sql, list_where_params);
                ps["list_rows"] = list_rows;
                ps["pager"] = FormUtils.getPager(count, pagenum, pagesize);

                // add/modify rows from db
                foreach (Hashtable row in list_rows)
                {
                    row["fsize_human"] = Utils.bytes2str(Utils.f2long(row["fsize"]));
                    if (Utils.f2int(row["is_image"]) == 1)
                        row["url_s"] = model.getUrl(Utils.f2int(row["id"]), "s");
                    row["url_direct"] = model.getUrlDirect(Utils.f2int(row["id"]));

                    var att_categories_id = Utils.f2int(row["att_categories_id"]);
                    if (att_categories_id>0)
                        row["cat"] = fw.model<AttCategories>().one(att_categories_id);
                }
            }
            ps["f"] = f;

            ps["select_att_categories_ids"] = fw.model<AttCategories>().listSelectOptions();

            return ps;
        }

        public override Hashtable ShowFormAction(string form_id = "")
        {
            Hashtable ps = new();
            Hashtable item;
            int id = Utils.f2int(form_id);

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
                item = model.one(id);

                // and merge new values from the form
                Utils.mergeHash(item, reqh("item"));
            }
            ps["fsize_human"] = Utils.bytes2str(Utils.f2long(item["fsize"]));
            ps["url"] = model.getUrl(id);
            if (Utils.f2int(item["is_image"]) == 1)
                ps["url_m"] = model.getUrl(id, "m");

            ps["select_options_att_categories_id"] = fw.model<AttCategories>().listSelectOptions();

            setAddUpdUser(ps, item);

            ps["id"] = id;
            ps["i"] = item;
            if (fw.FormErrors.Count > 0)
                logger(fw.FormErrors);

            return ps;
        }


        public override Hashtable SaveAction(string form_id = "")
        {
            Hashtable ps = new();
            Hashtable item = reqh("item");

            int id = Utils.f2int(form_id);

            try
            {
                Validate(id, item);
                // load old record if necessary
                // Dim itemold As Hashtable = model.one(id)

                Hashtable itemdb = FormUtils.filter(item, Utils.qw("att_categories_id iname status"));
                if (string.IsNullOrEmpty((string)itemdb["iname"]))
                    itemdb["iname"] = "new file upload";

                if (id > 0)
                {
                    model.update(id, itemdb);
                    fw.flash("updated", 1);

                    // Proceed upload - for edit - just one file
                    model.uploadOne(id, 0, false);
                }
                else
                {
                    // Proceed upload - for add - could be multiple files
                    var addedAtt = model.uploadMulti(itemdb);
                    if (addedAtt.Count > 0)
                        id = (int)((Hashtable)addedAtt[0])["id"];
                    fw.flash("added", 1);
                }

                // if select in popup - return json
                ps["_json"] = true;
                ps["id"] = id;
                if (id > 0)
                {
                    item = model.one(id);
                    ps["success"] = true;
                    ps["url"] = model.getUrlDirect(id);
                    ps["iname"] = item["iname"];
                    ps["is_image"] = item["is_image"];
                }
                else
                    ps["success"] = false;

                // otherwise just redirect
                if (return_url.Length>0)
                {
                    fw.flash("success", "File uploaded");
                    ps["_redirect"] = return_url;
                }
                else
                    ps["_redirect"] = base_url + "/" + id + "/edit";
            }
            catch (ApplicationException ex)
            {
                ps["success"] = false;
                ps["err_msg"] = ex.Message;
                ps["_json"] = true;

                fw.setGlobalError(ex.Message);
                ps["_route_redirect"] = new Hashtable()
                {
                    {"method","ShowForm"},
                    {"args",new string[] { id.ToString() }}
                };
            }

            return ps;
        }

        public override void Validate(int id, Hashtable item)
        {
            bool result = true;
            // only require file during first upload
            // only require iname during update
            Hashtable itemdb;
            if (id > 0)
            {
                itemdb = model.one(id);
                result &= validateRequired(item, Utils.qw(required_fields));
            }
            else
            {
                itemdb = new();
                itemdb["fsize"] = "0";
            }

            if (Utils.f2int(itemdb["fsize"]) == 0)
            {
                if (fw.request.Form.Files.Count == 0 || fw.request.Form.Files[0]==null || fw.request.Form.Files[0].Length == 0)
                {
                    result = false;
                    fw.FormErrors["file1"] = "NOFILE";
                }
            }

            if (!result)
                fw.FormErrors["REQUIRED"] = true;

            if (fw.FormErrors.Count > 0 && !fw.FormErrors.ContainsKey("REQ"))
                fw.FormErrors["INVALID"] = 1;

            if (!result)
                throw new ApplicationException("");
        }

        public Hashtable SelectAction()
        {
            Hashtable ps = new();
            string category_icode = reqs("category");
            int att_categories_id = reqi("att_categories_id");

            Hashtable where = new();
            where["status"] = 0;
            if (category_icode.Length>0)
            {
                var att_cat = fw.model<AttCategories>().oneByIcode(category_icode);
                if (att_cat.Count > 0)
                {
                    att_categories_id = Utils.f2int(att_cat["id"]);
                    where["att_categories_id"] = att_categories_id;
                }
            }
            if (att_categories_id > 0)
                where["att_categories_id"] = att_categories_id;

            var rows = db.array(model.table_name, where, "add_time desc");
            foreach (var row in rows)
                row["direct_url"] = model.getUrlDirect(row);
            ps["att_dr"] = rows;
            ps["select_att_categories_id"] = fw.model<AttCategories>().listSelectOptions();
            ps["att_categories_id"] = att_categories_id;

            return ps;
        }
    }
}