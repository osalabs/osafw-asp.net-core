# DB.cs

Simplified database helper for SQL Server, MySQL or MS Access. Part of the [OSA Framework](https://github.com/osalabs/osafw-asp.net-core).

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
- `listForeignKeys([table])`

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

`array()` and `array<T>()` accept optional `offset, limit` paging arguments after the select-fields argument. `limit = -1` means no limit, and `offset = 0` is the default. When `offset` is greater than zero, pass both a non-negative `limit` and an explicit `order` value so the page is deterministic and portable across SQL Server, MySQL, and OLE providers.

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
```

When passing an offset to `limit()`, include an `ORDER BY` in the SQL. SQL Server requires it syntactically, and all providers need it for stable paging. Direct `limit()` offset paging is not supported for TOP-only providers such as Access/OLE; use `array()` or `selectRaw()` there so the framework can over-fetch and trim the requested page.

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
