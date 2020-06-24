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
