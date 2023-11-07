// Activity Logs controller
// used only for:
// - accept post and save new user comments/notes or custom events
// - redirect back to entity page
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class AdminActivityLogsController : FwController
{
    public static new int access_level = Users.ACL_MEMBER; // any logged in user can add comments

    protected ActivityLogs model;

    public override void init(FW fw)
    {
        base.init(fw);
        model0 = fw.model<ActivityLogs>();
        model = model0 as ActivityLogs;

        base_url = "/Admin/ActivityLogs";
        db = model.getDB();

        required_fields = "log_type entity item_id";
        save_fields = "reply_id item_id idate users_id idesc";
        save_fields_nullable = "reply_id item_id idate users_id idesc";

        //set default return url just for the case
        if (Utils.isEmpty(return_url))
            return_url = (string)fw.config("LOGGED_DEFAULT_URL");
    }

    public virtual Hashtable IndexAction()
    {
        //if error - save it to flash as we doing redirect
        if (!Utils.isEmpty(fw.G["err_msg"]))
        {
            fw.flash("error", fw.G["err_msg"]);
            if (fw.FormErrors.Count > 0) logger(fw.FormErrors);
        }
        fw.redirect(return_url);
        return null;
    }

    //save new comment/note or custom event
    //requires 
    public virtual Hashtable SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_INDEX;

        fw.model<Users>().checkReadOnly();
        if (reqi("refresh") == 1)
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
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes);
        FormUtils.filterNullable(itemdb, save_fields_nullable);

        //for new items - convert log_type and entity to ids
        if (is_new)
        {
            var log_type = fw.model<LogTypes>().oneByIcode(Utils.f2str(item["log_type"]));
            if (log_type.Count == 0 || Utils.f2int(log_type["itype"]) != LogTypes.ITYPE_USER)
                throw new UserException("Invalid log_type");

            var fwentity = fw.model<FwEntities>().oneByIcode(Utils.f2str(item["entity"]));
            if (fwentity.Count == 0)
                throw new UserException("Invalid entity");
            // TODO Customize - check if entity is allowed for this log_type
            // TODO Customize - check if logged user can add comments for this entity

            itemdb["log_types_id"] = log_type["id"];
            itemdb["fwentities_id"] = fwentity["id"];
        }

        if (Utils.f2date(itemdb["idate"]) == null)
            itemdb["idate"] = DB.NOW; //if no date specified - use current date
        if (Utils.f2int(itemdb["users_id"]) == 0)
            itemdb["users_id"] = fw.userId; //if no user specified - use current user

        id = this.modelAddOrUpdate(id, itemdb);

        return this.afterSave(success, id, is_new);
    }

    public virtual void Validate(int id, Hashtable item)
    {
        bool result = this.validateRequired(item, this.required_fields);

        // comment or user event should be related to some item
        if (result && Utils.f2int(item["item_id"]) == 0)
            fw.FormErrors["REQUIRED"] = true;

        // If result AndAlso Not SomeOtherValidation() Then
        // FW.FERR("other field name") = "HINT_ERR_CODE"
        // End If

        this.validateCheckResult();
    }

}