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

    public virtual ArrayList listSelectOptionsParent(Hashtable where = null)
    {
        where ??= [];

        where["parent_id"] = 0;
        where["status"] = db.opNOT(STATUS_DELETED);

        return db.array(table_name, where, "iname", Utils.qw("id iname"));
    }

    // override to process custom lookup_params
    public override ArrayList listSelectOptions(Hashtable def = null)
    {
        var item = def["i"] as Hashtable;
        item ??= [];

        var lookup_params = def["lookup_params"].toStr();
        logger("LOOKUP PARAMS: ", lookup_params);
        var hparams = Utils.qh(lookup_params); // ex: parent demo_dicts_id|parent_demo_dicts_id
        var where = new Hashtable();
        if (hparams.ContainsKey("demo_dicts_id"))
        {
            var field_name = hparams["demo_dicts_id"].toStr();
            where["demo_dicts_id"] = item[field_name];
        }

        if (hparams.ContainsKey("parent"))
            return listSelectOptionsParent(where);

        return base.listSelectOptions(def);
    }

    // demo for DB generics
    public decimal calcTotal(int id)
    {
        var item = db.row<TDemos>(table_name, DB.h("id", id));
        return (decimal)item.ffloat * item.fint;
    }
}