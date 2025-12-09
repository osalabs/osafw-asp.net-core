// Reports Base class
//
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

// Example usage from code:
//
// var repcode = "sample";
// var filters = new Hashtable();
//
// // get report data only, without rendering
// var report = FwReports.createInstance(fw, repcode, filters);
// report.setFilters(); // set filters data like select/lookups
// report.getData(); // report.list_rows now contains data
//
// // get html string of the report (based on report_html template only)
// var html = FwReports.createHtml(fw, repcode);

// // supported formats: html(default), csv, pdf, xls
// // get report data, render to pdf file (temporary file created), output to browser, cleanup
// var filepath = FwReports.createFile(fw, repcode, "pdf");
// fw.fileResponse(filepath, "report.pdf");
// Utils.cleanupTmpFiles();


using System;
using System.Collections;
using System.Linq;

namespace osafw;

public class FwReports
{
    //template paths
    public const string TPL_BASE_DIR = "/admin/reports";
    //public const string TPL_EXPORT_PDF = "/admin/reports/common/pdf.html"; //this is simplified template for wkhtmltopdf
    public const string TPL_EXPORT_PDF = "/layout_print.html"; // normal print template with latest bootstrap styles, good with Playwright
    public const string TPL_EXPORT_XLS = "/admin/reports/common/xls.html";

    public const string TO_BROWSER = "";
    public const string TO_STRING = "string";

    public string report_code = string.Empty;
    public string format = string.Empty; // report format, if empty - html, other options: html, csv, pdf, xls
    public string render_to = ""; // output to: empty(browser), "string"(render returns string, for html only), "/file/path"(render saves to file)
    public Hashtable f = []; // report filters/options
                        // render options for html to pdf/xls/etc... convertor
    public Hashtable f_data = []; //filters data, like dropdown options

    public Hashtable render_options = new()
    {
        {"cmd", "--page-size Letter --print-media-type"},
        {"landscape", true},
        //{"pdf_filename", "absolute path to save pdf to or just a filename (without extension) for browser output"} //define in report class
    };

    protected FW fw = null!;
    protected DB db = null!;
    public Hashtable ps = []; // final data for template rendering
    public long list_count;      // count of list rows returned from db
    public ArrayList list_rows = [];  // list rows returned from db (array of hashes)

    // access level for the report, default - Manager level.
    // Note, if you lower it for the specific report - you may want to update AdminReports access level as well
    protected int access_level = Users.ACL_MANAGER;
    // for sorting by click on column headers, define in report class
    protected string list_sortdef = string.Empty; // = "iname asc";
    protected Hashtable list_sortmap = []; // = Utils.qh("id|id iname|iname add_time|add_time status|status");
    protected string list_orderby = "1"; // sql for order by clause, set in getData() via setListSorting(), default by first column if no other sorting set

    public static string cleanupRepcode(string repcode)
    {
        return Utils.routeFixChars(repcode);
    }

    public static string filterSessionKey(FW fw, string repcode)
    {
        return "_filter_" + fw.G["controller.action"] + "." + repcode;
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
        if (reportType == null)
            throw new UserException("Report class not found");

        var instance = Activator.CreateInstance(reportType) as FwReports;
        if (instance == null)
            throw new UserException("Report initialization failed");

        FwReports report = instance;
        report.init(fw, repcode, f);
        report.checkAccess();
        return report;
    }

    /// <summary>
    /// return html string of the report (based on report_html template only)
    /// </summary>
    /// <param name="fw"></param>
    /// <param name="repcode"></param>
    /// <param name="f"></param>
    /// <param name="ps"></param>
    /// <returns></returns>
    public static string createHtml(FW fw, string repcode, Hashtable? f = null, Hashtable? ps = null)
    {
        f ??= [];

        var report = createInstance(fw, repcode, f);
        report.setFilters(); // set filters data like select/lookups
        report.getData();
        report.render_to = TO_STRING;
        return report.render(ps);
    }

    public static string createFile(FW fw, string repcode, string format = "", Hashtable? f = null, Hashtable? ps = null)
    {
        f ??= [];
        f["format"] = format;

        var report = createInstance(fw, repcode, f);
        report.setFilters(); // set filters data like select/lookups
        report.getData();

        report.render_to = Utils.getTmpFilename() + format2ext(format);
        report.render(ps);

        return report.render_to;
    }

    public static string format2ext(string format)
    {
        string ext = format switch
        {
            "pdf" => ".pdf",
            "xls" => ".xls",
            "csv" => ".csv",
            _ => ".html",
        };
        return ext;
    }

    public FwReports()
    {
        // constructor
    }

    public virtual void init(FW fw, string report_code, Hashtable f)
    {
        this.fw = fw;
        this.db = fw.db;
        this.report_code = report_code ?? string.Empty;
        this.f = f ?? [];
        this.format = f["format"] as string ?? string.Empty;
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
            if (!fw.model<Users>().isAccessByRolesResourceAction(fw.userId, report_code + "Report", fw.route.action, fw.route.action_more))
                throw new AuthException("Bad access - Not authorized to view the Report (2)");
        }
    }

    public virtual void setListSorting()
    {
        string sortby = f["sortby"] as string ?? string.Empty;
        string sortdir = f["sortdir"] as string ?? string.Empty;

        string sortdef_field = string.Empty;
        string sortdef_dir = string.Empty;
        Utils.split2(" ", list_sortdef ?? string.Empty, ref sortdef_field, ref sortdef_dir);

        list_sortmap ??= [];

        // validation/mapping
        if (string.IsNullOrEmpty(sortby) || string.IsNullOrEmpty(list_sortmap[sortby].toStr().Trim()))
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

        ps["is_run"] = true; //show data in render
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
    public virtual string render(Hashtable? ps_more = null)
    {
        var result = "";

        ps["report_code"] = report_code;
        ps["f"] = f; // filter values
        ps["filter"] = f_data; // filter data
        ps["count"] = list_count;
        ps["list_rows"] = list_rows;

        if (ps_more != null)
            // merge ps_more into ps
            Utils.mergeHash(ps, ps_more);

        ps["IS_EXPORT_PDF"] = false;
        ps["IS_EXPORT_XLS"] = false;

        string base_dir = TPL_BASE_DIR + '/' + report_code.ToLowerInvariant();
        switch (this.format)
        {
            case "pdf":
                {
                    f["edit"] = false; // force any edit modes off
                    ps["IS_EXPORT_PDF"] = true; //use as <~PARSEPAGE.TOP[IS_EXPORT_PDF]> in templates
                    string out_filename = Utils.isEmpty(render_options["pdf_filename"]) ? report_code : (render_options["pdf_filename"] as string ?? string.Empty);
                    if (isFileRender())
                        out_filename = render_to;

                    ConvUtils.parsePagePdf(fw, base_dir, TPL_EXPORT_PDF, ps, out_filename, render_options);
                    break;
                }

            case "xls":
                {
                    ps["IS_EXPORT_XLS"] = true; //use as <~PARSEPAGE.TOP[IS_EXPORT_XLS]> in templates
                    var out_filename = Utils.isEmpty(render_options["xls_filename"]) ? report_code : (render_options["xls_filename"] as string ?? string.Empty);
                    if (isFileRender())
                        out_filename = render_to;

                    ConvUtils.parsePageExcelSimple(fw, base_dir, TPL_EXPORT_XLS, ps, out_filename);
                    break;
                }

            case "xlsx":
                {
                    var out_filename = Utils.isEmpty(render_options["xls_filename"]) ? report_code : (render_options["xls_filename"] as string ?? string.Empty);
                    // TODO make headers as array of readable values, not the same as fields names
                    var headers = list_rows.Count > 0 ? (list_rows[0] as Hashtable)?.Keys.Cast<string>().ToArray() ?? Array.Empty<string>() : Array.Empty<string>();
                    var fields = headers;

                    ConvUtils.exportNativeExcel(fw, headers, fields, list_rows, out_filename);
                    break;
                }
            case "csv":
                {
                    if (isFileRender())
                    {
                        //make csv and save to file
                        var content = Utils.getCSVExport("", "", list_rows).ToString();
                        Utils.setFileContent(render_to, ref content);
                    }
                    else
                        Utils.writeCSVExport(fw.response, report_code + ".csv", "", "", list_rows);
                    break;
                }

            default:
                {
                    // html
                    if (render_to != TO_BROWSER)
                    {
                        ps["IS_PRINT_MODE"] = true;

                        var layout = fw.G["PAGE_LAYOUT_PRINT"] as string ?? string.Empty;
                        if (ps.ContainsKey("_layout"))
                            layout = ps["_layout"] as string ?? layout;

                        result = fw.parsePage(base_dir, layout, ps);

                        if (render_to != TO_STRING)
                            //this is render to file
                            Utils.setFileContent(render_to, ref result);
                    }
                    else
                    {
                        // show report using templates from related report dir
                        fw.parser(base_dir, ps);
                    }
                    break;
                }
        }

        return result;
    }

    protected bool isFileRender()
    {
        return render_to != TO_BROWSER && render_to != TO_STRING;
    }

    // REPORT HELPERS

    // add "perc" value for each row (percentage of row's "ctr" from sum of all ctr)
    protected int _calcPerc(ArrayList rows)
    {
        int total_ctr = 0;
        foreach (Hashtable row in rows)
            total_ctr += row["ctr"].toInt();
        if (total_ctr > 0)
        {
            foreach (Hashtable row in rows)
                row["perc"] = row["ctr"].toInt() / (double)total_ctr * 100;
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
