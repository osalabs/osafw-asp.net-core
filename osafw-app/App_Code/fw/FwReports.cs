// Reports Base class
//
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class FwReports
{
    //template paths
    public const string TPL_BASE_DIR = "/admin/reports";
    public const string TPL_EXPORT_PDF = "/admin/reports/common/pdf.html";
    public const string TPL_EXPORT_XLS = "/admin/reports/common/xls.html";

    public string report_code;
    public string format; // report format, if empty - html, other options: html, csv, pdf, xls
    public Hashtable f; // report filters/options
                        // render options for html to pdf/xls/etc... convertor
    public Hashtable f_data = new(); //filters data, like dropdown options

    public Hashtable render_options = new()
    {
        {"cmd", "--page-size Letter --print-media-type"},
        {"landscape", true},
        //{"pdf_filename", "absolute path to save pdf to or just a filename (without extension) for browser output"} //define in report class
    };

    protected FW fw;
    protected DB db;
    protected Hashtable ps = new(); // final data for template rendering
    protected long list_count;      // count of list rows returned from db
    protected ArrayList list_rows;  // list rows returned from db (array of hashes)

    // access level for the report, default - Manager level.
    // Note, if you lower it for the specific report - you may want to update AdminReports access level as well
    protected int access_level = Users.ACL_MANAGER;
    // for sorting by click on column headers, define in report class
    protected string list_sortdef; // = "iname asc";
    protected Hashtable list_sortmap; // = Utils.qh("id|id iname|iname add_time|add_time status|status");
    protected string list_orderby = "1"; // sql for order by clause, set in getData() via setListSorting(), default by first column if no other sorting set


    public static string cleanupRepcode(string repcode)
    {
        return Utils.routeFixChars(repcode);
    }

    /// <summary>
    /// Convert report code into class name
    /// </summary>
    /// <param name="repcode">pax-something-summary or Sample</param>
    /// <returns>code with "Report" suffix - PaxSomethingSummaryReport or SampleReport</returns>
    /// <remarks></remarks>
    public static string repcodeToClass(string repcode)
    {
        string result = "";
        if (repcode.Contains('-'))
        {
            //if repcode contains "-" then use code below (compatibility with legacy reports)
            string[] pieces = repcode.Split("-");
            foreach (string piece in pieces)
                result += Utils.capitalize(piece);
        }
        else
        {
            result = repcode;
        }

        return result + "Report";
    }

    /// <summary>
    /// Create instance of report class by repcode
    /// </summary>
    /// <param name="repcode">cleaned report code</param>
    /// <param name="f">filters passed from request</param>
    /// <returns></returns>
    public static FwReports createInstance(FW fw, string repcode, Hashtable f)
    {
        string report_class_name = repcodeToClass(repcode);
        if (string.IsNullOrEmpty(report_class_name))
            throw new UserException("Wrong Report Code");

        var reportType = Type.GetType(FW.FW_NAMESPACE_PREFIX + report_class_name, true, true);
        FwReports report = (FwReports)Activator.CreateInstance(reportType);
        report.init(fw, repcode, f);
        report.checkAccess();
        return report;
    }

    public FwReports()
    {
        // constructor
    }

    public virtual void init(FW fw, string report_code, Hashtable f)
    {
        this.fw = fw;
        this.db = fw.db;
        this.report_code = report_code;
        this.f = f;
        this.format = (string)f["format"];
    }

    // called from createInstance to check if logged user has access to the report
    public virtual void checkAccess()
    {
        //check simple access level first
        if (fw.userAccessLevel < access_level)
            throw new AuthException("Bad access - Not authorized to view the Report");

        // then check access by roles (if enabled)
        // note - report_code is used as resource icode
        // fw.route.action - will be wither Show or Save

        // if user is logged and not SiteAdmin(can access everything)
        // and user's access level is enough for the controller - check access by roles (if enabled)
        int current_user_level = fw.userAccessLevel;
        if (current_user_level > Users.ACL_VISITOR && current_user_level < Users.ACL_SITEADMIN)
        {
            if (!fw.model<Users>().isAccessByRolesResourceAction(fw.userId, report_code, fw.route.action, fw.route.action_more))
                throw new AuthException("Bad access - Not authorized to view the Report (2)");
        }
    }

    public virtual void setListSorting()
    {
        string sortby = (string)f["sortby"] ?? "";
        string sortdir = (string)f["sortdir"] ?? "";

        string sortdef_field = null;
        string sortdef_dir = null;
        Utils.split2(" ", list_sortdef, ref sortdef_field, ref sortdef_dir);

        // validation/mapping
        if (string.IsNullOrEmpty(sortby) || string.IsNullOrEmpty(((string)list_sortmap[sortby] ?? "").Trim()))
            sortby = sortdef_field; // use default if initial load or mapping not set
        if (sortdir != "desc" && sortdir != "asc")
            sortdir = sortdef_dir;

        // save back to filter to render in template
        f["sortby"] = sortby;
        f["sortdir"] = sortdir;

        list_orderby = FormUtils.sqlOrderBy(db, sortby, sortdir, list_sortmap);
    }

    /// <summary>
    /// override to define info for report filters like dropdown options, etc
    /// </summary>
    public virtual void setFilters()
    {
        // f_data("select_something")=fw.model(of Something).listSelectOptions()
    }

    /// <summary>
    /// override to define report data
    /// </summary>
    /// <returns></returns>
    public virtual void getData()
    {
        // setListSorting();
        // list_rows =db.array("select * from something where 1=1 " & where & " order by {list_sortby}")
        // list_count = list_rows.Count();
        // ps["totals"] = 123;
    }

    //override if report has inputs that needs to be saved to db
    public virtual bool saveChanges()
    {
        return false;
    }

    /// <summary>
    /// render report according to format
    /// </summary>
    /// <param name="ps_more">additional data for the template</param>
    public virtual void render(Hashtable ps_more)
    {
        ps["f"] = f; // filter values
        ps["filter"] = f_data; // filter data
        ps["count"] = list_count;
        ps["list_rows"] = list_rows;

        // merge ps_more into ps
        Utils.mergeHash(ps, ps_more);

        ps["IS_EXPORT_PDF"] = false;
        ps["IS_EXPORT_XLS"] = false;

        string base_dir = TPL_BASE_DIR + '/' + report_code.ToLower();
        switch (this.format)
        {
            case "pdf":
                {
                    ((Hashtable)ps["f"])["edit"] = false; // force any edit modes off
                    ps["IS_EXPORT_PDF"] = true; //use as <~PARSEPAGE.TOP[IS_EXPORT_PDF]> in templates
                    string file_name = Utils.isEmpty(render_options["pdf_filename"]) ? report_code : (string)render_options["pdf_filename"];
                    ConvUtils.parsePagePdf(fw, base_dir, TPL_EXPORT_PDF, ps, file_name, render_options);
                    break;
                }

            case "xls":
                {
                    ps["IS_EXPORT_XLS"] = true; //use as <~PARSEPAGE.TOP[IS_EXPORT_XLS]> in templates
                    ConvUtils.parsePageExcelSimple(fw, base_dir, TPL_EXPORT_XLS, ps, report_code);
                    break;
                }

            case "csv":
                {
                    Utils.writeCSVExport(fw.response, report_code + ".csv", "", "", list_rows);
                    break;
                }

            default:
                {
                    // html
                    // show report using templates from related report dir
                    fw.parser(base_dir, ps);
                    break;
                }
        }
    }

    // REPORT HELPERS

    // add "perc" value for each row (percentage of row's "ctr" from sum of all ctr)
    protected int _calcPerc(ArrayList rows)
    {
        int total_ctr = 0;
        foreach (Hashtable row in rows)
            total_ctr += Utils.toInt(row["ctr"]);
        if (total_ctr > 0)
        {
            foreach (Hashtable row in rows)
                row["perc"] = Utils.toInt(row["ctr"]) / (double)total_ctr * 100;
        }
        return total_ctr;
    }

    /// <summary>
    /// add " and status<>127" to reports where
    /// </summary>
    /// <param name="alias">table alias with a dot, example: "t."</param>
    /// <returns></returns>
    protected string andNotDeleted(string alias = "")
    {
        return $" and {alias}status<>{db.qi(FwModel.STATUS_DELETED)}";
    }
}
