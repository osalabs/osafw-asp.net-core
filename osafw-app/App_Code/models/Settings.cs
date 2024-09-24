// Settings model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class Settings : FwModel
{
    public Settings() : base()
    {
        table_name = "settings";

        field_status = "";
    }

    /// <summary>
    /// Return site setting by icode, simplified alias of getValue, use: fw.model(Of Settings).read('icode')
    /// </summary>
    /// <param name="icode"></param>
    /// <returns></returns>
    /// <remarks></remarks>
    public string read(string icode)
    {
        return this.getValue(icode);
    }

    /// <summary>
    /// Read integer value from site settings
    /// </summary>
    /// <param name="icode"></param>
    /// <returns></returns>
    /// <remarks></remarks>
    public int readi(string icode)
    {
        return Utils.toInt(read(icode));
    }

    /// <summary>
    /// Read date value from site settings
    /// </summary>
    /// <param name="icode"></param>
    /// <returns></returns>
    /// <remarks></remarks>
    public object readd(string icode)
    {
        return Utils.toDate(read(icode));
    }

    /// <summary>
    /// Change site setting by icode, static function for easier use: Settings.write('icode', value)
    /// </summary>
    /// <param name="icode"></param>
    /// <remarks></remarks>
    public void write(string icode, string value)
    {
        this.setValue(icode, value);
    }


    // just return first row by icode field
    public override DBRow oneByIcode(string icode)
    {
        Hashtable where = new();
        where["icode"] = icode;
        return db.row(table_name, where);
    }

    public string getValue(string icode)
    {
        return (string)oneByIcode(icode)["ivalue"];
    }
    public void setValue(string icode, string ivalue)
    {
        var item = this.oneByIcode(icode);
        Hashtable fields = new();
        if (item.ContainsKey("id"))
        {
            // exists - update
            fields["ivalue"] = ivalue;
            update(Utils.toInt(item["id"]), fields);
        }
        else
        {
            // not exists - add new
            fields["icode"] = icode;
            fields["ivalue"] = ivalue;
            fields["is_user_edit"] = "0"; // all auto-added settings is not user-editable by default
            this.add(fields);
        }
    }

    // check if item exists for a given icode
    public override bool isExists(object uniq_key, int not_id)
    {
        return isExistsByField(uniq_key, not_id, "icode");
    }
}