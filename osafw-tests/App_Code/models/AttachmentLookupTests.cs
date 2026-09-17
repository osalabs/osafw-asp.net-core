using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;

namespace osafw.Tests;

[TestClass]
public class AttachmentLookupTests
{
    private sealed class LookupDb : RejectingDb
    {
        internal FwDict Where = [];
        internal DBList Rows = [];
        internal int Selects;
        public override DBRow row(string table, FwDict where, string order_by = "") => table switch
        {
            "att_categories" => where["icode"].toStr() == "known" ? new DBRow { ["id"] = "5" } : [],
            "fwentities" => where["icode"].toStr() == "lookup_parents" || where["id"].toInt() == 6
                ? new DBRow { ["id"] = "6", ["icode"] = "lookup_parents" }
                : where["id"].toInt() == 7
                    ? new DBRow { ["id"] = "7", ["icode"] = "nonconventional_parent" }
                    : where["id"].toInt() == 8
                        ? new DBRow { ["id"] = "8", ["icode"] = "missing_parent_model" }
                        : [],
            _ => throw Unexpected("row " + table)
        };
        public override DBList array(string table, FwDict where, string order_by = "", ICollection? aselect_fields = null, int offset = 0, int limit = -1)
        {
            Where = new FwDict(where);
            Selects++;
            return Rows;
        }
    }

    private class CheckedAtt : Att
    {
        internal List<int> Checks = [];
        internal bool Deny;
        protected override FwDict oneActive(int id) => DB.h("id", id, "fname", "public-document.txt", "fwentities_id", 6, "item_id", 3);
        public override void checkAccess(int id = 0, string action = "")
        {
            Assert.AreEqual(ACCESS_ACTION_VIEW, action);
            Checks.Add(id);
            if (Deny) throw new AuthException();
        }
    }

    private sealed class LookupParents : FwModel
    {
        internal bool Deny;
        internal List<int> Checks = [];
        public override void checkAccess(int id = 0, string action = "")
        {
            Checks.Add(id);
            Assert.AreEqual(Att.ACCESS_ACTION_VIEW, action);
            if (Deny) throw new AuthException();
        }
    }

    private sealed class CustomParentPolicyAtt : CheckedAtt
    {
        internal HashSet<(int EntityId, int ItemId)> AllowedBindings = [];
        internal List<(int EntityId, int ItemId)> BindingChecks = [];

        protected override bool isParentAccessAllowed(FwDict item, string action)
        {
            Assert.AreEqual(ACCESS_ACTION_VIEW, action);
            Assert.AreEqual(7, item["id"].toInt());
            Assert.AreEqual("public-document.txt", item["fname"]);
            var binding = (item["fwentities_id"].toInt(), item["item_id"].toInt());
            BindingChecks.Add(binding);
            return AllowedBindings.Contains(binding);
        }
    }

    private static CheckedAtt Attachment(FW fw)
    {
        var att = new CheckedAtt();
        att.init(fw);
        fw.registerModelForTesting<Att>(att);
        return att;
    }

    [TestMethod]
    public void CategoryLookupDistinguishesOmittedAndZeroFiltersAndChecksEachResult()
    {
        var db = new LookupDb { Rows = [new DBRow { ["id"] = "7" }, new DBRow { ["id"] = "8" }] };
        using var scope = new FwTestScope(_ => db);
        var att = Attachment(scope.Fw);
        Assert.HasCount(2, att.listByCategory("known"));
        Assert.AreEqual(5, db.Where["att_categories_id"].toInt());
        Assert.AreEqual(FwModel.STATUS_ACTIVE, db.Where["status"]);
        Assert.IsFalse(db.Where.ContainsKey("item_id"));
        CollectionAssert.AreEqual(new[] { 7, 8 }, att.Checks);
        att.listByCategory("known", 0, 1);
        Assert.AreEqual(0, db.Where["item_id"]);
        Assert.AreEqual(1, db.Where["is_image"]);
        Assert.IsEmpty(att.listByCategory("unknown"));
        Assert.IsEmpty(att.listByCategory(""));
        Assert.AreEqual(2, db.Selects);
        att.Deny = true;
        Assert.ThrowsExactly<AuthException>(() => att.listByCategory("known"));
    }

    [TestMethod]
    public void AllEntityLookupIsReadOnlyAndExistingZeroAndCategoryDefaultsRemainExact()
    {
        var db = new LookupDb();
        using var scope = new FwTestScope(_ => db);
        var att = Attachment(scope.Fw);
        Assert.IsEmpty(att.listAllByEntity("unknown"));
        Assert.AreEqual(0, db.Selects);
        att.listAllByEntity("lookup_parents", 0);
        Assert.AreEqual(6, db.Where["fwentities_id"].toInt());
        Assert.AreEqual(0, db.Where["is_image"]);
        Assert.IsFalse(db.Where.ContainsKey("item_id"));
        att.listByEntity("lookup_parents", 0);
        Assert.AreEqual(0, db.Where["item_id"]);
        att.listByEntityCategory("lookup_parents", 4);
        Assert.AreEqual(4, db.Where["item_id"]);
        Assert.AreEqual(0, db.Where["att_categories_id"].toInt());
    }

    [TestMethod]
    public void ReverseLookupAuthorizesAttachmentBeforeQueryAndEveryLinkedParent()
    {
        var db = new LookupDb { Rows = [new DBRow { ["att_id"] = "7", ["fwentities_id"] = "6", ["item_id"] = "9" }] };
        using var scope = new FwTestScope(_ => db);
        var att = Attachment(scope.Fw);
        var parent = new LookupParents();
        parent.init(scope.Fw);
        scope.Fw.registerModelForTesting(parent);
        var links = scope.Fw.model<AttLinks>();
        Assert.HasCount(1, links.listByAtt(7));
        Assert.AreEqual(7, db.Where["att_id"]);
        Assert.AreEqual(FwModel.STATUS_ACTIVE, db.Where["status"]);
        CollectionAssert.AreEqual(new[] { 9 }, parent.Checks);
        parent.Deny = true;
        Assert.ThrowsExactly<AuthException>(() => links.listByAtt(7));
        att.Deny = true;
        var selects = db.Selects;
        Assert.ThrowsExactly<AuthException>(() => links.listByAtt(7));
        Assert.AreEqual(selects, db.Selects);
        att.Deny = false;
        db.Rows = [new DBRow { ["att_id"] = "7", ["fwentities_id"] = "999", ["item_id"] = "9" }];
        Assert.ThrowsExactly<AuthException>(() => links.listByAtt(7));
    }

    [TestMethod]
    public void ReverseLookupUsesCustomPolicyForNonconventionalParent()
    {
        var db = new LookupDb { Rows = [new DBRow { ["att_id"] = "7", ["fwentities_id"] = "7", ["item_id"] = "9" }] };
        using var scope = new FwTestScope(_ => db);
        var att = new CustomParentPolicyAtt();
        att.AllowedBindings.Add((7, 9));
        att.init(scope.Fw);
        scope.Fw.registerModelForTesting<Att>(att);

        Assert.HasCount(1, scope.Fw.model<AttLinks>().listByAtt(7));
        CollectionAssert.AreEqual(new[] { (7, 9) }, att.BindingChecks);
    }

    [TestMethod]
    public void ReverseLookupChecksEveryLinkedParentAndRejectsDeniedOtherParent()
    {
        var db = new LookupDb
        {
            Rows =
            [
                new DBRow { ["att_id"] = "7", ["fwentities_id"] = "7", ["item_id"] = "9" },
                new DBRow { ["att_id"] = "7", ["fwentities_id"] = "7", ["item_id"] = "10" }
            ]
        };
        using var scope = new FwTestScope(_ => db);
        var att = new CustomParentPolicyAtt();
        att.AllowedBindings.Add((7, 9));
        att.init(scope.Fw);
        scope.Fw.registerModelForTesting<Att>(att);

        Assert.ThrowsExactly<AuthException>(() => scope.Fw.model<AttLinks>().listByAtt(7));
        CollectionAssert.AreEqual(new[] { (7, 9), (7, 10) }, att.BindingChecks);
    }

    [TestMethod]
    public void ReverseLookupNormalizesMissingParentModelToAuthException()
    {
        var db = new LookupDb { Rows = [new DBRow { ["att_id"] = "7", ["fwentities_id"] = "8", ["item_id"] = "9" }] };
        using var scope = new FwTestScope(_ => db);
        Attachment(scope.Fw);

        Assert.ThrowsExactly<AuthException>(() => scope.Fw.model<AttLinks>().listByAtt(7));
    }
}
