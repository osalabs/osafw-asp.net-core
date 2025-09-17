// Att Categories Dictionary model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class AttCategories : FwModel
{
    public const string CAT_GENERAL = "general";
    public const string CAT_USERS = "users";
    public const string CAT_FILES = "files";
    public const string CAT_SPAGE_BANNER = "spage_banner";

    public AttCategories() : base()
    {
        table_name = "att_categories";
    }

    // just return first row by iname field (you may want to make it unique)
    public override DBRow oneByIcode(string icode)
    {
        Hashtable where = [];
        where["icode"] = icode;
        return db.row(table_name, where);
    }

    public ArrayList listSelectOptionsLikeIcode(string icode_prefix)
    {
        return db.array(table_name, new Hashtable()
        {
            {
                field_status,
                STATUS_ACTIVE
            },
            {
                field_icode,
                db.opLIKE(icode_prefix + "[_]%")
            }
        }, field_id, Utils.qw("id iname"));
    }
}