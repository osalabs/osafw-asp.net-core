using System.Collections;

namespace osafw;

public class AdminSimpleVueController : FwController
{
    public static new int access_level = Users.ACL_MANAGER;

    protected DemoDicts model;

    public override void init(FW fw)
    {
        base.init(fw);

        base_url = "/Admin/SimpleVue";
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_VUE");
        model = fw.model<DemoDicts>();
    }

    public override Hashtable IndexAction()
    {
        Hashtable ps = [];
        if (fw.isJsonExpected())
        {
            var rows = db.array(model.table_name,
                DB.h(model.field_status, db.opNOT(FwModel.STATUS_DELETED)),
                model.field_iname);
            ps["rows"] = rows;
            ps["_json"] = true;
            return ps;
        }

        return ps;
    }
}
