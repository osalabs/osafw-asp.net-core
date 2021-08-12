// Demo model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw
{
    public class Demos : FwModel
    {
        public string table_link = "demos_demo_dicts_link";

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
            return db.array("select id, iname from " + this.table_name + " where parent_id=0 and status<>127 order by iname");
        }
    }
}