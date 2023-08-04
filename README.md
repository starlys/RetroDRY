# RetroDRY
Multi-tier framework in C#/React for handling load/save data, replication across tiers, change subscriptions and locks, and user editing.

## Documentation contents

* [Introduction](documentation/0intro.md)
  * Overview - what it is, why we built it
  * Data theory
  * High level walkthrough
  * Deployment architectures

* [Specification](documentation/1spec.md)
  * Systemwide concerns (keys, timing, cache, locks, casing, permissions...)
  * Data types
  * Wire API specification (endpoints, condensed and expanded daton formats)

* [Guide to Implementing Server-side RetroDRY](documentation/2server.md)
  * Data model
  * Server application plumbing (data model class annotations, REST endpoints, initializing, behavior overrides)

* [Guide to Implementing Client-side RetroDRY](documentation/3client.md)
  * Design guidance
  * Setup
  * Client application plumbing (layouts, daton stack)
  * Reference

* [Documentation for Maintainting RetroDRY Packages](documentation/4retrodoc.md)
  * Preparing workspace
  * Change/rebuild/test cycle (running unit tests, integration tests)
  * Distribution


## Version notes

The product should be considered not market ready quite yet, and we should have used 0.x version numbering. It's being upgraded for use in plug.events so we only focus on that use case for now.

* Version 1.5.2 fixes bugs in 1.5.1
  * Breaking change: The RetroController method for export has to be changed.
  * Breaking change: In SQL overrides, you should override both Load() and LoadForExport() because features that you add in Load() won't be seen in an export context.
  
* Version 1.5.1 fixes bugs in 1.5.0
  * Breaking change: API members ending with *ColumnName are now *FieldName.
  * Breaking change: All List<> members in daton classes must be non-null. For example `public List<CustRow> Cust = new();` This was needed for the nullable types upgrade.
  * Both client libraries are upgraded to full typescript with exported types.

* Version 1.5.0 contains many breaking changes both client side and server side:
  * This version is unusable.
  * The C# code base was updated to use nullable semantics, which changed a few function signatures.
  * The ColDef and TableDef classes have new member names to reflect the difference between SQL column names and in-memory field names.
  * RetroLock and LockManager now assign a new version number at the time a save is committed, rather than when the lock is released.
  * The client must provide a version number when subscribing to a daton.
  * A new feature was added that affects server, API, client and React controls: the CSV export option.

* Version 1.4.3 is the latest stable before major upgrades.

* Version 1.5.1 contains a few breaking changes server side. Most importanly the database resolver is async, which makes many other things async and changes the initialization sequence. It also supports multiple environments (test, production, etc).

* Version 1.5.3 was updated to work with Node version 18, react 18, dotnet 7, and other package updates.