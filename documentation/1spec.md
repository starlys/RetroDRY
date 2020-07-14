[docs index](../README.md) > RetroDRY Specifications

Introduction
============

-   You should have already read the Introduction to RetroDRY to understand the high level concepts, before reading this.
-   This document contains all specifications that define the framework and some of them are very detailed and not needed to be able to use it in your application. However every implementor needs to understand the systemwide concerns at the top of this document.

Systemwide concerns
===================

Daton keys
----------

-   Daton keys are strings composed of pipe delimited segments, whose purpose is to uniquely identify the data contained in the daton.
-   If a segment contains the pipe or backslash characters, those must be escaped with backslash. So "a\\\\b|c\\|d" is the format for the two segments "a\\b" and "c|d".
-   For persistons, the key contains the type name and the primary key separated by pipe-equals. For example "Customer|=123"

    -   If the persiston is a whole-table type, then it has no primary key and in that case the format is pipe-plus, as in "CustomerTypes|+
    -   If the persiston is new, it uses -1 as the key, as in "Customer|=-1"
-   For viewons it is more complex

    -   Basically the key contains the type name and a list of criteria and sort order. Or if there are no criteria/sort order, then it is just the type name with no pipe characters.
    -   Each criterion is composed of a coluum name, equals sign and value. If the value contains an equals sign, it doesn't need to be escaped (valid: Formula=a=b)
    -   The sort order looks like a criterion but the column name is \_sort (example: \_sort=Name); if omitted or invalid, the default sort order is used.

        -   Note: In this early version, only a single column ascending sort is supported.
    -   The page number looks like a criterion but the column name is \_page (example: \_page=1). Pages are zero-based. The segment \_page=0 is never included in the full string since it is the default.
    -   Each value is defined by the type.

        -   strings: user entry. Example: "Drink=coffee"
        -   all numbers: low + tilde + high, where either of low or high may be missing, and numbers use american format. Example: "price=~2.5" (price is up to 2.50).
        -   dates: low + tilde + high in YYYYMMDD format, where either of low or high may be missing. Example: TransactionDate=20030416~20030430
        -   datetimes: similar to dates except (1) use YYYYMMDDHHMM format (no seconds), and (2) the value is in the database time zone, so timezone conversion must be on client
        -   If no tilde is included for numbers dates, or datetimes, it treats it as an exact value.
        -   bool: 0 or 1 for false, true respectively. Example: "IsHot=1"
        -   byte array not supported as parameter
    -   The order of segments must be normalized so a string comparison could locate cached data by key. The order is defined by:

        -   Column name
        -   All other segments in alpha order by strict unicode. (note that \_page may be omitted)

-   Note that type names and criterion names are case sensitive and normally in PascalCase to match class names in C\#, unlike most other data transmitted on the wire which uses camelCase - see section on casing.

Database-assigned keys
----------------------

-   RetroDRY works nicely with database-assigned primary keys (aka. "serial" or autonumbering columns), or keys assigned by server logic prior to saving.
-   If a persiston is newly created by a client, it cannot obtain the key in advance, and in this case it always uses the value -1 (as an integer, or string "-1"). It cannot use other negative numbers; only the -1 key value means it is new.
-   If you can, design the database so every table has a single-column primary key that is assigned by the database. This is the default expectation. If you can't do this, then you must override a method in RetroSql (explained in separate section) and manually assign the key in each new row.
-   The default process for saving when there are new rows with new child rows is to obtain the key from the database when inserting the parent row, and carry that through the recursion to use as the parent key in the inserted child rows.
-   There are cases that the framework cannot handle. The main one is when there are cousin references or two-way references instead of a strict parent-child structure. For example if you have a sale persiston with a child table of line items and another child table of shipments, and you need to assign line items to shipping boxes, then there is no key value to use for those cousin references. For these scenarios you would need to design a workaroud and override the saving behavior.

Timing, caching and locks
-------------------------

-   Client to server data link modes

    -   Unlinked - For viewons, and optionally for persistons, the client may request it in unlinked mode. This will cause the server to send and forget.
    -   Subscribed - For persistons that are being displayed and might be edited, and for persistons used as lookups, the client normally gets them with a subscription. This forces them to stay updated whenever they are modified by any user. Subscriptions only last as long as the session, and are not stored in the database. The client should unsubscribe when it dumps any persiston from memory.
    -   Subscribed and Locked - For persistons currently being edited, the lock prevents any other user from making changes.
-   Caching on server

    -   The server caches any persiston for which any of its clients has in subscribed or locked mode. Thus, client behavior completely controls caching on the server.
    -   Because of this behavior, clients should always subscribe to persistons that many cliens will need, to improve server performance.
    -   The server may drop items from the cache to keep memory within a limit, but the behavior looks the same from the client perspective.
-   Caching on client

    -   Clients should cache anything they have a subscription to, generally.
    -   Any other client caching is optional.
-   Subscription updates

    -   The server has to check for updates to all persistons to which any of its clients are subscribed.
    -   If the edit comes through the same server, then the check should happen immediately after making the save.
    -   To check for edits made through other servers, it polls every 60s. When an update is found (that is, the lock table contains a different version than the known version in memory), the persiston is reloaded and sent to all subscribed clients.
    -   Diffs are not used here, because this does not happen that often. Most of the business transactional data (like sales) won't be subscribed to, but lookup tables are the main use case for this, which tend to be edited rarely.
-   Handshakes and timeouts

    -   Clients must call the long polling endpoint repeatedly during the session. If a client fails to do this, the server will, after 120s, forget about the session and cancel any locks.
    -   A client could be devised to keep something locked forever, and that will prevent any other user from editing. That should be avoided. To avoid this situation, clients must ensure that long-duration locks that are waiting on user entry do not lock the persiston for too long. They should track keystrokes and if none occur for some time (suggest 100s), it should threaten to undo the edits; if no user action occurs for another 30s or so, it should end editing and release the lock.
    -   See data model notes for server behavior required to maintain locks.
-   Crash modes

    -   If a client crashes, long polling stops and this causes the server to unlock any locked persistons.
    -   If a server crashes, the client will need to restart with another server, but locks remain active. In this case, there could be a longer delay before another client can get the lock.

Character casing
----------------

-   Everything is case sensitive.
-   The general scheme is as follows:

    -   Database data dictionary items should use PascalCase. Example: CustomerType.
    -   C\# daton classes should use PascalCase as well. If the database uses camelCase or lowercase, this should work anyway since databases are usually case insensitive with respect to names. If, however, the database uses lower\_case\_with\_underscores or has misleading names, then you can declare the databse name in the data dictionary using attributes. This way, all the code can still use PascalCase with clear names.
    -   JSON and all wire data uses camelCase for property names, but uses the same server casing for JSON values. Importantly that means daton keys and daton type names will still be PascalCase within JSON, while the table and column names will be camelCase. The transmission of the data dictionary breaks the rule somewhat for consistency, so for example, the column name "firstName" will be sent in camelCase as a JSON value when it appears in the data dictionary, and also in camelCase when used as a property name.
    -   Criterion names embedded in daton keys are in PascalCase.
    -   Javascript objects will intepret JSON, so they will also be in camelCase.
-   Example of the casing used in each tier:

    -   Database table and column name: Employee.FirstName
    -   C\# code:

        -   class name: Employee
        -   field name within Employee class: FirstName
        -   field value: FirstName = "Thelma";
    -   JSON wire representation:

        -   in data dictionary: employee, firstName
        -   data objects: "employee": \[{ "firstName": "Thelma", ... }\]
    -   Javascript code:

        -   data objects: { employee: \[{ firstName: 'Thelma', ... }\] }
    -   Daton keys - the same in all tiers!

        -   persiston key: Employee|=123
        -   viewon key: EmployeeList|\_sort=FirstName|LastName=Singh

Permissions
-----------

-   The framework requires use of the permissions system. It can be defined to be wide open with a public password, but it still must be there.
-   The database-connected tier is assumed to be a superuser, and the database itself does not enforce any permissions.
-   Servers at a lower tier and browser clients must send credentials with each connection, so that all actions are in a user context.
-   Clients are assumed to not know about permissions and may attempt any action. The system must not require coopration of client code.
-   The system of roles

    -   Each named role contains a permission set. Permission sets are managed by RetroDRY.
    -   Each user is a member of zero or more roles. Users are NOT managed by RetroDRY, so the developer needs to implement a mapping from user IDs to permission sets.
    -   Roles may be defined programmatically (hard coded) by the developer or loaded from database tables.
    -   In cases where roles are editable by superusers, then when roles are changed, the system should dynamically reload on all tiers so there is only a short delay before the changes take effect.
-   Limitations

    -   Overrides cannot define a permission level by row. The current value of a field can only be used in overrides if it is a single-row table.
    -   Validation can be used to catch anything that the permission system doesn't catch.
    -   Here is some more involved information about the problem with per-row permissions in viewons:

        -   In viewons, if a user cannot view some rows, then the number of rows to load cannot be determined before the load, and multiple SQL calls would be needed to ensure a page is filled.
        -   To avoid this complexity, use validation on viewon criteria. So for example, a custumer user might be allowed to see their own orders but not orders from other customers, so the customer criterion's value is checked for the allowed value.

New persiston handling
----------------------

-   Clients should get new persistons from the server using the key formatted like: Customer|=-1 - this allows the server to initialize default values.
-   When initially saving the persiston, the client should diff it against a blank version created client side, so that the diff includes the default values.
-   Upon saving the server returns the newly assigned daton key.

Image and attachment types
--------------------------

-   RetroDRY does not handle images (or any blob attachment) except to give the developer these hints and techniques:

    -   The data dictionary can contain entries to denote image columns.
    -   A column can be defined as computed and you can use the feature to code on the server side the qualified URL of external images based on the value of some database field.
    -   If an image column is included in a client side layout (in view mode only), it can show the image and provide a button so the developer can implement the upload. Upon saving this would send an update to the client which might refresh the image display.
    -   As of June 2020 these features ARE NOT FULLY CODED.

Data types
==========

-   When you design the model, the developer is responsible for ensuring the database model types can be converted to and from the types in the C\# classes. The table shows what the required types are.

official type name and c# data type | SQL types | JSON formatting
----------------------------------- | --------- | ---------------
bool - bool | bit | true or false (JSON-defined constants)
byte - byte (unsigned) | tiny | number
int16 - int16 (signed) | short | number
int32 - int32 (signed) | int | number
int64 - int64 (signed) | bigint | number
double - double | double precision | number
decimal - decimal | decimal | number
string - string | varchar or char | string
date - datetime (date only) | date | string containing YYYYMMDD
datetime - datetime (date and time) | timestamp, strongly recommend storing in UTC | string containing YYYYMMDDHHMM in server timezone (no seconds)
blob - byte[] | blob | base64 encoded string (not suitable for javascript client)

Nulls
-----

-   Each of the official type names can be prefixed with n for nullable. For example: nbool, nbyte, nint16, etc. The corresponding c\# types are nullable (bool?, byte?, etc).
-   In the case of nullable strings and blobs, both nullable and non-nullable versions are still available, but the c\# types are the same. The difference is that for non-nullable strings and blobs, the framework will be careful to write empty values instead of null values when the c\# value is null.
-   The JSON null keyword can be used to indicate a null value. Null never means "don't update this" or "value not known"; it always means the database value is null.

Client side data dictionary naming
----------------------------------

-   The c\# type names are not used inside JSON because it ultimately has to be compatible with other server languages. So the "official" names in the table above are used: bool, byte, int16, datetime, etc.

Types allowed within custom columns
-----------------------------------

-   The entire collection of custom values is stored in a jsonb column in the table.
-   Bool, numerics, and string types are allowed using standard JSON formatting.

    -   If these columns are foreign keys, they behave just like others except the database won't be able to ensure referential integrity.
-   Date and datetime are allowed, stored as strings using RetroDRY JSON formatting.
-   Others are not allowed.

Datetime formatting details
---------------------------

-   RetroDRY doesn't use ISO formats because there are so many pitfalls with unintentional timezone shifting. Date-only fields are considered timeless and must not be shifted (in other words it indicates a date in the abstract, not midnight on that date). Date-time fields are communicated in server timezone in all cases, and the client must handle an offset for the UI display and entry. Javascript should read/write dates in the string format, not assign a Javascript Date object as the field value.
-   Databases should normally be in UTC, as well as the server process, to ensure the database driver doesn't attempt conversions. Non-UTC databases/servers will not be tested or supported.

Wire API Specification
======================

-   All API communications are in JSON format.

Endpoints for use with RetroDRY clients
---------------------------------------

-   The endpoints are designed to be as minimal as possible to encapsulate everything away from the host application. The main endpoint does all the functions that are client-initiated, while the long endpoint implements http long polling to allow the client to respond to server-initiated actions.
-   POST /api/retro/main
-   POST /api/retro/long

Main endpoint behaviors
-----------------------

-   initializing (only if the Initialize member of the request is present)
    -   perform initialization and return data dictionary and permission set

-   get datons (only if the GetDatons member of the request is present)
    -   If known version is provided, this indicates the client already has the persiston. Use this when the client did not subscribe at first but wants to get the latest version now. In this case, if the client already has the latest version, the return value for the persison will be missing, indicating no update.
    -   If daton is requested with subscription, then any diffs will be sent in the future using the long polling endpoint until it is unsubscribed
    -   Returns consensed datons or blank datons with error messages. The return array might be shorter than the request array, so do not assume the array position of the request and response will match.

-   manage datons' status (only if the ManageDatons member of the request is present)
    -   Notes
        -   Lock must be obtained a daton can be saved. This can be immediately before saving, or at the beginning of user editing to ensure user edits are not lost.
        -   Client should call this every 55s to extend lock on every locked persiston, or else the lock will expire. This repetition is not needed to maintain subscriptions.
    -   behaviors
        -   Ends subscription conditonally.
        -   For lock changes, this updates the lock table immediately.
        -   If locking, the version is checked to ensure client has the current version, and fails if not

-   save persistons (only if the SavePersistons member of the request is present)
    -   Saves all persistons (given as diffs) in a transaction; any failure will roll back all; all of them must have been previously locked
    -   Returns an array element for every element provided, or fewer elements on some error conditions. The key will be changed if the persiston was new and the key was database-assigned.
    -   Note that caller needs to unlock/unsubscribe in a separate call.

-   quit (only if DoQuit is true)
    -   clean up session

Long-polling endpoint behaviors
-------------------------------

-   This should be called after initializing and immediately after receiving a response from the server, endlessly.
-   The server will return as soon as there is anything to do, or 30s at the most, possibly with no return values.
-   Subscription updates will return full datons in condensed format.
-   Permission set will be included in the rare case that permissions were changed during the session

Condensed daton wire format
---------------------------

-   This wire format may only be used when both sides of the channel know the data dictionary and the entire daton is needed. Normally this is used only when both sides are RetroDRY, not for compatibility with other callers. The column names are omitted to reduce payload size.
-   JSON specification

    -   Key - the daton key
    -   Version - the daton version code
    -   IsComplete - false if the load is incomplete, else omitted
    -   Content - array of rows of the main table, where each element is an array of values, one for each defined in the data dictionary

        -   The content array includes all the values defined for the table, followed by nested arrays for child table rows.
        -   Any column defined as a ParentKey is completely omitted, since it would be redundant.

-   Data dictionary used in examples:

    -   Persiston "Customer" contains members:

        -   Company (string)
        -   Contact - child table with members:

            -   Name (string)
            -   Primary (bool)

-   JSON example with explanatory comments 

```javascript
{
    "Key": "Customer-123",
    "Version": "hkj34jh2k3j42k4",
    "Content": [ //begin array of main rows
        [ //begin row 0 (the single main customer row)
            "123 Enterprises", //values defined in the customer
            [ //begin array of child contact rows
                [ //begin contact row 0
                    "Alice",
                    true
                ] //end contact row 0
            ]
        ] //end customer row 0
    ] //end array of main rows
}
```

Compatible daton wire format
----------------------------

-   The compatible wire format includes column names. It is used when compatibility is required.
-   In the spec below, words in capitals are replaced by actual names from your data dictionary.
-   Any column defined as a ParentKey is completely omitted, since it would be redundant.
-   JSON specification

    -   Key - the daton key
    -   Version - the daton version code
    -   IsComplete - false if the load is incomplete, else omitted
    -   TABLENAME - array of recods, where the elements in each row are either

        -   named by the column name and contain a value or null
        -   named by the child table name and contain a nested array of rows

-   JSON example (using the same data dictionary as above)

```javascript
{
    "Key": "Customer-123",
    "Version": "hkj34jh2k3j42k4",
    "Customer": [
        {
            "Company": "123 Enterprises",
            "Contact": [
                {
                    "Name": "Alice",
                    "Primary": true
                }
            ]
        }
    ]
}
```

-   JSON examples restated without formatting to show size comparison

    -   condensed format: {"key": "Customer-123","content": \[ \["123 Enterprises",\[ \["Alice",true\]\]\]\]}
    -   compatible format: {"key": "Customer-123","Customer":\[{"Company": "123 Enterprises","Contact": \[{"Name": "Alice","Primary": true}\]}\]}

Diff daton wire format
----------------------

-   The diff format is almost identical to the compatible wire format with these differences:

    -   Unchanged columns or rows are not included.
    -   Nulls may used as values, and this means to set the value to null. It does not mean to ignore the column.
    -   The TABLENAME may have -new or -deleted appended to indicate the mode for the rows included. This is used at all levels of nesting. (For example Customer-new or Customer-deleted)
    -   For deleted rows, only the primary key is included as a value to identify the row. Deletes of any row automatically delete child rows. If the main table's rows are all deleted, then the persiston is completely deleted.
    -   For new rows, the primary key is a temporary client-assigned key as defined elsewhere.
    -   When updated, new, AND deleted rows exist, the JSON string may include all three. For example: { ... "Customer": \[...\], "Customer-new": \[...\], "Customer-deleted": \[...\]
