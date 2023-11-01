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
    public const string TPL_EXPORT_XLS = "/admin/reports/common/xls.html";

    public string report_code;
    public string format; // report format, if empty - html, other options: html, csv, pdf, xls
    public Hashtable f; // report filters/options
                        // render options for html to pdf/xls/etc... convertor
    public Hashtable f_data = new(); //filters data, like dropdown options

    public Hashtable render_options = new()
    {
        {"cmd", "--page-size Letter --print-media-type"},
        {"landscape", true}
    };

    protected FW fw;
    protected DB db;
    protected Hashtable ps = new(); // final data for template rendering
    protected long list_count;      // count of list rows returned from db
    protected ArrayList list_rows;  // list rows returned from db (array of hashes)


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
        // list_rows =db.array("select * from something where 1=1 " & where & " order by something")
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
                    ConvUtils.parsePagePdf(fw, base_dir, (string)fw.config("PAGE_LAYOUT_PRINT"), ps, report_code, render_options);
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
                    var rep = (Hashtable)ps["rep"];
                    Utils.writeCSVExport(fw.response, report_code + ".csv", "", "", (ArrayList)rep["rows"]);
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
            total_ctr += Utils.f2int(row["ctr"]);
        if (total_ctr > 0)
        {
            foreach (Hashtable row in rows)
                row["perc"] = Utils.f2int(row["ctr"]) / (double)total_ctr * 100;
        }
        return total_ctr;
    }

    /// <summary>
    /// add " and status<>127" to reports where
    /// </summary>
    /// <param name="alias"></param>
    /// <returns></returns>
    protected string andNotDeleted(string alias = "")
    {
        return $" and {alias}status<>{db.qi(FwModel.STATUS_DELETED)}";
    }
}