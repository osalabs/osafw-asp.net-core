using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace osafw.Tests;

// Owns isolated settings and the FW lifetime. Every DB name must be supplied explicitly.
internal sealed class FwTestScope : IDisposable
{
    private readonly IDisposable settingsScope;
    public FW Fw { get; }

    public FwTestScope(Func<string, DB> databases, IDictionary<string, string?>? settings = null, HttpContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(databases);
        settingsScope = FwConfig.beginScope();
        try
        {
            var values = new Dictionary<string, string?>
            {
                ["appSettings:log_level"] = "0",
                ["appSettings:log"] = "",
            };
            if (settings != null)
                foreach (var pair in settings)
                    values[pair.Key] = pair.Value;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            Fw = new FW(context, configuration, databases);
        }
        catch
        {
            settingsScope.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try { Fw.Dispose(); }
        finally { settingsScope.Dispose(); }
    }
}

// No connection string, settings lookup, SQL emulation, or success defaults.
// Specific tests override only the operations they expect.
internal class RejectingDb : DB
{
    public RejectingDb(string name = "main") : base("", DBTYPE_SQLSRV, name) { }

    public override DbConnection connect() => throw Unexpected("connect");
    public override DbConnection createConnection(string connstr, string dbtype = "SQL") => throw Unexpected("createConnection");
    public override DbDataReader query(string sql, FwDict? in_params = null) => throw Unexpected("query");
    public override int exec(string sql, FwDict? @params = null, bool is_get_identity = false) => throw Unexpected("exec");
    public override FwList loadTableSchemaFull(string table) => throw Unexpected("schema");

    protected static InvalidOperationException Unexpected(string operation) => new("Unexpected DB operation: " + operation);
}
