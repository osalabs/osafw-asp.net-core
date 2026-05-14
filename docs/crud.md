# CRUD workflows with `FwModel`

The framework offers two complementary ways to work with database rows.
You can keep using lightweight `FwDict`/`FwList` collections for maximum flexibility, or describe your rows as strongly typed DTOs by inheriting from `FwModel<TRow>`.
Both flows share the same routing, permissions, and caching behaviour, so you can pick whichever fits each controller.

For model helper names, follow the framework naming guide in [naming.md](naming.md).

## FwDict workflow

### Why use FwDict?
FwDict-based models are quick to scaffold and easy to extend.
A row is simply a set of key/value pairs, making it trivial to add optional fields, merge dynamic metadata, or echo submitted data back to templates without defining dedicated classes.

### Model setup
```csharp
public class Users : FwModel
{
    public Users()
    {
        table_name = "users";
        field_id = "id";
        field_iname = "iname";
        field_status = "status";
    }
}
```

### Reading data
```csharp
var users = fw.model<Users>();

DBRow row = users.one(5);                     // load a single record
DBList active = users.list(new FwList      // filter by status list
{
    FwModel.STATUS_ACTIVE,
});
DBList filtered = users.listByWhere(new FwDict // custom where expression
{
    ["status"] = 0,
    ["access_level"] = DB.opGT(0),
});
DBList page = users.listByWhere(new FwDict { ["status"] = 0 }, offset: 20, limit: 10, orderby: "id");
DBRow required = users.oneOrFail(5);          // throws if not found
```

`listByWhere()` accepts `offset, limit` paging arguments and passes them through to `DB.array()`. Use an explicit `orderby` when paging so the returned slice is stable.

### Writing data
```csharp
var users = fw.model<Users>();

var fresh = new FwDict
{
    ["iname"] = "Alice",
    ["status"] = FwModel.STATUS_ACTIVE,
};
int id = users.add(fresh);                    // insert

var changes = new FwDict
{
    ["iname"] = "Alice Johnson",
};
users.update(id, changes);                    // update

users.delete(id);                             // soft delete (uses field_status)
users.delete(id, true);                       // pass true to delete permanently
```

> `DB.h()` remains a handy helper when you only need one or two keys, but `new FwDict { ... }` keeps examples explicit.

### Controllers
```csharp
public override int SaveAction(int id = 0)
{
    var item = reqh(); // get submitted form key/values

    Validate(id, item);              // dictionary validation
    return modelAddOrUpdate(id, item); // insert or update
}
```

## Typed workflow

### When to choose DTOs?
Typed rows shine when you prefer compile-time checks, IDE navigation, and consistent naming across controllers, models, and templates. 
They are ideal once your schema stabilises or when business logic benefits from strongly typed properties.

### Model setup
```csharp
public class Users : FwModel<Users.Row>
{
    public class Row
    {
        public int id { get; set; }

        [DBName("iname")]
        public string title { get; set; }

        public int status { get; set; }
        public DateTime add_time { get; set; }
    }

    public Users()
    {
        table_name = "users";
        field_id = "id";
        field_iname = "iname";
        field_status = "status";
    }
}
```

### Reading data
```csharp
var users = fw.model<Users>();

Users.Row? row = users.oneT(5);                   // null if missing
Users.Row? byCode = users.oneTByIcode("demo");   // null if missing
List<Users.Row> active = users.listT();           // list with typed rows
List<Users.Row> page = users.listTByWhere(offset: 20, limit: 10, orderby: "id");
Users.Row required = users.oneTOrFail(5);         // throws if missing
```

Typed single-row methods use `null` for "not found" because a default DTO can look like a real record with `0` and empty-string values. Use `oneTOrFail` when the route or workflow requires the record to exist.

### Writing data
```csharp
var users = fw.model<Users>();

var dto = new Users.Row
{
    // No need to set id here; add() will populate it on success
    title = "Alice",                              // maps to iname
    status = FwModel.STATUS_ACTIVE,
};
int id = users.add(dto);                          // insert DTO
int generatedId = dto.id;                         // add() populates the identity

var existing = users.oneTOrFail(id);              // load current values
existing.title = "Alice Johnson";
users.update(id, existing);                       // update DTO

users.delete(id);                                 // soft delete (status=127)

// When only a couple of fields need to change, either load the row first as above
// or fall back to the FwDict overload: users.update(id, new FwDict { ["status"] = FwModel.STATUS_INACTIVE });
```

`FwModel.add(TRow dto)` writes any generated identity or audit columns back onto the DTO, so the `id` property stays in sync without extra queries. 
For partial updates stick with the loaded DTO instance (ensuring unchanged fields remain intact) 
or call the FwDict overload to patch a narrow set of columns without constructing temporary classes.

### Controllers
```csharp
public override int SaveAction(int id = 0)
{
    var dto = new Users.Row();
    reqh().applyTo(dto); // copy request values onto the DTO

    Validate(id, dto);                 // typed validation overload
    return modelAddOrUpdate(id, dto);  // persists DTO
}
```

## Working across both flows
The extension helpers in `FwExtensions` let you bridge the styles whenever needed:

```csharp
var users = fw.model<Users>();
FwDict item = reqh();                 // data from request
Users.Row dto = item.to<Users.Row>(); // FwDict -> DTO

FwDict payload = dto.toFwDict();      // DTO -> FwDict
DBList rows = users.list();              // untyped query
List<Users.Row> typed = rows.toList<Users.Row>();
```

Use whichever approach suits each feature. Many teams keep FwDict for quick admin tools while adopting typed rows for core business entities.
