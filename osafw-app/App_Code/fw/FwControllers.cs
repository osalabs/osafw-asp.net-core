// FwControllers model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2025 Oleg Savchuk www.osalabs.com

using System;
using System.Collections.Concurrent;

namespace osafw;

public class FwControllers : FwModel
{
    private static readonly ConcurrentDictionary<string, DBRow> VirtualControllerCache = new(StringComparer.OrdinalIgnoreCase);

    public FwControllers() : base()
    {
        db_config = "";
        table_name = "fwcontrollers";
    }

    /// <summary>
    /// Returns virtual-controller metadata from a process-wide cache shared by requests.
    /// </summary>
    public override DBRow oneByIcode(string icode)
    {
        if (field_icode == "" || string.IsNullOrEmpty(icode))
            return [];

        var row = VirtualControllerCache.GetOrAdd(icode, oneByIcodeIC);

        return cloneRow(row);
    }

    /// <summary>
    /// Reads virtual-controller metadata case-insensitively from storage on cache miss.
    /// </summary>
    protected virtual DBRow oneByIcodeIC(string icode)
    {
        var sql = $@"select *
                    from {db.qid(table_name)}
                    where LOWER({db.qid(field_icode)})=LOWER(@icode)";
        return db.rowp(sql, DB.h("@icode", icode));
    }

    public override void removeCache(int id)
    {
        base.removeCache(id);
        VirtualControllerCache.Clear();
    }

    public override void removeCacheAll()
    {
        base.removeCacheAll();
        VirtualControllerCache.Clear();
    }

    public DBList listGrouped()
    {
        return db.array(table_name, new FwDict
        {
            ["status"] = db.opNOT(STATUS_DELETED),
            ["access_level"] = db.opLE(fw.userAccessLevel)
        }, "igroup, iname");
    }

    private static DBRow cloneRow(DBRow row)
    {
        var result = new DBRow(row.Count);
        foreach (var kv in row)
            result[kv.Key] = kv.Value;
        return result;
    }
}
