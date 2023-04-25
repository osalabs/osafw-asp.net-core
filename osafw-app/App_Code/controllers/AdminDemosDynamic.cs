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

        // override sortmap for date fields
        list_sortmap["fdate_pop_str"] = "fdate_pop";
    }

    //public override Hashtable ShowFormAction(int id = 0)
    //{
    //    var ps = base.ShowFormAction(id);
    //    if (is_dynamic_showform)
    //    {
    //        var hfields = _fieldsToHash((ArrayList)ps["fields"]);
    //        var def_subtable = (Hashtable)hfields["demo_dicts_subtable"];            
    //        var select_demo_dicts = model_related.listSelectOptions();
    //        foreach (Hashtable row in (ArrayList)def_subtable["list_rows"])
    //        {
    //            row["select_demo_dicts"] = select_demo_dicts;
    //        }
    //    }

    //    return ps;
    //}
}