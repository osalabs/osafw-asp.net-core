using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
{
    public class FwController
    {
        public static int access_level = -1; // access level for the controller. fw.config("access_levels") overrides this. -1 (public access), 0(min logged level), 100(max admin level)

        public static string route_default_action;   // supported values - "" (use Default Parser for unknown actions), index (use IndexAction for unknown actions), show (assume action is id and use ShowAction)
        public string base_url;               // base url for the controller
        public string base_url_suffix;        // additional base url suffix

        public Hashtable form_new_defaults;   // optional, defaults for the fields in new form
        public string required_fields;        // optional, default required fields, space-separated
        public string save_fields;            // required, fields to save from the form to db, space-separated
        public string save_fields_checkboxes; // optional, checkboxes fields to save from the form to db, qw string: "field|def_value field2|def_value2"
        public string save_fields_nullable;   // optional, nullable fields that should be set to null in db if form submit as ''

        protected FW fw;
        protected DB db;
        protected FwModel model0;
        protected Hashtable config;           // controller config, loaded from template dir/config.json

        protected string list_view;                  // table/view to use in list sql, if empty model0.table_name used
        protected string list_orderby;               // orderby for the list screen
        protected Hashtable list_filter;             // filter values for the list screen
        protected string list_where = " 1=1 ";       // where to use in list sql, default is non-deleted records (see setListSearch() )
        protected int list_count;                    // count of list rows returned from db
        protected ArrayList list_rows;               // list rows returned from db (array of hashes)
        protected ArrayList list_pager;              // pager for the list from FormUtils.getPager
        protected string list_sortdef;               // required for Index, default list sorting: name asc|desc
        protected Hashtable list_sortmap;            // required for Index, sortmap fields
        protected string search_fields;              // optional, search fields, space-separated 
        // fields to search via $s=list_filter("s"), ! - means exact match, not "like"
        // format: "field1 field2,!field3 field4" => field1 LIKE '%$s%' or (field2 LIKE '%$s%' and field3='$s') or field4 LIKE '%$s%'

        // support of customizable view list
        // map of fileld names to screen names
        protected bool is_dynamic_index = false;   // true if controller has dynamic IndexAction, then define below:
        protected string view_list_defaults = "";  // qw list of default columns
        protected Hashtable view_list_map;         // list of all available columns fieldname|visiblename
        protected string view_list_custom = "";    // qw list of custom-formatted fields for the list_table

        protected bool is_dynamic_show = false;     //true if controller has dynamic ShowAction, requires "show_fields" to be defined in config.json
        protected bool is_dynamic_showform = false; //true if controller has dynamic ShowFormAction, requires "showform_fields" to be defined in config.json

        protected string return_url;                 // url to return after SaveAction successfully completed, passed via request
        protected string related_id;                 // related id, passed via request. Controller should limit view to items related to this id
        protected string related_field_name;         // if set (in Controller) and $related_id passed - list will be filtered on this field


        protected FwController(FW fw = null) {
            if (fw != null) {
                this.fw = fw;
                this.db = fw.db;
            }
        }

        public virtual void init(FW fw) {
            this.fw = fw;
            this.db = fw.db;

            return_url = reqs("return_url");
            related_id = reqs("related_id");
        }

        // set of helper functions to return string, integer, date values from request (fw.FORM)
        public Object req(String iname) {
            return fw.FORM[iname];
        }
        public Hashtable reqh(String iname) {
            if (fw.FORM[iname] != null && fw.FORM[iname].GetType() == typeof(Hashtable)) {
                return (Hashtable)fw.FORM[iname];
            } else {
                return new Hashtable();
            }
        }

        public string reqs(string iname) {
            string value = (string)fw.FORM[iname];
            if (value == null) value = "";
            return value;
        }
        public int reqi(string iname) {
            return 0;// Utils.f2int(fw.FORM(iname))
        }
        public Object reqd(string iname) {
            return new DateTime(); //Utils.f2date(fw.FORM(iname))
        }

        public void rw(string str) {
            fw.rw(str);
        }

        public virtual Hashtable IndexAction()
        {
            return new Hashtable();
        }
    }
}
