// Reports Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class AdminReportsController : FwController
{
    public static new int access_level = Users.ACL_MANAGER;
    public static new string route_default_action = FW.ACTION_SHOW;

    public override void init(FW fw)
    {
        base.init(fw);
        required_fields = "iname"; // default required fields, space-separated
        base_url = "/Admin/Reports"; // base url for the controller
    }

    public Hashtable IndexAction()
    {
        Hashtable ps = new();

        return ps;
    }

    public void ShowAction(string repcode)
    {
        Hashtable ps = new();
        repcode = FwReports.cleanupRepcode(repcode);

        var is_run = reqs("dofilter").Length > 0 || reqs("is_run").Length > 0;
        ps["is_run"] = is_run;

        // report filters (options)
        Hashtable f = initFilter("AdminReports." + repcode);

        // get format directly form request as we don't need to remember format
        f["format"] = reqh("f")["format"];
        if (Utils.isEmpty(f["format"]))
            f["format"] = "html";

        var report = FwReports.createInstance(fw, repcode, f);

        ps["filter"] = report.getReportFilters(); // filter data like select/lookups
        ps["f"] = report.f; // filter values

        if (is_run)
            ps["rep"] = report.getReportData();

        // show or output report according format
        report.render(ps);
    }

    // save changes from editable reports
    public void SaveAction()
    {
        route_onerror = FW.ACTION_SHOW;

        var repcode = FwReports.cleanupRepcode(reqs("repcode"));

        var report = FwReports.createInstance(fw, repcode, reqh("f"));

        if (report.saveChanges())
            fw.redirect(base_url + "/" + repcode + "?is_run=1");
        else
        {
            fw.FORM["is_run"] = 1;
            fw.routeRedirect(FW.ACTION_SHOW, new String[] { repcode });
        }
    }
}