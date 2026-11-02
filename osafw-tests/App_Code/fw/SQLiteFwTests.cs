#if isSQLite
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace osafw.Tests;

[TestClass]
public class SQLiteFwTests
{
    public class NoteRow
    {
        public int id { get; set; }
        [DBName("iname")]
        public string Title { get; set; } = "";
        public int? quantity { get; set; }
    }

    public class SqliteNotes : FwModel<NoteRow>
    {
        public SqliteNotes()
        {
            table_name = "notes";
            db_config = "archive";
            is_log_changes = false;
            field_upd_time = "";
        }
    }

    [TestMethod]
    public void NamedModel_UsesDisposableSqlite_ForCrudMappingMissingRowsAndRollback()
    {
        using var file = new SqliteFile();
        var database = file.CreateDb("archive");
        using var scope = new FwTestScope(name => name switch
        {
            "main" => new RejectingDb(),
            "archive" => database,
            _ => throw new InvalidOperationException("Unknown DB name."),
        });
        var model = scope.Fw.model<SqliteNotes>();
        database.exec("CREATE TABLE notes (id INTEGER PRIMARY KEY AUTOINCREMENT, iname TEXT NOT NULL, quantity INTEGER NULL)");
        var row = new NoteRow { Title = "sample", quantity = null };
        var id = model.add(row);

        Assert.AreEqual(id, row.id);
        Assert.AreEqual("sample", model.oneT(id)!.Title);
        Assert.IsNull(model.oneT(id)!.quantity);
        Assert.AreEqual("sample", model.one(id)["iname"]);
        Assert.IsTrue(model.update(id, DB.h("iname", "revised", "quantity", 3)));
        Assert.AreEqual("revised", model.oneT(id)!.Title);
        Assert.AreEqual(3, model.oneT(id)!.quantity);
        Assert.HasCount(1, database.array<NoteRow>("notes", []));
        Assert.IsFalse(model.update(id + 100, DB.h("iname", "absent")));

        database.begin();
        database.update("notes", DB.h("iname", "rolled back"), DB.h("id", id));
        database.rollback();
        Assert.AreEqual("revised", database.row<NoteRow>("notes", DB.h("id", id))!.Title);

        model.delete(id, true);
        Assert.IsEmpty(model.one(id));
        Assert.IsNull(model.oneT(id));
        Assert.IsNull(database.row<NoteRow>("notes", DB.h("id", id)));
        Assert.IsNull(database.rowp<NoteRow>("SELECT * FROM notes WHERE id=@id", DB.h("id", id)));
        Assert.IsEmpty(database.rowp("SELECT * FROM notes WHERE id=@id", DB.h("id", id)));
        Assert.IsEmpty(database.arrayp<NoteRow>("SELECT * FROM notes"));
        Assert.ThrowsExactly<NotFoundException>(() => model.oneTOrFail(id));

        var connection = database.getConnection();
        scope.Fw.Dispose();
        Assert.AreEqual(ConnectionState.Closed, connection.State);
    }

    [TestMethod]
    public void DefaultFactory_KeepsWrappersFresh_AndExistingHttpConnectionSharing()
    {
        using var file = new SqliteFile();
        using var settings = FwConfig.beginScope();
        var configuration = file.Configuration();
        var context = TestHelpers.CreateHttpContext("");
        using var fw = new FW(context, configuration);
        var first = fw.db;
        var second = fw.getDB();
        var named = fw.getDB("archive");
        first.sql_command_timeout = 11;

        Assert.AreNotSame(first, second);
        Assert.AreNotSame(second, named);
        Assert.AreEqual(30, second.sql_command_timeout);
        Assert.AreEqual(30, named.sql_command_timeout);
        var connection = first.connect();
        Assert.AreSame(connection, second.connect());
        Assert.AreSame(connection, named.connect());
        first.exec("CREATE TABLE notes (id INTEGER PRIMARY KEY)");
        Assert.AreEqual(0, second.valuep("SELECT COUNT(*) FROM notes").toInt());

        fw.Dispose();
        Assert.AreEqual(ConnectionState.Closed, connection.State);
    }

    [TestMethod]
    public void DefaultFactory_ClosesEveryOfflineConnection_IncludingNamedAndRepeatedWrappers()
    {
        using var file = new SqliteFile();
        using var settings = FwConfig.beginScope();
        using var fw = new FW(null, file.Configuration());
        var main = fw.db.connect();
        var named = fw.getDB("archive").connect();
        var repeated = fw.getDB("archive").connect();

        Assert.AreNotSame(main, named);
        Assert.AreNotSame(named, repeated);
        fw.Dispose();
        Assert.AreEqual(ConnectionState.Closed, main.State);
        Assert.AreEqual(ConnectionState.Closed, named.State);
        Assert.AreEqual(ConnectionState.Closed, repeated.State);
    }

}
#endif
