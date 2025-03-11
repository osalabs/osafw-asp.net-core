// Demo model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class TDemos
{
    public int id { get; set; }

    [DBName("iname")]
    public string title { get; set; }
    public int fint { get; set; }
    public float ffloat { get; set; }
}

public class Demos : FwModel
{
    public Demos() : base()
    {
        table_name = "demos";
    }

    // check if item exists for a given email
    public override bool isExists(object uniq_key, int not_id)
    {
        return isExistsByField(uniq_key, not_id, "email");
    }

    public virtual ArrayList listSelectOptionsParent()
    {
        Hashtable where = new();
        where["parent_id"] = 0;
        where["status"] = db.opNOT(STATUS_DELETED);
        return db.array(table_name, where, "iname", Utils.qw("id iname"));
    }

    // demo for DB generics
    public decimal calcTotal(int id)
    {
        var item = db.row<TDemos>(table_name, DB.h("id", id));
        return (decimal)item.ffloat * item.fint;
    }
}