// Demo Dynamic Admin controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class AdminDemosDynamicController : FwDynamicController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected Demos model;

    public override void init(FW fw)
    {
        base.init(fw);
        // use if config doesn't contains model name
        // model0 = fw.model(Of Demos)()
        // model = model0

        base_url = "/Admin/DemosDynamic";
        this.loadControllerConfig();
        model = model0 as Demos;
        db = model.getDB(); // model-based controller works with model's db

        model_related = fw.model<DemoDicts>();
        is_userlists = true;
        is_activity_logs = true;  //enable work with activity_logs (comments, history)

        // override sortmap for date fields
        // list_sortmap["fdate_pop_str"] = "fdate_pop";
    }

    // override as we need to preset parent_demo_dicts_id filter from parent item
    public override Hashtable modelOne(int id)
    {
        var item = base.modelOne(id);
        var parent_item = fw.model<Demos>().one(item["parent_id"]);
        item["parent_demo_dicts_id"] = parent_item["demo_dicts_id"];
        return item;
    }
}