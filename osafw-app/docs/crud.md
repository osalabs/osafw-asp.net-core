# CRUD workflows with `FwModel`

The framework offers two complementary ways to work with database rows. You can keep using lightweight `Hashtable`/`ArrayList` collections for maximum flexibility, or describe your rows as strongly typed DTOs by inheriting from `FwModel<TRow>`. Both flows share the same routing, permissions, and caching behaviour, so you can pick whichever fits each controller.

## Hashtable workflow

### Why use Hashtables?
Hashtable-based models were selected for the initial release because they are quick to scaffold and easy to extend. A row is simply a set of key/value pairs, making it trivial to add optional fields, merge dynamic metadata, or echo submitted data back to templates without defining dedicated classes.

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
DBList active = users.list(new ArrayList      // filter by status list
{
    FwModel.STATUS_ACTIVE,
});
DBList filtered = users.listByWhere(new Hashtable // custom where expression
{
    ["status"] = 0,
    ["access_level"] = DB.opGT(0),
});
DBRow required = users.oneOrFail(5);          // throws if not found
```

### Writing data
```csharp
var users = fw.model<Users>();

var fresh = new Hashtable
{
    ["iname"] = "Alice",
    ["status"] = FwModel.STATUS_ACTIVE,
};
int id = users.add(fresh);                    // insert

var changes = new Hashtable
{
    ["iname"] = "Alice Johnson",
};
users.update(id, changes);                    // update

users.delete(id);                             // soft delete (uses field_status)
users.delete(id, true);                       // pass true to delete permanently
```

> `DB.h()` remains a handy helper when you only need one or two keys, but `new Hashtable { ... }` keeps examples explicit.

### Controllers
```csharp
public override int SaveAction(int id = 0)
{
    var item = reqh(); // get submitted form key/values

    Validate(id, item);              // hashtable validation
    return modelAddOrUpdate(id, item); // insert or update
}
```

## Typed workflow

### When to choose DTOs?
Typed rows shine when you prefer compile-time checks, IDE navigation, and consistent naming across controllers, models, and templates. They are ideal once your schema stabilises or when business logic benefits from strongly typed properties.

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

Users.Row row = users.oneT(5);                    // load by id
Users.Row byCode = users.oneTByIcode("demo");    // use icode lookup
List<Users.Row> active = users.listT();           // list with typed rows
Users.Row required = users.oneTOrFail(5);         // throws if missing
```

### Writing data
```csharp
var users = fw.model<Users>();

var dto = new Users.Row
{
    // No need to set id here; add() will populate it on success
    title = "Alice",
    status = FwModel.STATUS_ACTIVE,
};
int id = users.add(dto);                          // insert DTO
int generatedId = dto.id;                         // add() populates the identity

var existing = users.oneT(id);                    // load current values
existing.title = "Alice Johnson";
users.update(id, existing);                       // update DTO

users.delete(id);                                 // soft delete (status=127)

// When only a couple of fields need to change, either load the row first as above
// or fall back to the Hashtable overload: users.update(id, new Hashtable { ["status"] = FwModel.STATUS_INACTIVE });
```

`FwModel.add(TRow dto)` writes any generated identity or audit columns back onto the DTO, so the `id` property stays in sync without extra queries. For partial updates stick with the loaded DTO instance (ensuring unchanged fields remain intact) or call the Hashtable overload to patch a narrow set of columns without constructing temporary classes.

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
Hashtable item = reqh();                 // data from request
Users.Row dto = item.@as<Users.Row>();   // Hashtable -> DTO

Hashtable payload = dto.toHashtable();   // DTO -> Hashtable
DBList rows = users.list();              // untyped query
List<Users.Row> typed = rows.asList<Users.Row>();
```

Use whichever approach suits each feature. Many teams keep Hashtables for quick admin tools while adopting typed rows for core business entities.
