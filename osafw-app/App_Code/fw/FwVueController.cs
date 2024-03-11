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
        initFilter();

        // set standard output - load html with Vue app
        Hashtable ps = [];

        if (fw.isJsonExpected())
        {
            // if json expected - return data only as json
            ps["_json"] = true;
            setListSorting();

            setListSearch();
            setListSearchStatus();

            setViewList(ps, list_filter_search, false);

            //only select from db visible fields + id, save as comma-separated string into list_fields
            //TODO refactor into method setListFields?
            var headers = (ArrayList)ps["headers"]; //arraylist of hashtables, we need header["field_name"]
            var quoted_fields = new ArrayList();
            var is_id_in_fields = false;
            foreach (Hashtable header in headers)
            {
                var field_name = (string)header["field_name"];
                quoted_fields.Add(db.qid(field_name));
                if (field_name == model0.field_id)
                    is_id_in_fields = true;
            }
            //always include id field
            if (!is_id_in_fields && !Utils.isEmpty(model0.field_id))
                quoted_fields.Add(db.qid(model0.field_id));
            //join quoted_fields arraylist into comma-separated string
            list_fields = string.Join(",", quoted_fields.ToArray());

            getListRows();
            //TODO filter rows for json output
        }

        ps = setPS(ps);

        return ps;
    }

}
