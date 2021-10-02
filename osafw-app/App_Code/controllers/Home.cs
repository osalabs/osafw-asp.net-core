using System.Collections;
using Microsoft.VisualBasic;

namespace osafw
{
    public class HomeController : FwController
    {
        public static new string route_default_action = "show";

        public override void init(FW fw)
        {
            base.init(fw);
            // override global layout because for this controller we need public pages template, not admin pages
            fw.G["PAGE_LAYOUT"] = fw.config("PAGE_LAYOUT_PUBLIC");
        }

        // CACHED as home_page
        public Hashtable IndexAction()
        {
            Hashtable ps = (Hashtable)FwCache.getValue("home_page");

            if (ps == null || ps.Count == 0)
            {
                // CACHE MISS
                ps = new Hashtable();

                // create home page with heavy queries

                FwCache.setValue("home_page", ps);
            }
            ps["hide_sidebar"] = true;
            // ps["_layout"] = fw.config("PAGE_LAYOUT_PUBLIC") 'alternatively override layout just for this action
            return ps;
        }

        public void ShowAction(string id = "")
        {
            Hashtable ps = new ();
            ps["hide_sidebar"] = true; // TODO control via pages
            fw.parser("/home/" + Utils.routeFixChars(Strings.LCase(id)), (string)fw.config("PAGE_LAYOUT_PUBLIC"), ps);
        }

        // called if fw.dispatch can't find controller
        public void NotFoundAction()
        {
            fw.model<Spages>().showPageByFullUrl(fw.request_url);
        }

        public void TestAction(string id = "")
        {
            Hashtable hf = new ();
            logger("in the TestAction");
            rw("here it is Test");
            rw("id=" + id);
            rw("more_action_name=" + fw.route.action_more);
        }
    }
}