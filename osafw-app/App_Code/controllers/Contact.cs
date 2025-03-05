// Contact Us public controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Text;

namespace osafw;

public class ContactController : FwController
{
    public override void init(FW fw)
    {
        base.init(fw);

        base_url = "/Contact";
        // override layout
        fw.G["PAGE_LAYOUT"] = fw.G["PAGE_LAYOUT_PUBLIC"];
    }

    public Hashtable IndexAction()
    {
        Hashtable ps = new();

        fw.Session("contact_view_time", DateTime.Now.ToString());

        Hashtable page = fw.model<Spages>().oneByFullUrl(base_url);
        ps["page"] = page;
        ps["hide_sidebar"] = true;
        return ps;
    }

    public void SaveAction()
    {
        string mail_from = (string)fw.config("mail_from");
        string mail_to = (string)fw.config("support_email");
        string mail_subject = "Contact Form Submission";

        // validation
        var is_spam = false;
        var view_time = fw.Session("contact_view_time").toDate();
        if (!Utils.isDate(view_time) || (DateTime.Now - view_time).TotalSeconds < 5)
            is_spam = true;
        if (reqs("real_email").Length > 0)
            // honeypot
            is_spam = true;

        Hashtable sys_fields = Utils.qh("form_format redirect subject submit RAWURL XSS real_email");

        StringBuilder msg_body = new();
        foreach (string key in fw.FORM.Keys)
        {
            if (sys_fields.ContainsKey(key))
                continue;
            msg_body.AppendLine(key + " = " + fw.FORM[key]);
        }

        // ip address
        msg_body.Append(Environment.NewLine + Environment.NewLine);
        // https://stackoverflow.com/questions/28664686/how-do-i-get-client-ip-address-in-asp-net-core
        var ip = fw.context.Connection.RemoteIpAddress;
        msg_body.AppendLine("IP: " + ip);

        if (is_spam)
            logger("* SPAM DETECTED: " + msg_body.ToString());
        else
            fw.sendEmail(mail_from, mail_to, mail_subject, msg_body.ToString());

        // need to add root_domain, so no one can use our redirector for bad purposes
        fw.redirect(base_url + "/(Sent)");
    }

    public Hashtable SentAction(string url = "")
    {
        Hashtable ps = new();

        Hashtable page = fw.model<Spages>().oneByFullUrl(base_url + "/Sent");
        ps["page"] = page;
        ps["hide_sidebar"] = true;
        return ps;
    }
}