// Settings model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class Settings : FwModel<Settings.Row>
{
    public const string ICAT_AI = "AI";

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
    /// Reads a site setting value by icode.
    /// </summary>
    public string read(string icode)
    {
        return this.getValue(icode);
    }

    /// <summary>
    /// Reads a site setting as an integer.
    /// </summary>
    public int readi(string icode)
    {
        return read(icode).toInt();
    }

    public string read(string icode, string defaultValue)
    {
        return getValue(icode, defaultValue);
    }

    public bool readBool(string icode, bool defaultValue = false)
    {
        var value = read(icode);
        return string.IsNullOrEmpty(value) ? defaultValue : value.toBool();
    }

    public int readInt(string icode, int defaultValue = 0)
    {
        var value = read(icode);
        return string.IsNullOrEmpty(value) ? defaultValue : value.toInt(defaultValue);
    }

    public long readLong(string icode, long defaultValue = 0)
    {
        var value = read(icode);
        return string.IsNullOrEmpty(value) ? defaultValue : value.toLong(defaultValue);
    }

    /// <summary>
    /// Reads a site setting as a nullable date.
    /// </summary>
    public object? readd(string icode)
    {
        return read(icode).toDateOrNull();
    }

    /// <summary>
    /// Writes a site setting value by icode.
    /// </summary>
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
        return row.TryGetValue("ivalue", out string? value) ? value.toStr() : string.Empty;
    }

    public string getValue(string icode, string defaultValue)
    {
        var value = getValue(icode);
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    public void setValue(string icode, string ivalue)
    {
        var item = this.oneByIcode(icode);
        FwDict fields = [];
        if (item.TryGetValue("id", out string? value))
        {
            // exists - update
            fields["ivalue"] = ivalue;
            update(value.toInt(), fields);
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

    /// <summary>
    /// Lists categories currently used by settings rows for the admin settings tabs.
    /// </summary>
    public FwList listCategories()
    {
        FwList rows = db.arrayp($@"
select icat
  from {db.qid(table_name)}
 group by icat
 order by case when icat='' then 0 else 1 end, icat", DB.h());

        return rows;
    }

    // check if item exists for a given icode
    public override bool isExists(object uniq_key, int not_id)
    {
        return isExistsByField(uniq_key, not_id, "icode");
    }
}
