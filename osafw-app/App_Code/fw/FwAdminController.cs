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
    // Public Shared Shadows route_default_action As String = "index" 'empty|index|show - calls IndexAction or ShowAction accordingly if no requested controller action found. If empty (default) - show template from /cur_controller/cur_action dir

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
        Hashtable item = model0.one(id);
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
        Hashtable ps = new();
        var item = reqh("item"); // set defaults from request params

        if (isGet())
        {
            if (id > 0)
            {
                item = model0.one(id);
            }
            else
            {
                // override any defaults here
                Utils.mergeHash(item, this.form_new_defaults);
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
        if (fw.FormErrors.Count > 0)
            logger(fw.FormErrors);

        return ps;
    }

    public virtual Hashtable SaveAction(int id = 0)
    {
        // checkXSS() 'no need to check in standard SaveAction, but add to your custom actions that modifies data
        if (this.save_fields == null)
            throw new Exception("No fields to save defined, define in Controller.save_fields");

        if (reqi("refresh") == 1)
        {
            fw.routeRedirect("ShowForm", new object[] { id });
            return null;
        }

        Hashtable item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        try
        {
            Validate(id, item);
            // load old record if necessary
            // Dim item_old As Hashtable = model0.one(id)

            Hashtable itemdb = FormUtils.filter(item, this.save_fields);
            FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes);
            FormUtils.filterNullable(itemdb, save_fields_nullable);

            id = this.modelAddOrUpdate(id, itemdb);
        }
        catch (ApplicationException ex)
        {
            success = false;
            this.setFormError(ex);
        }

        return this.afterSave(success, id, is_new);
    }

    public virtual void Validate(int id, Hashtable item)
    {
        bool result = this.validateRequired(item, this.required_fields);

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
        model0.deleteWithPermanentCheck(id);
        fw.flash("onedelete", 1);
        return this.afterSave(true);
    }

    public virtual Hashtable SaveMultiAction()
    {
        Hashtable cbses = reqh("cb");
        bool is_delete = fw.FORM.ContainsKey("delete");
        int user_lists_id = reqi("addtolist");
        var remove_user_lists_id = reqi("removefromlist");
        int ctr = 0;

        if (user_lists_id > 0)
        {
            var user_lists = fw.model<UserLists>().one(user_lists_id);
            if (user_lists.Count == 0 || Utils.f2int(user_lists["add_users_id"]) != fw.userId)
                throw new UserException("Wrong Request");
        }

        foreach (string id1 in cbses.Keys)
        {
            var id = Utils.f2int(id1);
            if (is_delete)
            {
                model0.deleteWithPermanentCheck(id);
                ctr += 1;
            }
            else if (user_lists_id > 0)
            {
                fw.model<UserLists>().addItemList(user_lists_id, id);
                ctr += 1;
            }
            else if (remove_user_lists_id > 0)
            {
                fw.model<UserLists>().delItemList(remove_user_lists_id, id);
                ctr += 1;
            }
        }

        if (is_delete)
            fw.flash("multidelete", ctr);
        if (user_lists_id > 0)
            fw.flash("success", ctr + " records added to the list");

        return this.afterSave(true, new Hashtable() { { "ctr", ctr } });
    }
}