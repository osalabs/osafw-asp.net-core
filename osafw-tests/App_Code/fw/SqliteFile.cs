#if isSQLite
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Collections.Generic;
namespace osafw.Tests;

internal sealed class SqliteFile : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "osafw-testability-" + Guid.NewGuid().ToString("N") + ".sqlite");
    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Pooling = false,
        ForeignKeys = true,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    public DB CreateDb(string name) => new(DB.h(
        "type", DB.DBTYPE_SQLITE, "connection_string", ConnectionString, "timezone", "UTC"), name);

    public IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["appSettings:log_level"] = "0",
        ["appSettings:db:main:type"] = DB.DBTYPE_SQLITE,
        ["appSettings:db:main:connection_string"] = ConnectionString,
        ["appSettings:db:main:timezone"] = "UTC",
        ["appSettings:db:archive:type"] = DB.DBTYPE_SQLITE,
        ["appSettings:db:archive:connection_string"] = ConnectionString,
        ["appSettings:db:archive:timezone"] = "UTC",
    }).Build();

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
            File.Delete(path + suffix);
    }
}
#endif
