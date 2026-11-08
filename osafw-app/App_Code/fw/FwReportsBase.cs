// Reports Base class
//
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

// Example usage from code:
//
// var repcode = "sample";
// var filters = new FwRow();
//
// // get report data only, without rendering
// var report = FwReportsBase.createInstance(fw, repcode, filters);
// report.setFilters(); // set filters data like select/lookups
// report.getData(); // report.list_rows now contains data
//
// // get html string of the report (based on report_html template only)
// var html = FwReportsBase.createHtml(fw, repcode);

// // supported formats: html(default), csv, pdf, xls, xlsx, json
// // get report data, render to pdf file (temporary file created), output to browser, cleanup
// var filepath = FwReportsBase.createFile(fw, repcode, "pdf");
// fw.fileResponse(filepath, "report.pdf");
// Utils.cleanupTmpFiles();


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;

namespace osafw;

public class FwReportsBase
{
    //template paths
    public const string TPL_BASE_DIR = "/admin/reports";
    public const string TPL_EXPORT_PDF = "/layout_print.html"; // normal print template with latest bootstrap styles, printed by installed Microsoft Edge
    public const string TPL_EXPORT_XLS = "/admin/reports/common/xls.html";

    public const string TO_BROWSER = "";
    public const string TO_STRING = "string";

    public string report_code = string.Empty;
    public string format = string.Empty; // report format, if empty - html, other options: html, csv, pdf, xls, xlsx, json
    public string render_to = ""; // output to: empty(browser), "string"(render returns string, for html only), "/file/path"(render saves to file)
    public FwDict f = []; // report filters/options
                        // render options for html to pdf/xls/etc... convertor
    public FwDict f_data = []; //filters data, like dropdown options

    public FwDict render_options = new()
    {
        {"landscape", true},
        //{"pdf_filename", "absolute path to save pdf to or just a filename (without extension) for browser output"} //define in report class
    };

    protected FW fw = null!;
    protected DB db = null!;
    protected string db_config = ""; // if empty(default) - fw.db used, otherwise - new db connection created based on this config name
    public FwDict ps = []; // final data for template rendering
    public long list_count;      // count of list rows returned from db
    public FwList list_rows = [];  // list rows returned from db (array of hashes)

    // access level for the report, default - Manager level.
    // Note, if you lower it for the specific report - you may want to update AdminReports access level as well
    protected int access_level = Users.ACL_MANAGER;
    // for sorting by click on column headers, define in report class
    protected string list_sortdef = string.Empty; // = "iname asc";
    protected FwDict list_sortmap = []; // = Utils.qh("id|id iname|iname add_time|add_time status|status");
    protected string list_orderby = "1"; // sql for order by clause, set in getData() via setListSorting(), default by first column if no other sorting set

    /// <summary>Opt in only after verifying offline rendering and the report's data-sharing scope.</summary>
    public virtual bool isSchedulingSupported => false;

    /// <summary>Uses the compiled report's casing for direct routes while preserving legacy dashed codes.</summary>
    public virtual string schedulingCode
    {
        get
        {
            var name = GetType().Name;
            return name.Equals(report_code + "Report", StringComparison.OrdinalIgnoreCase) ? name[..^6] : report_code;
        }
    }

    /// <summary>Uses the existing report title template as plain text for schedule names.</summary>
    public virtual string schedulingTitle
    {
        get
        {
            var title = fw.parsePageInstance().parse_page(templateBaseDir(), "title.html", ps);
            var plain = WebUtility.HtmlDecode(Utils.html2text(title)).Trim();
            return plain.Length > 0 ? plain : schedulingCode;
        }
    }

    private FwList? scheduling_parameters;

    /// <summary>Reads the optional scheduling filter definitions once per initialized report instance.</summary>
    protected virtual FwList schedulingParameters()
    {
        if (scheduling_parameters != null)
            return scheduling_parameters;

        var path = Path.Combine(fw.config("template").toStr(), templateBaseDir().TrimStart('/'), "schedule.json");
        if (!File.Exists(path))
            return scheduling_parameters = [];

        var json = Utils.getFileContent(path, out var error);
        if (error != null || Utils.jsonDecodeList(json) is not { } definitions)
            throw new UserException("REPORT_FILTER_INVALID");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new FwList(definitions.Count);
        foreach (var item in definitions)
        {
            if (item is not FwDict def || def["name"] is not string name
                || string.IsNullOrWhiteSpace(name) || !names.Add(name)
                || def["type"] is not (null or string))
            {
                throw new UserException("REPORT_FILTER_INVALID");
            }

            try
            {
                FwReports.normalizeParamDefinition(def);
            }
            catch (UserException)
            {
                throw new UserException("REPORT_FILTER_INVALID");
            }
            result.Add(def);
        }

        return scheduling_parameters = result;
    }

    // A report-owned relative selector may resolve from this instant and fw.userTimezone during execution.
    protected DateTime? scheduled_run_utc { get; private set; }
    protected bool is_scheduled_snapshot { get; private set; }

    /// <summary>Captures only declared filter inputs after applying and validating normal report defaults.</summary>
    internal FwList captureScheduledFilters(FwDict submitted, bool is_frozen)
    {
        var definitions = checkedSchedulingParameters();
        var filters = new FwDict();
        foreach (var def in definitions)
        {
            var name = def["name"].toStr();
            if (submitted[name] is string submittedValue)
                filters[name] = submittedValue;
            else if (submitted.ContainsKey(name))
                throw new UserException("REPORT_FILTER_INVALID");
            else if (is_frozen)
                throw new UserException("REPORT_FILTER_MISSING");
        }
        readScheduledSort(submitted, filters);

        f = filters;
        f["format"] = format;
        is_scheduled_snapshot = is_frozen;
        prepareScheduledFilters(definitions);

        var rows = new FwList();
        foreach (var def in definitions)
        {
            var name = def["name"].toStr();
            rows.Add(new FwDict
            {
                ["name"] = name,
                ["type"] = def["type"].toStr(),
                ["value"] = f[name].toStr()
            });
        }
        return rows;
    }

    /// <summary>Applies a complete saved filter snapshot without resolving defaults again.</summary>
    internal void applyScheduledFilters(FwDict payload, DateTime? executionUtc = null)
    {
        var definitions = checkedSchedulingParameters();
        var filters = new FwDict();
        var saved = (FwList)payload["params"]!;
        foreach (var row in saved)
        {
            var name = row["name"].toStr();
            if (filters.ContainsKey(name) || !definitions.Any(def => def["name"].toStr() == name && def["type"].toStr() == row["type"].toStr().Trim().ToLowerInvariant()))
                throw new UserException("REPORT_FILTER_CHANGED");

            filters[name] = row["value"];
        }
        if (filters.Count != definitions.Count)
            throw new UserException("REPORT_FILTER_MISSING");

        readScheduledSort(payload, filters);
        var sort = filters["sortby"].toStr();
        var direction = filters["sortdir"].toStr();
        if ((sort.Length > 0 && list_sortmap[sort].toStr().Length == 0)
            || (direction.Length > 0 && direction is not ("asc" or "desc"))
            || (list_sortmap.Count == 0 && (payload.ContainsKey("sortby") || payload.ContainsKey("sortdir"))))
        {
            throw new UserException("REPORT_SORT_CHANGED");
        }

        f = filters;
        f["format"] = format;
        scheduled_run_utc = executionUtc;
        is_scheduled_snapshot = true;
        prepareScheduledFilters(definitions);
    }

    /// <summary>Redisplays attempted values after a rejected filter update without applying changed defaults.</summary>
    internal void showScheduledFormFilters(FwDict submitted, bool is_frozen = true)
    {
        f = submitted;
        is_scheduled_snapshot = is_frozen;
        setFilters();
        checkAccess();
    }

    /// <summary>Describes saved inputs using current report labels and lookup options without running the report query.</summary>
    public virtual FwList describeScheduledFilters(FwList saved)
    {
        var definitions = schedulingParameters();
        var result = new FwList();
        foreach (var parameter in saved)
        {
            var name = parameter["name"].toStr();
            var definition = definitions.FirstOrDefault(def => def["name"].toStr() == name);
            var value = parameter["value"].toStr();
            if (definition != null && value.Length > 0 && FwReports.isLookupParamType(definition["type"].toStr()))
            {
                var options = fw.model<FwReports>().listParamOptions(definition, db);
                value = options.FirstOrDefault(option => option["id"].toStr() == value)?["iname"].toStr() ?? value;
            }
            result.Add(new FwDict
            {
                ["name"] = name,
                ["label"] = fw.parsePageInstance().langMap(definition?["label"].toStr(name) ?? name),
                ["value"] = value
            });
        }
        return result;
    }

    private FwList checkedSchedulingParameters()
    {
        var definitions = schedulingParameters();
        foreach (var def in definitions)
        {
            var name = def["name"].toStr().ToLowerInvariant();
            if (name is "format" or "sortby" or "sortdir" or "is_preview" or "edit" or "xss"
                or "email_as" or "to_emails" or "pagenum" or "pagesize" or "send_email" or "is_run")
            {
                throw new UserException("REPORT_FILTER_INVALID");
            }
        }

        return definitions;
    }

    private void prepareScheduledFilters(FwList definitions)
    {
        setFilters();
        fw.model<FwReports>().buildSqlParams(definitions, f, fw.userDateFormat, fw.userTimeFormat, is_scheduled_snapshot);
        foreach (var def in definitions)
        {
            var name = def["name"].toStr();
            f[name] = f[name].toStr();
        }
        if (list_sortmap.Count > 0)
            setListSorting();

        checkAccess();
    }

    private void readScheduledSort(FwDict source, FwDict filters)
    {
        if (list_sortmap.Count == 0)
            return;

        filters["sortby"] = source["sortby"].toStr();
        filters["sortdir"] = source["sortdir"].toStr();
    }

    internal void saveScheduledSort(FwDict payload)
    {
        if (list_sortmap.Count == 0)
            return;

        payload["sortby"] = f["sortby"].toStr();
        payload["sortdir"] = f["sortdir"].toStr();
    }

    public static string cleanupRepcode(string repcode)
    {
        return Utils.routeFixChars(repcode);
    }

    public static string filterSessionKey(FW fw, string repcode)
    {
        return "_filter_" + fw.G["controller.action"] + "." + repcode;
    }

    /// <summary>
    /// Converts a route report code into the compiled report class name.
    /// </summary>
    /// <returns>Class name with <c>Report</c> suffix, such as <c>PaxSomethingSummaryReport</c>.</returns>
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
    /// Creates a hardcoded or custom report instance for a cleaned report code.
    /// </summary>
    /// <param name="f">Submitted report filters.</param>
    public static FwReportsBase createInstance(FW fw, string repcode, FwDict f)
    {
        var report = initializeReport(fw, repcode, f);
        report.checkAccess();
        return report;
    }

    /// <summary>Creates an opted-in report whose access check follows filter validation.</summary>
    /// <remarks>Use captureScheduledFilters or applyScheduledFilters before running a scheduled report.</remarks>
    internal static FwReportsBase createScheduledInstance(FW fw, string repcode, FwDict f, bool is_hardcoded_only = false)
    {
        var report = is_hardcoded_only
            ? initializeHardcodedReport(fw, repcode, f) ?? throw new UserException("REPORT_SCHEDULE_UNAVAILABLE")
            : initializeReport(fw, repcode, f);

        if (!report.isSchedulingSupported)
            throw new UserException("REPORT_SCHEDULE_UNAVAILABLE");

        // Stored-report access is independent of filters and protects its SQL-backed filter metadata.
        if (report is FwCustomReport)
            report.checkAccess();

        return report;
    }

    private static FwReportsBase initializeReport(FW fw, string repcode, FwDict f)
    {
        var report = initializeHardcodedReport(fw, repcode, f);
        if (report != null)
            return report;

        var custom = fw.model<FwReports>().oneActiveByIcode(repcode);
        if (custom.Count == 0)
            throw new UserException("Report not found");

        report = new FwCustomReport(custom.toFwDict());
        report.init(fw, repcode, f);
        return report;
    }

    /// <summary>
    /// Checks whether a route code resolves to a compiled report class before custom-report fallback.
    /// </summary>
    /// <param name="repcode">Cleaned report code.</param>
    /// <returns>True when a matching report class exists.</returns>
    public static bool isHardcodedReport(string repcode)
    {
        return resolveReportType(repcode) != null;
    }

    /// <summary>
    /// Creates a compiled report instance without consulting database-backed custom reports.
    /// </summary>
    /// <param name="fw">Current framework request context.</param>
    /// <param name="repcode">Cleaned report code.</param>
    /// <param name="f">Submitted filters.</param>
    /// <returns>Initialized report instance or null when no compiled report class exists.</returns>
    protected static FwReportsBase? createHardcodedInstance(FW fw, string repcode, FwDict f)
    {
        var report = initializeHardcodedReport(fw, repcode, f);
        report?.checkAccess();
        return report;
    }

    private static FwReportsBase? initializeHardcodedReport(FW fw, string repcode, FwDict f)
    {
        var reportType = resolveReportType(repcode);
        if (reportType == null)
            return null;

        var instance = Activator.CreateInstance(reportType) as FwReportsBase;
        if (instance == null)
            throw new UserException("Report initialization failed");

        instance.init(fw, repcode, f);
        return instance;
    }

    /// <summary>
    /// Resolves the compiled report class for a report code without throwing when it is absent.
    /// </summary>
    /// <param name="repcode">Cleaned report code.</param>
    /// <returns>Report type or null when the report is not hardcoded.</returns>
    private static Type? resolveReportType(string repcode)
    {
        string report_class_name = repcodeToClass(repcode);
        if (string.IsNullOrEmpty(report_class_name))
            throw new UserException("Wrong Report Code");

        return Type.GetType(FW.FW_NAMESPACE_PREFIX + report_class_name, false, true);
    }

    /// <summary>
    /// Renders report HTML using the report template path without writing to the response.
    /// </summary>
    public static string createHtml(FW fw, string repcode, FwDict? f = null, FwDict? ps = null)
    {
        f ??= [];

        var report = createInstance(fw, repcode, f);
        report.setFilters(); // set filters data like select/lookups
        report.getData();
        report.render_to = TO_STRING;
        return report.render(ps);
    }

    public static string createFile(FW fw, string repcode, string format = "", FwDict? f = null, FwDict? ps = null)
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
        string ext = (format ?? string.Empty).ToLowerInvariant() switch
        {
            "pdf" => ".pdf",
            "xls" => ".xls",
            "csv" => ".csv",
            "json" => ".json",
            _ => ".html",
        };
        return ext;
    }

    public FwReportsBase()
    {
        // constructor
    }

    public virtual void init(FW fw, string report_code, FwDict f)
    {
        scheduling_parameters = null;
        this.fw = fw;
        if (!string.IsNullOrEmpty(this.db_config))
        {
            this.db = fw.getDB(this.db_config);
        }
        else
            this.db = fw.db;

        this.report_code = report_code ?? string.Empty;
        this.f = f ?? [];
        this.format = this.f["format"].toStr().ToLowerInvariant();
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
    public virtual string render(FwDict? ps_more = null)
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

        string base_dir = templateBaseDir();
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
                    var headers = list_rows.Count > 0 ? list_rows[0].Keys.ToArray() : Array.Empty<string>();
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
                    {
                        var response = fw.response ?? throw new InvalidOperationException("Response is not available");
                        Utils.writeCSVExport(response, report_code + ".csv", "", "", list_rows);
                    }
                    break;
                }
            case "json":
                {
                    var content = Utils.jsonEncode(ps);
                    if (isFileRender())
                        Utils.setFileContent(render_to, ref content);
                    else if (render_to == TO_STRING)
                        result = content;
                    else
                        fw.parserJson(ps);
                    break;
                }

            default:
                {
                    // html
                    if (render_to != TO_BROWSER)
                    {
                        ps["IS_PRINT_MODE"] = true;

                        var layout = fw.G["PAGE_LAYOUT_PRINT"] as string ?? string.Empty;
                        if (ps.TryGetValue("_layout", out object? value))
                            layout = value as string ?? layout;

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

    /// <summary>Returns valid plain addresses separated by semicolons, or an empty string.</summary>
    internal static string normalizeRecipients(string recipients, bool is_scheduled = false)
    {
        if (!is_scheduled)
            return string.Join(";", Utils.splitEmails(recipients).Where(FormUtils.isEmail));

        var emails = recipients.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(email => MailAddress.TryCreate(email, out var address) && address.Address == email && address.DisplayName.Length == 0
                && Utils.splitEmails(email).SequenceEqual([email]))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(";", emails);
    }

    /// <summary>Renders an initialized report once and delivers it, deleting only the generated attachment.</summary>
    internal bool sendReportEmail(string recipients, string emailFormat, bool is_scheduled = false)
    {
        string? pdf = null;
        try
        {
            string body;
            FwDict attachments = [];
            if (emailFormat == "pdf")
            {
                pdf = Utils.getTmpFilename() + ".pdf";
                format = "pdf";
                f["format"] = "pdf";
                render_to = pdf;
                render();
                if (!File.Exists(pdf) || new FileInfo(pdf).Length == 0)
                    throw new UserException("REPORT_PDF_EMPTY");

                attachments[report_code + ".pdf"] = pdf;
                body = "";
            }
            else
            {
                render_to = TO_STRING;
                body = render(new FwDict { ["_layout"] = fw.config("PAGE_LAYOUT_EMAIL") });
                if (is_scheduled && string.IsNullOrWhiteSpace(body))
                    throw new UserException("REPORT_BODY_EMPTY");
            }

            try
            {
                // The HTML body comes from the server-rendered report, not submitted markup.
                return fw.sendEmailTpl(recipients, "report.html", new FwDict
                {
                    ["report_code"] = report_code,
                    ["is_pdf"] = emailFormat == "pdf",
                    ["body"] = body
                }, attachments);
            }
            catch (Exception) when (is_scheduled && fw.is_email_send_attempted)
            {
                throw new FwCronUncertainException("REPORT_DELIVERY_UNCERTAIN");
            }
        }
        finally
        {
            Utils.deleteFile(pdf);
        }
    }

    protected bool isFileRender()
    {
        return render_to != TO_BROWSER && render_to != TO_STRING;
    }

    /// <summary>
    /// Returns the ParsePage template directory for the current report so adapters can share templates.
    /// </summary>
    /// <returns>Report template base directory.</returns>
    protected virtual string templateBaseDir()
    {
        return TPL_BASE_DIR + '/' + report_code.ToLowerInvariant();
    }

    // REPORT HELPERS

    // add "perc" value for each row (percentage of row's "ctr" from sum of all ctr)
    protected int _calcPerc(FwList rows)
    {
        int total_ctr = 0;
        foreach (FwDict row in rows)
            total_ctr += row["ctr"].toInt();
        if (total_ctr > 0)
        {
            foreach (FwDict row in rows)
                row["perc"] = row["ctr"].toInt() / (double)total_ctr * 100;
        }
        return total_ctr;
    }

    /// <summary>
    /// Returns the standard report predicate that excludes deleted rows for an optional table alias.
    /// </summary>
    protected string andNotDeleted(string alias = "")
    {
        return $" and {alias}status<>{db.qi(FwModel.STATUS_DELETED)}";
    }
}
