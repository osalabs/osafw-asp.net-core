﻿// LookupManager Admin controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Collections.Generic;

namespace osafw;

public class AdminLookupManagerController : FwController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected LookupManager model = new();
    protected LookupManagerTables model_tables = new();
    protected string dict; // current lookup dictionary
    protected Hashtable defs;
    protected string dictionaries_url;

    public override void init(FW fw)
    {
        base.init(fw);
        model.init(fw);
        model_tables = fw.model<LookupManagerTables>();
        required_fields = ""; // default required fields, space-separated
        base_url = "/Admin/LookupManager"; // base url for the controller
        dictionaries_url = base_url + "/(Dictionaries)";

        dict = reqs("d");
        defs = model_tables.oneByTname(dict);
        if (defs.Count == 0)
            dict = "";
        else
        {
            //don't allow access to tables with access_level higher than current user
            var acl = defs["access_level"].toInt();
            if (!fw.model<Users>().isAccessLevel(acl))
                dict = "";
        }
    }

    public override void checkAccess()
    {
        // add custom actions to permissions mapping
        access_actions_to_permissions = new() {
            { "Dictionaries", Permissions.PERMISSION_LIST },
        };
        base.checkAccess();
    }

    private void check_dict()
    {
        if (string.IsNullOrEmpty(dict))
            fw.redirect(base_url + "/(Dictionaries)");
        if (!Utils.isEmpty(defs["url"]))
            fw.redirect((string)defs["url"]);

        //now check access rights via RBAC
        int current_user_level = fw.userAccessLevel;
        if (current_user_level < Users.ACL_SITEADMIN)
        {
            var action_more = fw.route.action_more;
            if ((fw.route.action == FW.ACTION_SAVE || fw.route.action == FW.ACTION_SHOW_FORM) && Utils.isEmpty(fw.route.id))
            {
                //if save/showform and no id - it's add new - check for Add permission
                action_more = FW.ACTION_MORE_NEW;
            }

            var resource_code = LookupManager.RBAC_RESOURCE_PREFIX + dict;
            if (!fw.model<Users>().isAccessByRolesResourceAction(fw.userId, resource_code, fw.route.action, action_more))
                throw new AuthException("Bad access - Not authorized to perform this action with Lookup Table");
        }

    }

    public Hashtable DictionariesAction()
    {
        Hashtable ps = [];

        // code below to show list of items in columns instead of plain list

        int columns = 4;
        DBList tables1 = model_tables.listByGroup();
        DBList tables = [];

        //first filter out inaccessible tables
        foreach (var table in tables1)
        {
            //do not show tables with access_level higher than current user
            var acl = table["access_level"].toInt();
            if (!fw.model<Users>().isAccessLevel(acl))
                continue;

            // do not show tables if RBAC denies list access
            var resource_code = LookupManager.RBAC_RESOURCE_PREFIX + table["tname"];
            if (!fw.model<Users>().isAccessByRolesResourceAction(fw.userId, resource_code, FW.ACTION_INDEX))
                continue;

            tables.Add(table);
        }

        int max_rows = (int)Math.Ceiling(tables.Count / (double)columns);
        ArrayList cols = [];

        // add rows
        int curcol = 0;
        int curcol_rows = 0;
        string curgroup = "";
        foreach (var table in tables)
        {
            var igroup = table["igroup"].Trim();
            bool is_new_group = igroup != curgroup;
            if ((is_new_group || igroup == "") && curcol_rows >= max_rows)
            {
                //if we got more rows than max - move to next column (for new group or empty group)
                curcol += 1;
            }

            // add new column if needed
            if (cols.Count <= curcol)
                cols.Add(new Hashtable());
            Hashtable h = (Hashtable)cols[curcol];
            if (h.Count == 0)
            {
                h["col_sm"] = Math.Floor(12 / (double)columns);
                h["list_rows"] = new ArrayList();
            }

            ArrayList al = (ArrayList)h["list_rows"];
            if (is_new_group)
            {
                Hashtable group = new()
                {
                    ["is_group"] = true,
                    ["igroup"] = igroup
                };
                al.Add(group);
                curgroup = igroup;
            }
            al.Add(table.toHashtable());
            curcol_rows = al.Count;
        }

        ps["list_сols"] = cols;
        return ps;
    }

    public Hashtable IndexAction()
    {
        check_dict();

        // if this is one-form dictionary - show edit form with first record
        if (defs["is_one_form"].toBool())
        {
            string id_fname = fw.model<LookupManagerTables>().getColumnId(defs);
            var row = model.topByTname((string)defs["tname"]);
            // fw.redirect(base_url & "/" & row(id_fname) & "/edit/?d=" & dict)
            String[] args = ([(string)row[id_fname]]);
            fw.routeRedirect(FW.ACTION_SHOW_FORM, args);
            return null;
        }

        // get columns
        ArrayList cols = model_tables.getColumns(defs);
        string list_table_name = (string)defs["tname"];
        // logger(defs)
        // logger(cols)

        Hashtable ps = [];
        ps["is_two_modes"] = true;
        Hashtable f = initFilter("_filter_lookupmanager_" + list_table_name);

        // sorting
        if (Utils.isEmpty(f["sortby"]))
        {
            var is_prio_exists = false;
            foreach (Hashtable col in cols)
            {
                if (col["name"].toStr() == "prio")
                {
                    is_prio_exists = true;
                    break;
                }
            }
            if (cols.Count > 0)
            {
                if (is_prio_exists)
                {
                    f["sortby"] = "prio asc, iname";
                }
                else
                {
                    f["sortby"] = ((Hashtable)cols[0])["name"]; // by default - sort by first column
                }
            }
            else
                f["sortby"] = "";
        }
        if ((string)f["sortdir"] != "desc")
            f["sortdir"] = "asc";
        Hashtable SORTSQL = [];
        ArrayList fields_headers = [];
        ArrayList group_headers = [];
        bool is_group_headers = false;

        Hashtable list_cols = [];
        if (!Utils.isEmpty(defs["list_columns"]))
        {
            list_cols = Utils.commastr2hash((string)defs["list_columns"]);
            ps["is_two_modes"] = false; // if custom list defined - don't enable table edit mode
        }
        else
            // if no custom columns - remove sys cols
            cols = model.filterOutSysCols(cols);

        foreach (Hashtable col in cols)
        {
            SORTSQL[col["name"]] = db.qid((string)col["name"]);

            if (list_cols.Count > 0 && !list_cols.ContainsKey(col["name"]))
                continue;

            Hashtable fh = [];
            fh["iname"] = col["iname"];
            fh["colname"] = col["name"];
            fh["maxlen"] = col["maxlen"];
            fh["type"] = col["itype"];
            if ((string)fh["type"] == "textarea")
                fh["type"] = ""; // show textarea as inputtext in table edit mode

            if (col["itype"].ToString().Contains('.'))
            {
                // lookup type
                fh["type"] = "lookup";
                fh["select_options"] = model_tables.getLookupSelectOptions((string)col["itype"], "");
            }

            fields_headers.Add(fh);

            // detect/build group headers
            string igroup = col["igroup"].ToString().Trim();
            if (group_headers.Count == 0)
            {
                Hashtable h = [];
                h["iname"] = igroup;
                h["colspan"] = 0;
                group_headers.Add(h);
            }
            if (igroup == (string)((Hashtable)group_headers[group_headers.Count - 1])["iname"])
                ((Hashtable)group_headers[group_headers.Count - 1])["colspan"] = (int)((Hashtable)group_headers[group_headers.Count - 1])["colspan"] + 1;
            else
            {
                Hashtable h = [];
                h["iname"] = igroup;
                h["colspan"] = 1;
                group_headers.Add(h);
            }

            if (!string.IsNullOrEmpty(igroup))
                is_group_headers = true;
        }

        list_where = " 1=1";
        if (!Utils.isEmpty(f["s"]))
        {
            list_where_params["@slike"] = "%" + f["s"] + "%";
            string swhere = "";
            foreach (Hashtable col in cols)
                swhere += " or " + db.qid((string)col["name"]) + " like @slike";

            if (!string.IsNullOrEmpty(swhere))
                list_where += " and (0=1 " + swhere + ")";
        }

        ps["count"] = db.valuep("select count(*) from " + db.qid(list_table_name) + " where " + list_where, list_where_params).toLong();

        if ((long)ps["count"] > 0)
        {
            int pagenum = list_filter["pagenum"].toInt();
            int pagesize = list_filter["pagesize"].toInt();
            int offset = pagenum * pagesize;
            int limit = pagesize;
            string orderby = (string)SORTSQL[(string)f["sortby"]];
            if (string.IsNullOrEmpty(orderby))
                orderby = "1";
            if ((string)f["sortdir"] == "desc")
            {
                if (orderby.Contains(','))
                    orderby = orderby.Replace(",", " desc,");
                orderby += " desc";
            }

            ArrayList list_rows = db.selectRaw("*", db.qid(list_table_name), list_where, list_where_params, orderby, offset, limit);
            ps["list_rows"] = list_rows;

            ps["count_from"] = pagenum * pagesize + 1;
            ps["count_to"] = pagenum * pagesize + list_rows.Count;

            ps["pager"] = FormUtils.getPager((long)ps["count"], pagenum, pagesize);
            if (ps["pager"] != null)
            {
                // add dict info for pager
                foreach (Hashtable page in (ArrayList)ps["pager"])
                    page["d"] = dict;
            }

            // add/modify rows from db
            foreach (Hashtable row in list_rows)
            {
                // calc md5 first if in edit mode
                if ((string)f["mode"] == "edit")
                    row["row_md5"] = model.getRowMD5(row);

                row["is_readonly"] = is_readonly;
                row["id"] = row[model_tables.getColumnId(defs)];
                row["d"] = dict;
                row["f"] = f;

                ArrayList fv = [];
                foreach (Hashtable col in cols)
                {
                    var colname = (string)col["name"];
                    var colitype = (string)col["itype"];
                    if (list_cols.Count > 0 && !list_cols.ContainsKey(colname))
                        continue;

                    Hashtable fh = [];
                    fh["colname"] = colname;
                    fh["iname"] = col["iname"];
                    fh["value"] = row[colname];
                    if (list_cols.Count == 0 && (colname == "status" || colname == "iname" || colname == "prio" || colitype == "date"))
                        fh["is_custom"] = true;

                    fh["id"] = row["id"];
                    fh["maxlen"] = col["maxlen"];
                    fh["type"] = colitype;
                    if ((string)fh["type"] == "textarea")
                        fh["type"] = ""; // show textarea as inputtext in table edit mode

                    if (colitype.Contains('.'))
                    {
                        // lookup type
                        fh["type"] = "lookup";
                        fh["select_options"] = model_tables.getLookupSelectOptions(colitype, fh["value"]);
                        // for lookup type display value should be from lookup table
                        fh["value"] = model_tables.getLookupValue(colitype, fh["value"]);
                    }

                    fv.Add(fh);
                }
                row["fields_values"] = fv;
            }
        }
        ps["fields_headers"] = fields_headers;
        ps["group_headers"] = group_headers;
        ps["is_group_headers"] = is_group_headers;
        ps["f"] = f;
        ps["defs"] = defs;
        ps["d"] = dict;
        ps["is_readonly"] = is_readonly;
        ps["return_url"] = reqs("return_url");

        return ps;
    }

    public Hashtable ShowFormAction(int id = 0)
    {
        fw.model<Users>().checkReadOnly();

        check_dict();

        Hashtable hf = [];
        Hashtable item;
        ArrayList cols = model_tables.getColumns(defs);
        bool is_fwtable = false;

        if (Utils.isEmpty(defs["list_columns"]))
        {
            // if no custom columns - remove sys cols
            is_fwtable = true;
            cols = model.filterOutSysCols(cols);
        }


        if (isGet())
        {
            if (id > 0)
                item = model.oneByTname(dict, id);
            else
            {
                // set defaults here
                item = [];
                // item["field"]="default value";
                item["prio"] = model.maxIdByTname(dict) + 1; // default prio (if exists) = max(id)+1
            }
        }
        else
        {
            // read from db
            item = model.oneByTname(dict, id);
            // and merge new values from the form
            Utils.mergeHash(item, reqh("item"));
        }

        ArrayList fv = [];
        string last_igroup = "";
        foreach (Hashtable col in cols)
        {
            if (is_fwtable && (string)col["name"] == "status")
                continue; // for fw tables - status displayed in standard way

            Hashtable fh = [];
            fh["colname"] = col["name"];
            fh["iname"] = col["iname"];
            fh["value"] = item[col["name"]];
            fh["type"] = col["itype"].ToString().Trim();
            if (!Utils.isEmpty(col["maxlen"]))
            {
                if ((string)col["maxlen"] == "-1")
                {
                    fh["maxlen"] = ""; // textarea
                    fh["type"] = "textarea";
                }
                else
                    fh["maxlen"] = col["maxlen"];
            }
            else
                fh["maxlen"] = col["numeric_precision"].toInt() + (col["numeric_scale"].toInt() > 0 ? 1 : 0);

            if (col["itype"].toStr().Contains('.'))
            {
                // lookup type
                fh["type"] = "lookup";
                fh["select_options"] = model_tables.getLookupSelectOptions((string)col["itype"], fh["value"]);
            }

            string igroup = col["igroup"].toStr().Trim();
            if (igroup != last_igroup)
            {
                fh["is_group"] = true;
                fh["igroup"] = igroup;
                last_igroup = igroup;
            }

            fv.Add(fh);
        }
        hf["fields"] = fv;

        hf["is_fwtable"] = is_fwtable;
        if (is_fwtable)
        {
            hf["add_users_id_name"] = fw.model<Users>().iname(item["add_users_id"]);
            hf["upd_users_id_name"] = fw.model<Users>().iname(item["upd_users_id"]);
        }

        hf["id"] = id;
        hf["i"] = item;
        hf["defs"] = defs;
        hf["d"] = dict;

        return hf;
    }

    public Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM; //set route to go if error happens

        fw.model<Users>().checkReadOnly();

        check_dict();

        Hashtable item = reqh("item");
        ArrayList cols = model_tables.getColumns(defs);

        Validate(id, item);

        Hashtable itemdb = [];
        foreach (Hashtable col in cols)
        {
            if (item.ContainsKey(col["name"]))
                itemdb[(string)col["name"]] = item[col["name"]];
            else if ((string)col["itype"] == "checkbox")
                itemdb[(string)col["name"]] = "0";// for checkboxes just set them 0
        }

        if (id > 0)
        {
            if (model.updateByTname(dict, id, itemdb))
                fw.flash("updated", 1);
        }
        else
        {
            model.addByTname(dict, itemdb);
            fw.flash("added", 1);
        }

        // redirect to list as we don't have id on insert
        // fw.redirect(base_url + "/" + id + "/edit")
        var return_url = base_url + "/?d=" + dict;
        return afterSave(true, null, id == 0, "no_action", return_url);
    }

    public void Validate(int id, Hashtable item)
    {
        validateRequired(id, item, Utils.qw(required_fields));

        this.validateCheckResult();
    }

    public Hashtable ShowDeleteAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        check_dict();

        Hashtable hf = [];
        Hashtable item = model.oneByTname(dict, id);
        if (item.Count == 0)
            throw new ApplicationException("Not found");

        hf["i"] = item;
        hf["iname"] = item[new ArrayList(item.Keys)[0]];
        hf["id"] = id;
        hf["defs"] = defs;
        hf["d"] = dict;

        return hf;
    }

    public void DeleteAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        check_dict();

        model.deleteByTname(dict, id);
        fw.flash("onedelete", 1);
        fw.redirect(base_url + "/?d=" + dict);
    }

    public void SaveMultiAction()
    {
        route_onerror = FW.ACTION_INDEX;

        fw.model<Users>().checkReadOnly();

        check_dict();

        int del_ctr = 0;
        Hashtable cbses = reqh("cb");
        if (cbses.Count > 0)
        {
            // multirecord delete
            foreach (string id in cbses.Keys)
            {
                if (fw.FORM.ContainsKey("delete"))
                {
                    model.deleteByTname(dict, id.toInt());
                    del_ctr += 1;
                }
            }
        }

        if (reqs("mode") == "edit")
        {
            // multirecord save
            ArrayList cols = model_tables.getColumns(defs);

            // go thru all existing rows
            Hashtable rows = reqh("row");
            Hashtable rowsdel = reqh("del");
            foreach (string key in rows.Keys)
            {
                string form_id = key;
                int id = form_id.toInt();
                if (id == 0)
                    continue; // skip wrong rows

                string md5 = (string)rows[key];
                // logger(form_id)
                Hashtable item = reqh("f" + form_id);
                Hashtable itemdb = [];
                // copy from form item to db item - only defined columns
                foreach (Hashtable col in cols)
                {
                    if (item.ContainsKey(col["name"]))
                        itemdb[(string)col["name"]] = item[col["name"]];
                }
                // check if this row need to be deleted
                if (rowsdel.ContainsKey(form_id))
                {
                    model.deleteByTname(dict, id);
                    del_ctr += 1;
                }
                else
                {
                    // existing row
                    model.updateByTname(dict, id, itemdb, md5);
                    fw.flash("updated", 1);
                }
            }

            // new rows
            rows = reqh("new");
            foreach (string key in rows.Keys)
            {
                string form_id = key;
                int id = form_id.toInt();
                if (id == 0)
                    continue; // skip wrong rows
                              // logger("new formid=" & form_id)

                Hashtable item = reqh("fnew" + form_id);
                Hashtable itemdb = [];
                bool is_row_empty = true;
                // copy from form item to db item - only defined columns
                foreach (Hashtable col in cols)
                {
                    if (item.ContainsKey(col["name"]))
                    {
                        itemdb[(string)col["name"]] = item[col["name"]];
                        if (!Utils.isEmpty(item[col["name"]]))
                            is_row_empty = false; // detect at least one non-empty value
                    }
                }

                // add new row, but only if at least one value is not empty
                if (!is_row_empty)
                {
                    model.addByTname(dict, itemdb);
                    fw.flash("updated", 1);
                }
            }
        }

        if (del_ctr > 0)
            fw.flash("multidelete", del_ctr);

        fw.redirect(base_url + "/?d=" + dict);
    }

    public Hashtable SaveSortAction()
    {
        fw.model<Users>().checkReadOnly();

        var ps = new Hashtable();
        ps["success"] = true;

        var sortdir = reqs("sortdir");
        var id = reqi("id");
        var under_id = reqi("under");
        var above_id = reqi("above");

        model.reorderPrio(defs, sortdir, id, under_id, above_id);

        return new Hashtable() { { "_json", ps } };
    }

    // TODO for lookup tables
    public Hashtable AutocompleteAction()
    {
        List<string> items = model_tables.getAutocompleteList(reqs("q"));

        return new Hashtable() { { "_json", items } };
    }
}
