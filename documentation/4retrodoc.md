[docs index](../README.md) > Documentation for Maintainting RetroDRY Packages

Preparing workspace to run RetroDRY sample app from source code
===============================================================

Create sample database
----------------------

-   Development has focused on PostgreSQL, so use that for quickest results.
-   Refer to the server implementation guide for the crete-table script for the RetroLock table.
-   Then run the create table script below to add sample tables that are used by the sample server and client and integration tests.

Create table scripts for sample database
----------------------------------------

```sql
--syntax for postgresql
--NOTE: It is unindexed because it is just for small, demo purposes

drop table SaleItemNote;
drop table SaleItem;
drop table Sale;
drop table ItemVariant;
drop table Item;
drop table Customer;
drop table EmployeeContact;
drop table Employee;
drop table SaleStatus;
drop table PhoneType;
create table PhoneType (

PhoneTypeId smallserial primary key,
TypeOfPhone varchar(20)
);

create table SaleStatus (
StatusId smallserial primary key,
Name varchar(20),
Note varchar(4000)
);

create table Employee (
EmployeeId serial primary key,
FirstName varchar(50) not null,
LastName varchar(50) not null,
SupervisorId int references Employee,
HireDate date
);

create table EmployeeContact (
ContactId serial primary key,
EmployeeId int not null references Employee,
Phone varchar(50),
PhoneType smallint references PhoneType
);

create table Customer (
CustomerId serial primary key,
Company varchar(200) not null,
SalesRepId int not null references Employee,
Notes varchar(4000),
CustomValues jsonb
);

create table Item (
ItemId serial primary key,
ItemCode varchar(20) not null,
Description varchar(200) not null,
CustomValues jsonb
);

create table ItemVariant (
ItemVariantId serial primary key,
ItemId int not null references Item,
VariantCode varchar(20) not null,
Description varchar(200) not null,
CustomValues jsonb
);

create table Sale (
SaleId serial primary key,
CustomerId int not null references Customer,
SaleDate timestamp not null,
ShippedDate timestamp,
Status smallint not null references SaleStatus,
CustomValues jsonb
);

create table SaleItem (
SaleItemId serial primary key,
SaleId int not null references Sale,
ItemId int not null references Item,
ItemVariantId int references ItemVariant,
Quantity int not null,
CustomValues jsonb
);

create table SaleItemNote (
SaleItemNoteId serial primary key,
SaleItemId int not null references SaleItem,
Note varchar(4000)
);

Server side setup
-----------------

-   Clone repository
-   Steps in Visual Studio 2019 or later

    -   Open solution RetroDRY.sln
    -   Set startup project to SampleServer.
    -   Create file SampleServer/appsettings\_dev.json by copying the content of appsettings\_sample.json and modifying it to suit your local database server.
    -   Run server. This opens a window noting that it is listening on port 5001 (https)

Client RetroDRY library setup
-----------------------------

-   Steps in command line to prepare client

    -   Install Node (from node.js)
    -   In terminal, go to RetroDRYClient folder.
    -   Install typescript with npm install -g typescript
    -   Install dependencies with this command: npm install
    -   Possibly usefule diagnostic commands

        -   ensuring Typescript is installed: npm run tsc --version

Viewing demo app
----------------

-   To view the demo app, please go through the steps below up through "React component testing".

Change - rebuild - test cycle
=============================

Running unit tests
------------------

-   Server side unit tests

    -   These are handled through Visual Studio (see Test Explorer window).
-   Client side unit tests

    -   Unit tests are using chai, mocha, and ts-node. Note we are using node's built in assert module: https://nodejs.org/api/assert.html
    -   The general set up follows this advice: https://journal.artfuldev.com/unit-testing-node-applications-with-typescript-using-mocha-and-chai-384ef05f32b2
    -   To run tests in the terminal, go to RetroDRYClient folder and then use this command: npm run utest
    -   To debug tests in VS Code, ensure the launch settings are noticed by VS Code, then select "run unit tests" from the launch dropdown. This technique allows breakpoints to be set.

        -   As a complication noted in launch.json, an evironment variable TS\_NODE\_PROJECT has to be set for this to work.

Running integration tests - Don't skip this step!
-------------------------------------------------

-   The reason to not skip this step is because the folder structure and options for running and debugging are a bit complex, and going through running integration tests helps clear all that up.
-   Steps to launch integration tests:

    -   Ensure the database server is running.
    -   In Visual Studio, Run SampleServer in debug or non-debug mode. It should be listening on ports 5001, 5002, 5003 (simulating three servers)
    -   Ensure terminal is in RetroDRYClient folder.
    -   Use this command to build the client library: npm run buildprod

        -   This creates a single budled js file in folder: dist (and leaves intermediate files in folder: lib)

    -   Use this command to start a local node server and open the test page in your browser: npm run itest

-   Now you should see a simple page served by localhost:8080 (or 127.0.0.1:8080). Things to do here:

    -   Click "Run Samples" then view the browser console to confirm they are working. The sample code in main.js is really a tiny integration test that shows the simplest working code.
    -   Click "Run Quick Test Suite" then view the browser console to confirm they are working. Running either of the integration test suites completely cleans out the database then creates some records, which you can use in manual testing.
    -   Click "Run Slow Test Suite" then view the browser console to confirm they are working. Note that the slow tests can be really slow when the server is running in debug mode; so try in non-debug if it fails.
-   To debug integration tests:

    -   Note that the retroDRY library is built in TypeScript and transpiled with webpack, while the integration test page uses plain javascript only (no npm, no webpack, etc)
    -   Use breakpoints in the browser developer tools window, not in VS Code!
    -   If you need to make changes and don't want to stop and rebuild each time, then you can use the watcher mode from webpack that rebuilds automatically. To do this, open two terminal windows and run these commands, one in each window:

        -   npm run builddev

            -   (This creates the retrodry.js bundle and also watches for changes and rebuilds the bundle every time you save a source file.)
        -   npm run itest

    -   If you make a server side change, you only need to refresh the browser page
    -   If you make a change in the client library, saving rebuilds retrodry.js, so you then need to refresh the browser page.
    -   If you make a change in the client integration tests, you need to refresh the browser page to reload (there is no bundling).

React component testing
-----------------------

-   TBD
-   This step assumes you've built the client side library as noted above.
-   Ensure the terminal is in SampleClient folder, then use commands:

    -   Install dependencies including the local dependency on retrodry: npm i
    -   Run react app and watch for changes: npm start

Distribution
============

TBD

Distribution of the server side C\# assembly
--------------------------------------------

Distribution of the client side retrodry library
------------------------------------------------

Distribution of the client React controls
-----------------------------------------

-   \[will need to copy files from sampleclient to a package for export\]

    -   retrodryreact - a compnent library with no compilation - to be sent to npm as source code only

        -   may have to copy files from inside sampleclient to create the library
        -   note this in doc

-   to prepare for distribution: npx webpack

    -   \[really?\]
-   \[add notes about pushing to npm\]
-   The dist folder is for the final bundled output, which is sent to the npm repository and used in client apps.
