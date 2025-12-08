// FW Route class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class FwRoute
{
    public string controller_path = string.Empty; // store /Prefix/Prefix2/Controller - to use in parser a default path for templates
    public string method = string.Empty;
    public string prefix = string.Empty;
    public string controller = string.Empty;
    public string action = string.Empty;
    public string action_raw = string.Empty;
    public string id = string.Empty;
    public string action_more = string.Empty; // new, edit, delete, etc
    public string format = string.Empty; // html, json, pjax
    public ArrayList @params = new();
}
