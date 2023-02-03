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

* Version 1.5.0 contains many breaking changes both client side and server side:
  * The C# code base was updated to use nullable semantics, which changed a few function signatures.
  * The ColDef and TableDef classes have new member names to reflect the difference between SQL column names and in-memory field names.
  * RetroLock and LockManager now assign a new version number at the time a save is committed, rather than when the lock is released.
  * The client must provide a version number when subscribing to a daton.
  * A new feature was added that affects server, API, client and React controls: the CSV export option.
  * WARNING: The number of changes was so large that it likely could have new bugs.

* Version 1.4.3 is the latest stable before major upgrades.

* Version 1.4.1 contains a few breaking changes server side. Most importanly the database resolver is async, which makes many other things async and changes the initialization sequence. It also supports multiple environments (test, production, etc).