// FW Route class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class FwRoute
{
    public string controller_path; // store /Prefix/Prefix2/Controller - to use in parser a default path for templates
    public string method;
    public string prefix;
    public string controller;
    public string action;
    public string action_raw;
    public string id;
    public string action_more; // new, edit, delete, etc
    public string format; // html, json, pjax
    public ArrayList @params;
}
