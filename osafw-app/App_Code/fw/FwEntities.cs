// Application Entities model class
// handles list of entities (tables) in the application
// so entities can be referenced by id in other tables
// Use idByIcodeOrAdd("icode") to get id by icode, if not exists - automatically adds new record
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

namespace osafw;

public class FwEntities : FwModel
{
    //constants for standard entities
    public const string ICODE_USERS = "users";

    public FwEntities() : base()
    {
        table_name = "fwentities";
        is_log_changes = false;
    }

    //find record by icode, if not exists - add, return id (existing or newly added)
    public virtual int idByIcodeOrAdd(string icode)
    {
        var row = oneByIcode(icode);
        var id = row[field_id].toInt();
        if (id == 0)
            id = add(DB.h(field_icode, icode, field_iname, Utils.name2human(icode)));
        return id;
    }

}