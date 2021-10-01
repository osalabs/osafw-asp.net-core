// Reports model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com


using System;
using System.Collections;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;

namespace osafw
{
    public class Reports : FwModel
    {
        public Reports() : base()
        {
        }

        public string cleanupRepcode(string repcode)
        {
            return Regex.Replace(repcode, @"[^\w-]", "").ToLower();
        }
        /// <summary>
        /// Convert report code into class name
        /// </summary>
        /// <param name="repcode">pax-something-summary</param>
        /// <returns>ReportPaxSomethingSummary</returns>
        /// <remarks></remarks>
        public string repcodeToClass(string repcode)
        {
            string result = "";
            string[] pieces = Strings.Split(repcode, "-");
            foreach (string piece in pieces)
                result += Utils.capitalize(piece);
            return "Report" + result;
        }

        /// <summary>
        /// Create instance of report class by repcode
        /// </summary>
        /// <param name="repcode">cleaned report code</param>
        /// <param name="f">filters passed from request</param>
        /// <returns></returns>
        public ReportBase createInstance(string repcode, Hashtable f)
        {
            string report_class_name = repcodeToClass(repcode);
            if (string.IsNullOrEmpty(report_class_name))
                throw new ApplicationException("Wrong Report Code");

            ReportBase report = (ReportBase)Activator.CreateInstance(Type.GetType(FW.FW_NAMESPACE_PREFIX + report_class_name, true));
            report.init(fw, repcode, f);
            return report;
        }
    }
}