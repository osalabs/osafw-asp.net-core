// Base Admin screens controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class FwAdminController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;
    //public static new string route_default_action = FW.ACTION_INDEX; //empty|Index|Show - calls IndexAction or ShowAction accordingly if no requested controller action found. If empty (default) - show template from /cur_controller/cur_action dir

    public override void init(FW fw)
    {
        base.init(fw);
    }

    public virtual Hashtable IndexAction()
    {
        // get filters from the search form
        Hashtable f = this.initFilter();

        this.setListSorting();

        this.setListSearch();
        this.setListSearchStatus(); // status field is not always in table, so keep it separate
                                    // set here non-standard search
                                    // If f("field") > "" Then
                                    // Me.list_where &= " and field=" & db.q(f("field"))
                                    // End If

        this.getListRows();
        // add/modify rows from db if necessary
        // For Each row As Hashtable In Me.list_rows
        // row("field") = "value"
        // Next

        // set standard output parse strings
        var ps = this.setPS();

        // userlists support if necessary
        if (this.is_userlists)
            this.setUserLists(ps);

        return ps;
    }

    public virtual Hashtable ShowAction(int id)
    {
        Hashtable ps = new();
        var item = model0.one(id);
        if (item.Count == 0)
            throw new NotFoundException();

        setAddUpdUser(ps, item);

        // userlists support if necessary
        if (this.is_userlists)
            this.setUserLists(ps, id);

        ps["id"] = id;
        ps["i"] = item;
        ps["return_url"] = return_url;
        ps["related_id"] = related_id;
        ps["base_url"] = base_url;
        ps["is_userlists"] = is_userlists;
        ps["is_activity_logs"] = is_activity_logs;
        ps["is_readonly"] = is_readonly;

        return ps;
    }

    /// <summary>
    /// Shows editable Form for adding or editing one entity row
    /// </summary>
    /// <param name="id"></param>
    /// <returns>in Hashtable:
    /// id - id of the entity
    /// i - hashtable of entity fields
    /// </returns>
    /// <remarks></remarks>
    public virtual Hashtable ShowFormAction(int id = 0)
    {
        Hashtable ps = [];
        var item = reqh("item"); // set defaults from request params

        if (isGet())
        {
            if (id > 0)
            {
                // edit screen
                item = model0.one(id);
            }
            else
            {
                // add new screen
                Hashtable item_new = [];
                Utils.mergeHash(item_new, form_new_defaults); // use hardcoded defaults if any
                Utils.mergeHash(item_new, item); // override with passed defaults
                item = item_new;
            }
        }
        else
        {
            // read from db
            Hashtable itemdb = model0.one(id);
            // and merge new values from the form
            Utils.mergeHash(itemdb, item);
            item = itemdb;
        }

        setAddUpdUser(ps, item);

        ps["id"] = id;
        ps["i"] = item;
        ps["return_url"] = return_url;
        ps["related_id"] = related_id;
        ps["is_readonly"] = is_readonly;
        if (fw.FormErrors.Count > 0)
            logger(fw.FormErrors);

        return ps;
    }

    public virtual Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_SHOW_FORM;
        // checkXSS() 'no need to check in standard SaveAction, but add to your custom actions that modifies data
        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");

        fw.model<Users>().checkReadOnly();
        if (reqi("refresh") == 1 && (id == 0)) //for new record - just refresh the form, for existing - also try to save
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, new object[] { id });
            return null;
        }

        Hashtable item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // Dim item_old As Hashtable = model0.one(id)

        Hashtable itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());
        FormUtils.filterNullable(itemdb, save_fields_nullable);

        id = this.modelAddOrUpdate(id, itemdb);

        return this.afterSave(success, id, is_new);
    }

    public virtual void Validate(int id, Hashtable item)
    {
        bool result = this.validateRequired(id, item, this.required_fields);

        // If result AndAlso model0.isExists(item("iname"), id) Then
        // fw.FERR("iname") = "EXISTS"
        // End If

        // If result AndAlso Not SomeOtherValidation() Then
        // FW.FERR("other field name") = "HINT_ERR_CODE"
        // End If

        this.validateCheckResult();
    }

    public virtual void ShowDeleteAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        var ps = new Hashtable()
        {
            {"i", model0.one(id)},
            {"related_id", this.related_id},
            {"return_url", this.return_url},
            {"base_url", this.base_url},
        };

        fw.parser("/common/form/showdelete", ps);
    }

    public virtual Hashtable DeleteAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        model0.deleteWithPermanentCheck(id);
        fw.flash("onedelete", 1);
        return this.afterSave(true);
    }

    public virtual Hashtable RestoreDeletedAction(int id)
    {
        fw.model<Users>().checkReadOnly();

        model0.update(id, new Hashtable() { { model0.field_status, FwModel.STATUS_ACTIVE } });

        fw.flash("record_updated", 1);
        return this.afterSave(true, id);
    }

    public virtual Hashtable SaveMultiAction()
    {
        route_onerror = FW.ACTION_INDEX;

        Hashtable cbses = reqh("cb");
        bool is_delete = fw.FORM.ContainsKey("delete");
        if (is_delete)
            fw.model<Users>().checkReadOnly();

        int user_lists_id = reqi("addtolist");
        var remove_user_lists_id = reqi("removefromlist");

        if (user_lists_id > 0)
        {
            var user_lists = fw.model<UserLists>().one(user_lists_id);
            if (user_lists.Count == 0 || user_lists["add_users_id"].toInt() != fw.userId)
                throw new UserException("Wrong Request");
        }

        int ctr = saveMultiRows(cbses.Keys, is_delete, user_lists_id, remove_user_lists_id);

        saveMultiResult(ctr, is_delete, user_lists_id, remove_user_lists_id);

        return this.afterSave(true, new Hashtable() { { "ctr", ctr } });
    }

}