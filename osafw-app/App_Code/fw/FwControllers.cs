// FwControllers model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

namespace osafw;

public class FwControllers : FwModel
{
    public FwControllers() : base()
    {
        db_config = "";
        table_name = "fwcontrollers";
    }

    public DBList listGrouped()
    {
        return db.array(table_name, new FwDict
        {
            ["status"] = db.opNOT(STATUS_DELETED),
            ["access_level"] = db.opLE(fw.userAccessLevel)
        }, "igroup, iname");
    }
}