using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace osafw.Tests;

[TestClass]
public class NullableConversionContractTests
{
    [TestMethod]
    public void NullableStringInputsUseExistingConversionDefaults()
    {
        string? value = null;
        Assert.AreEqual(DateTime.MinValue, value.toDate());
        Assert.IsNull(value.toDateOrNull());
        Assert.AreEqual(7m, value.toDecimal(7m));
        Assert.AreEqual(7d, value.toDouble(7d));
        Assert.AreEqual(7f, value.toFloat(7f));
        Assert.AreEqual(7, value.toInt(7));
        Assert.AreEqual(7L, value.toLong(7L));
        Assert.AreEqual(2147483648L, "2147483648".toLong());
        Assert.AreEqual(7, "2147483648".toInt(7));
        Assert.AreEqual(12, "12".toInt());
    }

    [TestMethod]
    public void OptionalModelColumnsPreserveDatabaseNullAndNewRowDefaults()
    {
        FwDict values = new() { ["idesc"] = DBNull.Value, ["add_users_id"] = DBNull.Value, ["upd_users_id"] = DBNull.Value };
        AssertOptional(values.to<AttCategories.Row>().toFwDict(), true);
        AssertOptional(values.to<DemoDicts.Row>().toFwDict(), true);
        AssertOptional(values.to<Permissions.Row>().toFwDict(), true);
        AssertOptional(values.to<Resources.Row>().toFwDict(), true);
        AssertOptional(values.to<Roles.Row>().toFwDict(), true);
        AssertOptional(values.to<RolesResourcesPermissions.Row>().toFwDict(), false);
        AssertOptional(values.to<UsersRoles.Row>().toFwDict(), false);
        var att = values.to<Att.Row>();
        AssertOptional(att.toFwDict(), false);
        Assert.AreEqual(0, new Att.Row().add_users_id);
        Assert.AreEqual("", new AttCategories.Row().idesc);
        new FwDict { ["fsize"] = 2147483648L }.applyTo(att);
        Assert.AreEqual(2147483648L, att.fsize);
    }

    private static void AssertOptional(FwDict row, bool description)
    {
        if (description) Assert.IsNull(row["idesc"]);
        Assert.IsNull(row["add_users_id"]);
        Assert.IsNull(row["upd_users_id"]);
    }
}
