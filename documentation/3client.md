[docs index](../README.md) > Guide to Implementing Client-side RetroDRY

Client design guidance
======================

UX flow advice
--------------

-   While RetroDRY can handle search, drilldown, and editing records with very little code, you still need to design the navigation and overall structure around it.
-   Whole-app navigation notes

    -   This framework doesn't stipulate anyting about navigation or windowing, so this section is advisory.
    -   The data source (that is: clients, sales, etc) should be the thing that the user selects. This is a plural noun (reservations, expenses) not a command (book a flight, file report).
    -   For simpler public apps, there are normally very few sources and they can be tabs (often down the left side); each time you go to the tab, you reuse the view of that source, and can only have one active view of each source.
    -   For power user and larger apps, a separate source navigator can create windows or tabs, and then you have multiple views of the same source and they might be listed in the order in which you opened them. This style allows closing each tab or window that the user created.
    -   The selection of a source opens a viewon with capability to find existing items and add new ones of that type. Adding a new one might open a dialog for entry, then add the row to the viewon grid when saved.
    -   The selection of a persiston (either from viewon results or hyperlink from some other context) can open the persison as a readonly view (not a form) in a new tab, or an indented tab under the source tab, or can collapse the viewon and show the persiston detail under the collapsed grid in the same tab.
    -   This app style works for nearly anything - social media and games included. A huge majority of what goes on in computing and the web is filtered searches from databases with occasional updates.

Setup
=====

Referencing client side code 
-----------------------------

Client code is in two parts, both distributed as npm modules:

-   A nonvisual javascript module declares the typescript class library, which contains all the non-visual behavior such as server communications, caching, and validation. This is compatible with any javascript framework.
    -   Install this in your client app as an npm module using command: npm install --save-dev retrodryclient
    -   Alternately download it and include it using html syntax like: &lt;script src="retrodryclient.js"&gt;&lt;/script&gt;
    -   (See SampleClient folder for how this is implemented as a script tag.)

-   A collection of React components implements criteria entry, grids, form building and submission.

    -   Install this in your client app as an npm module using command: npm install --save-dev retrodryreact

As an alternative, if you are starting a new app, you can use the client side template from here: https://github.com/starlys/RetroDRY-ClientTemplate

Initialization
--------------

-   The RetroDRY initialization steps happen after login (if authentication is required).
-   After logging in, your authentication endpoint will return a session key, which you must use to initialize RetroDRY. RetryDRY does not include the authentication step. At a minimum, your server endpoint should perform authentication then return the session key as described in the server side documentation.
-   Example code

```javascript
const serverList = ['app1.example.com', 'app2.example.com']; //hard coded or obtained from a json file
const sessionKey = await myAuthentication.attemptLogin(....); //replace this with your authentication scheme
const ses = new Session();
ses.sessionKey = sessionKey;
ses.serverList = serverList;
ses.timeZoneOffset = -4 * 60; //value from login result or from browser or hard coded - the number of minutes to add to UTC to yield times in this time zone
await ses.start();
```

-   Once the promise returned by the start function is resolved, the client has received the data dictionary and it knows how to fetch, edit and save instances of your data types.

Client application plumbing
===========================

Declaring layouts and layout rendering details
----------------------------------------------

-   The developer may register a card and grid layout for each table where appropriate; if not registered, it will autogenerate. Grid layouts are read only. Card layouts can be used for display or edit.
-   To register, call Session.layouts.registerCard or registerGrid with arguments (datonName, tableName, businessContext, layout).
-   The "business context" is an empty string by default but can be set to something else if your app requires different layouts to be used for different situations. A daton that opens another daton will look for layouts using the calling daton's context.

-   Overall multi-table layout rules, in processing order:
    -   For the main table of a single-main-row datons, the card is always used.
    -   If a grid is explicitly registered, or a card is NOT explicitly registered, then it will display the grid, allowing a row to be selected. When a row is selected, the card for that row shows under the row. Only one card is visible at a time.
    -   If the above test fails, it will show a vertical list of cards.
    -   For any detail table, its grid+card layouts are attached under the card of its parent, which can mean the whole thing is nested within a grid of a grandparent.
    -   If a table has 2 child tables (siblings of each other) then appear in a vertical stack.

-   Typical cases
    -   A viewon listing customers has a single table, so only a grid layout for the customer table is registered.
    -   A persiston has a single main table and 2 levels of child detail tables (Sale, LineItem, Note), so a card layout is registered for the main Sale table, which appears on top; a grid is registered for the immediate detail - LineItem; a card is also registered for the LineItem, which shows the detail row as editable; finally, only a card is registered for the grandchild level of Note so under the LineItem card will be a stack of cards for the notes for that line item.

-   Headings
    -   The first card has a heading band with the prompt for the daton. Subsequent cards are unadorned.
    -   Grids have a heading band with the prompt for the table.
    -   A stack of cards has a heading (appearing detached from the top card) for the table; this only appears if the table has no grid layout registered.
    -   A grid row that has an expanded card under it is highlighted slightly.

-   Heights
    -   The entire daton layout is vertically scrollable only. Widths are responsive.
    -   Cards are natural height, and do not scroll.
    -   Grids are more complex:
        -   Grids are a minimum height of 15em, maximum of 90em and otherwise 1.5 times the hight of the contained card (including all of its visual descendants).
        -   The grid for the main table has an additional minimum height of 100% of the remaining space under the card. Simple one-table viewons will therefore size to the container that the developer put them in without scrolling.

-   Widths
    -   Column defs are used to calculate the tentative widh of each column
        -   strings use 0.8 times the max length in em units, with 8em minimum and 30em maximum
        -   dates, numbers, checkboxes use fixed width
    -   Once the tentative widths are available for an immediate group, the max width of that group determines group width. All members of the group are rendered as 100% of the group box width.
    -   In a card layout, you can also append the width to the column name like "firstName:20" (in em units)

Layout examples
---------------

Here are simple layouts for a grid and a card. Refer to layouts.ts for the detail definition of the class.

````javascript
const customerListGrid = {
    columns: [
        {width: 3, name: 'customerId'},
        {width: 20, name: 'company'},
        {width: 20, name: 'salesRepLastName'}
    ]
};

const customerCard = {
    content: [
        {
            label: 'Identification',
            border: true,
            content: [
                { horizontal: true, content: ['customerId', 'company'] },
                'salesRepId salesRepLastName:30'
            ]
        },
        {
            label: 'Details',
            border: true,
            content: [
                'notes'
            ]
        }
    ]
};
````

Layouts can also inject anything into it - so you can put instructions or other controls in the card. Here is a card showing how you would add a button and define the button's behavior.

````javascript
//define function to return the JSX to inject into the card layout
const resetCustomerNotesButton = (row, edit, layer) => {
    if (!edit) return null; //don't show button if not editing
    const handler = () => {
        //you can change row values here or do pretty much anything!
        row.notes = 'No notes';
        layer.rerender(); //if you change any values in the row, call layer.rerender to make sure it gets updated
    }
    return <button onClick={handler}>Reset Notes</button>;
};

const customerCard = {
    content: [
        'customerId',
        'company',
        'notes',
        resetCustomerNotesButton //your function is called to inject content here
    ]
};

````


Managing the cache
------------------

-   Layout components will automatically get and cache the persistons needed to resolve the display value for "foreign key to row in peristion"-style keys. So when the app first displays a persiston with that kind of foreign key, this causes a subscription to last for the rest of the session.
-   For typical sized apps with under 100 lookup tables, probably no cache management is needed.
-   For larger apps, some management could be helpful for cutting down on memory usage. (As of June 2020 there is no feature yet to do cache management.)

Using the daton stack
---------------------

-   A daton "stack" is a visual collection of daton display or editing controls, which include card and grid layouts. The paradigm makes dialogs unnecessary.
-   In the simplest case the developer provides the stack with the initial daton key and then automatic behavior allows for user navigation and editing starting from that point. For example, the seed could be a viewon of customers, and from there the user can search for customers, add a new one, edit customers, and access other viewons and persistons with hyperlinks.
-   The visual model
    -   The stack shows a vertical group of datons. They are visually separated by a bold header on each one.
    -   Each one can be in edit or view mode. (edit mode is only default for new peristons; otherwise view is default)
    -   Newly opened datons are appended to the bottom of the stack.
    -   The user can close any one of them at any time. If the user closes all of them, the stack will be empty and there will be no further user options available.
    -   When a search is performed from an entry field, the entry form (a persiston or viewon criteria form) depends on the viewon used for search, and that relationship exists as long as both datons are in the stack. When the dependent viewon closes because of selection of a row, that row info populates the entry field, which also regains focus. If either daton is closed, the relationship is severed but the other one can remain open. (This somewhat complex behavior replaces modal dialogs, which have similar issues.)

-   Integrating the stack into an app
    -   This can work with a page oriented site, although transferring the data dictionary and initiating signalR is an overhead that would take place on every page load. Therefore it should be designed when possible on a single-page app.
    -   For "lite" uses the stack should be able to be embedded on a page in virutally any host, even as a wordpress page. An example of embedding this is to place a survey or data lookup functionality on a blog post. 
    -   Best results will be possible if the stack is a top level component that stays mounted on the page regardless of tabbed navigation or any other page features. If you intend to show the stack inside tab pages or dialogs then it will unmount when those things are done, and it will be more complex to track abandoned editing. But you can hide it with CSS and re-show to make it play nicely with other features.
    -   Navigation away from the page should be designed to prevent accidental abandonment of a locked editing session. That means any links should open a new tab, or be connected to javascript to check if navigation is permitted before proceeding.
    -   The width of the stack's container should be limited to some reasonable maximum, but its height should be unlimited.

Displaying cards and grids outside the daton stack
--------------------------------------------------

For cases where you don't want to use the daton stack, you can write code that displays cards in an ad-hoc manner, and then you have to wire up the buttons and events manually. For example, if you are writing a public-facing app and don't want them to have the default functionality of the stack, you might create your custom card layouts and then include a CardView in your React component to edit that card. An example of this is in the sample app PointOfSaleEntry component.

Styling
-------

-   The framework is style-agnostic but it does use a library of controls that has default styles.
-   You will need to include CSS for:
    -   Card and grid layouts and all aspects of the daton stack
    -   Editor controls including overrides for 3rd party controls
    -   As a starting point, copy the relevant section of App.css into your app's css.

Reference
=========

Notes

-   The type "Daton" is used in the object reference to indicate a daton, but in typescript it is actually coded as type "any".

Object reference
----------------

-   functions in window.retrodry:

    -   start(servers: string\[\], sessionKey: string, timezoneoffset: number) -

        -   Starts a new session, returning a Session object that you can make further calls on.

-   data members in Session

    -   dataDictionary - the daton, table and column definitions including prompts and validation. App authors probably won't need this directly unless using custom fields; in that case this can be inspected to reveal which custom fields are present at run time.
    -   onReceiveDaton: (Daton) =&gt; void - declares a function to be called whenever a new daton is received for any reason
    -   onSave: (Daton) =&gt; void - declares a function to be called whenever a daton is successfully saved from this client
    -   onSubscriptionUpdate: (Daton) =&gt; void - declares a function to be called whenever a different session has changed a persiston that this session is subscribed to

-   functions in the prototype of Session

    -   start() - initializes the session 

    -   get(key: string, options?) =&gt; Promise&lt;Daton&gt; - get a daton by key
        -   If you omit options, you may get a refernced to the cached version so be careful not to change it.
        -   Options argument is an object with these members:
            -   doSubscribeEdit - if true, then the client will subscribe to the persiston requested, so that changes from other users will propoage to this client; also this will clone it and allow for edits
            -   forceCheckVersion - if true, the database will be checked for the latest version even if there is a cached version

    -   getMulti(keys: string\[\], options) =&gt; Promise&lt;GetInfo\[\]&gt; - get one or more datons; see documentation for get() for details

    -   createEpmtyViewon(datonType: string) =&gt; any - create an empty viewon withtou loading any rows

    -   getSubscribeState(key: string) =&gt; number - get whether the daton for the given key is subcribed and/or locked, returning values:
        -   0 - not present
        -   1 - subscribed
        -   2 - subscribed and locked for edits

    -   changeSubscribeState(datons: Daton\[\], state: number) =&gt; Promise&lt;any&gt; - change one or more datons to subscribed and/or locked
        -   The state number is documented in getSubscribeState.
        -   The return value is a plain object with error codes indexed by daton key

    -   save(Daton\[\]) =&gt; Promise&lt;SaveInfo&gt; - save one or more persistons in a single database transaction

        -   General notes on saving

            -   As a prerequisite to saving, you must have retrieved the persison using the doSubscribeEdit option. This allows the framework to compare the new version with the old and only send the changed values to the server.
            -   If there are multiple databases in use, the server will perform a distributed transaction and roll back saves of all persistons and all databases on failure.
            -   If you lock the persiston in a separate call before saving, then it will be left locked after the save call. If you did not have it locked before, the save function will lock it, save, then unlock. In cases when the lock is still in place after saving, you must unlock using changeSubscribeState. When unlocked, the server assigns a new version number and propogates the change to other subscribed clients.
            -   The saved persiston instance may not be identical to the version in the database due to newly assigned keys or actions done by database triggers. Therefore if you still need to work with it after saving, you must get it again with the forceCheckVersion option.
        -   The return value (SaveInfo) contains these members:

            -   success: boolean
            -   details: array of SavePersistonResponse object, where each element contains these members:

                -   oldKey: string
                -   newKey: string - either the same as oldKey or if the database assigned a primary key when saving a new persiston, then this gives you the new daton key (The whole key like Customer|=123, not just the primary key value.)
                -   errors: string\[\]
                -   isSuccess: boolean
                -   isDeleted: boolean - true if the whole persison was deleted

    -   quit() =&gt; Promise - ends the session

        -   Note that if you fail to call quit, the client will continue long polling and could keep locks active forever, preventing other users from editing those locked persistons.
    -   getDatonDef(name) - get the DatonDef instance for the given type name

-   functions in the prototype of LayoutCollection
    -   registerCard(datonName, tableName, businessContext, layout) - registers the default layout for all daton stacks for the daton/table
    -   registerGrid(datonName, tableName, businessContext, layout) - registers the default layout for all daton stacks for the daton/table
    -   getCard(datonName, tableName, businessContext) - get the registered or autogenerated card layout
    -   getGrid(datonName, tableName, businessContext) - get the registered or autogenerated grid layout

-   members of DatonStackState
    -   onCustomLookup - allows your app to define how the lookup button works by setting this to a function; in particular you can auto-search on more specific things than can be customized using server-side annotations. Example:

````javascript
//sample custom lookup behavior: changes the default way to look up customers to one where it pre-searches on customers beginning with 'customer 1'
mystackstate.onCustomLookup = (editingLayer, editingTableDef, editingRow, editingColDef) => {
    if (editingColDef.name === 'customerId') return 'CustomerList|Company=Customer 1';
};
````
    -   onLayerSaved - allows your app to do something after a persiston is saved. Function accepts key of saved persiston. Example:

````javascript
//change save behavior: after saving, remove daton from stack
mystackstate.onLayerSaved = datonKey => mystackstate.removeByKey(datonKey, true);
````

-   functions in the prototype of DatonStackState
    -   add(key, edit, businessContext) =&gt; Promise - show a daton on the stack by its key; businessContext is optional
    -   addEmptyViewon(datonType, businessContext) - show an empty viewon with no rows that is ready for user search; businessContext is optional
    -   removeByKey(key, doUnsubscribe) - remove a layer by its daton key and optionally unsubscribe
    -   replaceKey(oldKey, newKey, forceLoad) =&gt; Promise - replace a viewon key, which performs a search on the new criteria; or replace a new persiston key with the actual key