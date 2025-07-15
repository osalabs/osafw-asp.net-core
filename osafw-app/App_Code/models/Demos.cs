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

    public virtual ArrayList listSelectOptionsParent(Hashtable def = null, Hashtable where = null)
    {
        where ??= [];

        where["parent_id"] = 0;
        where["status"] = db.opNOT(STATUS_DELETED);

        // Support filter_by/filter_field from config
        if (def != null && def.ContainsKey("filter_by") && def.ContainsKey("filter_field"))
        {
            var item = def["i"] as Hashtable ?? [];
            var filter_by = def["filter_by"].toStr();
            var filter_field = def["filter_field"].toStr();
            if (item.ContainsKey(filter_by))
                where[filter_field] = item[filter_by];
        }

        return db.array(table_name, where, "iname", Utils.qw("id iname"));
    }

    // override to process custom lookup_params
    public override ArrayList listSelectOptions(Hashtable def = null)
    {
        var lookup_params = def["lookup_params"].toStr();
        var hparams = Utils.qh(lookup_params); // ex: parent

        if (hparams.ContainsKey("parent"))
            return listSelectOptionsParent(def);

        return base.listSelectOptions(def);
    }

    // demo for DB generics
    public decimal calcTotal(int id)
    {
        var item = db.row<TDemos>(table_name, DB.h("id", id));
        return (decimal)item.ffloat * item.fint;
    }
}