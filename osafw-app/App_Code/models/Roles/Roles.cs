// Roles model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

namespace osafw;

public class Roles : FwModel
{

    public const string INAME_VISITOR = "visitor";

    public Roles() : base()
    {
        db_config = "";
        table_name = "roles";
        field_prio = "prio";
    }

    public int idVisitor()
    {
        return oneByIname(INAME_VISITOR)[field_id].toInt();
    }

}