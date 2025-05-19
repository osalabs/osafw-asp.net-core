using Microsoft.AspNetCore.Http;
using System;
using System.Collections;

namespace osafw;

public class ParsePageOptions
{
    public string TemplatesRoot { get; set; } = "";
    public bool CheckFileModifications { get; set; } = false;
    public string Lang { get; set; } = "en";
    public bool LangUpdate { get; set; } = true;
    public Func<Hashtable>? GlobalsGetter { get; set; }
    public Func<string, object>? ConfigGetter { get; set; }
    public ISession? Session { get; set; }
    public Action<LogLevel, string[]>? Logger { get; set; }
}
