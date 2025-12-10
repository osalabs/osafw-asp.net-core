using Microsoft.AspNetCore.DataProtection.Repositories;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;

namespace osafw;

public class FwKeysXmlRepository : IXmlRepository
{
    const int ITYPE_GENERIC_KEY = 0;
    const int ITYPE_DATA_PROTECTION_KEY = 10;

    private readonly DB db;
    private readonly string table_name = "fwkeys";

    public FwKeysXmlRepository(DB db)
    {
        this.db = db;
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var allXml = new List<XElement>();

        try
        {
            var values = db.col(table_name, new FwRow { { "itype", ITYPE_DATA_PROTECTION_KEY } }, "XmlValue");
            foreach (var xmlStr in values)
            {
                var elem = XElement.Parse(xmlStr);
                allXml.Add(elem);
            }
            db.disconnect();

        }
        catch (Exception ex)
        {
            // ignore errors, could happen when db is not configured yet
            System.Diagnostics.Debug.WriteLine("Exception in FwKeysXmlRepository.GetAllElements:", ex.Message);
        }

        return allXml;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        // Because keys can be updated as well as added, 
        // we should check if there's an existing row for this key ID.
        // The system includes a <key id="GUID"> in the XML. 
        // We can parse the <key id="..."> from the element:

        var keyId = element.Attribute("id")?.Value;
        var xmlStr = element.ToString(SaveOptions.DisableFormatting);

        try
        {

            // Try to see if a row with the same key id already exists:
            var where = new FwRow { { "itype", ITYPE_DATA_PROTECTION_KEY }, { "iname", keyId } };
            var is_exists = db.value(table_name, where, "1").toBool();
            if (is_exists)
            {
                // Update existing row
                db.update(table_name, where, new FwRow { { "XmlValue", xmlStr }, { "upd_time", DB.NOW } });
            }
            else
            {
                // Insert new row
                where["XmlValue"] = xmlStr;
                db.insert(table_name, where);
            }

            //also launch cleanup of old keys whenever we store a new key
            _cleanup();
            db.disconnect();

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Exception in FwKeysXmlRepository.StoreElement:", ex.Message);
        }

    }

    private void _cleanup()
    {
        db.exec($@"DELETE FROM {table_name} 
                WHERE itype=@itype 
                  AND upd_time < DATEADD(day, -90, GETDATE())", new FwRow { { "itype", ITYPE_DATA_PROTECTION_KEY } });
    }
}
