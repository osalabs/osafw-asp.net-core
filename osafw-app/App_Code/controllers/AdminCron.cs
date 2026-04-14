// Cron Admin controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class AdminCronController : FwDynamicController
{
    public static new int access_level = Users.ACL_ADMIN;

    protected FwCron model = null!;

    public override void init(FW fw)
    {
        base.init(fw);
        // use if config doesn't contains model name
        // model = fw.model<FwCron>();
        // model0 = model;

        base_url = "/Admin/Cron";
        this.loadControllerConfig();
        model = model0 as FwCron ?? throw new FwConfigUndefinedModelException();
        db = model.getDB(); // model-based controller works with model's db

        is_activity_logs = FwCron.IS_TRACK_JOB_RUN_IN_ACTIVITY_LOGS;
    }

    public override void getListRows()
    {
        base.getListRows();

        foreach (FwDict row in list_rows)
        {
            var ced = model.getCronExpressionDescriptor(row["cron"].toStr());
            row["cron_human"] = ced.is_error ? ced.error_msg : ced.cron_human;
        }
    }

    public override FwDict ShowAction(int id = 0)
    {
        var ps = base.ShowAction(id) ?? [];
        setCommonPS(ref ps);
        return ps;
    }

    public override FwDict ShowFormAction(int id = 0)
    {
        var ps = base.ShowFormAction(id) ?? [];
        setCommonPS(ref ps);
        return ps;
    }

    public void ManualRunAction(int id = 0)
    {
        checkXSS();

        var job = model.oneJob(id);

        try
        {
            if (job == null)
                throw new NotFoundException("Wrong Cron Job ID");

            if (job.is_running)
                throw new ApplicationException("The job is currently running. Do nothing.");

            model.runJob(job, is_manual_run: true);

            fw.flash("success", "Manual Run Performed");
        }
        catch (Exception ex)
        {
            fw.flash("error", ex.Message);
        }

        fw.redirect(base_url + "/" + id);
    }

    public void SetStatusAction(int id = 0)
    {
        checkXSS();
        var status = reqi("status");

        if (status == FwCron.STATUS_ACTIVE || status == FwCron.STATUS_INACTIVE)
        {
            model.update(id, DB.h("status", status));
        }

        fw.redirect(base_url + "/" + id);
    }

    public void ResetIsRunningAction(int id = 0)
    {
        checkXSS();

        model.resetIsRunning(id);

        fw.redirect(base_url + "/" + id);
    }

    public override int modelAddOrUpdate(int id, FwDict fields)
    {
        // Re/Calculate next run immediately
        // 1. make sure the record is under update status, so the Cron Service will not pick it up during the update
        var status_save = fields["status"].toInt();
        fields["status"] = FwCron.STATUS_UNDER_UPDATE;

        id = base.modelAddOrUpdate(id, fields);

        model.updateNextRun(id);

        // 2. re-read the updated job record
        var job = model.oneJob(id);

        // 3. revert the status if recalcutaion didn't set it to Completed
        if (job?.status != FwCron.STATUS_COMPLETED)
            model.update(id, DB.h("status", status_save));

        return id;
    }

    public override void Validate(int id, FwDict item)
    {
        if (!model.isValidCronExpression(item["cron"].toStr()))
        {
            fw.FormErrors["cron"] = "INVALID";
        }

        base.Validate(id, item);
    }

    private void setCommonPS(ref FwDict ps)
    {
        var item = ps["i"] as FwDict ?? [];
        var fields = ps["fields"] as FwList ?? [];

        var ced = model.getCronExpressionDescriptor(item["cron"].toStr());

        var def_cron_human = defByFieldname("cron_human", fields);
        def_cron_human?["value"] = ced.is_error ? ced.error_msg : ced.cron_human;
    }

}