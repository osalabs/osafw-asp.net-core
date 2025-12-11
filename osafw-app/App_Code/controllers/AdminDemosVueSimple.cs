// Demo Vue Simple controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class AdminDemosVueSimpleController : FwController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected DemoDicts model = null!;

    public override void init(FW fw)
    {
        base.init(fw);

        base_url = "/Admin/DemosVueSimple";
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_VUE");
        model = fw.model<DemoDicts>();
    }

    public FwDict IndexAction()
    {
        FwDict ps = [];
        if (!fw.isJsonExpected())
            return ps; //just load Vue app html

        var rows = model.list();
        ps["rows"] = rows;
        ps["_json"] = true;
        return ps;
    }
}
