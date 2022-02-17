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

* Version 1.4.1 contains a few breaking changes server side. Most importanly the database resolver is async, which makes many other things async and changes the initialization sequence. It also supports multiple environments (test, production, etc).