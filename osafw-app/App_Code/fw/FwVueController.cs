// Fw Vue controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class FwVueController : FwController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected FwModel model_related;

    public override void init(FW fw)
    {
        base.init(fw);
        fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_VUE"); // layout for Vue pages
    }

    /// <summary>
    /// basically return layout/js to the browser, then Vue will load data via API
    /// </summary>
    /// <returns>Hashtable - related template will be parsed, null - no templates parsed (if action did all the output)</returns>
    public virtual Hashtable IndexAction()
    {
        // get filters from the search form
        this.initFilter();

        // set standard output
        var ps = this.setPS();

        return ps;
    }

}
