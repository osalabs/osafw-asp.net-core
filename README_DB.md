# db.net
Simplified work with SQL Server, MySQL or MS Access (OLE, ODBC) databases for your website, convenient wrapper

Contains `DB` class. Pure C# .NET Core.

### Why I created this library?
Because of much easier and simplier work with queries and results. Compare:

**DB.vb usage:**
```vb.net

Dim db = New DB()
Dim sql = "SELECT * FROM table ORDER by id"
Dim rows = db.array(sql); 'db opened automatically based on web.config, errors handled automatically
For Each row As Hashtable In rows
    'work with row("Field1"), row("Field2")
Next
db.disconnect() 'not necessary as disconnect happens on db object disposal
```

compare to **"native" SqlConnection/SqlCommand/SqlDataReader usage:**
```vb.net
Dim connectionString as String = "Data Source=(local)\SQLEXPRESS;Initial Catalog=demo;Integrated Security=True" //sample
Dim sql As String = "SELECT * FROM table ORDER by id"
Using connection As New SqlConnection(connectionString)
    Dim command As New SqlCommand(sql, connection)
    connection.Open()
    Dim reader As SqlDataReader = command.ExecuteReader()
    Try
        While reader.Read()
            'work with fields: Field1 now in reader(0), Field2 in reader(1)
        End While
    Finally
        ' Always need to call Close when done reading.
        reader.Close()
    End Try
End Using
```

### API Summary

The following methods available

#### optional
- `connect()` opens connection to database (optional as connection opened on first sql request to database)
- `check_create_mdb(filepath)` create new MS Access database (TBD remove? is it necessary)
- `disconnect()` disconnect from currently connected db (optional as disconnect happens on db object disposal)

#### for parametrized sql queries (best practice to prevent sql-injections)
- `value(table_name, where[, field_name[, order_by]])` get single value from table/where conditions and optional field_name (if not passed - first field value returned)
- `value(table_name, where, 'count(*)')` get count(\*) from table/where
- `value(table_name, where, '1')` get "1" if row exists
```VB.NET
    Dim name = db.value("users", New Hashtable From {{"id", 1}}, "user_name")
    Dim ctr = db.value("users", New Hashtable, "count(*)")
```

- `row(table_name, where[, order_by])` get single row (first row) by table/where and optional order by
```VB.NET
    Dim table_name = "items"
    Dim row = db.row(table_name, New Hashtable From {{"id", 1}})
    'select * from items where id=1

    Dim where = New Hashtable From {{"status", 0}}
    Dim row = db.row(table_name, where, "prio desc")
    'first row from - select * from items where status=0 order by prio desc
```

- `array(table_name, where[, order_by])` get all rows by table/where and optional order by
```VB.NET
    Dim table_name = "items"
    Dim where = New Hashtable From {{"status", 0}}
    Dim rows = db.array(table_name, where, "prio desc")
    'select * from items where status=0 order by prio desc
    For Each row As Hashtable In rows
        'work with row("fieldname") values
    Next
```

- `col(table_name, where[, field_name[, order_by]])` get all value from table/where conditions and optional field_name (if not passed - first field/column values returned)
```VB.NET
    Dim names = db.col("users", New Hashtable From {{"status", 0}}, "user_name", "user_name desc")
    'select user_name from users where status=0 order by user_name desc
    For Each name As String In names
        'work with user name
    Next
```

- `insert(table_name, data)` insert new row into db, return last inserted id
```VB.NET
    Dim user = New Hashtable From {
        {"name","John"},
        {"email","john@email.com"}
    }
    Dim id = db.insert("users", user)
```

- `update(table_name, data, where)` update record by where conditions (AND)
```VB.NET
    Dim user = new Hashtable From {
        {"name","John Smith"}
    }
    Dim id = db.update("users", user, New Hashtable From {{"id", 1}})
```

- `update_or_insert(table_name, data, where)` tries to update, it no records affected - insert new record, retrun number of affected rows
```VB.NET
    'assuming email is unique key, so if no record with such email found - new record will be inserted
    Dim user = new Hashtable From {
        {"name","John Smith"},
        {"email","john@email.com"}
    }
    db.update_or_insert("users", user, user)
    Dim id = value("SELECT @@identity")
```

- `del(table_name, where)` delete record by where conditions (AND)
```VB.NET
   db.del("users", New Hashtable From {{"id", 1}})
```

#### for raw sql queries
- `query(sql)` run arbitrary sql query and return DbDataReader
- `exec(sql)` run arbitrary non-select sql query (for inserts, updates...)
- `update(sql)` alias for `exec(sql)`
- `value(sql)` get single value via arbitrary sql
- `row(sql)` get single row As Hashtable via arbitrary sql
- `array(sql)` get all rows As ArrayList of Hashtables via arbitrary sql
```VB.NET
    Dim rows = db.array("SELECT * FROM users")
    For Each row As Hashtable In rows
        'work with row("fieldname") values
    Next
```
- `col(sql)` get all values As ArrayList from first column

#### helpers
- `q(string[, length=0])` quote string - double single quotes and wrap result into single quotes, optionally trim to left `length` chars
- `q_ident(string)` quote identifier (table or field name)
- `qq(string)` quote string witout wrapping result into single quotes
- `qi(string)` quote string as integer - convert string into Integer
- `qf(string)` quote string as float - convert string into Double
- `qd(string)` quote string as date or NULL (if string cannot be parsed as Date)
- `quote(table_name, data)` quote all field names and values in `data` for a table according to field types
- `qone(table_name, field_name, field_value)` quote one field value according to table/field type
- `left(string, length)` trim string and return only left `length` chars

#### where helpers for parametrized queries
- `insql(params)` - create sql like "IN (1,2,3)" or "IN (NULL)"" if empty params passed
```VB.NET
    where = " field "& db.insql("1,2,3,4")
    where = " field "& db.insql("this,that,another,value")
    where = " field "& db.insql(string())
    where = " field "& db.insql(ArrayList)
```
- `opIN(value1,value2)` or `opIN(array_of_values)` IN operator
```VB.NET
    Dim rows = db.array("users", New Hashtable From {{"id", db.opIN(1, 2)}})
    'select * from users where id IN (1,2)
```
- `opNOTIN(value1,value2)` or `opNOTIN(array_of_values)` NOT IN operator
```VB.NET
    Dim rows = db.array("users", New Hashtable From {{"id", db.opNOTIN(1, 2)}})
    'select * from users where id NOT IN (1,2)
```

- `opNOT(value)` NOT EQUAL condition
```VB.NET
    Dim rows = db.array("users", New Hashtable From {{"status", db.opNOT(127)}})
    'select * from users where status<>127
```
- `opLE(value)` LESS THAN condition
```VB.NET
    Dim rows = db.array("users", New Hashtable From {{"access_level", db.opLT(50)}})
    'select * from users where access_level<50
```
- `opLT(value)` GREATER or EQUAL than operation
```VB.NET
    Dim rows = db.array("users", New Hashtable From {{"access_level", db.opGE(50)}})
    'select * from users where access_level>=50
```
- `opGT(value)` GREATER THAN than operation
```VB.NET
    Dim rows = db.array("users", New Hashtable From {{"access_level", db.opGT(50)}})
    'select * from users where access_level>50
```
- `opISNULL(value)` check if field IS NULL
```VB.NET
    Dim rows = db.array("users", New Hashtable From {{"field", db.opISNULL()}})
    'select * from users where field IS NULL
```
- `opISNOTNULL(value)` check if field IS NOT NULL
```VB.NET
    Dim rows = db.array("users", New Hashtable From {{"field", db.opISNOTNULL()}})
    'select * from users where field IS NOT NULL
```
- `opLIKE(value)` LIKE operator
```VB.NET
    Dim rows = DB.array("users", New Hashtable From {{"address1", db.opLIKE("%Orlean%")}})
    'select * from users where address1 LIKE '%Orlean%'
```
- `opNOTLIKE(value)` LIKE operator
```VB.NET
    Dim rows = DB.array("users", New Hashtable From {{"address1", db.opNOTLIKE("%Orlean%")}})
    'select * from users where address1 NOT LIKE '%Orlean%'
```

#### db structure
- `tables()` return names of all database tables as ArrayList
- `load_table_schema_full(table)` return ArrayList of Hashtables with information about table columns
- `get_foreign_keys(table)` return ArrayList of Hashtables with information about table foreign keys


