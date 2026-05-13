// Demo model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;

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

    /// <summary>
    /// Returns parent demo options while using the framework's active-plus-selected inactive lookup rule.
    /// </summary>
    /// <param name="def">Dynamic field definition or lookup parameters.</param>
    /// <param name="where">Additional predicates to apply to the parent lookup.</param>
    /// <param name="selected_id">Explicit selected parent id or ids to preserve on edit forms.</param>
    /// <param name="valueFromIname">When true, use names as option values.</param>
    /// <param name="inameSql">Optional label SQL expression to pass through to the framework lookup implementation.</param>
    /// <returns>Parent demo option rows.</returns>
    public virtual FwList listSelectOptionsParent(FwDict? def = null, FwDict? where = null, object? selected_id = null, bool valueFromIname = false, string? inameSql = null)
    {
        var baseWhere = where != null ? new FwDict(where) : [];
        baseWhere["parent_id"] = 0;

        return base.listSelectOptions(def, selected_id, valueFromIname, baseWhere, inameSql);
    }

    /// <summary>
    /// Processes demo-specific lookup parameters before returning framework-standard select options.
    /// </summary>
    /// <param name="def">Dynamic field definition or lookup parameters.</param>
    /// <param name="selected_id">Explicit selected id or ids to preserve on edit forms.</param>
    /// <param name="valueFromIname">When true, use names as option values.</param>
    /// <param name="baseWhere">Optional base predicates to pass through to the framework lookup implementation.</param>
    /// <param name="inameSql">Optional label SQL expression to pass through to the framework lookup implementation.</param>
    /// <returns>Demo option rows.</returns>
    public override FwList listSelectOptions(FwDict? def = null, object? selected_id = null, bool valueFromIname = false, FwDict? baseWhere = null, string? inameSql = null)
    {
        var lookup_params = (def?["lookup_params"] ?? string.Empty).toStr();
        var hparams = Utils.qh(lookup_params); // ex: parent

        if (hparams.ContainsKey("parent"))
            return listSelectOptionsParent(def, baseWhere, selected_id, valueFromIname, inameSql);

        return base.listSelectOptions(def, selected_id, valueFromIname, baseWhere, inameSql);
    }

    /// <summary>
    /// Demonstrates typed DB row materialization by calculating a total from numeric demo fields.
    /// </summary>
    /// <param name="id">Primary key of the demo record whose numeric fields should be multiplied.</param>
    /// <returns>The demo row's floating-point value multiplied by its integer value.</returns>
    /// <exception cref="NotFoundException">Thrown when the requested demo record does not exist.</exception>
    public decimal calcTotal(int id)
    {
        var item = db.row<Row>(table_name, DB.h("id", id)) ?? throw new NotFoundException();
        return (decimal)item.ffloat * item.fint;
    }
}
