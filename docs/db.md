# DB.cs

Simplified database helper for SQL Server, SQLite, MySQL or MS Access. Part of the [OSA Framework](https://github.com/osalabs/osafw-asp.net-core).

`DB` wraps ADO.NET and hides repetitive plumbing. It automatically opens connections, builds parametrised queries and converts results to handy collections or custom types.

## Why DB.cs?
Working with raw `SqlConnection` is verbose. With `DB` the same query looks like:

```csharp
var db = fw.getDB();
var rows = db.array("users", DB.h("status", 0));
foreach (DBRow row in rows)
    Console.WriteLine(row["iname"]);
```

instead of manual connection/command/reader handling.

## Getting started

A `DB` instance is usually created by the framework but can be constructed directly:

```csharp
var db = new DB("Server=(local);Database=demo;Trusted_Connection=True;", DB.DBTYPE_SQLSRV);
```

You rarely call `connect()`/`disconnect()` yourself – the first query opens the connection automatically.

### Database lifetime and explicit dependencies

Ordinary `fw.getDB(name)` calls create fresh wrappers; `fw.db` is the initial main wrapper. Wrapper timeouts and transaction handles are separate. With an HTTP context, wrappers using the same connection string can still share a physical connection. Offline wrappers normally open separate connections.

FW disposes every distinct wrapper it obtains, including named/repeated calls and a replacement assigned to `fw.db`. Keep those wrappers within the FW lifetime; use a directly constructed, caller-owned `DB` for a longer lifetime. Disposal attempts every wrapper and the logger even if one fails, then throws an `AggregateException`. Calls to `getDB` after FW disposal throw `ObjectDisposedException`. Constructor failures also clean up returned dependencies.

For an explicit dependency source, use the constructor overload:

```csharp
using var settingsScope = FwConfig.beginScope();
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["appSettings:log_level"] = "0",
    }).Build();

using var fw = new FW(null, configuration, name => name switch
{
    "main" => new RejectingDb(), // test-owned fake; see FwTestScope.cs
    "archive" => new DB(new FwDict
    {
        ["type"] = DB.DBTYPE_SQLITE,
        ["connection_string"] = disposableConnectionString,
        ["timezone"] = "UTC",
    }, name),
    _ => throw new InvalidOperationException("Unexpected DB name."),
});
```

The factory receives every requested name, starting with `main` during base construction. It must return a DB or throw; a null result fails and never falls back to configured credentials. Pass dependencies through the constructor arguments rather than a virtual FW creation override. FW binds the context (null offline), logger, and `log_pii` policy. Returned instances transfer to FW ownership; allocations never returned remain the factory/caller's responsibility. A factory can return the same instance repeatedly to explicitly share its timeout/transaction state within one FW. Do not reuse an instance across FW lifetimes.

For tests, keep configuration in memory and allow only explicit names. [FwTestScope.cs](../osafw-tests/App_Code/fw/FwTestScope.cs) combines configuration and FW disposal. [FwDependencyTests.cs](../osafw-tests/App_Code/fw/FwDependencyTests.cs) demonstrates synthetic models and small strict fakes. [SQLiteFwTests.cs](../osafw-tests/App_Code/fw/SQLiteFwTests.cs) demonstrates a unique local SQLite file, typed mapping, missing rows, CRUD, rollback, and connection lifetime; it closes connections before deleting only its own file and sidecars.

### Small DB substitutes

Dictionary and typed `row`/`rowp` and `array`/`arrayp`, `col`/`colp`, `value`/`valuep`, `insert`, `update`, and `del` expose virtual operation boundaries. `query` is a lower-level read hook when an explicit ADO.NET reader is useful; its returned reader must be owned by the caller, which closes it through `closeQuery`. Existing conversion and missing-row semantics still apply: dictionary reads return an empty row, typed single-row reads return null.

A query override alone does not replace schema discovery or the connection work done before table queries. `loadTableSchemaFull` and `schemaFieldType` are virtual metadata boundaries; `connect` and `createConnection` are virtual connection boundaries. A strict fake should reject connection creation and unexpected operations, overriding only the behavior needed by the test. Do not return success by default or emulate a SQL engine. Use disposable SQLite when testing relational behavior.

`setLogger(callback)` returns the previous delegate; `setLogger(null)` suppresses logging and its returned delegate can be restored in a `finally` block. The method does not change PII redaction. Ordinary statement calls can ignore the return value. Code assigning the method group to an `Action<DB.LoggerDelegate?>` must use a lambda, for example `logger => { db.setLogger(logger); }`, or use a `Func<DB.LoggerDelegate?, DB.LoggerDelegate?>` when the previous delegate is needed.

### Isolated configuration scopes

`FwConfig.beginScope()` creates empty settings/cache state for the current async flow. Initialize it with `FwConfig.init` or an FW constructor. All static `FwConfig` consumers and `fw.config()` use the active scope, including host trust, route prefixes, reload, and configuration reinitialization. Nested scopes restore the prior state when disposed, including after exceptions. Repeating a successful disposal is harmless and leaves the current scope unchanged. Genuinely out-of-order disposal still throws and can be retried after inner scopes finish. Dispose scopes in nesting order, after their FW instances and dependent async work finish.

Child tasks inherit a scope and its mutable settings; independently initialized scopes have separate host buckets, nested dictionaries, base settings, and trusted-host caches, even with the same configuration provider. FW configuration is still ambient: use an FW only inside its intended active scope. This is not a per-instance configuration snapshot. Outside a scope, the existing shared application behavior remains.

Scopes do not isolate environment variables, caller-owned configuration-provider mutations, `FwCache.MemoryCache`, DB schema caches, or static SQL diagnostics. Do not mutate process environment variables for parallel tests. The existing suite retains its assembly-level parallelism restriction; targeted concurrent scope tests prove the narrower configuration guarantee.

For the current Debug defaults, run the safe suites with:

```powershell
dotnet test osafw-tests/osafw-tests.csproj --filter 'FullyQualifiedName!~osafw.Tests.DBTests'
dotnet test osafw-tests/osafw-tests.csproj '-p:DefineConstants=TRACE%3BDEBUG%3BisSQLite' --filter 'FullyQualifiedName!~osafw.Tests.DBTests'
```

The excluded legacy class connects to a configured SQL Server database and mutates a test table. Do not include it without separate resource authority. Preserve any additional project constants when selecting the SQLite variant. SQLite evidence does not prove other providers' SQL, locking, precision, mapping, or deployment behavior.

### SQLite provider
SQLite is optional and intended for embedded single-node deployments or disposable provider-neutral local/integration tests. It is the preferred database for parallel worktree isolation when the task does not depend on SQL Server-specific SQL, types, locking, or deployment behavior. SQL Server is the production-primary provider.

To enable SQLite:

1. Define `isSQLite` in `osafw-app/osafw-app.csproj` or pass `-p:DefineConstants=isSQLite` when building.
2. The project file includes the `Microsoft.Data.Sqlite` package automatically when `isSQLite` is defined.
3. Configure the main database:

```json
"db": {
  "main": {
    "connection_string": "Data Source=App_Data/db/osafw.sqlite;Mode=ReadWriteCreate;Foreign Keys=True;Default Timeout=30;Pooling=True;",
    "type": "SQLite",
    "timezone": "UTC"
  }
}
```

4. Initialize with the scripts from `osafw-app/App_Data/sql/sqlite/` in this order: `fwdatabase.sql`, `database.sql`, `lookups.sql`, `views.sql`, then optional `roles.sql` and `demo.sql`.

When the app is compiled with `isSQLite` and `type` is `SQLite`, `FwSessionCache` stores sessions in the SQLite `fwsessions` table and data-protection keys use `fwkeys` through the normal `FwKeysXmlRepository`. For multi-node deployments, use SQL Server or another verified distributed provider/cache instead of SQLite.

Data Protection key XML stored in `fwkeys` is protected with Windows DPAPI before it is written to the database. Non-Windows deployments fail closed by default; for local/dev-only environments that intentionally accept plaintext key XML, change the `ALLOW_PLAINTEXT_DP_KEYS` constant in `Program.cs` to `true`.

### Provider status, fresh schemas, and updates

SQL Server is the production-primary provider and owns the primary schema/update history under `osafw-app/App_Data/sql/`. SQLite is the verified optional provider for embedded single-node deployments and disposable provider-neutral tests; it has matching fresh-install scripts and compile-gated integration tests under `osafw-app/App_Data/sql/sqlite/`.

The MySQL compile/runtime adapter is available, but the bundled MySQL fresh-install schema is not at parity with the SQL Server/SQLite core schema. Do not treat `osafw-app/App_Data/sql/mysql/fwdatabase.sql` and `lookups.sql` as a turnkey new-database setup until their required framework-table parity has been verified or repaired for the target deployment.

Fresh schema files are destructive initialization inputs, not upgrade scripts: the `fwdatabase.sql` variants drop and recreate framework tables. Use them only for a new or disposable database. Existing deployments use additive scripts through `FwUpdates`:

- SQL Server scans `osafw-app/App_Data/sql/updates/`.
- SQLite scans only `osafw-app/App_Data/sql/sqlite/updates/` and does not replay SQL Server updates.
- MySQL scans the SQL Server/root update folder first, then `osafw-app/App_Data/sql/mysql/updates/`; a same-named MySQL file overrides the root file.

When a schema change applies to a provider, update its fresh-install schema and add the provider-appropriate additive script. Verify the exact runtime/provider path rather than assuming SQL syntax or schema parity. The default test run does not compile SQLite-only tests; for SQLite changes, run a focused variant such as:

```powershell
dotnet test osafw-tests/osafw-tests.csproj -p:DefineConstants=isSQLite
```

## API summary

### Optional
- `connect()` – open connection manually
- `disconnect()` – close current connection
- `begin()` / `commit()` / `rollback()` – manage transactions

### Parameterised helpers
- `value(table, where[, field[, order]])`
- `row(table, where[, order])`
- `array(table, where[, order[, fields[, offset[, limit]]]])`
- `col(table, where, field[, order])`
- `insert(table, data)`
- `update(table, data, where)`
- `updateOrInsert(table, data, where)`
- `del(table, where)`

### Raw SQL
- `query(sql, params)`
- `exec(sql, params)` / `update(sql, params)`
- `valuep(sql, params)`
- `rowp(sql, params)`
- `arrayp(sql, params)`
- `colp(sql, params)`

### Helpers
- `qid(str)` / `q(str[, len])` / `qq(str)`
- `qi(obj)` / `qf(obj)` / `qdec(obj)` / `qd(obj)`
- `insql(list)` / `insqli(list)`
- `limit(sql, n[, offset])`
- `sqlNOW()` / `Now()` and constant `DB.NOW`
- `sqlTextExpr(expr)` / `sqlNumberExpr(expr)` / `sqlDateExpr(expr)` / `sqlConcat(...)`
- `left(str, len)`

### Where helpers
- `opEQ(value)` / `opNOT(value)`
- `opLE(value)` / `opLT(value)` / `opGE(value)` / `opGT(value)`
- `opIN(params)` / `opNOTIN(params)`
- `opBETWEEN(from,to)`
- `opLIKE(value)` / `opNOTLIKE(value)`
- `opISNULL()` / `opISNOTNULL()`

### DB structure information
- `tables()`
- `views()`
- `tableSchemaFull(table)`
- `schemaField(table, field)`
- `listForeignKeys([table])`

`loadTableSchemaFull(table)` and `tableSchemaFull(table)` expose provider-normalized `is_computed` metadata for SQL Server computed columns, SQLite stored/virtual generated columns, and MySQL generated columns. The value is `1` for a computed/generated column and `0` otherwise.

The bundled `demos` schema provides a provider-specific example: editable `icode` and `iname` columns produce the computed `display_name` value `CODE — Title`.

### Typed operations
All major methods have `T` versions returning your own classes. Map property names with `[DBName("field")]` when they differ.
Typed single-row reads return `null` when no record is found. The non-generic `row`/`rowp` methods still return an empty `DBRow`, so dictionary callers can keep using `Count` checks.

```csharp
class User
{
    public int id { get; set; }

    [DBName("iname")]
    public string Name { get; set; }
}

User? u = db.row<User>("users", DB.h("id", 1));
if (u == null)
    return;

List<User> list = db.array<User>("users", DB.h());
List<User> page = db.array<User>("users", DB.h("status", 0), "id", offset: 10, limit: 10);
```

`insert`, `update` and `updateOrInsert` also accept typed objects:

```csharp
var user = new User { Name = "John" };
int id = db.insert<User>("users", user);

user.Name = "John Smith";
db.update<User>("users", user, DB.h("id", id));
```

You can convert a dictionary to a typed object using extension helpers:

```csharp
FwDict ht = DB.h("id", 3, "iname", "Alice");
User typed = ht.to<User>();
```

## Usage examples

### Basic CRUD
```csharp
// read single value
string name = db.value("users", DB.h("id", 5), "iname").toStr();

// first row
DBRow row = db.row("users", DB.h("id", 5));

// list of rows
DBList rows = db.array("users", DB.h("status", 0), "iname desc");

// first and second pages
DBList firstPage = db.array("users", DB.h("status", 0), "id", null, 0, 10);
DBList secondPage = db.array("users", DB.h("status", 0), "id", null, 10, 10);

// column values
List<string> names = db.col("users", DB.h("status", 0), "iname");

// insert new user
int id = db.insert("users", DB.h("iname", "John"));

// update record
db.update("users", DB.h("iname", "Jane"), DB.h("id", id));

// update or insert
db.updateOrInsert("users", DB.h("id", id, "iname", "Jack"), DB.h("id", id));

// delete
db.del("users", DB.h("id", id));
```

`array()` and `array<T>()` accept optional `offset, limit` paging arguments after the select-fields argument. `limit = -1` means no limit, and `offset = 0` is the default. When `offset` is greater than zero, pass both a non-negative `limit` and an explicit `order` value so the page is deterministic and portable across SQL Server, SQLite, MySQL, and OLE providers.

### Using raw SQL
```csharp
// execute custom SQL
DbDataReader r = db.query("SELECT * FROM users WHERE id=@id", DB.h("@id", 1));

// non-select statement
db.exec("UPDATE users SET hits=hits+1 WHERE id=@id", DB.h("@id", 1));

// aliases
db.update("UPDATE users SET hits=hits+1 WHERE id=@id", DB.h("@id", 1));

// read helpers
string title = db.valuep("SELECT iname FROM users WHERE id=@id", DB.h("@id", 1)).toStr();
DBRow user = db.rowp("SELECT * FROM users WHERE id=@id", DB.h("@id", 1));
DBList list = db.arrayp("SELECT * FROM users WHERE status=@s", DB.h("@s", 0));
List<string> cols = db.colp("SELECT iname FROM users WHERE status=@s", DB.h("@s", 0));
```

### Executing SQL scripts
`execMultipleSQL(sql)` runs trusted framework scripts such as files under `App_Data/sql/updates`. For SQL Server, line-only `GO` is the explicit batch separator. SQL Server batches containing variables or control-flow statements are kept intact so `DECLARE`, `IF`, `BEGIN`/`END`, and related statements keep their scope. Simple SQL Server batches without scoped T-SQL can still use semicolon-newline separators for compatibility. For other database types, scripts are split on simple semicolon or `GO` boundaries, so keep those scripts straightforward.

Use `GO` on its own line when SQL Server requires a separate batch, such as for `CREATE VIEW`, stored procedures, triggers, or other statements that must be first in a batch. `GO`-delimited view, procedure, function, and trigger bodies are preserved as one command. Keep update scripts straightforward; this helper is a practical script splitter, not a full SQL parser.

### Transactions
```csharp
db.begin();
try
{
    db.exec("UPDATE accounts SET balance=balance-10 WHERE id=@id", DB.h("@id", 1));
    db.exec("UPDATE accounts SET balance=balance+10 WHERE id=@id", DB.h("@id", 2));
    db.commit();
}
catch
{
    db.rollback();
    throw;
}
```

### Helper functions
```csharp
// quoting
string table = db.qid("users");          // [users]
string safe = db.q("O'Reilly");           // 'O''Reilly'
string noWrap = db.qq("O'Reilly");       // O''Reilly
int intVal = db.qi("123");               // 123
```

```csharp
// IN helpers
string inClause = db.insql(new[] { "a", "b" });     // IN ('a', 'b')
string inIds = db.insqli(new[] { 1, 2, 3 });        // IN (1, 2, 3)

// limit helpers
string firstTen = db.limit("SELECT * FROM users ORDER BY id", 10);
string nextTen = db.limit("SELECT * FROM users ORDER BY id", 10, 10);

// current DB time
DateTime now = db.Now();
db.insert("log", DB.h("add_time", DB.NOW));         // uses NOW() or GETDATE()

// portable SQL expressions for raw/list SQL
string labelSql = db.sqlConcat("fname", db.q(" "), "lname");
string daySql = db.sqlDateExpr("add_time");
```

When passing an offset to `limit()`, include an `ORDER BY` in the SQL. SQL Server requires it syntactically, and all providers need it for stable paging. Direct `limit()` offset paging is not supported for TOP-only providers such as Access/OLE; use `array()` or `selectRaw()` there so the framework can over-fetch and trim the requested page.

### Date, UTC, and datetimeoffset values
`DB` applies the framework datetime contract automatically for helper-built reads and writes:

- SQL `date` stays date-only.
- SQL `datetime`/`datetime2` is converted between the configured DB timezone and internal UTC.
- Fields ending in `_utc` skip DB timezone conversion and are treated as UTC instants.
- SQL Server `datetimeoffset` and SQLite fields declared as `DATETIMEOFFSET` are treated as instants. Dictionary rows/scalars normalize output to UTC-compatible values; typed DTO properties can be `DateTimeOffset`.
- `DB.NOW` is field-aware in helper-built SQL: normal datetime fields use DB-local current time, `_utc` fields use current UTC time, and SQL Server `datetimeoffset` fields use an offset-aware current time.

```csharp
db.insert("events", DB.h(
    "starts_at", DateTime.UtcNow,        // UTC -> DB timezone for datetime/datetime2
    "sent_at_utc", DateTime.UtcNow,      // stored as UTC
    "source_at_utc", DateTimeOffset.UtcNow)); // stored as datetimeoffset UTC
```

For raw SQL, `DB` cannot reliably infer the target column name. Use `_utc` in the parameter name, or pass `DateTimeOffset`, when the value should not be converted through the DB timezone:

```csharp
db.exec("UPDATE events SET sent_at_utc=@sent_at_utc WHERE id=@id",
    DB.h("@sent_at_utc", DateTime.UtcNow, "@id", id));
```

### Date, UTC, and datetimeoffset values
`DB` applies the framework datetime contract automatically for helper-built reads and writes:

- SQL `date` stays date-only.
- SQL `datetime`/`datetime2` is converted between the configured DB timezone and internal UTC.
- Fields ending in `_utc` skip DB timezone conversion and are treated as UTC instants.
- SQL Server `datetimeoffset` is treated as an instant. Dictionary rows/scalars normalize output to UTC-compatible values; typed DTO properties can be `DateTimeOffset`.
- `DB.NOW` is field-aware in helper-built SQL: normal datetime fields use DB-local current time, `_utc` fields use current UTC time, and SQL Server `datetimeoffset` fields use an offset-aware current time.

```csharp
db.insert("events", DB.h(
    "starts_at", DateTime.UtcNow,        // UTC -> DB timezone for datetime/datetime2
    "sent_at_utc", DateTime.UtcNow,      // stored as UTC
    "source_at_utc", DateTimeOffset.UtcNow)); // stored as datetimeoffset UTC
```

For raw SQL, `DB` cannot reliably infer the target column name. Use `_utc` in the parameter name, or pass `DateTimeOffset`, when the value should not be converted through the DB timezone:

```csharp
db.exec("UPDATE events SET sent_at_utc=@sent_at_utc WHERE id=@id",
    DB.h("@sent_at_utc", DateTime.UtcNow, "@id", id));
```

### Where helper operations - `db.opXXX()`
```csharp
// id IN (1,2)
DBList res1 = db.array("users", DB.h("id", db.opIN(1, 2)));

// status <> 0
DBList res2 = db.array("users", DB.h("status", db.opNOT(0)));

// age BETWEEN 18 AND 30
DBList adults = db.array("users", DB.h("age", db.opBETWEEN(18, 30)));

// text search
DBList like = db.array("users", DB.h("address", db.opLIKE("%Street%")));

// explicit NULL check
DBList nulls = db.array("users", DB.h("deleted", db.opISNULL()));
```

### Inspecting schema
```csharp
StrList tables = db.tables();
StrList views = db.views();
FwDict schema = db.tableSchemaFull("users");
DBList fkeys = db.listForeignKeys("orders");
```

Refer to the `DB.cs` source for detailed behaviour of each method. For full CRUD examples using both FwDict-based and typed models see [`docs/crud.md`](./crud.md).

### Attachment lookup helpers

`Att.listByCategory(categoryCode, item_id: null, is_image: -1)` reads active attachments in an existing category. An omitted/null item filter means all item IDs; explicitly passing zero selects item zero. Unknown categories return an empty list. `Att.listAllByEntity(entityCode, is_image: -1)` explicitly reads across all items of an existing entity. Neither helper creates entity metadata. Both authorize every attachment through `checkAccess` before returning; if one parent is denied, the whole lookup fails. These helpers are for bounded result sets, not paginated attachment browsing.

`AttLinks.listByAtt(attachmentId)` first authorizes the attachment, then reads active links and authorizes every linked parent record. It returns no partial result on denied/missing parent access. Existing `listByEntity` and `listByEntityCategory` keep their exact zero/default filters; use `listByEntity` to read all categories for one item. URL creation and file delivery retain their existing access rules.

### Nullable conversion and shared Row fields

The string overloads of `toDate`, `toDateOrNull`, `toDecimal`, `toDouble`, `toFloat`, `toInt` and `toLong` accept `string?`. Null and whitespace retain their existing fallback result; parsing rules have not changed.

In the attachment, attachment-category, demo-dictionary and role-related Row classes, optional `idesc` fields use `string?` and nullable audit user IDs use `int?` to match the schema. New rows retain their previous empty-description and zero-user defaults. Database NULL is preserved during materialization. Callers that require an integer can use `GetValueOrDefault()`; handle a missing description before dereferencing it. Required names, IDs and timestamps keep their existing declarations, and `Att.Row.fsize` remains `long`.
