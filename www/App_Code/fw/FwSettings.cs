using osafw_asp.net_core.fw;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp.net_cor.fw
{
    public class FwSettings : FwModel
    {
        public FwSettings() : base()
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
        public String read(String icode)
        {
            return getValue(icode);
        }

        /// <summary>
        /// Read integer value from site settings
        /// </summary>
        /// <param name="icode"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public int readi(String icode)
        {
            return Utils.f2int(read(icode));
        }
        /// <summary>
        /// Read date value from site settings
        /// </summary>
        /// <param name="icode"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public Object readd(String icode)
        {
            return Utils.f2date(read(icode));
        }

        /// <summary>
        /// Change site setting by icode, static function for easier use: Settings.write('icode', value)
        /// </summary>
        /// <param name="icode"></param>
        /// <remarks></remarks>
        public void write(String icode, String value)
        {
            setValue(icode, value);
        }

        // just return first row by icode field
        public Hashtable oneByIcode(String icode)
        {
            Hashtable where = new Hashtable();
            where["icode"] = icode;
            return db.row(table_name, where);
        }

        public String getValue(String icode)
        {
            return (String)oneByIcode(icode)["ivalue"];
        }
        public void setValue(String icode, String ivalue)
        {
            Hashtable item = oneByIcode(icode);
            Hashtable fields = new Hashtable();
            if (item.ContainsKey("id"))
            {
                int id = Utils.f2int(item["id"]);
                // exists - update
                fields["ivalue"] = ivalue;
                update(id, fields);
            }
            else
            {
                // not exists - add new
                fields["icode"] = icode;
                fields["ivalue"] = ivalue;
                fields["is_user_edit"] = 0; // all auto-added settings is not user-editable by default
                add(fields);
            }
        }

        // check if item exists for a given icode
        public override bool isExists(Object uniq_key, int not_id) 
        {
            return isExistsByField(uniq_key, not_id, "icode");
        }
    }
}
