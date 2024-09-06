// Reports Admin  controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class AdminReportsController : FwController
{
    public static new int access_level = Users.ACL_MANAGER;
    public static new string route_default_action = FW.ACTION_SHOW;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Admin/Reports"; // base url for the controller
    }

    public Hashtable IndexAction()
    {
        Hashtable ps = new();

        return ps;
    }

    public void ShowAction(string id)
    {
        Hashtable ps = [];
        ps["return_url"] = return_url;

        var repcode = FwReports.cleanupRepcode(id);
        var filter_session_key = "_filter_" + fw.G["controller.action"] + "." + repcode;

        if (reqs("doreset").Length > 0) {
            fw.Session(filter_session_key, "");
            fw.redirect(base_url + "/" + repcode);
        }

        var is_run = reqs("dofilter").Length > 0 || reqs("is_run").Length > 0;
        ps["is_run"] = is_run;

        // report filters (options)
        initFilter(filter_session_key);

        // get format directly form request as we don't need to remember format
        list_filter["format"] = reqh("f")["format"];
        if (Utils.isEmpty(list_filter["format"]))
            list_filter["format"] = "html";

        var report = FwReports.createInstance(fw, repcode, list_filter);

        report.setFilters(); // set filters data like select/lookups

        if (is_run)
            report.getData();

        // show or output report according format
        report.render(ps);
    }

    // save changes from editable reports
    public void SaveAction(string id)
    {
        route_onerror = FW.ACTION_SHOW;

        var repcode = FwReports.cleanupRepcode(id);

        var report = FwReports.createInstance(fw, repcode, reqh("f"));

        if (report.saveChanges())
            fw.redirect(base_url + "/" + repcode + "?is_run=1");
        else
        {
            fw.FORM["is_run"] = 1;
            fw.routeRedirect(FW.ACTION_SHOW, new string[] { repcode });
        }
    }
}