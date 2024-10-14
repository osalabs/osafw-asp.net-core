# Entity Definition Format

This format allows you to define database entities and fields in a concise and intuitive way. It facilitates automatic generation of SQL scripts, JSON definitions, and UI components for CRUD operations.

## General Rules

- **Entity Separation**: Entities are defined in plain text, separated by empty lines.

- **Entity Names**: Use either `snake_case` or `CamelCase`.
  - **Table Names**: Converted to `snake_case`.
  - **Model Class Names**: Converted to `CamelCase`.

- **Standard Fields**: Automatically added to each entity unless excluded:
  - `id`: Primary key.
  - `iname`: Name or title of the entity.
  - `idesc`: Description or notes.
  - `status`: Status flag (standard values: 0-Active, 10-Inactive, 127-Deleted)
  - `add_time`: Record creation timestamp.
  - `add_users_id`: ID of the user who created the record.
  - `upd_time`: Last update timestamp.
  - `upd_users_id`: ID of the user who last updated the record.

- **Comments**: Use `-- some comment` to add comments

## Entity Definition

- **Syntax**:

  ```plaintext
  -- Entity-level comment
  EntityName [parameters]
  [field definition]
  [field definition]
  ...
  [index definition]
  ...
  ```

- **Parameters**:
  - `lookup`: Marks the entity as a lookup table, for Lookup Manager
  - `noui`: No UI will be generated for this entity, just a model
  - `nostd`: Excludes all standard fields from the entity.

## Field Definitions

- **Syntax**:

  ```plaintext
  FieldName [Type(Length)] [NULL|NOT NULL] [DEFAULT(Value)] [UNIQUE] [UI:options]
  ```

- **Defaults**:
  - **Type**: If not specified, defaults to `nvarchar(255) NOT NULL DEFAULT('')`.
  - **UI**: Defaults to an input of type `text`.

- **Field Attributes**:
  - **Type and Length**: Specify data type and length (e.g., `varchar(20)`, `decimal(10,2)`).
    - `text` type converted to `NVARCHAR(MAX)`
    - `currency` type converted to `DECIMAL(18,2)`
  - **NULL/NOT NULL**:
    - Fields are `NOT NULL` by default (except dates)
    - Use `NULL` to allow `NULL` values. In this case DEFAULT not applied (unless explicitly specified)
  - **DEFAULT(Value)**: Sets a default value.
    - Fields by default are DEFAULT('') or DEFAULT(0) or other relevant
    - use `getdate` or `now` without parentheses for current datetime
    - Specify `DEFAULT()` to disable default values
  - **UNIQUE**: Adds a unique constraint.
  - **UI Options**: Controls how the field is rendered in the UI.

- **UI Options**:
  - **Syntax**: `UI:option1,option2,...`
  - **Common Options**:
    - `required`: Field is mandatory; implies `NOT NULL` without a default.
    - `checkbox`: Renders as a checkbox.
    - `number`: Numeric input.
    - `password`: Password input.
    - `label(text)`: Overrides the default label.
    - `placeholder(text)`: Adds placeholder text.
    - `step(value)`: Sets increment step for numeric inputs.
    - `pattern(value)`: input pattern
    - `multiple`: for select tag
    - `rows(x)`: for textarea tag
    - `step(x)`: for number inputs
    - `min(x)`: for number inputs
    - `max(x)`: for number inputs
    - `validate(exists isemail)`: for backend validation
    - `class`, `class_label`, `class_contents`, `class_control`: for different input classes
    - `data-attribute(value)`: Adds custom data attributes.
      - Example: `UI:data-category(special)`
  - **Options with Values**:
    - Use `|` delimiters: `options(value1|Display1 value2|Display2)`
      - Example: `UI:select,options(0|Active 1|Inactive)`
    - Link to a template file: `options(/common/sel/status.sel)` or `options(status.sel)` for relative path to controller base template folder

- **Foreign Keys**:
  - **Simplest Form**: `RelatedEntity.id [NULL|NOT NULL]`
    - Creates a field `relatedentity_id` (snake_case), foreign key to `RelatedEntity(id)`.
  - **Custom Field Name**: `fieldname2 FK(TableName.fieldname) [NULL|NOT NULL]`
    - Creates a field `fieldname2`, foreign key to `TableName(fieldname)`.
  - Indexes are automatically added for foreign key fields.

- **Removing Standard Fields**:
  - Use `fieldname remove` to exclude specific standard fields when `nostd` is not used.

- **Indexes**:
  - **Syntax**:
    - `INDEX (Field1, Field2, ...)`
    - `UNIQUE INDEX (Field1, Field2, ...)`

## Junction Tables

- **Definition**:
  - Use `RelatedEntity junction` within an entity to create a junction table.
  - Automatically creates a table named `currententity_relatedentity`.
  - Contains foreign keys to both entities and standard fields.
- **Note**:
  - If additional fields are needed in the junction table, define it as a separate entity instead.

## Example for simple CRM

---

```plaintext
-- Customer entity with standard fields
Customers
email UNIQUE UI:required -- Email is unique and required
phone varchar(20) -- Defining specific field length
address text -- Makes SQL field "address nvarchar(MAX) NOT NULL DEFAULT ''"
is_active bit DEFAULT(1) UI:checkbox -- Makes "is_active bit DEFAULT(1) NULL"
lead_users_id FK(users.id) NULL

-- Notes for customers, excludes standard fields
customers_notes nostd
customers.id UI:required -- Foreign key to customers, NOT NULL because of required
idesc text -- Using idesc field for the note content
add_time datetime2 DEFAULT(getdate)
add_users_id int DEFAULT 0

-- Vendor entity with standard fields
vendors
iname remove
iname UI:label(Company name) -- Override standard iname
contact_name
phone varchar(20)
email UNIQUE
address text

-- Lookup table for product categories
categories lookup

-- Product entity with vendor link and category junction
products
vendors.id UI:required -- Foreign key to vendors
categories junction -- Creates junction table `products_categories`
price decimal(10,2) DEFAULT(0.0) UI:number,required,step(0.1)
stock_quantity int DEFAULT(0)
UNIQUE INDEX (iname) -- 'iname' (product name) is unique

-- Orders entity with items junction
orders
customers.id UI:required -- Foreign key to customers
order_date date DEFAULT(now)
total_amount decimal(10,2) DEFAULT(0.0)

-- Order items entity with additional fields, no controller
orders_items noui
iname remove -- Remove some standard fields
idesc remove
orders.id UI:required -- Foreign key to orders
products.id UI:required -- Foreign key to products
quantity int DEFAULT(1)
price decimal(10,2) DEFAULT(0.0)
UNIQUE INDEX (orders_id, products_id)
```
