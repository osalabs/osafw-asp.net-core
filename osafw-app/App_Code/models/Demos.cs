// Demo model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class Demos : FwModel<Demos.Row>
{
    public class Row
    {
        public int id { get; set; }
        public int parent_id { get; set; }
        public int? demo_dicts_id { get; set; }
        public string iname { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public int fint { get; set; }
        public double ffloat { get; set; }
        public int dict_link_auto_id { get; set; }
        public string dict_link_multi { get; set; } = string.Empty;
        public int fcombo { get; set; }
        public int fradio { get; set; }
        private int _fyesno;
        public bool fyesno
        {
            get { return _fyesno != 0; }
            set { _fyesno = value ? 1 : 0; }
        }
        public int is_checkbox { get; set; }
        public DateTime? fdate_combo { get; set; }
        public DateTime? fdate_pop { get; set; }
        public DateTime? fdatetime { get; set; }
        public int ftime { get; set; }
        public int? att_id { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public Demos() : base()
    {
        table_name = "demos";
    }

    // check if item exists for a given email
    public override bool isExists(object uniq_key, int not_id)
    {
        return isExistsByField(uniq_key, not_id, "email");
    }

    public virtual ArrayList listSelectOptionsParent(Hashtable? def = null, Hashtable? where = null)
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
        var item = db.row<Row>(table_name, DB.h("id", id));
        return (decimal)item.ffloat * item.fint;
    }
}