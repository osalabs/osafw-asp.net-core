// Demo Dynamic Admin controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

namespace osafw;

public class AdminDemosDynamicController : FwDynamicController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected Demos model = null!;
    protected const string PAGE_HEADER_KEY = "page_header_mode";

    public override void init(FW fw)
    {
        base.init(fw);
        // use if config doesn't contains model name
        // model = fw.model<Demos>();
        // model0 = model;

        base_url = "/Admin/DemosDynamic";
        this.loadControllerConfig();
        model = model0 as Demos ?? throw new FwConfigUndefinedModelException();
        db = model.getDB(); // model-based controller works with model's db

        model_related = fw.model<DemoDicts>();
        is_userlists = true;
        is_activity_logs = true;  //enable work with activity_logs (comments, history)

        // override sortmap for additional computed fields
        // allow sorting by display-friendly date to map to real DB field
        list_sortmap["fdate_pop_str"] = "fdate_pop";
    }

    public override FwDict IndexAction()
    {
        var ps = base.IndexAction();
        applyPageHeader(ps, "list");
        ps["page_header_count"] = ps["count"];
        return ps;
    }

    public override FwDict? ShowAction(int id = 0)
    {
        var ps = base.ShowAction(id);
        if (ps != null)
            applyPageHeader(ps, "view");
        return ps;
    }

    public override FwDict? ShowFormAction(int id = 0)
    {
        var ps = base.ShowFormAction(id);
        if (ps != null)
        {
            applyPageHeader(ps, "edit");
            ps["page_header_is_new"] = id == 0;
        }
        return ps;
    }

    protected virtual void applyPageHeader(FwDict ps, string mode)
    {
        ps[PAGE_HEADER_KEY] = mode;
        ps["page_header_title"] = ps["title"];
    }
}
