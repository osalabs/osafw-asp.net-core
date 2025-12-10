// Settings model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class Settings : FwModel<Settings.Row>
{
    public class Row
    {
        public int id { get; set; }
        public string icat { get; set; } = string.Empty;
        public string icode { get; set; } = string.Empty;
        public string ivalue { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public int input { get; set; }
        public string allowed_values { get; set; } = string.Empty;
        public int is_user_edit { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

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
        return read(icode).toInt();
    }

    /// <summary>
    /// Read date value from site settings
    /// </summary>
    /// <param name="icode"></param>
    /// <returns></returns>
    /// <remarks></remarks>
    public object? readd(string icode)
    {
        return read(icode).toDateOrNull();
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
        FwDict where = [];
        where["icode"] = icode;
        return db.row(table_name, where);
    }

    public string getValue(string icode)
    {
        var row = oneByIcode(icode);
        return row.ContainsKey("ivalue") ? row["ivalue"].toStr() : string.Empty;
    }
    public void setValue(string icode, string ivalue)
    {
        var item = this.oneByIcode(icode);
        FwDict fields = [];
        if (item.ContainsKey("id"))
        {
            // exists - update
            fields["ivalue"] = ivalue;
            update(item["id"].toInt(), fields);
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