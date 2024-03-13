// Fw Vue controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

        if (fw.isJsonExpected() || export_format.Length > 0)
        {
            //do db work only if json or export expected

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

            // if export - no need to parse templates and prep for them - just return empty hashtable asap
            if (export_format.Length > 0)
                return []; // return empty hashtable just in case action overriden to avoid check for null


            //TODO filter rows for json output

            // userlists support if necessary
            if (this.is_userlists)
                this.setUserLists(ps);

            ps["XSS"] = fw.Session("XSS");
            ps["access_level"] = fw.userAccessLevel;
            //TODO only specific from global ps["global"] = fw.G;
        }

        ps = setPS(ps);

        return ps;
    }


    //TODO refactor with FwDynamicController same method to deduplicate code
    public virtual Hashtable SaveUserViewsAction()
    {
        var fld = reqh("fld");
        var load_id = reqi("load_id");
        var is_reset = reqi("is_reset");
        var density = reqs("density");

        if (load_id > 0)
            // set fields from specific view
            fw.model<UserViews>().setViewForIcode(base_url, load_id);
        else if (is_reset == 1)
            // reset fields to defaults
            fw.model<UserViews>().updateByIcodeFields(base_url, view_list_defaults);
        else if (density.Length > 0)
        {
            // save density
            // validate density can be only table-sm, table-dense, table-normal, otherwise - set empty
            if (!"table-sm table-dense table-normal".Contains(density))
                density = "";
            fw.model<UserViews>().updateByIcode(base_url, DB.h("density", density));
        }
        else
        {
            var item = reqh("item");
            var iname = Utils.f2str(item["iname"]);

            // save fields
            // order by value
            var ordered = fld.Cast<DictionaryEntry>().OrderBy(entry => Utils.f2int(entry.Value)).ToList();
            // and then get ordered keys
            List<string> anames = new();
            foreach (var el in ordered)
                anames.Add((string)el.Key);
            var fields = string.Join(" ", anames);

            if (!string.IsNullOrEmpty(iname))
            {
                // create new view by name or update if this name exists
                fw.model<UserViews>().addOrUpdateByUK(base_url, fields, iname);
            }
            // update default view with fields
            fw.model<UserViews>().updateByIcodeFields(base_url, fields);
        }

        return afterSave(true, null, false, "no_action", return_url);
    }

}
