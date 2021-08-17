// Reports Base class
//
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw
{

    public class ReportBase
    {
        protected FW fw;
        protected DB db;
        public string report_code;
        public string format; // report format, if empty - html, other options: html, csv, pdf, xls
        public Hashtable f; // report filters/options
                            // render options for html to pdf/xls/etc... convertor
        public Hashtable render_options = new()
        {
            {"cmd", "--page-size Letter --print-media-type"},
            {"landscape", true}
        };

        public ReportBase()
        {
        }

        public virtual void init(FW fw, string report_code, Hashtable f)
        {
            this.fw = fw;
            this.db = fw.db;
            this.report_code = report_code;
            this.f = f;
            this.format = (string)f["format"];
        }

        public virtual Hashtable getReportData()
        {
            Hashtable ps = new();
            return ps;
        }

        public virtual Hashtable getReportFilters()
        {
            Hashtable result = new();
            // result("select_something")=fw.model(of Something).listSelectOptions()
            return result;
        }

        public virtual bool saveChanges()
        {
            return false;
        }

        // render report according to format
        public virtual void render(Hashtable ps)
        {
            string base_dir = "/admin/reports/" + this.report_code;
            switch (this.format)
            {
                case "pdf":
                    {
                        ((Hashtable)ps["f"])["edit"] = false; // force any edit modes off
                        ps["IS_EXPORT_PDF"] = true;
                        fw.G["IS_EXPORT_PDF"] = true; // TODO make TOP[] in ParsePage?
                        ConvUtils.parsePagePdf(fw, base_dir, (string)fw.config("PAGE_LAYOUT_PRINT"), ps, report_code, render_options);
                        break;
                    }

                case "xls":
                    {
                        ps["IS_EXPORT_XLS"] = true;
                        fw.G["IS_EXPORT_XLS"] = true; // TODO make TOP[] in ParsePage?
                        ConvUtils.parsePageExcelSimple(fw, base_dir, "/admin/reports/common/xls.html", ps, report_code);
                        break;
                    }

                case "csv":
                    {
                        throw new NotImplementedException("CSV format not yet supported");
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
    }
}