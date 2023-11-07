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

using System.Collections;

namespace osafw;

public class ActivityLogs : FwModel
{
    public const string TAB_ALL = "all";
    public const string TAB_COMMENTS = "comments";
    public const string TAB_HISTORY = "history";

    public ActivityLogs() : base()
    {
        table_name = "activity_logs";
    }

    /// <summary>
    /// return activity for given entity
    /// </summary>
    /// <param name="entity_icode">entity table name</param>
    /// <param name="id">entity item id</param>
    /// <param name="log_types_icodes">optional list of log types(by icode) to filter on</param>
    /// <returns></returns>
    public DBList listByEntity(string entity_icode, int id, IList log_types_icodes = null)
    {
        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);
        var where = new Hashtable
        {
            {"fwentities_id", fwentities_id },
            {"item_id", id }
        };
        if (log_types_icodes != null)
        {
            var log_types_ids = new ArrayList();
            foreach (string icode in log_types_icodes)
            {
                var log_type = fw.model<LogTypes>().oneByIcode(icode);
                log_types_ids.Add(log_type["id"]);
            }
            where["log_types_id"] = db.opIN(log_types_ids);
        }

        return db.array(table_name, where, "idate desc, id desc");
    }

    /// <summary>
    /// return activity for given entity for UI
    /// </summary>
    /// <param name="entity_icode"></param>
    /// <param name="id"></param>
    /// <param name="tab">"all", "comments" or "history"</param>
    /// <returns></returns>
    public ArrayList listByEntityForUI(string entity_icode, int id, string tab = "")
    {
        // convert tab to log_types_icodes
        var log_types_icodes = new ArrayList();
        switch (tab)
        {
            case TAB_COMMENTS:
                log_types_icodes.Add(LogTypes.ICODE_COMMENT);
                break;
            case TAB_HISTORY:
                log_types_icodes.Add(LogTypes.ICODE_ADDED);
                log_types_icodes.Add(LogTypes.ICODE_UPDATED);
                log_types_icodes.Add(LogTypes.ICODE_DELETED);
                break;
        }

        ArrayList result = listByEntity(entity_icode, id, log_types_icodes);
        foreach (Hashtable row in result)
        {
            row["log_type"] = fw.model<LogTypes>().one(row["log_types_id"]);
            row["user"] = fw.model<Users>().one(row["users_id"]);
        }
        return result;
    }
}