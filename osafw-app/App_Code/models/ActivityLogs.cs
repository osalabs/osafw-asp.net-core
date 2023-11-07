// Activity Logs model class
// can be used for:
// - log user activity
// - log comments per entity
// - log changes per entity
// - log related events per entity
// - log custom user events per entity
//
// Can be used as a base class for custom log models
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

namespace osafw;

public class ActivityLogs : FwModel
{
    public ActivityLogs() : base()
    {
        table_name = "activity_logs";
    }
}