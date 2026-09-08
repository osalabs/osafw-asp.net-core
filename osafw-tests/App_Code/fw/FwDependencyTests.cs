using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace osafw.Tests;

[TestClass]
public class FwDependencyTests
{
    public class Notes : FwModel
    {
        public Notes()
        {
            table_name = "notes";
            is_log_changes = false;
            field_upd_time = "";
        }
    }

    public class ArchiveNotes : Notes
    {
        public ArchiveNotes() { db_config = "archive"; }
    }

    private sealed class NoteDb(string name) : RejectingDb(name)
    {
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public int Disposals { get; private set; }
        public bool FailWrites { get; set; }
        public bool FailDispose { get; set; }
        public HttpContext? BoundContext => context;
        public bool HasLogger => ext_logger != null;

        public override DBRow row(string table, FwDict where, string order_by = "")
        {
            Assert.AreEqual("notes", table);
            Assert.AreEqual(7, where["id"]);
            Reads++;
            return new DBRow { ["id"] = "7", ["iname"] = db_name };
        }

        public override int update(string table, FwDict fields, FwDict where)
        {
            Assert.AreEqual("notes", table);
            Assert.AreEqual(7, where["id"]);
            Assert.AreEqual("changed", fields["iname"]);
            Writes++;
            if (FailWrites)
                throw new InvalidOperationException("Controlled write failure.");
            return 1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Disposals++;
            base.Dispose(disposing);
            if (FailDispose)
                throw new InvalidOperationException("Controlled disposal failure.");
        }
    }

    [TestMethod]
    public void Models_UseExplicitMainAndNamedDependencies_AndKeepModelCaching()
    {
        var main = new NoteDb("main");
        var archive = new NoteDb("archive");
        using var scope = new FwTestScope(name => name switch
        {
            "main" => main,
            "archive" => archive,
            _ => throw new InvalidOperationException("Unknown DB name."),
        });
        var fw = scope.Fw;

        Assert.AreSame(main, fw.model<Notes>().getDB());
        Assert.AreSame(archive, fw.model<ArchiveNotes>().getDB());
        Assert.AreSame(fw.model<ArchiveNotes>(), fw.model(typeof(ArchiveNotes)));
        Assert.AreSame(fw.model<ArchiveNotes>(), fw.model(nameof(ArchiveNotes)));
        Assert.AreEqual("main", fw.model<Notes>().one(7)["iname"]);
        Assert.AreEqual("archive", fw.model<ArchiveNotes>().one(7)["iname"]);
        Assert.AreEqual("archive", fw.model<ArchiveNotes>().one(7)["iname"]);
        Assert.AreEqual(1, archive.Reads);
        Assert.AreSame(archive, fw.getDB("archive"));
        Assert.ThrowsExactly<InvalidOperationException>(() => fw.getDB("unknown"));
        Assert.AreEqual(0, main.Writes + archive.Writes);
    }

    [TestMethod]
    public void ModelUpdate_PropagatesFailure_ThenInvalidatesCachedRowOnlyOnSuccess()
    {
        var db = new NoteDb("main") { FailWrites = true };
        using var scope = new FwTestScope(_ => db);
        var model = scope.Fw.model<Notes>();
        model.one(7);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => model.update(7, DB.h("iname", "changed")));
        Assert.AreEqual("Controlled write failure.", error.Message);
        model.one(7);
        Assert.AreEqual(1, db.Reads);

        db.FailWrites = false;
        Assert.IsTrue(model.update(7, DB.h("iname", "changed")));
        model.one(7);
        Assert.AreEqual(2, db.Reads);
        Assert.AreEqual(2, db.Writes);
    }

    [TestMethod]
    public void Factory_BindsContextLoggerAndPrivacy_AndDisposesDistinctResultsOnce()
    {
        var context = TestHelpers.CreateHttpContext("");
        var main = new NoteDb("main") { is_log_pii = true };
        var archive = new NoteDb("archive") { is_log_pii = true };
        using var scope = new FwTestScope(name => name == "main" ? main : archive, context: context);
        var fw = scope.Fw;
        fw.getDB("archive");
        fw.getDB("archive");

        Assert.AreSame(context, archive.BoundContext);
        Assert.IsTrue(archive.HasLogger);
        Assert.IsFalse(main.is_log_pii);
        Assert.IsFalse(archive.is_log_pii);
        fw.Dispose();
        fw.Dispose();
        Assert.AreEqual(1, main.Disposals);
        Assert.AreEqual(1, archive.Disposals);
        Assert.ThrowsExactly<ObjectDisposedException>(() => fw.getDB());
    }

    [TestMethod]
    public void Dispose_OwnsOriginalAndReplacementMainDb_ExactlyOnce()
    {
        var original = new NoteDb("original");
        var replacement = new NoteDb("replacement");
        using var scope = new FwTestScope(_ => original);
        scope.Fw.db = replacement;

        scope.Dispose();
        scope.Dispose();
        Assert.AreEqual(1, original.Disposals);
        Assert.AreEqual(1, replacement.Disposals);
    }

    [TestMethod]
    public void Dispose_AttemptsEveryOwnedWrapper_WhenOneFails()
    {
        var main = new NoteDb("main") { FailDispose = true };
        var archive = new NoteDb("archive");
        using var scope = new FwTestScope(name => name == "main" ? main : archive);
        scope.Fw.getDB("archive");

        var error = Assert.ThrowsExactly<AggregateException>(() => scope.Fw.Dispose());
        Assert.HasCount(1, error.InnerExceptions);
        Assert.AreEqual(1, main.Disposals);
        Assert.AreEqual(1, archive.Disposals);
    }

    [TestMethod]
    public void ConstructorFailure_DisposesReturnedDependency_AndRestoresConfiguration()
    {
        var original = FwConfig.GetCurrentSettings();
        var main = new NoteDb("main");
        var context = TestHelpers.CreateHttpContext("");
        context.Session.SetString("_flash", "[]"); // Invalid flash shape fails after DB initialization.

        Assert.ThrowsExactly<InvalidCastException>(() => new FwTestScope(_ => main, context: context));
        Assert.AreEqual(1, main.Disposals);
        Assert.AreSame(original, FwConfig.GetCurrentSettings());
    }

    [TestMethod]
    public void Factory_IsAvailableDuringBaseConstruction_AndOfflineContextIsCleared()
    {
        var main = new NoteDb("main");
        main.setContext(TestHelpers.CreateHttpContext(""));
        using var settings = FwConfig.beginScope();
        using var fw = new DerivedFw(main);
        Assert.AreSame(main, fw.db);
        Assert.AreSame(main, fw.InitializedDependency);
        Assert.IsNull(main.BoundContext);
    }

    private sealed class DerivedFw(DB dependency) : FW(null, new ConfigurationBuilder().Build(), _ => dependency)
    {
        public DB InitializedDependency { get; } = dependency;
    }

    [TestMethod]
    public void SuppliedFactory_NeverFallsBackToConfiguredDependencies()
    {
        using var scope = new FwTestScope(_ => new RejectingDb(), new Dictionary<string, string?>
        {
            ["appSettings:db:archive:type"] = "SQL",
            ["appSettings:db:archive:connection_string"] = "invalid-unused-connection",
        });
        Assert.ThrowsExactly<InvalidOperationException>(() => new FwTestScope(_ => null!));
        Assert.ThrowsExactly<InvalidOperationException>(() => new FwTestScope(_ => throw new InvalidOperationException("Rejected.")));
        using var db = scope.Fw.getDB("archive");
        Assert.ThrowsExactly<InvalidOperationException>(() => db.rowp("SELECT 1"));
        Assert.ThrowsExactly<InvalidOperationException>(() => db.arrayp<NoteDto>("SELECT 1"));
        Assert.ThrowsExactly<InvalidOperationException>(() => db.insert("notes", DB.h("iname", "value")));
        Assert.ThrowsExactly<InvalidOperationException>(() => db.tableSchemaFull("notes"));
        Assert.ThrowsExactly<InvalidOperationException>(() => db.begin());
        Assert.ThrowsExactly<InvalidOperationException>(() => db.createConnection("invalid-unused-connection"));
    }

    [TestMethod]
    public void QueryOverride_UsesRealTypedMapping_AndPreservesMissingRowContracts()
    {
        using var db = new ReaderDb();
        Assert.AreEqual("sample", db.rowp<NoteDto>("synthetic")!.Title);
        Assert.IsNull(db.rowp<NoteDto>("synthetic"));
        Assert.IsEmpty(db.rowp("synthetic"));
        Assert.AreEqual(3, db.Readers.Count);
        foreach (var reader in db.Readers)
            Assert.IsTrue(reader.IsClosed);
    }

    private sealed class ReaderDb : RejectingDb
    {
        public List<DbDataReader> Readers { get; } = [];

        public override DbDataReader query(string sql, FwDict? in_params = null)
        {
            Assert.AreEqual("synthetic", sql);
            var table = new DataTable();
            table.Columns.Add("iname", typeof(string));
            if (Readers.Count == 0)
                table.Rows.Add("sample");
            var reader = table.CreateDataReader();
            Readers.Add(reader);
            return reader;
        }
    }

    public class NoteDto
    {
        [DBName("iname")]
        public string Title { get; set; } = "";
    }

    [TestMethod]
    public void Logger_ReturnsPreviousDelegate_ForSuppressionAndRestoration()
    {
        using var db = new RejectingDb();
        var calls = 0;
        DB.LoggerDelegate callback = (_, _) => calls++;
        Assert.IsNull(db.setLogger(callback));
        Assert.AreSame(callback, db.setLogger(null));
        db.logger(LogLevel.ERROR, "suppressed");
        Assert.AreEqual(0, calls);
        Assert.IsNull(db.setLogger(callback));
        db.logger(LogLevel.ERROR, "visible");
        Assert.AreEqual(1, calls);
        Assert.IsFalse(db.is_log_pii);
    }
}
