// My Feedback controller
// when user post feedback - send it to the support_email
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

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

        is_readonly = false;//allow for all
    }

    public void IndexAction()
    {
        throw new ApplicationException("Not Implemented");
    }

    public FwDict? SaveAction()
    {
        var item = reqh("item");
        var id = fw.userId;

        Validate(id, item);
        // load old record if necessary
        // var itemOld = model.one(id);

        FwDict itemdb = FormUtils.filter(item, save_fields);
        var user = fw.model<Users>().one(id);
        FwDict ps = new()
            {
                { "user", user },
                { "i", itemdb },
                { "url", return_url }
            };
        fw.sendEmailTpl(fw.config("feedback_email").toStr(), "feedback.txt", ps, null, null, user["email"]);

        fw.flash("success", "Feedback sent. Thank you.");

        return afterSave(true);
    }

    public virtual void Validate(int id, FwDict item)
    {
        bool result = this.validateRequired(id, item, this.required_fields);

        this.validateCheckResult();
    }
}