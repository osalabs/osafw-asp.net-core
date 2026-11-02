using Microsoft.Data.SqlClient;
using System;
using System.IO;

namespace osafw.Tests;

// Owns only a database whose CREATE succeeded in this process. Never uses app configuration.
internal sealed class SqlServerTestDatabase : IDisposable
{
    private readonly string name = "osafw_test_" + Guid.NewGuid().ToString("N");
    private readonly string master;
    private bool isOwned;
    public string ConnectionString { get; }

    public SqlServerTestDatabase()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Environment.GetEnvironmentVariable("FW_TEST_SQLSERVER_SERVER") ?? @"(localdb)\MSSQLLocalDB",
            InitialCatalog = "master",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 10,
            Pooling = false,
        };
        master = builder.ConnectionString;
        builder.InitialCatalog = name;
        ConnectionString = builder.ConnectionString;
        try
        {
            ExecuteMaster("CREATE DATABASE [" + name + "]");
            isOwned = true;
            using var db = new DB(ConnectionString, DB.DBTYPE_SQLSRV);
            var root = Path.Combine(TestHelpers.RepoRoot(), "osafw-app", "App_Data", "sql");
            foreach (var script in new[] { "fwdatabase.sql", "database.sql", "settings.sql", "demo.sql", "lookups.sql", "views.sql", "roles.sql" })
            {
                var path = Path.Combine(root, script);
                if (script == "database.sql" && !File.Exists(path))
                    continue; // Optional application schema, as in DevCli initialization.
                db.execMultipleSQL(File.ReadAllText(path));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void ExecuteMaster(string sql)
    {
        using var connection = new SqlConnection(master);
        connection.Open();
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (!isOwned)
            return;
        ExecuteMaster("ALTER DATABASE [" + name + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + name + "]");
        isOwned = false;
    }
}
