[docs index](../README.md) > Guide to Implementing Server-side RetroDRY

Data model
==========

-   This section contains the tables that you will need to add to your database to support RetroDRY. The same database can also contain all your business data.

Table: RetroLock
----------------

One row for each persiston lock that has been obtained in the last 5 days, including those that have been released. The version identifier is used to ensure that edits to a persiston are based on the latest version.

To update this table, the process has to always use a where clause and check the number of rows affected, to ensure that collisions only are successful by one caller. For example: update RetroLock set DatonVersion=xxx where DatonKey=xxx and DatonVersion=xxx

Updates to this table should NOT be in any transaction. Any failed write of a persison should not affect this table.

If Touched is more than 120s old or LockedBy is null, then the persison is not locked for edit.

Every persiston write must be preceded by a lock, but the lock might be held only for the duration of the write operation. So in fast mode, the server process obtains the lock, writes the persiston, then releass the lock all within milliseconds. In slower mode, initiating user edits causes a lock, then it remains locked the whole time during edit.

Servers should clean out rows older than 5 days after last change.

Indexed on:

-   DatonKey (PK)

Name | Type, Nulls | Comments
---- | ----------- | --------
DatonKey | varchar(100)  | persiston key, acts as table primary key
DatonVersion | varchar(100) | client-assigned GUID string
Touched | datetime | the UTC datetime when the row was last modified (which includes when requested, locked, or unlocked)
LockedBy | varchar(100), null | null if not locked; a server assigned session ID if locked
UpdatedByServer | int, null | when locked for update and an update to the daton actually occurred, then this is set to a random number that identifies the server lifetime. If two servers happen to use the same number it isn't critical, but it allows servers to check the recent updates to datons made by other servers

SQL create statement for SQL Server:

```sql
create table RetroLock (
DatonKey varchar(100) not null primary key,
DatonVersion varchar(100) not null,
Touched datetime not null,
LockedBy varchar(100),
UpdatedByServer int
)

--for mysql use timestamp

--for postgresql use timestamp
```

Sample Table: Customer
----------------------

This sample table shows you how you might define a business table, with notes.

One row in this table per customer.

Note that all table and column names are in "PascalCase". Also see notes about casing in wire formats.

Indexing:

-   You should index any column that you have defined a critieria for. You can avoid overindexing by adding validation to your viewon critieria to prevent users from searching solely on non-indexed columns. The crossover between table indexing and defining your data model in code is important!
-   You might index any column that the user can sort by, as well.
-   In postgresql, you can use a GIN index on the CustomValues column to allow efficient use of custom columns as criteria or sorting.

Name | Type, Nulls | Comments
---- | ----------- | --------
CustomerId | serial primary key | primary key; every table must have one single-column primary key
Company | varchar(200) not null | customer company name
SalesRepId | int not null | references Employee; the internal sales rep for this customer.
CustomValues | jsonb null | optional custom values. Any table may have this column, and the name CustomValues is one of the few decisions that the RetroDRY framework forces on your data model. The type jsonb is part of postgreSQL, but you can use any json column type of string if your database does not support json. If you use postgreSQL then you can index CustomValues and use custom columns in viewon criteria, which generates SQL syntax in the where-clause for selecting by custom values. For other databases you can load/view/edit custom values but you cannot use them as criteria.
ImageName | varchar(100) | Sample of how to store images or other external blob data or attachments. If the image is required to exist and there is always exactly one image per row, then you can use the row primary key as the image name and omit an actual database column. If it may have an arbitrary name and/or might not exist, then use an ImageName column as in this example. The storage itself is not defined by RetroDRY, but you can use files in a filesystem or Amazon S3 for example.

Server application plumbing 
============================

Server side code organization
-----------------------------

-   RetroDRY is a single .net standard assembly. (Other languages could also be supported later.) Add it to your project using Nuget: \[name TBD\]
-   Because it is not an application template and does not have an entry point itself, the developer needs to wire up the behavior as noted in the "Server application plumbing" section.
-   If you are using your models in two tiers, the models should go into their own separate assembly to be used in multiple places.

Defining the data model using annotated classes
-----------------------------------------------

-   Declare your daton class inheriting from Persiston of Viewon.
-   There are two arrangements supported for classes:

    -   For single-main-row datons, the top level class itself corresponds to the main table, so include the columns of the main table in your top level class as field members.
    -   For other types of datons, declare the main table as a List of a nested type, just like how you declare child tables explained below.
-   If the daton has child tables, declare them as nested classes named after the table name plus "Row". (Example: ContactRow). Then declare a member in the main table as List&lt;T&gt; using the child table name.
-   Naming is important:

    -   For single-main-row persistons, the persiston class name should usually match the SQL table name.
    -   For other persistons, name the persison class something else that is not the same as the SQL table name. Examples:

        -   For a whole-table persiston on a table named CustomerType, you ca use CustomerTypeLookup as the class name.
        -   For a multi-row persiston on a table named ItemInventory, you can use ItemInventoryInfo as the class name.
    -   For the child table member (such as List&lt;CustomerRow&gt; Customer) the member name should usually match your SQL table name, and this is used throughout the system as the identifier. The class (CustomerRow) is not used for anything so you could use a different naming scheme, but the standard is to use the table name + Row.

-   Column typing

    -   Generally, use c\# non-nullable types for columns that don't allow nulls (such as: int, short, DateTime), and use nullable types for columns that allow nulls (such as int?, short?, DateTime?).
    -   An important exception is if the database assigns primary keys, then retroDRY needs to treat the column as nullable, since it is legal to insert rows that do not specify the key. So use nullable types in this case.
    -   Also ses section below covering WireType annotation.
    -   Also see data types section in the specification document.
-   Use these annotations on the Daton class itself (not on any child tables):

    -   DatabaseNumber - used only in multi-database situations, to indicate the table is found on database number 1, 2, etc.
    -   SingleMainRow - if present on the daton, it is built as a single-main-row daton; otherwise the daton must define exactly one List member which will be taken as the main table
-   Use these annotations on the Daton class or any nested child row classes (anything that derives from Row)

    -   SqlTableName(TableName) - only necessary if the SQL table name is different than the class name
    -   Prompt - defines the default natural language prompt for the daton. For whole-table persistons, it should be plural at the top level (such as "Contact types"); for row declarations within datons, it should be singular, not plural.
    -   ParentKey - used to name the colun in this table that matches the primary key of the parent table in the same daton. Required on all tables except the main table. Note that the column should not be declared in the class at all since that would be redundant.
-   Use annotations on columns, from the System.ComponentModel.DataAnnotations namespace

    -   Annotations supported:

        -   Key - used to indicate the primary key
        -   StringLength(MaxLength, MinLength, ErrorMessage)
        -   RegularExpression(Pattern, ErrorMessage)
        -   Range(Minimum, Maximum, ErrorMessage)
    -   RetroDRY will use the error message with substitutions are noted in the Microsoft documentation.
    -   Only the Microsoft annotations listed above are supported. Note that RetroDRY only uses the annotations to build its internal data dictionary, and then you can change the dictionary later; the annotations are never re-examined.

-   A note about required columns: There is no overt way to specify that a column is required. Instead, for value types, use non-nullable declarations to indicate that it is required. For strings, use the minimum length parameter in the StringLength attribute.
-   Use these annotations on columns, defined by RetroDRY

    -   WireType - Normally you can omit this, and the framework will use the declared field type. In two cases you need to specify the type:

        -   (1) If it is a DateTime, the framework will assume it is date only; if you want time to be included, specify WireType(Constants.TYPE\_DATETIME) or WireType(Constants.TYPE\_NDATETIME)
        -   (2) If it is a string or byte\[\] declaration, the framework will assume that the database column is non-nullable; to specify if it is nullable, use WireType(Constants.TYPE\_NSTRING) or WireType(Constants.TYPE\_NBLOB)
    -   ForeignKey - used to indicate that the column is a foreign key to some persiston known in the data dictionary

        -   If the FK points to a single-main-row persiston, or to a row in the main table of a whole-table persiston, the argument includes the type name and the framework can figure out how to manage the key value
        -   If the FK potints to any other database table/row, don't use an annotation. RetroDry will just treat it like any user-enterable value, but you can override the input control if needed.
    -   Prompt - used to indicate the prompt in the default natural language; if omitted, uses the field name (also see manual techniques for setting up multiple languages)
    -   MainColumn - used to indicate the main readable column which is used for lookup scenarios: when a foreign key references this table, the display value associated with that key comes from the column having the MainColumn annotation. So, use it on the principal "Name" or "Description" columns in most cases, not on the primary key.

        -   When this data dictionary item is inherited by a viewon column, it tells the system which viewon value to use when selecting a persiston using the viewon.
    -   VisibleInDropdown - used in conjuction with MainColumn, and only relevant if this is a whole-table persiston used for lookups. In that case any column adorned with VisibleInDropdown will be visible in a dropdown list from this table.
    -   ImageColumn - used to indicate the computed image URL column name
    -   ComputedColumn - used to indicate the column is not user editble and is not in the database, so it is omitted from SQL.

-   Use the annotation RetroHide on any class or member to make it invisible to RetroDRY.
-   Example

```c#
[SqlTableName("Cust"), MainTable]
public class Customer : Persiston {

    [Key]
    public int? CompanyId

    [StringLength(200), Required, Prompt("Co. name")]
    public string Company;

    [ForeignKey("SalesRep")]
    public int SalesRepId;

    public class ContactRow : Row{
        [Key]
        public int? ContactId;

        [ParentKey]
        public int CompanyId;

        [StringLength(100), Required]
        public string Name;

        public bool Primary;
    }

    public List<ContactRow> Contact;
}
```

Viewon declarations
-------------------

-   As a starting point, viewons can be declared exactly as persistons, except deriving the class from Viewon. You don't need any of the validation annotations since it is not editable.
-   Naming convention is to use the persison name plus List - as in "CustomerList".
-   Since a majority of viewon columns draw from persiston columns that you already declared, the data dictionary can be set to inherit the persiston's definitions, elimintating duplicate definitions and language strings.

    -   To do this, use InheritFrom annotation on the column, giving it the persiston class name, table names and column name. For columns in the main table, omit the table name.
    -   Examples:

        -   \[InheritFrom("Customer.Company")\] //inherit from Company column in main table of Customer daton
        -   \[InheritFrom("Customer.Contact.Name")\] //inherit from Name column in Contact table of Customer daton
        -   \[InheritFrom("Customer.Contact.Phone.PhoneNo")\] //inherit from PhoneNo column in Phone table (which is a child of Contact table) of Customer daton
    -   As a shortcut, use InheritFrom on the class that declares the row type, using just the name of the persiston as the argument; then if the member names match the persiston's main table's member names, they automatically inherit. Or for child tables, specify the persiston name and table name.
    -   Inheritance is live in the sense that if you modify any part of the persiston's data dictionary (such as natural language prompts), the viewon column will inherit the change when calling DataDictionary.FinalizeInheritance. The inheritance is not live after that point in time.
    -   By default viewons do not inherit the custom columns of the persiston when using InheritFrom on the row class. To make it inherit custom columns, use an optional argument in the annotation like this: InheritFrom("Customer", includeCustom: true)
    -   Since inheritance is ultimately a copy operation, if you define multi step or circular inheritances (C inherits B which inherts A, or D and E inherit fom each other), the result will not be well defined because the order of copying metadata is not defined.
    -   Inheritance copies prompts, all validation related properties, all data entry related properties. It does NOT copy the type (including WireType annotation), sorting and left joins or other things affecting database load and save.

-   Viewons also define criteria, a concept not present in persistons.

    -   To define criteria using classes and annotations, define a nested class using any name inside the Viewon class, and annotate that class with Criteria. Then declare the criteria that may be used as if they were data columns. Criteria members must be public fields.
    -   The critieria nested class can be abstract since it will never be instantiated; it's only purpose is for developer documentation and for building the data dictionary. If you declare a member as int for example, the user will still be able to enter a range; you don't have to declare the min and max of the range. The int datatype just tells the framework that the criterion is for an integer database field.
    -   By default, if you declare a criterion with the same name as a column in the viewon's main table, it will include where clauses based on that column.
    -   You can also use InheritFrom in critiera if you want the same prompt and data entry behaviors. In the example below, SalesRepId is inherited from the persiston's SalesRepId field, and this one line anotation causes the behavior in the UI of allowing the user to select any sales rep when they are in the critiera entry field. As with regular columns, you can use InheritFrom on the class to match all columns by name match, and/or on the field to inherit just the one column.
    -   Generally viewon criteria should be nullable types unless you want to force users to always search by a certain criterion.
-   Sorting and paging

    -   There is a global page size which is used for all viewons. Default is 500. All viewon loading is limited to this page size.
    -   Only the main table is subject to paging. If it has child rows, the viewon must be designed so that the child row count is designed to be limited and reasonable; otherwise a separate viewon should be designed to droll down to child rows.
    -   Since viewons can load in pages, the sort order for loading is important. To declare the default order, use the annotation SortColumn on the column to order by. To allow the user to sort by other columns, use SortColumn(true) on the default column, and SortColumn(false) on the other allowed columns.
    -   Pages need not be loaded in order
    -   There are two ways the sort order can be changed:

        -   By reloading using a different order by clause (base case)
        -   By sorting in memory. This can be done on the client, but only if the viewon was completely loaded (which it will be if it has less than 500 rows).

-   Example showing criteria definions and column inheritance:

```c#
public class CustomerList : Viewon {

    [InheritFrom("Customer"), MainTable]
    public class CustomerRow {

        public int CompanyId;

        public string Company;

        [InheritFrom("Customer.Contact.Name")] //we will use some tricky SQL to load only the first primary contact name in the list of customers
        public string MainContactName;
    }

    public List<CustomerRow> Customer; //field name matches database table name

    [Criteria]
    public abstract class Cri {

        [InheritFrom("Customer.Company")] //this ensures the prompt for searching by customer name matches the prompt defined in the persiston
        public string Company;

        [InheritFrom("Customer.SalesRepId")]
        public int? SalesRepId;
    }
}
```

-   Index performance notes

    -   Indexes are the developer responsibility, so when you define critiera for viewons, you must also consider the database performance should someone search by that criterion.
    -   Sort order can also affect performance, so any column that can be in an order-by clause might also need an index.

Foreign key details
-------------------

### Discussion

-   In the following, the "key value" refers to the foreign key value, usually an int, GUID, or string code column that the database enforces referential integrity on. The "display value" refers to some other column in the target table, usually a name or description column. Users need to see the display value on load and after any edits to the column.
-   For foreign keys that reference a row in the main table of a whole-table persiston:

    -   This scenario is called "foreign key to row within persiston". An example is a column CustomerTypeId which references a small CustomerType lookup table. The entire CustomerTable table is part of the CustomerType persiston.
    -   Only the key value is loaded, stored, and communicated, not the associated display value if any.
    -   In order to format the display client side, the key is hidden and replaced with a dynamic lookup of the display value from the whole-table persiston.
    -   The client fetches, caches, and subscribes to the target persiston, so that this lookup can occur repeatedly for display (and for dropdown selection) with good performance.
-   For foreign keys that reference a single-main-row persiston:

    -   This scenario is called "foreign key as persiston key". An example is a column CustomerId, which references a large Customer table. Each customer in the Customer table is a separate persiston.
    -   Both the key value and display value are loaded and communicated.
    -   In persistons, the display value is read only (not saved) except that it is updated when the key value changes. This can affect the diff format; the changed display value will be included in the diff.
    -   The data dictionary info on the display column is used to enable automatic SQL to left-join the display value.
    -   The display column may also include InheritFrom annotation - this works in viewons and persistons
    -   The client can allow the user to enter the key value directly or use some other viewon to search for it. To automate the lookup process, use the annotation LookupBehavior with arguments for the viewon type to use, and the column name in the main table of the viewon that is the key value (or omit to use primary key). When a row is selected in the viewon, the key is copied back to the row being edited, and any other columns in that row with LeftJoin defined cause description values to also be copied back.

### Implementation

-   Use the InheritFrom annotation within any daton to copy the data dictionary from some other persiston. This doesn't affect loading behavior.
-   Use the ForeignKey annotation with a single argument for the persiston type whose primary key the column points to.
-   Use the LeftJoin annotation with 2 arguments: the name of the foreign key value column in the table being defined, and the name of the column in the table that the foreign key refers to.
-   You can override SQL loading; if you do, then the LeftJoin annotation won't have any effect.
-   Example in a single-main-row persiston:

```c#
public class Customer : Persiston {

    [Key]
    public int CompanyId;

    [ForeignKey(typeof(Employee))]
    [LookupBehavior(typeof(EmployeeList), KeyColumnName: "EmployeeId")] //normally KeyColumnName can be omitted
    public int SalesRepId;

    [LeftJoin("SalesRepId", "Name"), InheritFrom("'Employee.Name")] //"SalesRepId" must be the name of some other field in this class
    public string SalesRepName;
}
```

-   Example in a viewon:

```c#
public class CustomerList : Viewon {

    [InheritFrom("Customer")]
    public class CustomerRow {

        [Key] //viewon main table keys should be defined as Key and ForeignKey
        [ForeignKey(typeof(Customer))] //this links the viewon with its associated persiston
        public int CompanyId;

        public string Company;

        [ForeignKey(typeof(SalesRep))] //this can be omitted because CustomerRow inherits metadata from Customer persison
        public int SalesRepId;

        [LeftJoin("SalesRepId", "Name"), InheritFrom("'Employee.Name")]
        public string SalesRepName;
    }

    public List<CustomerRow> Customer;
}
```

Computed column details
-----------------------

-   As noted above use the ComputedColumn attribute to note that a column is computed.
-   Then in the Row class, override Recompute to set values on that row.
-   Recompute is called by the framework after rows are loaded from the database or deserialized from a client; you can also call the method from other places.

REST-style endpoints
--------------------

### Login

-   At a minimum you will need to build the endpoint for authentication, if the app requires logins at all. The mechanics of this is up to you. One typical implementation is to set up a POST endpoint at /api/login, accepting userId and password strings, and returning an error message or accepted flag with a session key.
-   Steps

    -   Authentication is confirmed by your server side code.
    -   You register the session in Retroverse.CreateSession, which assigns the session key. If the app does not require authentication, then you can just use a hardcoded user for the registration.
    -   You return the session key to the client.
    -   Your client code registers the session key with retrodry object in javascript.
-   The login response might also send back a timezone offset to the client, if the app deals with editable date-times.

### Connect endpoints to RetroDRY implementation

-   RetroDRY does not have an http listener of its own so you need to wire up two required API endpoints.
-   Code steps

    -   Add these packages

        -   Newtonsoft.Json
        -   Microsoft.AspNetCode.Mvc.NewtonsoftJson
    -   In Startup class, change the line services.AddControllers() to:

        -   services.AddControllers().AddNewtonsoftJson();
    -   Create initialization method:

        -   In Startup class, add static method InitializeRetroDRY (It doesn't have to be here and you can name it something different if needed)
        -   Call the intitialization method from Program.Main (or wherever your startup code is) - which can be done before the CreateHostBuilder line.
        -   Refer to the initialization section below for advice on what to code inside that function.
    -   Create a controller with two endpoints.

        -   The typical class and file name is Controllers/RetroController.cs
        -   The typical endpoints are

            -   POST /api/retro/main
            -   POST /api/retro/long
        -   Refer to sample code for the implementation,

### Get and modify persistons/viewons (optional)

-   You may also set up endpoints that work with RetroDRY to provide a compatible interface that can be used with non-RetroDRY clients.
-   It's not strictly REST-compliant since it uses only the POST verb for all types of modifications, and it does not include the data path semantically inside the URL. Nevertheless, it is easy to program a client for this API.
-   Sample code for the compatible API is found in WeaselController in the sample server project.
-   To use the POST endpoint, the client would post to /api/retro/Customer-123 and include the daton diff as explained in the section on the Diff wire format.
-   If the client wants to delete the persiston completely, it must construct a diff that deletes the single row of the main table within the persiston.

Initializations
---------------

-   The following sections describe what must be in your initialization code.

### Prepare data dictionary

-   You are responsible for creating and initializing the data dictionary, then storing it in some global variable or dependency-injection container, as you like. You can use all your Persiston/Viewon classes with annotations to make this automatic, as in this example:

```c#
var dataDictionary = new DataDictionary();
dataDictionary.AddDatonsUsingClassAnnotation(typof(Startup).Assembly); //build data dictionary from all classes declared in this assembly
MyGlobals.DataDictionary = dataDictionary;
```

-   If the annotations are not sufficient, you can add procedural code to set or override any data dictionary item.

    -   Example to set prompts in a secondary language:

        -   `dataDictionary.Daton\["Customer"\].Table\["Customer"\].Column\["SalesRepId"\].SetPrompt("de", "Vertriebsmitarbeiter");`
        -   (In practice you would design storage for all your language strings and loop through them to push them into the data dictionary, instead of line by line as in this example.)
    -   Example to set up a custom colum:

```c#
var ticklerDate = dataDictionary.Daton\["Customer"\].Table\["Customer"\].AddCustomColumn("TicklerDate", typeof(DateTime));
tickerDate.SetPrompt("Tickler Date"); //set up other data dictionary info here
```

        -   (In practice you would design storage for all your custom columns and loop through them to push them into the data dictionary, instead of line by line as in this example. Also see the data model section for how to make room in your database to store custom values.)
    -   Example to override load and save:

        -   `dataDictionary.Daton\["Customer"\].SqlOverride = new CustomerSql(); //CustomerSql is a class that overrides some or all SQL behavior`

-   When you are done setting up all the data dictionary details, call DataDictionary.FinalizeInheritance; this copies inherited metadata based on the InheritFrom annotation.

### Validations

-   The data dictionary also stores function references for validation.

    -   Validation functions can return a list of error messages, or null if the item is ok.
    -   Validators are async so you could potentially check with an outside system during validation.
    -   Example to set a persiston validator:

        -   `dataDictionary.DatonDefs\["Customer"\].Validator = (cust) => { /\* customer validation here \*/ };`

    -   Example to set up a viewon criteria validator (which is run before viewon loads):

        -   `dataDictionary.DatonDefs\["CustomerList"\].Validator = (cri) => { /\* customer list criteria validation here \*/ };`

### Default values

-   To set up default values for new unpersisted persistons, inject an async function using DatonDef.Initializer.

### Define roles

-   Define permission sets using the classes RetroRole, DatonPermission, TablePermission, ColumnPermission, RowPermissionLevel, ColumnPermissionLevel
-   Some simple systems have only 2 or 3 hardcoded roles (public user, manager, and admin, for example), while others have a completely customizable role system with potentially hundreds of roles.
-   For a simple role system, see UserCache class in the sample app.
-   For a complex role system, you would read permission data from the database and create the roles, storing them in global variables.

### Override save error messages

-   To change cryptic database exceptions into user-readable strings, set Retroverse.CleanUpSaveException to a function that returns the improved error message.

### Initialize retroverse and API

-   By defining your authentication injected function, you are letting the framework know who can connect and what permissions they have. Then you don't need to really do anything after that, since the clients will request data, save data, and receive updates automatically through this connection.
-   The client will need to call your authentication endpoint to obtain a user token that your app defines. That token is then used to initialize the connection from the client side. This plumbing can't be completely automated by RetroDRY because there are many ways to set up security and we don't want to tell you how to do that, but would rather work with whichever way you use.
-   Code example is found in the sample app, Startup class, InitializeRetroDRY method.

### Other potential init steps

-   CORS might be required depending on your deployment model.

Load and save overrides
-----------------------

-   By default RetroDRY uses the data dictionary to construct SQL selects, inserts, updates, and deletes. However there are cases where your database schema doesn't match the in-memory daton structure exactly, or you use stored procedures, or you need some kind of optimization, such as a full text search. For these cases you can override the load, save, and/or some of the detail behaviors.
-   To override this behavior, first create a class that derives from RetroSql with your Daton subclass as a generic type argument, like this:

    -   public class CustomerSql : RetroSql { ... }
-   In that class, you can override these methods to either write all new behavior or override certain details. You should look at the RetroDRY source code to fully understand how to override behavior.
-   Overridable methods for loading:

    -   Load() - Override this to load the daton by key value, and bypass all default behavior. Or you can call the base behavior and add some post-loading behavior after that.
    -   LoadTable() - Override this to change the loading behavior for one table. The default behavior loads the rows related to the daton at once even if it is a child table that cannot select by a single parent key. The return value from this method is a structure of lists of rows indexed by the parent key value, so that the framework can distribute those rows to their parents.
    -   MainTableWhereClause() - Override this to change the where-clause on the main table's load. There are two overloads of the method, one for persistons and one for viewons. A typical use for this is to define your own special criteria that don't map to column names. In this case, you can all the base implementation then modify the where clause to include your implementation of the special criteria, if present.
    -   SqlColumnExpression() - Override this to change the column name or expression to use to load a column.
-   Overridable methods for saving:

    -   Save() - Override this to change the save behavior as a whole. You receive the pristine and modifed daton versions as well as the diff, so you can scan for values and call stored procedures from here. Be sure that your implementation reads database-assigned primary keys assigns them to the Modified persison's rows; this allows the framework to detect the new daton key for newly inserted persistons.
    -   DatabaseAssignsKeysForTable() - Override this to specify that the database will not assign primary key values; the default is true for all tables.
    -   PopulateWriterColumns() - Override this to change the way column values are accumulated for a particular row. This gets called when the diff row is being examined, for both insert and updates. The default behavior appends all columns from a row (except primary key and computed columns) to a SqlWriteBuilder instance, including custom values. You might override this to append more values to the SqlWriteBuilder or change the values.
    -   DeleteRowWithCascade() - Override this to change the delete behavior for a row with cascading to child rows. The default implementation recursively calls DeleteSingleRow, but if you have a stored procedure or some other way to handle cascading, override this method.
    -   DeleteSingleRow() - Override this to change delete behavior for a single row. The framework will call this for the most nested child rows first, working up to the top level deleted row, so the implementation of this override does not need to cascade.
-   Overridable methods that affect both save and load:

    -   CustomizeSqlStatement() - Override this to rewrite the actual SQL statement as a string; this may be any kind of statement. This is a last resort if you can't override the behavior using the other virtual methods.
-   You can inject these overrides in a way that is particular to the daton type, or globally, or write one override class that handles a subset of types.

    -   An example usage of overriding it globally is to set values for date created and modified, and user created and modified.
    -   An example usage of overriding either globally or per-type is to use stored procedures instead of direct SQL.
    -   To inject a global override, call Retroverse.OverrideAllSql, passing an instance of your RetroSql derived class.
    -   To inject an override for only one type, call Retroverse.OverrideSql, passing the daton type name and an instance of your RetroSql derived class.
    -   To inject an override for a subset of types, call Retroverse.OverrideSql repeatedly for each type, but you can pass in the same instance of the override.
    -   If you need something more complex, you will need to write common behavior somewhere and call it from each type-specific override.
