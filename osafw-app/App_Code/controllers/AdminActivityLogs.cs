// Activity Logs controller
// used only for:
// - accept post and save new user comments/notes or custom events
// - redirect back to entity page
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

namespace osafw;

public class AdminActivityLogsController : FwController
{
    public static new int access_level = Users.ACL_MEMBER; // any logged in user can add comments

    protected FwActivityLogs model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        model = fw.model<FwActivityLogs>();
        model0 = model;

        base_url = "/Admin/ActivityLogs";
        db = model.getDB();

        required_fields = "log_type entity item_id";
        save_fields = "reply_id item_id idate users_id idesc";

        //set default return url just for the case
        if (Utils.isEmpty(return_url))
            return_url = fw.config("LOGGED_DEFAULT_URL").toStr();
    }

    public virtual FwDict? IndexAction()
    {
        //if error - save it to flash as we doing redirect
        if (!Utils.isEmpty(fw.G["err_msg"]))
        {
            fw.flash("error", fw.G["err_msg"]!);
            if (fw.FormErrors.Count > 0) logger(fw.FormErrors);
        }
        fw.redirect(return_url);
        return null;
    }

    //save new comment/note or custom event
    //requires
    public virtual FwDict? SaveAction(int id = 0)
    {
        route_onerror = FW.ACTION_INDEX;

        fw.model<Users>().checkReadOnly();
        if (reqb("refresh"))
        {
            fw.routeRedirect(FW.ACTION_SHOW_FORM, [id]);
            return null;
        }

        FwDict item = reqh("item");
        var success = true;
        var is_new = (id == 0);

        Validate(id, item);
        // load old record if necessary
        // var itemOld = model0.one(id);

        FwDict itemdb = FormUtils.filter(item, this.save_fields);
        FormUtils.filterCheckboxes(itemdb, item, save_fields_checkboxes, isPatch());

        //for new items - convert log_type and entity to ids
        if (is_new)
        {
            var log_type = fw.model<FwLogTypes>().oneByIcode(item["log_type"].toStr());
            if (log_type.Count == 0 || log_type["itype"].toInt() != FwLogTypes.ITYPE_USER)
                throw new UserException("Invalid log_type");

            var fwentity = fw.model<FwEntities>().oneByIcode(item["entity"].toStr());
            if (fwentity.Count == 0)
                throw new UserException("Invalid entity");
            // TODO Customize - check if entity is allowed for this log_type
            // TODO Customize - check if logged user can add comments for this entity

            itemdb["log_types_id"] = log_type["id"];
            itemdb["fwentities_id"] = fwentity["id"];
        }

        if (!Utils.isDate(itemdb["idate"]))
            itemdb["idate"] = DB.NOW; //if no date specified - use current date
        if (itemdb["users_id"].toInt() == 0)
            if (fw.userId > 0)
                itemdb["users_id"] = fw.userId; //if no user specified - use current user, unless visitor

        id = this.modelAddOrUpdate(id, itemdb);

        return this.afterSave(success, id, is_new);
    }

    public virtual void Validate(int id, FwDict item)
    {
        bool result = this.validateRequired(id, item, this.required_fields);

        // comment or user event should be related to some item
        if (result && item["item_id"].toInt() == 0)
            fw.FormErrors["REQUIRED"] = true;

        // If result AndAlso Not SomeOtherValidation() Then
        // FW.FERR("other field name") = "HINT_ERR_CODE"
        // End If

        this.validateCheckResult();
    }

}