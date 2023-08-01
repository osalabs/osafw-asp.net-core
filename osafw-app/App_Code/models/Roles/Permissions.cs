// Permissions model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

namespace osafw;

public class Permissions : FwModel
{
    public Permissions() : base()
    {
        db_config = "";
        table_name = "permissions";
        field_prio = "prio";
    }
}