using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace osafw;

/// <summary>Durable Data Protection keys. Persistence failures are fatal; expired keys remain available for decryption.</summary>
public class FwKeysXmlRepository : IXmlRepository
{
    private const int ITYPE_DATA_PROTECTION_KEY = 10;
    private readonly Func<DB> createDb;
    private readonly bool is_owned;
    private const string TABLE_NAME = "fwkeys";

    /// <summary>Uses a caller-owned wrapper, retained for existing callers.</summary>
    public FwKeysXmlRepository(DB db)
    {
        createDb = () => db;
    }

    /// <summary>Creates and disposes a separate wrapper per operation, suitable for singleton key services.</summary>
    public FwKeysXmlRepository(Func<DB> createDb)
    {
        this.createDb = createDb;
        is_owned = true;
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var db = createDb();
        try
        {
            return db.col(TABLE_NAME, new FwDict { ["itype"] = ITYPE_DATA_PROTECTION_KEY }, "XmlValue")
                .Select(value => XElement.Parse(value.toStr())).ToList();
        }
        catch (Exception)
        {
            throw new CryptographicException("Unable to read the durable Data Protection key store. Initialize or restore the database first.");
        }
        finally
        {
            if (is_owned)
                db.Dispose();
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        var db = createDb();
        try
        {
            // Revocation elements need a stable name too; they do not have a key id.
            var keyId = element.Attribute("id")?.Value ?? friendlyName;
            if (string.IsNullOrWhiteSpace(keyId))
                throw new CryptographicException("A key-store element requires a name.");

            var where = new FwDict { ["itype"] = ITYPE_DATA_PROTECTION_KEY, ["iname"] = keyId };
            var fields = new FwDict { ["XmlValue"] = element.ToString(SaveOptions.DisableFormatting), ["upd_time"] = DB.NOW };
            if (db.value(TABLE_NAME, where, "1").toBool())
            {
                if (db.update(TABLE_NAME, fields, where) != 1)
                    throw new CryptographicException("A protection key could not be updated.");
            }
            else
            {
                fields["itype"] = ITYPE_DATA_PROTECTION_KEY;
                fields["iname"] = keyId;
                db.insert(TABLE_NAME, fields);
            }
        }
        catch (Exception)
        {
            throw new CryptographicException("Unable to persist a durable Data Protection key.");
        }
        finally
        {
            if (is_owned)
                db.Dispose();
        }
    }
}
